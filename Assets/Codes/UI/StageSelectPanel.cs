using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Stage selection: grid of stages on the left, and on the right a switch between
/// the stage artwork + name and a scrollable list of its enemies and obstacles.
/// Stages come from ContentDatabase, so new StageDefinitions show up automatically.
/// Build the UI with Tools > Straight Road Runner > Build Stage Select Panel.
/// </summary>
public class StageSelectPanel : MonoBehaviour
{
    [Header("Grid")]
    public Transform gridContent;
    [Tooltip("Inactive tile cloned for every stage")]
    public ContentEntryView stageTileTemplate;
    public bool includeChaosMode = true;
    public Color tileNormalColor = Color.white;
    public Color tileSelectedColor = new Color(1f, 0.82f, 0.3f, 1f);

    [Header("Switch")]
    public Button artworkTabButton;
    public Button infoTabButton;
    public Color tabActiveColor = new Color(1f, 0.82f, 0.3f, 1f);
    public Color tabInactiveColor = Color.white;

    [Header("Artwork View")]
    public GameObject artworkView;
    public Image artworkImage;
    public TMP_Text stageNameText;
    public TMP_Text stageDescriptionText;

    [Header("Info View (scrollable)")]
    public GameObject infoView;
    public ScrollRect infoScroll;
    public Transform infoContent;
    [Tooltip("Inactive section title cloned for \"Enemies\" / \"Obstacles\"")]
    public TMP_Text sectionHeaderTemplate;
    [Tooltip("Inactive row cloned for every enemy / obstacle")]
    public ContentEntryView infoEntryTemplate;
    [Tooltip("Inactive text cloned when a section is empty")]
    public TMP_Text emptyTextTemplate;

    [Header("Texts")]
    public string enemiesTitle = "Enemies";
    public string obstaclesTitle = "Obstacles";
    public string noEnemiesText = "No enemies on this stage.";
    public string noObstaclesText = "No obstacles on this stage.";

    [Header("Play")]
    public Button playButton;
    [Tooltip("Optional: fades out before loading the stage")]
    public UIFader fader;

    private readonly List<StageDefinition> stages = new List<StageDefinition>();
    private readonly List<ContentEntryView> tiles = new List<ContentEntryView>();
    private readonly List<GameObject> infoRows = new List<GameObject>();
    private StageDefinition selected;
    private bool showingInfo = false;
    private bool initialized = false;

    public StageDefinition Selected => selected;

    // PanelManager deactivates the panel in Awake, so set up on first open instead
    void OnEnable()
    {
        if (!initialized)
            Initialize();

        Select(GameSession.SelectedStage);
    }

    private void Initialize()
    {
        initialized = true;

        if (artworkTabButton != null) artworkTabButton.onClick.AddListener(ShowArtwork);
        if (infoTabButton != null) infoTabButton.onClick.AddListener(ShowInfo);
        if (playButton != null) playButton.onClick.AddListener(PlaySelected);

        HideTemplates();
        BuildGrid();
        ShowArtwork();
    }

    private void HideTemplates()
    {
        if (stageTileTemplate != null) stageTileTemplate.gameObject.SetActive(false);
        if (sectionHeaderTemplate != null) sectionHeaderTemplate.gameObject.SetActive(false);
        if (infoEntryTemplate != null) infoEntryTemplate.gameObject.SetActive(false);
        if (emptyTextTemplate != null) emptyTextTemplate.gameObject.SetActive(false);
    }

    // ===== GRID =====

