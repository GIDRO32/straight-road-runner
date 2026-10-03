using System;

/// <summary>
/// Base class for bosses. Subclass it for each boss's attack patterns.
/// BossDirector listens to BossDefeated to resume the normal stage.
/// </summary>
public abstract class BossBase : EnemyBase
{
    public static event Action<BossBase> BossDefeated;

    public BossDefinition BossDefinition => definition as BossDefinition;
    public float HealthNormalized => MaxHealth > 0 ? (float)currentHealth / MaxHealth : 0f;

    protected override void OnDefeated()
    {
        base.OnDefeated();
        BossDefeated?.Invoke(this);
    }
}
