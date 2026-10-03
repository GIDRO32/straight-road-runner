using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Displays one piece of content (stage tile, enemy row, obstacle row...): icon, name and optional description.
/// Used as a template that menus clone for every entry.
/// </summary>
public class ContentEntryView : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    [Tooltip("Optional: Button on this entry (grid tiles)")]
    public Button button;
    [Tooltip("Optional: Graphic tinted when the entry is selected")]
    public Graphic highlight;

    public void Set(string displayName, Sprite sprite, string description = null)
    {
        if (nameText != null)
            nameText.text = displayName;

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (descriptionText != null)
        {
            descriptionText.text = description ?? "";
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
        }
    }

    public void SetHighlighted(bool on, Color normal, Color selected)
    {
        if (highlight != null)
            highlight.color = on ? selected : normal;
    }
}
