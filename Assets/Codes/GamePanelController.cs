using UnityEngine;
using System.Collections.Generic;

public class GamePanelController : MonoBehaviour
{
    public static GamePanelController Instance { get; private set; }

    [System.Serializable]
    public class PanelEntry
    {
        public string panelName;
        public PanelManager panel;
        public KeyCode hotkey = KeyCode.None; // Optional hotkey
    }

    [Header("Panel Registry")]
    public PanelEntry[] panels;

    [Header("Special Panels (Optional - set by name if exists)")]
    public string pausePanelName = "Pause";
    public string settingsPanelName = "Settings";

    private Dictionary<string, PanelManager> panelDictionary;
    private bool isPaused = false;
    private Stack<string> panelStack; // Track panel history for back button

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        InitializePanels();
    }

    void Start()
    {
        // Close all panels on start
        CloseAllPanels(true);
    }

    private void InitializePanels()
    {
        panelDictionary = new Dictionary<string, PanelManager>();
        panelStack = new Stack<string>();

        foreach (PanelEntry entry in panels)
        {
            if (entry.panel != null && !string.IsNullOrEmpty(entry.panelName))
            {
                panelDictionary[entry.panelName] = entry.panel;
            }
        }
    }

    void Update()
    {
        // ESC key handling
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }

        // Check hotkeys
        foreach (PanelEntry entry in panels)
        {
            if (entry.hotkey != KeyCode.None && Input.GetKeyDown(entry.hotkey))
            {
                TogglePanel(entry.panelName);
            }
        }
    }

    private void HandleEscapeKey()
    {
        // If any panel is open, close the top one
        if (panelStack.Count > 0)
        {
            CloseTopPanel();
        }
        // Otherwise toggle pause
        else if (HasPanel(pausePanelName))
        {
            TogglePause();
        }
    }

    // ===== MAIN API METHODS =====

    public void OpenPanel(string panelName)
    {
        if (!panelDictionary.ContainsKey(panelName))
        {
            Debug.LogWarning($"Panel '{panelName}' not found!");
            return;
        }

        PanelManager panel = panelDictionary[panelName];

        isPaused = true;
        Time.timeScale = 0f;

        panel.OpenPanel();

        // Add to stack if not already on top
        if (panelStack.Count == 0 || panelStack.Peek() != panelName)
        {
            panelStack.Push(panelName);
        }

        Debug.Log($"Opened panel: {panelName}");
    }

    public void ClosePanel(string panelName)
    {
        if (!panelDictionary.ContainsKey(panelName))
        {
            Debug.LogWarning($"Panel '{panelName}' not found!");
            return;
        }

        PanelManager panel = panelDictionary[panelName];
        panel.ClosePanel();

        // Remove from stack
        if (panelStack.Count > 0 && panelStack.Peek() == panelName)
        {
            panelStack.Pop();
        }

        isPaused = false;
        Time.timeScale = 1f;


        Debug.Log($"Closed panel: {panelName}");
    }

    public void TogglePanel(string panelName, bool pauseGame = false)
    {
        if (!panelDictionary.ContainsKey(panelName))
        {
            Debug.LogWarning($"Panel '{panelName}' not found!");
            return;
        }

        PanelManager panel = panelDictionary[panelName];

        if (panel.gameObject.activeSelf)
            ClosePanel(panelName);
        else
            OpenPanel(panelName);
    }

    public void CloseTopPanel()
    {
        if (panelStack.Count > 0)
        {
            string topPanel = panelStack.Pop();
            panelDictionary[topPanel].ClosePanel();

            // Resume game if no panels left
            if (panelStack.Count == 0 && isPaused)
            {
                isPaused = false;
                Time.timeScale = 1f;
            }
        }
    }

    public void CloseAllPanels(bool instant = false)
    {
        foreach (var panel in panelDictionary.Values)
        {
            if (instant)
            {
                panel.gameObject.SetActive(false);
            }
            else
            {
                panel.ClosePanel();
            }
        }

        panelStack.Clear();
        isPaused = false;
        Time.timeScale = 1f;
    }

    public bool HasPanel(string panelName)
    {
        return panelDictionary.ContainsKey(panelName);
    }

    public bool IsPanelOpen(string panelName)
    {
        return HasPanel(panelName) && panelDictionary[panelName].gameObject.activeSelf;
    }

    // ===== CONVENIENCE METHODS =====

    public void TogglePause()
    {
        if (!HasPanel(pausePanelName)) return;

        if (isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (!HasPanel(pausePanelName)) return;

        OpenPanel(pausePanelName);
    }

    public void Resume()
    {
        if (!HasPanel(pausePanelName)) return;

        ClosePanel(pausePanelName);
    }

    public void OpenSettings()
    {
        if (!HasPanel(settingsPanelName)) return;

        // Close pause menu if open
        if (HasPanel(pausePanelName))
            ClosePanel(pausePanelName);

        OpenPanel(settingsPanelName);
    }

    public void CloseSettings()
    {
        if (!HasPanel(settingsPanelName)) return;

        ClosePanel(settingsPanelName);

        // Reopen pause menu
        if (HasPanel(pausePanelName))
            OpenPanel(pausePanelName);
    }
}
