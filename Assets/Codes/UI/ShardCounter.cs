using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows the player's Shard balance (icon + number). Updates by itself.</summary>
public class ShardCounter : MonoBehaviour
{
    public Image icon;
    public TMP_Text amountText;
    [Tooltip("{0} = shards")]
    public string format = "{0}";

    void OnEnable()
    {
        PlayerProgress.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        PlayerProgress.Changed -= Refresh;
    }

    public void Refresh()
    {
        if (amountText != null) amountText.text = string.Format(format, PlayerProgress.Shards);
        if (icon != null && icon.sprite == null && ContentDatabase.Instance != null)
            icon.sprite = ContentDatabase.Instance.shardIcon;
    }
}
