using UnityEngine;

[System.Serializable]
public class EnemyStats
{
    [Tooltip("Hits needed to defeat")]
    public int health = 1;
    [Tooltip("Stamina the player loses when touching this enemy")]
    public float contactStaminaDamage = 10f;
    [Tooltip("How long the player is stunned when touching this enemy")]
    public float contactStunDuration = 1f;
    public int scoreValue = 100;
}

[System.Serializable]
public class EnemySounds
{
    public AudioClip[] spawn;
    public AudioClip[] hit;
    public AudioClip[] defeat;
    public AudioClip[] attack;
}

[CreateAssetMenu(fileName = "NewEnemy", menuName = "SRR/Enemy")]
public class EnemyDefinition : ContentDefinition
{
    [Header("Prefab")]
    [Tooltip("Must have a component deriving from EnemyBase")]
    public GameObject prefab;

    [Header("Info")]
    [TextArea(2, 6)] public string description;

    [Header("Gameplay")]
    public EnemyStats stats = new EnemyStats();
    public EnemySounds sounds = new EnemySounds();

    [Header("Spawning")]
    [Tooltip("Relative chance to be picked compared to other enemies in the same pool")]
    public float spawnWeight = 1f;
    public Vector2 spawnYRange = new Vector2(-2f, 2f);
}
