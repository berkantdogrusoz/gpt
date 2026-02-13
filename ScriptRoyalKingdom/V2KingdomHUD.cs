using TMPro;
using UnityEngine;

public class V2KingdomHUD : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text scoreText;
    public TMP_Text movesText;
    public TMP_Text targetText;
    public TMP_Text bestText;

    [Header("Panels")]
    public GameObject winPanel;
    public GameObject losePanel;

    private void Start()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        int best = V2KingdomSave.GetInt("best_score", 0);
        if (bestText != null) bestText.text = $"Best: {best}";
    }

    public void SetScore(int score)
    {
        if (scoreText != null) scoreText.text = $"Score: {score}";

        int best = V2KingdomSave.GetInt("best_score", 0);
        if (score > best)
        {
            V2KingdomSave.SetInt("best_score", score);
            best = score;
        }

        if (bestText != null) bestText.text = $"Best: {best}";
    }

    public void SetMoves(int moves)
    {
        if (movesText != null) movesText.text = $"Moves: {moves}";
    }

    public void SetTarget(int target)
    {
        if (targetText != null) targetText.text = $"Target: {target}";
    }

    public void ShowWin()
    {
        if (winPanel != null) winPanel.SetActive(true);
    }

    public void ShowLose()
    {
        if (losePanel != null) losePanel.SetActive(true);
    }
}
