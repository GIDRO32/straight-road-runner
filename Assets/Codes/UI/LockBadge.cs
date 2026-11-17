using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lock overlay on a grid tile of locked content: darkens the tile and shows the lock and the price in Shards.
/// Added at runtime by the Character / Stage Select panels.
/// </summary>
public class LockBadge : MonoBehaviour
{
    private GameObject overlay;
    private TMP_Text costText;

    /// <summary>Gets (or creates) the badge of a tile.</summary>
    public static LockBadge For(Component tile, TMP_FontAsset font)
    {
        LockBadge badge = tile.GetComponent<LockBadge>();
        if (badge == null)
        {
            badge = tile.gameObject.AddComponent<LockBadge>();
            badge.Build(font);
        }
        return badge;
    }

    public void Show(bool locked, int cost)
    {
        if (overlay == null) return;
        overlay.SetActive(locked);
        if (costText != null) costText.text = cost.ToString();
        overlay.transform.SetAsLastSibling();
    }

    private void Build(TMP_FontAsset font)
    {
        var db = ContentDatabase.Instance;

        overlay = Child("Lock", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image dim = overlay.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = false;

        GameObject lockIcon = Child("LockIcon", overlay.transform, new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.9f), Vector2.zero, Vector2.zero);
        Image lockImage = lockIcon.AddComponent<Image>();
        lockImage.sprite = db != null ? db.lockIcon : null;
        lockImage.preserveAspect = true;
        lockImage.raycastTarget = false;
        lockImage.enabled = lockImage.sprite != null;

        // Price: number + shard icon along the bottom
        GameObject cost = Child("Cost", overlay.transform, new Vector2(0f, 0f), new Vector2(0.62f, 0.3f), Vector2.zero, Vector2.zero);
        costText = cost.AddComponent<TextMeshProUGUI>();
        if (font != null) costText.font = font;
        costText.enableAutoSizing = true;
        costText.fontSizeMin = 12;
        costText.fontSizeMax = 40;
        costText.alignment = TextAlignmentOptions.MidlineRight;
        costText.color = Color.white;
        costText.raycastTarget = false;

        GameObject shard = Child("Shard", overlay.transform, new Vector2(0.64f, 0.02f), new Vector2(0.9f, 0.28f), Vector2.zero, Vector2.zero);
        Image shardImage = shard.AddComponent<Image>();
        shardImage.sprite = db != null ? db.shardIcon : null;
        shardImage.preserveAspect = true;
        shardImage.raycastTarget = false;
        shardImage.enabled = shardImage.sprite != null;
    }

    private static GameObject Child(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        go.layer = parent.gameObject.layer;
        return go;
    }
}
