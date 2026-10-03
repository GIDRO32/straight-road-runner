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

    [Header("Spawn Pools")]
    public EnemyDefinition[] enemies;
    public ObstacleDefinition[] obstacles;

    [Header("Bosses")]
    public bool bossesEnabled = true;

    [Header("Leaderboard")]
    [Tooltip("Leaderboard id in the Unity Cloud dashboard. Empty = same as id.")]
    public string leaderboardId;

    public string IntroTitle => string.IsNullOrEmpty(introTitle) ? DisplayName : introTitle;
    public string LeaderboardId => string.IsNullOrEmpty(leaderboardId) ? id : leaderboardId;

    public virtual IReadOnlyList<EnemyDefinition> GetEnemyPool() => Clean(enemies);
    public virtual IReadOnlyList<ObstacleDefinition> GetObstaclePool() => Clean(obstacles);

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
