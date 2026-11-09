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
        if (playerData != null)
        {
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
        StartCoroutine(SpinAndDestroy());
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