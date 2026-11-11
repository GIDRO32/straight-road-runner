using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Plays every song in a StreamingAssets folder as a shuffled playlist.
/// When the playlist ends it is reshuffled, and the new first song is never the one that just played.
/// Volume comes from the saved setting (VolumeSettings), so it survives new songs and scene changes.
/// </summary>
public class MusicManager : MonoBehaviour
{
    [Header("Music Folder")]
    [Tooltip("Folder path relative to StreamingAssets (e.g., 'Music/Menu' or 'Music/Regular')")]
    public string musicFolder = "Music/Menu";

    [Tooltip("In stage scenes: use the current stage's music folder (StageDefinition.musicFolder) if it has one")]
    public bool useStageMusicFolder = false;

    [Header("Audio Source")]
    public AudioSource musicSource;

    [Header("Settings")]
    public bool crossfade = true;
    public float crossfadeDuration = 2f;
    public bool playOnStart = true;

    [Header("Supported Formats")]
    public string[] supportedFormats = new string[] { ".mp3", ".ogg", ".wav" };

    [Header("Current State (Read-Only)")]
    public string currentTrackName = "None";
    public int remainingTracks = 0;
    public int totalTracks = 0;
    public bool isLoading = false;
    [Tooltip("Optional: legacy slider hook (AdjustVolume). A VolumeSlider component on the slider does the same.")]
    public Slider volumeAdjustSlider;

    // Playback state
    private readonly List<string> trackPaths = new List<string>();
    private readonly List<string> playlist = new List<string>();
    private int playlistIndex = 0;
    private string lastPlayedPath;
    private AudioClip currentTrack;
    private bool isLoadingTrack = false;
    private bool isTransitioning = false;
    private bool isPaused = false;
    private bool lostFocusPause = false;
    private float fade = 1f; // Crossfade multiplier on top of the saved volume
    private bool trackStarted = false; // A song is playing and hasn't finished yet

    /// <summary>
    /// A song finished playing on its own (not skipped, stopped or switched away from).
    /// Argument: the folder it came from. BossDirector starts boss fights on it.
    /// </summary>
    public event System.Action<string> TrackFinished;

