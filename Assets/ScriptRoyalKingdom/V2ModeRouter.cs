using UnityEngine;
using UnityEngine.SceneManagement;

public class V2ModeRouter : MonoBehaviour
{
    [Header("Scene Names")]
    public string classicSceneName = "SampleScene";
    public string kingdomMenuSceneName = "KingdomMenuScene";
    public string kingdomLevelSceneName = "KingdomLevelScene";

    public void OpenClassicMode()
    {
        SceneManager.LoadScene(classicSceneName);
    }

    public void OpenKingdomMenu()
    {
        SceneManager.LoadScene(kingdomMenuSceneName);
    }

    public void StartKingdomLevel()
    {
        SceneManager.LoadScene(kingdomLevelSceneName);
    }
}
