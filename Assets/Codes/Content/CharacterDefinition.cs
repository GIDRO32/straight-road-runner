using UnityEngine;

[System.Serializable]
public class CharacterStats
{
    [Header("Movement")]
    public float runSpeed = 7f;
    public float superRunSpeed = 15f;
    public float jumpHeight = 10f;
    public float superJumpHeight = 15f;
    public float dashSpeedMultiplier = 2f;

    [Header("Combat")]
    public float punchSpeedReduction = 4f;
    public float uppercutSpeedReduction = 2f;

    [Header("Stamina")]
    public float maxStamina = 100f;
}

[System.Serializable]
public class MoveInfo
{
    public string moveName;
    [Tooltip("How to perform it, e.g. \"Z\" or \"Hold S, then C\"")]
    public string input;
    [TextArea] public string description;
    public Sprite icon;
}

[CreateAssetMenu(fileName = "NewCharacter", menuName = "SRR/Character")]
public class CharacterDefinition : ContentDefinition
{
    [Header("Prefabs & Art")]
    [Tooltip("Gameplay prefab spawned from the portal (Assets/Prefabs/Players)")]
    public GameObject prefab;
    [Tooltip("Large icon for the bio / info panel")]
    public Sprite portrait;
    [Tooltip("Big art shown on the menu / character select (can be an animated prefab)")]
    public GameObject menuArt;

    [Header("Info")]
    [TextArea(4, 12)] public string bio;

    [Header("Gameplay")]
    public CharacterStats stats = new CharacterStats();
    public MoveInfo[] moveset;
}
