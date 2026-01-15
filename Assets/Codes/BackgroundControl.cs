using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BackgroundControl : MonoBehaviour
{
    #region Background Layer Settings
    [Header("Background Prefabs")]
    public GameObject bottomLayerPrefab;
    public GameObject middleLayerPrefab;
    public GameObject topLayerPrefab;

    [Header("Background Sprite Variants")]
    public Sprite[] bottomVariants;
    public Sprite[] middleVariants;
    public Sprite[] topVariants;

    [Header("Background Positions")]
    public float bottomLayerY = -2f;
    public float middleLayerY = 0f;
    public float topLayerY = 2f;

    [Header("Background Scrolling")]
    public float backgroundScrollSpeed = 2f;
    public float backgroundSpawnX = -20f;     // Spawn from left
    public float backgroundDestroyX = 30f;    // Destroy on right
    public float backgroundWidth = 40f;       // Width of each background piece

    private float nextBackgroundSpawnX;
    private List<GameObject> activeBackgrounds = new List<GameObject>();
    #endregion

    #region Decoration Settings
    [System.Serializable]
    public class DecorationData
    {
        [Header("Decoration Properties")]
        public Sprite sprite;
        public DecorLayer layer = DecorLayer.Back;
        public MovementDirection direction = MovementDirection.LeftToRight;
        public float movementSpeed = 3f;
        public bool flipSprite = false;

        [Header("Spawn Settings")]
        [Range(0f, 1f)]
        public float spawnChance = 0.3f;      // 30% chance to spawn
        public Vector2 yPositionRange = new Vector2(-3f, 5f);
        public Vector2 scaleRange = new Vector2(0.5f, 1.5f);
    }

    public enum DecorLayer
    {
        Back,   // Behind everything (sorting order -1)
        Front   // In front of platforms (sorting order 4)
    }

    public enum MovementDirection
    {
        LeftToRight,
        RightToLeft
    }

    [Header("Decoration List")]
    public DecorationData[] decorations;

    [Header("Decoration Spawn Settings")]
    public float decorationSpawnInterval = 5f;  // Check every 5 seconds
    public int maxActiveDecorations = 10;

    private float decorationSpawnTimer = 0f;
    private List<GameObject> activeDecorations = new List<GameObject>();
    #endregion

    void Start()
    {
        // Start spawning from the left
        nextBackgroundSpawnX = backgroundSpawnX;
        SpawnInitialBackgrounds();
    }

    void Update()
    {
        HandleBackgroundSpawning();
        HandleDecorationSpawning();
        CleanupDestroyedObjects();
    }

    #region Background Management
    private void SpawnInitialBackgrounds()
    {
        // Spawn enough background sets to cover from spawn point to beyond screen
        // This ensures seamless scrolling from the start
        int initialSets = Mathf.CeilToInt((backgroundDestroyX - backgroundSpawnX) / backgroundWidth) + 1;
        
        for (int i = 0; i < initialSets; i++)
        {
            SpawnBackgroundSet();
        }
        
        Debug.Log($"Spawned {initialSets} initial background sets");
    }

    private void HandleBackgroundSpawning()
    {
        // Clean up null references first
        activeBackgrounds.RemoveAll(bg => bg == null);

        // Check if any backgrounds exist
        if (activeBackgrounds.Count == 0)
        {
            Debug.LogWarning("All backgrounds destroyed! Respawning...");
            nextBackgroundSpawnX = backgroundSpawnX;
            SpawnInitialBackgrounds();
            return;
        }

        // Find the leftmost background (the one that spawned most recently)
        float leftmostBgX = float.MaxValue;
        
        foreach (GameObject bg in activeBackgrounds)
        {
            if (bg != null)
            {
                float bgX = bg.transform.position.x;
                if (bgX < leftmostBgX)
                    leftmostBgX = bgX;
            }
        }

        // When the leftmost background has scrolled far enough right, spawn a new one behind it
        // We want to spawn when there's a gap appearing on the left side
        float spawnThreshold = backgroundSpawnX + backgroundWidth * 0.5f;
        
        if (leftmostBgX > spawnThreshold)
        {
            // Reset spawn position to the left and spawn new set
            nextBackgroundSpawnX = backgroundSpawnX;
            SpawnBackgroundSet();
            Debug.Log($"Spawned new background. Leftmost was at: {leftmostBgX}, Threshold: {spawnThreshold}");
        }
    }

    private void SpawnBackgroundSet()
    {
        // Always spawn at the designated spawn position (left side)
        float spawnX = nextBackgroundSpawnX;

        // Bottom layer
        if (bottomLayerPrefab != null)
        {
            GameObject bottom = SpawnBackgroundLayer(
                bottomLayerPrefab, 
                spawnX, 
                bottomLayerY, 
                -1,  // Sorting order
                bottomVariants
            );
            activeBackgrounds.Add(bottom);
        }

        // Middle layer
        if (middleLayerPrefab != null)
        {
            GameObject middle = SpawnBackgroundLayer(
                middleLayerPrefab, 
                spawnX, 
                middleLayerY, 
                0,  // Sorting order
                middleVariants
            );
            activeBackgrounds.Add(middle);
        }

        // Top layer
        if (topLayerPrefab != null)
        {
            GameObject top = SpawnBackgroundLayer(
                topLayerPrefab, 
                spawnX, 
                topLayerY, 
                1,  // Sorting order
                topVariants
            );
            activeBackgrounds.Add(top);
        }

        // Move spawn position for NEXT spawn (but we'll reset it when actually spawning)
        // This is used during initial spawning only
        nextBackgroundSpawnX += backgroundWidth;
    }

    private GameObject SpawnBackgroundLayer(GameObject prefab, float x, float y, int sortingOrder, Sprite[] variants)
    {
        Vector3 spawnPos = new Vector3(x, y, 10f);
        GameObject layer = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

        // Setup physics
        Rigidbody2D rb = layer.GetComponent<Rigidbody2D>();
        if (rb == null) rb = layer.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Setup scrolling
        ScrollingObject scrollScript = layer.GetComponent<ScrollingObject>();
        if (scrollScript == null) scrollScript = layer.AddComponent<ScrollingObject>();
        scrollScript.Init(backgroundScrollSpeed, backgroundDestroyX);

        // Set random variant sprite
        SpriteRenderer sr = layer.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = sortingOrder;
            if (variants != null && variants.Length > 0)
            {
                sr.sprite = variants[Random.Range(0, variants.Length)];
            }
        }

        return layer;
    }
    #endregion

    #region Decoration Management
    private void HandleDecorationSpawning()
    {
        decorationSpawnTimer += Time.deltaTime;

        if (decorationSpawnTimer >= decorationSpawnInterval)
        {
            decorationSpawnTimer = 0f;
            TrySpawnDecorations();
        }
    }

    private void TrySpawnDecorations()
    {
        if (activeDecorations.Count >= maxActiveDecorations)
            return;

        foreach (DecorationData decor in decorations)
        {
            if (activeDecorations.Count >= maxActiveDecorations)
                break;

            // Random chance to spawn this decoration
            if (Random.value <= decor.spawnChance)
            {
                SpawnDecoration(decor);
            }
        }
    }

    private void SpawnDecoration(DecorationData data)
    {
        // Create decoration object
        GameObject decoration = new GameObject("Decoration");
        decoration.transform.SetParent(transform);

        // Determine spawn position based on direction
        float spawnX = data.direction == MovementDirection.LeftToRight 
            ? backgroundSpawnX - 5f      // Spawn off-screen left
            : backgroundDestroyX + 5f;   // Spawn off-screen right

        float spawnY = Random.Range(data.yPositionRange.x, data.yPositionRange.y);
        decoration.transform.position = new Vector3(spawnX, spawnY, 5f);

        // Random scale
        float scale = Random.Range(data.scaleRange.x, data.scaleRange.y);
        decoration.transform.localScale = Vector3.one * scale;

        // Add sprite renderer
        SpriteRenderer sr = decoration.AddComponent<SpriteRenderer>();
        sr.sprite = data.sprite;
        
        // Set sorting order based on layer
        sr.sortingOrder = data.layer == DecorLayer.Back ? -1 : 4;

        // Flip sprite if needed
        if (data.flipSprite)
        {
            Vector3 localScale = decoration.transform.localScale;
            localScale.x *= -1;
            decoration.transform.localScale = localScale;
        }

        // Add physics
        Rigidbody2D rb = decoration.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Add scrolling behavior
        ScrollingObject scrollScript = decoration.AddComponent<ScrollingObject>();
        
        // Set scroll speed and destroy point based on direction
        float scrollSpeed = data.direction == MovementDirection.LeftToRight 
            ? data.movementSpeed 
            : -data.movementSpeed;
        
        float destroyX = data.direction == MovementDirection.LeftToRight 
            ? backgroundDestroyX + 5f 
            : backgroundSpawnX - 5f;
        
        scrollScript.Init(scrollSpeed, destroyX);

        activeDecorations.Add(decoration);
    }
    #endregion

    #region Cleanup
    private void CleanupDestroyedObjects()
    {
        // Remove null references (destroyed objects)
        activeBackgrounds.RemoveAll(bg => bg == null);
        activeDecorations.RemoveAll(decor => decor == null);
    }

    void OnDestroy()
    {
        // Clean up all spawned objects
        foreach (GameObject bg in activeBackgrounds)
        {
            if (bg != null) Destroy(bg);
        }
        
        foreach (GameObject decor in activeDecorations)
        {
            if (decor != null) Destroy(decor);
        }
        
        activeBackgrounds.Clear();
        activeDecorations.Clear();
    }
    #endregion

    #region Debug Helpers
    void OnDrawGizmosSelected()
    {
        // Draw spawn line (left side)
        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            new Vector3(backgroundSpawnX, -10f, 0f),
            new Vector3(backgroundSpawnX, 10f, 0f)
        );
        
        #if UNITY_EDITOR
        Handles.Label(
            new Vector3(backgroundSpawnX, 10f, 0f), 
            "SPAWN"
        );
        #endif

        // Draw destroy line (right side)
        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            new Vector3(backgroundDestroyX, -10f, 0f),
            new Vector3(backgroundDestroyX, 10f, 0f)
        );
        
        #if UNITY_EDITOR
        Handles.Label(
            new Vector3(backgroundDestroyX, 10f, 0f), 
            "DESTROY"
        );
        #endif

        // Draw background width indicator
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            new Vector3(backgroundSpawnX + backgroundWidth / 2f, 0f, 10f),
            new Vector3(backgroundWidth, 15f, 0f)
        );

        // Draw background layers
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(new Vector3(backgroundSpawnX, bottomLayerY, 0f), 0.5f);
        Gizmos.DrawWireSphere(new Vector3(backgroundSpawnX, middleLayerY, 0f), 0.5f);
        Gizmos.DrawWireSphere(new Vector3(backgroundSpawnX, topLayerY, 0f), 0.5f);

        // Draw scroll direction arrow
        Gizmos.color = Color.yellow;
        Vector3 arrowStart = new Vector3(backgroundSpawnX, -8f, 0f);
        Vector3 arrowEnd = new Vector3(backgroundDestroyX, -8f, 0f);
        Gizmos.DrawLine(arrowStart, arrowEnd);
        // Arrow head
        Gizmos.DrawLine(arrowEnd, arrowEnd + new Vector3(-1f, 0.5f, 0f));
        Gizmos.DrawLine(arrowEnd, arrowEnd + new Vector3(-1f, -0.5f, 0f));
        
        #if UNITY_EDITOR
        Handles.Label(
            new Vector3((backgroundSpawnX + backgroundDestroyX) / 2f, -8.5f, 0f),
            $"Scroll Direction (Speed: {backgroundScrollSpeed})"
        );
        #endif
    }
    #endregion
}