    void Awake()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.loop = false;
        musicSource.playOnAwake = false;
        ApplyVolume();
    }

    void OnEnable() => VolumeSettings.Changed += OnVolumeChanged;
    void OnDisable() => VolumeSettings.Changed -= OnVolumeChanged;

    void Start()
    {
        if (volumeAdjustSlider != null)
            volumeAdjustSlider.SetValueWithoutNotify(VolumeSettings.Music);

        if (useStageMusicFolder)
        {
            StageDefinition stage = GameSession.CurrentStage;
            if (stage != null && !string.IsNullOrEmpty(stage.musicFolder))
                musicFolder = stage.musicFolder;
        }

        if (playOnStart)
        {
            StartCoroutine(LoadAndPlayMusic());
        }
    }

    void Update()
    {
        // Update UI info
        currentTrackName = currentTrack != null ? currentTrack.name : "None";
        remainingTracks = Mathf.Max(0, playlist.Count - playlistIndex);
        totalTracks = trackPaths.Count;

        // Current song finished: next one
        if (!isLoading && !isLoadingTrack && !isTransitioning && !isPaused && !lostFocusPause
            && trackPaths.Count > 0 && !musicSource.isPlaying)
        {
            if (trackStarted)
            {
                trackStarted = false;
                string folder = musicFolder;
                TrackFinished?.Invoke(folder);
                if (folder != musicFolder) return; // A listener switched folders (e.g. boss music)
            }
            PlayNextTrack();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && musicSource.isPlaying)
        {
            lostFocusPause = true;
            musicSource.Pause();
        }
        else if (hasFocus && lostFocusPause)
        {
            lostFocusPause = false;
            musicSource.UnPause();
        }
    }

    // ===== VOLUME =====

    // Called by the music slider's On Value Changed
    public void AdjustVolume()
    {
        if (volumeAdjustSlider != null)
            VolumeSettings.Music = volumeAdjustSlider.value;
    }

    private void OnVolumeChanged(VolumeChannel channel, float volume)
    {
        if (channel == VolumeChannel.Music) ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (musicSource != null)
            musicSource.volume = VolumeSettings.Music * fade;
    }

    // ===== PLAYLIST =====

    /// <summary>Finds the songs in the folder and starts the playlist.</summary>
    private IEnumerator LoadAndPlayMusic()
    {
        isLoading = true;

        string fullPath = Path.Combine(Application.streamingAssetsPath, musicFolder);

        trackPaths.Clear();
        playlist.Clear();
        playlistIndex = 0;

        if (!Directory.Exists(fullPath))
        {
            Debug.LogWarning($"MusicManager: Music folder not found: {fullPath}");
            isLoading = false;
            yield break;
        }

        foreach (string format in supportedFormats)
        {
            trackPaths.AddRange(Directory.GetFiles(fullPath, "*" + format));
        }

        if (trackPaths.Count == 0)
        {
            isLoading = false;
            yield break;
        }

        yield return PlayNextTrackAsync(NextTrackPath());

        isLoading = false;
    }

    private string NextTrackPath()
    {
        if (playlistIndex >= playlist.Count)
            BuildPlaylist();

        return playlist[playlistIndex++];
    }

    /// <summary>Shuffles every song. The first one is never the song that just played.</summary>
    private void BuildPlaylist()
    {
        playlist.Clear();
        playlist.AddRange(trackPaths);

        for (int i = playlist.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (playlist[i], playlist[j]) = (playlist[j], playlist[i]);
        }

        if (playlist.Count > 1 && playlist[0] == lastPlayedPath)
        {
            int swap = Random.Range(1, playlist.Count);
            (playlist[0], playlist[swap]) = (playlist[swap], playlist[0]);
        }

        playlistIndex = 0;
    }

    private void PlayNextTrack()
    {
        if (trackPaths.Count == 0) return;
        StartCoroutine(PlayNextTrackAsync(NextTrackPath()));
    }

    private IEnumerator PlayNextTrackAsync(string path)
    {
        isLoadingTrack = true;
        isPaused = false;

        string uri = "file://" + path;
        AudioType audioType = GetAudioType(path);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
        {
            ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"MusicManager: Couldn't load {path}: {www.error}");
                lastPlayedPath = path; // Skip it next time round too
                isLoadingTrack = false;
                yield break;
            }

            if (musicSource.clip != null)
            {
                Destroy(musicSource.clip);
            }

            currentTrack = DownloadHandlerAudioClip.GetContent(www);
            currentTrack.name = Path.GetFileNameWithoutExtension(path);

            musicSource.clip = currentTrack;
            ApplyVolume();
            musicSource.Play();
            trackStarted = true;
            lastPlayedPath = path;
        }

        isLoadingTrack = false;
    }

    /// <summary>
    /// Get AudioType based on file extension
    /// </summary>
    private AudioType GetAudioType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();

        switch (extension)
        {
            case ".mp3":
                return AudioType.MPEG;
            case ".ogg":
                return AudioType.OGGVORBIS;
            case ".wav":
                return AudioType.WAV;
            default:
                return AudioType.UNKNOWN;
        }
    }

    // ===== CONTROLS =====

    /// <summary>
    /// Reload music from folder
    /// </summary>
    public void ReloadMusic()
    {
        StopAllCoroutines();
        StopMusic();
        isTransitioning = false;
        isLoadingTrack = false;
        fade = 1f;

        StartCoroutine(LoadAndPlayMusic());
    }

    /// <summary>
    /// Switch to another StreamingAssets folder (e.g. boss music) and start playing it
    /// </summary>
    public void PlayFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || folder == musicFolder) return;

        musicFolder = folder;
        ReloadMusic();
    }

    /// <summary>
    /// Skip to next track immediately
    /// </summary>
    public void SkipTrack()
    {
        if (isTransitioning || isLoadingTrack) return;

        if (crossfade)
            StartCoroutine(CrossfadeToNextTrack());
        else
            PlayNextTrack();
    }

    private IEnumerator CrossfadeToNextTrack()
    {
        isTransitioning = true;
        float half = crossfadeDuration / 2f;

        // Fade out current track
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            fade = 1f - t / half;
            ApplyVolume();
            yield return null;
        }

        fade = 0f;
        ApplyVolume();
        yield return PlayNextTrackAsync(NextTrackPath());

        // Fade in new track
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            fade = t / half;
            ApplyVolume();
            yield return null;
        }

        fade = 1f;
        ApplyVolume();
        isTransitioning = false;
    }

    /// <summary>
    /// Stop music playback
    /// </summary>
    public void StopMusic()
    {
        trackStarted = false;
        musicSource.Stop();
        currentTrack = null;
    }

    /// <summary>
    /// Pause music playback
    /// </summary>
    public void PauseMusic()
    {
        isPaused = true;
        musicSource.Pause();
    }

    /// <summary>
    /// Resume music playback
    /// </summary>
    public void ResumeMusic()
    {
        isPaused = false;
        musicSource.UnPause();
    }

    /// <summary>
    /// Set (and save) music volume
    /// </summary>
    public void SetVolume(float volume)
    {
        VolumeSettings.Music = volume;
    }

    public string GetCurrentTrackName()
    {
        return currentTrack != null ? currentTrack.name : "None";
    }

    public int GetRemainingTracks()
    {
        return Mathf.Max(0, playlist.Count - playlistIndex);
    }

    public int GetTotalTracks()
    {
        return trackPaths.Count;
    }

    public bool IsLoading()
    {
        return isLoading;
    }
}
