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
    public float enemySpawnInterval = 20f; // Now public
    private bool firstEnemyGuaranteed = true;   // ← NEW
    public float[] Ypositions;
    [Header("Game Conditions")]
    public int maxEnemies = 6; // Now public
    public float spawnChance = 0.35f;     // 35%
    [Range(0f, 1f)]
    public float enemySpawnChance = 0.35f; // Now public
    public Vector2 enemySpawnYRange = new Vector2(-2f, 4f);
    [Range(0f, 1f)]
    public float currentPlatformSpawnChance = 0.8f;
    public float platformSpawnInterval = 0.7f;


    void Start()
    {
        nextBackgroundX = backgroundSpawnX;
        Time.timeScale = 10f;
    }

    void Update()
    {
        HandlePlatformSpawning();
        if (playerSpawned)
        {
            HandleEnemySpawning();
        }

    }
    void HandleEnemySpawning()
    {
        enemyTimer += Time.unscaledDeltaTime;

        if (enemyTimer >= enemySpawnInterval && activeEnemies.Count < maxEnemies)
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
    void OnDestroy()
    {
        activePlatforms.Clear();
        activeBackgrounds.Clear();
    }

}
