using System.Collections;
using UnityEngine;

/// <summary>
/// Shared logic for every enemy: stats, sounds, taking hits, score and death.
/// Behaviour (movement, attacks) goes in a subclass, e.g. BallDevil.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Definition")]
    [Tooltip("Set automatically by the spawner. Assign it for enemies placed by hand in a scene.")]
    [SerializeField] protected EnemyDefinition definition;

    [Header("References")]
    public Transform player;

    [Header("Death")]
    public float knockbackForce = 5f;
    public float fadeDuration = 1f;

    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected int currentHealth;
    protected bool isDefeated = false;

    public EnemyDefinition Definition => definition;
    public bool IsDefeated => isDefeated;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => definition != null ? Mathf.Max(1, definition.stats.health) : 1;
    public float ContactStaminaDamage => definition != null ? definition.stats.contactStaminaDamage : 10f;
    public float ContactStunDuration => definition != null ? definition.stats.contactStunDuration : 1f;

    /// <summary>Called by spawners right after Instantiate, before Start.</summary>
    public virtual void Init(EnemyDefinition def)
    {
        definition = def;
        currentHealth = MaxHealth;
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        currentHealth = MaxHealth;
    }

    protected virtual void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        PlaySound(definition != null ? definition.sounds.spawn : null);
        OnSpawned();
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isDefeated && collision.CompareTag("Hits"))
            TakeHit(1, collision);
    }

    public virtual void TakeHit(int damage, Collider2D source = null)
    {
        if (isDefeated) return;

        currentHealth -= damage;
        PlaySound(definition != null ? definition.sounds.hit : null);
        OnHit(source);

        if (currentHealth <= 0)
            Defeat();
    }

    public virtual void Defeat()
    {
        if (isDefeated) return;
        isDefeated = true;

        PlaySound(definition != null ? definition.sounds.defeat : null);

        if (ScoreManager.Instance != null)
        {
            if (definition != null)
                ScoreManager.Instance.AddScore(definition.stats.scoreValue);
            else
                ScoreManager.Instance.AddEnemyKillScore();
        }

        if (rb != null)
        {
            Vector2 force = new Vector2(
                Random.Range(-knockbackForce, knockbackForce),
                Random.Range(0f, knockbackForce * 1.5f)
            );
            rb.AddForce(force, ForceMode2D.Impulse);
        }

        OnDefeated();
        StartCoroutine(FadeAndDestroy());
    }

    // ===== HOOKS FOR SUBCLASSES =====

    protected virtual void OnSpawned() { }
    protected virtual void OnHit(Collider2D source) { }
    protected virtual void OnDefeated() { }

    // ===== HELPERS =====

    protected void PlayAttackSound()
    {
        PlaySound(definition != null ? definition.sounds.attack : null);
    }

    protected void PlaySound(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        AudioManager.Instance?.PlayRandomClip(clips);
    }

    protected IEnumerator FadeAndDestroy()
    {
        if (spriteRenderer != null)
        {
            float timer = 0f;
            Color originalColor = spriteRenderer.color;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        }

        Destroy(gameObject);
    }
}
