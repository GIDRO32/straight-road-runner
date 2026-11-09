// Platform.cs - attach to the base platform prefab

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A platform instance. StageControl calls Setup() right after spawning it with the platform type
/// (look + mechanic, from PlatformDefinition) and the stage's obstacle chances.
/// </summary>
public class Platform : MonoBehaviour
{
    [Header("Particle Effects")]
    public ParticleSystem normalLandingParticles;
    public ParticleSystem fragileLandingParticles;
    [Tooltip("Played when a fragile platform breaks")]
    public ParticleSystem fragileBreak;

    public PlatformDefinition Definition { get; private set; }
    public PlatformMechanic Mechanic { get; private set; }
    public SpriteFrameAnimator FrameAnimator { get; private set; }
    public bool IsBroken { get; private set; }

    private SpriteRenderer spriteRenderer;
    private readonly List<GameObject> obstacleInstances = new List<GameObject>();

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>Called once by StageControl right after Instantiate.</summary>
    public void Setup(PlatformDefinition definition, IReadOnlyList<ObstacleSpawnState> obstacles)
    {
        Definition = definition;

        if (definition != null)
        {
            ApplyLook(definition);
            AddMechanic(definition);
        }

        if (definition == null || definition.allowObstacles)
            SpawnObstacles(obstacles);
    }

    private void ApplyLook(PlatformDefinition definition)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (definition.idleFrames != null && definition.idleFrames.Length > 0)
        {
            FrameAnimator = gameObject.AddComponent<SpriteFrameAnimator>();
            FrameAnimator.PlayLoop(definition.idleFrames, definition.idleFramesPerSecond);
        }
        else if (definition.sprite != null)
        {
            spriteRenderer.sprite = definition.sprite;
        }

        // Lava etc. may need the animator for one-shot animations even without an idle loop
        if (FrameAnimator == null && definition.mechanic == PlatformMechanicType.Lava)
            FrameAnimator = gameObject.AddComponent<SpriteFrameAnimator>();
    }

    private void AddMechanic(PlatformDefinition definition)
    {
        switch (definition.mechanic)
        {
            case PlatformMechanicType.Fragile: Mechanic = gameObject.AddComponent<FragileMechanic>(); break;
            case PlatformMechanicType.Lava: Mechanic = gameObject.AddComponent<LavaMechanic>(); break;
            default: Mechanic = null; break;
        }

        if (Mechanic != null) Mechanic.Init(this, definition);
    }

    // Each obstacle rolls its chance; only one obstacle per slot
    private void SpawnObstacles(IReadOnlyList<ObstacleSpawnState> obstacles)
    {
        if (obstacles == null) return;

        var usedSlots = new HashSet<ObstacleSlot>();
        foreach (var spawn in obstacles)
        {
            ObstacleDefinition obstacle = spawn.definition;
            if (obstacle == null || obstacle.prefab == null) continue;
            if (usedSlots.Contains(obstacle.slot)) continue;
            if (Random.value >= spawn.chance) continue;

            usedSlots.Add(obstacle.slot);

            // Child of the platform so it scrolls with it; keeps the prefab's world scale
            GameObject instance = Instantiate(obstacle.prefab, transform, true);
            instance.transform.localPosition = new Vector3(obstacle.offset.x, obstacle.offset.y, 0f);
            obstacleInstances.Add(instance);

            WallShatter wall = instance.GetComponentInChildren<WallShatter>(true);
            if (wall != null && obstacle.hitsToBreak.x > 0)
                wall.SetHitsToBreak(obstacle.hitsToBreak.x, Mathf.Max(obstacle.hitsToBreak.x, obstacle.hitsToBreak.y));
        }
    }

    // Called by Movement when the player lands from the air
    public void OnPlayerLanded(Vector3 landPosition)
    {
        ParticleSystem particlesToPlay = Mechanic != null ? Mechanic.LandingParticles : normalLandingParticles;

        if (particlesToPlay != null)
        {
            // Position particles at landing point
            particlesToPlay.transform.position = new Vector3(
                landPosition.x,
                transform.position.y + GetComponent<Collider2D>().bounds.extents.y,
                landPosition.z
            );
            particlesToPlay.Play();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (Mechanic != null && collision.gameObject.CompareTag("Player"))
            Mechanic.OnPlayerEnter(collision.gameObject);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (Mechanic != null && collision.gameObject.CompareTag("Player"))
            Mechanic.OnPlayerExit(collision.gameObject);
    }

    /// <summary>Removes the platform (fragile platforms call this when they give way).</summary>
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;

        // Disable collider so player falls through
        GetComponent<Collider2D>().enabled = false;

        // Hide the platform sprite and whatever stood on it
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        foreach (var obstacle in obstacleInstances)
            if (obstacle != null) obstacle.SetActive(false);

        // Play particles and destroy after they finish
        if (fragileBreak != null)
        {
            fragileBreak.Play();
            Destroy(gameObject, fragileBreak.main.duration + fragileBreak.main.startLifetime.constantMax);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
