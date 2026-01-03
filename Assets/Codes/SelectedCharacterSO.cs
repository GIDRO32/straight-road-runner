using UnityEngine;

[CreateAssetMenu(fileName = "SelectedCharacter", menuName = "Game/Selected Character")]
public class SelectedCharacterSO : ScriptableObject
{
    public GameObject characterPrefab;
    public Sprite uiIcon;
    public Sprite bioIcon;
    public Sprite menuDisplayArt;
    public string characterName;
}