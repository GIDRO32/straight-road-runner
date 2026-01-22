using UnityEngine;

[CreateAssetMenu(fileName = "SelectedCharacter", menuName = "Game/Selected Character")]
public class SelectedCharacterSO : ScriptableObject
{
    [Header("Default Character (Fallback)")]
    public GameObject defaultCharacterPrefab; // Assign PlaceHolderMan here
    
    [Header("Selected Character Data")]
    public GameObject characterPrefab;
    public Sprite uiIcon;
    public Sprite bioIcon;
    public Sprite menuDisplayArt;
    public string characterName;
    
    // NEW METHOD: Get character with fallback
    public GameObject GetCharacterPrefab()
    {
        if (characterPrefab != null)
            return characterPrefab;
            
        if (defaultCharacterPrefab != null)
        {
            Debug.Log("No character selected, using default character");
            return defaultCharacterPrefab;
        }
            
        Debug.LogError("No character available!");
        return null;
    }
}