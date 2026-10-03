using UnityEngine;

/// <summary>Result of a finished run, submitted to the leaderboards.</summary>
[System.Serializable]
public class RunResult
{
    public int score;
    public string characterId;
    public string stageId;
    public string stageLeaderboardId;
    public float durationSeconds;
    public int bossesDefeated;
}

/// <summary>Extra info stored with each leaderboard score (UGS "metadata").</summary>
[System.Serializable]
public class RunMetadata
{
    public string characterId;
    public string stageId;
    public float durationSeconds;
    public int bossesDefeated;
}

/// <summary>One row of a leaderboard, with the character and stage it was played with.</summary>
public class LeaderboardRecord
{
    /// <summary>1-based rank</summary>
    public int rank;
    public string playerId;
    public string playerName;
    public int score;
    public string characterId;
    public string stageId;
    public float durationSeconds;
    public int bossesDefeated;

    // Resolved through ContentDatabase. Null if the content was removed/renamed; use the ids instead.
    public CharacterDefinition Character => ContentDatabase.Instance != null ? ContentDatabase.Instance.GetCharacter(characterId) : null;
    public StageDefinition Stage => ContentDatabase.Instance != null ? ContentDatabase.Instance.GetStage(stageId) : null;

    public string CharacterName => Character != null ? Character.DisplayName : characterId;
    public string StageName => Stage != null ? Stage.DisplayName : stageId;
    public Sprite CharacterIcon => Character != null ? Character.icon : null;

    public void ApplyMetadataJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            RunMetadata meta = JsonUtility.FromJson<RunMetadata>(json);
            if (meta == null) return;

            characterId = meta.characterId;
            stageId = meta.stageId;
            durationSeconds = meta.durationSeconds;
            bossesDefeated = meta.bossesDefeated;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Leaderboard: Couldn't read score metadata: {e.Message}");
        }
    }
}
