using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton — persists between scenes. Manages background music and SFX volume.
/// LevelTracks[0] plays on build index 1, LevelTracks[1] on build index 2, etc.
/// Place one instance in each level scene; duplicates are destroyed on load.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music clips — assign in Inspector (index matches level build index - 1)")]
    public AudioClip[] LevelTracks;

    [Header("Volume defaults")]
    [Range(0f, 1f)] public float MusicVolume = 1f;
    [Range(0f, 1f)] public float SfxVolume   = 1f;

    AudioSource _musicSource;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _musicSource              = gameObject.AddComponent<AudioSource>();
            _musicSource.loop         = true;
            _musicSource.playOnAwake  = false;
            _musicSource.volume       = MusicVolume;
            _musicSource.spatialBlend = 0f;   // 2D

            SceneManager.sceneLoaded += OnSceneLoaded;
            PlayTrackForScene(SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayTrackForScene(scene.buildIndex);
    }

    void PlayTrackForScene(int buildIndex)
    {
        if (LevelTracks == null || LevelTracks.Length == 0) return;

        // Index 0 = MainMenu: stop music there
        if (buildIndex == 0)
        {
            _musicSource.Stop();
            return;
        }

        // Level indices start at 1; map to array with wraparound
        int trackIdx = (buildIndex - 1) % LevelTracks.Length;
        AudioClip clip = LevelTracks[trackIdx];

        if (clip == null) return;
        if (_musicSource.clip == clip && _musicSource.isPlaying) return;

        _musicSource.clip = clip;
        _musicSource.Play();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Volume API — called from MainMenuController and PauseMenuController
    // ─────────────────────────────────────────────────────────────────────────

    public void SetMusicVolume(float v)
    {
        MusicVolume = v;
        if (_musicSource) _musicSource.volume = v;
    }

    public void SetSfxVolume(float v)
    {
        SfxVolume = v;
    }
}
