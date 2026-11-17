using UnityEngine;

/// <summary>Breaks after the player has stood on it for FragileSettings.breakTime seconds.</summary>
public class FragileMechanic : PlatformMechanic
{
    private bool playerOnPlatform = false;
    private float timer = 0f;

    public override ParticleSystem LandingParticles =>
        platform.fragileLandingParticles != null ? platform.fragileLandingParticles : platform.normalLandingParticles;

    public override void OnPlayerEnter(GameObject player)
    {
        playerOnPlatform = true;

        if (platform.fragileLandingParticles != null && !platform.fragileLandingParticles.isPlaying)
            platform.fragileLandingParticles.Play();
    }

    public override void OnPlayerExit(GameObject player)
    {
        playerOnPlatform = false;
        timer = 0f;
    }

    void Update()
    {
        if (!playerOnPlatform || platform.IsBroken) return;

        timer += Time.deltaTime;
        if (timer >= definition.fragile.breakTime)
            platform.Break();
    }
}
