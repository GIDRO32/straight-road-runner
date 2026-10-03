using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Spawns a random boss at a random time during a run.
/// While a boss is alive, normal enemy spawning and difficulty progression are paused.
/// Bosses come from ContentDatabase, so new bosses only need a BossDefinition asset.
/// </summary>
public class BossDirector : MonoBehaviour
{
    [Header("Timing (seconds)")]
    public float minInterval = 90f;
    public float maxInterval = 180f;
    [Tooltip("Wait before trying again when no boss was allowed yet (e.g. minRunTimeSeconds not reached)")]
    public float retryDelay = 15f;

    [Header("Spawn Position")]
    [Tooltip("If empty, uses StageControl.enemyStartX with Y = 0")]
    public Transform spawnPoint;

    [Header("References (auto-found if empty)")]
    public StageControl stageControl;
    public DifficultyManager difficultyManager;
    public MusicManager musicManager;

    private bool isRunning = false;
    private float timer;
    private BossBase activeBoss;
    private string previousMusicFolder;

    public bool IsBossActive => activeBoss != null;
    public BossBase ActiveBoss => activeBoss;

    public static event System.Action<BossBase> BossStarted;
    public static event System.Action<BossBase> BossEnded;

    void Awake()
    {
        if (stageControl == null) stageControl = GetComponent<StageControl>();
        if (stageControl == null) stageControl = FindObjectOfType<StageControl>();
        if (difficultyManager == null) difficultyManager = DifficultyManager.Instance;
        if (difficultyManager == null) difficultyManager = FindObjectOfType<DifficultyManager>();
        if (musicManager == null) musicManager = FindObjectOfType<MusicManager>();
    }

    void OnEnable() => BossBase.BossDefeated += HandleBossDefeated;
    void OnDisable() => BossBase.BossDefeated -= HandleBossDefeated;

    /// <summary>Called once the player has spawned.</summary>
    public void Begin()
    {
        isRunning = true;
        ScheduleNext();
    }

    void Update()
    {
        if (!isRunning) return;

        if (!ReferenceEquals(activeBoss, null))
        {
            // Unity's "== null" is true for destroyed objects: the boss vanished without being defeated
            if (activeBoss == null) EndBoss(null);
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            if (!TrySpawnBoss())
                timer = retryDelay;
        }
    }

    private void ScheduleNext()
    {
        timer = Random.Range(minInterval, Mathf.Max(minInterval, maxInterval));
    }

    private bool TrySpawnBoss()
    {
        StageDefinition stage = GameSession.CurrentStage;
        if (stage != null && !stage.bossesEnabled) return false;

        var db = ContentDatabase.Instance;
        if (db == null) return false;

        float runTime = GameSession.CurrentRun != null ? GameSession.CurrentRun.Duration : 0f;

        List<BossDefinition> candidates = db.Bosses
            .Where(b => b.prefab != null && b.CanAppearOn(stage) && runTime >= b.minRunTimeSeconds)
            .ToList();

        BossDefinition boss = WeightedRandom.Pick(candidates, b => b.spawnWeight);
        if (boss == null) return false;

        SpawnBoss(boss);
        return true;
    }

    public void SpawnBoss(BossDefinition boss)
    {
        Vector3 pos = spawnPoint != null
            ? spawnPoint.position
            : new Vector3(stageControl != null ? stageControl.enemyStartX : 25f, 0f, 0f);

        GameObject obj = Instantiate(boss.prefab, pos, Quaternion.identity);
        activeBoss = obj.GetComponent<BossBase>();

        if (activeBoss == null)
        {
            Debug.LogError($"BossDirector: Prefab of boss '{boss.id}' has no BossBase component!");
            Destroy(obj);
            ScheduleNext();
            return;
        }

        activeBoss.Init(boss);

        if (stageControl != null) stageControl.SetEnemySpawningPaused(true);
        if (difficultyManager != null) difficultyManager.PauseDifficultyProgression();

        if (musicManager != null && !string.IsNullOrEmpty(boss.musicFolder))
        {
            previousMusicFolder = musicManager.musicFolder;
            musicManager.PlayFolder(boss.musicFolder);
        }

        Debug.Log($"Boss incoming: {boss.DisplayName}");
        BossStarted?.Invoke(activeBoss);
    }

    private void HandleBossDefeated(BossBase boss)
    {
        if (boss != activeBoss) return;

        if (GameSession.CurrentRun != null)
            GameSession.CurrentRun.bossesDefeated++;

        EndBoss(boss);
    }

    private void EndBoss(BossBase boss)
    {
        activeBoss = null;

        if (stageControl != null) stageControl.SetEnemySpawningPaused(false);
        if (difficultyManager != null) difficultyManager.ResumeDifficultyProgression();

        if (musicManager != null && !string.IsNullOrEmpty(previousMusicFolder))
        {
            musicManager.PlayFolder(previousMusicFolder);
            previousMusicFolder = null;
        }

        BossEnded?.Invoke(boss);
        ScheduleNext();
    }
}
