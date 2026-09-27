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
    public GameObject menuDisplayArt;  // CHANGED: Big art on main menu (can be animated prefab)
}

public class MenuControl : MonoBehaviour
{
    [Header("Character Data")]
    public CharacterData[] characters;

    [Header("UI References")]
    public Transform selectedCharacterDisplay;   // CHANGED: Main menu big art (parent transform for prefab)
    public Image bioIconImage;                  // Info panel icon
    public Text characterNameText;              // Character name in info
    public Button[] characterSelectButtons;     // Buttons in selection menu
    private GameObject currentDisplayInstance;  // NEW: Track spawned display art

    private int currentSelectedIndex = 0;
    public SelectedCharacterSO selectedCharacterData;

    void Start()
    {
        if (characters.Length == 0) return;

        // Load saved selection or default to first character
        int savedIndex = PlayerPrefs.GetInt("SelectedCharacterIndex", 0);
        savedIndex = Mathf.Clamp(savedIndex, 0, characters.Length - 1);

        currentSelectedIndex = savedIndex;
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

        // Save selection
        CharacterData data = characters[index];
        selectedCharacterData.characterPrefab = data.prefab;
        selectedCharacterData.uiIcon = data.uiIcon;
        selectedCharacterData.bioIcon = data.bioIcon;
        selectedCharacterData.menuDisplayArt = data.menuDisplayArt;
        selectedCharacterData.characterName = data.characterName;

        // Persist selection
        PlayerPrefs.SetInt("SelectedCharacterIndex", index);
        PlayerPrefs.Save();

        Debug.Log($"Saved selection: {data.characterName}");
    }
    public void QuitGame()
    {
        Debug.Log("Quitting game...");

#if UNITY_EDITOR
        // Stop playing in editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit application in build
        Application.Quit();
#endif
    }

    public void OpenBrowserLink(string link)
    {
        if (string.IsNullOrEmpty(link))
        {
            Debug.LogWarning("Cannot open browser: link is empty!");
            return;
        }

        // Ensure URL has a protocol
        if (!link.StartsWith("http://") && !link.StartsWith("https://"))
        {
            link = "https://" + link;
        }

        Debug.Log($"Opening browser link: {link}");

        try
        {
            Application.OpenURL(link);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open browser link: {e.Message}");
        }
    }
    // In UpdateCharacterDisplay(int index) – replace the three assignments
    void UpdateCharacterDisplay(int index)
    {
        CharacterData data = characters[index];

        // Destroy previous display art instance
        if (currentDisplayInstance != null)
        {
            Destroy(currentDisplayInstance);
            currentDisplayInstance = null;
        }

        // Spawn new display art prefab
        if (selectedCharacterDisplay != null && data.menuDisplayArt != null)
        {
            currentDisplayInstance = Instantiate(
                data.menuDisplayArt,
                selectedCharacterDisplay.position,
                Quaternion.identity,
                selectedCharacterDisplay
            );

            // Reset local position/scale if needed
            currentDisplayInstance.transform.localPosition = Vector3.zero;
            currentDisplayInstance.transform.localScale = Vector3.one;
        }

        // Bio icon (still a sprite)
        if (bioIconImage != null && data.bioIcon != null)
            bioIconImage.sprite = data.bioIcon;

        // Character name
        if (characterNameText != null)
            characterNameText.text = data.characterName;
    }

    // Call this when starting the game
    public GameObject GetSelectedCharacterPrefab()
    {
        return characters[currentSelectedIndex].prefab;
    }
}