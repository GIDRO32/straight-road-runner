// MenuControl.cs - New Script

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[System.Serializable]
public class CharacterData
{
    public string characterName;
    public GameObject prefab;           // From Assets/Prefabs/Players
    public Sprite uiIcon;              // Small icon near healthbar & selection button
    public Sprite bioIcon;             // Larger icon in info panel
    public Sprite menuDisplayArt;      // Big art on main menu
}

public class MenuControl : MonoBehaviour
{
    [Header("Character Data")]
    public CharacterData[] characters;

    [Header("UI References")]
    public Image selectedCharacterDisplay;   // Main menu big art
    public Image bioIconImage;              // Info panel icon
    public Text characterNameText;      // Character name in info
    public Button[] characterSelectButtons; // Buttons in selection menu

    private int currentSelectedIndex = 0;
    public SelectedCharacterSO selectedCharacterData;

    void Start()
    {
        if (characters.Length == 0) return;

        // Initialize first character
        UpdateCharacterDisplay(currentSelectedIndex);
        SetupSelectionButtons();
    }

    void SetupSelectionButtons()
    {
        for (int i = 0; i < characterSelectButtons.Length && i < characters.Length; i++)
        {
            int index = i;
            Button btn = characterSelectButtons[i];
            Image btnImage = btn.GetComponent<Image>();
            // In SetupSelectionButtons() – button icon assignment
            if (btnImage != null && characters[i].uiIcon != null)
            {
                btnImage.sprite = characters[i].uiIcon;   // uiIcon is a Sprite
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectCharacter(index));
        }
    }
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    public void SelectCharacter(int index)
    {
        if (index < 0 || index >= characters.Length) return;

        currentSelectedIndex = index;
        UpdateCharacterDisplay(index);

        // Save selection to ScriptableObject
        CharacterData data = characters[index];
        selectedCharacterData.characterPrefab = data.prefab;
        selectedCharacterData.uiIcon = data.uiIcon;
        selectedCharacterData.bioIcon = data.bioIcon;
        selectedCharacterData.menuDisplayArt = data.menuDisplayArt;
        selectedCharacterData.characterName = data.characterName;

        Debug.Log($"Saved selection: {data.characterName}");
    }

    // In UpdateCharacterDisplay(int index) – replace the three assignments
    void UpdateCharacterDisplay(int index)
    {
        CharacterData data = characters[index];

        // ---- UI images (all are Sprite → Image) ----
        if (selectedCharacterDisplay != null && data.menuDisplayArt != null)
            selectedCharacterDisplay.sprite = data.menuDisplayArt;

        if (bioIconImage != null && data.bioIcon != null)
            bioIconImage.sprite = data.bioIcon;

        if (characterNameText != null)
            characterNameText.text = data.characterName;
    }

    // Call this when starting the game
    public GameObject GetSelectedCharacterPrefab()
    {
        return characters[currentSelectedIndex].prefab;
    }
}