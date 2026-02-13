using UnityEngine;

public class V2GridBoardSpawner : MonoBehaviour
{
    [Header("Board")]
    public RectTransform boardRoot;
    public GameObject slotPrefab;
    public int rows = 8;
    public int cols = 8;
    public float cellSize = 100f;

    [Header("Root Snap")]
    [Tooltip("Board root'u her rebuild'de parent merkezine kilitler.")]
    public bool forceRootToParentCenter = true;

    [Header("Alignment")]
    [Tooltip("True: tek bir hücre merkezi root(0,0)'a oturur. False: tüm ızgaranın geometrik merkezi root'a oturur.")]
    public bool alignToCenterCell = false;

    [Tooltip("Çift sayılı satır/sütunda hangi orta hücre seçilecek. 0.5=üst/sol, 1=alt/sağ")]
    [Range(0.5f, 1f)] public float evenGridCenterBias = 1f;

    [ContextMenu("Rebuild Grid")]
    public void RebuildGrid()
    {
        if (boardRoot == null || slotPrefab == null)
        {
            Debug.LogError("V2GridBoardSpawner: boardRoot veya slotPrefab atanmadı.");
            return;
        }

        PrepareRootTransform();
        ClearChildren();

        rows = Mathf.Max(1, rows);
        cols = Mathf.Max(1, cols);

        Vector2 anchor = GetAnchorCell();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject go = Instantiate(slotPrefab, boardRoot);
                go.name = $"slot_{r}_{c}";

                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);

                float x = (c - anchor.x) * cellSize;
                float y = (anchor.y - r) * cellSize;
                rt.anchoredPosition = new Vector2(x, y);
                rt.localScale = Vector3.one;
            }
        }
    }

    private void PrepareRootTransform()
    {
        // Root'un local uzayında 0,0 gerçekten merkez olsun.
        boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        boardRoot.pivot = new Vector2(0.5f, 0.5f);

        if (forceRootToParentCenter)
            boardRoot.anchoredPosition = Vector2.zero;
    }

    private Vector2 GetAnchorCell()
    {
        if (!alignToCenterCell)
        {
            // Geometrik merkez (8x8 için 3.5,3.5)
            return new Vector2((cols - 1) * 0.5f, (rows - 1) * 0.5f);
        }

        // Hücre merkezi root'a otursun (çift sayıda iki ortadan biri seçilir)
        int anchorCol = GetCenterCellIndex(cols);
        int anchorRow = GetCenterCellIndex(rows);
        return new Vector2(anchorCol, anchorRow);
    }

    private int GetCenterCellIndex(int count)
    {
        if (count % 2 == 1) return count / 2;

        int left = (count / 2) - 1;
        int right = count / 2;
        return evenGridCenterBias >= 0.75f ? right : left;
    }

    private void ClearChildren()
    {
        for (int i = boardRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = boardRoot.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }
}
