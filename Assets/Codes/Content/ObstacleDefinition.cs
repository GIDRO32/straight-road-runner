using UnityEngine;

[CreateAssetMenu(fileName = "NewObstacle", menuName = "SRR/Obstacle")]
public class ObstacleDefinition : ContentDefinition
{
    [Header("Prefab")]
    [Tooltip("Spawned on top of platforms at Platform.obstacleAnchor")]
    public GameObject prefab;

    [Header("Info")]
    [TextArea(2, 6)] public string description;

    [Header("Spawning")]
    public float spawnWeight = 1f;
}
