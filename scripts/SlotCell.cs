using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SlotCell : MonoBehaviour
{
    public int row;
    public int col;

    public RectTransform snapPoint;
    
    [Header("Border Only")]
    public Outline outline;
    public Color borderColor = Color.black;
    public Vector2 borderThickness = new Vector2(2, -2);

    [HideInInspector] public PuzzlePiece currentPiece;

    public bool IsOccupied => currentPiece != null;

    private void Awake()
    {
        if (snapPoint == null)
            snapPoint = GetComponent<RectTransform>();

        if (outline == null)
            outline = GetComponent<Outline>();
    }

    private void Start()
    {
        SetupBorder();
        UpdateVisuals();
    }

    private void SetupBorder()
    {
        if (outline != null)
        {
            outline.effectColor = borderColor;
            outline.effectDistance = borderThickness;
        }
    }

    public void SetHighlight(bool on)
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (outline == null) return;
        outline.enabled = true;
    }

    // ✅ ESKİ AnimateDestroy - Slot animasyonu yok, sadece delay
    public IEnumerator AnimateDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Artık animasyon yok, sadece bekleme
    }
}