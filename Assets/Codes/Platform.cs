// Platform.cs - NEW SCRIPT (attach to Platform prefab)

using UnityEngine;

public class Platform : MonoBehaviour
{
    [Header("Platform Features")]
    public GameObject sign;
    public GameObject wall;
    public float wallSpawnChance = 0.33f;  // 20% chance to spawn wall
    public float signSpawnChance = 0.33f;  // 20% chance
    public float lowestPossibleChance = 0.75f; // 10% minimum chance

    void Start()
    {
        // 20% chance for each
        bool spawnSign = Random.value < signSpawnChance;
        bool spawnWall = Random.value < wallSpawnChance;

        if (sign != null)
            sign.SetActive(spawnSign);

        if (wall != null)
            wall.SetActive(spawnWall);
    }
}