// Platform.cs - NEW SCRIPT (attach to Platform prefab)

using UnityEngine;
using UnityEngine.UI;

public class Platform : MonoBehaviour
{
    [Header("Platform Features")]
    public GameObject sign;
    public GameObject wall;

    // NEW: Static difficulty variables (shared across all platforms)
    public static float wallSpawnChance = 0.33f;
    public static float signSpawnChance = 0.33f;
    public static float fragileChance = 0f; // Starts at 0, increases over time

    [Header("Fragile Platform Settings")]
    public Sprite normalSprite;
    public Sprite fragileSprite;
    public float fragileTime = 3f;
    public GameObject timerUI; // Assign a UI slider/text prefab

    private bool isFragile = false;
    private bool playerOnPlatform = false;
    private float fragileTimer = 0f;
    private SpriteRenderer spriteRenderer;
    private GameObject timerInstance;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Determine if this platform is fragile
        isFragile = Random.value < fragileChance;

        if (isFragile && fragileSprite != null)
        {
            spriteRenderer.sprite = fragileSprite;
        }
        else if (normalSprite != null)
        {
            spriteRenderer.sprite = normalSprite;
        }

        // Spawn features (walls/signs)
        bool spawnSign = Random.value < signSpawnChance;
        bool spawnWall = Random.value < wallSpawnChance;

        if (sign != null)
            sign.SetActive(spawnSign);

        if (wall != null)
            wall.SetActive(spawnWall);
    }
    void Update()
    {
        if (!isFragile) return;

        if (playerOnPlatform)
        {
            fragileTimer += Time.deltaTime;

            // Update timer UI
            if (timerInstance != null)
            {
                Slider timerSlider = timerInstance.GetComponentInChildren<Slider>();
                if (timerSlider != null)
                {
                    timerSlider.value = fragileTimer / fragileTime;
                }
            }

            // Platform breaks
            if (fragileTimer >= fragileTime)
            {
                BreakPlatform();
            }
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && isFragile)
        {
            playerOnPlatform = true;
            ShowTimer();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && isFragile)
        {
            playerOnPlatform = false;
            fragileTimer = 0f;
            HideTimer();
        }
    }

    private void ShowTimer()
    {
        if (timerUI != null && timerInstance == null)
        {
            timerInstance = Instantiate(timerUI, transform.position + Vector3.up * 1f, Quaternion.identity, transform);
        }
    }

    private void HideTimer()
    {
        if (timerInstance != null)
        {
            Destroy(timerInstance);
            timerInstance = null;
        }
    }

    private void BreakPlatform()
    {
        Debug.Log("Platform broke!");

        // Optional: spawn break particles/effects here

        Destroy(gameObject);
    }
}