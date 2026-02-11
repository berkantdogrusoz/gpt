using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance;

    [Header("Level")]
    public LevelData currentLevel;

    [Header("Spawners")]
    public GridSlotSpawner boardSpawner;
    public PieceSpawner traySpawner;

    [Header("UI")]
    public GameObject winPanel;
    public GameObject losePanel;
    public HUDController hud;

    [Header("Settings")]
    public float respawnDelay = 0.5f;

    [Header("Scoring")]
    public int score = 0;
    public int baseRowPoints = 100;
    public float comboMultiplierStep = 0.25f;
    private int comboCount = 0;

    [Header("Row Clear Animation")]
    public float slotClearDelay = 0.08f;

    [Header("Floating Text")]
    public GameObject floatingTextPrefab;
    public Transform floatingTextParent;

    private int totalSlots = 0;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(false);
            Debug.Log("✅ Win Panel kapatıldı");
        }

        if (losePanel != null)
        {
            losePanel.SetActive(false);
            Debug.Log("✅ Lose Panel kapatıldı");
        }

        if (currentLevel != null && boardSpawner != null)
        {
            boardSpawner.BuildGridFromLevelData(currentLevel);
            totalSlots = boardSpawner.spawnedSlots.Count;
            Debug.Log($"✅ Level: {currentLevel.name}, Total slots: {totalSlots}");
        }
        else if (boardSpawner != null)
        {
            boardSpawner.BuildGrid();
            totalSlots = boardSpawner.spawnedSlots.Count;
            Debug.Log($"✅ Default grid: {totalSlots} slots");
        }

        if (hud != null) hud.UpdateHUD(score, GetTargetScoreForCurrentArena());

        SpawnNewPieces();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("🔥 SPACE - Win panel test");
            ShowWin();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("🔄 Restart");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }

    public SlotCell GetSlotUnderPointer(PointerEventData eventData)
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("⚠️ EventSystem.current is null - UI raycasts won't work.");
            return null;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            SlotCell slot = result.gameObject.GetComponentInParent<SlotCell>();
            if (slot != null) return slot;
        }

        return null;
    }

    public SlotCell FindNearestSlot(Vector3 pieceWorldPos, float maxDist)
    {
        if (boardSpawner == null || boardSpawner.spawnedSlots == null) return null;

        SlotCell bestSlot = null;
        float bestDistance = maxDist;

        foreach (var slot in boardSpawner.spawnedSlots)
        {
            if (slot == null) continue;

            float distance = Vector3.Distance(pieceWorldPos, slot.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSlot = slot;
            }
        }

        return bestSlot;
    }

    public void OnPiecePlaced(PuzzlePiece piece, SlotCell slot)
    {
        Debug.Log($"✅ Piece placed at ({slot.row}, {slot.col})");

        StartCoroutine(CheckAndClearFullRowsCoroutine());
    }

    private IEnumerator CheckAndClearFullRowsCoroutine()
    {
        if (boardSpawner == null || boardSpawner.spawnedSlots == null) yield break;

        int rows = boardSpawner.rows;
        int cols = boardSpawner.cols;

        List<int> rowsCleared = new List<int>();
        HashSet<SlotCell> slotsToClear = new HashSet<SlotCell>();

        for (int r = 0; r < rows; r++)
        {
            bool rowFull = true;
            for (int c = 0; c < cols; c++)
            {
                SlotCell s = boardSpawner.GetSlotAt(r, c);
                if (s == null || !s.IsOccupied)
                {
                    rowFull = false;
                    break;
                }
            }

            if (rowFull)
            {
                rowsCleared.Add(r);
                for (int c = 0; c < cols; c++)
                {
                    SlotCell s = boardSpawner.GetSlotAt(r, c);
                    if (s != null) slotsToClear.Add(s);
                }
            }
        }

        if (rowsCleared.Count == 0)
        {
            comboCount = 0;
            
            CheckTrayEmpty();
            if (IsBoardFull())
            {
                Debug.Log("🎉 BOARD TAMAMEN DOLU! KAZANDIN!");
                Invoke(nameof(ShowWin), 0.5f);
                yield break;
            }
            CheckLoseCondition();
            yield break;
        }

        Debug.Log($"🔔 Clearing rows: {string.Join(", ", rowsCleared)}");

        HashSet<PuzzlePiece> affectedPieces = new HashSet<PuzzlePiece>();
        foreach (var s in slotsToClear)
        {
            if (s != null && s.currentPiece != null)
                affectedPieces.Add(s.currentPiece);
        }

        foreach (var p in affectedPieces)
        {
            if (p != null)
            {
                p.OnSlotsCleared(slotsToClear);
            }
        }

        yield return new WaitForSeconds(0.4f);

        foreach (var s in slotsToClear)
        {
            if (s == null) continue;
            s.currentPiece = null;
            s.SetHighlight(false);
        }

        comboCount++;
        float comboMult = 1f + comboMultiplierStep * (comboCount - 1);
        
        // YENİ: Satır sayısına göre bonus çarpan (1 satır=1x, 2 satır=2x, 3 satır=3x)
        int rowMultiplier = rowsCleared.Count;
        int gained = Mathf.RoundToInt(baseRowPoints * rowsCleared.Count * rowMultiplier * comboMult);
        score += gained;

        Debug.Log($"🎉 {rowsCleared.Count} row(s) cleared! +{gained} points (row x{rowMultiplier}, combo x{comboMult:F2}). Total score: {score}");

        if (hud != null) hud.UpdateHUD(score, GetTargetScoreForCurrentArena());

        ShowRowClearText(rowsCleared.Count, comboCount);

        CheckTrayEmpty();

        if (IsBoardFull())
        {
            Debug.Log("🎉 BOARD TAMAMEN DOLU! KAZANDIN!");
            Invoke(nameof(ShowWin), 0.5f);
            yield break;
        }

        CheckLoseCondition();
    }

    private void ShowRowClearText(int rowCount, int combo)
    {
        if (floatingTextPrefab == null) return;
        if (boardSpawner == null) return;

        Vector3 centerPos = boardSpawner.transform.position;

        GameObject textObj = Instantiate(floatingTextPrefab);
        
        Transform parent = floatingTextParent != null ? floatingTextParent : FindObjectOfType<Canvas>().transform;
        textObj.transform.SetParent(parent, false);

        FloatingText floatingText = textObj.GetComponent<FloatingText>();
        if (floatingText != null)
        {
            string message = "";
            Color color = Color.white;

            if (combo > 1)
            {
                message = $"COMBO x{combo}!";
                color = new Color(1f, 0.5f, 0f); // Turuncu
            }
            else if (rowCount >= 3)
            {
                message = $"AMAZING! x{rowCount}";
                color = new Color(1f, 0.2f, 0.8f); // Pembe
            }
            else if (rowCount == 2)
            {
                message = $"DOUBLE! x{rowCount}";
                color = new Color(0f, 0.8f, 1f); // Mavi
            }
            else
            {
                message = "PERFECT!";
                color = new Color(0f, 1f, 0.5f); // Yeşil
            }

            floatingText.Show(message, color, centerPos);
        }
    }

    private void CheckTrayEmpty()
    {
        if (traySpawner == null) return;

        int remainingPieces = 0;

        foreach (var piece in traySpawner.spawnedPieces)
        {
            if (piece != null && piece.transform.parent == traySpawner.transform)
            {
                remainingPieces++;
            }
        }

        Debug.Log($"📦 Tray'de kalan piece: {remainingPieces}");

        if (remainingPieces == 0)
        {
            Debug.Log("🔄 Tray boş! Yeni piece'ler geliyor...");
            Invoke(nameof(SpawnNewPieces), respawnDelay);
        }
    }

    private bool IsBoardFull()
    {
        if (boardSpawner == null || boardSpawner.spawnedSlots == null) return false;

        int filledSlots = 0;

        foreach (var slot in boardSpawner.spawnedSlots)
        {
            if (slot == null) continue;

            if (slot.IsOccupied)
                filledSlots++;
        }

        Debug.Log($"📊 Dolu slot: {filledSlots}/{totalSlots}");

        return filledSlots >= totalSlots;
    }

    private void CheckLoseCondition()
    {
        if (traySpawner == null || traySpawner.spawnedPieces == null) return;
        if (boardSpawner == null || boardSpawner.spawnedSlots == null) return;

        List<PuzzlePiece> activePieces = new List<PuzzlePiece>();
        foreach (var piece in traySpawner.spawnedPieces)
        {
            if (piece != null && piece.transform.parent == traySpawner.transform)
            {
                activePieces.Add(piece);
            }
        }

        if (activePieces.Count == 0)
        {
            Debug.Log("📦 Tray boş - yeni parçalar gelecek, lose kontrolü atlandı");
            return;
        }

        bool anyPieceCanBePlaced = false;

        foreach (var piece in activePieces)
        {
            if (piece == null || piece.shapeData == null) continue;

            if (CanPieceBePlacedAnywhere(piece))
            {
                anyPieceCanBePlaced = true;
                break;
            }
        }

        if (!anyPieceCanBePlaced)
        {
            Debug.Log("💀 GAME OVER - Hiçbir parça yerleştirilemiyor!");
            Invoke(nameof(ShowLose), 0.5f);
        }
        else
        {
            Debug.Log("✅ En az bir parça yerleştirilebilir - oyun devam ediyor");
        }
    }

    private bool CanPieceBePlacedAnywhere(PuzzlePiece piece)
    {
        if (piece == null || piece.shapeData == null) return false;

        List<Vector2Int> cells = piece.shapeData.GetCells();
        if (cells.Count == 0) return false;

        int rows = boardSpawner.rows;
        int cols = boardSpawner.cols;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                SlotCell anchorSlot = boardSpawner.GetSlotAt(r, c);
                if (anchorSlot == null) continue;

                bool canPlace = true;

                foreach (var cellOffset in cells)
                {
                    int targetRow = r + cellOffset.y;
                    int targetCol = c + cellOffset.x;

                    if (targetRow < 0 || targetRow >= rows || targetCol < 0 || targetCol >= cols)
                    {
                        canPlace = false;
                        break;
                    }

                    SlotCell targetSlot = boardSpawner.GetSlotAt(targetRow, targetCol);

                    if (targetSlot == null || targetSlot.IsOccupied)
                    {
                        canPlace = false;
                        break;
                    }
                }

                if (canPlace)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void SpawnNewPieces()
    {
        if (traySpawner == null) return;

        traySpawner.SpawnPieces();

        int spawnCount = traySpawner.spawnPoints != null ? traySpawner.spawnPoints.Count : 0;
        Debug.Log($"🎲 Yeni piece'ler spawn edildi: {spawnCount} adet");

        Invoke(nameof(CheckLoseCondition), 0.1f);
    }

    private void ShowWin()
    {
        if (hud != null) hud.UpdateHUD(score, GetTargetScoreForCurrentArena());
        if (winPanel == null)
        {
            Debug.LogError("❌ WIN PANEL NULL!");
            return;
        }

        winPanel.SetActive(true);
        Debug.Log("🎊 WIN PANEL AÇILDI!");
    }

    private void ShowLose()
    {
        if (hud != null) hud.UpdateHUD(score, GetTargetScoreForCurrentArena());
        
        if (losePanel == null)
        {
            Debug.LogError("❌ LOSE PANEL NULL!");
            return;
        }

        losePanel.SetActive(true);
        Debug.Log("💀 LOSE PANEL AÇILDI!");
    }

    private int GetTargetScoreForCurrentArena()
    {
        return 5000;
    }

    [ContextMenu("Debug Fill First Row (editor/runtime)")]
    public void Debug_FillFirstRow()
    {
        if (boardSpawner == null || boardSpawner.spawnedSlots == null)
        {
            Debug.LogWarning("BoardSpawner ya da spawnedSlots yok.");
            return;
        }

        int r = 0;
        for (int c = 0; c < boardSpawner.cols; c++)
        {
            var slot = boardSpawner.GetSlotAt(r, c);
            if (slot == null) continue;
            if (slot.currentPiece == null)
            {
                GameObject go = new GameObject($"DBG_P_{r}_{c}");
                var dummy = go.AddComponent<PuzzlePiece>();
                Transform parentTransform = slot.snapPoint != null ? (Transform)slot.snapPoint : slot.transform;
                go.transform.SetParent(parentTransform, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = Vector2.zero;

                slot.currentPiece = dummy;
                slot.SetHighlight(true);
            }
        }

        Debug.Log("🔧 Debug: First row filled. Testing animated clear...");
        StartCoroutine(CheckAndClearFullRowsCoroutine());
    }

    public SlotCell GetSlotAt(int row, int col)
    {
        if (boardSpawner == null || boardSpawner.spawnedSlots == null) return null;

        foreach (var slot in boardSpawner.spawnedSlots)
        {
            if (slot != null && slot.row == row && slot.col == col)
            {
                return slot;
            }
        }

        return null;
    }
}