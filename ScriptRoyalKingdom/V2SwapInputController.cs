using UnityEngine;

public class V2SwapInputController : MonoBehaviour
{
    public V2MatchBoardManager board;
    public Camera uiCamera;
    public RectTransform boardRect;

    private Vector2Int? first;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            OnBoardClick(Input.mousePosition);
    }

    public void OnBoardClick(Vector2 screenPos)
    {
        if (board == null || boardRect == null) return;

        int rows = board.Rows;
        int cols = board.Cols;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPos, uiCamera, out var local))
            return;

        Rect rect = boardRect.rect;
        float x = local.x - rect.xMin;
        float y = rect.yMax - local.y;

        int c = Mathf.Clamp(Mathf.FloorToInt(x / (rect.width / cols)), 0, cols - 1);
        int r = Mathf.Clamp(Mathf.FloorToInt(y / (rect.height / rows)), 0, rows - 1);

        Vector2Int cell = new Vector2Int(r, c);

        Debug.Log($"[V2Input] Clicked cell: ({r}, {c})");

        if (!first.HasValue)
        {
            first = cell;
            Debug.Log($"[V2Input] First selected: ({r}, {c})");
            return;
        }

        Debug.Log($"[V2Input] Trying swap: {first.Value} -> {cell}");
        board.TrySwap(first.Value, cell);
        first = null;
    }
}