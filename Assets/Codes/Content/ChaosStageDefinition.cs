using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chaos Mode: spawns enemies and obstacles from every other stage.
/// New stages are picked up automatically, nothing to maintain here.
/// Anything put in its own enemies/obstacles lists is added on top.
/// </summary>
[CreateAssetMenu(fileName = "ChaosMode", menuName = "SRR/Chaos Stage")]
public class ChaosStageDefinition : StageDefinition
{
    public override IReadOnlyList<EnemyDefinition> GetEnemyPool()
    {
        var pool = new List<EnemyDefinition>(enemies ?? new EnemyDefinition[0]);
        foreach (var stage in OtherStages())
            pool.AddRange(stage.GetEnemyPool());
        return Clean(pool);
    }

    public override IReadOnlyList<ObstacleDefinition> GetObstaclePool()
    {
        var pool = new List<ObstacleDefinition>(obstacles ?? new ObstacleDefinition[0]);
        foreach (var stage in OtherStages())
            pool.AddRange(stage.GetObstaclePool());
        return Clean(pool);
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
