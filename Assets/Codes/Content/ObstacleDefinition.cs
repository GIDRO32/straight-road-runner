using UnityEngine;

/// <summary>Obstacles in the same slot never appear together on one platform.</summary>
public enum ObstacleSlot
{
    Wall,
    Sign,
    Extra
}

/// <summary>
/// Something that can appear on a platform (wall, sign...).
/// Stages list which obstacles they use and how often (StageDefinition > Obstacles).
/// </summary>
[CreateAssetMenu(fileName = "NewObstacle", menuName = "SRR/Obstacle")]
public class ObstacleDefinition : ContentDefinition
{
    [Header("Prefab")]
    [Tooltip("Spawned as a child of the platform")]
    public GameObject prefab;
    [Tooltip("Position on the platform, in the platform's local space")]
    public Vector2 offset;
    public ObstacleSlot slot = ObstacleSlot.Wall;

    [Header("Info")]
    [TextArea(2, 6)] public string description;

    [Header("Walls")]
    [Tooltip("Hits needed to break (random between X and Y). 0 = use the prefab's WallShatter setting.")]
    public Vector2Int hitsToBreak = new Vector2Int(0, 0);
}
