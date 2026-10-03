using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Info about the run currently being played.</summary>
public class RunInfo
{
    public CharacterDefinition character;
    public StageDefinition stage;
    public float startTime;
    public int bossesDefeated;

    public float Duration => Time.time - startTime;
}

/// <summary>
/// Remembers the player's selected character and stage across scenes (saved by id in PlayerPrefs),
/// and tracks the current run.
/// </summary>
public static class GameSession
{
    private const string CharacterKey = "SelectedCharacterId";
    private const string StageKey = "SelectedStageId";

    public static RunInfo CurrentRun { get; private set; }

    public static CharacterDefinition SelectedCharacter
    {
        get
        {
            var db = ContentDatabase.Instance;
            if (db == null) return null;
            return db.GetCharacter(PlayerPrefs.GetString(CharacterKey, "")) ?? db.DefaultCharacter;
        }
        set
        {
            if (value == null) return;
            PlayerPrefs.SetString(CharacterKey, value.id);
            PlayerPrefs.Save();
        }
    }

    public static StageDefinition SelectedStage
    {
        get
        {
            var db = ContentDatabase.Instance;
            if (db == null) return null;
            return db.GetStage(PlayerPrefs.GetString(StageKey, "")) ?? db.DefaultStage;
        }
        set
        {
            if (value == null) return;
            PlayerPrefs.SetString(StageKey, value.id);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The stage being played in the active scene. Uses the selected stage when it belongs to this scene,
    /// otherwise the stage registered for the scene (e.g. when pressing Play on a stage scene in the Editor).
    /// </summary>
    public static StageDefinition CurrentStage
    {
        get
        {
            if (CurrentRun != null && CurrentRun.stage != null) return CurrentRun.stage;

            string scene = SceneManager.GetActiveScene().name;
            var selected = SelectedStage;
            if (selected != null && selected.sceneName == scene) return selected;

            var db = ContentDatabase.Instance;
            return db != null ? db.GetStageForScene(scene) : null;
        }
    }

    public static void LoadStage(StageDefinition stage)
    {
        if (stage == null)
        {
            Debug.LogWarning("GameSession: No stage to load!");
            return;
        }
        if (string.IsNullOrEmpty(stage.sceneName))
        {
            Debug.LogError($"GameSession: Stage '{stage.id}' has no scene name!");
            return;
        }

        SelectedStage = stage;
        CurrentRun = null;
        Time.timeScale = 1f;
        SceneManager.LoadScene(stage.sceneName);
    }

    /// <summary>Called when the player spawns.</summary>
    public static RunInfo BeginRun()
    {
        CurrentRun = null; // So CurrentStage resolves from the scene again
        CurrentRun = new RunInfo
        {
            character = SelectedCharacter,
            stage = CurrentStage,
            startTime = Time.time,
            bossesDefeated = 0
        };
        return CurrentRun;
    }

    public static void EndRun()
    {
        CurrentRun = null;
    }
}
