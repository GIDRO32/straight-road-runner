using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ramps up a run's difficulty. Every pacing.difficultyInterval seconds it picks ONE random value
/// that hasn't reached its limit yet and steps it: platform/enemy pacing from StageDefinition.pacing,
/// plus every special platform and obstacle chance from the stage's lists, and the next boss's max health.
/// Paused during boss fights.
/// When everything has reached its limit, the endgame starts: platforms keep speeding up.
/// All numbers live on the stage asset, so each stage can ramp differently.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Header("Progression")]
    public bool isDifficultyProgressing = true; // Toggle for boss fights

    [Header("Stage References")]
    public StageControl stageControl;

    [Header("Debug")]
    [Tooltip("Log every nudge to the Console")]
    public bool logChanges = true;

    /// <summary>One value DifficultyManager can nudge.</summary>
    private class Track
    {
        public string name;
        public DifficultyRange range;
        public Func<float> get;
        public Action<float> set;

        public bool IsMaxed => range == null || range.IsMaxed(get());
        public void Step() => set(range.Next(get()));
    }

    private readonly List<Track> tracks = new List<Track>();
    private StagePacing pacing;
    private float difficultyTimer = 0f;
    private bool allLimitsReached = false;
    private float endgameTimer = 0f;

    public int Nudges { get; private set; }
    public bool AllLimitsReached => allLimitsReached;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        isDifficultyProgressing = false;
    }

    /// <summary>Called by StageControl once the stage's starting values are in place.</summary>
    public void Initialize(StageControl control, StagePacing stagePacing)
    {
        stageControl = control;
        pacing = stagePacing ?? new StagePacing();
        tracks.Clear();
        allLimitsReached = false;
        difficultyTimer = 0f;
        endgameTimer = 0f;
        Nudges = 0;

        if (stageControl == null) return;
        StageControl sc = stageControl;

        Add("Platform interval", pacing.platformInterval, () => sc.platformSpawnInterval, v => sc.platformSpawnInterval = v);
        Add("Platform lane chance", pacing.platformLaneChance, () => sc.currentPlatformSpawnChance, v => sc.currentPlatformSpawnChance = v);
        Add("Enemy interval", pacing.enemyInterval, () => sc.enemySpawnInterval, v => sc.enemySpawnInterval = v);
        Add("Enemy chance", pacing.enemyChance, () => sc.enemySpawnChance, v => sc.enemySpawnChance = v);
        Add("Max enemies", pacing.maxEnemies, () => sc.maxEnemies, v => sc.maxEnemies = Mathf.RoundToInt(v));
        Add("Boss max health", pacing.bossMaxHealth, () => sc.bossMaxHealth, v => sc.bossMaxHealth = Mathf.RoundToInt(v));

        foreach (var spawn in sc.PlatformSpawns)
        {
            var s = spawn;
            Add($"{Name(s.definition)} platform chance", s.range, () => s.chance, v => s.chance = v);
        }
        foreach (var spawn in sc.ObstacleSpawns)
        {
            var s = spawn;
            Add($"{Name(s.definition)} chance", s.range, () => s.chance, v => s.chance = v);
        }
    }

    private void Add(string name, DifficultyRange range, Func<float> get, Action<float> set)
    {
        if (range == null || range.step == 0f) return; // Constant: nothing to ramp
        tracks.Add(new Track { name = name, range = range, get = get, set = set });
    }

    private static string Name(ContentDefinition definition) => definition != null ? definition.DisplayName : "?";

    void Update()
    {
        if (!isDifficultyProgressing || pacing == null || stageControl == null) return;

        if (!allLimitsReached)
        {
            difficultyTimer += Time.deltaTime;
            if (difficultyTimer >= pacing.difficultyInterval)
            {
                difficultyTimer = 0f;
                IncreaseDifficulty();
            }
        }
        else
        {
            endgameTimer += Time.deltaTime;
            if (endgameTimer >= pacing.endgameInterval)
            {
                endgameTimer = 0f;
                IncreaseEndgameDifficulty();
            }
        }
    }

    // Step ONE random value that can still change
    private void IncreaseDifficulty()
    {
        var open = tracks.FindAll(t => !t.IsMaxed);
        if (open.Count == 0)
        {
            allLimitsReached = true;
            Debug.Log("ALL DIFFICULTY LIMITS REACHED! Entering endgame difficulty mode - platform speed will increase infinitely!");
            return;
        }

        Track track = open[UnityEngine.Random.Range(0, open.Count)];
        track.Step();
        Nudges++;

        if (logChanges)
            Debug.Log($"Difficulty: {track.name} → {track.get():0.##}");
    }

    // Endgame: keep making platforms faster (in whichever direction they scroll)
    private void IncreaseEndgameDifficulty()
    {
        float speed = stageControl.platformScrollSpeed;
        float direction = speed < 0f ? -1f : 1f;
        stageControl.platformScrollSpeed = speed + direction * pacing.endgameSpeedStep;

        if (logChanges)
            Debug.Log($"ENDGAME: Platform speed → {stageControl.platformScrollSpeed:F2} (increasing every {pacing.endgameInterval}s)");
    }

    // Call this when boss fight starts
    public void PauseDifficultyProgression()
    {
        isDifficultyProgressing = false;
        Debug.Log("Difficulty progression paused (Boss Fight)");
    }

    // Call this when boss fight ends
    public void ResumeDifficultyProgression()
    {
        isDifficultyProgressing = true;
        difficultyTimer = 0f; // Reset timer
        Debug.Log("Difficulty progression resumed");
    }
}
