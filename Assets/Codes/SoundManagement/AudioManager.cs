using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Source")]
    public AudioSource sfxSource;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float sfxVolume = 1f;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
    }
    
    public void PlayRandomClip(AudioClip[] clips, float volumeScale = 1f)
    {
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("No audio clips provided!");
            return;
        }
        
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }
    
    public void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("Audio clip is null!");
            return;
        }
        
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }
}