using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuMusicManager : MonoBehaviour
{
    public static MenuMusicManager Instance { get; private set; }

    [Header("Menu Music")]
    public AudioSource musicSource;
    public AudioClip menuMusicClip;
    [Range(0f, 1f)] public float menuMusicVolume = 0.8f;
    public bool playOnStart = true;
    public bool loop = true;

    [Header("Scene Filter")]
    public bool playOnlyOnMainMenuScene = true;
    public string mainMenuSceneName = "MainMenu";
    public bool sceneNameCaseInsensitive = true;

    [Header("Settings UI (Optional)")]
    public Toggle musicToggle;
    public Slider musicVolumeSlider;

    [Header("Debug")]
    public bool verboseLogs = true;

    private const string MusicEnabledKey = "music_enabled";
    private const string MusicVolumeKey = "music_volume";

    public bool IsMusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSource();
        LoadSettings();
        TryAdoptClipFromSource();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        BindOptionalUI();

        if (playOnStart)
            RefreshPlaybackByScene(SceneManager.GetActiveScene().name);
    }

    private void OnValidate()
    {
        menuMusicVolume = Mathf.Clamp01(menuMusicVolume);

        if (musicSource != null)
        {
            musicSource.loop = loop;
            musicSource.spatialBlend = 0f;
            musicSource.volume = menuMusicVolume;
        }
    }

    private void EnsureAudioSource()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
                musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = loop;
        musicSource.spatialBlend = 0f;
        musicSource.mute = false;
        musicSource.volume = Mathf.Clamp01(menuMusicVolume);
    }

    private void TryAdoptClipFromSource()
    {
        if (menuMusicClip == null && musicSource != null && musicSource.clip != null)
        {
            menuMusicClip = musicSource.clip;
            if (verboseLogs)
                Debug.Log("🎵 MenuMusicManager: menuMusicClip boştu, AudioSource.clip alındı.");
        }
    }

    private void LoadSettings()
    {
        if (!PlayerPrefs.HasKey(MusicEnabledKey))
            PlayerPrefs.SetInt(MusicEnabledKey, 1);

        if (!PlayerPrefs.HasKey(MusicVolumeKey))
            PlayerPrefs.SetFloat(MusicVolumeKey, menuMusicVolume);

        menuMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, menuMusicVolume));
        ApplyVolume();
    }

    private void BindOptionalUI()
    {
        if (musicToggle != null)
        {
            musicToggle.SetIsOnWithoutNotify(IsMusicEnabled);
            musicToggle.onValueChanged.RemoveListener(SetMusicEnabled);
            musicToggle.onValueChanged.AddListener(SetMusicEnabled);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(menuMusicVolume);
            musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshPlaybackByScene(scene.name);
    }

    private bool IsSceneAllowedForMusic(string sceneName)
    {
        if (!playOnlyOnMainMenuScene) return true;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            return sceneName.ToLowerInvariant().Contains("menu");

        if (sceneNameCaseInsensitive)
            return string.Equals(sceneName, mainMenuSceneName, System.StringComparison.OrdinalIgnoreCase);

        return sceneName == mainMenuSceneName;
    }

    private void RefreshPlaybackByScene(string sceneName)
    {
        bool sceneAllowed = IsSceneAllowedForMusic(sceneName);

        if (!sceneAllowed)
        {
            if (verboseLogs)
                Debug.Log($"🎵 MenuMusicManager: '{sceneName}' menü sahnesi değil, müzik durduruldu.");
            StopMusic();
            return;
        }

        if (!IsMusicEnabled)
        {
            if (verboseLogs)
                Debug.Log("🎵 MenuMusicManager: Müzik ayarlardan kapalı.");
            StopMusic();
            return;
        }

        if (menuMusicClip == null)
        {
            if (verboseLogs)
                Debug.LogWarning("🎵 MenuMusicManager: menuMusicClip atanmamış.");
            StopMusic();
            return;
        }

        PlayMusic();
    }

    public void PlayMusic()
    {
        if (musicSource == null || menuMusicClip == null || !IsMusicEnabled) return;

        if (musicSource.clip != menuMusicClip)
            musicSource.clip = menuMusicClip;

        musicSource.loop = loop;
        ApplyVolume();

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
            if (verboseLogs)
                Debug.Log($"🎵 MenuMusicManager: Çalıyor -> {menuMusicClip.name}");
        }
    }

    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Stop();
    }

    public void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (enabled)
            RefreshPlaybackByScene(SceneManager.GetActiveScene().name);
        else
            StopMusic();
    }

    public void ToggleMusic()
    {
        SetMusicEnabled(!IsMusicEnabled);
    }

    public void SetMusicVolume(float value)
    {
        menuMusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, menuMusicVolume);
        PlayerPrefs.Save();
        ApplyVolume();
    }

    public void ResetMusicSettingsToDefault()
    {
        PlayerPrefs.SetInt(MusicEnabledKey, 1);
        PlayerPrefs.SetFloat(MusicVolumeKey, 0.8f);
        PlayerPrefs.Save();

        menuMusicVolume = 0.8f;
        ApplyVolume();

        if (musicToggle != null)
            musicToggle.SetIsOnWithoutNotify(true);

        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(menuMusicVolume);

        RefreshPlaybackByScene(SceneManager.GetActiveScene().name);
    }

    private void ApplyVolume()
    {
        if (musicSource != null)
            musicSource.volume = menuMusicVolume;
    }
}
