using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for bosses. Subclass it for each boss's attack patterns.
/// - Two ways to hurt a boss: punches on a hurtbox (BossDefinition.punchDamage) and signs punched with
///   full stamina, which fly at the boss (BossDefinition.signDamage, see Sign.cs).
/// - Boss attacks hurt the player through HitPlayer(): with no stamina left it's game over.
/// - BossDirector listens to BossDefeated to resume the normal stage.
/// </summary>
public abstract class BossBase : EnemyBase
{
    public static event Action<BossBase> BossDefeated;

    /// <summary>The boss currently in the scene (null when there is none).</summary>
    public static BossBase Current { get; private set; }

    [Header("Boss")]
    [Tooltip("Colliders that take punches. Empty = any collider of the boss.")]
    public Collider2D[] hurtboxes;
    [Tooltip("Where thrown signs fly to. Empty = this transform.")]
    public Transform signTarget;

    [Header("Hit Flash")]
    public Color hitFlashColor = new Color(1f, 0.35f, 0.35f, 1f);
    public float hitFlashDuration = 0.15f;

    public BossDefinition BossDefinition => definition as BossDefinition;
    public float HealthNormalized => MaxHealth > 0 ? Mathf.Clamp01((float)currentHealth / MaxHealth) : 0f;
    public string Title => BossDefinition != null ? BossDefinition.Title : name;
    public Sprite Icon => definition != null ? definition.icon : null;

    /// <summary>False while the boss is entering, leaving or defeated: hits are ignored.</summary>
    public virtual bool IsVulnerable => !isDefeated;
    public Vector3 SignTargetPosition => signTarget != null ? signTarget.position : transform.position;

    // Bosses hurt the player with their attacks, not by being touched
    public override bool DealsContactDamage => false;

    private SpriteRenderer[] flashRenderers;
    private Color[] flashColors;
    private Coroutine flashRoutine;

    protected override void Awake()
    {
        base.Awake();
        Current = this;
    }

    protected virtual void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // ===== TAKING DAMAGE =====

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDefeated || !collision.CompareTag("Hits")) return;
        if (!TouchesHurtbox(collision)) return;

        TakeHit(BossDefinition != null ? BossDefinition.punchDamage : 1, collision);
    }

    /// <summary>A sign punched with full stamina reached the boss.</summary>
    public virtual void TakeSignHit(Sign sign)
    {
        TakeHit(BossDefinition != null ? BossDefinition.signDamage : 10);
    }

    public override void TakeHit(int damage, Collider2D source = null)
    {
        if (!IsVulnerable || damage <= 0) return;
        base.TakeHit(damage, source);
        if (!isDefeated) Flash();
    }

    public override void Defeat()
    {
        if (isDefeated) return;
        StopFlash(); // So the fade-out starts from the real colors
        base.Defeat();
    }

    protected override void OnDefeated()
    {
        base.OnDefeated();
        BossDefeated?.Invoke(this);
    }

    private bool TouchesHurtbox(Collider2D other)
    {
        if (hurtboxes == null || hurtboxes.Length == 0) return true;
        foreach (var box in hurtboxes)
            if (box != null && box.enabled && box.IsTouching(other)) return true;
        return false;
    }

    // ===== HURTING THE PLAYER =====

    /// <summary>Boss attack landed on the player. With no stamina left it's game over.</summary>
    protected bool HitPlayer(Movement movement)
    {
        if (movement == null) return false;
        return movement.TakeBossHit(ContactStaminaDamage, ContactStunDuration);
    }

    // ===== HIT FLASH =====

    protected void Flash()
    {
        if (flashRenderers == null)
        {
            flashRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            flashColors = new Color[flashRenderers.Length];
            for (int i = 0; i < flashRenderers.Length; i++) flashColors[i] = flashRenderers[i].color;
        }

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private void StopFlash()
    {
        if (flashRoutine == null) return;
        StopCoroutine(flashRoutine);
        flashRoutine = null;
        for (int i = 0; i < flashRenderers.Length; i++)
            if (flashRenderers[i] != null) flashRenderers[i].color = flashColors[i];
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < flashRenderers.Length; i++)
            if (flashRenderers[i] != null) flashRenderers[i].color = flashColors[i] * hitFlashColor;

        yield return new WaitForSeconds(hitFlashDuration);

        for (int i = 0; i < flashRenderers.Length; i++)
            if (flashRenderers[i] != null) flashRenderers[i].color = flashColors[i];
        flashRoutine = null;
    }
}
