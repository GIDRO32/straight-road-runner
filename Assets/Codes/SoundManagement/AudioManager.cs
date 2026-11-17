using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays sound effects at the saved SFX volume (VolumeSettings).
/// One per scene: the newest one becomes Instance, so each scene's own settings slider keeps working.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Source")]
    public AudioSource sfxSource;

    [Header("Volume Settings")]
    [Tooltip("Optional: legacy slider hook (AdjustVolume). A VolumeSlider component on the slider does the same.")]
    public Slider sfxVolumeSlider;

    public float sfxVolume => VolumeSettings.Sfx;

    void Awake()
    {
        Instance = this;

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(VolumeSettings.Sfx);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Called by the SFX slider's On Value Changed
    public void AdjustVolume()
    {
        if (sfxVolumeSlider != null)
            VolumeSettings.Sfx = sfxVolumeSlider.value;
    }

    public void PlayRandomClip(AudioClip[] clips, float volumeScale = 1f)
    {
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("No audio clips provided!");
            return;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        sfxSource.PlayOneShot(clip, VolumeSettings.Sfx * volumeScale);
    }

    public void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("Audio clip is null!");
            return;
        }

        sfxSource.PlayOneShot(clip, VolumeSettings.Sfx * volumeScale);
    }
}
