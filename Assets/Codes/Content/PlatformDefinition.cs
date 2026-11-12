using UnityEngine;

public enum PlatformMechanicType
{
    Regular,
    Fragile,
    Lava
}

[System.Serializable]
public class FragileSettings
{
    [Tooltip("Seconds of standing on it before it breaks")]
    public float breakTime = 0.66f;
}

[System.Serializable]
public class LavaSettings
{
    [Tooltip("Seconds of standing on it before the first hit")]
    public float heatUpTime = 1f;
    [Tooltip("Seconds between hits while you keep standing on it")]
    public float hitInterval = 1.5f;
    public float staminaDamage = 10f;
    public float stunDuration = 1f;

    [Header("Hit animation (plays once per hit)")]
    public Sprite[] hitFrames;
    public float hitFramesPerSecond = 10f;

    [Header("Hit particles (lava droplets, once per hit)")]
    public bool hitParticles = true;
    [Tooltip("Your own particle prefab. Empty = built-in lava droplets.")]
    public GameObject hitParticlesPrefab;
    [Tooltip("Droplets per hit (built-in effect)")]
    public int hitDroplets = 16;
}

/// <summary>
/// One platform type: how it looks and what it does when you stand on it.
/// Stages list which types they use and how often (StageDefinition > Platforms).
/// </summary>
[CreateAssetMenu(fileName = "NewPlatform", menuName = "SRR/Platform")]
public class PlatformDefinition : ContentDefinition
{
    [Header("Info")]
    [TextArea(2, 6)] public string description;

    [Header("Look")]
    public Sprite sprite;
    [Tooltip("Optional looping animation. Overrides Sprite when set.")]
    public Sprite[] idleFrames;
    public float idleFramesPerSecond = 6f;

    [Header("Mechanic")]
    public PlatformMechanicType mechanic = PlatformMechanicType.Regular;
    [Tooltip("Used when Mechanic is Fragile")]
    public FragileSettings fragile = new FragileSettings();
    [Tooltip("Used when Mechanic is Lava")]
    public LavaSettings lava = new LavaSettings();

    [Header("Obstacles")]
    [Tooltip("Untick to keep walls, signs etc. off this platform type")]
    public bool allowObstacles = true;

    public Sprite DisplaySprite => sprite != null ? sprite : (idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : null);
}
