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
        [Tooltip("Only used if the background has no sprites: the width is measured from its sprites")]
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

    // While the leftmost tile's left edge is right of the screen's left edge (or the spawn line), add a tile
    // directly to its left. Tile widths are measured from their sprites, so the Width setting can't cause gaps.
    // Several per frame if needed (first frames after launch can be long).
    private void FillLeft()
    {
        if (currentBackground == null || currentBackground.backgroundPrefab == null) return;
        float coverTo = Mathf.Min(spawnX, ScreenLeftEdge() - 1f);

        for (int guard = 0; guard < 50; guard++)
        {
            GameObject leftmost = null;
            float leftEdge = float.MaxValue;
            foreach (GameObject bg in activeBackgrounds)
            {
                if (bg == null) continue;
                float edge = TileBounds(bg).min.x;
                if (edge < leftEdge) { leftEdge = edge; leftmost = bg; }
            }

            if (leftmost == null)
                SpawnBackground(rightEdge: Mathf.Max(destroyX, ScreenRightEdge() + 1f));
            else if (leftEdge > coverTo)
                SpawnBackground(rightEdge: leftEdge + tileOverlap);
            else
                return; // Covered
        }
    }

    /// <summary>
    /// Spawn a single background tile whose right edge is at the given x
    /// </summary>
    private void SpawnBackground(float rightEdge)
    {
        if (currentBackground.backgroundPrefab == null) return;

        GameObject bg = Instantiate(currentBackground.backgroundPrefab, new Vector3(0f, 0f, 10f), Quaternion.identity, spawnParent);
        Vector3 pos = bg.transform.position;
        bg.transform.position = new Vector3(0f, pos.y, pos.z);
        Bounds b = TileBounds(bg);
        bg.transform.position += new Vector3(rightEdge - b.max.x, 0f, 0f);

        // Setup physics
        Rigidbody2D rb = bg.GetComponent<Rigidbody2D>();
        if (rb == null) rb = bg.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Setup scrolling; only destroy it once its left edge is past the right edge of the screen
        ScrollingObject scrollScript = bg.GetComponent<ScrollingObject>();
        if (scrollScript == null) scrollScript = bg.AddComponent<ScrollingObject>();
        b = TileBounds(bg);
        float leftEdgeOffset = b.min.x - bg.transform.position.x;
        float destroyAt = Mathf.Max(destroyX, ScreenRightEdge() + 1f - leftEdgeOffset);
        scrollScript.Init(currentBackground.scrollSpeed, destroyAt);

        activeBackgrounds.Add(bg);
    }

    // World bounds of all sprites in a tile; falls back to the Width setting if it has none
    private Bounds TileBounds(GameObject tile)
    {
        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            float w = Mathf.Abs(currentBackground.backgroundWidth);
            return new Bounds(tile.transform.position, new Vector3(w, 1f, 1f));
        }
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static float ScreenRightEdge()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position.x + cam.orthographicSize * cam.aspect : 0f;
    }

    private static float ScreenLeftEdge()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position.x - cam.orthographicSize * cam.aspect : 0f;
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