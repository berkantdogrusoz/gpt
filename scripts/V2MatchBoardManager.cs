using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class V2MatchBoardManager : MonoBehaviour
{
    [Header("References")]
    public V2LevelData levelData;
    public RectTransform boardRoot;
    public GameObject tilePrefab;
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

    [Header("Debug")]
    public bool verboseLogs = true;

    private V2Tile[,] grid;
    private int score;
    private int movesLeft;
    private bool busy;

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
            HashSet<Vector2Int> matches = FindAllMatches();
            if (matches.Count == 0) break;

            yield return StartCoroutine(ClearCollapseRefill(matches, giveScore: false));
        }
    }

    public void TrySwap(Vector2Int a, Vector2Int b)
    {
        if (busy || movesLeft <= 0 || !IsInside(a) || !IsInside(b) || !AreNeighbors(a, b)) return;
        StartCoroutine(SwapResolve(a, b));
    }

    private IEnumerator SwapResolve(Vector2Int a, Vector2Int b)
    {
        busy = true;
        SwapCells(a, b);

        HashSet<Vector2Int> matches = FindAllMatches();
        if (matches.Count == 0)
        {
            SwapCells(a, b);
            busy = false;
            yield break;
        }

        movesLeft--;

        while (matches.Count > 0)
        {
            yield return StartCoroutine(ClearCollapseRefill(matches, giveScore: true));
            matches = FindAllMatches();
        }

        RefreshHUD();
        busy = false;
    }

    private IEnumerator ClearCollapseRefill(HashSet<Vector2Int> matches, bool giveScore)
    {
        int clearedCount = ClearMatches(matches);
        if (giveScore)
            score += clearedCount * 20;

        yield return null;

        CollapseColumns();
        yield return null;

        RefillFromTop();
        yield return new WaitForSeconds(fallStepDelay);

        RefreshHUD();
    }

    private int ClearMatches(HashSet<Vector2Int> matches)
    {
        int cleared = 0;

        foreach (Vector2Int p in matches)
        {
            V2Tile tile = grid[p.x, p.y];
            if (tile == null) continue;

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

                V2Tile tile = SpawnTile(r, c, RandomTileId(), anchor, spawnAboveTop: true);
                grid[r, c] = tile;
            }
        }
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
        tile.SetVisual(GetSpriteForId(id), GetColorForId(id));

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

    private void SwapCells(Vector2Int a, Vector2Int b)
    {
        V2Tile ta = grid[a.x, a.y];
        V2Tile tb = grid[b.x, b.y];
        grid[a.x, a.y] = tb;
        grid[b.x, b.y] = ta;

        (ta.row, ta.col) = (b.x, b.y);
        (tb.row, tb.col) = (a.x, a.y);

        Vector2 anchor = GetAnchor(grid.GetLength(0), grid.GetLength(1));
        SetTilePosition(ta, b.x, b.y, anchor, false);
        SetTilePosition(tb, a.x, a.y, anchor, false);
    }

    private HashSet<Vector2Int> FindAllMatches()
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            int streak = 1;
            for (int c = 1; c < cols; c++)
            {
                if (grid[r, c].colorId == grid[r, c - 1].colorId) streak++;
                else
                {
                    if (streak >= 3) for (int k = 0; k < streak; k++) set.Add(new Vector2Int(r, c - 1 - k));
                    streak = 1;
                }
            }
            if (streak >= 3) for (int k = 0; k < streak; k++) set.Add(new Vector2Int(r, cols - 1 - k));
        }

        for (int c = 0; c < cols; c++)
        {
            int streak = 1;
            for (int r = 1; r < rows; r++)
            {
                if (grid[r, c].colorId == grid[r - 1, c].colorId) streak++;
                else
                {
                    if (streak >= 3) for (int k = 0; k < streak; k++) set.Add(new Vector2Int(r - 1 - k, c));
                    streak = 1;
                }
            }
            if (streak >= 3) for (int k = 0; k < streak; k++) set.Add(new Vector2Int(rows - 1 - k, c));
        }

        return set;
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
