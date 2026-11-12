using UnityEngine;

/// <summary>
/// What a platform does when the player stands on it. Platform adds the right one
/// from PlatformDefinition.mechanic. To add a new kind (ice, bouncy...): add a value to
/// PlatformMechanicType, a subclass of this, and a case in Platform.AddMechanic.
/// </summary>
public abstract class PlatformMechanic : MonoBehaviour
{
    protected Platform platform;
    protected PlatformDefinition definition;

    public virtual void Init(Platform owner, PlatformDefinition def)
    {
        platform = owner;
        definition = def;
    }

    public virtual void OnPlayerEnter(GameObject player) { }
    public virtual void OnPlayerExit(GameObject player) { }

    /// <summary>Particles played when the player lands on this platform.</summary>
    public virtual ParticleSystem LandingParticles => platform.normalLandingParticles;
}
