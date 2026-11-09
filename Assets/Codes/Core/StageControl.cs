using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageControl : MonoBehaviour
{
    [Header("Platform Settings")]
    [Tooltip("Base platform prefab. A stage can override it (StageDefinition > Platform Prefab).")]
    public GameObject platformPrefab;
    public float platformSpawnX = 20f;
    public float minY = -2f;
    public float maxY = 2f;
    public float platformScrollSpeed = -2f;  
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
    [Tooltip("Fallback only: used when the current stage has no enemies in its StageDefinition")]
    public GameObject enemyPrefab;
    public float enemyStartX = 25f;
    private float enemyTimer = 0f;
    private bool playerSpawned = false;
    private readonly List<GameObject> activeEnemies = new();
    [Tooltip("Seconds between enemy waves (set from the stage's pacing)")]
    public float enemySpawnInterval = 20f;
    [Tooltip("Seconds between enemies of one wave")]
    public float enemyWaveSpawnDelay = 0.5f;
    [Tooltip("Seconds after the player appears until the first, guaranteed enemy")]
    public float firstEnemyDelay = 5f;
    private bool firstEnemySpawned = false;
    private bool waveRunning = false;
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

    // Stage content (from StageDefinition)
    private StageDefinition stage;
    private IReadOnlyList<EnemyDefinition> enemyPool = new List<EnemyDefinition>();
    private bool enemySpawningPaused = false;

    // Platforms & obstacles of the current stage, with live chances (nudged by DifficultyManager)
    private GameObject stagePlatformPrefab;
    private PlatformDefinition basePlatform;
    private readonly List<PlatformSpawnState> platformSpawns = new List<PlatformSpawnState>();
    private readonly List<ObstacleSpawnState> obstacleSpawns = new List<ObstacleSpawnState>();

    public StageDefinition Stage => stage;
    public IReadOnlyList<PlatformSpawnState> PlatformSpawns => platformSpawns;
    public IReadOnlyList<ObstacleSpawnState> ObstacleSpawns => obstacleSpawns;

    void Start()
    {
        nextBackgroundX = backgroundSpawnX;
        Time.timeScale = 10f;

        stage = GameSession.CurrentStage;
        if (stage != null)
        {
            enemyPool = stage.GetEnemyPool();
            LoadStagePlatforms(stage);
            ApplyPacingStart(stage.pacing);
        }
        else
        {
            Debug.LogWarning("StageControl: No StageDefinition found for this scene, using fallback enemyPrefab.");
        }

        // DifficultyManager nudges the values set up above
        DifficultyManager difficulty = DifficultyManager.Instance != null ? DifficultyManager.Instance : FindObjectOfType<DifficultyManager>();
        if (difficulty != null)
            difficulty.Initialize(this, stage != null ? stage.pacing : null);
    }

    private void LoadStagePlatforms(StageDefinition stageDefinition)
    {
        stagePlatformPrefab = stageDefinition.platformPrefab;
        basePlatform = stageDefinition.GetBasePlatform();

        platformSpawns.Clear();
        foreach (var spawn in stageDefinition.GetSpecialPlatforms())
            platformSpawns.Add(new PlatformSpawnState { definition = spawn.platform, range = spawn.chance, chance = spawn.chance.start });

        obstacleSpawns.Clear();
        foreach (var spawn in stageDefinition.GetObstacleSpawns())
            obstacleSpawns.Add(new ObstacleSpawnState { definition = spawn.obstacle, range = spawn.chance, chance = spawn.chance.start });
    }

    // Starting values of everything DifficultyManager ramps up
    private void ApplyPacingStart(StagePacing pacing)
    {
        if (pacing == null) return;

        platformSpawnInterval = pacing.platformInterval.start;
        currentPlatformSpawnChance = pacing.platformLaneChance.start;
        enemySpawnInterval = pacing.enemyInterval.start;
        enemyWaveSpawnDelay = pacing.enemyWaveSpawnDelay;
        firstEnemyDelay = pacing.firstEnemyDelay;
        enemySpawnChance = pacing.enemyChance.start;
        maxEnemies = Mathf.RoundToInt(pacing.maxEnemies.start);
    }

    // Special platforms are rolled in random order; the first that hits wins, otherwise the base platform
    private PlatformDefinition PickPlatformType()
    {
        int count = platformSpawns.Count;
        int offset = count > 0 ? Random.Range(0, count) : 0;

        for (int i = 0; i < count; i++)
        {
            PlatformSpawnState spawn = platformSpawns[(i + offset) % count];
            if (spawn.definition != null && Random.value < spawn.chance)
                return spawn.definition;
        }
        return basePlatform;
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
        // Cleanup destroyed enemies
        activeEnemies.RemoveAll(e => e == null);

        if (enemySpawningPaused) return;

        enemyTimer += Time.deltaTime;

        // The first enemy always appears, no chance roll and no wave
        if (!firstEnemySpawned)
        {
            if (enemyTimer >= firstEnemyDelay)
            {
                enemyTimer = 0f;
                firstEnemySpawned = true;
                if (!SpawnOneEnemy())
                    Debug.LogWarning("StageControl: Couldn't spawn the first enemy. Does the stage have enemies with prefabs?");
            }
            return;
        }

        if (enemyTimer >= enemySpawnInterval && !waveRunning && activeEnemies.Count < maxEnemies)
        {
            enemyTimer = 0f;
            StartCoroutine(SpawnEnemyWave());
        }
    }

    // Up to maxEnemies rolls; each rolls enemySpawnChance and is a random type from the stage's list.
    // Stops once maxEnemies are alive.
    private IEnumerator SpawnEnemyWave()
    {
        waveRunning = true;

        int rolls = maxEnemies;
        for (int i = 0; i < rolls; i++)
        {
            if (activeEnemies.Count >= maxEnemies || enemySpawningPaused)
                break;

            if (Random.value <= enemySpawnChance && SpawnOneEnemy())
            {
                // Little pause before the next enemy of the wave
                if (i < rolls - 1)
                    yield return new WaitForSeconds(enemyWaveSpawnDelay);
            }
        }

        waveRunning = false;
    }

    // Helper: spawn single enemy (random type from the stage's list). Returns false if nothing could spawn.
    private bool SpawnOneEnemy()
    {
        EnemyDefinition def = PickEnemy();
        GameObject prefab = def != null ? def.prefab : enemyPrefab;
        if (prefab == null) return false;

        float y = def != null
            ? Random.Range(def.spawnYRange.x, def.spawnYRange.y)
            : Random.Range(minY, maxY);
        Vector3 pos = new Vector3(enemyStartX, y, 0f);

        GameObject enemy = Instantiate(prefab, pos, Quaternion.identity);

        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase != null && def != null)
            enemyBase.Init(def);

        activeEnemies.Add(enemy);
        StartCoroutine(CleanupEnemy(enemy));
        return true;
    }

    // Random enemy type from the stage's list (Spawn Weight on the enemy asset; all 1 = equal odds)
    private EnemyDefinition PickEnemy()
    {
        return WeightedRandom.Pick(enemyPool, e => e.prefab != null ? e.spawnWeight : 0f);
    }

    // Used by BossDirector during boss fights
    public void SetEnemySpawningPaused(bool paused)
    {
        enemySpawningPaused = paused;
        if (paused) enemyTimer = 0f;
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
        firstEnemySpawned = false;   // RESET on new game
        waveRunning = false;
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
                    GameObject prefab = stagePlatformPrefab != null ? stagePlatformPrefab : platformPrefab;
                    GameObject platform = Instantiate(prefab, spawnPos, Quaternion.identity);

                    var rb = platform.GetComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.gravityScale = 0f;

                    var so = platform.GetComponent<ScrollingObject>();
                    so.Init(platformScrollSpeed, platformDestroyX);

                    // Platform type (look + mechanic) and obstacles for this stage
                    var platformScript = platform.GetComponent<Platform>();
                    if (platformScript != null)
                        platformScript.Setup(PickPlatformType(), obstacleSpawns);

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
