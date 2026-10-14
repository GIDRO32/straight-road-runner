using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static MenuUiKit;

/// <summary>
/// Builds the Stage Select panel inside the open MainMenu scene:
/// Tools > Straight Road Runner > Build Stage Select Panel.
/// Reuses the sprites and font of the existing menu so it matches the rest of the UI.
/// Safe to run again: it replaces the panel and button it created before (Ctrl+Z works too).
/// </summary>
public static class StageSelectPanelBuilder
{
    private const string Title = "Build Stage Select Panel";
    private const string PanelName = "StageSelect";
    public const string OpenButtonName = "SelectStageButton";

    // Layout (canvas reference resolution is 1920x1080)
    public static readonly Vector2 OpenButtonPos = new Vector2(720, -380); // Bottom right, above the version text
    public static readonly Vector2 OpenButtonSize = new Vector2(420, 110);

    [MenuItem("Tools/Straight Road Runner/Build Stage Select Panel")]
    private static void Build()
    {
        if (!TryGetMenu(Title, out MenuRefs refs)) return;

        Transform existingPanel = refs.panelCollection.Find(PanelName);
        Transform existingButton = refs.mainMenu.Find(OpenButtonName);
        if ((existingPanel != null || existingButton != null) &&
            !EditorUtility.DisplayDialog(Title, "A Stage Select panel already exists. Replace it?", "Replace", "Cancel"))
            return;

        bool rewirePlay = EditorUtility.DisplayDialog(Title,
            "Make the main Play button load the stage chosen in Stage Select?\n(Instead of always loading SketchyRoad.)",
            "Yes", "No");

        Undo.SetCurrentGroupName(Title);
        int undoGroup = Undo.GetCurrentGroup();

        ReadStyle(refs.canvas); // Before deleting the old panel, it's one of the style sources

        // Keep the button where the user placed it when rebuilding
        Vector2 buttonPos = OpenButtonPos, buttonSize = OpenButtonSize;
        string buttonLabel = "Select Stage";
        if (existingButton != null)
        {
            var rect = existingButton.GetComponent<RectTransform>();
            buttonPos = rect.anchoredPosition;
            buttonSize = rect.sizeDelta;
            TMP_Text label = existingButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) buttonLabel = label.text;
        }

        if (existingPanel != null) Undo.DestroyObjectImmediate(existingPanel.gameObject);
        if (existingButton != null) Undo.DestroyObjectImmediate(existingButton.gameObject);

        GameObject panel = BuildPanel(refs, out PanelManager panelManager);
        Undo.RegisterCreatedObjectUndo(panel, "Create Stage Select Panel");

        Button openButton = MakeButton(refs.mainMenu, OpenButtonName, buttonLabel, buttonPos, buttonSize, 50);
        UnityEventTools.AddPersistentListener(openButton.onClick, panelManager.OpenPanel);
        Undo.RegisterCreatedObjectUndo(openButton.gameObject, "Create Select Stage Button");

        if (rewirePlay)
            RewirePlayButton(refs.mainMenu, refs.fader);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = panel;

