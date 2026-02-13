using UnityEngine;
using UnityEngine.UI;

public class V2Tile : MonoBehaviour
{
    [HideInInspector] public int row;
    [HideInInspector] public int col;
    [HideInInspector] public int colorId;

    public Image icon;

    public void SetData(int r, int c, int id)
    {
        row = r;
        col = c;
        colorId = id;
    }

    public void SetVisual(Sprite sprite, Color tint)
    {
        if (icon == null) return;

        if (sprite != null)
            icon.sprite = sprite;

        if (tint.a <= 0.001f)
            tint.a = 1f;

        icon.color = tint;
    }
}
