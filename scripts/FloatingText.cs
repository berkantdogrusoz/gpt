using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FloatingText : MonoBehaviour
{
    public TMP_Text textComponent;
    
    [Header("Animation Settings")]
    public float duration = 1.5f;
    public float startScale = 0.5f;
    public float maxScale = 1.2f;
    public float fadeStartDelay = 0.3f; // Ne kadar süre sonra fade başlasın
    public Vector2 moveOffset = new Vector2(0, 50f); // Yukarı doğru hareket

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (textComponent == null)
            textComponent = GetComponent<TMP_Text>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Show(string text, Color color, Vector3 worldPosition)
    {
        if (textComponent != null)
        {
            textComponent.text = text;
            textComponent.color = color;
        }

        // World position'ı screen position'a çevir
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && rectTransform != null)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), 
                screenPoint, 
                canvas.worldCamera, 
                out Vector2 localPoint
            );
            rectTransform.anchoredPosition = localPoint;
        }

        StartCoroutine(AnimateText());
    }

    private IEnumerator AnimateText()
    {
        float elapsed = 0f;
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 endPos = startPos + moveOffset;

        rectTransform.localScale = Vector3.one * startScale;
        canvasGroup.alpha = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Scale animasyonu (büyü, sonra normal)
            float scaleT = t < 0.3f ? t / 0.3f : 1f - ((t - 0.3f) / 0.7f) * 0.2f;
            float currentScale = Mathf.Lerp(startScale, maxScale, scaleT);
            rectTransform.localScale = Vector3.one * currentScale;

            // Pozisyon animasyonu (yukarı doğru)
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            // Fade out animasyonu (belirli süre sonra)
            if (elapsed > fadeStartDelay)
            {
                float fadeT = (elapsed - fadeStartDelay) / (duration - fadeStartDelay);
                canvasGroup.alpha = 1f - fadeT;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}