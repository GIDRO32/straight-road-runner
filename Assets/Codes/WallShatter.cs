// WallShatter.cs - NEW SCRIPT (attach to Wall root object)

using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class WallShatter : MonoBehaviour
{
    [Header("Shatter Settings")]
    public GameObject[] shards;           // Assign child shard objects in Inspector
    public float shardForce = 5f;         // Explosion force
    public float destroyDelay = 3f;       // Auto-destroy shards after

    private bool isShattered = false;
    [Header("Sound Effects")]
    public SoundEffects soundEffects;

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
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (isShattered) return;
        if (!col.CompareTag("Hits")) return;  // Only player hitbox

        Shatter();
    }

    private void Shatter()
    {
        isShattered = true;

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

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    private IEnumerator DestroyShard(GameObject shard)
    {
        yield return new WaitForSeconds(destroyDelay);
        if (shard != null) Destroy(shard);
    }
}