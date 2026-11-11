using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Created automatically, lives across scenes:
/// - saves PlayerProgress when leaving a scene, when the game is paused/unfocused and on quit;
/// - shows a "Task complete! +1 Shard" note in the top right corner when a task pays out.
/// </summary>
public class ProgressRuntime : MonoBehaviour
{
    private static ProgressRuntime instance;

    private readonly Queue<(string title, int shards)> toasts = new Queue<(string, int)>();
    private bool showing;
    private CanvasGroup toastGroup;
    private TextMeshProUGUI toastTitle;
    private TextMeshProUGUI toastReward;
    private Image toastIcon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("ProgressRuntime");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ProgressRuntime>();
    }

    void OnEnable()
    {
        PlayerProgress.TaskCompleted += OnTaskCompleted;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    void OnDisable()
    {
        PlayerProgress.TaskCompleted -= OnTaskCompleted;
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void OnSceneChanged(Scene from, Scene to) => PlayerProgress.SaveIfDirty();
    void OnApplicationPause(bool paused) { if (paused) PlayerProgress.SaveIfDirty(); }
    void OnApplicationFocus(bool focused) { if (!focused) PlayerProgress.SaveIfDirty(); }
    void OnApplicationQuit() => PlayerProgress.SaveIfDirty();

    // ===== TOAST =====

    private void OnTaskCompleted(TaskDefinition task, int shards)
    {
        toasts.Enqueue((task != null ? task.DisplayName : "Task", shards));
        if (!showing) StartCoroutine(ShowToasts());
    }

    private IEnumerator ShowToasts()
    {
        showing = true;
        if (toastGroup == null) BuildToast();

        while (toasts.Count > 0)
        {
            var (title, shards) = toasts.Dequeue();
            toastTitle.text = $"Task complete!\n<size=75%>{title}</size>";
            toastReward.text = $"+{shards}";

            yield return Fade(0f, 1f, 0.3f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Fade(1f, 0f, 0.4f);
        }
        showing = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        RectTransform rt = (RectTransform)toastGroup.transform;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            toastGroup.alpha = Mathf.Lerp(from, to, k);
            rt.anchoredPosition = new Vector2(-30f, Mathf.Lerp(from < to ? 60f : -30f, from < to ? -30f : 60f, k));
            yield return null;
        }
        toastGroup.alpha = to;
    }

    private void BuildToast()
    {
        TMP_FontAsset font = null;
        var anyText = FindObjectOfType<TMP_Text>();
        if (anyText != null) font = anyText.font;
        if (font == null) font = TMP_Settings.defaultFontAsset;

        var canvasGo = new GameObject("ToastCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var rt = (RectTransform)panel.transform;
        rt.SetParent(canvasGo.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(560f, 140f);
        rt.anchoredPosition = new Vector2(-30f, -30f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        panel.GetComponent<Image>().raycastTarget = false;
        toastGroup = panel.GetComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;

        toastTitle = MakeText(rt, font, 40f, TextAlignmentOptions.MidlineLeft, new Vector2(24f, 0f), new Vector2(-170f, 0f));

        var icon = new GameObject("Shard", typeof(RectTransform), typeof(Image));
        var iconRt = (RectTransform)icon.transform;
        iconRt.SetParent(rt, false);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(1f, 0.5f);
        iconRt.anchoredPosition = new Vector2(-60f, 0f);
        iconRt.sizeDelta = new Vector2(90f, 90f);
        toastIcon = icon.GetComponent<Image>();
        toastIcon.preserveAspect = true;
        toastIcon.raycastTarget = false;
        var db = ContentDatabase.Instance;
        toastIcon.sprite = db != null ? db.shardIcon : null;
        toastIcon.enabled = toastIcon.sprite != null;

        toastReward = MakeText(rt, font, 44f, TextAlignmentOptions.MidlineRight, new Vector2(0f, 0f), new Vector2(-110f, 0f));
    }

    private static TextMeshProUGUI MakeText(RectTransform parent, TMP_FontAsset font, float size, TextAlignmentOptions align, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
