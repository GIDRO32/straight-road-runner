using UnityEngine;
using System.Collections.Generic;

public class MenuBackgroundScroller : MonoBehaviour
{
    [System.Serializable]
    public class BackgroundData
    {
        public string stageName;
        public GameObject backgroundPrefab;
        public float scrollSpeed = 1f;
        public float backgroundWidth = 20f;
    }

    [Header("Selected Stage")]
    [Tooltip("Use the selected stage's Menu Background (StageDefinition > Title Screen) and switch when another stage is picked")]
    public bool followSelectedStage = true;

    [Header("Background Presets")]
    [Tooltip("First background in array is the default (used when the stage has no Menu Background)")]
    public BackgroundData[] backgrounds;

    [Header("Spawn Settings")]
    public Transform spawnParent;           // Parent for spawned backgrounds
    public float spawnX = -15f;             // Where backgrounds spawn (left)
    public float destroyX = 15f;            // Where backgrounds destroy (right)

    [Header("Current Background")]
    [Tooltip("Leave empty to use default (first in array)")]
    public string selectedStageName = "";

    // Runtime variables
    private BackgroundData currentBackground;
    private List<GameObject> activeBackgrounds = new List<GameObject>();
    private float nextSpawnX;

    void OnEnable()
    {
        GameSession.SelectedStageChanged += OnSelectedStageChanged;
    }

    void OnDisable()
    {
        GameSession.SelectedStageChanged -= OnSelectedStageChanged;
    }

    void Start()
    {
        if (followSelectedStage)
        {
            currentBackground = BackgroundForStage(GameSession.SelectedStage);
        }
        else if (string.IsNullOrEmpty(selectedStageName))
        {
            // Use default (first background)
            currentBackground = DefaultBackground();
        }
        else
        {
            // Find selected background
            currentBackground = FindBackground(selectedStageName);

            if (currentBackground == null)
            {
                Debug.LogWarning($"Background '{selectedStageName}' not found! Using default.");
                currentBackground = DefaultBackground();
            }
        }

        if (currentBackground == null || currentBackground.backgroundPrefab == null)
        {
            Debug.LogError("No valid background found!");
            return;
        }

        // Initialize spawn position
        nextSpawnX = spawnX;

        // Spawn initial backgrounds to fill screen
        SpawnInitialBackgrounds();
    }

    private BackgroundData DefaultBackground()
    {
        return backgrounds != null && backgrounds.Length > 0 ? backgrounds[0] : null;
    }

    /// <summary>The stage's own Menu Background, or the default one.</summary>
    private BackgroundData BackgroundForStage(StageDefinition stage)
    {
        if (stage == null || stage.menuBackgroundPrefab == null)
            return DefaultBackground();

        return new BackgroundData
        {
            stageName = stage.DisplayName,
            backgroundPrefab = stage.menuBackgroundPrefab,
            scrollSpeed = stage.menuBackgroundScrollSpeed,
            backgroundWidth = stage.menuBackgroundWidth
        };
    }

    private void OnSelectedStageChanged(StageDefinition stage)
    {
        if (followSelectedStage)
            SwitchTo(BackgroundForStage(stage));
    }

    void Update()
    {
        if (currentBackground == null) return;

        // Check if we need to spawn new background
        if (activeBackgrounds.Count > 0)
        {
            // Find leftmost background
            float leftmostX = float.MaxValue;

            foreach (GameObject bg in activeBackgrounds)
            {
                if (bg != null && bg.transform.position.x < leftmostX)
                    leftmostX = bg.transform.position.x;
            }

            // Spawn new background when gap appears
            float spawnThreshold = spawnX + currentBackground.backgroundWidth * 0.5f;

            if (leftmostX > spawnThreshold)
            {
                nextSpawnX = spawnX;
                SpawnBackground();
            }
        }

        // Cleanup destroyed backgrounds
        activeBackgrounds.RemoveAll(bg => bg == null);
    }

