using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates (once) and reuses TextMeshPro font assets and outline materials for the project's legacy fonts.
/// Assets are saved next to the source font, e.g. "MyFont SDF.asset" and "MyFont SDF Outline 00109A.mat".
/// </summary>
public static class TmpFontUtility
{
    private const string FallbackFolder = "Assets/Fonts";

    /// <summary>True when Window > TextMeshPro > Import TMP Essential Resources has been done.</summary>
    public static bool EssentialsImported =>
        Shader.Find("TextMeshPro/Mobile/Distance Field") != null && TMP_Settings.instance != null;

    public static void OpenEssentialsImporter()
    {
        EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
    }

    public static TMP_FontAsset GetFontAsset(Font font)
    {
        string fontPath = font != null ? AssetDatabase.GetAssetPath(font) : null;

        // Built-in Arial (or no font): use TMP's default font asset
        if (string.IsNullOrEmpty(fontPath) || !fontPath.StartsWith("Assets/"))
            return TMP_Settings.defaultFontAsset;

        string folder = Path.GetDirectoryName(fontPath).Replace('\\', '/');
        string assetPath = $"{folder}/{font.name} SDF.asset";

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        // Dynamic font asset: glyphs are added to the atlas as text uses them
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
        if (fontAsset == null)
        {
            Debug.LogError($"TMP: Couldn't create a font asset from '{fontPath}'. Using the default TMP font.");
            return TMP_Settings.defaultFontAsset;
        }

        fontAsset.name = $"{font.name} SDF";
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        fontAsset.material.name = $"{font.name} SDF Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        foreach (Texture2D atlas in fontAsset.atlasTextures)
        {
            if (atlas == null) continue;
            atlas.name = $"{font.name} SDF Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"TMP: Created font asset {assetPath}", fontAsset);
        return fontAsset;
    }

    private static readonly Dictionary<string, Material> outlineCache = new Dictionary<string, Material>();

    /// <summary>
    /// Material preset with an outline, the TMP equivalent of the legacy Outline component.
    /// effectDistance is the legacy Outline distance in pixels.
    /// </summary>
    public static Material GetOutlineMaterial(TMP_FontAsset fontAsset, Color color, float effectDistance)
    {
        if (fontAsset == null || fontAsset.material == null) return null;

        float width = Mathf.Round(Mathf.Clamp(effectDistance * 0.07f, 0.1f, 0.35f) * 100f) / 100f;
        string hex = ColorUtility.ToHtmlStringRGB(color);
        string key = $"{fontAsset.GetInstanceID()}_{hex}_{width}";

        if (outlineCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        string fontAssetPath = AssetDatabase.GetAssetPath(fontAsset);
        string folder = fontAssetPath.StartsWith("Assets/")
            ? Path.GetDirectoryName(fontAssetPath).Replace('\\', '/')
            : FallbackFolder;
        Directory.CreateDirectory(folder);

        string materialPath = $"{folder}/{fontAsset.name} Outline {hex} {width:0.00}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            material = new Material(fontAsset.material) { name = Path.GetFileNameWithoutExtension(materialPath) };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, color);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        outlineCache[key] = material;
        return material;
    }
}
