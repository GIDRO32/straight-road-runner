using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static MenuUiKit;

/// <summary>
/// Builds the Character Select panel inside the open MainMenu scene:
/// Tools > Straight Road Runner > Build Character Select Panel.
/// Left: character grid (icons only). Middle: Character Select artwork + name. Right: info wall with Bio / Stats / Moveset.
/// Also adds the "Character" button to the left of the Stage button.
/// Safe to run again: it replaces what it created before (Ctrl+Z works too).
/// </summary>
public static class CharacterSelectPanelBuilder
{
    private const string Title = "Build Character Select Panel";
    private const string PanelName = "CharacterSelect";
    private const string OpenButtonName = "SelectCharacterButton";
    private const string OldScreenName = "CharacterSelectionScreen";
    private const float ButtonGap = 30f;

    [MenuItem("Tools/Straight Road Runner/Build Character Select Panel")]
    private static void Build()
    {
        if (!TryGetMenu(Title, out MenuRefs refs)) return;

        Transform existingPanel = refs.panelCollection.Find(PanelName);
        Transform existingButton = refs.mainMenu.Find(OpenButtonName);
        if ((existingPanel != null || existingButton != null) &&
            !EditorUtility.DisplayDialog(Title, "A Character Select panel already exists. Replace it?", "Replace", "Cancel"))
            return;

        Transform oldScreen = refs.panelCollection.Find(OldScreenName);
        bool deleteOldScreen = oldScreen != null && EditorUtility.DisplayDialog(Title,
            $"Delete the old '{OldScreenName}'? Nothing opens it any more; the new panel replaces it.",
            "Delete", "Keep");

        Undo.SetCurrentGroupName(Title);
        int undoGroup = Undo.GetCurrentGroup();

        ReadStyle(refs.canvas); // Before deleting anything, old panels are style sources

        GetOpenButtonLayout(refs.mainMenu, existingButton, out Vector2 buttonPos, out Vector2 buttonSize, out string buttonLabel);

        if (existingPanel != null) Undo.DestroyObjectImmediate(existingPanel.gameObject);
        if (existingButton != null) Undo.DestroyObjectImmediate(existingButton.gameObject);
        if (deleteOldScreen) Undo.DestroyObjectImmediate(oldScreen.gameObject);

        GameObject panel = BuildPanel(refs, out PanelManager panelManager);
        Undo.RegisterCreatedObjectUndo(panel, "Create Character Select Panel");

        Button openButton = MakeButton(refs.mainMenu, OpenButtonName, buttonLabel, buttonPos, buttonSize, 50);
        UnityEventTools.AddPersistentListener(openButton.onClick, panelManager.OpenPanel);
        Undo.RegisterCreatedObjectUndo(openButton.gameObject, "Create Character Button");

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = panel;

        Debug.Log("Character Select panel built. Save the scene (Ctrl+S) to keep it.", panel);
    }

    // Rebuild: keep the user's placement. First build: left of the Stage button, same size.
    private static void GetOpenButtonLayout(Transform mainMenu, Transform existingButton,
        out Vector2 pos, out Vector2 size, out string label)
    {
        label = "Character";

        if (existingButton != null)
        {
            var rect = existingButton.GetComponent<RectTransform>();
            pos = rect.anchoredPosition;
            size = rect.sizeDelta;
            TMP_Text text = existingButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null) label = text.text;
            return;
        }

