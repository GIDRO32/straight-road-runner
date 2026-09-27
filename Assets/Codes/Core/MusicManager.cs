using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class MusicManager : MonoBehaviour
{
    [Header("Music Folder")]
    [Tooltip("Folder path relative to StreamingAssets (e.g., 'Music/Menu' or 'Music/Regular')")]
    public string musicFolder = "Music/Menu";

    [Header("Audio Source")]
    public AudioSource musicSource;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float musicVolume = 0.7f;
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
    public Slider volumeAdjustSlider;

    // Playback state
    private List<string> trackPaths = new List<string>();
    private Queue<string> shuffledQueue = new Queue<string>();
    private AudioClip currentTrack;
    private bool isTransitioning = false;
    private bool firstTrackPlaying = false;
    private bool lostFocusPause = false;


    void Awake()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.loop = false;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;
    }

    void Start()
    {
        PlayerPrefs.GetFloat("MusicVolume", musicSource.volume);
        volumeAdjustSlider.value = musicSource.volume;
        if (playOnStart)
        {
            StartCoroutine(LoadAndPlayMusic());
        }
    }

    void Update()
    {
        // Update UI info
        currentTrackName = currentTrack != null ? currentTrack.name : "None";
        remainingTracks = shuffledQueue.Count;
        totalTracks = trackPaths.Count;

        // Check if current track finished and play next
        if (!isTransitioning && musicSource.isPlaying == false && shuffledQueue.Count > 0 && !lostFocusPause)
        {
            PlayNextTrack();
        }
        // If queue is empty but we have a playlist, refill
        else if (!isTransitioning && musicSource.isPlaying == false && trackPaths.Count > 0 && !lostFocusPause)
        {
            RefillAndShuffleQueue();
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

    public void AdjustVolume()
    {
        musicSource.volume = volumeAdjustSlider.value;
        PlayerPrefs.SetFloat("MusicVolume", volumeAdjustSlider.value);
    }
    /// <summary>
    /// Load first track and play immediately, then load rest in background
    /// </summary>
    private IEnumerator LoadAndPlayMusic()
    {
        isLoading = true;

        string fullPath = Path.Combine(Application.streamingAssetsPath, musicFolder);

        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            isLoading = false;
            yield break;
        }

        trackPaths.Clear();

        foreach (string format in supportedFormats)
        {
            trackPaths.AddRange(Directory.GetFiles(fullPath, "*" + format));
        }

        if (trackPaths.Count == 0)
        {
            isLoading = false;
            yield break;
        }

        RefillAndShuffleQueue();
        yield return PlayNextTrackAsync();

        isLoading = false;
    }
    private IEnumerator PlayNextTrackAsync()
    {
        if (shuffledQueue.Count == 0)
        {
            RefillAndShuffleQueue();
        }

        string nextPath = shuffledQueue.Dequeue();
        string uri = "file://" + nextPath;

        AudioType audioType = GetAudioType(nextPath);

        using (UnityWebRequest www =
            UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
        {
            ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }

            if (musicSource.clip != null)
            {
                Destroy(musicSource.clip);
            }

            currentTrack = DownloadHandlerAudioClip.GetContent(www);
            currentTrack.name = Path.GetFileNameWithoutExtension(nextPath);

            musicSource.clip = currentTrack;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
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

    /// <summary>
    /// Reload music from folder
    /// </summary>
    public void ReloadMusic()
    {
        StopMusic();
        trackPaths.Clear();
        shuffledQueue.Clear();
        currentTrack = null;
        firstTrackPlaying = false;

        StartCoroutine(LoadAndPlayMusic());
    }

    /// <summary>
    /// Refill the queue with all tracks and shuffle them
    /// </summary>
    private void RefillAndShuffleQueue()
    {
        shuffledQueue.Clear();

        List<string> shuffled = new List<string>(trackPaths);

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        foreach (string path in shuffled)
        {
            shuffledQueue.Enqueue(path);
        }
    }

    /// <summary>
    /// Play the next track in the queue
    /// </summary>
    private void PlayNextTrack()
    {
        StartCoroutine(PlayNextTrackAsync());
    }


    /// <summary>
    /// Crossfade to the next track
    /// </summary>
    private IEnumerator CrossfadeToNextTrack()
    {
        isTransitioning = true;

        // Fade out current track
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < crossfadeDuration / 2f)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (crossfadeDuration / 2f));
            yield return null;
        }

        // Play next track
        PlayNextTrack();

        // Fade in new track
        elapsed = 0f;
        while (elapsed < crossfadeDuration / 2f)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0f, musicVolume, elapsed / (crossfadeDuration / 2f));
            yield return null;
        }

        musicSource.volume = musicVolume;
        isTransitioning = false;
    }

    /// <summary>
    /// Skip to next track immediately
    /// </summary>
    public void SkipTrack()
    {
        if (shuffledQueue.Count == 0)
        {
            RefillAndShuffleQueue();
        }

        if (crossfade)
        {
            StartCoroutine(CrossfadeToNextTrack());
        }
        else
        {
            PlayNextTrack();
        }
    }

    /// <summary>
    /// Stop music playback
    /// </summary>
    public void StopMusic()
    {
        musicSource.Stop();
        currentTrack = null;
    }

    /// <summary>
    /// Pause music playback
    /// </summary>
    public void PauseMusic()
    {
        musicSource.Pause();
    }

    /// <summary>
    /// Resume music playback
    /// </summary>
    public void ResumeMusic()
    {
        musicSource.UnPause();
    }

    /// <summary>
    /// Set music volume
    /// </summary>
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        musicSource.volume = musicVolume;
    }

    /// <summary>
    /// Get current track name
    /// </summary>
    public string GetCurrentTrackName()
    {
        return currentTrack != null ? currentTrack.name : "None";
    }

    /// <summary>
    /// Get remaining tracks in current queue
    /// </summary>
    public int GetRemainingTracks()
    {
        return shuffledQueue.Count;
    }

    /// <summary>
    /// Get total tracks loaded
    /// </summary>
    public int GetTotalTracks()
    {
        return trackPaths.Count;
    }

    /// <summary>
    /// Check if music is currently loading
    /// </summary>
    public bool IsLoading()
    {
        return isLoading;
    }
}