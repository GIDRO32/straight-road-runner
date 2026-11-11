using UnityEngine;

/// <summary>
/// Purple sparkles around an Epic Sign. Built in code (no asset needed).
/// Particles stay in the world, so they trail behind the sign as it scrolls.
/// </summary>
public static class EpicSparkles
{
    public static readonly Color ColorA = new Color(0.65f, 0.2f, 1f, 1f);
    public static readonly Color ColorB = new Color(1f, 0.55f, 1f, 1f);

    public static ParticleSystem Attach(Transform target, SpriteRenderer sprite = null)
    {
        var go = new GameObject("EpicSparkles");
        go.transform.SetParent(target, false);
        // World scale 1, so sizes below are in world units whatever the sign's scale
        Vector3 s = target.lossyScale;
        go.transform.localScale = new Vector3(SafeInverse(s.x), SafeInverse(s.y), 1f);
        if (sprite != null) go.transform.position = sprite.bounds.center;

        float radius = sprite != null ? Mathf.Max(0.3f, sprite.bounds.extents.magnitude * 0.7f) : 0.6f;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(ColorA, ColorB);
        main.gravityModifier = -0.15f; // Float up
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 60;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = SmokePuff.SoftMaterial;
        renderer.sortingOrder = sprite != null ? sprite.sortingOrder + 1 : 20;

        ps.Play();
        return ps;
    }

    private static float SafeInverse(float v) => Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;
}
