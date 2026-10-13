using TMPro;
using UnityEngine;

/// <summary>One stat line in Character Select: label on the left, value on the right (e.g. "Speed   A").</summary>
public class StatRowView : MonoBehaviour
{
    public TMP_Text label;
    public TMP_Text value;

    public void Set(string labelText, string valueText)
    {
        if (label != null) label.text = labelText;
        if (value != null) value.text = valueText;
    }
}