    /// <summary>
    /// Spawn initial backgrounds to fill the screen
    /// </summary>
    private void SpawnInitialBackgrounds()
    {
        // Calculate visible screen width
        Camera mainCam = Camera.main;
        float cameraHeight = mainCam.orthographicSize * 2f;
        float cameraWidth = cameraHeight * mainCam.aspect;

        // Calculate how many backgrounds needed to cover screen from left to right
        float totalWidth = destroyX - spawnX + cameraWidth;
        float initialCount = 0;

        // Start spawning from far left
        nextSpawnX = 0f;
        SpawnBackground();
        Debug.Log($"Spawned {initialCount} initial backgrounds (screen width: {cameraWidth}, total coverage: {totalWidth})");
    }

    /// <summary>
    /// Spawn a single background instance
    /// </summary>
    private void SpawnBackground()
    {
        if (currentBackground.backgroundPrefab == null) return;

        Vector3 spawnPos = new Vector3(nextSpawnX, 0f, 10f);
        GameObject bg = Instantiate(currentBackground.backgroundPrefab, spawnPos, Quaternion.identity, spawnParent);

        // Setup physics
        Rigidbody2D rb = bg.GetComponent<Rigidbody2D>();
        if (rb == null) rb = bg.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Setup scrolling
        ScrollingObject scrollScript = bg.GetComponent<ScrollingObject>();
        if (scrollScript == null) scrollScript = bg.AddComponent<ScrollingObject>();
        scrollScript.Init(currentBackground.scrollSpeed, destroyX);

        activeBackgrounds.Add(bg);

        // Update next spawn position
        nextSpawnX += currentBackground.backgroundWidth;
    }

    /// <summary>
    /// Find background data by stage name
    /// </summary>
    private BackgroundData FindBackground(string stageName)
    {
        foreach (BackgroundData bg in backgrounds)
        {
            if (bg.stageName.Equals(stageName, System.StringComparison.OrdinalIgnoreCase))
                return bg;
        }
        return null;
    }

    /// <summary>
    /// Change background at runtime (for future stage selection)
    /// </summary>
    public void SetBackground(string stageName)
    {
        BackgroundData newBackground = FindBackground(stageName);

        if (newBackground == null)
        {
            Debug.LogWarning($"Background '{stageName}' not found!");
            return;
        }

        SwitchTo(newBackground);
    }

    /// <summary>Replaces the scrolling background (no-op if it's already showing).</summary>
    private void SwitchTo(BackgroundData newBackground)
    {
        if (newBackground == null || newBackground.backgroundPrefab == null) return;

        if (currentBackground != null && currentBackground.backgroundPrefab == newBackground.backgroundPrefab
            && Mathf.Approximately(currentBackground.scrollSpeed, newBackground.scrollSpeed))
            return;

        string stageName = newBackground.stageName;

        // Clear current backgrounds
        foreach (GameObject bg in activeBackgrounds)
        {
            if (bg != null) Destroy(bg);
        }
        activeBackgrounds.Clear();

        // Set new background
        currentBackground = newBackground;
        selectedStageName = stageName;
        nextSpawnX = spawnX;

        // Spawn new backgrounds
        SpawnInitialBackgrounds();

        Debug.Log($"Changed background to: {stageName}");
    }

    /// <summary>
    /// Get list of available stage names
    /// </summary>
    public string[] GetAvailableStages()
    {
        string[] stageNames = new string[backgrounds.Length];

        for (int i = 0; i < backgrounds.Length; i++)
        {
            stageNames[i] = backgrounds[i].stageName;
        }

        return stageNames;
    }

    /// <summary>
    /// Get current background stage name
    /// </summary>
    public string GetCurrentStageName()
    {
        return currentBackground != null ? currentBackground.stageName : "None";
    }

    void OnDestroy()
    {
        // Cleanup all backgrounds
        foreach (GameObject bg in activeBackgrounds)
        {
            if (bg != null) Destroy(bg);
        }
        activeBackgrounds.Clear();
    }
}