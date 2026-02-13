using UnityEngine;
using UnityEngine.UI;

public class V2Tile : MonoBehaviour
{
    [HideInInspector] public int row;
    [HideInInspector] public int col;
    [HideInInspector] public int colorId;

    [Header("Visual")]
    public Image icon;
    public Color[] palette;

    public void SetData(int r, int c, int color, Color[] sourcePalette)
    {
        row = r;
        col = c;
        colorId = color;
        palette = sourcePalette;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (icon == null || palette == null || palette.Length == 0) return;
        int idx = Mathf.Clamp(colorId, 0, palette.Length - 1);
        icon.color = palette[idx];
    }
}
