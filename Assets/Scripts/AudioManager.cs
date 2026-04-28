using UnityEngine;
using UnityEngine.SceneManagement;

// Singleton — persists between scenes, manages music and SFX volume.
// LevelTracks[0] plays on build index 1, [1] on index 2, etc. (wraps around).
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music clips — assign in Inspector (index matches level build index - 1)")]
    public AudioClip[] LevelTracks;

    [Header("Volume defaults")]
    [Range(0f, 1f)] public float MusicVolume = 1f;
    [Range(0f, 1f)] public float SfxVolume   = 1f;

    AudioSource _musicSource;

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

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayTrackForScene(scene.buildIndex);
    }

    void PlayTrackForScene(int buildIndex)
    {
        if (LevelTracks == null || LevelTracks.Length == 0) return;

        // Index 0 is the main menu — no music there
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
