using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put on the title screen logo Image. Shows the selected stage's Title Logo
/// (StageDefinition.titleLogo), or the Image's own sprite when the stage has none.
/// Updates as soon as another stage is picked.
/// </summary>
[RequireComponent(typeof(Image))]
public class StageLogo : MonoBehaviour
{
    [Tooltip("Shown when the selected stage has no Title Logo. Empty = the sprite the Image starts with.")]
    public Sprite defaultLogo;

    private Image image;

    void Awake()
    {
        image = GetComponent<Image>();
        if (defaultLogo == null) defaultLogo = image.sprite;
    }

    void OnEnable()
    {
        GameSession.SelectedStageChanged += Show;
        Show(GameSession.SelectedStage);
    }

    void OnDisable()
    {
        GameSession.SelectedStageChanged -= Show;
    }

    private void Show(StageDefinition stage)
    {
        if (image == null) return;
        image.sprite = stage != null && stage.titleLogo != null ? stage.titleLogo : defaultLogo;
    }
}
