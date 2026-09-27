using UnityEngine;

[CreateAssetMenu(fileName = "NewBoss", menuName = "SRR/Boss")]
public class BossDefinition : EnemyDefinition
{
    [Header("Boss")]
    [Tooltip("Shown when the boss appears, e.g. \"The Eraser King\"")]
    public string title;
    [Tooltip("Boss can't appear before the run has lasted this long")]
    public float minRunTimeSeconds = 60f;
    [Tooltip("Stages this boss may appear on. Leave empty for any stage.")]
    public StageDefinition[] allowedStages;
    [Tooltip("Optional StreamingAssets music folder played during the fight")]
    public string musicFolder;

    public bool CanAppearOn(StageDefinition stage)
    {
        if (allowedStages == null || allowedStages.Length == 0) return true;
        if (stage == null) return false;

        foreach (var s in allowedStages)
            if (s == stage) return true;
        return false;
    }
}
