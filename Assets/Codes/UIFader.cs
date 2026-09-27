using UnityEngine;
using System.Collections; // Required for using Coroutines
using UnityEngine.UI; // Required for Image, Text components (if you don't use CanvasGroup)
using UnityEngine.SceneManagement;

public class UIFader : MonoBehaviour
{
    // Reference to the CanvasGroup component
    private CanvasGroup canvasGroup;

    // Duration of the fade effect in seconds
    public float fadeDuration = 0.5f;
    public string sceneToLoad;

    private void Awake()
    {
        // Get the CanvasGroup component attached to this GameObject
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogError("UIFader requires a CanvasGroup component on the same GameObject.");
            // Optionally add the component automatically if not found
            // canvasGroup = gameObject.AddComponent<CanvasGroup>(); 
        }
        if (canvasGroup.alpha == 1f)
        {
            FadeOut();
        }
    }

    /// <summary>
    /// Fades the UI element in (makes it visible).
    /// </summary>
    public void FadeIn()
    {
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(
            canvasGroup,
            canvasGroup.alpha,
            1f,
            fadeDuration,
            true
        ));

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }


    /// <summary>
    /// Fades the UI element out (makes it invisible).
    /// </summary>
    public void FadeOut()
    {
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(
            canvasGroup,
            canvasGroup.alpha,
            0f,
            fadeDuration,
            false
        ));

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }


    /// <summary>
    /// Coroutine to handle the gradual change in alpha over time.
    /// </summary>
    private IEnumerator FadeCanvasGroup(
        CanvasGroup cg,
        float startAlpha,
        float endAlpha,
        float duration,
        bool useUnscaledTime
    )
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        cg.alpha = endAlpha;

        if (endAlpha == 1f && !string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }


}