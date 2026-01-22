using UnityEngine;
using System.Collections;

public class PanelManager : MonoBehaviour
{
    [Header("Animation Settings")]
    public float fadeDuration = 0.3f;
    public float scaleDuration = 0.25f;
    public AnimationType animationType = AnimationType.FadeAndScale;
    
    [Header("Panel References")]
    public CanvasGroup canvasGroup;
    public RectTransform panelRect;
    
    public enum AnimationType
    {
        Fade,
        Scale,
        FadeAndScale,
        SlideFromTop,
        SlideFromBottom
    }
    
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private bool isOpen = false;
    
    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
            
        if (panelRect == null)
            panelRect = GetComponent<RectTransform>();
            
        originalScale = panelRect.localScale;
        originalPosition = panelRect.anchoredPosition;
        
        // Start hidden
        SetPanelState(false, true);
    }
    
    public void OpenPanel()
    {
        if (isOpen) return;
        
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(AnimatePanel(true));
    }
    
    public void ClosePanel()
    {
        if (!isOpen) return;
        
        StopAllCoroutines();
        StartCoroutine(AnimatePanel(false));
    }
    
    public void TogglePanel()
    {
        if (isOpen)
            ClosePanel();
        else
            OpenPanel();
    }
    
    private IEnumerator AnimatePanel(bool open)
    {
        isOpen = open;
        
        switch (animationType)
        {
            case AnimationType.Fade:
                yield return FadeAnimation(open);
                break;
                
            case AnimationType.Scale:
                yield return ScaleAnimation(open);
                break;
                
            case AnimationType.FadeAndScale:
                yield return FadeAndScaleAnimation(open);
                break;
                
            case AnimationType.SlideFromTop:
                yield return SlideAnimation(open, Vector2.up);
                break;
                
            case AnimationType.SlideFromBottom:
                yield return SlideAnimation(open, Vector2.down);
                break;
        }
        
        if (!open)
            gameObject.SetActive(false);
    }
    
    private IEnumerator FadeAnimation(bool fadeIn)
    {
        float start = fadeIn ? 0f : 1f;
        float end = fadeIn ? 1f : 0f;
        float elapsed = 0f;
        
        canvasGroup.interactable = fadeIn;
        canvasGroup.blocksRaycasts = fadeIn;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, end, elapsed / fadeDuration);
            yield return null;
        }
        
        canvasGroup.alpha = end;
    }
    
    private IEnumerator ScaleAnimation(bool scaleUp)
    {
        Vector3 start = scaleUp ? Vector3.zero : originalScale;
        Vector3 end = scaleUp ? originalScale : Vector3.zero;
        float elapsed = 0f;
        
        canvasGroup.interactable = scaleUp;
        canvasGroup.blocksRaycasts = scaleUp;
        canvasGroup.alpha = 1f;
        
        while (elapsed < scaleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / scaleDuration;
            // Ease out back for bouncy effect
            t = EaseOutBack(t);
            panelRect.localScale = Vector3.Lerp(start, end, t);
            yield return null;
        }
        
        panelRect.localScale = end;
    }
    
    private IEnumerator FadeAndScaleAnimation(bool open)
    {
        Vector3 scaleStart = open ? Vector3.zero : originalScale;
        Vector3 scaleEnd = open ? originalScale : Vector3.zero;
        float alphaStart = open ? 0f : 1f;
        float alphaEnd = open ? 1f : 0f;
        
        float duration = Mathf.Max(fadeDuration, scaleDuration);
        float elapsed = 0f;
        
        canvasGroup.interactable = open;
        canvasGroup.blocksRaycasts = open;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float easeT = EaseOutBack(t);
            
            panelRect.localScale = Vector3.Lerp(scaleStart, scaleEnd, easeT);
            canvasGroup.alpha = Mathf.Lerp(alphaStart, alphaEnd, t);
            yield return null;
        }
        
        panelRect.localScale = scaleEnd;
        canvasGroup.alpha = alphaEnd;
    }
    
    private IEnumerator SlideAnimation(bool slideIn, Vector2 direction)
    {
        Vector2 offScreenPos = originalPosition + direction * 2000f;
        Vector2 start = slideIn ? offScreenPos : originalPosition;
        Vector2 end = slideIn ? originalPosition : offScreenPos;
        
        float elapsed = 0f;
        
        canvasGroup.interactable = slideIn;
        canvasGroup.blocksRaycasts = slideIn;
        canvasGroup.alpha = 1f;
        
        while (elapsed < scaleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / scaleDuration;
            t = EaseOutCubic(t);
            panelRect.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }
        
        panelRect.anchoredPosition = end;
    }
    
    private void SetPanelState(bool open, bool instant)
    {
        isOpen = open;
        
        if (instant)
        {
            canvasGroup.alpha = open ? 1f : 0f;
            canvasGroup.interactable = open;
            canvasGroup.blocksRaycasts = open;
            panelRect.localScale = open ? originalScale : Vector3.zero;
            panelRect.anchoredPosition = originalPosition;
            gameObject.SetActive(open);
        }
    }
    
    // Easing functions
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}