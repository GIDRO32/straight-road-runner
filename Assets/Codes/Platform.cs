// Platform.cs - NEW SCRIPT (attach to Platform prefab)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Platform : MonoBehaviour
{
    [Header("Platform Features")]
    public GameObject sign;
    public GameObject wall;
    [Tooltip("Where stage obstacles spawn. Falls back to the built-in wall's position.")]
    public Transform obstacleAnchor;

    // Set by StageControl right after spawning. When empty, the built-in wall child is used instead.
    [System.NonSerialized] public IReadOnlyList<ObstacleDefinition> obstaclePool;

    // NEW: Static difficulty variables (shared across all platforms)
    public static float wallSpawnChance = 0.03f;
    public static float signSpawnChance = 0.7f;
    public static float fragileChance = 0.01f; // Starts at 0, increases over time

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
    [Header("Particle Effects")]
    public ParticleSystem normalLandingParticles;
    public ParticleSystem fragileLandingParticles;
    public ParticleSystem fragileBreak;

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

        // Stage obstacles replace the built-in wall when the stage defines any
        if (spawnWall && TrySpawnStageObstacle())
            spawnWall = false;

        if (sign != null)
            sign.SetActive(spawnSign);

        if (wall != null)
            wall.SetActive(spawnWall);
    }
    private bool TrySpawnStageObstacle()
    {
        if (obstaclePool == null || obstaclePool.Count == 0) return false;

        ObstacleDefinition obstacle = WeightedRandom.Pick(obstaclePool, o => o.prefab != null ? o.spawnWeight : 0f);
        if (obstacle == null) return false;

        Transform anchor = obstacleAnchor != null ? obstacleAnchor : (wall != null ? wall.transform : transform);

        // Parent to the platform so it scrolls with it, keeping the prefab's world scale
        GameObject instance = Instantiate(obstacle.prefab, transform, true);
        instance.transform.position = anchor.position;
        return true;
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
    public void OnPlayerLanded(Vector3 landPosition)
    {
        ParticleSystem particlesToPlay = isFragile ? fragileLandingParticles : normalLandingParticles;

        if (particlesToPlay != null)
        {
            // Position particles at landing point
            particlesToPlay.transform.position = new Vector3(
                landPosition.x,
                transform.position.y + GetComponent<Collider2D>().bounds.extents.y,
                landPosition.z
            );
            particlesToPlay.Play();
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && isFragile)
        {
            playerOnPlatform = true;

            // ✅ Optional: Play different particle when stepping on fragile platform
            if (fragileLandingParticles != null && !fragileLandingParticles.isPlaying)
            {
                fragileLandingParticles.Play();
            }
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && isFragile)
        {
            playerOnPlatform = false;
            fragileTimer = 0f;
        }
    }

    private void BreakPlatform()
    {
        // Disable collider so player falls through
        GetComponent<Collider2D>().enabled = false;

        // Hide the platform sprite
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        // Play particles and destroy after they finish
        if (fragileBreak != null)
        {
            fragileBreak.Play();
            Destroy(gameObject, fragileBreak.main.duration + fragileBreak.main.startLifetime.constantMax);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}