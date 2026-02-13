using TMPro;
using UnityEngine;

public class V2KingdomHUD : MonoBehaviour
{
    public TMP_Text scoreText;
    public TMP_Text movesText;
    public TMP_Text targetText;

    public void SetScore(int score)
    {
        if (scoreText != null) scoreText.text = $"Score: {score}";
    }

    public void SetMoves(int moves)
    {
        if (movesText != null) movesText.text = $"Moves: {moves}";
    }

    public void SetTarget(int target)
    {
        if (targetText != null) targetText.text = $"Target: {target}";
    }
}
