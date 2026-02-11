using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("UI References (TextMeshPro)")]
    public TMP_Text scoreText;
    public Image progressFill; // Image set to Filled

    [Header("Defaults")]
    public int targetScore = 5000;

    public void UpdateHUD(int score, int target)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
        else
            Debug.LogWarning("HUDController: scoreText is not assigned!");

        int t = target > 0 ? target : targetScore;
        if (progressFill != null && t > 0)
        {
            progressFill.fillAmount = Mathf.Clamp01((float)score / (float)t);
        }
        else if (progressFill == null)
        {
            // progressFill optional — sadece uyarı, değilse sorun değil
            // Debug.LogWarning("HUDController: progressFill is not assigned!");
        }
    }
}