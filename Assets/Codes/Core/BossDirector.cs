using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Boss fights. Whenever a regular song ends, a fight starts:
/// 1. Stage difficulty and normal enemy spawning freeze, the music switches to the fight folder (Music/Intense),
///    which keeps playing until the boss is defeated.
/// 2. A red warning with the boss's title fades in and out in the middle of the screen (BossHud).
/// 3. A random boss from the ContentDatabase is summoned with the stage's current Boss Max Health
///    (same for every boss, raised by DifficultyManager), and its health bar + icon appear.
/// 4. When it's defeated the stage music and everything else resume. The next song's end brings the next boss.
/// Without a MusicManager (or songs), a fight starts every fallbackInterval seconds instead.
/// Bosses come from ContentDatabase, so new bosses only need a BossDefinition asset.
/// Added automatically to the StageControl object by IntroControl.
/// </summary>
public class BossDirector : MonoBehaviour
{
    public static BossDirector Instance { get; private set; }

    [Header("Timing")]
    [Tooltip("Only used when there's no music to follow: seconds of play between fights")]
    public float fallbackInterval = 90f;

    [Header("Introduction")]
    public string warningText = "WARNING!";
    [Tooltip("Pause between the music switch and the warning")]
    public float warningDelay = 0.5f;
    public float warningFadeIn = 0.6f;
    public float warningHold = 1.8f;
    public float warningFadeOut = 0.5f;
    public AudioClip[] warningSounds;

    [Header("Music")]
    [Tooltip("StreamingAssets folder played during fights (a boss can override it)")]
    public string fightMusicFolder = "Music/Intense";

    [Header("Spawn Position")]
    [Tooltip("Optional. Bosses usually place themselves (e.g. the crab sits on the right screen edge).")]
    public Transform spawnPoint;

    [Header("References (auto-found or created if empty)")]
    public StageControl stageControl;
    public DifficultyManager difficultyManager;
    public MusicManager musicManager;
    public BossHud hud;

    private bool isRunning = false;
    private bool fightInProgress = false;
    private bool fightRequested = false;
    private float timer;
    private BossBase activeBoss;
    private string previousMusicFolder;

    public bool IsFightInProgress => fightInProgress;
    public bool IsBossActive => activeBoss != null;
    public BossBase ActiveBoss => activeBoss;

    public static event System.Action<BossBase> BossStarted;
    public static event System.Action<BossBase> BossEnded;

    void Awake()
    {
        Instance = this;
        if (stageControl == null) stageControl = GetComponent<StageControl>();
        if (stageControl == null) stageControl = FindObjectOfType<StageControl>();
        if (difficultyManager == null) difficultyManager = DifficultyManager.Instance;
        if (difficultyManager == null) difficultyManager = FindObjectOfType<DifficultyManager>();
        if (musicManager == null) musicManager = FindObjectOfType<MusicManager>();
        if (hud == null) hud = FindObjectOfType<BossHud>();
        if (hud == null) hud = new GameObject("BossHud").AddComponent<BossHud>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        BossBase.BossDefeated += HandleBossDefeated;
        if (musicManager != null) musicManager.TrackFinished += HandleTrackFinished;
    }

    void OnDisable()
    {
        BossBase.BossDefeated -= HandleBossDefeated;
        if (musicManager != null) musicManager.TrackFinished -= HandleTrackFinished;
    }

    /// <summary>Called once the player has spawned.</summary>
    public void Begin()
    {
        isRunning = true;
        timer = fallbackInterval;
    }

    // A regular song ended: boss time
    private void HandleTrackFinished(string folder)
    {
        if (!isRunning || fightInProgress || Time.timeScale <= 0f) return;
        if (folder == fightMusicFolder) return;
        fightRequested = true;
        TryStartFight(); // Right away, so the fight music replaces the next regular song
    }

    // No music to follow (no MusicManager, or no songs in its folder)
    private bool UsesFallbackTimer =>
        musicManager == null || (!musicManager.IsLoading() && musicManager.GetTotalTracks() == 0);

