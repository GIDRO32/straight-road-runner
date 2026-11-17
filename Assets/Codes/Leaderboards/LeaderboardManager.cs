using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Entry point for leaderboards. Creates itself on game start (no scene setup needed).
/// Scores are queued on disk first, so a run finished offline is uploaded on the next successful connection.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public const string GlobalBoardId = "global";
    private const string PendingFileName = "pending_scores.json";

    public static LeaderboardManager Instance { get; private set; }

    public bool IsReady => service != null && service.IsReady;
    public string PlayerName => service != null ? service.PlayerName : null;

    /// <summary>Fired once the service is signed in and usable.</summary>
    public event Action Ready;
    /// <summary>Fired after a queued score was uploaded.</summary>
    public event Action<RunResult> ScoreUploaded;

    private ILeaderboardService service;
    private Task initTask;
    private bool isFlushing = false;

    [Serializable]
    private class PendingScores
    {
        public List<RunResult> items = new List<RunResult>();
    }
    private PendingScores pending = new PendingScores();

    private string PendingPath => Path.Combine(Application.persistentDataPath, PendingFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("LeaderboardManager");
        go.AddComponent<LeaderboardManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        service = new UgsLeaderboardService();
        LoadPending();
    }

    async void Start()
    {
        if (await EnsureReadyAsync())
            await FlushPendingAsync();
    }

    // ===== PUBLIC API =====

    /// <summary>Queue a finished run and try to upload it.</summary>
    public async void Submit(RunResult result)
    {
        if (result == null) return;

        pending.items.Add(result);
        SavePending();

        if (await EnsureReadyAsync())
            await FlushPendingAsync();
    }

    public Task<IReadOnlyList<LeaderboardRecord>> GetGlobalTopAsync(int limit = 50)
    {
        return GetTopAsync(GlobalBoardId, limit);
    }

    public Task<IReadOnlyList<LeaderboardRecord>> GetStageTopAsync(StageDefinition stage, int limit = 50)
    {
        return GetTopAsync(stage != null ? stage.LeaderboardId : GlobalBoardId, limit);
    }

    public async Task<IReadOnlyList<LeaderboardRecord>> GetTopAsync(string boardId, int limit = 50)
    {
        if (!await EnsureReadyAsync()) return new List<LeaderboardRecord>();

        try
        {
            return await service.GetTopAsync(boardId, limit);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaderboard: Couldn't load '{boardId}': {e.Message}");
            return new List<LeaderboardRecord>();
        }
    }

    public async Task<LeaderboardRecord> GetPlayerEntryAsync(string boardId)
    {
        if (!await EnsureReadyAsync()) return null;
        return await service.GetPlayerEntryAsync(boardId);
    }

    public async Task<bool> SetPlayerNameAsync(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || !await EnsureReadyAsync()) return false;

        try
        {
            await service.SetPlayerNameAsync(playerName.Trim());
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaderboard: Couldn't set player name: {e.Message}");
            return false;
        }
    }

    // ===== INTERNALS =====

    private async Task<bool> EnsureReadyAsync()
    {
        if (IsReady) return true;

        // Share one attempt between simultaneous callers; retry on the next call if it failed
        if (initTask == null || initTask.IsCompleted)
            initTask = InitializeAsync();

        await initTask;
        return IsReady;
    }

    private async Task InitializeAsync()
    {
        try
        {
            await service.InitializeAsync();
            Debug.Log($"Leaderboard: Signed in as {service.PlayerName}");
            Ready?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaderboard: Unity Gaming Services unavailable, scores stay queued. ({e.Message})");
        }
    }

    private async Task FlushPendingAsync()
    {
        if (isFlushing || pending.items.Count == 0) return;
        isFlushing = true;

        try
        {
            while (pending.items.Count > 0)
            {
                RunResult next = pending.items[0];
                await service.SubmitAsync(next, GlobalBoardId);

                pending.items.RemoveAt(0);
                SavePending();
                ScoreUploaded?.Invoke(next);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaderboard: Upload failed, will retry later. ({e.Message})");
        }
        finally
        {
            isFlushing = false;
        }
    }

    private void LoadPending()
    {
        try
        {
            if (File.Exists(PendingPath))
                pending = JsonUtility.FromJson<PendingScores>(File.ReadAllText(PendingPath)) ?? new PendingScores();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaderboard: Couldn't read pending scores: {e.Message}");
            pending = new PendingScores();
        }
    }

    private void SavePending()
    {
        try
        {
            File.WriteAllText(PendingPath, JsonUtility.ToJson(pending));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaderboard: Couldn't save pending scores: {e.Message}");
        }
    }
}
