using System.Collections.Generic;
using UnityEngine;

public class GridSlotSpawner : MonoBehaviour
{
    public SlotCell slotPrefab;

    [Header("Grid Layout")]
    public int rows = 5;
    public int cols = 5;
    public float spacingX = 130f;
    public float spacingY = 130f;

    [Header("Manual Offset (eğer yanlış spawn ediyorsa)")]
    public Vector2 manualOffset = Vector2.zero; // Inspector'dan ayarla

    [Header("Auto board size (fixes scaling issues on different devices)")]
    public bool autoSizeBoard = true;
    public float boardPadding = 10f; // ek margin (px)

    [Header("Spawned")]
    public List<SlotCell> spawnedSlots = new List<SlotCell>();

    private RectTransform rectTransformCached;

    private float TotalWidth => (cols - 1) * spacingX;
    private float TotalHeight => (rows - 1) * spacingY;

    private void Awake()
    {
        rectTransformCached = GetComponent<RectTransform>();
    }

    public void BuildGrid()
    {
        // RectTransform ortala (UI container ise)
        RectTransform rt = rectTransformCached;
        if (rt != null)
        {
            // Sadece anchoredPosition/pivot ayarları ile oynamaya çalışıyoruz
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = manualOffset; // Manuel offset kullan
        }

        // Temizle (güvenli şekilde)
        var children = new List<GameObject>();
        foreach (Transform child in transform)
            children.Add(child.gameObject);
        foreach (var go in children)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        spawnedSlots.Clear();

        if (slotPrefab == null)
        {
            Debug.LogError("❌ slotPrefab atanmamış! Grid oluşturulamadı.");
            return;
        }

        // Grid merkez hesapla
        float totalWidth = TotalWidth;
        float totalHeight = TotalHeight;

        Debug.Log($"🔧 Grid: {rows}x{cols}, Width: {totalWidth}, Height: {totalHeight}, Offset: {manualOffset}");

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                SlotCell slot = Instantiate(slotPrefab, transform);
                slot.row = r;
                slot.col = c;
                slot.name = $"Slot_{r}_{c}";

                RectTransform slotRt = slot.GetComponent<RectTransform>();

                float x = -totalWidth * 0.5f + (c * spacingX);
                float y = totalHeight * 0.5f - (r * spacingY);

                if (slotRt != null)
                    slotRt.anchoredPosition = new Vector2(x, y);
                else
                    slot.transform.localPosition = new Vector3(x, y, 0f);

                slot.SetHighlight(false);
                spawnedSlots.Add(slot);
            }
        }

        // Otomatik board size: grid toplam ölçüsüne göre board RectTransform'u ayarla
        if (autoSizeBoard && rectTransformCached != null)
        {
            float width = totalWidth + spacingX + boardPadding;   // hücre genişlikleri arası mesafe + padding
            float height = totalHeight + spacingY + boardPadding;
            rectTransformCached.sizeDelta = new Vector2(width, height);
            Debug.Log($"🔧 Board size auto-set: {rectTransformCached.sizeDelta}");
        }

        Debug.Log($"✅ Grid spawned: {spawnedSlots.Count} slots");
    }

    public void BuildGridFromLevelData(LevelData levelData)
    {
        if (levelData == null)
        {
            Debug.LogError("❌ LevelData null!");
            return;
        }

        rows = Mathf.Max(1, levelData.rows);
        cols = Mathf.Max(1, levelData.cols);

        BuildGrid();
    }

    // Yeni: PuzzleManager ve diğer kodların beklediği imza
    // Verilen row,col değerine göre slot'u döndür (varsa)
    public SlotCell GetSlotAt(int row, int col)
    {
        if (spawnedSlots == null) return null;
        foreach (var s in spawnedSlots)
        {
            if (s == null) continue;
            if (s.row == row && s.col == col) return s;
        }
        return null;
    }

    // Daha önceki yardımcı metod (ismi farklıydı); koruyorum
    public SlotCell GetSlotAtLocalPosition(Vector2 localPos)
    {
        float halfW = TotalWidth * 0.5f;
        float halfH = TotalHeight * 0.5f;

        float colFloat = (localPos.x + halfW) / Mathf.Max(0.0001f, spacingX);
        float rowFloat = (halfH - localPos.y) / Mathf.Max(0.0001f, spacingY);

        int col = Mathf.RoundToInt(colFloat);
        int row = Mathf.RoundToInt(rowFloat);

        if (row < 0 || row >= rows || col < 0 || col >= cols) return null;

        return GetSlotAt(row, col);
    }

    // Expose rect transform for other scripts
    public RectTransform GetRectTransform() => rectTransformCached;
}