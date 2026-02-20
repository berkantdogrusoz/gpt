using UnityEngine;

public class V2AudioManager : MonoBehaviour
{
    public static V2AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Music")]
    public AudioClip gameplayMusic;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    public bool playMusicOnStart = true;

    [Header("SFX")]
    public AudioClip swapSfx;
    public AudioClip clearSfx;
    public AudioClip bombSpecialSfx;
    public AudioClip laneSpecialSfx;
    public AudioClip discoSpecialSfx;
    [Range(0f, 1f)] public float swapVolume = 1f;
    [Range(0f, 1f)] public float clearVolume = 1f;
    [Range(0f, 1f)] public float bombSpecialVolume = 1f;
    [Range(0f, 1f)] public float laneSpecialVolume = 1f;
    [Range(0f, 1f)] public float discoSpecialVolume = 1f;

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
        if (playMusicOnStart)
            PlayGameplayMusic();
    }

    public void PlayGameplayMusic()
    {
        if (musicSource == null || gameplayMusic == null)
            return;

        if (musicSource.clip == gameplayMusic && musicSource.isPlaying)
            return;

        musicSource.clip = gameplayMusic;
        musicSource.loop = true;
        musicSource.volume = Mathf.Clamp01(musicVolume);
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    public void PlaySwap()
    {
        PlaySfx(swapSfx, swapVolume);
    }

    public void PlayClear()
    {
        PlaySfx(clearSfx, clearVolume);
    }

    public void PlayBombSpecial()
    {
        PlaySfx(bombSpecialSfx, bombSpecialVolume);
    }

    public void PlayLaneSpecial()
    {
        PlaySfx(laneSpecialSfx, laneSpecialVolume);
    }

    public void PlayDiscoSpecial()
    {
        PlaySfx(discoSpecialSfx, discoSpecialVolume);
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}