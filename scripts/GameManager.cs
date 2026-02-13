using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; }

    [Header("Scene Names")]
    public string mainMenuScene = "MainMenu";
    public string gameScene = "SampleScene";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Sahneye göre state belirle
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == mainMenuScene)
            State = GameState.MainMenu;
        else
            State = GameState.Playing;
    }

    // Ana menüden oyuna geç
    public void StartGame()
    {
        State = GameState.Playing;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1); // Bir sonraki sahneye geç
    }

    // Oyunu duraklat
    public void PauseGame()
    {
        if (State != GameState.Playing) return;
        
        State = GameState.Paused;
        Time.timeScale = 0f;
    }

    // Oyuna devam et
    public void ResumeGame()
    {
        if (State != GameState.Paused) return;
        
        State = GameState.Playing;
        Time.timeScale = 1f;
    }

    // Oyun bitti
    public void GameOver()
    {
        State = GameState.GameOver;
        // Time.timeScale = 0f; // Bunu yapma, UI çalışmasını engeller
        Debug.Log("💀 Game Over!");
    }

    // Ana menüye dön
    public void GoToMainMenu()
    {
        State = GameState.MainMenu;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    // Oyunu yeniden başlat
    public void RestartGame()
    {
        State = GameState.Playing;
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameScene);
    }

    // Oyundan çık
    public void QuitGame()
    {
        Debug.Log("👋 Oyundan çıkılıyor...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}