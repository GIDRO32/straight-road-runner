using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Leaderboard backend. UgsLeaderboardService is the Unity Gaming Services implementation;
/// another backend (Steam, custom server) only needs to implement this interface.
/// </summary>
public interface ILeaderboardService
{
    bool IsReady { get; }
    string PlayerName { get; }

    Task InitializeAsync();

    /// <summary>Submits the score to the stage's board and to the global board.</summary>
    Task SubmitAsync(RunResult result, string globalBoardId);

    Task<IReadOnlyList<LeaderboardRecord>> GetTopAsync(string boardId, int limit);

    /// <summary>The local player's entry, or null if they have no score on this board yet.</summary>
    Task<LeaderboardRecord> GetPlayerEntryAsync(string boardId);

    Task SetPlayerNameAsync(string playerName);
}