    private void BuildGrid()
    {
        var db = ContentDatabase.Instance;
        if (db == null || gridContent == null || stageTileTemplate == null) return;

        stages.AddRange(db.Stages);
        if (includeChaosMode && db.ChaosStage != null)
            stages.Add(db.ChaosStage);

        foreach (var stage in stages)
        {
            ContentEntryView tile = Instantiate(stageTileTemplate, gridContent);
            tile.gameObject.SetActive(true);
            tile.name = stage.id;
            tile.Set(stage.DisplayName, stage.icon != null ? stage.icon : stage.artwork);

            StageDefinition captured = stage;
            Button button = tile.button != null ? tile.button : tile.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => Select(captured));

            tiles.Add(tile);
        }
    }

    // ===== SELECTION =====

    public void Select(StageDefinition stage)
    {
        if (stages.Count == 0) return;
        if (stage == null || !stages.Contains(stage)) stage = stages[0];

        selected = stage;
        GameSession.SelectedStage = stage;

        for (int i = 0; i < tiles.Count; i++)
            tiles[i].SetHighlighted(stages[i] == stage, tileNormalColor, tileSelectedColor);

        RefreshArtwork();
        RefreshInfo();
    }

    public void SelectById(string stageId)
    {
        var db = ContentDatabase.Instance;
        if (db != null) Select(db.GetStage(stageId));
    }

    // ===== SWITCH =====

    public void ShowArtwork() => SetView(false);
    public void ShowInfo() => SetView(true);
    public void ToggleView() => SetView(!showingInfo);

    private void SetView(bool info)
    {
        showingInfo = info;

        if (artworkView != null) artworkView.SetActive(!info);
        if (infoView != null) infoView.SetActive(info);

        TintTab(artworkTabButton, !info);
        TintTab(infoTabButton, info);

        if (info && infoScroll != null)
            infoScroll.verticalNormalizedPosition = 1f; // Back to top
    }

    private void TintTab(Button tab, bool active)
    {
        if (tab != null && tab.targetGraphic != null)
            tab.targetGraphic.color = active ? tabActiveColor : tabInactiveColor;
    }

    // ===== RIGHT SIDE CONTENT =====

    private void RefreshArtwork()
    {
        if (selected == null) return;

        if (stageNameText != null) stageNameText.text = selected.DisplayName;
        if (stageDescriptionText != null) stageDescriptionText.text = selected.description;

        if (artworkImage != null)
        {
            Sprite art = selected.artwork != null ? selected.artwork : selected.icon;
            artworkImage.sprite = art;
            artworkImage.enabled = art != null;
        }
    }

    private void RefreshInfo()
    {
        foreach (var row in infoRows)
            Destroy(row);
        infoRows.Clear();

        if (selected == null || infoContent == null) return;

        AddSection(enemiesTitle, selected.GetEnemyPool(), noEnemiesText, e => e.description);
        AddSection(obstaclesTitle, selected.GetObstaclePool(), noObstaclesText, o => o.description);

        if (infoScroll != null)
            infoScroll.verticalNormalizedPosition = 1f;
    }

    private void AddSection<T>(string title, IReadOnlyList<T> items, string emptyText, System.Func<T, string> describe)
        where T : ContentDefinition
    {
        if (sectionHeaderTemplate != null)
        {
            TMP_Text header = Instantiate(sectionHeaderTemplate, infoContent);
            header.gameObject.SetActive(true);
            header.text = title;
            infoRows.Add(header.gameObject);
        }

        bool any = false;
        foreach (var item in items)
        {
            if (item == null || !item.includeInDemo || infoEntryTemplate == null) continue;

            ContentEntryView row = Instantiate(infoEntryTemplate, infoContent);
            row.gameObject.SetActive(true);
            row.Set(item.DisplayName, item.icon, describe(item));
            infoRows.Add(row.gameObject);
            any = true;
        }

        if (!any && emptyTextTemplate != null)
        {
            TMP_Text empty = Instantiate(emptyTextTemplate, infoContent);
            empty.gameObject.SetActive(true);
            empty.text = emptyText;
            infoRows.Add(empty.gameObject);
        }
    }

    // ===== PLAY =====

    public void PlaySelected()
    {
        if (selected == null) return;

        GameSession.SelectedStage = selected;

        if (fader != null)
            fader.FadeInToSelectedStage();
        else
            GameSession.LoadStage(selected);
    }
}