        Transform stageButton = mainMenu.Find(StageSelectPanelBuilder.OpenButtonName);
        if (stageButton != null)
        {
            var rect = stageButton.GetComponent<RectTransform>();
            size = rect.sizeDelta;
            pos = rect.anchoredPosition - new Vector2(size.x + ButtonGap, 0f);
        }
        else
        {
            size = StageSelectPanelBuilder.OpenButtonSize;
            pos = StageSelectPanelBuilder.OpenButtonPos - new Vector2(size.x + ButtonGap, 0f);
        }
    }

    private static GameObject BuildPanel(MenuRefs refs, out PanelManager panelManager)
    {
        GameObject root = CreatePanelRoot(PanelName, refs, out panelManager);

        CharacterSelectPanel characterSelect = root.AddComponent<CharacterSelectPanel>();
        characterSelect.panelManager = panelManager;

        GameObject frame = CreateFrame(root, "Select Character", refs.settingsPanel, panelManager);

        BuildGrid(frame.transform, characterSelect);
        BuildMiddle(frame.transform, characterSelect);
        BuildInfoWall(frame.transform, characterSelect);

        // Selection is saved on click; this just closes the panel
        characterSelect.confirmButton = MakeButton(frame.transform, "SelectButton", "Select", new Vector2(0, -380), new Vector2(470, 110), 55);

        return root;
    }

    // Left: scrollable grid of character tiles
    private static void BuildGrid(Transform frame, CharacterSelectPanel characterSelect)
    {
        GameObject area = NewUI("CharacterGrid", frame);
        SetRect(area, Center, Center, new Vector2(-560, -10), new Vector2(400, 620));

        RectTransform content = CreateScrollView(area, false, out _);
        MakeGrid(content, 170, 2);

        characterSelect.gridContent = content;
        characterSelect.characterTileTemplate = BuildTileTemplate(content, "CharacterTileTemplate", 140, withName: false);
    }

    // Middle: big artwork + name
    private static void BuildMiddle(Transform frame, CharacterSelectPanel characterSelect)
    {
        GameObject area = NewUI("CharacterArt", frame);
        SetRect(area, Center, Center, new Vector2(-95, -10), new Vector2(520, 640));

        GameObject artwork = NewUI("Artwork", area.transform);
        SetRect(artwork, Center, Center, new Vector2(0, 50), new Vector2(520, 540));
        Image artworkImage = AddImage(artwork, null, Color.white, raycast: false);
        artworkImage.preserveAspect = true;

        GameObject nameGo = NewUI("CharacterName", area.transform);
        SetRect(nameGo, Center, Center, new Vector2(0, -275), new Vector2(520, 90));
        TextMeshProUGUI nameText = AddText(nameGo, "Character", 60, TextAlignmentOptions.Center, Color.white, true);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 30;
        nameText.fontSizeMax = 60;

        characterSelect.artworkImage = artworkImage;
        characterSelect.characterNameText = nameText;
    }

    // Right: Bio / Stats / Moveset tabs over one scrollable info area
    private static void BuildInfoWall(Transform frame, CharacterSelectPanel characterSelect)
    {
        GameObject area = NewUI("InfoWall", frame);
        SetRect(area, Center, Center, new Vector2(480, -10), new Vector2(560, 620));

        Vector2 tabSize = new Vector2(175, 90);
        characterSelect.bioTabButton = MakeButton(area.transform, "BioTab", "Bio", new Vector2(-190, 265), tabSize, 36);
        characterSelect.statsTabButton = MakeButton(area.transform, "StatsTab", "Stats", new Vector2(0, 265), tabSize, 36);
        characterSelect.movesetTabButton = MakeButton(area.transform, "MovesetTab", "Moveset", new Vector2(190, 265), tabSize, 36);

        GameObject infoView = NewUI("InfoView", area.transform);
        SetRect(infoView, Center, Center, new Vector2(0, -50), new Vector2(560, 520));
        AddImage(infoView, null, DarkPanel);

        RectTransform content = CreateScrollView(infoView, true, out ScrollRect scroll);
        MakeVerticalList(content);

        // Templates
        characterSelect.paragraphTemplate = BuildTextTemplate(content, "ParagraphTemplate", "Bio", 32, Color.white, false);
        characterSelect.statRowTemplate = BuildStatRowTemplate(content);
        characterSelect.moveEntryTemplate = BuildInfoEntry(content, "MoveEntryTemplate");
        TextMeshProUGUI empty = BuildTextTemplate(content, "EmptyTextTemplate", "Nothing here.", 32, new Color(1, 1, 1, 0.7f), false);
        empty.fontStyle = FontStyles.Italic;
        characterSelect.emptyTextTemplate = empty;

        characterSelect.infoScroll = scroll;
        characterSelect.infoContent = content;
    }

    // Label on the left, value on the right: "Speed .......... A"
    private static StatRowView BuildStatRowTemplate(Transform parent)
    {
        GameObject row = NewUI("StatRowTemplate", parent);
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 60;

        GameObject label = NewUI("Label", row.transform);
        SetRect(label, new Vector2(0f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI labelText = AddText(label, "Stat", 36, TextAlignmentOptions.Left, Color.white, true);
        labelText.enableWordWrapping = false;

        GameObject value = NewUI("Value", row.transform);
        SetRect(value, new Vector2(0.6f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI valueText = AddText(value, "C", 40, TextAlignmentOptions.Right, Gold, true);
        valueText.enableWordWrapping = false;

        StatRowView view = row.AddComponent<StatRowView>();
        view.label = labelText;
        view.value = valueText;
        row.SetActive(false);
        return view;
    }
}
