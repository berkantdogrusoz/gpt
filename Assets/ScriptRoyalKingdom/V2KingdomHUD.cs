using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("Navigation (Win Panel)")]
    public string kingdomMenuSceneName = "KingdomMenuScene";
    public string nextLevelSceneName = "KingdomLevelScene";

    [Header("Flow") ]
    public bool pauseGameOnResult = true;

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
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            winPanel.transform.SetAsLastSibling();
            EnsurePanelInteraction(winPanel);
        }

        if (pauseGameOnResult)
            Time.timeScale = 0f;
    }

    public void ShowLose()
    {
        if (losePanel != null)
        {
            losePanel.SetActive(true);
            losePanel.transform.SetAsLastSibling();
            EnsurePanelInteraction(losePanel);
        }

        if (pauseGameOnResult)
            Time.timeScale = 0f;
    }

    public void GoToKingdomMenu()
    {
        if (pauseGameOnResult)
            Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(kingdomMenuSceneName))
            SceneManager.LoadScene(kingdomMenuSceneName);
    }

    public void GoToNextLevel()
    {
        if (pauseGameOnResult)
            Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(nextLevelSceneName))
            SceneManager.LoadScene(nextLevelSceneName);
    }

    private void OnDisable()
    {
        if (pauseGameOnResult)
            Time.timeScale = 1f;
    }

    private void EnsurePanelInteraction(GameObject panel)
    {
        if (panel == null)
            return;

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = panel.AddComponent<CanvasGroup>();

        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }
}