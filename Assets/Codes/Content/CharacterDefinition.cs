using UnityEngine;
using UnityEngine.Serialization;

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

public enum StatGrade { F, E, D, C, B, A, S }
public enum SizeClass { XS, S, M, L, XL }
public enum WeightClass { Light, Medium, Heavy }

/// <summary>What Character Select shows under "Stats". Stamina is taken from the gameplay stats.</summary>
[System.Serializable]
public class CharacterProfile
{
    public StatGrade speed = StatGrade.C;
    public StatGrade jump = StatGrade.C;
    public SizeClass size = SizeClass.M;
    public WeightClass weight = WeightClass.Medium;
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
    [Tooltip("Image shown in the middle of Character Select")]
    [FormerlySerializedAs("portrait")]
    public Sprite selectArtwork;
    [Tooltip("Animated art on the title screen (prefab with SpriteRenderer/Animator)")]
    public GameObject menuArt;

    [Header("Character Select Info")]
    [TextArea(4, 12)] public string bio;
    public CharacterProfile profile = new CharacterProfile();

    [Header("Gameplay")]
    public CharacterStats stats = new CharacterStats();
    public MoveInfo[] moveset;
}
