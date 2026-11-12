using UnityEngine;

/// <summary>
/// After the player has stood on it for LavaSettings.heatUpTime, hits them like an enemy
/// (stamina loss + stun), then again every hitInterval while they stay. Plays the hit animation and
/// lava droplets (LavaDroplets, or LavaSettings.hitParticlesPrefab) each time.
/// </summary>
public class LavaMechanic : PlatformMechanic
{
    private Movement player;
    private float timer = 0f;

    public override void OnPlayerEnter(GameObject playerObject)
    {
        player = playerObject.GetComponentInParent<Movement>();
        timer = 0f;
    }

    public override void OnPlayerExit(GameObject playerObject)
    {
        player = null;
        timer = 0f;
    }

    void Update()
    {
        if (player == null || platform.IsBroken) return;

        LavaSettings lava = definition.lava;
        timer += Time.deltaTime;
        if (timer < lava.heatUpTime) return;

        if (player.TakeHit(lava.staminaDamage, lava.stunDuration))
        {
            if (platform.FrameAnimator != null)
                platform.FrameAnimator.PlayOnce(lava.hitFrames, lava.hitFramesPerSecond);
            if (lava.hitParticles) SpawnDroplets(lava);
        }

        // Next hit after hitInterval
        timer = lava.heatUpTime - Mathf.Max(0.1f, lava.hitInterval);
    }

    private void SpawnDroplets(LavaSettings lava)
    {
        SpriteRenderer sprite = platform.GetComponentInChildren<SpriteRenderer>();
        Collider2D col = platform.GetComponent<Collider2D>();
        Bounds area = sprite != null ? sprite.bounds : (col != null ? col.bounds : new Bounds(platform.transform.position, Vector3.one));
        int order = sprite != null ? sprite.sortingOrder + 1 : 11;
        LavaDroplets.Spawn(platform.transform, area, order, lava.hitParticlesPrefab, Mathf.Max(1, lava.hitDroplets));
    }
}
