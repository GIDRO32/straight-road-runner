using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss fight UI: the red warning in the middle of the screen and the boss health bar with icon.
/// Builds a default layout at runtime when nothing is assigned, so stage scenes need no setup.
/// To style it yourself, put a BossHud in the scene and assign the references (any left empty are built).
/// </summary>
public class BossHud : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Font for the built UI. Empty = the score text's font, else TMP's default font.")]
    public TMP_FontAsset font;
    public Color warningColor = new Color(0.9f, 0.08f, 0.08f, 1f);
    public Color barColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    public Color barChipColor = new Color(1f, 0.85f, 0.85f, 1f);
    public Color barBackColor = new Color(0f, 0f, 0f, 0.85f);

    [Header("Warning (optional, built if empty)")]
    public CanvasGroup warningGroup;
    public TMP_Text warningLabel;
    public TMP_Text warningSubLabel;

    [Header("Health Bar (optional, built if empty)")]
    public CanvasGroup barGroup;
    public Image iconImage;
    public TMP_Text nameLabel;
    [Tooltip("Image whose fillAmount shows the health (Image Type = Filled)")]
    public Image barFill;
    [Tooltip("Lighter bar that trails behind the health after a hit")]
    public Image barChip;

    [Header("Animation")]
    public float barFadeTime = 0.4f;
    [Tooltip("Seconds for the bar to fill up when the boss appears")]
    public float barFillUpTime = 1.2f;
    public float chipDelay = 0.4f;
    public float chipSpeed = 0.6f;

    private BossBase boss;
    private float shownHealth;
    private float chipHealth;
    private float chipTimer;
    private float fillUp = 1f;
    private Coroutine barFade;

    void Awake()
    {
        Build();
        if (warningGroup != null) warningGroup.alpha = 0f;
        if (barGroup != null) barGroup.alpha = 0f;
    }

    // ===== WARNING =====

    /// <summary>Red text fades in (slightly zooming), holds with a pulse, fades out.</summary>
    public IEnumerator PlayWarning(string text, string subText, float fadeIn, float hold, float fadeOut)
    {
        if (warningGroup == null) yield break;

        if (warningLabel != null) warningLabel.text = text;
        if (warningSubLabel != null) warningSubLabel.text = subText;
        RectTransform rt = warningGroup.transform as RectTransform;

        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / fadeIn);
            warningGroup.alpha = k;
            rt.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, k);
            yield return null;
        }

        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            warningGroup.alpha = 0.8f + 0.2f * Mathf.Cos(t * Mathf.PI * 3f); // Pulse
            yield return null;
        }

        float start = warningGroup.alpha;
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            warningGroup.alpha = Mathf.Lerp(start, 0f, t / fadeOut);
            yield return null;
        }

        warningGroup.alpha = 0f;
        rt.localScale = Vector3.one;
    }

    // ===== HEALTH BAR =====

    public void ShowBoss(BossBase newBoss)
    {
        boss = newBoss;
        if (boss == null) return;

        if (nameLabel != null) nameLabel.text = boss.Title;
        if (iconImage != null)
        {
            iconImage.sprite = boss.Icon;
            iconImage.enabled = boss.Icon != null;
        }

        shownHealth = chipHealth = 0f;
        fillUp = 0f;
        SetBar(0f, 0f);
        FadeBar(1f);
    }

    public void HideBoss()
    {
        boss = null;
        FadeBar(0f);
    }

    void Update()
    {
        if (boss == null) return;

        float target = boss.HealthNormalized;

        // Fill up when the boss appears
        if (fillUp < 1f)
        {
            fillUp = Mathf.Min(1f, fillUp + Time.deltaTime / Mathf.Max(0.01f, barFillUpTime));
            shownHealth = chipHealth = Mathf.Min(target, Mathf.SmoothStep(0f, 1f, fillUp));
            SetBar(shownHealth, chipHealth);
            return;
        }

        // Health drops at once, the chip follows after a moment
        if (target < shownHealth) chipTimer = chipDelay;
        shownHealth = target;

        if (chipTimer > 0f) chipTimer -= Time.deltaTime;
        else chipHealth = Mathf.MoveTowards(chipHealth, shownHealth, chipSpeed * Time.deltaTime);
        if (chipHealth < shownHealth) chipHealth = shownHealth;

        SetBar(shownHealth, chipHealth);
    }

    private void SetBar(float health, float chip)
    {
        if (barFill != null) barFill.fillAmount = health;
        if (barChip != null) barChip.fillAmount = chip;
    }

    private void FadeBar(float to)
    {
        if (barGroup == null) return;
        if (barFade != null) StopCoroutine(barFade);
        barFade = StartCoroutine(FadeGroup(barGroup, to, barFadeTime));
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float to, float duration)
    {
        float from = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    // ===== DEFAULT LAYOUT =====

    private void Build()
    {
        if (warningGroup != null && barGroup != null) return;

        if (font == null && ScoreManager.Instance != null && ScoreManager.Instance.scoreText != null)
            font = ScoreManager.Instance.scoreText.font;
        if (font == null) font = TMP_Settings.defaultFontAsset;

        var canvasGo = new GameObject("BossHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = (RectTransform)canvasGo.transform;

        if (warningGroup == null) BuildWarning(root);
        if (barGroup == null) BuildBar(root);
    }

    private void BuildWarning(RectTransform root)
    {
        RectTransform panel = MakeRect("Warning", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 320f));
        warningGroup = panel.gameObject.AddComponent<CanvasGroup>();
        warningGroup.blocksRaycasts = false;
        warningGroup.interactable = false;

        warningLabel = MakeText("Text", panel, 150f, warningColor, FontStyles.Bold);
        Stretch(warningLabel.rectTransform, new Vector2(0f, 0.38f), Vector2.one);

        warningSubLabel = MakeText("Title", panel, 64f, warningColor, FontStyles.Bold);
        Stretch(warningSubLabel.rectTransform, Vector2.zero, new Vector2(1f, 0.38f));
    }

    // Bottom right corner: name and bar, icon to the right of the bar
    private void BuildBar(RectTransform root)
    {
        RectTransform panel = MakeRect("BossBar", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(900f, 120f));
        panel.pivot = new Vector2(1f, 0f);
        barGroup = panel.gameObject.AddComponent<CanvasGroup>();
        barGroup.blocksRaycasts = false;
        barGroup.interactable = false;

        // Icon in a dark frame on the right
        RectTransform frame = MakeRect("IconFrame", panel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(120f, 120f));
        frame.gameObject.AddComponent<Image>().color = barBackColor;
        iconImage = MakeRect("Icon", frame, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12f, -12f)).gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;

        // Name above the bar
        nameLabel = MakeText("Name", panel, 40f, Color.white, FontStyles.Bold);
        nameLabel.alignment = TextAlignmentOptions.BottomRight;
        RectTransform nameRt = nameLabel.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 0.5f); nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(0f, 6f); nameRt.offsetMax = new Vector2(-140f, 0f);

        // Bar: back, chip, fill (drains toward the icon)
        RectTransform bar = MakeRect("Bar", panel, new Vector2(0f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        bar.offsetMin = new Vector2(0f, 14f); bar.offsetMax = new Vector2(-140f, -4f);
        bar.gameObject.AddComponent<Image>().color = barBackColor;

        barChip = FilledImage("Chip", bar, barChipColor);
        barFill = FilledImage("Fill", bar, barColor);
    }

    private Image FilledImage(string name, RectTransform parent, Color color)
    {
        RectTransform rt = MakeRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8f, -8f));
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = color;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Right;
        img.fillAmount = 1f;
        return img;
    }

    private TMP_Text MakeText(string name, RectTransform parent, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        return text;
    }

    private static RectTransform MakeRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Filled Images need a sprite
    private static Sprite whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }
}
