using UnityEngine;
using System.Collections;

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Header("Difficulty Progression")]
    public float difficultyInterval = 15f;
    public bool isDifficultyProgressing = true; // Toggle for boss fights

    [Header("Stage References")]
    public StageControl stageControl;

    [Header("Platform Difficulty Ranges")]
    [SerializeField] private float platformIntervalMin = 1.5f;
    [SerializeField] private float platformIntervalMax = 4f;
    [SerializeField] private float platformSpawnChanceMin = 0.5f;
    [SerializeField] private float platformSpawnChanceMax = 0.9f;

    [Header("Enemy Difficulty Ranges")]
    [SerializeField] private int maxEnemiesMin = 3;
    [SerializeField] private int maxEnemiesMax = 10;
    [SerializeField] private float enemySpawnIntervalMin = 8f;
    [SerializeField] private float enemySpawnIntervalMax = 25f;
    [SerializeField] private float enemySpawnChanceMin = 0.2f;
    [SerializeField] private float enemySpawnChanceMax = 0.6f;

    [Header("Platform Feature Ranges")]
    [SerializeField] private float wallSpawnChanceMin = 0.2f;
    [SerializeField] private float wallSpawnChanceMax = 0.7f;
    [SerializeField] private float signSpawnChanceMin = 0.15f;
    [SerializeField] private float signSpawnChanceMax = 0.4f;
    [SerializeField] private float fragileChanceMin = 0f;
    [SerializeField] private float fragileChanceMax = 0.5f;

    [Header("Difficulty Step Sizes")]
    [SerializeField] private float platformIntervalStep = 0.1f;
    [SerializeField] private float platformSpawnChanceStep = -0.02f;
    [SerializeField] private int maxEnemiesStep = 1;
    [SerializeField] private float enemySpawnIntervalStep = -0.5f;
    [SerializeField] private float enemySpawnChanceStep = 0.02f;
    [SerializeField] private float wallSpawnChanceStep = 0.03f;
    [SerializeField] private float signSpawnChanceStep = -0.02f;
    [SerializeField] private float fragileChanceStep = 0.03f;

    private float difficultyTimer = 0f;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Update()
    {
        if (!isDifficultyProgressing) return;

        difficultyTimer += Time.deltaTime;

        if (difficultyTimer >= difficultyInterval)
        {
            difficultyTimer = 0f;
            IncreaseDifficulty();
        }
    }

    private void IncreaseDifficulty()
    {
        if (stageControl == null) return;

        // Randomly pick one difficulty aspect to modify
        int randomChoice = Random.Range(0, 8);

        switch (randomChoice)
        {
            case 0: // Increase platform spawn interval (less platforms)
                stageControl.platformSpawnInterval = Mathf.Min(
                    stageControl.platformSpawnInterval + platformIntervalStep,
                    platformIntervalMax
                );
                Debug.Log($"Difficulty: Platform interval → {stageControl.platformSpawnInterval:F2}s");
                break;

            case 1: // Decrease platform spawn chance
                stageControl.currentPlatformSpawnChance = Mathf.Max(
                    stageControl.currentPlatformSpawnChance + platformSpawnChanceStep,
                    platformSpawnChanceMin
                );
                Debug.Log($"Difficulty: Platform spawn chance → {stageControl.currentPlatformSpawnChance:F2}");
                break;

            case 2: // Increase max enemies
                stageControl.maxEnemies = Mathf.Min(
                    stageControl.maxEnemies + maxEnemiesStep,
                    maxEnemiesMax
                );
                Debug.Log($"Difficulty: Max enemies → {stageControl.maxEnemies}");
                break;

            case 3: // Decrease enemy spawn interval (more frequent)
                stageControl.enemySpawnInterval = Mathf.Max(
                    stageControl.enemySpawnInterval + enemySpawnIntervalStep,
                    enemySpawnIntervalMin
                );
                Debug.Log($"Difficulty: Enemy interval → {stageControl.enemySpawnInterval:F1}s");
                break;

            case 4: // Increase enemy spawn chance
                stageControl.enemySpawnChance = Mathf.Min(
                    stageControl.enemySpawnChance + enemySpawnChanceStep,
                    enemySpawnChanceMax
                );
                Debug.Log($"Difficulty: Enemy spawn chance → {stageControl.enemySpawnChance:F2}");
                break;

            case 5: // Increase wall spawn chance
                Platform.wallSpawnChance = Mathf.Min(
                    Platform.wallSpawnChance + wallSpawnChanceStep,
                    wallSpawnChanceMax
                );
                Debug.Log($"Difficulty: Wall chance → {Platform.wallSpawnChance:F2}");
                break;

            case 6: // Decrease sign spawn chance
                Platform.signSpawnChance = Mathf.Max(
                    Platform.signSpawnChance + signSpawnChanceStep,
                    signSpawnChanceMin
                );
                Debug.Log($"Difficulty: Sign chance → {Platform.signSpawnChance:F2}");
                break;

            case 7: // Increase fragile platform chance
                Platform.fragileChance = Mathf.Min(
                    Platform.fragileChance + fragileChanceStep,
                    fragileChanceMax
                );
                Debug.Log($"Difficulty: Fragile chance → {Platform.fragileChance:F2}");
                break;
        }
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