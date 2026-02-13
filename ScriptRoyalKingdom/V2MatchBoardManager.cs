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

    [Header("Tile Colors")]
    public Color[] tileColors;

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
        int colorCount = Mathf.Clamp(levelData.colorCount, 3, Mathf.Max(3, tileColors.Length));

        grid = new V2Tile[rows, cols];

        PrepareRootTransform();
        Vector2 anchor = GetAnchor(rows, cols);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var go = Instantiate(tilePrefab, boardRoot);
                go.name = $"tile_{r}_{c}";

                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2((c - anchor.x) * cellSize, (anchor.y - r) * cellSize);

                V2Tile tile = go.GetComponent<V2Tile>();
                if (tile == null) tile = go.AddComponent<V2Tile>();
                int color = Random.Range(0, colorCount);
                tile.SetData(r, c, color, tileColors);

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
        return count % 2 == 1 ? count / 2 : count / 2; // even için sağ/alt merkez
    }

    private IEnumerator RemoveInitialMatches()
    {
        yield return null;
        while (true)
        {
            var matches = FindAllMatches();
            if (matches.Count == 0) break;

            int colorCount = Mathf.Clamp(levelData.colorCount, 3, Mathf.Max(3, tileColors.Length));
            foreach (var p in matches)
            {
                grid[p.x, p.y].colorId = Random.Range(0, colorCount);
                grid[p.x, p.y].RefreshVisual();
            }

            yield return null;
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

        var matches = FindAllMatches();
        if (matches.Count == 0)
        {
            SwapCells(a, b);
            busy = false;
            yield break;
        }

        movesLeft--;
        while (matches.Count > 0)
        {
            int cleared = ClearMatches(matches);
            score += cleared * 20;
            RefreshHUD();
            yield return null;
            matches = FindAllMatches();
        }

        busy = false;
    }

    private void SwapCells(Vector2Int a, Vector2Int b)
    {
        V2Tile ta = grid[a.x, a.y];
        V2Tile tb = grid[b.x, b.y];
        grid[a.x, a.y] = tb;
        grid[b.x, b.y] = ta;

        (ta.row, ta.col) = (b.x, b.y);
        (tb.row, tb.col) = (a.x, a.y);

        SetTilePosition(ta, b.x, b.y);
        SetTilePosition(tb, a.x, a.y);
    }

    private void SetTilePosition(V2Tile tile, int r, int c)
    {
        Vector2 anchor = GetAnchor(grid.GetLength(0), grid.GetLength(1));
        RectTransform rt = tile.GetComponent<RectTransform>();
        if (rt != null)
            rt.anchoredPosition = new Vector2((c - anchor.x) * cellSize, (anchor.y - r) * cellSize);
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

    private int ClearMatches(HashSet<Vector2Int> matches)
    {
        int cleared = 0;
        int colorCount = Mathf.Clamp(levelData.colorCount, 3, Mathf.Max(3, tileColors.Length));
        foreach (var p in matches)
        {
            grid[p.x, p.y].colorId = Random.Range(0, colorCount);
            grid[p.x, p.y].RefreshVisual();
            cleared++;
        }
        if (verboseLogs) Debug.Log($"V2 cleared={cleared}");
        return cleared;
    }

    private bool IsInside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < grid.GetLength(0) && p.y < grid.GetLength(1);
    private bool AreNeighbors(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;

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
            var ch = boardRoot.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(ch);
            else Destroy(ch);
#else
            Destroy(ch);
#endif
        }
    }
}
