using UnityEngine;

/// <summary>
/// One-shot smoke puff particle effect. Built in code (no asset needed), or pass your own
/// particle prefab to use that instead. The effect destroys itself when finished.
/// </summary>
public static class SmokePuff
{
    private static Material material;

    public static void Spawn(Vector3 position, float size = 1.5f, int sortingOrder = 20,
        GameObject prefabOverride = null, Color? color = null)
    {
        if (prefabOverride != null)
        {
            GameObject fx = Object.Instantiate(prefabOverride, position, Quaternion.identity);
            Object.Destroy(fx, 5f);
            return;
        }

        Color smokeColor = color ?? new Color(0.35f, 0.35f, 0.35f, 0.9f);

        var go = new GameObject("SmokePuff");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = smokeColor;
        main.gravityModifier = -0.1f; // Drifts up a little
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = size * 0.35f;

        // Slow down after the burst
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = size * 0.4f;
        limit.dampen = 0.15f;

        // Grow and fade out
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetMaterial();
        renderer.sortingOrder = sortingOrder;

        ps.Play();
    }

    /// <summary>Soft round particle material (white, tinted by particle color). Shared by other code-built effects.</summary>
    public static Material SoftMaterial => GetMaterial();

    // Soft round blob, generated once
    private static Material GetMaterial()
    {
        if (material != null) return material;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                float a = Mathf.Clamp01(1f - d);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        texture.Apply();

        Shader shader = Shader.Find("Sprites/Default");
        material = new Material(shader) { mainTexture = texture, name = "SmokePuff (generated)" };
        return material;
    }
}