        Debug.Log("Stage Select panel built. Save the scene (Ctrl+S) to keep it.", panel);
    }

    private static GameObject BuildPanel(MenuRefs refs, out PanelManager panelManager)
    {
        GameObject root = CreatePanelRoot(PanelName, refs, out panelManager);

        StageSelectPanel stageSelect = root.AddComponent<StageSelectPanel>();
        stageSelect.fader = refs.fader;

        GameObject frame = CreateFrame(root, "Select Stage", refs.settingsPanel, panelManager);

        BuildGrid(frame.transform, stageSelect);
        BuildRightSide(frame.transform, stageSelect);

        // Play the selected stage
        stageSelect.playButton = MakeButton(frame.transform, "PlayButton", "Play", new Vector2(0, -380), new Vector2(470, 110), 55);

        return root;
    }

    // Left: scrollable grid of stage tiles
    private static void BuildGrid(Transform frame, StageSelectPanel stageSelect)
    {
        GameObject area = NewUI("StageGrid", frame);
        SetRect(area, Center, Center, new Vector2(-430, 0), new Vector2(620, 600));

        RectTransform content = CreateScrollView(area, false, out _);
        MakeGrid(content, 180, 3);

        stageSelect.gridContent = content;
        stageSelect.stageTileTemplate = BuildTileTemplate(content, "StageTileTemplate", 120);
    }

    // Right: Artwork / Info switch
    private static void BuildRightSide(Transform frame, StageSelectPanel stageSelect)
    {
        GameObject area = NewUI("StageDetails", frame);
        SetRect(area, Center, Center, new Vector2(330, 0), new Vector2(840, 600));

        // Switch tabs
        stageSelect.artworkTabButton = MakeButton(area.transform, "ArtworkTab", "Artwork", new Vector2(-210, 245), new Vector2(380, 100), 45);
        stageSelect.infoTabButton = MakeButton(area.transform, "InfoTab", "Info", new Vector2(210, 245), new Vector2(380, 100), 45);

        Vector2 viewPos = new Vector2(0, -55);
        Vector2 viewSize = new Vector2(840, 480);

        // --- Artwork view: artwork + stage name
        GameObject artworkView = NewUI("ArtworkView", area.transform);
        SetRect(artworkView, Center, Center, viewPos, viewSize);

        GameObject artwork = NewUI("Artwork", artworkView.transform);
        SetRect(artwork, Center, Center, new Vector2(0, 45), new Vector2(800, 380));
        Image artworkImage = AddImage(artwork, null, Color.white, raycast: false);
        artworkImage.preserveAspect = true;

        GameObject stageName = NewUI("StageName", artworkView.transform);
        SetRect(stageName, Center, Center, new Vector2(0, -195), new Vector2(800, 90));
        TextMeshProUGUI stageNameText = AddText(stageName, "Stage Name", 60, TextAlignmentOptions.Center, Color.white, true);

        // --- Info view: scrollable description, enemies and obstacles
        GameObject infoView = NewUI("InfoView", area.transform);
        SetRect(infoView, Center, Center, viewPos, viewSize);
        AddImage(infoView, null, DarkPanel);

        RectTransform content = CreateScrollView(infoView, true, out ScrollRect scroll);
        MakeVerticalList(content);

        // Stage description (stays at the top, filled per stage)
        GameObject description = NewUI("Description", content);
        TextMeshProUGUI descriptionText = AddText(description, "Description", 34, TextAlignmentOptions.TopLeft, Color.white, false);

        // Templates
        TextMeshProUGUI headerText = BuildTextTemplate(content, "SectionHeaderTemplate", "Enemies", 48, Gold, true);
        TextMeshProUGUI emptyText = BuildTextTemplate(content, "EmptyTextTemplate", "Nothing here.", 32, new Color(1, 1, 1, 0.7f), false);
        emptyText.fontStyle = FontStyles.Italic;
        ContentEntryView entry = BuildInfoEntry(content);

        artworkView.SetActive(true);
        infoView.SetActive(false);

        stageSelect.artworkView = artworkView;
        stageSelect.artworkImage = artworkImage;
        stageSelect.stageNameText = stageNameText;
        stageSelect.stageDescriptionText = descriptionText;
        stageSelect.infoView = infoView;
        stageSelect.infoScroll = scroll;
        stageSelect.infoContent = content;
        stageSelect.sectionHeaderTemplate = headerText;
        stageSelect.emptyTextTemplate = emptyText;
        stageSelect.infoEntryTemplate = entry;
    }

    private static void RewirePlayButton(Transform mainMenu, UIFader fader)
    {
        Transform play = mainMenu.Find("MenuButtons/Play");
        Button button = play != null ? play.GetComponent<Button>() : null;
        if (button == null || fader == null)
        {
            Debug.LogWarning("Stage Select: Couldn't find MainMenu/MenuButtons/Play or the Fader; Play button left unchanged.");
            return;
        }

        Undo.RecordObject(button, "Rewire Play Button");

        // Replace the fader call (FadeIn, or an earlier FadeInToSelectedStage), keep anything else
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            string method = button.onClick.GetPersistentMethodName(i);
            if (button.onClick.GetPersistentTarget(i) == fader && (method == "FadeIn" || method == "FadeInToSelectedStage"))
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        UnityEventTools.AddPersistentListener(button.onClick, fader.FadeInToSelectedStage);
        EditorUtility.SetDirty(button);
    }
}
