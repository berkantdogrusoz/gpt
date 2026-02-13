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
                PuzzleManager.Instance.AddScore(addAmount);
                Debug.Log($"[ScoreTester] Added {addAmount}. New score: {PuzzleManager.Instance.score}");
            }
            else
            {
                Debug.LogWarning("[ScoreTester] PuzzleManager.Instance null");
            }
        }
    }
}