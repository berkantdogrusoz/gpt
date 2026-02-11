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

            // Şekli ata ve görseli oluştur
            ShapeData randomShape = availableShapes[Random.Range(0, availableShapes.Count)];

            // Hangi Canvas kullanılacak?
            Canvas targetCanvas = FindCanvasFor(sp);
            RectTransform canvasRt = targetCanvas != null ? targetCanvas.GetComponent<RectTransform>() : null;

            // Instantiate under tray (this.transform) always, to preserve tray-parent semantics
            PuzzlePiece p = Instantiate(piecePrefab, transform);
            p.name = $"Piece_{i}";

            p.shapeData = randomShape;
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
}