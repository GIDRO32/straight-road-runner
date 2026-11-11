using UnityEngine;

/// <summary>
/// Red warning circle on the ground/air where an attack will land. Built in code (no asset needed).
/// Warning: faint and pulsing. Lock(): solid red, the hit is coming. Pop(): flashes and disappears.
/// </summary>
public class WarningCircle : MonoBehaviour
{
    public Color warningColor = new Color(1f, 0.15f, 0.15f, 0.35f);
    public Color lockedColor = new Color(1f, 0.08f, 0.08f, 0.9f);
    public float appearTime = 0.25f;
    public float popTime = 0.2f;

    private SpriteRenderer sr;
    private float diameter;
    private float age;
    private bool locked;
    private float popTimer = -1f;

    public bool IsLocked => locked;

    public static WarningCircle Create(Vector3 position, float radius, int sortingOrder)
    {
        var go = new GameObject("WarningCircle");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        var circle = go.AddComponent<WarningCircle>();
        circle.sr = go.AddComponent<SpriteRenderer>();
        circle.sr.sprite = RingSprite();
        circle.sr.sortingOrder = sortingOrder;
        circle.diameter = radius * 2f;
        circle.Apply(0f);
        return circle;
    }

    public void Lock() => locked = true;

    public void Pop()
    {
        if (popTimer < 0f) popTimer = 0f;
    }

    void Update()
    {
        age += Time.deltaTime;

        if (popTimer >= 0f)
        {
            popTimer += Time.deltaTime;
            float k = Mathf.Clamp01(popTimer / popTime);
            transform.localScale = Vector3.one * diameter * (1f + 0.4f * k);
            sr.color = new Color(lockedColor.r, lockedColor.g, lockedColor.b, lockedColor.a * (1f - k));
            if (k >= 1f) Destroy(gameObject);
            return;
        }

        Apply(Mathf.Clamp01(age / appearTime));
    }

    private void Apply(float appear)
    {
        float pulse = locked ? 1f : 1f + 0.06f * Mathf.Sin(age * 10f);
        transform.localScale = Vector3.one * diameter * Mathf.Lerp(0.6f, 1f, appear) * pulse;
        Color c = locked ? lockedColor : warningColor;
        if (!locked) c.a *= 0.75f + 0.25f * Mathf.Sin(age * 10f);
        c.a *= appear;
        sr.color = c;
    }

    // Ring with a soft fill, generated once. The sprite is 1 unit wide.
    private static Sprite ring;
    private static Sprite RingSprite()
    {
        if (ring != null) return ring;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.1f);  // Ring
                a = Mathf.Max(a, d < 0.8f ? 0.3f : 0f);                       // Soft fill
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return ring;
    }
}
