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
        activeBackgrounds.RemoveAll(bg => bg == null);
        FillLeft();
    }

    [Header("Tiling")]
    [Tooltip("Neighbouring tiles overlap by this much (world units), so no seam shows between them")]
    public float tileOverlap = 0.05f;

    /// <summary>
    /// Fills the screen right away: the first tile just inside the destroy line, then tiles leftwards.
    /// </summary>
    private void SpawnInitialBackgrounds()
    {
        FillLeft();
    }

    // While the leftmost tile's left edge is right of the spawn line, add a tile directly to its left.
    // Several per frame if needed (first frames after launch can be long), so there are never gaps.
    private void FillLeft()
    {
        float width = Mathf.Abs(currentBackground.backgroundWidth);
        if (width <= 0.01f || currentBackground.backgroundPrefab == null) return;

        for (int guard = 0; guard < 50; guard++)
        {
            float leftmost = float.MaxValue;
            foreach (GameObject bg in activeBackgrounds)
                if (bg != null && bg.transform.position.x < leftmost) leftmost = bg.transform.position.x;

            if (leftmost == float.MaxValue)
                SpawnBackground(destroyX - width * 0.5f);
            else if (leftmost - width * 0.5f > spawnX)
                SpawnBackground(leftmost - width + tileOverlap);
            else
                return; // Covered from the spawn line to the right
        }
    }

    /// <summary>
    /// Spawn a single background instance
    /// </summary>
    private void SpawnBackground(float x)
    {
        if (currentBackground.backgroundPrefab == null) return;

        Vector3 spawnPos = new Vector3(x, 0f, 10f);
        GameObject bg = Instantiate(currentBackground.backgroundPrefab, spawnPos, Quaternion.identity, spawnParent);

        // Setup physics
        Rigidbody2D rb = bg.GetComponent<Rigidbody2D>();
        if (rb == null) rb = bg.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Setup scrolling; only destroy it once it's fully past the right edge of the screen
        ScrollingObject scrollScript = bg.GetComponent<ScrollingObject>();
        if (scrollScript == null) scrollScript = bg.AddComponent<ScrollingObject>();
        float width = Mathf.Abs(currentBackground.backgroundWidth);
        scrollScript.Init(currentBackground.scrollSpeed, Mathf.Max(destroyX, ScreenRightEdge() + width * 0.5f + 1f));

        activeBackgrounds.Add(bg);
    }

    private static float ScreenRightEdge()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position.x + cam.orthographicSize * cam.aspect : 0f;
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