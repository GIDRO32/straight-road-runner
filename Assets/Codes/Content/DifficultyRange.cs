using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A value that changes over a run: it begins at <see cref="start"/> and DifficultyManager moves it
/// by <see cref="step"/> each time it picks this value, until it reaches <see cref="limit"/>.
/// A negative step makes the value go down (e.g. sign chance getting rarer).
/// </summary>
[System.Serializable]
public class DifficultyRange
{
    public float start;
    public float limit;
    [Tooltip("Change per difficulty nudge. Negative = goes down. 0 = never changes.")]
    public float step;

    public DifficultyRange(float start, float limit, float step)
    {
        this.start = start;
        this.limit = limit;
        this.step = step;
    }

    public bool IsMaxed(float value)
    {
        if (step > 0f) return value >= limit;
        if (step < 0f) return value <= limit;
        return true;
    }

    public float Next(float value)
    {
        if (step > 0f) return Mathf.Min(value + step, limit);
        if (step < 0f) return Mathf.Max(value + step, limit);
        return value;
    }
}

/// <summary>A platform type and how likely each new platform is to be that type.</summary>
[System.Serializable]
public class PlatformSpawn
{
    public PlatformDefinition platform;
    [Tooltip("Chance (0-1) that a new platform is this type")]
    public DifficultyRange chance = new DifficultyRange(0.01f, 1f, 0.03f);
}

/// <summary>An obstacle and how likely each new platform is to carry it.</summary>
[System.Serializable]
public class ObstacleSpawn
{
    public ObstacleDefinition obstacle;
    [Tooltip("Chance (0-1) that a new platform gets this obstacle")]
    public DifficultyRange chance = new DifficultyRange(0.05f, 0.5f, 0.02f);
}

/// <summary>Stage-wide pacing: how often things spawn and how it ramps up.</summary>
[System.Serializable]
public class StagePacing
{
    [Tooltip("Seconds between difficulty nudges (each nudge steps ONE random value below)")]
    public float difficultyInterval = 5f;

    [Header("Platforms")]
    [Tooltip("Seconds between platform rows")]
    public DifficultyRange platformInterval = new DifficultyRange(1f, 5f, 0.1f);
    [Tooltip("Chance (0-1) that each lane in a row gets a platform")]
    public DifficultyRange platformLaneChance = new DifficultyRange(0.95f, 0.3f, -0.02f);

    [Header("Enemies")]
    [Tooltip("Seconds between enemy waves")]
    public DifficultyRange enemyInterval = new DifficultyRange(20f, 3f, -0.5f);
    [Tooltip("Chance (0-1) of each enemy in a wave")]
    public DifficultyRange enemyChance = new DifficultyRange(0.2f, 1f, 0.05f);
    public DifficultyRange maxEnemies = new DifficultyRange(6f, 20f, 1f);

    [Header("Endgame (everything above maxed)")]
    [Tooltip("Platform scroll speed added every endgame interval")]
    public float endgameSpeedStep = 0.2f;
    public float endgameInterval = 5f;
}

public static class SpawnListUtility
{
    /// <summary>Copies a list, skipping empty entries and repeats of the same definition.</summary>
    public static List<T> Clean<T>(IEnumerable<T> source, System.Func<T, Object> key) where T : class
    {
        var result = new List<T>();
        var seen = new HashSet<Object>();
        if (source == null) return result;

        foreach (var item in source)
        {
            Object k = item != null ? key(item) : null;
            if (k != null && seen.Add(k))
                result.Add(item);
        }
        return result;
    }
}
