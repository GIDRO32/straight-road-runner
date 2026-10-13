using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared helpers for the menu panel builders (Stage Select, Character Select...).
/// Reads sprites, font and outline from the existing MainMenu so new panels match it.
/// </summary>
public static class MenuUiKit
{
    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    public static readonly Vector2 FrameSize = new Vector2(1600, 900);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);
    public static readonly Color DarkPanel = new Color(0f, 0f, 0f, 0.35f);
    public static readonly Color Invisible = new Color(1f, 1f, 1f, 0.001f); // Catches scroll drags

    // Style taken from the scene
    public static TMP_FontAsset FontAsset;
    public static Material OutlineMaterial;
    public static Sprite ButtonSprite;
    public static Sprite FrameSprite;
    public static Sprite TileSprite;
    private static Color outlineColor = new Color(0f, 0.06f, 0.58f, 1f);

    public struct MenuRefs
    {
        public Transform canvas;
        public Transform panelCollection;
        public Transform mainMenu;
        public Transform settingsPanel;
        public UIFader fader;
    }

    // ===== SETUP =====

    /// <summary>Checks TMP essentials and finds the MainMenu objects. Shows a dialog and returns false if something is missing.</summary>
    public static bool TryGetMenu(string title, out MenuRefs refs)
    {
        refs = default;

        if (!TmpFontUtility.EssentialsImported)
        {
            EditorUtility.DisplayDialog(title,
                "Import TextMeshPro Essential Resources first (the importer opens now), then run this again.", "OK");
            TmpFontUtility.OpenEssentialsImporter();
            return false;
        }

        var scene = EditorSceneManager.GetActiveScene();
        GameObject canvasGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Canvas");
        refs.canvas = canvasGo != null ? canvasGo.transform : null;
        refs.panelCollection = refs.canvas != null ? refs.canvas.Find("PanelCollection") : null;
        refs.mainMenu = refs.canvas != null ? refs.canvas.Find("MainMenu") : null;

        if (refs.panelCollection == null || refs.mainMenu == null)
        {
            EditorUtility.DisplayDialog(title,
                "Open the MainMenu scene first.\n(Needs Canvas/PanelCollection and Canvas/MainMenu.)", "OK");
            return false;
        }

        refs.settingsPanel = refs.panelCollection.Find("Settings");
        Transform fader = refs.canvas.Find("Fader");
        refs.fader = fader != null ? fader.GetComponent<UIFader>() : null;
        return true;
    }

    public static void ReadStyle(Transform canvas)
    {
        Transform play = canvas.Find("MainMenu/MenuButtons/Play");
        ButtonSprite = play != null ? play.GetComponent<Image>().sprite : null;

        // Font + outline: from the Play button's text, whether it's TextMeshPro already or still legacy
        TMP_Text playTmp = play != null ? play.GetComponentInChildren<TMP_Text>(true) : null;
        Text playText = play != null ? play.GetComponentInChildren<Text>(true) : null;

        if (playTmp != null)
        {
            FontAsset = playTmp.font;
            OutlineMaterial = playTmp.fontSharedMaterial;
        }
        else
        {
            Outline playOutline = playText != null ? playText.GetComponent<Outline>() : null;
            if (playOutline != null) outlineColor = playOutline.effectColor;

            FontAsset = TmpFontUtility.GetFontAsset(playText != null ? playText.font : null);
            OutlineMaterial = TmpFontUtility.GetOutlineMaterial(FontAsset, outlineColor,
                playOutline != null ? playOutline.effectDistance.x : 3f);
        }

        if (FontAsset == null) FontAsset = TMP_Settings.defaultFontAsset;
        if (OutlineMaterial == null) OutlineMaterial = TmpFontUtility.GetOutlineMaterial(FontAsset, outlineColor, 3f);

        // Frame and tile sprites: from whichever panel still has them
        Image frame = FindImage(canvas, "PanelCollection/StageSelect/Frame")
                      ?? FindImage(canvas, "PanelCollection/CharacterSelect/Frame")
                      ?? FindImage(canvas, "PanelCollection/CharacterSelectionScreen/Frame")
                      ?? FindImage(canvas, "PanelCollection/Settings/MainFrame");
        FrameSprite = frame != null ? frame.sprite : null;

        Image tile = FindImage(canvas, "PanelCollection/StageSelect/Frame/StageGrid/Viewport/Content/StageTileTemplate")
                     ?? FindImage(canvas, "PanelCollection/CharacterSelect/Frame/CharacterGrid/Viewport/Content/CharacterTileTemplate");
        if (tile == null)
        {
            Transform grid = canvas.Find("PanelCollection/CharacterSelectionScreen/Frame/CharacterGrid");
            tile = grid != null && grid.childCount > 0 ? FindImage(grid, grid.GetChild(0).name) : null;
        }
        TileSprite = tile != null ? tile.sprite : ButtonSprite;
    }

    public static Image FindImage(Transform root, string path)
    {
        Transform t = root.Find(path);
        Image image = t != null ? t.GetComponent<Image>() : null;
        return image != null ? image : null; // Real null so "??" works
    }

    // ===== PANEL PARTS =====

    /// <summary>Full-screen panel root with dim background, CanvasGroup and PanelManager, set up like the Settings panel.</summary>
    public static GameObject CreatePanelRoot(string name, MenuRefs refs, out PanelManager panelManager)
    {
        Transform settingsPanel = refs.settingsPanel;
        GameObject root = NewUI(name, refs.panelCollection);
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
            SetRect(root, Center, Center, Vector2.zero, new Vector2(1920, 1080));
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

        return root;
    }

    /// <summary>Frame image + title + close button.</summary>
    public static GameObject CreateFrame(GameObject root, string title, Transform settingsPanel, PanelManager panelManager)
    {
        GameObject frame = NewUI("Frame", root.transform);
        SetRect(frame, Center, Center, Vector2.zero, FrameSize);
        AddImage(frame, FrameSprite, Color.white);

        GameObject titleGo = NewUI("Title", frame.transform);
        SetRect(titleGo, Center, Center, new Vector2(0, 375), new Vector2(800, 100));
        AddText(titleGo, title, 70, TextAlignmentOptions.Center, Color.white, true);

        Button close = BuildCloseButton(frame.transform, settingsPanel, panelManager);
        close.name = "Close";
        return frame;
    }

    public static Button BuildCloseButton(Transform parent, Transform settingsPanel, PanelManager panelManager)
    {
        // Reuse the Settings panel's close (X) button so it looks the same
        Transform source = settingsPanel != null ? settingsPanel.Find("Button (Legacy)") : null;
        Button close;

        if (source != null && source.GetComponent<Button>() != null)
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

    /// <summary>Vertical ScrollRect with a masked viewport. Returns the content transform.</summary>
    public static RectTransform CreateScrollView(GameObject area, bool withScrollbar, out ScrollRect scroll)
    {
        scroll = area.AddComponent<ScrollRect>();
        GameObject viewport = NewUI("Viewport", area.transform);
        Stretch(viewport);
        AddImage(viewport, null, Invisible);
        viewport.AddComponent<RectMask2D>();

        GameObject content = NewUI("Content", viewport.transform);
        RectTransform contentRect = TopStretch(content);

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;

        if (withScrollbar)
        {
            scroll.verticalScrollbar = BuildScrollbar(area.transform);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = -3;
        }

        return contentRect;
    }

    /// <summary>Content of a scroll view laid out as a vertical list that grows with its children.</summary>
    public static void MakeVerticalList(RectTransform content, int spacing = 15)
    {
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(25, 25, 20, 20);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>Content of a scroll view laid out as a grid of square tiles.</summary>
    public static void MakeGrid(RectTransform content, float cellSize, int columns)
    {
        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.spacing = new Vector2(20, 20);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperCenter;

        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>Inactive grid tile (button with icon, optionally a name below it) used as a template.</summary>
    public static ContentEntryView BuildTileTemplate(Transform parent, string name, float iconSize, bool withName = true)
    {
        GameObject tile = NewUI(name, parent);
        Image tileImage = AddImage(tile, TileSprite, Color.white);
        Button tileButton = tile.AddComponent<Button>();
        tileButton.targetGraphic = tileImage;

        GameObject icon = NewUI("Icon", tile.transform);
        SetRect(icon, Center, Center, new Vector2(0, withName ? 15 : 0), new Vector2(iconSize, iconSize));
        Image iconImage = AddImage(icon, null, Color.white, raycast: false);
        iconImage.preserveAspect = true;

        TextMeshProUGUI nameText = null;
        if (withName)
        {
            GameObject nameGo = NewUI("Name", tile.transform);
            SetRect(nameGo, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 25), new Vector2(-16, 46));
            nameText = AddText(nameGo, "Name", 28, TextAlignmentOptions.Center, Color.white, true);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 14;
            nameText.fontSizeMax = 28;
        }

        ContentEntryView view = tile.AddComponent<ContentEntryView>();
        view.icon = iconImage;
        view.nameText = nameText;
        view.button = tileButton;
        view.highlight = tileImage;
        tile.SetActive(false);
        return view;
    }

    /// <summary>Inactive list row: icon on the left, name + description on the right.</summary>
    public static ContentEntryView BuildInfoEntry(Transform parent, string name = "InfoEntryTemplate")
    {
        GameObject row = NewUI(name, parent);
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

        GameObject nameGo = NewUI("Name", column.transform);
        TextMeshProUGUI nameText = AddText(nameGo, "Name", 40, TextAlignmentOptions.TopLeft, Color.white, true);

        GameObject description = NewUI("Description", column.transform);
        TextMeshProUGUI descriptionText = AddText(description, "Description", 30, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.85f), false);

        ContentEntryView view = row.AddComponent<ContentEntryView>();
        view.icon = iconImage;
        view.nameText = nameText;
        view.descriptionText = descriptionText;
        row.SetActive(false);
        return view;
    }

    public static TextMeshProUGUI BuildTextTemplate(Transform parent, string name, string text, int size, Color color, bool outline)
    {
        GameObject go = NewUI(name, parent);
        TextMeshProUGUI t = AddText(go, text, size, TextAlignmentOptions.TopLeft, color, outline);
        go.SetActive(false);
        return t;
    }

    public static Scrollbar BuildScrollbar(Transform parent)
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

    // ===== BASICS =====

    public static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    public static RectTransform SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = Center;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(GameObject go)
    {
        return SetRect(go, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    public static RectTransform TopStretch(GameObject go)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    public static Image AddImage(GameObject go, Sprite sprite, Color color, bool raycast = true)
    {
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static TextMeshProUGUI AddText(GameObject go, string text, int size, TextAlignmentOptions alignment, Color color, bool outline)
    {
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.font = FontAsset;
        if (outline && OutlineMaterial != null)
            t.fontSharedMaterial = OutlineMaterial;
        t.text = text;
        t.fontSize = size;
        t.alignment = alignment;
        t.color = color;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, int fontSize)
    {
        GameObject go = NewUI(name, parent);
        SetRect(go, Center, Center, pos, size);
        Image image = AddImage(go, ButtonSprite, Color.white);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject text = NewUI("Text", go.transform);
        Stretch(text);
        AddText(text, label, fontSize, TextAlignmentOptions.Center, Color.white, true);
        return button;
    }
}
