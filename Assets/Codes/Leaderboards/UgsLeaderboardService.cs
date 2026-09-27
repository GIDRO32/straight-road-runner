using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;
using UgsEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;

/// <summary>
/// Leaderboards through Unity Gaming Services (package com.unity.services.leaderboards).
/// Players sign in anonymously. Each score carries RunMetadata (character, stage, duration, bosses).
/// Leaderboards must exist in the Unity Cloud dashboard with matching ids.
/// </summary>
public class UgsLeaderboardService : ILeaderboardService
{
    public bool IsReady =>
        UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance.IsSignedIn;

    public string PlayerName => IsReady ? AuthenticationService.Instance.PlayerName : null;

    public async Task InitializeAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async Task SubmitAsync(RunResult result, string globalBoardId)
    {
        var options = new AddPlayerScoreOptions
        {
            Metadata = new RunMetadata
            {
                characterId = result.characterId,
                stageId = result.stageId,
                durationSeconds = result.durationSeconds,
                bossesDefeated = result.bossesDefeated
            }
        };

        if (!string.IsNullOrEmpty(result.stageLeaderboardId))
            await LeaderboardsService.Instance.AddPlayerScoreAsync(result.stageLeaderboardId, result.score, options);

        if (!string.IsNullOrEmpty(globalBoardId) && globalBoardId != result.stageLeaderboardId)
            await LeaderboardsService.Instance.AddPlayerScoreAsync(globalBoardId, result.score, options);
    }

    public async Task<IReadOnlyList<LeaderboardRecord>> GetTopAsync(string boardId, int limit)
    {
        var page = await LeaderboardsService.Instance.GetScoresAsync(
            boardId,
            new GetScoresOptions { Limit = limit, IncludeMetadata = true });

        var records = new List<LeaderboardRecord>();
        foreach (UgsEntry entry in page.Results)
            records.Add(ToRecord(entry));
        return records;
    }

    public async Task<LeaderboardRecord> GetPlayerEntryAsync(string boardId)
    {
        try
        {
            UgsEntry entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(
                boardId,
                new GetPlayerScoreOptions { IncludeMetadata = true });
            return ToRecord(entry);
        }
        catch (System.Exception e)
        {
            // Thrown when the player has no score on this board yet
            Debug.Log($"Leaderboard: No player entry on '{boardId}' ({e.Message})");
            return null;
        }
    }

    public async Task SetPlayerNameAsync(string playerName)
    {
        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
    }

    private static LeaderboardRecord ToRecord(UgsEntry entry)
    {
        var record = new LeaderboardRecord
        {
            rank = entry.Rank + 1, // UGS ranks are 0-based
            playerId = entry.PlayerId,
            playerName = entry.PlayerName,
            score = (int)entry.Score
        };
        record.ApplyMetadataJson(entry.Metadata);
        return record;
    }
}
