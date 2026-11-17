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
    public GameObject floorLayerPrefab;  // NEW

    [Header("Background Sprite Variants")]
    public Sprite[] bottomVariants;
    public Sprite[] middleVariants;
    public Sprite[] topVariants;
    public Sprite[] floorVariants;  // NEW

    [Header("Background Positions")]
    public float bottomLayerY = -2f;
    public float middleLayerY = 0f;
    public float topLayerY = 2f;
    public float floorLayerY = -2f;  // NEW

    [Header("Background Scrolling")]
    public float backgroundScrollSpeed = 2f;
    public float floorScrollSpeed = 3f;
    public float backgroundSpawnX = -20f;     // Spawn from left
    public float backgroundDestroyX = 30f;    // Destroy on right
    public float backgroundWidth = 40f;       // Width of each background piece
    public float floorWidth = 16f;

    private List<GameObject> activeBackgrounds = new List<GameObject>();
    #endregion

    #region Decoration Settings
    [System.Serializable]
    public class DecorationData
    {
        [Header("Decoration Properties")]
        public GameObject decorationPrefab;
        [Tooltip("Sorting order for the decoration (e.g., -1 for back, 20 for front)")]
        public int sortingOrder = 1;  // CHANGED: Custom sorting order
        public MovementDirection direction = MovementDirection.LeftToRight;
        public float movementSpeed = 3f;
        public bool flipObject = false;

        [Header("Spawn Settings")]
        [Range(0f, 1f)]
        public float spawnChance = 0.3f;
        public Vector2 yPositionRange = new Vector2(-3f, 5f);
        public Vector2 scaleRange = new Vector2(0.5f, 1.5f);
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

    [Header("Tiling")]
    [Tooltip("Neighbouring tiles overlap by this much (world units), so no seam shows between them")]
    public float tileOverlap = 0.05f;

    // Tiles of each strip, so every new tile is placed right against the previous one
    private readonly List<GameObject> backgroundSets = new List<GameObject>(); // One layer per set marks its position
    private readonly List<GameObject> floorTiles = new List<GameObject>();

    void Start()
    {
        SpawnInitialBackgrounds();
    }

    void Update()
    {
        HandleBackgroundSpawning();
        HandleDecorationSpawning();
        CleanupDestroyedObjects();
    }

    #region Background Management
    // Covers everything from the destroy line back to the spawn line right away
    private void SpawnInitialBackgrounds()
    {
        backgroundSets.Clear();
        floorTiles.Clear();
        HandleBackgroundSpawning(); // Empty strips start at the destroy line and fill leftwards
    }

    // Keeps both strips continuous: while the leftmost tile's left edge is right of the spawn line, add a tile
    // directly to its left. Several per frame if needed (fast-forwarded intro, frame hitches), so no gaps.
    private void HandleBackgroundSpawning()
    {
        FillStrip(backgroundSets, Mathf.Abs(backgroundWidth), x => SpawnBackgroundSet(x));
        FillStrip(floorTiles, Mathf.Abs(floorWidth), x => SpawnFloorLayer(x));
    }

    private void FillStrip(List<GameObject> strip, float width, System.Func<float, GameObject> spawn)
    {
        if (width <= 0.01f) return;
        strip.RemoveAll(t => t == null);

        for (int guard = 0; guard < 50; guard++)
        {
            float leftmost = float.MaxValue;
            foreach (GameObject tile in strip)
                if (tile.transform.position.x < leftmost) leftmost = tile.transform.position.x;

            float x = leftmost == float.MaxValue
                ? backgroundDestroyX - width * 0.5f      // Strip empty: first tile just inside the destroy line
                : leftmost - width + tileOverlap;        // Right against the leftmost tile
            if (leftmost != float.MaxValue && leftmost - width * 0.5f <= backgroundSpawnX) return; // Covered

            GameObject spawned = spawn(x);
            if (spawned == null) return; // No prefab
        }
    }

    private GameObject SpawnBackgroundSet(float spawnX)
    {
        GameObject marker = null;

        // Bottom layer
        if (bottomLayerPrefab != null)
        {
            GameObject bottom = SpawnBackgroundLayer(bottomLayerPrefab, spawnX, bottomLayerY, -1, bottomVariants, -1f, backgroundWidth);
            activeBackgrounds.Add(bottom);
            marker = marker != null ? marker : bottom;
        }

        // Middle layer
        if (middleLayerPrefab != null)
        {
            GameObject middle = SpawnBackgroundLayer(middleLayerPrefab, spawnX, middleLayerY, 0, middleVariants, -1f, backgroundWidth);
            activeBackgrounds.Add(middle);
            marker = marker != null ? marker : middle;
        }

        // Top layer
        if (topLayerPrefab != null)
        {
            GameObject top = SpawnBackgroundLayer(topLayerPrefab, spawnX, topLayerY, 1, topVariants, -1f, backgroundWidth);
            activeBackgrounds.Add(top);
            marker = marker != null ? marker : top;
        }

        if (marker != null) backgroundSets.Add(marker);
        return marker;
    }

    private GameObject SpawnFloorLayer(float spawnX)
    {
        if (floorLayerPrefab == null) return null;

        GameObject floor = SpawnBackgroundLayer(
            floorLayerPrefab,
            spawnX,
            floorLayerY,
            6,
            floorVariants,
            floorScrollSpeed > 0f ? floorScrollSpeed : 8f,
            floorWidth
        );

        floor.name = "Floor";
        activeBackgrounds.Add(floor);
        floorTiles.Add(floor);
        return floor;
    }

    private GameObject SpawnBackgroundLayer(GameObject prefab, float x, float y, int sortingOrder, Sprite[] variants, float customScrollSpeed, float width)
    {
        Vector3 spawnPos = new Vector3(x, y, 10f);
        GameObject layer = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

        // Setup physics
        Rigidbody2D rb = layer.GetComponent<Rigidbody2D>();
        if (rb == null) rb = layer.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Setup scrolling - use custom speed if provided, otherwise use default
        ScrollingObject scrollScript = layer.GetComponent<ScrollingObject>();
        if (scrollScript == null) scrollScript = layer.AddComponent<ScrollingObject>();
        float scrollSpeed = customScrollSpeed > 0 ? customScrollSpeed : backgroundScrollSpeed;
        // Only destroy it once it's fully past the right edge of the screen
        scrollScript.Init(scrollSpeed, Mathf.Max(backgroundDestroyX, ScreenRightEdge() + Mathf.Abs(width) * 0.5f + 1f));

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

    // Widest the view gets (CameraFollow zooms out up to maxYSize)
    private static float ScreenRightEdge()
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;
        float size = cam.orthographicSize;
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null) size = Mathf.Max(size, follow.maxYSize);
        return cam.transform.position.x + size * cam.aspect;
    }

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
        if (data.decorationPrefab == null)
        {
            Debug.LogWarning("Decoration prefab is null!");
            return;
        }

        // Determine spawn position based on direction
        float spawnX = data.direction == MovementDirection.LeftToRight
            ? backgroundSpawnX - 5f      // Spawn off-screen left
            : backgroundDestroyX + 5f;   // Spawn off-screen right

        float spawnY = Random.Range(data.yPositionRange.x, data.yPositionRange.y);
        Vector3 spawnPos = new Vector3(spawnX, spawnY, 5f);

        // Instantiate decoration prefab
        GameObject decoration = Instantiate(data.decorationPrefab, spawnPos, Quaternion.identity, transform);

        // Random scale
        float scale = Random.Range(data.scaleRange.x, data.scaleRange.y);
        decoration.transform.localScale = Vector3.one * scale;

        // Set sorting order (if decoration has SpriteRenderer)
        SpriteRenderer sr = decoration.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = data.sortingOrder;
        }
        else
        {
            // If it has multiple sprite renderers (for animated decorations)
            SpriteRenderer[] renderers = decoration.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer renderer in renderers)
            {
                renderer.sortingOrder = data.sortingOrder;
            }
        }

        // Flip object if needed
        if (data.flipObject)
        {
            Vector3 localScale = decoration.transform.localScale;
            localScale.x *= -1;
            decoration.transform.localScale = localScale;
        }

        // Add physics if not already present
        Rigidbody2D rb = decoration.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = decoration.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Add scrolling behavior if not already present
        ScrollingObject scrollScript = decoration.GetComponent<ScrollingObject>();
        if (scrollScript == null)
        {
            scrollScript = decoration.AddComponent<ScrollingObject>();
        }

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
        Gizmos.DrawWireSphere(new Vector3(backgroundSpawnX, floorLayerY, 0f), 0.5f);  // NEW

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