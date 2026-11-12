using System;
using UnityEngine;

public enum VolumeChannel
{
    Music,
    Sfx
}

/// <summary>
/// Saved music / sound-effect volumes (PlayerPrefs), shared by every scene.
/// AudioManager and MusicManager read from here, VolumeSlider writes here.
/// </summary>
public static class VolumeSettings
{
    private const string MusicKey = "MusicVolume";
    private const string SfxKey = "SFXVolume";

    public const float DefaultMusic = 0.7f;
    public const float DefaultSfx = 1f;

    /// <summary>Fired whenever a volume changes.</summary>
    public static event Action<VolumeChannel, float> Changed;

    public static float Music
    {
        get => Get(VolumeChannel.Music);
        set => Set(VolumeChannel.Music, value);
    }

    public static float Sfx
    {
        get => Get(VolumeChannel.Sfx);
        set => Set(VolumeChannel.Sfx, value);
    }

    public static float Get(VolumeChannel channel)
    {
        return channel == VolumeChannel.Music
            ? PlayerPrefs.GetFloat(MusicKey, DefaultMusic)
            : PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
    }

    public static void Set(VolumeChannel channel, float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(Get(channel), volume)) return;

        PlayerPrefs.SetFloat(channel == VolumeChannel.Music ? MusicKey : SfxKey, volume);
        Changed?.Invoke(channel, volume);
    }

    /// <summary>Writes the volumes to disk (called when a settings slider closes).</summary>
    public static void Save() => PlayerPrefs.Save();
}
