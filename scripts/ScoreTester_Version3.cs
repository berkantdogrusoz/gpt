using UnityEngine;

public class ScoreTester : MonoBehaviour
{
    public int addAmount = 100;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (PuzzleManager.Instance != null)
            {
                PuzzleManager.Instance.score += addAmount;
                if (PuzzleManager.Instance.hud != null)
                {
                    PuzzleManager.Instance.hud.UpdateHUD(PuzzleManager.Instance.score, 5000);
                }
                Debug.Log($"[ScoreTester] Added {addAmount}. New score: {PuzzleManager.Instance.score}");
            }
            else
            {
                Debug.LogWarning("[ScoreTester] PuzzleManager.Instance null");
            }
        }
    }
}