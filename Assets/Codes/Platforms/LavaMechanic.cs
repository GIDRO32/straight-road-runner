using UnityEngine;

/// <summary>
/// After the player has stood on it for LavaSettings.heatUpTime, hits them like an enemy
/// (stamina loss + stun), then again every hitInterval while they stay. Plays the hit animation each time.
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
        }

        // Next hit after hitInterval
        timer = lava.heatUpTime - Mathf.Max(0.1f, lava.hitInterval);
    }
}
