using UnityEngine;

public class V2SwapInputController : MonoBehaviour
{
    public V2MatchBoardManager board;
    public Camera uiCamera;
    public RectTransform boardRect;
    public int rows = 8;
    public int cols = 8;

    private Vector2Int? first;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            OnBoardClick(Input.mousePosition);
    }

    public void OnBoardClick(Vector2 screenPos)
    {
        if (board == null || boardRect == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPos, uiCamera, out var local))
            return;

        Rect rect = boardRect.rect;
        float x = local.x - rect.xMin;
        float y = rect.yMax - local.y;

        int c = Mathf.Clamp(Mathf.FloorToInt(x / (rect.width / cols)), 0, cols - 1);
        int r = Mathf.Clamp(Mathf.FloorToInt(y / (rect.height / rows)), 0, rows - 1);

        Vector2Int cell = new Vector2Int(r, c);

        if (!first.HasValue)
        {
            first = cell;
            return;
        }

        board.TrySwap(first.Value, cell);
        first = null;
    }
}
