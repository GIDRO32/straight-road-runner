// MenuControl.cs - New Script

using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuControl : MonoBehaviour
{
    // Characters come from ContentDatabase; picking one happens in CharacterSelectPanel

    [Header("Main Menu Art")]
    public Transform selectedCharacterDisplay;   // Main menu big art (parent transform for the Menu Art prefab)
    private GameObject currentDisplayInstance;

    void OnEnable()
    {
        GameSession.SelectedCharacterChanged += ShowCharacter;
    }

    void OnDisable()
    {
        GameSession.SelectedCharacterChanged -= ShowCharacter;
    }

    void Start()
    {
        ShowCharacter(GameSession.SelectedCharacter);
    }

    public void LoadScene(string sceneName)
    {
        
        SceneManager.LoadScene(sceneName);
    }
    public void SelectCharacterById(string characterId)
    {
        CharacterDefinition character = ContentDatabase.Instance != null ? ContentDatabase.Instance.GetCharacter(characterId) : null;
        if (character == null)
        {
            Debug.LogWarning($"Character '{characterId}' not found in ContentDatabase!");
            return;
        }
        GameSession.SelectedCharacter = character; // Fires SelectedCharacterChanged -> ShowCharacter
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
    // Shows the character's Menu Art prefab on the main menu
    void ShowCharacter(CharacterDefinition data)
    {
        // Destroy previous display art instance
        if (currentDisplayInstance != null)
        {
            Destroy(currentDisplayInstance);
            currentDisplayInstance = null;
        }

        if (data == null || selectedCharacterDisplay == null || data.menuArt == null) return;

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

    // Call this when starting the game
    public GameObject GetSelectedCharacterPrefab()
    {
        CharacterDefinition character = GameSession.SelectedCharacter;
        return character != null ? character.prefab : null;
    }
}
