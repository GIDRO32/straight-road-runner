using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Stage Select panel inside the open MainMenu scene:
/// Tools > Straight Road Runner > Build Stage Select Panel.
/// Reuses the sprites and font of the existing menu so it matches the rest of the UI.
/// Safe to run again: it replaces the panel and button it created before (Ctrl+Z works too).
/// </summary>
public static class StageSelectPanelBuilder
{
    private const string PanelName = "StageSelect";
    private const string OpenButtonName = "SelectStageButton";

    // Layout (canvas reference resolution is 1920x1080)
    private static readonly Vector2 FrameSize = new Vector2(1600, 900);
    private static readonly Vector2 OpenButtonPos = new Vector2(720, -380); // Bottom right, above the version text
    private static readonly Vector2 OpenButtonSize = new Vector2(420, 110);

    private static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);
    private static readonly Color DarkPanel = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color Invisible = new Color(1f, 1f, 1f, 0.001f); // Catches scroll drags

    // Style taken from the scene
    private static Font font;
    private static Sprite buttonSprite;
    private static Sprite frameSprite;
    private static Sprite tileSprite;
    private static Color outlineColor = new Color(0f, 0.06f, 0.58f, 1f);

    [MenuItem("Tools/Straight Road Runner/Build Stage Select Panel")]
    private static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        GameObject canvasGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
        Transform canvas = canvasGo != null ? canvasGo.transform : null;
        Transform panelCollection = canvas != null ? canvas.Find("PanelCollection") : null;
        Transform mainMenu = canvas != null ? canvas.Find("MainMenu") : null;

        if (panelCollection == null || mainMenu == null)
        {
            EditorUtility.DisplayDialog("Build Stage Select Panel",
                "Open the MainMenu scene first.\n(Needs Canvas/PanelCollection and Canvas/MainMenu.)", "OK");
            return;
        }

        Transform existingPanel = panelCollection.Find(PanelName);
        Transform existingButton = mainMenu.Find(OpenButtonName);
        if ((existingPanel != null || existingButton != null) &&
            !EditorUtility.DisplayDialog("Build Stage Select Panel",
                "A Stage Select panel already exists. Replace it?", "Replace", "Cancel"))
            return;

        bool rewirePlay = EditorUtility.DisplayDialog("Build Stage Select Panel",
            "Make the main Play button load the stage chosen in Stage Select?\n(Instead of always loading SketchyRoad.)",
            "Yes", "No");

        Undo.SetCurrentGroupName("Build Stage Select Panel");
        int undoGroup = Undo.GetCurrentGroup();

        if (existingPanel != null) Undo.DestroyObjectImmediate(existingPanel.gameObject);
        if (existingButton != null) Undo.DestroyObjectImmediate(existingButton.gameObject);

        ReadStyle(canvas);

        UIFader fader = canvas.Find("Fader") != null ? canvas.Find("Fader").GetComponent<UIFader>() : null;
        Transform settingsPanel = panelCollection.Find("Settings");

        GameObject panel = BuildPanel(panelCollection, settingsPanel, fader, out PanelManager panelManager);
        Undo.RegisterCreatedObjectUndo(panel, "Create Stage Select Panel");

        GameObject openButton = BuildOpenButton(mainMenu, panelManager);
        Undo.RegisterCreatedObjectUndo(openButton, "Create Select Stage Button");

        if (rewirePlay)
            RewirePlayButton(mainMenu, fader);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panel;

        Debug.Log("Stage Select panel built. Save the scene (Ctrl+S) to keep it.", panel);
    }

    // ===== STYLE =====

    private static void ReadStyle(Transform canvas)
    {
        Transform play = canvas.Find("MainMenu/MenuButtons/Play");
        Text playText = play != null ? play.GetComponentInChildren<Text>(true) : null;
        Outline playOutline = playText != null ? playText.GetComponent<Outline>() : null;

        font = playText != null ? playText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonSprite = play != null ? play.GetComponent<Image>().sprite : null;
        if (playOutline != null) outlineColor = playOutline.effectColor;

        Image frame = FindImage(canvas, "PanelCollection/CharacterSelectionScreen/Frame")
                      ?? FindImage(canvas, "PanelCollection/Settings/MainFrame");
        frameSprite = frame != null ? frame.sprite : null;

        Transform grid = canvas.Find("PanelCollection/CharacterSelectionScreen/Frame/CharacterGrid");
        Image tile = grid != null && grid.childCount > 0 ? grid.GetChild(0).GetComponent<Image>() : null;
        tileSprite = tile != null ? tile.sprite : buttonSprite;
    }

    private static Image FindImage(Transform root, string path)
    {
        Transform t = root.Find(path);
        Image image = t != null ? t.GetComponent<Image>() : null;
        return image != null ? image : null; // Real null so "??" works
    }

    // ===== PANEL =====

    private static GameObject BuildPanel(Transform parent, Transform settingsPanel, UIFader fader, out PanelManager panelManager)
    {
        // Root: full-screen dim background, same setup as the Settings panel
        GameObject root = NewUI(PanelName, parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        RectTransform settingsRect = settingsPanel != null ? settingsPanel.GetComponent<RectTransform>() : null;
        if (settingsRect != null)
        {
            rootRect.anchorMin = settingsRect.anchorMin;
            rootRect.anchorMax = settingsRect.anchorMax;
            rootRect.anchoredPosition = settingsRect.anchoredPosition;
            rootRect.sizeDelta = settingsRect.sizeDelta;
        }
        else
        {
            SetRect(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080));
        }

        Image settingsBg = settingsPanel != null ? settingsPanel.GetComponent<Image>() : null;
        Image dim = root.AddComponent<Image>();
        if (settingsBg != null)
        {
            dim.sprite = settingsBg.sprite;
            dim.type = settingsBg.type;
            dim.color = settingsBg.color;
        }
        else
        {
            dim.color = new Color(0f, 0f, 0f, 0.6f);
        }

        CanvasGroup group = root.AddComponent<CanvasGroup>();
        panelManager = root.AddComponent<PanelManager>();
        panelManager.canvasGroup = group;
        panelManager.panelRect = rootRect;
        PanelManager settingsManager = settingsPanel != null ? settingsPanel.GetComponent<PanelManager>() : null;
        if (settingsManager != null)
        {
            panelManager.animationType = settingsManager.animationType;
            panelManager.fadeDuration = settingsManager.fadeDuration;
            panelManager.scaleDuration = settingsManager.scaleDuration;
        }

        StageSelectPanel stageSelect = root.AddComponent<StageSelectPanel>();
        stageSelect.fader = fader;

        // Frame
        GameObject frame = NewUI("Frame", root.transform);
        SetRect(frame, Center, Center, Vector2.zero, FrameSize);
        AddImage(frame, frameSprite, Color.white);

        GameObject title = NewUI("Title", frame.transform);
        SetRect(title, Center, Center, new Vector2(0, 375), new Vector2(800, 100));
        AddText(title, "Select Stage", 70, TextAnchor.MiddleCenter, Color.white, true);

        Button close = BuildCloseButton(frame.transform, settingsPanel, panelManager);
        close.name = "Close";

        BuildGrid(frame.transform, stageSelect);
        BuildRightSide(frame.transform, stageSelect);

        // Play the selected stage
        stageSelect.playButton = MakeButton(frame.transform, "PlayButton", "Play", new Vector2(0, -380), new Vector2(470, 110), 55);

        return root;
    }

    private static Button BuildCloseButton(Transform parent, Transform settingsPanel, PanelManager panelManager)
    {
        // Reuse the Settings panel's close (X) button so it looks the same
        Transform source = settingsPanel != null ? settingsPanel.Find("Button (Legacy)") : null;
        Button close;

        if (source != null)
        {
            GameObject copy = Object.Instantiate(source.gameObject, parent, false);
            close = copy.GetComponent<Button>();
            while (close.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(close.onClick, 0);
        }
        else
        {
            close = MakeButton(parent, "Close", "X", Vector2.zero, Vector2.one * 130, 60);
        }

        SetRect(close.gameObject, Center, Center, new Vector2(690, 370), new Vector2(130, 130));
        UnityEventTools.AddPersistentListener(close.onClick, panelManager.ClosePanel);
        return close;
    }

    // Left: scrollable grid of stage tiles
    private static void BuildGrid(Transform frame, StageSelectPanel stageSelect)
    {
        GameObject area = NewUI("StageGrid", frame);
        SetRect(area, Center, Center, new Vector2(-430, 0), new Vector2(620, 600));

        ScrollRect scroll = area.AddComponent<ScrollRect>();
        GameObject viewport = NewUI("Viewport", area.transform);
        Stretch(viewport);
        AddImage(viewport, null, Invisible);
        viewport.AddComponent<RectMask2D>();

        GameObject content = NewUI("Content", viewport.transform);
        RectTransform contentRect = TopStretch(content);

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180, 180);
        grid.spacing = new Vector2(20, 20);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        SetupScroll(scroll, viewport, contentRect);

        // Tile template
        GameObject tile = NewUI("StageTileTemplate", content.transform);
        Image tileImage = AddImage(tile, tileSprite, Color.white);
        Button tileButton = tile.AddComponent<Button>();
        tileButton.targetGraphic = tileImage;

        GameObject icon = NewUI("Icon", tile.transform);
        SetRect(icon, Center, Center, new Vector2(0, 15), new Vector2(120, 120));
        Image iconImage = AddImage(icon, null, Color.white, raycast: false);
        iconImage.preserveAspect = true;

        GameObject name = NewUI("Name", tile.transform);
        SetRect(name, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 25), new Vector2(-16, 46));
        Text nameText = AddText(name, "Stage", 28, TextAnchor.MiddleCenter, Color.white, true);
        nameText.resizeTextForBestFit = true;
        nameText.resizeTextMinSize = 14;
        nameText.resizeTextMaxSize = 28;

        ContentEntryView view = tile.AddComponent<ContentEntryView>();
        view.icon = iconImage;
        view.nameText = nameText;
        view.button = tileButton;
        view.highlight = tileImage;
        tile.SetActive(false);

        stageSelect.gridContent = content.transform;
        stageSelect.stageTileTemplate = view;
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

        // --- Artwork view: big artwork + stage name
        GameObject artworkView = NewUI("ArtworkView", area.transform);
        SetRect(artworkView, Center, Center, viewPos, viewSize);

        GameObject artwork = NewUI("Artwork", artworkView.transform);
        SetRect(artwork, Center, Center, new Vector2(0, 45), new Vector2(800, 380));
        Image artworkImage = AddImage(artwork, null, Color.white, raycast: false);
        artworkImage.preserveAspect = true;

        GameObject stageName = NewUI("StageName", artworkView.transform);
        SetRect(stageName, Center, Center, new Vector2(0, -195), new Vector2(800, 90));
        Text stageNameText = AddText(stageName, "Stage Name", 60, TextAnchor.MiddleCenter, Color.white, true);

        // --- Info view: scrollable description, enemies and obstacles
        GameObject infoView = NewUI("InfoView", area.transform);
        SetRect(infoView, Center, Center, viewPos, viewSize);
        AddImage(infoView, null, DarkPanel);

        ScrollRect scroll = infoView.AddComponent<ScrollRect>();
        GameObject viewport = NewUI("Viewport", infoView.transform);
        Stretch(viewport);
        AddImage(viewport, null, Invisible);
        viewport.AddComponent<RectMask2D>();

        GameObject content = NewUI("Content", viewport.transform);
        RectTransform contentRect = TopStretch(content);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(25, 25, 20, 20);
        layout.spacing = 15;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = BuildScrollbar(infoView.transform);
        SetupScroll(scroll, viewport, contentRect);
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = -3;

        // Stage description (stays at the top, filled per stage)
        GameObject description = NewUI("Description", content.transform);
        Text descriptionText = AddText(description, "Description", 34, TextAnchor.UpperLeft, Color.white, false);

        // Templates
        GameObject header = NewUI("SectionHeaderTemplate", content.transform);
        Text headerText = AddText(header, "Enemies", 48, TextAnchor.MiddleLeft, Gold, true);
        header.SetActive(false);

        GameObject empty = NewUI("EmptyTextTemplate", content.transform);
        Text emptyText = AddText(empty, "Nothing here.", 32, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.7f), false);
        emptyText.fontStyle = FontStyle.Italic;
        empty.SetActive(false);

        ContentEntryView entry = BuildInfoEntry(content.transform);
        entry.gameObject.SetActive(false);

        artworkView.SetActive(true);
        infoView.SetActive(false);

        stageSelect.artworkView = artworkView;
        stageSelect.artworkImage = artworkImage;
        stageSelect.stageNameText = stageNameText;
        stageSelect.stageDescriptionText = descriptionText;
        stageSelect.infoView = infoView;
        stageSelect.infoScroll = scroll;
        stageSelect.infoContent = content.transform;
        stageSelect.sectionHeaderTemplate = headerText;
        stageSelect.emptyTextTemplate = emptyText;
        stageSelect.infoEntryTemplate = entry;
    }

    // One enemy/obstacle row: icon on the left, name + description on the right
    private static ContentEntryView BuildInfoEntry(Transform parent)
    {
        GameObject row = NewUI("InfoEntryTemplate", parent);
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 20;
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        GameObject icon = NewUI("Icon", row.transform);
        Image iconImage = AddImage(icon, null, Color.white, raycast: false);
        iconImage.preserveAspect = true;
        LayoutElement iconLayout = icon.AddComponent<LayoutElement>();
        iconLayout.minWidth = iconLayout.preferredWidth = 110;
        iconLayout.minHeight = iconLayout.preferredHeight = 110;

        GameObject column = NewUI("Texts", row.transform);
        VerticalLayoutGroup columnLayout = column.AddComponent<VerticalLayoutGroup>();
        columnLayout.spacing = 4;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;
        column.AddComponent<LayoutElement>().flexibleWidth = 1;

        GameObject name = NewUI("Name", column.transform);
        Text nameText = AddText(name, "Name", 40, TextAnchor.UpperLeft, Color.white, true);

        GameObject description = NewUI("Description", column.transform);
        Text descriptionText = AddText(description, "Description", 30, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.85f), false);

        ContentEntryView view = row.AddComponent<ContentEntryView>();
        view.icon = iconImage;
        view.nameText = nameText;
        view.descriptionText = descriptionText;
        return view;
    }

    private static Scrollbar BuildScrollbar(Transform parent)
    {
        GameObject bar = NewUI("Scrollbar", parent);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1, 0);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(1, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(24, 0);
        AddImage(bar, null, new Color(0, 0, 0, 0.4f));

        GameObject area = NewUI("Sliding Area", bar.transform);
        Stretch(area);

        GameObject handle = NewUI("Handle", area.transform);
        Stretch(handle);
        Image handleImage = AddImage(handle, null, Gold);

        Scrollbar scrollbar = bar.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    private static void SetupScroll(ScrollRect scroll, GameObject viewport, RectTransform content)
    {
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
    }

    // ===== MAIN MENU BUTTONS =====

    private static GameObject BuildOpenButton(Transform mainMenu, PanelManager panelManager)
    {
        Button button = MakeButton(mainMenu, OpenButtonName, "Select Stage", OpenButtonPos, OpenButtonSize, 50);
        UnityEventTools.AddPersistentListener(button.onClick, panelManager.OpenPanel);
        return button.gameObject;
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

        // Replace the "FadeIn" call on the fader, keep anything else
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (button.onClick.GetPersistentTarget(i) == fader && button.onClick.GetPersistentMethodName(i) == "FadeIn")
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        UnityEventTools.AddPersistentListener(button.onClick, fader.FadeInToSelectedStage);
        EditorUtility.SetDirty(button);
    }

    // ===== UI HELPERS =====

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = Center;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Stretch(GameObject go)
    {
        return SetRect(go, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static RectTransform TopStretch(GameObject go)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static Image AddImage(GameObject go, Sprite sprite, Color color, bool raycast = true)
    {
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static Text AddText(GameObject go, string text, int size, TextAnchor alignment, Color color, bool outline)
    {
        Text t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = alignment;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        if (outline)
        {
            Outline o = go.AddComponent<Outline>();
            o.effectColor = outlineColor;
            o.effectDistance = new Vector2(3, 3);
        }
        return t;
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, int fontSize)
    {
        GameObject go = NewUI(name, parent);
        SetRect(go, Center, Center, pos, size);
        Image image = AddImage(go, buttonSprite, Color.white);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject text = NewUI("Text", go.transform);
        Stretch(text);
        AddText(text, label, fontSize, TextAnchor.MiddleCenter, Color.white, true);
        return button;
    }
}
