using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools > Straight Road Runner > Convert Legacy Text to TextMeshPro.
/// Replaces every legacy UI Text in all scenes and prefabs under Assets/ with TextMeshProUGUI:
/// keeps text, font (converted to a TMP font asset), size, auto-size, color, alignment, style, wrapping,
/// turns Outline/Shadow effects into TMP outline materials, and re-links every script field
/// and button that pointed at the old Text.
/// </summary>
public static class LegacyTextToTmpConverter
{
    private const string MenuPath = "Tools/Straight Road Runner/Convert Legacy Text to TextMeshPro";

    private struct Reference
    {
        public Component owner;
        public string propertyPath;
    }

    private struct TextData
    {
        public string text;
        public Font font;
        public int fontSize;
        public FontStyle fontStyle;
        public bool bestFit;
        public int minSize, maxSize;
        public TextAnchor alignment;
        public bool richText;
        public HorizontalWrapMode horizontalOverflow;
        public VerticalWrapMode verticalOverflow;
        public float lineSpacing;
        public Color color;
        public bool raycastTarget;
        public bool maskable;
        public bool hasOutline;
        public Color outlineColor;
        public float outlineDistance;
    }

    [MenuItem(MenuPath)]
    private static void Run()
    {
        if (!TmpFontUtility.EssentialsImported)
        {
            EditorUtility.DisplayDialog("Convert to TextMeshPro",
                "TextMeshPro Essential Resources are not imported yet.\n\n" +
                "The importer window will open now: click \"Import TMP Essentials\", wait for the import to finish, " +
                "then run this command again.", "OK");
            TmpFontUtility.OpenEssentialsImporter();
            return;
        }

        if (!EditorUtility.DisplayDialog("Convert to TextMeshPro",
                "This replaces every legacy UI Text in all scenes and prefabs under Assets/ with TextMeshPro, " +
                "and saves those scenes and prefabs.\n\nCommit your project first so you can review or revert the result.",
                "Convert", "Cancel"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string[] openScenePaths = Enumerable.Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i).path)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        int converted = 0;
        var log = new List<string>();

        try
        {
            // Prefabs first, so scene instances of them are already converted
            string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                string path = prefabPaths[i];
                EditorUtility.DisplayProgressBar("Convert to TextMeshPro", path, (float)i / prefabPaths.Length);

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponentsInChildren<Text>(true).Length == 0) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int count = ConvertHierarchy(new[] { root }, path, log);
                if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);

