using UnityEngine;

/// <summary>
/// Base class for every piece of game content (characters, stages, enemies, bosses, obstacles).
/// Content is always referenced by its stable <see cref="id"/> (saves, leaderboards), never by list index.
/// </summary>
public abstract class ContentDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id used in saves and leaderboards. Auto-filled from the asset name. Don't change it after release!")]
    public string id;
    public string displayName;
    public Sprite icon;

    [Header("Listing")]
    [Tooltip("Lower numbers are listed first in menus")]
    public int sortOrder = 0;
    [Tooltip("Untick to hide this content from the demo build without deleting it")]
    public bool includeInDemo = true;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
            id = MakeId(name);
    }

    public static string MakeId(string source)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (char.IsUpper(c) && i > 0 && char.IsLower(source[i - 1]))
                sb.Append('_');

            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
                sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }
}
