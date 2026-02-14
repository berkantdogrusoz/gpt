using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class V2MatchBoardManager : MonoBehaviour
{
    [Header("References")]
    public V2LevelData levelData;
    public RectTransform boardRoot;
    public GameObject tilePrefab;
    public GameObject slotPrefab; // Arka plan slot prefab'ı
    public V2KingdomHUD hud;

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

    [Header("Animation")]
    public float fallStepDelay = 0.03f;
    public float swapDuration = 0.12f;
    public float invalidShakeDuration = 0.12f;
    public float invalidShakeOffset = 10f;

    [Header("Debug")]
    public bool verboseLogs = true;

    private V2Tile[,] grid;
    private int score;
    private int movesLeft;
    private bool busy;

    // Public getter'lar - Input controller için
    public int Rows => levelData != null ? levelData.rows : 8;
    public int Cols => levelData != null ? levelData.cols : 8;
    public float CellSize => cellSize;

    private void Start()
    {
        StartLevel();
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

        BuildBoard();
        RefreshHUD();
    }

    private void BuildBoard()
    {
        ClearBoard();

        int rows = Mathf.Max(1, levelData.rows);
        int cols = Mathf.Max(1, levelData.cols);

        grid = new V2Tile[rows, cols];

        PrepareRootTransform();
        Vector2 anchor = GetAnchor(rows, cols);

        // Önce arka plan slotlarını spawn et
        SpawnSlots(rows, cols, anchor);

        // Sonra tile'ları spawn et
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                V2Tile tile = SpawnTile(r, c, RandomTileId(), anchor, spawnAboveTop: false);
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
        if (busy)
        {
            Debug.Log("[V2Board] Busy, swap rejected");
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
        StartCoroutine(SwapResolve(a, b));
    }

    private IEnumerator SwapResolve(Vector2Int a, Vector2Int b)
    {
        busy = true;

        yield return StartCoroutine(AnimateSwapCells(a, b));

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
            matches = FindAllMatches(out specialSpawns);
        }

        RefreshHUD();
        busy = false;
    }

    private IEnumerator ClearCollapseRefill(HashSet<Vector2Int> matches, Dictionary<Vector2Int, V2SpecialType> specialSpawns, bool giveScore, bool allowSpecialSpawn)
    {
        int clearedCount = ClearMatches(matches, specialSpawns, allowSpecialSpawn);
        if (giveScore)
            score += clearedCount * 20;

        yield return null;

        CollapseColumns();
        yield return null;

        RefillFromTop();
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

            Destroy(tile.gameObject);
            grid[p.x, p.y] = null;
            cleared++;
        }

        if (verboseLogs) Debug.Log($"V2 cleared={cleared}");
        return cleared;
    }

    private void CollapseColumns()
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        Vector2 anchor = GetAnchor(rows, cols);

        for (int c = 0; c < cols; c++)
        {
            int writeRow = rows - 1;

            for (int r = rows - 1; r >= 0; r--)
            {
                V2Tile tile = grid[r, c];
                if (tile == null) continue;

                if (writeRow != r)
                {
                    grid[writeRow, c] = tile;
                    grid[r, c] = null;

                    tile.row = writeRow;
                    tile.col = c;
                    SetTilePosition(tile, writeRow, c, anchor, false);
                }

                writeRow--;
            }
        }
    }

    private void RefillFromTop()
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        Vector2 anchor = GetAnchor(rows, cols);

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (grid[r, c] != null) continue;

                // Not: Spawn-above davranışı animasyonla desteklenmediğinde taşlar
                // görünür ızgara dışına düşebiliyor. Stabil gameplay için doğrudan
                // hedef hücreye spawn ediyoruz.
                V2Tile tile = SpawnTile(r, c, RandomTileId(), anchor, spawnAboveTop: false);
                grid[r, c] = tile;
            }
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

    private V2Tile SpawnTile(int r, int c, int id, Vector2 anchor, bool spawnAboveTop)
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
        tile.SetSpecial(V2SpecialType.None);
        ApplyTileVisual(tile);

        SetTilePosition(tile, r, c, anchor, spawnAboveTop);
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

        if (streak >= 4)
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

        if (streak >= 4)
        {
            int spawnRow = (startRow + endRow) / 2;
            RegisterSpecialSpawn(new Vector2Int(spawnRow, col), V2SpecialType.RocketVertical, specialSpawns);
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
            return V2SpecialType.Bomb;

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

            if (tile.specialType == V2SpecialType.RocketHorizontal)
            {
                for (int c = 0; c < grid.GetLength(1); c++)
                    AddExpanded(new Vector2Int(p.x, c), expanded, queue);
            }
            else if (tile.specialType == V2SpecialType.RocketVertical)
            {
                for (int r = 0; r < grid.GetLength(0); r++)
                    AddExpanded(new Vector2Int(r, p.y), expanded, queue);
            }
            else if (tile.specialType == V2SpecialType.Bomb)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                        AddExpanded(new Vector2Int(p.x + dr, p.y + dc), expanded, queue);
                }
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

    private void ApplyTileVisual(V2Tile tile)
    {
        if (tile == null) return;

        Color tint = GetColorForId(tile.colorId);
        switch (tile.specialType)
        {
            case V2SpecialType.RocketHorizontal:
                tint = Color.Lerp(tint, Color.cyan, 0.35f);
                break;
            case V2SpecialType.RocketVertical:
                tint = Color.Lerp(tint, new Color(1f, 0.4f, 1f), 0.35f);
                break;
            case V2SpecialType.Bomb:
                tint = Color.Lerp(tint, Color.yellow, 0.45f);
                break;
        }

        tile.SetVisual(GetSpriteForId(tile.colorId), tint);
    }

    private bool IsInside(Vector2Int p)
    {
        return p.x >= 0 && p.y >= 0 && p.x < grid.GetLength(0) && p.y < grid.GetLength(1);
    }

    private bool AreNeighbors(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private void RefreshHUD()
    {
        if (hud == null || levelData == null) return;
        hud.SetScore(score);
        hud.SetMoves(movesLeft);
        hud.SetTarget(levelData.targetScore);
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
