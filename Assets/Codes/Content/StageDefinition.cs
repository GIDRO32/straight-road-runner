using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStage", menuName = "SRR/Stage")]
public class StageDefinition : ContentDefinition
{
    [Header("Scene")]
    [Tooltip("Scene loaded for this stage. Must be added to Build Settings.")]
    public string sceneName;

    [Header("Presentation")]
    [Tooltip("Big artwork for stage select")]
    public Sprite artwork;
    [TextArea(2, 6)] public string description;
    [Tooltip("Title typed out during the stage intro. Falls back to Display Name.")]
    public string introTitle;
    [Tooltip("StreamingAssets music folder, e.g. \"Music/Regular\". Empty = scene default.")]
    public string musicFolder;

    [Header("Title Screen (when this stage is selected)")]
    [Tooltip("Scrolling background tile on the main menu. Empty = the menu's default background.")]
    public GameObject menuBackgroundPrefab;
    public float menuBackgroundScrollSpeed = 4f;
    [Tooltip("Width of one background tile in world units (distance between tiles)")]
    public float menuBackgroundWidth = 39f;
    [Tooltip("Logo on the title screen. Empty = the default logo.")]
    public Sprite titleLogo;

    [Header("Enemies")]
    public EnemyDefinition[] enemies;

    [Header("Platforms")]
    [Tooltip("Base platform prefab (collider, scrolling, particles). Empty = the scene's StageControl prefab.")]
    public GameObject platformPrefab;
    [Tooltip("Used whenever none of the special platforms below is rolled")]
    public PlatformDefinition basePlatform;
    [Tooltip("Fragile, lava... Each new platform rolls these chances; the first hit wins.")]
    public List<PlatformSpawn> specialPlatforms = new List<PlatformSpawn>();

    [Header("Obstacles")]
    [Tooltip("Each new platform rolls each chance. Obstacles in the same slot never stack.")]
    public List<ObstacleSpawn> obstacleSpawns = new List<ObstacleSpawn>();

    [Header("Pacing & Difficulty")]
    public StagePacing pacing = new StagePacing();

    [Header("Bosses")]
    public bool bossesEnabled = true;

    [Header("Leaderboard")]
    [Tooltip("Leaderboard id in the Unity Cloud dashboard. Empty = same as id.")]
    public string leaderboardId;

    public string IntroTitle => string.IsNullOrEmpty(introTitle) ? DisplayName : introTitle;
    public string LeaderboardId => string.IsNullOrEmpty(leaderboardId) ? id : leaderboardId;

    public virtual IReadOnlyList<EnemyDefinition> GetEnemyPool() => Clean(enemies);
    public virtual PlatformDefinition GetBasePlatform() => basePlatform;
    public virtual IReadOnlyList<PlatformSpawn> GetSpecialPlatforms() =>
        SpawnListUtility.Clean(specialPlatforms, s => s.platform);
    public virtual IReadOnlyList<ObstacleSpawn> GetObstacleSpawns() =>
        SpawnListUtility.Clean(obstacleSpawns, s => s.obstacle);

    /// <summary>Obstacles that can appear on this stage (for the Stage Select info).</summary>
    public IReadOnlyList<ObstacleDefinition> GetObstaclePool()
    {
        var result = new List<ObstacleDefinition>();
        foreach (var spawn in GetObstacleSpawns()) result.Add(spawn.obstacle);
        return Clean(result);
    }

    /// <summary>Special platforms that can appear on this stage (for the Stage Select info).</summary>
    public IReadOnlyList<PlatformDefinition> GetPlatformPool()
    {
        var result = new List<PlatformDefinition>();
        foreach (var spawn in GetSpecialPlatforms()) result.Add(spawn.platform);
        return Clean(result);
    }

    protected static List<T> Clean<T>(IEnumerable<T> source) where T : Object
    {
        var result = new List<T>();
        if (source == null) return result;

        foreach (var item in source)
            if (item != null && !result.Contains(item))
                result.Add(item);
        return result;
    }
}
