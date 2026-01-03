using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageControl : MonoBehaviour
{
    [Header("Platform Settings")]
    public GameObject platformPrefab;
    public float platformSpawnX = 20f;
    public float minY = -2f;
    public float maxY = 2f;
    public float platformScrollSpeed = -2f;  // negative = move right
    public float platformDestroyX = 30f;

    [Header("Background Settings")]
    public float backgroundScrollSpeed = 2f;
    public float backgroundSpawnX = 20f;
    public float backgroundDestroyX = 30f;
    public float backgroundWidth = 40f;

    private float platformTimer = 0f;
    private float nextBackgroundX;
    public int minPlatformsCount;
    public int maxPlatformsCount;

    [Header("Spawning")]
    public float spawnX = -20f;  // spawn from left side
    [Header("Background Prefabs")]
    public GameObject layer1Prefab;
    public GameObject layer2Prefab;
    public GameObject layer3Prefab;

    [Header("Layer1 Sprite Variants")]
    public Sprite[] layer1Variants;

    [Header("Background Layer Positions")]
    public float layer1Y = 0f;
    public float layer2Y = 2f;
    public float layer3Y = -2f;
    // === NEW VARIABLES ===
    private readonly List<GameObject> activePlatforms = new();
    private readonly List<GameObject> activeBackgrounds = new();
    // === NEW VARIABLES (add to top of class) ===
    [Header("Enemy Spawning")]
    public GameObject enemyPrefab;
    public float enemyStartX = 25f;
    private float enemyTimer = 0f;
    private bool playerSpawned = false;
    private readonly List<GameObject> activeEnemies = new();
    private bool firstEnemyGuaranteed = true;   // ← NEW
    public float[] Ypositions;
    [Header("Game Conditions")]
    public float platformSpawnInterval = 3f;
    public int maxEnemies = 6;
    public float spawnInterval = 20f;
    public float spawnChance = 0.35f;     // 35%
    public float currentPlatformSpawnChance = 0.8f;


    void Start()
    {
        nextBackgroundX = backgroundSpawnX;
        Time.timeScale = 10f;
    }

    void Update()
    {
        HandlePlatformSpawning();
        HandleBackgroundSpawning();
        if (playerSpawned)
        {
            HandleEnemySpawning();
        }

    }
    void HandleEnemySpawning()
    {
        enemyTimer += Time.unscaledDeltaTime;

        if (enemyTimer >= spawnInterval && activeEnemies.Count < maxEnemies)
        {
            enemyTimer = 0f;

            // FIRST enemy is 100% guaranteed
            if (firstEnemyGuaranteed)
            {
                SpawnOneEnemy();
                firstEnemyGuaranteed = false;
                return; // skip random attempts this wave
            }

            // Normal wave: up to 3 attempts, 35% each
            for (int i = 0; i < 3; i++)
            {
                if (activeEnemies.Count >= maxEnemies) break;
                if (Random.value > spawnChance) continue;

                SpawnOneEnemy();
            }
        }

        activeEnemies.RemoveAll(e => e == null);
    }

    // Helper: spawn single enemy
    private void SpawnOneEnemy()
    {
        float y = Random.Range(minY, maxY);
        Vector3 pos = new Vector3(enemyStartX, y, 0f);

        GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
        activeEnemies.Add(enemy);
        StartCoroutine(CleanupEnemy(enemy));
    }

    // Auto-remove from list when destroyed
    private IEnumerator CleanupEnemy(GameObject enemy)
    {
        while (enemy != null)
            yield return null;
        activeEnemies.Remove(enemy);
    }
    // === MODIFY NotifyPlayerSpawned() ===
    public void NotifyPlayerSpawned()
    {
        playerSpawned = true;
        enemyTimer = 0f;
        firstEnemyGuaranteed = true;   // ← RESET on new game
    }
    void HandlePlatformSpawning()
    {
        platformTimer += Time.deltaTime;
        if (platformTimer >= platformSpawnInterval)
        {
            int count = Random.Range(minPlatformsCount, maxPlatformsCount);
            for (int i = 0; i < Ypositions.Length; i++)
            {
                if (Random.value < currentPlatformSpawnChance || i == count - 1)
                {
                    Vector3 spawnPos = new Vector3(spawnX, Ypositions[i], 0f);
                    GameObject platform = Instantiate(platformPrefab, spawnPos, Quaternion.identity);

                    var rb = platform.GetComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.gravityScale = 0f;

                    var so = platform.GetComponent<ScrollingObject>();
                    so.Init(platformScrollSpeed, platformDestroyX);

                    activePlatforms.Add(platform);
                }
            }
            platformTimer = 0f;
        }

        // Destroy platforms that passed destroyX
        for (int i = activePlatforms.Count - 1; i >= 0; i--)
        {
            GameObject p = activePlatforms[i];
            if (p == null) { activePlatforms.RemoveAt(i); continue; }

            bool shouldDestroy = (platformScrollSpeed > 0 && p.transform.position.x >= platformDestroyX) ||
                                 (platformScrollSpeed < 0 && p.transform.position.x <= platformDestroyX);

            if (shouldDestroy)
            {
                activePlatforms.RemoveAt(i);
                Destroy(p);
            }
        }
    }
    // --- Replace HandleBackgroundSpawning() & SpawnBackground() ---
    void HandleBackgroundSpawning()
    {
        // Clean up old backgrounds first
        for (int i = activeBackgrounds.Count - 1; i >= 0; i--)
        {
            GameObject bg = activeBackgrounds[i];
            if (bg == null) { activeBackgrounds.RemoveAt(i); continue; }

            bool shouldDestroy = (backgroundScrollSpeed > 0 && bg.transform.position.x >= backgroundDestroyX) ||
                                 (backgroundScrollSpeed < 0 && bg.transform.position.x <= backgroundDestroyX);

            if (shouldDestroy)
            {
                activeBackgrounds.RemoveAt(i);
                Destroy(bg);
            }
        }

        // Spawn new set when needed
        float distanceNeeded = Mathf.Abs(backgroundSpawnX - nextBackgroundX);
        float traveled = Time.timeSinceLevelLoad * Mathf.Abs(backgroundScrollSpeed);
        if (traveled >= distanceNeeded)
        {
            SpawnBackground();
        }
    }

    void SpawnBackground()
    {
        // Layer 1
        GameObject layer1 = Instantiate(layer1Prefab, new Vector3(spawnX, layer1Y, 10f), Quaternion.identity);
        var rb1 = layer1.GetComponent<Rigidbody2D>();
        rb1.bodyType = RigidbodyType2D.Kinematic;
        rb1.gravityScale = 0f;
        var so1 = layer1.GetComponent<ScrollingObject>();
        so1.Init(backgroundScrollSpeed, backgroundDestroyX);
        if (layer1.GetComponent<SpriteRenderer>() is SpriteRenderer sr && layer1Variants.Length > 0)
            sr.sprite = layer1Variants[Random.Range(0, layer1Variants.Length)];
        activeBackgrounds.Add(layer1);

        // Layer 2 & 3 – same pattern
        GameObject layer2 = Instantiate(layer2Prefab, new Vector3(spawnX, layer2Y, 10f), Quaternion.identity);
        var rb2 = layer2.GetComponent<Rigidbody2D>();
        rb2.bodyType = RigidbodyType2D.Kinematic;
        rb2.gravityScale = 0f;
        layer2.GetComponent<ScrollingObject>().Init(backgroundScrollSpeed, backgroundDestroyX);
        activeBackgrounds.Add(layer2);

        GameObject layer3 = Instantiate(layer3Prefab, new Vector3(spawnX, layer3Y, 10f), Quaternion.identity);
        var rb3 = layer3.GetComponent<Rigidbody2D>();
        rb3.bodyType = RigidbodyType2D.Kinematic;
        rb3.gravityScale = 0f;
        layer3.GetComponent<ScrollingObject>().Init(backgroundScrollSpeed, backgroundDestroyX);
        activeBackgrounds.Add(layer3);

        nextBackgroundX += backgroundWidth;
    }
    // --- Optional: clear lists on scene reload ---
    void OnDestroy()
    {
        activePlatforms.Clear();
        activeBackgrounds.Clear();
    }

}
