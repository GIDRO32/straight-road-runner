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

    [Header("Epic Sign")]
    [Tooltip("Stamina from an Epic Sign (bought in the Tasks shop) instead of Stamina Gain")]
    public float epicStaminaGain = 50f;
    public Color epicTint = new Color(0.85f, 0.6f, 1f, 1f);

    [Header("Particles (optional, empty = built-in)")]
    [Tooltip("Your own particles for Epic Signs. Empty = built-in purple sparkles.")]
    public GameObject epicParticlesPrefab;
    [Tooltip("Your own effect when a thrown sign hits a boss. Empty = built-in ink puff.")]
    public GameObject bossHitEffectPrefab;

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
    private bool isEpic = false;

    // Epic Signs on screen right now: never more than the player owns
    private static readonly System.Collections.Generic.HashSet<Sign> activeEpicSigns = new System.Collections.Generic.HashSet<Sign>();

    public bool IsEpic => isEpic;
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

        if (RollEpic()) MakeEpic();
    }

    void OnDestroy()
    {
        activeEpicSigns.Remove(this);
    }

    // Owned Epic Signs show up now and then, until they're punched
    private static bool RollEpic()
    {
        activeEpicSigns.RemoveWhere(s => s == null);
        if (PlayerProgress.EpicSigns <= activeEpicSigns.Count) return false;
        var db = ContentDatabase.Instance;
        float chance = db != null ? db.epicSignChance : 0.2f;
        return Random.value < chance;
    }

    public void MakeEpic()
    {
        if (isEpic) return;
        isEpic = true;
        activeEpicSigns.Add(this);
        if (sr != null) sr.color = epicTint;
        EpicSparkles.Attach(transform, sr, epicParticlesPrefab);
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
            float gain = isEpic ? epicStaminaGain : staminaGain;
            float room = Mathf.Max(0f, playerData.maxStamina - playerData.currentStamina);
            float gained = Mathf.Min(gain, room);
            float remnant = gain - gained;

            playerData.currentStamina += gained;
            playerData.UpdateStaminaUI();

            int points = Mathf.RoundToInt(remnant);
            if (points > 0 && ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(points);
            }
        }

        isHit = true;

        // An Epic Sign is used up only when it's punched
        if (isEpic)
        {
            activeEpicSigns.Remove(this);
            PlayerProgress.UseEpicSign();
        }

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
        SmokePuff.Spawn(transform.position, 2f, throwSortingOrder + 1, bossHitEffectPrefab, new Color(0.08f, 0.08f, 0.08f, 0.9f));
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