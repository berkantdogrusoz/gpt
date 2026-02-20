using UnityEngine;

public class V2SwapInputController : MonoBehaviour
{
    public V2MatchBoardManager board;
    public Camera uiCamera;
    public RectTransform boardRect;

    [Header("Input")]
    public bool allowTapToSwap = true;
    public float dragThreshold = 24f;

    private Vector2Int? first;
    private bool pointerDown;
    private Vector2 pointerDownScreen;
    private Vector2Int pointerDownCell;

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
    }

    private void AutoAssignReferences()
    {
        if (board == null)
            board = FindFirstObjectByType<V2MatchBoardManager>();

        if (boardRect == null && board != null)
            boardRect = board.boardRoot;

        if (uiCamera == null)
            uiCamera = Camera.main;
    }

    private void Update()
    {
        if (board == null)
            return;

        if (Input.GetMouseButtonDown(0))
            OnPointerDown(Input.mousePosition);

        if (Input.GetMouseButtonUp(0))
            OnPointerUp(Input.mousePosition);
    }

    private void OnPointerDown(Vector2 screenPos)
    {
        pointerDown = ResolveCell(screenPos, out pointerDownCell);
        pointerDownScreen = screenPos;

        if (pointerDown)
            Debug.Log($"[V2Input] PointerDown cell: ({pointerDownCell.x}, {pointerDownCell.y})");
    }

    private void OnPointerUp(Vector2 screenPos)
    {
        if (board == null || boardRect == null) return;

        if (!pointerDown)
        {
            if (allowTapToSwap)
                OnTap(screenPos);
            return;
        }

        Vector2 delta = screenPos - pointerDownScreen;
        if (delta.magnitude < dragThreshold)
        {
            if (allowTapToSwap)
                OnTap(screenPos);
            pointerDown = false;
            return;
        }

        Vector2Int dir;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            dir = new Vector2Int(0, delta.x > 0f ? 1 : -1);
        else
            dir = new Vector2Int(delta.y > 0f ? -1 : 1, 0);

        Vector2Int target = pointerDownCell + dir;

        Debug.Log($"[V2Input] Drag swap: {pointerDownCell} -> {target}, delta={delta}");
        board.TrySwap(pointerDownCell, target);

        first = null;
        pointerDown = false;
    }

    private void OnTap(Vector2 screenPos)
    {
        if (!ResolveCell(screenPos, out Vector2Int cell))
            return;

        Debug.Log($"[V2Input] Tap cell: ({cell.x}, {cell.y})");

        if (!first.HasValue)
        {
            first = cell;
            Debug.Log($"[V2Input] First selected: ({cell.x}, {cell.y})");
            return;
        }

        if (first.Value == cell)
        {
            Debug.Log($"[V2Input] Same cell selected twice: {cell}. Waiting for neighbor.");
            return;
        }

        Debug.Log($"[V2Input] Tap swap: {first.Value} -> {cell}");
        board.TrySwap(first.Value, cell);
        first = null;
    }

    private bool ResolveCell(Vector2 screenPos, out Vector2Int cell)
    {
        cell = default;

        if (board == null || boardRect == null)
            return false;

        int rows = board.Rows;
        int cols = board.Cols;

        // Öncelik: ekrandaki gerçek tile pozisyonuna en yakın hücreyi al.
        if (board.TryGetClosestCellFromScreen(screenPos, uiCamera, out cell))
            return true;

        // Fallback: klasik rect tabanlı çözüm.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPos, uiCamera, out var local))
            return false;

        Rect rect = boardRect.rect;
        int r;
        int c;

        if (rect.width > 1f && rect.height > 1f)
        {
            float x = local.x - rect.xMin;
            float y = rect.yMax - local.y;

            c = Mathf.Clamp(Mathf.FloorToInt(x / (rect.width / cols)), 0, cols - 1);
            r = Mathf.Clamp(Mathf.FloorToInt(y / (rect.height / rows)), 0, rows - 1);
        }
        else
        {
            Vector2 anchor = board.GetBoardAnchor();
            float cellSize = Mathf.Max(1f, board.CellSize);

            c = Mathf.Clamp(Mathf.RoundToInt((local.x / cellSize) + anchor.x), 0, cols - 1);
            r = Mathf.Clamp(Mathf.RoundToInt(anchor.y - (local.y / cellSize)), 0, rows - 1);
        }

        cell = new Vector2Int(r, c);
        return true;
    }
}