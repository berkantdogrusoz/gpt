using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("UI References (TextMeshPro)")]
    public TMP_Text scoreText;
    public TMP_Text highScoreText;
    public Image progressFill; // Image set to Filled

    [Header("Power UI")]
    public Image powerFill; // Hammer power dolumu
    public GameObject powerReadyFx; // Dolu olduğunda parlayacak obje
    public TMP_Text powerText; // Opsiyonel: % veya READY

    [Header("High Score")]
    public string highScoreKey = "high_score";

    [Header("Defaults")]
    public int targetScore = 5000;

    private int cachedHighScore;

    private void Awake()
    {
        cachedHighScore = Mathf.Max(0, PlayerPrefs.GetInt(highScoreKey, 0));
    }

    public void UpdateHUD(int score, int target)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
        else
            Debug.LogWarning("HUDController: scoreText is not assigned!");

        UpdateAndShowHighScore(score);

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

    private void UpdateAndShowHighScore(int currentScore)
    {
        if (currentScore > cachedHighScore)
        {
            cachedHighScore = currentScore;
            PlayerPrefs.SetInt(highScoreKey, cachedHighScore);
            PlayerPrefs.Save();
        }

        if (highScoreText != null)
            highScoreText.text = $"Best: {cachedHighScore}";
    }

    public void UpdatePowerUI(float normalized, bool ready)
    {
        float n = Mathf.Clamp01(normalized);

        if (powerFill != null)
            powerFill.fillAmount = n;

        if (powerReadyFx != null)
            powerReadyFx.SetActive(ready);

        if (powerText != null)
            powerText.text = ready ? "HAMMER READY" : $"POWER {(int)(n * 100f)}%";
    }
}
