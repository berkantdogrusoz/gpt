using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PieceSpawner : MonoBehaviour
{
    public PuzzlePiece piecePrefab;

    [Header("Shapes")]
    public List<ShapeData> availableShapes;

    [Header("Spawn Points (can be RectTransform UI or world-space Transform)")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Spawned")]
    public List<PuzzlePiece> spawnedPieces = new List<PuzzlePiece>();

    [Header("Options")]
    public bool useAnchoredPositionForUI = true; // UI spawnPoints için anchoredPosition kullan
    public bool devLogSpawn = true; // spawn sırasında log at
    
    [Header("Smart Spawn")]
    public bool useSmartSpawn = true; // Akıllı spawn sistemi
    [Range(0f, 1f)]
    public float smartSpawnChance = 0.8f; // %80 sığan shape, %20 random

    // Helper: tries to find the appropriate Canvas for a given spawn point (priority)
    private Canvas FindCanvasFor(Transform spawnPoint)
    {
        if (spawnPoint == null) return FindAnyCanvas();

        // 1) If spawnPoint or its parents are under a Canvas, use that
        var c = spawnPoint.GetComponentInParent<Canvas>();
        if (c != null) return c;

        // 2) If this spawner is under a Canvas, use that
        c = GetComponentInParent<Canvas>();
        if (c != null) return c;

        // 3) fallback to any Canvas in scene
        return FindAnyCanvas();
    }

    private Canvas FindAnyCanvas()
    {
        var any = FindObjectOfType<Canvas>();
        return any;
    }

    public void SpawnPieces()
    {
        // Temizle (güvenli şekilde) — eski children'ları kaldırıyoruz (tray temizleme)
        var children = new List<GameObject>();
        foreach (Transform child in transform)
            children.Add(child.gameObject);
        foreach (var go in children)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        spawnedPieces.Clear();

        if (piecePrefab == null)
        {
            Debug.LogError("❌ piecePrefab atanmamış!");
            return;
        }

        if (availableShapes == null || availableShapes.Count == 0)
        {
            Debug.LogError("❌ Hiç shape yok!");
            return;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("❌ Spawn point atanmamış!");
            return;
        }

        // NOTE: IMPORTANT: instantiate pieces as children of this.transform (traySpawner.transform)
        // so PuzzleManager.CheckTrayEmpty (which checks parent == traySpawner.transform) continues to work.
        RectTransform pieceParentRt = transform as RectTransform;

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            var sp = spawnPoints[i];
            if (sp == null)
            {
                Debug.LogWarning($"⚠️ Spawn Point {i} null!");
                continue;
            }

            // Şekli seç - akıllı veya random
            ShapeData selectedShape = SelectShape();

            // Hangi Canvas kullanılacak?
            Canvas targetCanvas = FindCanvasFor(sp);
            RectTransform canvasRt = targetCanvas != null ? targetCanvas.GetComponent<RectTransform>() : null;

            // Instantiate under tray (this.transform) always, to preserve tray-parent semantics
            PuzzlePiece p = Instantiate(piecePrefab, transform);
            p.name = $"Piece_{i}";

            p.shapeData = selectedShape;
            p.BuildShape();

            RectTransform pieceRt = p.GetComponent<RectTransform>();

            // Pozisyonlandırma:
            RectTransform spawnRt = sp.GetComponent<RectTransform>();
            if (spawnRt != null && pieceRt != null && pieceParentRt != null && useAnchoredPositionForUI)
            {
                // spawnPoint is UI. Convert spawnRt.position -> screenPoint -> pieceParent local point
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(spawnRt.GetComponentInParent<Canvas>()?.worldCamera, spawnRt.position);
                Camera uiCam = targetCanvas != null ? targetCanvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(pieceParentRt, screenPoint, uiCam, out var localPt))
                {
                    pieceRt.anchoredPosition = localPt;
                }
                else
                {
                    // fallback: try copying anchoredPosition if parents equal
                    if (pieceRt.parent == spawnRt.parent)
                    {
                        pieceRt.anchoredPosition = spawnRt.anchoredPosition;
                    }
                    else
                    {
                        // as last resort, set world position
                        p.transform.position = spawnRt.position;
                    }
                }
            }
            else
            {
                // spawn point is world-space OR piece parent not a RectTransform
                // Convert world -> screen -> pieceParent local
                Vector2 screenPoint;
                if (Camera.main != null)
                    screenPoint = (Vector2)Camera.main.WorldToScreenPoint(sp.position);
                else
                    screenPoint = new Vector2(sp.position.x, sp.position.y);

                if (pieceParentRt != null)
                {
                    Camera uiCam = targetCanvas != null ? targetCanvas.worldCamera : null;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(pieceParentRt, screenPoint, uiCam, out var localPt))
                    {
                        if (pieceRt != null) pieceRt.anchoredPosition = localPt;
                        else p.transform.localPosition = (Vector3)localPt;
                    }
                    else
                    {
                        // fallback to world position
                        p.transform.position = sp.position;
                    }
                }
                else
                {
                    // no rect parent: fallback world
                    p.transform.position = sp.position;
                }
            }

            // normalize scale / rotation
            if (pieceRt != null)
            {
                pieceRt.localScale = Vector3.one;
                pieceRt.localRotation = Quaternion.identity;
                
                // Shape'i spawn noktasına ortala
                Vector2 centerOffset = p.GetShapeCenterOffset();
                pieceRt.anchoredPosition -= centerOffset * p.trayScale;
            }
            else
            {
                p.transform.localScale = Vector3.one;
                p.transform.localRotation = Quaternion.identity;
            }

            spawnedPieces.Add(p);

            if (devLogSpawn)
            {
                string parentName = p.transform.parent != null ? p.transform.parent.name : "null";
                Vector2 anchored = pieceRt != null ? pieceRt.anchoredPosition : Vector2.zero;
                Debug.Log($"[Spawn] Piece {p.name} parent:{parentName} anchored:{anchored} worldPos:{p.transform.position}");
            }
        }

        Debug.Log($"✅ Toplam {spawnedPieces.Count} piece spawn edildi");
    }

    // Akıllı veya random shape seç
    private ShapeData SelectShape()
    {
        if (!useSmartSpawn || PuzzleManager.Instance == null || PuzzleManager.Instance.boardSpawner == null)
        {
            return availableShapes[Random.Range(0, availableShapes.Count)];
        }

        var board = PuzzleManager.Instance.boardSpawner;

        // 1. Öncelik: Satır tamamlayacak shape'ler
        List<ShapeData> rowCompletingShapes = GetRowCompletingShapes(board);
        
        if (rowCompletingShapes.Count > 0 && Random.value < smartSpawnChance)
        {
            ShapeData selected = rowCompletingShapes[Random.Range(0, rowCompletingShapes.Count)];
            if (devLogSpawn) Debug.Log($"[SmartSpawn] ✨ Satır tamamlayacak shape: {selected.shapeName}");
            return selected;
        }

        // 2. Öncelik: En azından sığabilen shape'ler
        List<ShapeData> fittingShapes = GetFittingShapes();
        
        if (fittingShapes.Count > 0 && Random.value < smartSpawnChance)
        {
            ShapeData selected = fittingShapes[Random.Range(0, fittingShapes.Count)];
            if (devLogSpawn) Debug.Log($"[SmartSpawn] Sığan shape: {selected.shapeName}");
            return selected;
        }

        // 3. Fallback: Random
        ShapeData randomShape = availableShapes[Random.Range(0, availableShapes.Count)];
        if (devLogSpawn) Debug.Log($"[SmartSpawn] Random shape: {randomShape.shapeName}");
        return randomShape;
    }

    // Satır tamamlayabilecek shape'leri bul
    private List<ShapeData> GetRowCompletingShapes(GridSlotSpawner board)
    {
        List<ShapeData> completing = new List<ShapeData>();
        
        int rows = board.rows;
        int cols = board.cols;

        // Her satırın boşluklarını analiz et
        for (int r = 0; r < rows; r++)
        {
            List<int> emptyColsInRow = new List<int>();
            int filledCount = 0;

            for (int c = 0; c < cols; c++)
            {
                SlotCell slot = board.GetSlotAt(r, c);
                if (slot == null) continue;

                if (slot.IsOccupied)
                    filledCount++;
                else
                    emptyColsInRow.Add(c);
            }

            // Satır en az %50 doluysa ve boşluk varsa, tamamlayacak shape ara
            float fillRatio = (float)filledCount / cols;
            if (fillRatio >= 0.5f && emptyColsInRow.Count > 0 && emptyColsInRow.Count <= 4)
            {
                // Bu boşlukları dolduracak shape var mı?
                foreach (var shape in availableShapes)
                {
                    if (shape == null) continue;
                    if (completing.Contains(shape)) continue;

                    if (CanShapeFillGaps(shape, board, r, emptyColsInRow, rows, cols))
                    {
                        completing.Add(shape);
                    }
                }
            }
        }

        return completing;
    }

    // Shape verilen satırdaki boşlukları doldurabilir mi?
    private bool CanShapeFillGaps(ShapeData shape, GridSlotSpawner board, int targetRow, List<int> emptyCols, int rows, int cols)
    {
        List<Vector2Int> cells = shape.GetCells();
        if (cells.Count == 0) return false;

        // Shape'in her olası pozisyonunu dene
        for (int startCol = -3; startCol < cols; startCol++)
        {
            for (int startRow = 0; startRow < rows; startRow++)
            {
                bool allCellsValid = true;
                int gapsFilled = 0;

                foreach (var cellOffset in cells)
                {
                    int checkRow = startRow + cellOffset.y;
                    int checkCol = startCol + cellOffset.x;

                    // Grid dışı?
                    if (checkRow < 0 || checkRow >= rows || checkCol < 0 || checkCol >= cols)
                    {
                        allCellsValid = false;
                        break;
                    }

                    SlotCell slot = board.GetSlotAt(checkRow, checkCol);
                    if (slot == null || slot.IsOccupied)
                    {
                        allCellsValid = false;
                        break;
                    }

                    // Bu hücre hedef satırdaki boşluklardan biri mi?
                    if (checkRow == targetRow && emptyCols.Contains(checkCol))
                    {
                        gapsFilled++;
                    }
                }

                // Tüm hücreler geçerli VE en az 1 boşluk dolduruyorsa
                if (allCellsValid && gapsFilled > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Grid'e sığabilen shape'leri döndür
    private List<ShapeData> GetFittingShapes()
    {
        List<ShapeData> fitting = new List<ShapeData>();

        if (PuzzleManager.Instance == null || PuzzleManager.Instance.boardSpawner == null)
            return fitting;

        var board = PuzzleManager.Instance.boardSpawner;
        int rows = board.rows;
        int cols = board.cols;

        foreach (var shape in availableShapes)
        {
            if (shape == null) continue;

            if (CanShapeFitAnywhere(shape, board, rows, cols))
            {
                fitting.Add(shape);
            }
        }

        return fitting;
    }

    // Shape grid'in herhangi bir yerine sığabiliyor mu?
    private bool CanShapeFitAnywhere(ShapeData shape, GridSlotSpawner board, int rows, int cols)
    {
        List<Vector2Int> cells = shape.GetCells();
        if (cells.Count == 0) return false;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool canPlace = true;

                foreach (var cellOffset in cells)
                {
                    int targetRow = r + cellOffset.y;
                    int targetCol = c + cellOffset.x;

                    // Grid dışı mı?
                    if (targetRow < 0 || targetRow >= rows || targetCol < 0 || targetCol >= cols)
                    {
                        canPlace = false;
                        break;
                    }

                    // Slot dolu mu?
                    SlotCell slot = board.GetSlotAt(targetRow, targetCol);
                    if (slot == null || slot.IsOccupied)
                    {
                        canPlace = false;
                        break;
                    }
                }

                if (canPlace) return true;
            }
        }

        return false;
    }

    // En küçük (en az hücreli) shape'i bul
    private ShapeData GetSmallestShape()
    {
        ShapeData smallest = null;
        int minCells = int.MaxValue;

        foreach (var shape in availableShapes)
        {
            if (shape == null) continue;
            int cellCount = shape.GetCells().Count;
            if (cellCount < minCells)
            {
                minCells = cellCount;
                smallest = shape;
            }
        }

        return smallest;
    }
}