using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class V2MatchBoardManager : MonoBehaviour
{
    [Header("References")]
    public V2LevelData levelData;
    [Header("Level Queue (Optional)")]
    public V2LevelData[] levelQueue;
    public int initialLevelQueueIndex = 0;

    public RectTransform boardRoot;
    public GameObject tilePrefab;
    public GameObject slotPrefab; // Arka plan slot prefab'ı
    public V2KingdomHUD hud;
    public V2BossEnemy bossEnemy;
    public Transform bossTarget;
    public GameObject attackProjectilePrefab;
    public V2AudioManager audioManager;

    [Header("Board Layout")]
    public float cellSize = 104f;
    [Tooltip("false: ızgaranın geometrik merkezi root'a oturur (8x8 için önerilen)")]
    public bool alignToCenterCell = false;

    [Header("Tile Library")]
    public Sprite[] tileSprites;
    public Color[] tileColors;
    [Tooltip("Kapalıysa sprite'lar beyaz tint ile görünür; renk listesi sadece ID kaynağı olur.")]
    public bool tintSpritesWithTileColors = false;
    public Color defaultTileTint = Color.white;

    [Header("Special Tile Sprites (Optional)")]
    public Sprite bombSpecialSprite;
    public Sprite discoSpecialSprite;
    public Sprite rocketHorizontalSpecialSprite;
    public Sprite rocketVerticalSpecialSprite;

    [Header("Special Tile Random Spawn")]
    [Range(0f, 1f)] public float randomBombSpawnChance = 0.015f;
    [Range(0f, 1f)] public float randomRocketSpawnChance = 0.02f;
    [Range(0f, 1f)] public float randomDiscoSpawnChance = 0.01f;
    public bool allowRandomSpecialOnInitialFill = true;
    public bool allowRandomSpecialOnRefill = true;

    [Header("Animation")]
    public float fallStepDelay = 0.03f;
    public float fallAnimationDuration = 0.5f;
    public float swapDuration = 0.12f;
    public float invalidShakeDuration = 0.12f;
    public float invalidShakeOffset = 10f;

    [Header("Debug")]
    public bool verboseLogs = true;

    [Header("Tile Pop Animation")]
    public bool playTilePopOnSpawn = true;
    public float tilePopDuration = 0.22f;
    public float tilePopMinScale = 0.86f;
    public float tilePopOvershootScale = 1.08f;
    public int tilePopWobbleCount = 2;
    [Tooltip("İlk board kurulurken her tile için eklenecek gecikme (sn).")]
    public float initialPopStaggerStep = 0.01f;
    [Tooltip("Refill sırasında her tile için eklenecek gecikme (sn).")]
    public float refillPopStaggerStep = 0.005f;

    [Header("Combat / Boss")]
    [Tooltip("Temizlenen her tile için boss'a verilecek hasar.")]
    public int damagePerClearedTile = 1;
    [Tooltip("Bir eşleşmede spawn edilecek maksimum mermi sayısı.")]
    public int maxProjectilesPerBurst = 8;
    public float attackTravelDuration = 0.22f;

    [Header("Special Tile FX")]
    public GameObject bombExplosionPrefab;
    public GameObject lineClearFxPrefab;
    [Tooltip("Rocket line clear için hareket eden roket prefabı. Boşsa lineClearFxPrefab fallback olur.")]
    public GameObject rocketTravelFxPrefab;
    [Tooltip("Roketin hücre boyunca akış hızı (hücre/sn). Yüksek tut: 35+.")]
    public float rocketTravelCellsPerSecond = 40f;
    [Tooltip("Prefab 0° yönünde yukarı bakmıyorsa buradan düzeltme ver. (örn: sağa bakan prefab için +90)")]
    public float rocketRotationOffset = 0f;
    public float specialFxLifetime = 1.2f;
    public float bombChargeDuration = 0.85f;
    public float bombHoldDuration = 0.45f;
    public float bombLiftScale = 1.18f;
    public float bombShakeAmount = 10f;
    public GameObject normalTileClearFxPrefab;


    private V2Tile[,] grid;
    private int score;
    private int movesLeft;
    private bool busy;
    private bool levelEnded;

    private int initialSpawnSequence;
    private int refillSpawnSequence;
    private int currentLevelQueueIndex;

    private struct FallAnimationItem
    {
        public RectTransform rt;
        public Vector2 from;
        public Vector2 to;
    }

    // Public getter'lar - Input controller için
    public int Rows => levelData != null ? levelData.rows : 8;
    public int Cols => levelData != null ? levelData.cols : 8;
    public float CellSize => cellSize;
    public bool IsInputLocked => busy || levelEnded;

    private void Awake()
    {
        if (levelQueue != null && levelQueue.Length > 0)
        {
            currentLevelQueueIndex = Mathf.Clamp(initialLevelQueueIndex, 0, levelQueue.Length - 1);
            if (levelQueue[currentLevelQueueIndex] != null)
                levelData = levelQueue[currentLevelQueueIndex];
        }
    }

    private void Start()
    {
        StartLevel();
    }

    private void OnEnable()
    {
        if (bossEnemy != null)
            bossEnemy.OnBossDied += HandleBossDied;
    }

    private void OnDisable()
    {
        if (bossEnemy != null)
            bossEnemy.OnBossDied -= HandleBossDied;
    }

    public void StartLevel()
    {
        if (levelData == null || boardRoot == null || tilePrefab == null)
        {
            Debug.LogError("V2MatchBoardManager: Missing references.");
            return;
        }

        score = 0;
        movesLeft = Mathf.Max(1, levelData.moveLimit);
        busy = false;
        levelEnded = false;

        if (bossEnemy != null)
            bossEnemy.ResetBoss();

        EnsureAudioManager();
        if (audioManager != null)
            audioManager.PlayGameplayMusic();

        BuildBoard();
        RefreshHUD();
    }

    private void BuildBoard()
    {
        ClearBoard();

        int rows = Mathf.Max(1, levelData.rows);
        int cols = Mathf.Max(1, levelData.cols);

        grid = new V2Tile[rows, cols];
        initialSpawnSequence = 0;

        PrepareRootTransform();
        Vector2 anchor = GetAnchor(rows, cols);

        // Önce arka plan slotlarını spawn et
        SpawnSlots(rows, cols, anchor);

        // Sonra tile'ları spawn et
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (!IsCellPlayable(r, c))
                    continue;

                V2Tile tile = SpawnTile(r, c, RandomTileId(), anchor, spawnAboveTop: false, isRefill: false);
                grid[r, c] = tile;
            }
        }

        StartCoroutine(RemoveInitialMatches());
    }

    private void SpawnSlots(int rows, int cols, Vector2 anchor)
    {
        if (slotPrefab == null) return;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (!IsCellPlayable(r, c))
                    continue;

                GameObject slot = Instantiate(slotPrefab, boardRoot);
                slot.name = $"slot_{r}_{c}";

                RectTransform rt = slot.GetComponent<RectTransform>();
                if (rt == null) rt = slot.AddComponent<RectTransform>();

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);

                float x = (c - anchor.x) * cellSize;
                float y = (anchor.y - r) * cellSize;
                rt.anchoredPosition = new Vector2(x, y);

                // Slotları en arkaya at
                rt.SetAsFirstSibling();
            }
        }
    }

    private void PrepareRootTransform()
    {
        boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        boardRoot.pivot = new Vector2(0.5f, 0.5f);
        boardRoot.anchoredPosition = Vector2.zero;
    }

    private Vector2 GetAnchor(int rows, int cols)
    {
        if (alignToCenterCell)
            return new Vector2(GetCenterCell(cols), GetCenterCell(rows));

        return new Vector2((cols - 1) * 0.5f, (rows - 1) * 0.5f);
    }

    private int GetCenterCell(int count)
    {
        return count / 2;
    }

    private IEnumerator RemoveInitialMatches()
    {
        yield return null;
        while (true)
        {
            HashSet<Vector2Int> matches = FindAllMatches(out _);
            if (matches.Count == 0) break;

            yield return StartCoroutine(ClearCollapseRefill(matches, new Dictionary<Vector2Int, V2SpecialType>(), giveScore: false, allowSpecialSpawn: false));
        }
    }

    public void TrySwap(Vector2Int a, Vector2Int b)
    {
        if (busy || levelEnded)
        {
            Debug.Log("[V2Board] Busy/ended, swap rejected");
            return;
        }
        if (movesLeft <= 0)
        {
            Debug.Log("[V2Board] No moves left");
            return;
        }
        if (!IsInside(a) || !IsInside(b))
        {
            Debug.Log($"[V2Board] Outside grid: a={a}, b={b}");
            StartCoroutine(ShakeTileAt(a));
            return;
        }
        if (!AreNeighbors(a, b))
        {
            Debug.Log($"[V2Board] Not neighbors: a={a}, b={b}");
            StartCoroutine(ShakeTileAt(a));
            return;
        }

        Debug.Log($"[V2Board] Starting swap: {a} <-> {b}");
        PlaySwapSfx();
        StartCoroutine(SwapResolve(a, b));
    }

    private IEnumerator SwapResolve(Vector2Int a, Vector2Int b)
    {
        busy = true;

        yield return StartCoroutine(AnimateSwapCells(a, b));

        bool usedSpecialSwap = IsSwapSpecial(grid[a.x, a.y]) || IsSwapSpecial(grid[b.x, b.y]);

        if (usedSpecialSwap)
        {
            movesLeft--;
            yield return StartCoroutine(ResolveSpecialSwap(a, b));

            HashSet<Vector2Int> cascadeMatches = FindAllMatches(out Dictionary<Vector2Int, V2SpecialType> cascadeSpawns);
            while (cascadeMatches.Count > 0)
            {
                yield return StartCoroutine(ClearCollapseRefill(cascadeMatches, cascadeSpawns, giveScore: true, allowSpecialSpawn: true));
                if (levelEnded)
                {
                    busy = false;
                    yield break;
                }

                cascadeMatches = FindAllMatches(out cascadeSpawns);
            }

            if (!levelEnded && movesLeft <= 0)
                HandleOutOfMoves();

            RefreshHUD();
            busy = false;
            yield break;
        }

        HashSet<Vector2Int> matches = FindAllMatches(out Dictionary<Vector2Int, V2SpecialType> specialSpawns);
        if (matches.Count == 0)
        {
            yield return StartCoroutine(AnimateSwapCells(b, a));
            yield return StartCoroutine(ShakeTileAt(a));
            busy = false;
            yield break;
        }

        movesLeft--;

        while (matches.Count > 0)
        {
            yield return StartCoroutine(ClearCollapseRefill(matches, specialSpawns, giveScore: true, allowSpecialSpawn: true));
            if (levelEnded)
            {
                busy = false;
                yield break;
            }

            matches = FindAllMatches(out specialSpawns);
        }

        if (!levelEnded && movesLeft <= 0)
            HandleOutOfMoves();

        RefreshHUD();
        busy = false;
    }

    private bool IsSwapSpecial(V2Tile tile)
    {
        if (tile == null)
            return false;

        return tile.specialType != V2SpecialType.None;
    }

    private IEnumerator ResolveSpecialSwap(Vector2Int a, Vector2Int b)
    {
        HashSet<Vector2Int> cellsToClear = new HashSet<Vector2Int>();
        HashSet<Vector2Int> delayedBombFxCells = new HashSet<Vector2Int>();
        List<IEnumerator> anims = new List<IEnumerator>();

        CollectSpecialSwapTargets(a, cellsToClear, delayedBombFxCells, anims);
        CollectSpecialSwapTargets(b, cellsToClear, delayedBombFxCells, anims);

        foreach (IEnumerator anim in anims)
            yield return StartCoroutine(anim);

        if (delayedBombFxCells.Count > 0)
            SpawnBombAreaFx(delayedBombFxCells);

        int clearedCount = ClearCellsDirect(cellsToClear);
        if (clearedCount > 0)
        {
            score += clearedCount * 20;
            TriggerBossAttack(clearedCount);
        }

        yield return null;
        List<FallAnimationItem> fallItems = new List<FallAnimationItem>();
        CollapseColumns(fallItems);
        RefillFromTop(fallItems);
        yield return StartCoroutine(AnimateFallItems(fallItems));
        yield return new WaitForSeconds(fallStepDelay);
        RefreshHUD();
    }

    private void CollectSpecialSwapTargets(Vector2Int origin, HashSet<Vector2Int> cellsToClear, HashSet<Vector2Int> delayedBombFxCells, List<IEnumerator> anims)
    {
        if (!IsInside(origin))
            return;

        V2Tile tile = grid[origin.x, origin.y];
        if (tile == null)
            return;

        if (tile.specialType == V2SpecialType.Bomb)
        {
            anims.Add(AnimateBombDetonation(tile));
            AddBombArea4x4(origin, cellsToClear);
            AddBombArea4x4(origin, delayedBombFxCells);
            return;
        }

        if (tile.specialType == V2SpecialType.Disco)
        {
            AddCrossArea(origin, cellsToClear);
            SpawnLineClearFx(origin, V2SpecialType.Disco);
            return;
        }

        if (tile.specialType == V2SpecialType.RocketHorizontal || tile.specialType == V2SpecialType.RocketVertical)
        {
            AddCrossArea(origin, cellsToClear);
            PlayRocketCrossSweep(origin);
            return;
        }
    }

    private int ClearCellsDirect(HashSet<Vector2Int> cells)
    {
        int cleared = 0;
        foreach (Vector2Int p in cells)
        {
            if (!IsInside(p))
                continue;

            V2Tile tile = grid[p.x, p.y];
            if (tile == null)
                continue;

            SpawnFxAtWorldPosition(normalTileClearFxPrefab, tile.transform.position, specialFxLifetime * 0.8f);
            Destroy(tile.gameObject);
            grid[p.x, p.y] = null;
            cleared++;
        }

        if (cleared > 0)
            PlayClearSfx();

        if (verboseLogs)
            Debug.Log($"V2 special cleared={cleared}");

        return cleared;
    }

    private void AddBombArea4x4(Vector2Int center, HashSet<Vector2Int> output)
    {
        for (int dr = -1; dr <= 2; dr++)
        {
            for (int dc = -1; dc <= 2; dc++)
            {
                Vector2Int p = new Vector2Int(center.x + dr, center.y + dc);
                if (IsInside(p))
                    output.Add(p);
            }
        }
    }

    private void AddCrossArea(Vector2Int center, HashSet<Vector2Int> output)
    {
        for (int c = 0; c < grid.GetLength(1); c++)
            output.Add(new Vector2Int(center.x, c));

        for (int r = 0; r < grid.GetLength(0); r++)
            output.Add(new Vector2Int(r, center.y));
    }

    private void AddHorizontalArea(Vector2Int center, HashSet<Vector2Int> output)
    {
        for (int c = 0; c < grid.GetLength(1); c++)
            output.Add(new Vector2Int(center.x, c));
    }

    private void AddVerticalArea(Vector2Int center, HashSet<Vector2Int> output)
    {
        for (int r = 0; r < grid.GetLength(0); r++)
            output.Add(new Vector2Int(r, center.y));
    }

    private IEnumerator AnimateBombDetonation(V2Tile tile)
    {
        if (tile == null)
            yield break;

        RectTransform rt = tile.GetComponent<RectTransform>();
        if (rt == null)
            yield break;

        Vector3 baseScale = rt.localScale;
        Vector2 basePos = rt.anchoredPosition;
        float charge = Mathf.Max(0.05f, bombChargeDuration);
        float hold = Mathf.Max(0f, bombHoldDuration);
        float shake = Mathf.Max(0f, bombShakeAmount);
        float t = 0f;

        while (t < charge)
        {
            if (rt == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / charge);
            float s = Mathf.Lerp(1f, bombLiftScale, k);
            rt.localScale = baseScale * s;
            yield return null;
        }

        t = 0f;
        while (t < hold)
        {
            if (rt == null)
                yield break;

            t += Time.deltaTime;
            float wave = Mathf.Sin(t * 45f) * shake;
            rt.anchoredPosition = basePos + new Vector2(wave, 0f);
            yield return null;
        }

        if (rt != null)
            rt.anchoredPosition = basePos;

        if (tile != null)
            SpawnFxAtWorldPosition(bombExplosionPrefab, tile.transform.position, specialFxLifetime);
    }

    private void SpawnBombAreaFx(HashSet<Vector2Int> cells)
    {
        if (bombExplosionPrefab == null || cells == null)
            return;

        PlaySpecialTypeSfx(V2SpecialType.Bomb);

        foreach (Vector2Int cell in cells)
            SpawnFxAtCell(cell, bombExplosionPrefab, specialFxLifetime * 0.8f);
    }

    private void SpawnLineClearFx(Vector2Int center, V2SpecialType specialType)
    {
        if (lineClearFxPrefab == null)
            return;

        PlaySpecialTypeSfx(specialType);

        for (int c = 0; c < grid.GetLength(1); c++)
            SpawnFxAtCell(new Vector2Int(center.x, c), lineClearFxPrefab, specialFxLifetime * 0.8f);

        for (int r = 0; r < grid.GetLength(0); r++)
            SpawnFxAtCell(new Vector2Int(r, center.y), lineClearFxPrefab, specialFxLifetime * 0.8f);
    }

    private void PlayRocketCrossSweep(Vector2Int center)
    {
        PlaySpecialTypeSfx(V2SpecialType.RocketHorizontal);
        PlayRocketLineSweep(center, true);
        PlayRocketLineSweep(center, false);
    }

    private void PlayRocketLineSweep(Vector2Int center, bool horizontal)
    {
        if (horizontal)
        {
            StartCoroutine(AnimateRocketSweep(center, new Vector2Int(0, -1)));
            StartCoroutine(AnimateRocketSweep(center, new Vector2Int(0, 1)));
        }
        else
        {
            StartCoroutine(AnimateRocketSweep(center, new Vector2Int(-1, 0)));
            StartCoroutine(AnimateRocketSweep(center, new Vector2Int(1, 0)));
        }
    }

    private float GetRocketRotationZ(Vector2Int step)
    {
        // 0° = yukarı varsayımı
        if (step.x < 0) return 0f + rocketRotationOffset;      // up
        if (step.x > 0) return 180f + rocketRotationOffset;    // down
        if (step.y < 0) return 90f + rocketRotationOffset;     // left
        return 270f + rocketRotationOffset;                    // right
    }

    private IEnumerator AnimateRocketSweep(Vector2Int startCell, Vector2Int step)
    {
        GameObject rocketPrefab = rocketTravelFxPrefab != null ? rocketTravelFxPrefab : lineClearFxPrefab;
        if (rocketPrefab == null)
            yield break;

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int cell = startCell;
        while (IsInside(cell) && IsCellPlayable(cell.x, cell.y))
        {
            path.Add(cell);
            cell += step;
        }

        if (path.Count == 0)
            yield break;

        Transform parent = boardRoot != null ? (boardRoot.parent != null ? boardRoot.parent : boardRoot) : null;
        GameObject rocket = parent != null ? Instantiate(rocketPrefab, parent) : Instantiate(rocketPrefab);

        Transform rocketTr = rocket.transform;
        rocketTr.rotation = Quaternion.Euler(0f, 0f, GetRocketRotationZ(step));

        float speed = Mathf.Max(1f, rocketTravelCellsPerSecond);
        float cellDuration = 1f / speed;

        Vector3 from = GetCellWorldPosition(path[0]);
        rocketTr.position = from;

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 to = GetCellWorldPosition(path[i]);
            float t = 0f;
            while (t < cellDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / cellDuration);
                rocketTr.position = Vector3.Lerp(from, to, k);
                yield return null;
            }

            from = to;
            rocketTr.position = from;
        }

        Destroy(rocket, specialFxLifetime);
    }

    private Vector3 GetCellWorldPosition(Vector2Int cell)
    {
        if (IsInside(cell))
        {
            V2Tile tile = grid[cell.x, cell.y];
            if (tile != null)
                return tile.transform.position;
        }

        Vector2 anchored = GetCellAnchoredPosition(cell.x, cell.y);
        return boardRoot.TransformPoint(anchored);
    }

    private void SpawnFxAtCell(Vector2Int cell, GameObject prefab, float lifetime)
    {
        if (!IsInside(cell))
            return;

        V2Tile tile = grid[cell.x, cell.y];
        if (tile != null)
        {
            SpawnFxAtWorldPosition(prefab, tile.transform.position, lifetime);
            return;
        }

        Vector2 anchored = GetCellAnchoredPosition(cell.x, cell.y);
        Vector3 world = boardRoot.TransformPoint(anchored);
        SpawnFxAtWorldPosition(prefab, world, lifetime);
    }

    private void SpawnFxAtWorldPosition(GameObject prefab, Vector3 worldPos, float lifetime)
    {
        if (prefab == null)
            return;

        Transform parent = boardRoot != null ? (boardRoot.parent != null ? boardRoot.parent : boardRoot) : null;
        GameObject fx = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
        fx.transform.position = worldPos;

        if (lifetime > 0f)
            Destroy(fx, lifetime);
    }

    private IEnumerator ClearCollapseRefill(HashSet<Vector2Int> matches, Dictionary<Vector2Int, V2SpecialType> specialSpawns, bool giveScore, bool allowSpecialSpawn)
    {
        int clearedCount = ClearMatches(matches, specialSpawns, allowSpecialSpawn);
        if (giveScore)
        {
            score += clearedCount * 20;
            TriggerBossAttack(clearedCount);
        }

        yield return null;

        List<FallAnimationItem> fallItems = new List<FallAnimationItem>();
        CollapseColumns(fallItems);
        RefillFromTop(fallItems);
        yield return StartCoroutine(AnimateFallItems(fallItems));
        yield return new WaitForSeconds(fallStepDelay);

        RefreshHUD();
    }

    private int ClearMatches(HashSet<Vector2Int> matches, Dictionary<Vector2Int, V2SpecialType> specialSpawns, bool allowSpecialSpawn)
    {
        int cleared = 0;
        HashSet<Vector2Int> expanded = ExpandMatchesWithSpecials(matches);

        foreach (Vector2Int p in expanded)
        {
            V2Tile tile = grid[p.x, p.y];
            if (tile == null) continue;

            if (allowSpecialSpawn && specialSpawns != null && specialSpawns.TryGetValue(p, out V2SpecialType spawnType) && spawnType != V2SpecialType.None)
            {
                tile.SetSpecial(spawnType);
                ApplyTileVisual(tile);
                continue;
            }

            SpawnFxAtWorldPosition(normalTileClearFxPrefab, tile.transform.position, specialFxLifetime * 0.8f);
            Destroy(tile.gameObject);
            grid[p.x, p.y] = null;
            cleared++;
        }

        if (cleared > 0)
            PlayClearSfx();

        if (verboseLogs) Debug.Log($"V2 cleared={cleared}");
        return cleared;
    }

    private void CollapseColumns(List<FallAnimationItem> fallItems)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        for (int c = 0; c < cols; c++)
        {
            List<int> playableRowsDesc = new List<int>();
            for (int r = rows - 1; r >= 0; r--)
            {
                if (IsCellPlayable(r, c))
                    playableRowsDesc.Add(r);
            }

            int writePtr = 0;
            for (int i = 0; i < playableRowsDesc.Count; i++)
            {
                int r = playableRowsDesc[i];
                V2Tile tile = grid[r, c];
                if (tile == null)
                    continue;

                int targetRow = playableRowsDesc[writePtr];
                if (targetRow != r)
                {
                    grid[targetRow, c] = tile;
                    grid[r, c] = null;

                    tile.row = targetRow;
                    tile.col = c;

                    RectTransform rt = tile.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        fallItems.Add(new FallAnimationItem
                        {
                            rt = rt,
                            from = rt.anchoredPosition,
                            to = GetCellAnchoredPosition(targetRow, c)
                        });
                    }
                }

                writePtr++;
            }
        }
    }

    private void RefillFromTop(List<FallAnimationItem> fallItems)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        Vector2 anchor = GetAnchor(rows, cols);
        refillSpawnSequence = 0;

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (!IsCellPlayable(r, c))
                    continue;

                if (grid[r, c] != null) continue;

                V2Tile tile = SpawnTile(r, c, RandomTileId(), anchor, spawnAboveTop: true, isRefill: true);
                grid[r, c] = tile;

                RectTransform rt = tile != null ? tile.GetComponent<RectTransform>() : null;
                if (rt != null)
                {
                    fallItems.Add(new FallAnimationItem
                    {
                        rt = rt,
                        from = rt.anchoredPosition,
                        to = GetCellAnchoredPosition(r, c)
                    });
                }
            }
        }
    }

    private IEnumerator AnimateFallItems(List<FallAnimationItem> fallItems)
    {
        if (fallItems == null || fallItems.Count == 0)
            yield break;

        float duration = Mathf.Max(0.05f, fallAnimationDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);

            for (int i = 0; i < fallItems.Count; i++)
            {
                RectTransform rt = fallItems[i].rt;
                if (rt == null)
                    continue;

                rt.anchoredPosition = Vector2.LerpUnclamped(fallItems[i].from, fallItems[i].to, eased);
            }

            yield return null;
        }

        for (int i = 0; i < fallItems.Count; i++)
        {
            RectTransform rt = fallItems[i].rt;
            if (rt != null)
                rt.anchoredPosition = fallItems[i].to;
        }
    }

    public Vector2 GetBoardAnchor()
    {
        int rows = Mathf.Max(1, Rows);
        int cols = Mathf.Max(1, Cols);
        return GetAnchor(rows, cols);
    }


    public bool TryGetClosestCellFromScreen(Vector2 screenPos, Camera cam, out Vector2Int cell)
    {
        cell = default;
        if (grid == null) return false;

        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        float bestDist = float.MaxValue;
        bool found = false;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                V2Tile tile = grid[r, c];
                if (tile == null) continue;

                RectTransform rt = tile.GetComponent<RectTransform>();
                if (rt == null) continue;

                Vector2 tileScreen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
                float d = (tileScreen - screenPos).sqrMagnitude;

                if (d < bestDist)
                {
                    bestDist = d;
                    cell = new Vector2Int(r, c);
                    found = true;
                }
            }
        }

        return found;
    }

    private V2Tile SpawnTile(int r, int c, int id, Vector2 anchor, bool spawnAboveTop, bool isRefill)
    {
        GameObject go = Instantiate(tilePrefab, boardRoot);
        go.name = $"tile_{r}_{c}";

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cellSize, cellSize);

        V2Tile tile = go.GetComponent<V2Tile>();
        if (tile == null) tile = go.AddComponent<V2Tile>();

        tile.SetData(r, c, id);
        tile.SetSpecial(RollSpawnSpecialType(isRefill));
        ApplyTileVisual(tile);

        SetTilePosition(tile, r, c, anchor, spawnAboveTop);

        if (playTilePopOnSpawn)
        {
            float step = isRefill ? Mathf.Max(0f, refillPopStaggerStep) : Mathf.Max(0f, initialPopStaggerStep);
            int seq = isRefill ? refillSpawnSequence++ : initialSpawnSequence++;
            float delay = step * seq;
            StartCoroutine(AnimateTilePop(rt, delay));
        }
        else
        {
            rt.localScale = Vector3.one;
        }

        return tile;
    }

    private void SetTilePosition(V2Tile tile, int r, int c, Vector2 anchor, bool spawnAboveTop)
    {
        RectTransform rt = tile.GetComponent<RectTransform>();
        if (rt == null) return;

        float x = (c - anchor.x) * cellSize;
        float y = (anchor.y - r) * cellSize;

        if (spawnAboveTop)
            y = (anchor.y + 1f) * cellSize;

        rt.anchoredPosition = new Vector2(x, y);
    }

    private int RandomTileId()
    {
        int librarySize = Mathf.Max(tileSprites != null ? tileSprites.Length : 0, tileColors != null ? tileColors.Length : 0);
        int count = Mathf.Clamp(levelData.colorCount, 3, Mathf.Max(3, librarySize));
        return Random.Range(0, count);
    }

    private Sprite GetSpriteForId(int id)
    {
        if (tileSprites == null || tileSprites.Length == 0) return null;
        return tileSprites[Mathf.Clamp(id, 0, tileSprites.Length - 1)];
    }

    private Color GetColorForId(int id)
    {
        Color c = defaultTileTint;

        if (tintSpritesWithTileColors && tileColors != null && tileColors.Length > 0)
            c = tileColors[Mathf.Clamp(id, 0, tileColors.Length - 1)];

        if (c.a <= 0.001f)
            c.a = 1f;

        return c;
    }

    private V2SpecialType RollSpawnSpecialType(bool isRefill)
    {
        bool allow = isRefill ? allowRandomSpecialOnRefill : allowRandomSpecialOnInitialFill;
        if (!allow)
            return V2SpecialType.None;

        float bombChance = Mathf.Clamp01(randomBombSpawnChance);
        float rocketChance = Mathf.Clamp01(randomRocketSpawnChance);
        float discoChance = Mathf.Clamp01(randomDiscoSpawnChance);
        float roll = Random.value;

        if (roll < bombChance)
            return V2SpecialType.Bomb;

        if (roll < bombChance + rocketChance)
            return V2SpecialType.RocketHorizontal;

        if (roll < bombChance + rocketChance + discoChance)
            return V2SpecialType.Disco;

        return V2SpecialType.None;
    }

    private IEnumerator AnimateSwapCells(Vector2Int a, Vector2Int b)
    {
        V2Tile ta = grid[a.x, a.y];
        V2Tile tb = grid[b.x, b.y];

        if (ta == null || tb == null)
        {
            Debug.LogError($"[V2Board] AnimateSwapCells null tile! ta={ta}, tb={tb}");
            yield break;
        }

        grid[a.x, a.y] = tb;
        grid[b.x, b.y] = ta;

        (ta.row, ta.col) = (b.x, b.y);
        (tb.row, tb.col) = (a.x, a.y);

        Vector2 targetA = GetCellAnchoredPosition(b.x, b.y);
        Vector2 targetB = GetCellAnchoredPosition(a.x, a.y);

        RectTransform rtA = ta.GetComponent<RectTransform>();
        RectTransform rtB = tb.GetComponent<RectTransform>();

        if (rtA == null || rtB == null)
            yield break;

        Vector2 startA = rtA.anchoredPosition;
        Vector2 startB = rtB.anchoredPosition;

        float t = 0f;
        float duration = Mathf.Max(0.01f, swapDuration);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            rtA.anchoredPosition = Vector2.Lerp(startA, targetA, k);
            rtB.anchoredPosition = Vector2.Lerp(startB, targetB, k);
            yield return null;
        }

        rtA.anchoredPosition = targetA;
        rtB.anchoredPosition = targetB;
    }

    private IEnumerator ShakeTileAt(Vector2Int p)
    {
        if (!IsInside(p))
            yield break;

        V2Tile tile = grid[p.x, p.y];
        if (tile == null)
            yield break;

        RectTransform rt = tile.GetComponent<RectTransform>();
        if (rt == null)
            yield break;

        Vector2 basePos = rt.anchoredPosition;
        float duration = Mathf.Max(0.05f, invalidShakeDuration);
        float offset = Mathf.Max(2f, invalidShakeOffset);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float wave = Mathf.Sin((t / duration) * Mathf.PI * 6f);
            rt.anchoredPosition = basePos + new Vector2(wave * offset, 0f);
            yield return null;
        }

        rt.anchoredPosition = basePos;
    }

    private Vector2 GetCellAnchoredPosition(int r, int c)
    {
        Vector2 anchor = GetAnchor(grid.GetLength(0), grid.GetLength(1));
        float x = (c - anchor.x) * cellSize;
        float y = (anchor.y - r) * cellSize;
        return new Vector2(x, y);
    }

    private HashSet<Vector2Int> FindAllMatches(out Dictionary<Vector2Int, V2SpecialType> specialSpawns)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();
        specialSpawns = new Dictionary<Vector2Int, V2SpecialType>();

        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        // Yatay kontrol
        for (int r = 0; r < rows; r++)
        {
            int streak = 1;
            for (int c = 1; c < cols; c++)
            {
                if (grid[r, c] == null || grid[r, c - 1] == null)
                {
                    RegisterHorizontalRun(r, c - streak, c - 1, streak, set, specialSpawns);
                    streak = 1;
                    continue;
                }

                if (grid[r, c].colorId == grid[r, c - 1].colorId) streak++;
                else
                {
                    RegisterHorizontalRun(r, c - streak, c - 1, streak, set, specialSpawns);
                    streak = 1;
                }
            }

            RegisterHorizontalRun(r, cols - streak, cols - 1, streak, set, specialSpawns);
        }

        // Dikey kontrol
        for (int c = 0; c < cols; c++)
        {
            int streak = 1;
            for (int r = 1; r < rows; r++)
            {
                if (grid[r, c] == null || grid[r - 1, c] == null)
                {
                    RegisterVerticalRun(c, r - streak, r - 1, streak, set, specialSpawns);
                    streak = 1;
                    continue;
                }

                if (grid[r, c].colorId == grid[r - 1, c].colorId) streak++;
                else
                {
                    RegisterVerticalRun(c, r - streak, r - 1, streak, set, specialSpawns);
                    streak = 1;
                }
            }

            RegisterVerticalRun(c, rows - streak, rows - 1, streak, set, specialSpawns);
        }

        return set;
    }

    private void RegisterHorizontalRun(int row, int startCol, int endCol, int streak, HashSet<Vector2Int> matchSet, Dictionary<Vector2Int, V2SpecialType> specialSpawns)
    {
        if (streak < 3 || startCol < 0 || endCol < startCol)
            return;

        for (int c = startCol; c <= endCol; c++)
            matchSet.Add(new Vector2Int(row, c));

        if (streak >= 5)
        {
            int spawnCol = (startCol + endCol) / 2;
            RegisterSpecialSpawn(new Vector2Int(row, spawnCol), V2SpecialType.Disco, specialSpawns);
        }
        else if (streak >= 4)
        {
            int spawnCol = (startCol + endCol) / 2;
            RegisterSpecialSpawn(new Vector2Int(row, spawnCol), V2SpecialType.RocketHorizontal, specialSpawns);
        }
    }

    private void RegisterVerticalRun(int col, int startRow, int endRow, int streak, HashSet<Vector2Int> matchSet, Dictionary<Vector2Int, V2SpecialType> specialSpawns)
    {
        if (streak < 3 || startRow < 0 || endRow < startRow)
            return;

        for (int r = startRow; r <= endRow; r++)
            matchSet.Add(new Vector2Int(r, col));

        if (streak >= 5)
        {
            int spawnRow = (startRow + endRow) / 2;
            RegisterSpecialSpawn(new Vector2Int(spawnRow, col), V2SpecialType.Disco, specialSpawns);
        }
        else if (streak >= 4)
        {
            int spawnRow = (startRow + endRow) / 2;
            RegisterSpecialSpawn(new Vector2Int(spawnRow, col), V2SpecialType.RocketHorizontal, specialSpawns);
        }
    }

    private void RegisterSpecialSpawn(Vector2Int cell, V2SpecialType incomingType, Dictionary<Vector2Int, V2SpecialType> specialSpawns)
    {
        if (!specialSpawns.TryGetValue(cell, out V2SpecialType existing))
        {
            specialSpawns[cell] = incomingType;
            return;
        }

        specialSpawns[cell] = MergeSpecialTypes(existing, incomingType);
    }

    private V2SpecialType MergeSpecialTypes(V2SpecialType a, V2SpecialType b)
    {
        if (a == b) return a;
        if (a == V2SpecialType.None) return b;
        if (b == V2SpecialType.None) return a;

        if ((a == V2SpecialType.RocketHorizontal && b == V2SpecialType.RocketVertical) ||
            (a == V2SpecialType.RocketVertical && b == V2SpecialType.RocketHorizontal))
            return V2SpecialType.RocketHorizontal;

        if (a == V2SpecialType.Bomb || b == V2SpecialType.Bomb)
            return V2SpecialType.Bomb;

        return a;
    }

    private HashSet<Vector2Int> ExpandMatchesWithSpecials(HashSet<Vector2Int> baseMatches)
    {
        HashSet<Vector2Int> expanded = new HashSet<Vector2Int>(baseMatches);
        Queue<Vector2Int> queue = new Queue<Vector2Int>(baseMatches);

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            if (!IsInside(p)) continue;

            V2Tile tile = grid[p.x, p.y];
            if (tile == null) continue;

            if (tile.specialType == V2SpecialType.RocketHorizontal || tile.specialType == V2SpecialType.RocketVertical)
            {
                for (int c = 0; c < grid.GetLength(1); c++)
                    AddExpanded(new Vector2Int(p.x, c), expanded, queue);

                for (int r = 0; r < grid.GetLength(0); r++)
                    AddExpanded(new Vector2Int(r, p.y), expanded, queue);
            }
            else if (tile.specialType == V2SpecialType.Bomb)
            {
                for (int dr = -1; dr <= 2; dr++)
                {
                    for (int dc = -1; dc <= 2; dc++)
                        AddExpanded(new Vector2Int(p.x + dr, p.y + dc), expanded, queue);
                }
            }
            else if (tile.specialType == V2SpecialType.Disco)
            {
                for (int c = 0; c < grid.GetLength(1); c++)
                    AddExpanded(new Vector2Int(p.x, c), expanded, queue);

                for (int r = 0; r < grid.GetLength(0); r++)
                    AddExpanded(new Vector2Int(r, p.y), expanded, queue);
            }
        }

        return expanded;
    }

    private void AddExpanded(Vector2Int p, HashSet<Vector2Int> expanded, Queue<Vector2Int> queue)
    {
        if (!IsInside(p)) return;
        if (expanded.Add(p))
            queue.Enqueue(p);
    }

    private Sprite GetSpriteForTile(V2Tile tile)
    {
        if (tile == null)
            return null;

        switch (tile.specialType)
        {
            case V2SpecialType.Bomb:
                if (bombSpecialSprite != null) return bombSpecialSprite;
                break;
            case V2SpecialType.Disco:
                if (discoSpecialSprite != null) return discoSpecialSprite;
                break;
            case V2SpecialType.RocketHorizontal:
                if (rocketHorizontalSpecialSprite != null) return rocketHorizontalSpecialSprite;
                if (rocketVerticalSpecialSprite != null) return rocketVerticalSpecialSprite;
                break;
            case V2SpecialType.RocketVertical:
                if (rocketVerticalSpecialSprite != null) return rocketVerticalSpecialSprite;
                if (rocketHorizontalSpecialSprite != null) return rocketHorizontalSpecialSprite;
                break;
        }

        return GetSpriteForId(tile.colorId);
    }

    private void ApplyTileVisual(V2Tile tile)
    {
        if (tile == null) return;

        // Özel taş ayrımı artık renk tint ile değil, direkt özel sprite'larla yapılıyor.
        Color tint = GetColorForId(tile.colorId);
        tile.SetVisual(GetSpriteForTile(tile), tint);
    }

    private IEnumerator AnimateTilePop(RectTransform rt, float delay)
    {
        if (rt == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (rt == null)
            yield break;

        float duration = Mathf.Max(0.05f, tilePopDuration);
        float minScale = Mathf.Clamp(tilePopMinScale, 0.5f, 1f);
        float overshoot = Mathf.Max(1f, tilePopOvershootScale);
        int wobble = Mathf.Clamp(tilePopWobbleCount, 1, 4);

        float t = 0f;
        while (t < duration)
        {
            if (rt == null)
                yield break;

            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);

            float baseScale = Mathf.Lerp(minScale, 1f, n);
            float wave = Mathf.Sin(n * Mathf.PI * wobble) * (1f - n);
            float s = baseScale + wave * (overshoot - 1f);

            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        if (rt != null)
            rt.localScale = Vector3.one;
    }

    private bool IsInside(Vector2Int p)
    {
        if (grid == null)
            return false;

        if (p.x < 0 || p.y < 0 || p.x >= grid.GetLength(0) || p.y >= grid.GetLength(1))
            return false;

        return IsCellPlayable(p.x, p.y);
    }

    private bool AreNeighbors(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private void TriggerBossAttack(int clearedCount)
    {
        if (clearedCount <= 0 || bossEnemy == null)
            return;

        int totalDamage = Mathf.Max(0, clearedCount * Mathf.Max(0, damagePerClearedTile));
        if (totalDamage <= 0)
            return;

        bossEnemy.ApplyDamage(totalDamage);

        if (attackProjectilePrefab == null || bossTarget == null)
            return;

        int projectileCount = Mathf.Clamp(clearedCount, 1, Mathf.Max(1, maxProjectilesPerBurst));
        for (int i = 0; i < projectileCount; i++)
            StartCoroutine(PlayAttackProjectile(i * 0.03f));
    }

    private IEnumerator PlayAttackProjectile(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (attackProjectilePrefab == null || bossTarget == null || boardRoot == null)
            yield break;

        Transform parent = null;
        if (attackProjectilePrefab.GetComponent<RectTransform>() != null)
            parent = boardRoot != null ? (boardRoot.parent != null ? boardRoot.parent : boardRoot) : null;

        GameObject go = parent != null
            ? Instantiate(attackProjectilePrefab, parent)
            : Instantiate(attackProjectilePrefab);

        Transform projectile = go.transform;
        projectile.localScale = Vector3.one;

        Vector3 start = boardRoot.position;
        Vector3 end = bossTarget.position;
        float duration = Mathf.Max(0.05f, attackTravelDuration);

        float t = 0f;
        while (t < duration)
        {
            if (projectile == null)
                yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            projectile.position = Vector3.Lerp(start, end, k);
            yield return null;
        }

        if (go != null)
            Destroy(go);
    }

    private void HandleBossDied()
    {
        if (levelEnded)
            return;

        levelEnded = true;
        busy = true;

        if (hud != null)
            hud.ShowWin();
    }

    private void HandleOutOfMoves()
    {
        if (levelEnded)
            return;

        levelEnded = true;
        busy = true;

        if (hud != null)
            hud.ShowLose();
    }

    private bool IsCellPlayable(int row, int col)
    {
        if (levelData == null)
            return true;

        return levelData.IsCellPlayable(row, col);
    }

    public void LoadLevelFromQueue(int queueIndex)
    {
        if (levelQueue == null || levelQueue.Length == 0)
            return;

        int idx = Mathf.Clamp(queueIndex, 0, levelQueue.Length - 1);
        if (levelQueue[idx] == null)
            return;

        currentLevelQueueIndex = idx;
        levelData = levelQueue[idx];
        StartLevel();
    }

    public void LoadNextLevelFromQueue()
    {
        if (levelQueue == null || levelQueue.Length == 0)
            return;

        int next = (currentLevelQueueIndex + 1) % levelQueue.Length;
        LoadLevelFromQueue(next);
    }

    private void RefreshHUD()
    {
        if (hud == null || levelData == null) return;
        hud.SetScore(score);
        hud.SetMoves(movesLeft);
        hud.SetTarget(levelData.targetScore);
    }

    private void EnsureAudioManager()
    {
        if (audioManager == null)
            audioManager = V2AudioManager.Instance;

        if (audioManager == null)
            audioManager = FindFirstObjectByType<V2AudioManager>();
    }

    private void PlaySwapSfx()
    {
        EnsureAudioManager();
        if (audioManager != null)
            audioManager.PlaySwap();
    }

    private void PlayClearSfx()
    {
        EnsureAudioManager();
        if (audioManager != null)
            audioManager.PlayClear();
    }

    private void PlaySpecialTypeSfx(V2SpecialType type)
    {
        EnsureAudioManager();
        if (audioManager == null)
            return;

        if (type == V2SpecialType.Bomb)
        {
            audioManager.PlayBombSpecial();
            return;
        }

        if (type == V2SpecialType.Disco)
        {
            audioManager.PlayDiscoSpecial();
            return;
        }

        if (type == V2SpecialType.RocketHorizontal || type == V2SpecialType.RocketVertical)
            audioManager.PlayLaneSpecial();
    }

    private void ClearBoard()
    {
        for (int i = boardRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = boardRoot.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child);
            else Destroy(child);
#else
            Destroy(child);
#endif
        }
    }
}