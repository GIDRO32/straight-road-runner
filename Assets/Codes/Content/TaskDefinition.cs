using UnityEngine;

public enum TaskType
{
    ScorePoints,  // Points earned in any run
    DefeatEnemy,  // Enemies or bosses defeated
    DestroyWall   // Walls destroyed
}

/// <summary>
/// A task that pays Shards: e.g. "Defeat 100 Ball Devils" gives 1 Shard, then the counter starts again.
/// Create one with Create > SRR > Task; it's registered in the ContentDatabase automatically.
/// Progress is counted by PlayerProgress.Report (score, kills and walls are reported by the game already).
/// </summary>
[CreateAssetMenu(fileName = "NewTask", menuName = "SRR/Task")]
public class TaskDefinition : ContentDefinition
{
    [Header("Task")]
    [TextArea(2, 4)] public string description;
    public TaskType type = TaskType.DefeatEnemy;
    [Tooltip("Which enemies / bosses / obstacles count. Empty = any.")]
    public ContentDefinition[] targets;
    [Tooltip("How many (points, kills, walls) to complete it")]
    public long goal = 100;

    [Header("Reward")]
    public int rewardShards = 1;
    [Tooltip("Starts again after completing (the counter resets). Untick for one-time tasks.")]
    public bool repeatable = true;

    public bool Matches(ContentDefinition subject)
    {
        if (targets == null || targets.Length == 0) return true;
        if (subject == null) return false;
        foreach (var target in targets)
            if (target != null && (target == subject || target.id == subject.id)) return true;
        return false;
    }

    /// <summary>The task's icon, else its first target's icon.</summary>
    public Sprite DisplayIcon
    {
        get
        {
            if (icon != null) return icon;
            if (targets != null)
                foreach (var target in targets)
                    if (target != null && target.icon != null) return target.icon;
            return null;
        }
    }
}
