using TMPro;
using UnityEngine;

/// <summary>One stat line: label, bar and value. The bar fills by stretching its anchor (no sprite needed).</summary>
public class StatRowView : MonoBehaviour
{
    public TMP_Text label;
    public TMP_Text value;
    [Tooltip("Stretched child of the bar background; its right anchor = fill amount")]
    public RectTransform fill;

    public void Set(string labelText, string valueText, float normalized)
    {
        if (label != null) label.text = labelText;
        if (value != null) value.text = valueText;

        if (fill != null)
        {
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }
}