                converted += count;
                if (count > 0) log.Add($"{path}: {count}");
            }

            // Scenes
            string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();

            for (int i = 0; i < scenePaths.Length; i++)
            {
                string path = scenePaths[i];
                EditorUtility.DisplayProgressBar("Convert to TextMeshPro", path, (float)i / scenePaths.Length);

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int count = ConvertHierarchy(scene.GetRootGameObjects(), path, log);
                if (count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    log.Add($"{path}: {count}");
                }
                converted += count;
            }

            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            // Reopen what was open before
            for (int i = 0; i < openScenePaths.Length; i++)
                EditorSceneManager.OpenScene(openScenePaths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
        }

        string summary = $"Converted {converted} Text component(s) to TextMeshPro.\n\n" + string.Join("\n", log);
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Convert to TextMeshPro", summary + "\n\nDetails are in the Console.", "OK");
    }

    private static int ConvertHierarchy(GameObject[] roots, string context, List<string> log)
    {
        var texts = roots.SelectMany(r => r.GetComponentsInChildren<Text>(true)).ToList();
        if (texts.Count == 0) return 0;

        // Index every object reference in this scene/prefab that points at one of these Texts
        var textIds = new HashSet<int>(texts.Select(t => t.GetInstanceID()));
        var references = new Dictionary<int, List<Reference>>();

        foreach (Component component in roots.SelectMany(r => r.GetComponentsInChildren<Component>(true)))
        {
            if (component == null || component is Text) continue; // null = missing script

            var so = new SerializedObject(component);
            SerializedProperty prop = so.GetIterator();
            while (prop.Next(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                int id = prop.objectReferenceInstanceIDValue;
                if (id == 0 || !textIds.Contains(id)) continue;

                if (!references.TryGetValue(id, out var list))
                    references[id] = list = new List<Reference>();
                list.Add(new Reference { owner = component, propertyPath = prop.propertyPath });
            }
        }

        int count = 0;
        foreach (Text text in texts)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(text))
            {
                Debug.LogWarning($"[{context}] Skipped '{GetPath(text.transform)}': it belongs to a prefab instance; convert the prefab instead.", text);
                continue;
            }

            int oldId = text.GetInstanceID();
            TextMeshProUGUI tmp = Convert(text);
            if (tmp == null) continue;
            count++;

            if (references.TryGetValue(oldId, out var refs))
            {
                foreach (Reference r in refs)
                {
                    if (r.owner == null) continue;
                    var so = new SerializedObject(r.owner);
                    SerializedProperty prop = so.FindProperty(r.propertyPath);
                    if (prop == null) continue;

                    prop.objectReferenceValue = tmp;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    if (prop.objectReferenceValue != tmp)
                        Debug.LogWarning($"[{context}] Couldn't re-link {r.owner.GetType().Name}.{r.propertyPath} on '{GetPath(r.owner.transform)}' (field type isn't TMP_Text?).", r.owner);
                }
            }
        }

        return count;
    }

    private static TextMeshProUGUI Convert(Text text)
    {
        GameObject go = text.gameObject;

        // Outline derives from Shadow; both become a TMP outline material
        Shadow effect = go.GetComponents<Shadow>().FirstOrDefault();

        var data = new TextData
        {
            text = text.text,
            font = text.font,
            fontSize = text.fontSize,
            fontStyle = text.fontStyle,
            bestFit = text.resizeTextForBestFit,
            minSize = text.resizeTextMinSize,
            maxSize = text.resizeTextMaxSize,
            alignment = text.alignment,
            richText = text.supportRichText,
            horizontalOverflow = text.horizontalOverflow,
            verticalOverflow = text.verticalOverflow,
            lineSpacing = text.lineSpacing,
            color = text.color,
            raycastTarget = text.raycastTarget,
            maskable = text.maskable,
            hasOutline = effect != null && effect.enabled,
            outlineColor = effect != null ? effect.effectColor : Color.black,
            outlineDistance = effect != null ? Mathf.Max(Mathf.Abs(effect.effectDistance.x), Mathf.Abs(effect.effectDistance.y)) : 0f
        };

        foreach (Shadow s in go.GetComponents<Shadow>())
            Object.DestroyImmediate(s, true);
        Object.DestroyImmediate(text, true);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogError($"Couldn't add TextMeshProUGUI to '{GetPath(go.transform)}'.", go);
            return null;
        }

        TMP_FontAsset fontAsset = TmpFontUtility.GetFontAsset(data.font);
        if (fontAsset != null) tmp.font = fontAsset;
        if (data.hasOutline)
        {
            Material outline = TmpFontUtility.GetOutlineMaterial(fontAsset, data.outlineColor, data.outlineDistance);
            if (outline != null) tmp.fontSharedMaterial = outline;
        }

        tmp.text = data.text;
        tmp.fontSize = data.fontSize;
        tmp.enableAutoSizing = data.bestFit;
        tmp.fontSizeMin = data.minSize;
        tmp.fontSizeMax = data.maxSize;
        tmp.fontStyle = MapStyle(data.fontStyle);
        tmp.alignment = MapAlignment(data.alignment);
        tmp.richText = data.richText;
        tmp.enableWordWrapping = data.horizontalOverflow == HorizontalWrapMode.Wrap;
        tmp.overflowMode = data.verticalOverflow == VerticalWrapMode.Truncate ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
        tmp.lineSpacing = (data.lineSpacing - 1f) * 100f;
        tmp.color = data.color;
        tmp.raycastTarget = data.raycastTarget;
        tmp.maskable = data.maskable;

        if (go.name == "Text (Legacy)" || go.name == "Text")
            go.name = "Text (TMP)";

        EditorUtility.SetDirty(go);
        return tmp;
    }

    private static FontStyles MapStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold: return FontStyles.Bold;
            case FontStyle.Italic: return FontStyles.Italic;
            case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
            default: return FontStyles.Normal;
        }
    }

    private static TextAlignmentOptions MapAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