    void Update()
    {
        if (!isRunning) return;

        if (fightInProgress)
        {
            // Unity's "== null" is true for destroyed objects: the boss vanished without being defeated
            if (!ReferenceEquals(activeBoss, null) && activeBoss == null)
                EndFight(null);
            return;
        }

        if (UsesFallbackTimer)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = fallbackInterval;
                fightRequested = true;
            }
        }

        if (fightRequested) TryStartFight();
    }

    private void TryStartFight()
    {
        if (fightInProgress) return;
        fightRequested = false;
        BossDefinition boss = PickBoss();
        if (boss != null) StartCoroutine(FightSequence(boss)); // Otherwise wait for the next song
    }

    // Testing: right-click the BossDirector component in Play mode (it's on the StageControl object)
    [ContextMenu("Start Boss Fight Now")]
    private void StartFightNow()
    {
        if (isRunning && !fightInProgress) fightRequested = true;
    }

    private BossDefinition PickBoss()
    {
        StageDefinition stage = GameSession.CurrentStage;
        if (stage != null && !stage.bossesEnabled) return null;

        var db = ContentDatabase.Instance;
        if (db == null) return null;

        float runTime = GameSession.CurrentRun != null ? GameSession.CurrentRun.Duration : 0f;

        List<BossDefinition> candidates = db.Bosses
            .Where(b => b.prefab != null && b.CanAppearOn(stage) && runTime >= b.minRunTimeSeconds)
            .ToList();

        return WeightedRandom.Pick(candidates, b => b.spawnWeight);
    }

    /// <summary>Starts a fight with this boss right away (warning included).</summary>
    public void StartFight(BossDefinition boss)
    {
        if (fightInProgress || boss == null) return;
        StartCoroutine(FightSequence(boss));
    }

    private IEnumerator FightSequence(BossDefinition boss)
    {
        fightInProgress = true;

        // 1. Freeze the stage: no difficulty changes, no new enemies
        if (stageControl != null) stageControl.SetEnemySpawningPaused(true);
        if (difficultyManager != null) difficultyManager.PauseDifficultyProgression();

        // 2. Fight music
        string folder = !string.IsNullOrEmpty(boss.musicFolder) ? boss.musicFolder : fightMusicFolder;
        if (musicManager != null && !string.IsNullOrEmpty(folder) && folder != musicManager.musicFolder)
        {
            previousMusicFolder = musicManager.musicFolder;
            musicManager.PlayFolder(folder);
        }

        // 3. Warning
        yield return new WaitForSeconds(warningDelay);
        if (warningSounds != null && warningSounds.Length > 0)
            AudioManager.Instance?.PlayRandomClip(warningSounds);
        if (hud != null)
            yield return hud.PlayWarning(warningText, boss.Title, warningFadeIn, warningHold, warningFadeOut);

        // 4. Summon
        if (!SpawnBoss(boss))
            EndFight(null);
    }

    private bool SpawnBoss(BossDefinition boss)
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
            return false;
        }

        activeBoss.Init(boss);
        if (stageControl != null) activeBoss.SetMaxHealth(stageControl.bossMaxHealth); // Same for every boss
        if (hud != null) hud.ShowBoss(activeBoss);

        Debug.Log($"Boss incoming: {boss.DisplayName}");
        BossStarted?.Invoke(activeBoss);
        return true;
    }

    private void HandleBossDefeated(BossBase boss)
    {
        if (boss != activeBoss) return;

        if (GameSession.CurrentRun != null)
            GameSession.CurrentRun.bossesDefeated++;

        EndFight(boss);
    }

    private void EndFight(BossBase boss)
    {
        activeBoss = null;
        fightInProgress = false;

        if (hud != null) hud.HideBoss();
        if (stageControl != null) stageControl.SetEnemySpawningPaused(false);
        if (difficultyManager != null) difficultyManager.ResumeDifficultyProgression();

        if (musicManager != null && !string.IsNullOrEmpty(previousMusicFolder))
        {
            musicManager.PlayFolder(previousMusicFolder);
            previousMusicFolder = null;
        }

        BossEnded?.Invoke(boss);
        timer = fallbackInterval;
    }
}
