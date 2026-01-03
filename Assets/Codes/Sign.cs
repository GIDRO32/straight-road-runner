// Sign.cs - NEW SCRIPT (attach to Sign prefab)

using UnityEngine;

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

        // Find player and add stamina
        playerData = col.transform.GetComponentInParent<PlayerData>();
        if (playerData != null)
        {
            playerData.currentStamina = Mathf.Min(playerData.maxStamina, 
                playerData.currentStamina + staminaGain);
            playerData.UpdateStaminaUI();
            Debug.Log("Stamina increased by " + staminaGain);
        }

        // Start spinning
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