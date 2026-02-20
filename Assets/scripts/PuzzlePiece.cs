using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class PuzzlePiece : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
{
    [Header("Shape")]
    public ShapeData shapeData;
    public float cellSize = 100f;

    [Header("Snap Settings")]
    public float snapDistance = 220f;
    public bool lockWhenSnapped = true;
    [Range(0.2f, 2f)]
    public float snapThresholdFactor = 0.75f;

    [Header("Scale Settings")]
    public float trayScale = 0.6f;
    public float dragScale = 1f;

    [Header("Drag Offset Settings")]
    public float dragOffsetY = 150f; // Piece yukarı kalkma miktarı (parmak kapatmasın)

    [Header("Preview Settings")]
    public bool showPreview = true;
    public Color previewColor = new Color(1f, 1f, 1f, 0.3f);

    private RectTransform rect;
    private CanvasGroup cg;
    private Canvas canvas;

    private Vector2 startPos;
    private Transform startParent;

    private bool locked;
    private SlotCell anchorSlot;
    [HideInInspector] public List<SlotCell> occupiedSlots = new List<SlotCell>();

    private List<GameObject> cellVisuals = new List<GameObject>();
    private List<Vector2Int> cellPositions = new List<Vector2Int>();

    private List<GameObject> previewVisuals = new List<GameObject>();
    private SlotCell lastPreviewAnchor = null;
    private Vector2Int currentDragAnchorOffset = Vector2Int.zero;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    // Drag başlangıcındaki EventSystem threshold'unu kapat.
    // Böylece mobilde ilk dokunuştan hemen sonra sürükleme başlar.
    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData != null)
            eventData.useDragThreshold = false;
    }

    private void Start()
    {
        if (rect != null)
            rect.localScale = Vector3.one * trayScale;
    }

    public void BuildShape()
    {
        if (shapeData == null)
        {
            Debug.LogError("❌ ShapeData null!");
            return;
        }

        foreach (var cell in cellVisuals)
        {
            if (cell != null)
            {
                if (Application.isPlaying) Destroy(cell);
                else DestroyImmediate(cell);
            }
        }
        cellVisuals.Clear();
        cellPositions.Clear();

        List<Vector2Int> cells = shapeData.GetCells();

        bool hasAnchorCell = false;
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == Vector2Int.zero)
            {
                hasAnchorCell = true;
                break;
            }
        }
        if (!hasAnchorCell)
            Debug.LogWarning($"⚠️ Shape {shapeData.shapeName} içinde (0,0) anchor hücresi yok. Preview/snap kayabilir.");

        Debug.Log($"🔧 Building shape: {shapeData.shapeName}, Cells: {cells.Count}");

        foreach (var cellPos in cells)
        {
            GameObject cellObj = new GameObject($"Cell_{cellPos.x}_{cellPos.y}");
            cellObj.transform.SetParent(transform, false);

            RectTransform cellRt = cellObj.AddComponent<RectTransform>();
            cellRt.sizeDelta = new Vector2(cellSize, cellSize);
            cellRt.anchoredPosition = new Vector2(cellPos.x * cellSize, -cellPos.y * cellSize);

            Image cellImg = cellObj.AddComponent<Image>();
            cellImg.sprite = shapeData.cellSprite;
            cellImg.color = shapeData.shapeColor;
            cellImg.raycastTarget = false;

            cellVisuals.Add(cellObj);
            cellPositions.Add(cellPos);
        }

        CreateDragArea();

        Debug.Log($"✅ Shape built: {cellVisuals.Count} cells");
    }

    // Shape'in görsel merkez offset'ini hesapla (spawn için)
    public Vector2 GetShapeCenterOffset()
    {
        if (shapeData == null) return Vector2.zero;
        
        List<Vector2Int> cells = shapeData.GetCells();
        if (cells.Count == 0) return Vector2.zero;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var cell in cells)
        {
            float x = cell.x * cellSize;
            float y = -cell.y * cellSize;

            if (x < minX) minX = x;
            if (x + cellSize > maxX) maxX = x + cellSize;
            if (y - cellSize < minY) minY = y - cellSize;
            if (y > maxY) maxY = y;
        }

        // Merkez offset
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        return new Vector2(centerX, centerY);
    }

    private void CreateDragArea()
    {
        Transform oldArea = transform.Find("DragArea");
        if (oldArea != null)
        {
            if (Application.isPlaying) Destroy(oldArea.gameObject);
            else DestroyImmediate(oldArea.gameObject);
        }

        if (shapeData == null) return;

        List<Vector2Int> cells = shapeData.GetCells();
        if (cells.Count == 0) return;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var cell in cells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        GameObject dragArea = new GameObject("DragArea");
        dragArea.transform.SetParent(transform, false);
        dragArea.transform.SetAsFirstSibling();

        RectTransform rt = dragArea.AddComponent<RectTransform>();
        float width = (maxX - minX + 1) * cellSize;
        float height = (maxY - minY + 1) * cellSize;
        rt.sizeDelta = new Vector2(width, height);

        float centerX = (minX + maxX) * 0.5f * cellSize;
        float centerY = -(minY + maxY) * 0.5f * cellSize;
        rt.anchoredPosition = new Vector2(centerX, centerY);

        Image img = dragArea.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (locked) return;

        if (rect == null) rect = GetComponent<RectTransform>();
        if (cg == null) cg = GetComponent<CanvasGroup>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        startPos = rect != null ? rect.anchoredPosition : Vector2.zero;
        startParent = transform.parent;

        currentDragAnchorOffset = Vector2Int.zero;
        Vector2Int touchedCell = GetCellIndexClosestToScreenPoint(eventData.position);
        if (touchedCell.x != -1)
            currentDragAnchorOffset = touchedCell;

        ClearOccupiedSlots();

        if (cg != null)
            cg.blocksRaycasts = false;
        if (cg != null)
            cg.alpha = 0.9f;

        transform.SetAsLastSibling();
        SetPieceSortingOrder(200);

        if (rect != null)
            rect.localScale = Vector3.one * dragScale;

        // YENİ: Piece'i yukarı kaldır (parmak kapatmasın)
        if (rect != null && canvas != null && dragOffsetY > 0)
        {
            rect.anchoredPosition += new Vector2(0, dragOffsetY / canvas.scaleFactor);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (locked) return;
        if (rect == null) return;

        if (canvas != null)
        {
            rect.anchoredPosition += eventData.delta / Mathf.Max(0.0001f, canvas.scaleFactor);
        }
        else
        {
            Vector3 pos = rect.position;
            pos += (Vector3)eventData.delta;
            rect.position = pos;
        }

        if (showPreview)
            UpdatePreview(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (locked) return;

        ClearPreview();

        if (cg != null)
        {
            cg.blocksRaycasts = true;
            cg.alpha = 1f;
        }

        if (PuzzleManager.Instance == null)
        {
            Debug.LogError("❌ PuzzleManager.Instance null! OnEndDrag iptal ediliyor.");
            SetPieceSortingOrder(100);
            transform.SetParent(startParent, true);
            if (rect != null) rect.anchoredPosition = startPos;
            if (rect != null) rect.localScale = Vector3.one * trayScale;
            return;
        }

        // Piece'in anchor (0,0) hücresinin world pozisyonunu bul
        Vector3 pieceAnchorWorldPos = GetCurrentDragAnchorWorldPosition();
        Vector2 pieceScreenPoint = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, pieceAnchorWorldPos);

        var board = PuzzleManager.Instance.boardSpawner;
        RectTransform boardRt = board.GetRectTransform();

        SlotCell anchorCandidate = null;
        if (boardRt != null)
        {
            Vector2 localPoint;
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRt, pieceScreenPoint, canvas != null ? canvas.worldCamera : null, out localPoint);
            if (ok)
            {
                anchorCandidate = board.GetSlotAtLocalPosition(localPoint);
            }
        }

        // Fallback: en yakın slotu bul
        if (anchorCandidate == null)
        {
            anchorCandidate = PuzzleManager.Instance.FindNearestSlot(pieceAnchorWorldPos, snapDistance);
        }

        if (anchorCandidate != null && CanPlaceShapeAt(anchorCandidate))
        {
            SnapTo(anchorCandidate);
            PuzzleManager.Instance.OnPiecePlaced(this, anchorCandidate);
        }
        else
        {
            transform.SetParent(startParent, true);
            if (rect != null) rect.anchoredPosition = startPos;
            if (rect != null) rect.localScale = Vector3.one * trayScale;
        }
    }

    private void UpdatePreview(PointerEventData eventData)
    {
        if (PuzzleManager.Instance == null || PuzzleManager.Instance.boardSpawner == null) return;

        // Piece'in anchor (0,0) hücresinin world pozisyonunu bul
        Vector3 pieceAnchorWorldPos = GetCurrentDragAnchorWorldPosition();
        Vector2 pieceScreenPoint = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, pieceAnchorWorldPos);

        var board = PuzzleManager.Instance.boardSpawner;
        RectTransform boardRt = board.GetRectTransform();

        SlotCell targetSlot = null;
        if (boardRt != null)
        {
            Vector2 localPoint;
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRt, pieceScreenPoint, canvas != null ? canvas.worldCamera : null, out localPoint);
            if (ok)
            {
                targetSlot = board.GetSlotAtLocalPosition(localPoint);
            }
        }

        // targetSlot şimdi piece'in (0,0) hücresinin üzerindeki slot
        SlotCell anchorCandidate = targetSlot;

        if (anchorCandidate != lastPreviewAnchor)
        {
            ClearPreview();
            lastPreviewAnchor = anchorCandidate;

            if (anchorCandidate != null && CanPlaceShapeAt(anchorCandidate))
            {
                ShowPreview(anchorCandidate);
            }
        }
    }

    // Drag sırasında parmağın aldığı hücreyi anchor kabul et
    private Vector3 GetCurrentDragAnchorWorldPosition()
    {
        if (cellVisuals != null && cellPositions != null)
        {
            int count = Mathf.Min(cellVisuals.Count, cellPositions.Count);
            for (int i = 0; i < count; i++)
            {
                if (cellPositions[i] == currentDragAnchorOffset && cellVisuals[i] != null)
                    return cellVisuals[i].transform.position;
            }

            for (int i = 0; i < count; i++)
            {
                if (cellPositions[i] == Vector2Int.zero && cellVisuals[i] != null)
                    return cellVisuals[i].transform.position;
            }
        }

        return transform.position;
    }

    private void ShowPreview(SlotCell anchorSlot)
    {
        if (shapeData == null) return;

        List<Vector2Int> cells = shapeData.GetCells();

        foreach (var cellOffset in cells)
        {
            int targetRow = anchorSlot.row + (cellOffset.y - currentDragAnchorOffset.y);
            int targetCol = anchorSlot.col + (cellOffset.x - currentDragAnchorOffset.x);

            SlotCell targetSlot = PuzzleManager.Instance.GetSlotAt(targetRow, targetCol);
            if (targetSlot == null) continue;

            GameObject previewCell = new GameObject($"Preview_{targetRow}_{targetCol}");
            Transform parentTransform = targetSlot.snapPoint != null ? (Transform)targetSlot.snapPoint : targetSlot.transform;
            previewCell.transform.SetParent(parentTransform, false);

            RectTransform previewRt = previewCell.AddComponent<RectTransform>();
            previewRt.sizeDelta = new Vector2(cellSize, cellSize);
            previewRt.anchoredPosition = Vector2.zero;

            Image previewImg = previewCell.AddComponent<Image>();
            previewImg.sprite = shapeData.cellSprite;
            previewImg.color = previewColor;
            previewImg.raycastTarget = false;

            previewVisuals.Add(previewCell);
        }
    }

    private void ClearPreview()
    {
        foreach (var previewObj in previewVisuals)
        {
            if (previewObj != null)
                Destroy(previewObj);
        }
        previewVisuals.Clear();
        lastPreviewAnchor = null;
    }

    private Vector2Int GetCellIndexClosestToScreenPoint(Vector2 screenPoint)
    {
        if (cellVisuals == null || cellVisuals.Count == 0)
            return Vector2Int.zero;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        Camera cam = canvas != null ? canvas.worldCamera : null;
        float scale = canvas != null ? canvas.scaleFactor : 1f;

        // 1) Önce gerçekten dokunulan hücreyi bul: hücre rect'inin içinde mi?
        // Mobilde merkez zorunluluğunu kaldırır; hücrenin herhangi bir yerine dokunmak yeter.
        float touchPadding = Mathf.Max(2f, cellSize * 0.18f) * Mathf.Max(0.01f, scale);
        for (int i = 0; i < cellVisuals.Count; i++)
        {
            GameObject go = cellVisuals[i];
            if (go == null) continue;

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;

            Vector2 cellScreen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            float half = (cellSize * scale) * 0.5f + touchPadding;

            bool insideX = screenPoint.x >= (cellScreen.x - half) && screenPoint.x <= (cellScreen.x + half);
            bool insideY = screenPoint.y >= (cellScreen.y - half) && screenPoint.y <= (cellScreen.y + half);
            if (insideX && insideY)
                return cellPositions[i];
        }

        // 2) Fallback: en yakın hücreye kilitle (merkez şartı olmasın)
        int bestIdx = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < cellVisuals.Count; i++)
        {
            GameObject go = cellVisuals[i];
            if (go == null) continue;

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;

            Vector2 cellScreen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            float d = (screenPoint - cellScreen).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
            }
        }

        return cellPositions[Mathf.Clamp(bestIdx, 0, cellPositions.Count - 1)];
    }

    private bool CanPlaceShapeAt(SlotCell anchorSlot)
    {
        if (shapeData == null) return false;
        if (PuzzleManager.Instance == null || PuzzleManager.Instance.boardSpawner == null) return false;

        List<Vector2Int> cells = shapeData.GetCells();

        foreach (var cellOffset in cells)
        {
            int targetRow = anchorSlot.row + (cellOffset.y - currentDragAnchorOffset.y);
            int targetCol = anchorSlot.col + (cellOffset.x - currentDragAnchorOffset.x);

            SlotCell targetSlot = PuzzleManager.Instance.GetSlotAt(targetRow, targetCol);

            if (targetSlot == null) return false;
            if (targetSlot.IsOccupied) return false;
        }

        return true;
    }

    private void SnapTo(SlotCell anchorSlot)
    {
        this.anchorSlot = anchorSlot;
        occupiedSlots.Clear();

        List<Vector2Int> cells = shapeData.GetCells();

        foreach (var cellOffset in cells)
        {
            int targetRow = anchorSlot.row + (cellOffset.y - currentDragAnchorOffset.y);
            int targetCol = anchorSlot.col + (cellOffset.x - currentDragAnchorOffset.x);

            SlotCell targetSlot = PuzzleManager.Instance.GetSlotAt(targetRow, targetCol);

            if (targetSlot != null)
            {
                targetSlot.currentPiece = this;
                targetSlot.SetHighlight(false);
                occupiedSlots.Add(targetSlot);
            }
        }

        Transform parentTransform = anchorSlot.snapPoint != null ? (Transform)anchorSlot.snapPoint : anchorSlot.transform;
        transform.SetParent(parentTransform, false);
        if (rect != null)
        {
            float offsetX = -currentDragAnchorOffset.x * cellSize;
            float offsetY = currentDragAnchorOffset.y * cellSize;
            rect.anchoredPosition = new Vector2(offsetX, offsetY);
        }

        EnsurePieceOnTop();

        if (rect != null) rect.localScale = Vector3.one * 1.08f;
        Invoke(nameof(BackToFullScale), 0.06f);

        if (lockWhenSnapped)
        {
            locked = true;
            if (cg != null) cg.blocksRaycasts = false;
        }
    }

    private void SetPieceSortingOrder(int order)
    {
        Canvas pieceCanvas = GetComponent<Canvas>();
        if (pieceCanvas == null)
            pieceCanvas = gameObject.AddComponent<Canvas>();

        pieceCanvas.overrideSorting = true;
        pieceCanvas.sortingOrder = order;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private void EnsurePieceOnTop()
    {
        SetPieceSortingOrder(100);
    }

    private void ClearOccupiedSlots()
    {
        foreach (var slot in occupiedSlots)
        {
            if (slot != null)
            {
                slot.currentPiece = null;
                slot.SetHighlight(false);
            }
        }
        occupiedSlots.Clear();
        anchorSlot = null;
    }

    public void RemoveFromBoardAndDestroy()
    {
        foreach (var s in occupiedSlots)
        {
            if (s != null)
            {
                s.currentPiece = null;
                s.SetHighlight(false);
            }
        }
        occupiedSlots.Clear();

        Destroy(gameObject);
    }

    // ✅ OnSlotsCleared - ANIMASYONLU + GÜVENLİ
    public void OnSlotsCleared(HashSet<SlotCell> clearedSlots)
    {
        if (clearedSlots == null || clearedSlots.Count == 0) return;
        if (this == null) return;

        List<SlotCell> remainingSlots = new List<SlotCell>();
        foreach (var s in occupiedSlots)
        {
            if (s == null) continue;
            if (clearedSlots.Contains(s))
            {
                s.currentPiece = null;
                s.SetHighlight(false);
            }
            else
            {
                remainingSlots.Add(s);
            }
        }

        List<int> removeVisualIndices = new List<int>();
        for (int i = 0; i < cellPositions.Count; i++)
        {
            var offset = cellPositions[i];
            int absRow = anchorSlot != null ? anchorSlot.row + (offset.y - currentDragAnchorOffset.y) : 0;
            int absCol = anchorSlot != null ? anchorSlot.col + (offset.x - currentDragAnchorOffset.x) : 0;

            SlotCell absSlot = PuzzleManager.Instance != null ? PuzzleManager.Instance.GetSlotAt(absRow, absCol) : null;
            if (absSlot != null && clearedSlots.Contains(absSlot))
            {
                removeVisualIndices.Add(i);
            }
        }

        StartCoroutine(AnimateAndRemoveCells(removeVisualIndices, remainingSlots, clearedSlots));
    }

    private System.Collections.IEnumerator AnimateAndRemoveCells(List<int> removeIndices, List<SlotCell> remainingSlots, HashSet<SlotCell> clearedSlots)
    {
        float duration = 0.3f;
        float elapsed = 0f;

        List<GameObject> cellsToAnimate = new List<GameObject>();
        List<Vector3> startScales = new List<Vector3>();

        foreach (int idx in removeIndices)
        {
            if (idx < cellVisuals.Count && cellVisuals[idx] != null)
            {
                cellsToAnimate.Add(cellVisuals[idx]);
                startScales.Add(cellVisuals[idx].transform.localScale);
            }
        }

        CanvasGroup cgAnim = GetComponent<CanvasGroup>();
        if (cgAnim == null) cgAnim = gameObject.AddComponent<CanvasGroup>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            for (int i = 0; i < cellsToAnimate.Count; i++)
            {
                if (cellsToAnimate[i] != null)
                {
                    cellsToAnimate[i].transform.localScale = Vector3.Lerp(startScales[i], startScales[i] * 1.5f, t);
                    
                    Image img = cellsToAnimate[i].GetComponent<Image>();
                    if (img != null)
                    {
                        Color c = img.color;
                        c.a = 1f - t;
                        img.color = c;
                    }
                }
            }

            yield return null;
        }

        removeIndices.Sort((a, b) => b.CompareTo(a));
        foreach (int idx in removeIndices)
        {
            if (idx < cellVisuals.Count)
            {
                var go = cellVisuals[idx];
                if (go != null) Destroy(go);
                cellVisuals.RemoveAt(idx);
                cellPositions.RemoveAt(idx);
            }
        }

        occupiedSlots = remainingSlots;

        if (occupiedSlots.Count == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        foreach (var slot in new List<SlotCell>(occupiedSlots))
        {
            if (slot == null) continue;

            ShapeData single = ScriptableObject.CreateInstance<ShapeData>();
            single.shapeName = "single_cell";
            single.shapeColor = this.shapeData != null ? this.shapeData.shapeColor : Color.white;
            single.cellSprite = this.shapeData != null ? this.shapeData.cellSprite : null;
            single.pattern = "X";

            GameObject go = new GameObject($"CellPiece_{slot.row}_{slot.col}");
            Transform parentTransform = slot.snapPoint != null ? (Transform)slot.snapPoint : slot.transform;
            go.transform.SetParent(parentTransform, false);

            PuzzlePiece newP = go.AddComponent<PuzzlePiece>();
            newP.shapeData = single;
            newP.cellSize = this.cellSize;
            newP.trayScale = 1f;
            newP.BuildShape();

            RectTransform newRt = go.GetComponent<RectTransform>();
            if (newRt != null)
            {
                newRt.anchoredPosition = Vector2.zero;
                newRt.localScale = Vector3.one;
            }

            Canvas splitCanvas = go.AddComponent<Canvas>();
            splitCanvas.overrideSorting = true;
            splitCanvas.sortingOrder = 100;
            go.AddComponent<GraphicRaycaster>();

            slot.currentPiece = newP;
            slot.SetHighlight(false);

            newP.lockWhenSnapped = true;
            newP.locked = true;
            if (newP.cg != null) newP.cg.blocksRaycasts = false;
        }

        Destroy(gameObject);
    }

    private void BackToFullScale()
    {
        if (rect != null) rect.localScale = Vector3.one;
    }

    private void BackScale()
    {
        if (rect != null) rect.localScale = Vector3.one * trayScale;
    }
}