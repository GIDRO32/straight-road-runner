// WallShatter.cs - attach to Wall root object

using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class WallShatter : MonoBehaviour
{
    [Header("Durability")]
    [Tooltip("Hits needed to break, random between min and max. ObstacleDefinition.hitsToBreak can override this per stage.")]
    public int minHits = 1;
    public int maxHits = 1;
    [Tooltip("Tint flashed when the wall is hit but not broken")]
    public Color hitFlashColor = new Color(1f, 0.6f, 0.6f, 1f);
    public float hitShakeDistance = 0.08f;
    public float hitFeedbackDuration = 0.15f;

    [Header("Shatter Settings")]
    public GameObject[] shards;           // Assign child shard objects in Inspector
    public float shardForce = 5f;         // Explosion force
    public float destroyDelay = 3f;       // Auto-destroy shards after

    private bool isShattered = false;
    private int hitsLeft;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private Coroutine feedback;

    [Header("Sound Effects")]
    public SoundEffects soundEffects;

    public int HitsLeft => hitsLeft;

    private void Awake()
    {
        // Auto-find shards if not assigned
        if (shards == null || shards.Length == 0)
        {
            shards = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                shards[i] = transform.GetChild(i).gameObject;
        }

        foreach (GameObject shard in shards)
        {
            shard.SetActive(false);  // Hide initially
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;

        SetHitsToBreak(minHits, maxHits);
    }

    /// <summary>Sets durability (random between min and max, inclusive).</summary>
    public void SetHitsToBreak(int min, int max)
    {
        min = Mathf.Max(1, min);
        max = Mathf.Max(min, max);
        hitsLeft = Random.Range(min, max + 1);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (isShattered) return;
        if (!col.CompareTag("Hits")) return;  // Only player hitbox

        hitsLeft--;
        if (hitsLeft <= 0)
            Shatter();
        else
            Damaged();
    }

    // Hit but still standing: sound + flash + shake
    private void Damaged()
    {
        soundEffects?.PlayMetalHit();
        if (feedback != null) StopCoroutine(feedback);
        feedback = StartCoroutine(HitFeedback());
    }

    private IEnumerator HitFeedback()
    {
        Vector3 basePosition = transform.localPosition;
        float t = 0f;

        while (t < hitFeedbackDuration)
        {
            t += Time.deltaTime;
            float k = 1f - t / hitFeedbackDuration;
            transform.localPosition = basePosition + new Vector3(Random.Range(-1f, 1f) * hitShakeDistance * k, 0f, 0f);
            if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(baseColor, hitFlashColor, k);
            yield return null;
        }

        transform.localPosition = basePosition;
        if (spriteRenderer != null) spriteRenderer.color = baseColor;
        feedback = null;
    }

    private void Shatter()
    {
        isShattered = true;
        if (feedback != null) StopCoroutine(feedback);
        if (spriteRenderer != null) spriteRenderer.color = baseColor;

        // Play wall break sound
        soundEffects?.PlayWallBreak();

        // Disable wall collider
        GetComponent<Collider2D>().enabled = false;

        // Enable & activate shards
        foreach (GameObject shard in shards)
        {
            shard.SetActive(true);
            shard.transform.SetParent(null);

            Rigidbody2D rb = shard.GetComponent<Rigidbody2D>();
            if (rb == null) rb = shard.AddComponent<Rigidbody2D>();

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1f;

            Vector2 force = new Vector2(
                Random.Range(-shardForce, shardForce),
                Random.Range(0f, shardForce * 1.5f)
            );
            rb.AddForce(force, ForceMode2D.Impulse);

            StartCoroutine(DestroyShard(shard));
        }

        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    private IEnumerator DestroyShard(GameObject shard)
    {
        yield return new WaitForSeconds(destroyDelay);
        if (shard != null) Destroy(shard);
    }
}
