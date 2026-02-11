// scripts/AudioManager.cs
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class SoundEntry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 2f)] public float pitch = 1f;
    }

    [Header("Library")]
    public List<SoundEntry> sfxLibrary = new List<SoundEntry>();
    public List<SoundEntry> musicLibrary = new List<SoundEntry>();

    [Header("Output")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;

    private readonly Dictionary<string, SoundEntry> sfxMap = new Dictionary<string, SoundEntry>();
    private readonly Dictionary<string, SoundEntry> musicMap = new Dictionary<string, SoundEntry>();

    private AudioSource sfxSource;
    private AudioSource musicSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;

        BuildMaps();
        ApplyVolumes();
    }

    private void BuildMaps()
    {
        sfxMap.Clear();
        musicMap.Clear();

        foreach (var e in sfxLibrary)
        {
            if (e == null || string.IsNullOrEmpty(e.id) || e.clip == null) continue;
            sfxMap[e.id] = e;
        }

        foreach (var e in musicLibrary)
        {
            if (e == null || string.IsNullOrEmpty(e.id) || e.clip == null) continue;
            musicMap[e.id] = e;
        }
    }

    private void ApplyVolumes()
    {
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    public void PlaySfx(string id)
    {
        if (string.IsNullOrEmpty(id) || sfxSource == null) return;

        if (!sfxMap.TryGetValue(id, out var e))
        {
            Debug.LogWarning($"AudioManager: SFX id bulunamadı -> {id}");
            return;
        }

        sfxSource.pitch = e.pitch;
        sfxSource.PlayOneShot(e.clip, e.volume * sfxVolume);
    }

    public void PlayMusic(string id, bool loop = true)
    {
        if (string.IsNullOrEmpty(id) || musicSource == null) return;

        if (!musicMap.TryGetValue(id, out var e))
        {
            Debug.LogWarning($"AudioManager: Music id bulunamadı -> {id}");
            return;
        }

        if (musicSource.clip == e.clip && musicSource.isPlaying) return;

        musicSource.clip = e.clip;
        musicSource.loop = loop;
        musicSource.pitch = e.pitch;
        musicSource.volume = e.volume * musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }
}
