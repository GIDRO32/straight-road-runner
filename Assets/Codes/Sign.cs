// Sign.cs - NEW SCRIPT (attach to Sign prefab)

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class Sign : MonoBehaviour
{
    [Header("Sign Settings")]
    public Sprite[] sprites;
    public float staminaGain = 25f;
    public float spinSpeed = 720f;  // Degrees per second

    [Header("Boss Fight")]
    [Tooltip("Punched with full stamina during a boss fight: the sign flies at the boss")]
    public float throwDuration = 0.55f;
    public float throwArcHeight = 2.5f;
    public float throwSpinSpeed = 1080f;
    [Tooltip("Added to the sprites' sorting order while flying, so the sign is drawn above the boss")]
    public int throwSortingOrder = 45;

    private SpriteRenderer sr;
    private PlayerData playerData;
    private bool isHit = false;
    [Header("Sound Effects")]
    public SoundEffects soundEffects;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sprites.Length > 0)
            sr.sprite = sprites[Random.Range(0, sprites.Length)];

        // Make collider trigger
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (isHit || !col.CompareTag("Hits")) return;

        // Play metal hit sound
        soundEffects?.PlayMetalHit();

        // Find player and add stamina
        playerData = col.transform.GetComponentInParent<PlayerData>();
        bool fullStamina = false;
        if (playerData != null)
        {
            fullStamina = playerData.currentStamina >= playerData.maxStamina;

            // Remnant rule: stamina that doesn't fit becomes points
            // (full stamina = staminaGain points; 115/120 with +10 = 5 stamina + 5 points)
            float room = Mathf.Max(0f, playerData.maxStamina - playerData.currentStamina);
            float gained = Mathf.Min(staminaGain, room);
            float remnant = staminaGain - gained;

            playerData.currentStamina += gained;
            playerData.UpdateStaminaUI();

            int points = Mathf.RoundToInt(remnant);
            if (points > 0 && ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(points);
            }
        }

        isHit = true;

        // Boss fight + full stamina: the sign flies at the boss (the points above still count)
        BossBase boss = BossBase.Current;
        if (fullStamina && boss != null && boss.IsVulnerable)
            StartCoroutine(ThrowAtBoss(boss));
        else
            StartCoroutine(SpinAndDestroy());
    }

    private System.Collections.IEnumerator ThrowAtBoss(BossBase boss)
    {
        // Leave the platform and fly in an arc to the boss
        transform.SetParent(null, true);
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.sortingOrder += throwSortingOrder;

        Vector3 start = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            if (boss == null || !boss.IsVulnerable)
            {
                StartCoroutine(SpinAndDestroy()); // Boss gone mid-flight
                yield break;
            }

            t += Time.deltaTime / Mathf.Max(0.05f, throwDuration);
            float k = Mathf.Clamp01(t);
            Vector3 end = boss.SignTargetPosition; // The boss may move
            Vector3 pos = Vector3.Lerp(start, end, k);
            pos.y += throwArcHeight * 4f * k * (1f - k);
            transform.position = pos;
            transform.Rotate(0f, 0f, -throwSpinSpeed * Time.deltaTime);
            yield return null;
        }

        boss.TakeSignHit(this);
        soundEffects?.PlayMetalHit();
        SmokePuff.Spawn(transform.position, 2f, throwSortingOrder + 1, null, new Color(0.08f, 0.08f, 0.08f, 0.9f));
        Destroy(gameObject);
    }

    private System.Collections.IEnumerator SpinAndDestroy()
    {
        float timer = 0f;
        while (timer < 2f)  // Spin for 2 seconds
        {
            transform.Rotate(0, spinSpeed * Time.deltaTime, 0);
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}