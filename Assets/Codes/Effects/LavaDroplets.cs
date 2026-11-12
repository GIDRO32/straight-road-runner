using UnityEngine;

/// <summary>
/// Lava droplets popping out of a lava platform when it burns the player: they jump up a little,
/// fall back down and fade. Built in code (no asset needed), or pass your own particle prefab.
/// The effect follows the platform as it scrolls and removes itself when finished.
/// </summary>
public static class LavaDroplets
{
    public static readonly Color Hot = new Color(1f, 0.85f, 0.25f, 1f);   // Yellow-orange
    public static readonly Color Warm = new Color(1f, 0.35f, 0.05f, 1f);  // Orange-red

    /// <param name="area">World bounds of the platform (droplets come up from its lower part)</param>
    public static void Spawn(Transform platform, Bounds area, int sortingOrder, GameObject prefabOverride = null, int count = 16)
    {
        if (prefabOverride != null)
        {
            GameObject fx = Object.Instantiate(prefabOverride, platform);
            fx.transform.position = new Vector3(area.center.x, area.center.y, platform.position.z);
            foreach (var system in fx.GetComponentsInChildren<ParticleSystem>()) system.Play();
            Object.Destroy(fx, 5f);
            return;
        }

        var go = new GameObject("LavaDroplets");
        go.transform.SetParent(platform, false);
        // World scale 1 so the sizes and speeds below are in world units whatever the platform's scale
        Vector3 s = platform.lossyScale;
        go.transform.localScale = new Vector3(SafeInverse(s.x), SafeInverse(s.y), 1f);
        go.transform.position = new Vector3(area.center.x, area.min.y + area.size.y * 0.3f, platform.position.z);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
        main.startSpeed = 0f; // Velocity comes from Velocity over Lifetime below
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(Hot, Warm);
        main.gravityModifier = 1.6f; // Up, then back down
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // Stay with the scrolling platform
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 40;
        main.stopAction = ParticleSystemStopAction.Destroy;

        // A few pops spread over a moment, not all at once
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        int first = Mathf.CeilToInt(count * 0.6f);
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)first),
            new ParticleSystem.Burst(0.15f, (short)(count - first))
        });

        // Along the platform's width, from its lower part
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.1f, area.size.x * 0.85f), Mathf.Max(0.05f, area.size.y * 0.3f), 0f);

        // Jump slightly upward, drifting a little to the sides
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        velocity.y = new ParticleSystem.MinMaxCurve(2.2f, 4f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Shrink a bit and cool down (yellow → red → dark) before vanishing
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.4f), 0.6f),
                new GradientColorKey(new Color(0.5f, 0.12f, 0.08f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = SmokePuff.SoftMaterial;
        renderer.sortingOrder = sortingOrder;

        ps.Play();
    }

    private static float SafeInverse(float v) => Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;
}
