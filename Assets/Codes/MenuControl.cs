// MenuControl.cs - New Script

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MenuControl : MonoBehaviour
{
    // Characters come from ContentDatabase (Assets/Resources/ContentDatabase.asset)
    private IReadOnlyList<CharacterDefinition> characters = new List<CharacterDefinition>();

    [Header("UI References")]
    public Transform selectedCharacterDisplay;   // CHANGED: Main menu big art (parent transform for prefab)
    public Image bioIconImage;                  // Info panel icon
    public TMP_Text characterNameText;              // Character name in info
    public Button[] characterSelectButtons;     // Buttons in selection menu
    private GameObject currentDisplayInstance;  // NEW: Track spawned display art

    private int currentSelectedIndex = 0;

    void Start()
    {
        if (ContentDatabase.Instance != null)
            characters = ContentDatabase.Instance.Characters;

        if (characters.Count == 0) return;

        // Load saved selection (by id) or default character
        int savedIndex = IndexOf(GameSession.SelectedCharacter);
        currentSelectedIndex = Mathf.Max(0, savedIndex);

        UpdateCharacterDisplay(currentSelectedIndex);
        SetupSelectionButtons();
    }

    int IndexOf(CharacterDefinition character)
    {
        for (int i = 0; i < characters.Count; i++)
            if (characters[i] == character) return i;
        return -1;
    }

    void SetupSelectionButtons()
    {
        for (int i = 0; i < characterSelectButtons.Length && i < characters.Count; i++)
        {
            int index = i;
            Button btn = characterSelectButtons[i];
            Image btnImage = btn.GetComponent<Image>();
            // Button icon assignment
            if (btnImage != null && characters[i].icon != null)
            {
                btnImage.sprite = characters[i].icon;
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
        if (index < 0 || index >= characters.Count) return;

        currentSelectedIndex = index;
        UpdateCharacterDisplay(index);

        // Save selection (persisted by id)
        CharacterDefinition data = characters[index];
        GameSession.SelectedCharacter = data;

        Debug.Log($"Saved selection: {data.DisplayName}");
    }

    public void SelectCharacterById(string characterId)
    {
        SelectCharacter(IndexOf(ContentDatabase.Instance?.GetCharacter(characterId)));
    }

    // ===== STAGES =====

    public void SelectStage(string stageId)
    {
        StageDefinition stage = ContentDatabase.Instance?.GetStage(stageId);
        if (stage == null)
        {
            Debug.LogWarning($"Stage '{stageId}' not found in ContentDatabase!");
            return;
        }
        GameSession.SelectedStage = stage;
    }

    public void PlaySelectedStage()
    {
        GameSession.LoadStage(GameSession.SelectedStage);
    }

    public void PlayStage(string stageId)
    {
        SelectStage(stageId);
        PlaySelectedStage();
    }

    public void PlayChaosMode()
    {
        GameSession.LoadStage(ContentDatabase.Instance?.ChaosStage);
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
        CharacterDefinition data = characters[index];

        // Destroy previous display art instance
        if (currentDisplayInstance != null)
        {
            Destroy(currentDisplayInstance);
            currentDisplayInstance = null;
        }

        // Spawn new display art prefab
        if (selectedCharacterDisplay != null && data.menuArt != null)
        {
            currentDisplayInstance = Instantiate(
                data.menuArt,
                selectedCharacterDisplay.position,
                Quaternion.identity,
                selectedCharacterDisplay
            );

            // Reset local position/scale if needed
            currentDisplayInstance.transform.localPosition = Vector3.zero;
            currentDisplayInstance.transform.localScale = Vector3.one;
        }

        // Bio icon (still a sprite)
        if (bioIconImage != null && data.portrait != null)
            bioIconImage.sprite = data.portrait;

        // Character name
        if (characterNameText != null)
            characterNameText.text = data.DisplayName;
    }

    // Call this when starting the game
    public GameObject GetSelectedCharacterPrefab()
    {
        return characters.Count > 0 ? characters[currentSelectedIndex].prefab : null;
    }
}