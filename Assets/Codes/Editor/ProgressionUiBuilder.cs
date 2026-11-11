using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static MenuUiKit;

/// <summary>
/// Builds the Tasks / Shop / Save UI inside the open MainMenu scene:
/// Tools > Straight Road Runner > Build Tasks, Shop and Save UI.
/// - "How to play?" in the main menu becomes "Tasks" (opens the Tasks panel with the Epic Sign shop).
/// - Settings gets the tutorial button ("How to play?"), Save and Load.
/// - Save Game / Load Game dialogs, and a Shard counter on the main menu, Stage Select and Character Select.
/// Safe to run again: it replaces what it built before (Ctrl+Z works too).
/// </summary>
public static class ProgressionUiBuilder
{
    private const string Title = "Build Tasks, Shop and Save UI";
    private const string ShardPath = "Assets/Sprites/UI/Shard.png";
    private const string LockPath = "Assets/Sprites/UI/Lock.png";
    private const string SignPrefabPath = "Assets/Prefabs/Obstacles/Sign.prefab";
    private static readonly Color EpicPurple = new Color(0.85f, 0.6f, 1f, 1f);
    private static readonly Color InputTextColor = new Color(0.12f, 0.12f, 0.2f, 1f);

    [MenuItem("Tools/Straight Road Runner/Build Tasks, Shop and Save UI")]
    private static void Build()
    {
        if (!TryGetMenu(Title, out MenuRefs refs)) return;
        if (refs.settingsPanel == null)
        {
            EditorUtility.DisplayDialog(Title, "Couldn't find Canvas/PanelCollection/Settings.", "OK");
            return;
        }

        Undo.SetCurrentGroupName(Title);
        int undoGroup = Undo.GetCurrentGroup();

        ReadStyle(refs.canvas);
        Sprite shard = AssetDatabase.LoadAssetAtPath<Sprite>(ShardPath);
        Sprite epicSign = FindEpicSignSprite();
        FillDatabaseIcons(shard, epicSign);

        // Remove what an earlier run built
        foreach (string name in new[] { "Tasks", "SaveGame", "LoadGame" })
            DestroyIfExists(refs.panelCollection.Find(name));
        DestroyIfExists(refs.mainMenu.Find("ShardCounter"));

        // Panels (added last, so they're drawn over Settings)
        GameObject tasks = BuildTasksPanel(refs, shard, epicSign, out PanelManager tasksManager);
        GameObject save = BuildSavePanel(refs, out PanelManager saveManager);
        GameObject load = BuildLoadPanel(refs, out PanelManager loadManager);
        Undo.RegisterCreatedObjectUndo(tasks, "Create Tasks Panel");
        Undo.RegisterCreatedObjectUndo(save, "Create Save Panel");
        Undo.RegisterCreatedObjectUndo(load, "Create Load Panel");

        string tutorialLabel = ConvertTutorialButton(refs, tasksManager);
        BuildSettingsButtons(refs, tutorialLabel, saveManager, loadManager);

        // Shard counters
        GameObject menuCounter = BuildShardCounter(refs.mainMenu, new Vector2(800, 455), shard);
        Undo.RegisterCreatedObjectUndo(menuCounter, "Create Shard Counter");
        foreach (string panel in new[] { "StageSelect/Frame", "CharacterSelect/Frame" })
        {
            Transform frame = refs.panelCollection.Find(panel);
            if (frame == null) continue;
            DestroyIfExists(frame.Find("ShardCounter"));
            Undo.RegisterCreatedObjectUndo(BuildShardCounter(frame, new Vector2(-620, 370), shard), "Create Shard Counter");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = tasks;
        Debug.Log("Tasks, shop and save UI built. Save the scene (Ctrl+S) to keep it.", tasks);
    }

    // ===== TASKS + SHOP =====

    private static GameObject BuildTasksPanel(MenuRefs refs, Sprite shard, Sprite epicSign, out PanelManager panelManager)
    {
        GameObject root = CreatePanelRoot("Tasks", refs, out panelManager);
        TasksPanel panel = root.AddComponent<TasksPanel>();
        GameObject frame = CreateFrame(root, "Tasks", refs.settingsPanel, panelManager);

        BuildShardCounter(frame.transform, new Vector2(-620, 370), shard);

        // Task list
        GameObject list = NewUI("TaskList", frame.transform);
        SetRect(list, Center, Center, new Vector2(0, 40), new Vector2(1400, 560));
        AddImage(list, null, DarkPanel);
        RectTransform content = CreateScrollView(list, true, out ScrollRect scroll);
        MakeVerticalList(content, 18);
        panel.listContent = content;
        panel.scroll = scroll;
        panel.rowTemplate = BuildTaskRow(content, shard);

        // Epic Sign shop
        GameObject shop = NewUI("EpicSignShop", frame.transform);
        SetRect(shop, Center, Center, new Vector2(0, -330), new Vector2(1400, 150));
        AddImage(shop, null, DarkPanel);

        GameObject icon = NewUI("EpicSignIcon", shop.transform);
        SetRect(icon, Center, Center, new Vector2(-620, 0), new Vector2(120, 120));
        panel.epicSignIcon = AddImage(icon, epicSign, EpicPurple, raycast: false);
        panel.epicSignIcon.preserveAspect = true;

        GameObject text = NewUI("EpicSignText", shop.transform);
        SetRect(text, Center, Center, new Vector2(-130, 0), new Vector2(820, 140));
        panel.epicSignText = AddText(text, "Epic Signs: 0", 42, TextAlignmentOptions.MidlineLeft, Color.white, true);

        panel.buyEpicSignButton = MakeButton(shop.transform, "BuyEpicSign", "Buy (3 Shards)", new Vector2(510, 0), new Vector2(340, 110), 36);
        panel.buyEpicSignLabel = panel.buyEpicSignButton.GetComponentInChildren<TMP_Text>(true);

        return root;
    }

    private static TaskRowView BuildTaskRow(Transform parent, Sprite shard)
    {
        GameObject row = NewUI("TaskRowTemplate", parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 25;
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        row.AddComponent<LayoutElement>().minHeight = 140;

        TaskRowView view = row.AddComponent<TaskRowView>();

        GameObject icon = NewUI("Icon", row.transform);
        view.icon = AddImage(icon, null, Color.white, raycast: false);
        view.icon.preserveAspect = true;
        Size(icon, 110, 110);

        // Title, description, progress bar
        GameObject column = NewUI("Texts", row.transform);
        VerticalLayoutGroup columnLayout = column.AddComponent<VerticalLayoutGroup>();
        columnLayout.spacing = 6;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;
        column.AddComponent<LayoutElement>().flexibleWidth = 1;

        view.titleText = AddText(NewUI("Title", column.transform), "Task", 42, TextAlignmentOptions.TopLeft, Color.white, true);
        view.descriptionText = AddText(NewUI("Description", column.transform), "Description", 30, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.85f), false);

        GameObject bar = NewUI("ProgressBar", column.transform);
        AddImage(bar, null, new Color(0f, 0f, 0f, 0.6f), raycast: false);
        LayoutElement barLayout = bar.AddComponent<LayoutElement>();
        barLayout.minHeight = barLayout.preferredHeight = 40;

        GameObject fill = NewUI("Fill", bar.transform);
        SetRect(fill, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
        view.progressFill = AddImage(fill, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"), Gold, raycast: false);
        view.progressFill.type = Image.Type.Filled;
        view.progressFill.fillMethod = Image.FillMethod.Horizontal;
        view.progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        view.progressFill.fillAmount = 0.4f;

        GameObject progress = NewUI("ProgressText", bar.transform);
        Stretch(progress);
        view.progressText = AddText(progress, "0 / 100", 28, TextAlignmentOptions.Center, Color.white, true);

        // Reward: "+1" and a shard
        GameObject reward = NewUI("Reward", row.transform);
        Size(reward, 160, 100);
        GameObject rewardText = NewUI("Amount", reward.transform);
        SetRect(rewardText, new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
        view.rewardText = AddText(rewardText, "+1", 46, TextAlignmentOptions.MidlineRight, Color.white, true);
        GameObject rewardIcon = NewUI("Shard", reward.transform);
        SetRect(rewardIcon, new Vector2(0.5f, 0.1f), new Vector2(1, 0.9f), Vector2.zero, Vector2.zero);
        view.rewardIcon = AddImage(rewardIcon, shard, Color.white, raycast: false);
        view.rewardIcon.preserveAspect = true;

        row.SetActive(false);
        return view;
    }

    // ===== SAVE / LOAD =====

    private static GameObject BuildSavePanel(MenuRefs refs, out PanelManager panelManager)
    {
        GameObject root = CreatePanelRoot("SaveGame", refs, out panelManager);
        SaveGamePanel panel = root.AddComponent<SaveGamePanel>();
        GameObject frame = CreateFrame(root, "Save Game", refs.settingsPanel, panelManager, new Vector2(1200, 780));

        panel.passwordInput = MakeInput(frame.transform, "Password", "Password", new Vector2(0, 170), new Vector2(800, 100), true);
        panel.confirmInput = MakeInput(frame.transform, "RepeatPassword", "Repeat password", new Vector2(0, 50), new Vector2(800, 100), true);

        GameObject warning = NewUI("Warning", frame.transform);
        SetRect(warning, Center, Center, new Vector2(0, -75), new Vector2(1000, 110));
        panel.warningText = AddText(warning, "If you forget your password, your save data will become unrecoverable.", 36,
            TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.4f, 1f), true);

        panel.saveButton = MakeButton(frame.transform, "SaveButton", "Save As...", new Vector2(0, -200), new Vector2(420, 110), 50);
        panel.openFolderButton = MakeButton(frame.transform, "OpenFolder", "Open Folder", new Vector2(420, -200), new Vector2(300, 90), 30);

        GameObject status = NewUI("Status", frame.transform);
        SetRect(status, Center, Center, new Vector2(0, -300), new Vector2(1100, 60));
        panel.statusText = AddText(status, "", 28, TextAlignmentOptions.Center, Color.white, false);
        return root;
    }

    // Step 1: Choose File (system dialog), Step 2: password + Load.
    // The file list is only used where there's no file dialog (Mac/Linux builds).
    private static GameObject BuildLoadPanel(MenuRefs refs, out PanelManager panelManager)
    {
        GameObject root = CreatePanelRoot("LoadGame", refs, out panelManager);
        LoadGamePanel panel = root.AddComponent<LoadGamePanel>();
        GameObject frame = CreateFrame(root, "Load Game", refs.settingsPanel, panelManager, new Vector2(1200, 800));

        // Step 1
        panel.chooseFileButton = MakeButton(frame.transform, "ChooseFile", "Choose File...", new Vector2(0, 200), new Vector2(460, 110), 44);
        GameObject selected = NewUI("SelectedFile", frame.transform);
        SetRect(selected, Center, Center, new Vector2(0, 95), new Vector2(1050, 80));
        panel.selectedFileText = AddText(selected, "No save file chosen.", 28, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.85f), false);

        // Fallback list (no file dialog)
        GameObject list = NewUI("SaveFiles", frame.transform);
        SetRect(list, Center, Center, new Vector2(0, 150), new Vector2(900, 260));
        AddImage(list, null, DarkPanel);
        RectTransform content = CreateScrollView(list, true, out _);
        MakeVerticalList(content, 10);
        panel.fileListRoot = list;
        panel.fileListContent = content;

        Button template = MakeButton(content, "SaveFileTemplate", "Save file", Vector2.zero, new Vector2(0, 80), 32);
        LayoutElement templateLayout = template.gameObject.AddComponent<LayoutElement>();
        templateLayout.minHeight = templateLayout.preferredHeight = 80;
        template.gameObject.SetActive(false);
        panel.fileButtonTemplate = template;

        GameObject noFiles = NewUI("NoFiles", list.transform);
        SetRect(noFiles, Center, Center, Vector2.zero, new Vector2(840, 200));
        panel.noFilesText = AddText(noFiles, "No save files yet.\nSave one, or put .srrsave files into the Saves folder.", 32,
            TextAlignmentOptions.Center, new Color(1, 1, 1, 0.75f), false);
        panel.refreshButton = MakeButton(frame.transform, "Refresh", "Refresh", new Vector2(-420, -230), new Vector2(280, 90), 30);
        panel.openFolderButton = MakeButton(frame.transform, "OpenFolder", "Open Folder", new Vector2(420, -230), new Vector2(280, 90), 30);

        // Step 2
        GameObject step = NewUI("PasswordStep", frame.transform);
        SetRect(step, Center, Center, new Vector2(0, -110), new Vector2(900, 260));
        panel.passwordStep = step;
        panel.passwordInput = MakeInput(step.transform, "Password", "Password", new Vector2(0, 60), new Vector2(800, 100), true);
        panel.loadButton = MakeButton(step.transform, "LoadButton", "Load", new Vector2(0, -70), new Vector2(420, 110), 50);

        GameObject status = NewUI("Status", frame.transform);
        SetRect(status, Center, Center, new Vector2(0, -320), new Vector2(1050, 60));
        panel.statusText = AddText(status, "", 30, TextAlignmentOptions.Center, Color.white, false);
        return root;
    }

    private static TMP_InputField MakeInput(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size, bool password)
    {
        var resources = new TMP_DefaultControls.Resources
        {
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd")
        };
        GameObject go = TMP_DefaultControls.CreateInputField(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("UI");
        SetRect(go, Center, Center, pos, size);

        TMP_InputField field = go.GetComponent<TMP_InputField>();
        if (FontAsset != null) field.fontAsset = FontAsset;
        field.pointSize = 42;
        field.characterLimit = 64;
        field.textComponent.color = InputTextColor;
        field.textComponent.alignment = TextAlignmentOptions.Midline;
        if (field.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = placeholder;
            placeholderText.alignment = TextAlignmentOptions.Midline;
        }
        if (password)
        {
            field.contentType = TMP_InputField.ContentType.Password;
            field.asteriskChar = '*';
        }
        return field;
    }

    // ===== MAIN MENU + SETTINGS =====

    // "How to play?" → "Tasks". Returns the old label so the Settings button can use it.
    private static string ConvertTutorialButton(MenuRefs refs, PanelManager tasksManager)
    {
        Transform buttons = refs.mainMenu.Find("MenuButtons");
        Transform source = buttons != null ? (buttons.Find("Tasks") ?? buttons.Find("Tutorial")) : null;
        if (source == null)
        {
            Debug.LogWarning("Couldn't find MainMenu/MenuButtons/Tutorial; add a Tasks button yourself (Tasks panel > PanelManager.OpenPanel).");
            return "How to play?";
        }

        Button button = source.GetComponent<Button>();
        TMP_Text label = source.GetComponentInChildren<TMP_Text>(true);
        string oldLabel = label != null && source.name == "Tutorial" ? label.text : "How to play?";

        Undo.RecordObject(source.gameObject, "Rename Tasks Button");
        source.name = "Tasks";
        if (label != null)
        {
            Undo.RecordObject(label, "Rename Tasks Button");
            label.text = "Tasks";
        }

        Undo.RecordObject(button, "Rewire Tasks Button");
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (button.onClick.GetPersistentTarget(i) is PanelManager || button.onClick.GetPersistentTarget(i) == null)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, tasksManager.OpenPanel);
        EditorUtility.SetDirty(button);
        return oldLabel;
    }

    // Two rows in the Settings frame: [Upcoming Features] [How to play?] / [Save] [Load]
    private static void BuildSettingsButtons(MenuRefs refs, string tutorialLabel, PanelManager saveManager, PanelManager loadManager)
    {
        Transform group = refs.settingsPanel.Find("MainFrame/Settings");
        if (group == null) group = refs.settingsPanel.Find("MainFrame");
        if (group == null)
        {
            Debug.LogWarning("Couldn't find Settings/MainFrame; add the Tutorial, Save and Load buttons yourself.");
            return;
        }

        foreach (string name in new[] { "TutorialButton", "SaveButton", "LoadButton" })
            DestroyIfExists(group.Find(name));

        // Style source: the existing button in Settings ("Upcoming Features")
        Transform features = group.Find("Button (Legacy)");
        if (features != null)
        {
            RectTransform rect = features.GetComponent<RectTransform>();
            Undo.RecordObject(rect, "Move Upcoming Features");
            rect.anchoredPosition = new Vector2(-305, -190);
            rect.sizeDelta = new Vector2(580, 120);
        }

        Transform tutorialPanel = refs.panelCollection.Find("Tutorial");
        PanelManager tutorialManager = tutorialPanel != null ? tutorialPanel.GetComponent<PanelManager>() : null;

        SettingsButton(group, features, "TutorialButton", tutorialLabel, new Vector2(305, -190), new Vector2(580, 120), tutorialManager);
        SettingsButton(group, features, "SaveButton", "Save", new Vector2(-305, -320), new Vector2(580, 110), saveManager);
        SettingsButton(group, features, "LoadButton", "Load", new Vector2(305, -320), new Vector2(580, 110), loadManager);

        if (tutorialManager == null)
            Debug.LogWarning("Couldn't find PanelCollection/Tutorial; wire the Settings tutorial button yourself.");
    }

    private static void SettingsButton(Transform parent, Transform styleSource, string name, string label, Vector2 pos, Vector2 size, PanelManager opens)
    {
        Button button;
        if (styleSource != null && styleSource.GetComponent<Button>() != null)
        {
            GameObject copy = Object.Instantiate(styleSource.gameObject, parent, false);
            copy.name = name;
            button = copy.GetComponent<Button>();
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            TMP_Text text = copy.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
            SetRect(copy, Center, Center, pos, size);
        }
        else
        {
            button = MakeButton(parent, name, label, pos, size, 50);
        }

        if (opens != null) UnityEventTools.AddPersistentListener(button.onClick, opens.OpenPanel);
        Undo.RegisterCreatedObjectUndo(button.gameObject, "Create " + name);
    }

    // ===== SHARDS =====

    private static GameObject BuildShardCounter(Transform parent, Vector2 pos, Sprite shard)
    {
        GameObject go = NewUI("ShardCounter", parent);
        SetRect(go, Center, Center, pos, new Vector2(260, 100));
        ShardCounter counter = go.AddComponent<ShardCounter>();

        GameObject icon = NewUI("Icon", go.transform);
        SetRect(icon, new Vector2(0, 0), new Vector2(0.38f, 1), Vector2.zero, Vector2.zero);
        counter.icon = AddImage(icon, shard, Color.white, raycast: false);
        counter.icon.preserveAspect = true;

        GameObject amount = NewUI("Amount", go.transform);
        SetRect(amount, new Vector2(0.42f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        counter.amountText = AddText(amount, "0", 60, TextAlignmentOptions.MidlineLeft, Color.white, true);
        return go;
    }

    private static void FillDatabaseIcons(Sprite shard, Sprite epicSign)
    {
        var db = Resources.Load<ContentDatabase>(ContentDatabase.ResourcePath);
        if (db == null) return;
        Undo.RecordObject(db, "Set Shop Icons");
        if (db.shardIcon == null) db.shardIcon = shard;
        if (db.lockIcon == null) db.lockIcon = AssetDatabase.LoadAssetAtPath<Sprite>(LockPath);
        if (db.epicSignIcon == null) db.epicSignIcon = epicSign;
        EditorUtility.SetDirty(db);
    }

    private static Sprite FindEpicSignSprite()
    {
        var db = Resources.Load<ContentDatabase>(ContentDatabase.ResourcePath);
        if (db != null && db.epicSignIcon != null) return db.epicSignIcon;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SignPrefabPath);
        if (prefab == null) return null;
        Sign sign = prefab.GetComponentInChildren<Sign>(true);
        if (sign != null && sign.sprites != null && sign.sprites.Length > 0 && sign.sprites[0] != null) return sign.sprites[0];
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>(true);
        return sr != null ? sr.sprite : null;
    }

    // ===== HELPERS =====

    private static void Size(GameObject go, float width, float height)
    {
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = width;
        layout.minHeight = layout.preferredHeight = height;
    }

    private static void DestroyIfExists(Transform t)
    {
        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
    }
}
