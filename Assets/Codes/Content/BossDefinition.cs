using UnityEngine;

[CreateAssetMenu(fileName = "NewBoss", menuName = "SRR/Boss")]
public class BossDefinition : EnemyDefinition
{
    [Header("Boss")]
    [Tooltip("Shown under the warning and on the health bar. Falls back to Display Name.")]
    public string title;
    [Tooltip("Boss can't appear before the run has lasted this long")]
    public float minRunTimeSeconds = 0f;
    [Tooltip("Stages this boss may appear on. Leave empty for any stage.")]
    public StageDefinition[] allowedStages;
    [Tooltip("StreamingAssets music folder played during the fight. Empty = BossDirector's fight music (Music/Intense).")]
    public string musicFolder;

    [Header("Taking Damage")]
    [Tooltip("Damage of one punch (bosses usually sit where punching is hard)")]
    public int punchDamage = 1;
    [Tooltip("Damage of a sign punched with full stamina, which flies at the boss")]
    public int signDamage = 10;

    public string Title => string.IsNullOrEmpty(title) ? DisplayName : title;

    public bool CanAppearOn(StageDefinition stage)
    {
        if (allowedStages == null || allowedStages.Length == 0) return true;
        if (stage == null) return false;

        foreach (var s in allowedStages)
            if (s == stage) return true;
        return false;
    }
}
