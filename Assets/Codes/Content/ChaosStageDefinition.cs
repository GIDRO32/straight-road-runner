using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chaos Mode: spawns enemies, platforms and obstacles from every other stage.
/// New stages are picked up automatically, nothing to maintain here.
/// Anything put in its own lists is added on top.
/// </summary>
[CreateAssetMenu(fileName = "ChaosMode", menuName = "SRR/Chaos Stage")]
public class ChaosStageDefinition : StageDefinition
{
    [Header("Chaos")]
    [Tooltip("Chance that a platform uses another stage's base platform look")]
    public float otherBasePlatformChance = 0.3f;

    public override IReadOnlyList<EnemyDefinition> GetEnemyPool()
    {
        var pool = new List<EnemyDefinition>(enemies ?? new EnemyDefinition[0]);
        foreach (var stage in OtherStages())
            pool.AddRange(stage.GetEnemyPool());
        return Clean(pool);
    }

    public override PlatformDefinition GetBasePlatform()
    {
        if (basePlatform != null) return basePlatform;
        foreach (var stage in OtherStages())
            if (stage.GetBasePlatform() != null) return stage.GetBasePlatform();
        return null;
    }

    public override IReadOnlyList<PlatformSpawn> GetSpecialPlatforms()
    {
        var pool = new List<PlatformSpawn>(specialPlatforms ?? new List<PlatformSpawn>());
        PlatformDefinition myBase = GetBasePlatform();

        foreach (var stage in OtherStages())
        {
            pool.AddRange(stage.GetSpecialPlatforms());

            // Other stages' regular platforms show up too
            PlatformDefinition otherBase = stage.GetBasePlatform();
            if (otherBase != null && otherBase != myBase)
                pool.Add(new PlatformSpawn
                {
                    platform = otherBase,
                    chance = new DifficultyRange(otherBasePlatformChance, otherBasePlatformChance, 0f)
                });
        }
        return SpawnListUtility.Clean(pool, s => s.platform);
    }

    public override IReadOnlyList<ObstacleSpawn> GetObstacleSpawns()
    {
        var pool = new List<ObstacleSpawn>(obstacleSpawns ?? new List<ObstacleSpawn>());
        foreach (var stage in OtherStages())
            pool.AddRange(stage.GetObstacleSpawns());
        return SpawnListUtility.Clean(pool, s => s.obstacle);
    }

    private IEnumerable<StageDefinition> OtherStages()
    {
        var db = ContentDatabase.Instance;
        if (db == null) yield break;

        foreach (var stage in db.Stages)
            if (!(stage is ChaosStageDefinition))
                yield return stage;
    }
}
