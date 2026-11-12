using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One task in the Tasks panel: icon, title, description, progress bar and reward.</summary>
public class TaskRowView : MonoBehaviour
{
    public Image icon;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    [Tooltip("Image Type = Filled")]
    public Image progressFill;
    public TMP_Text progressText;
    public TMP_Text rewardText;
    public Image rewardIcon;

    public void Set(TaskDefinition task)
    {
        if (task == null) return;

        long progress = PlayerProgress.GetProgress(task);
        bool finished = PlayerProgress.IsFinished(task);
        int completions = PlayerProgress.GetCompletions(task);

        if (icon != null)
        {
            Sprite sprite = task.DisplayIcon;
            if (sprite == null && ContentDatabase.Instance != null) sprite = ContentDatabase.Instance.shardIcon;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
        if (titleText != null) titleText.text = task.DisplayName;
        if (descriptionText != null)
        {
            string text = task.description;
            if (completions > 0 && task.repeatable) text += $"  <color=#FFD04D>(done {completions}x)</color>";
            descriptionText.text = text;
        }
        if (progressFill != null) progressFill.fillAmount = task.goal > 0 ? Mathf.Clamp01((float)progress / task.goal) : 0f;
        if (progressText != null) progressText.text = finished ? "Done!" : $"{progress:N0} / {task.goal:N0}";
        if (rewardText != null) rewardText.text = $"+{task.rewardShards}";
        if (rewardIcon != null && ContentDatabase.Instance != null) rewardIcon.sprite = ContentDatabase.Instance.shardIcon;
    }
}
