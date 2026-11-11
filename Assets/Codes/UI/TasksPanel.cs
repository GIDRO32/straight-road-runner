using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tasks panel: every task with its progress (from the ContentDatabase, so new Task assets show up),
/// the Shard balance and the Epic Sign shop.
/// Build the UI with Tools > Straight Road Runner > Build Tasks, Shop and Save UI.
/// </summary>
public class TasksPanel : MonoBehaviour
{
    [Header("Tasks")]
    public Transform listContent;
    [Tooltip("Inactive row cloned for every task")]
    public TaskRowView rowTemplate;
    public ScrollRect scroll;

    [Header("Epic Sign Shop")]
    public Image epicSignIcon;
    public TMP_Text epicSignText;
    public Button buyEpicSignButton;
    public TMP_Text buyEpicSignLabel;
    [Tooltip("{0} = owned")]
    public string epicSignFormat = "Epic Signs: {0}\n<size=70%>Restore 50 stamina. They show up in levels until you punch them.</size>";
    [Tooltip("{0} = price")]
    public string buyFormat = "Buy ({0} Shards)";
    public string notEnoughText = "Not enough Shards";

    private readonly List<TaskRowView> rows = new List<TaskRowView>();
    private readonly List<TaskDefinition> tasks = new List<TaskDefinition>();
    private bool initialized;
    private float notEnoughUntil;

    void OnEnable()
    {
        if (!initialized) Initialize();
        PlayerProgress.Changed += Refresh;
        Refresh();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    void OnDisable()
    {
        PlayerProgress.Changed -= Refresh;
    }

    void Update()
    {
        if (notEnoughUntil > 0f && Time.unscaledTime > notEnoughUntil)
        {
            notEnoughUntil = 0f;
            RefreshShop();
        }
    }

    private void Initialize()
    {
        initialized = true;
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (buyEpicSignButton != null) buyEpicSignButton.onClick.AddListener(BuyEpicSign);

        var db = ContentDatabase.Instance;
        if (db == null || rowTemplate == null || listContent == null) return;

        foreach (var task in db.Tasks)
        {
            TaskRowView row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            row.name = task.id;
            rows.Add(row);
            tasks.Add(task);
        }

        if (epicSignIcon != null && db.epicSignIcon != null) epicSignIcon.sprite = db.epicSignIcon;
    }

    public void Refresh()
    {
        for (int i = 0; i < rows.Count; i++)
            rows[i].Set(tasks[i]);
        RefreshShop();
    }

    private void RefreshShop()
    {
        if (epicSignText != null) epicSignText.text = string.Format(epicSignFormat, PlayerProgress.EpicSigns);
        if (buyEpicSignLabel != null && notEnoughUntil <= 0f)
            buyEpicSignLabel.text = string.Format(buyFormat, PlayerProgress.EpicSignPrice);
    }

    public void BuyEpicSign()
    {
        if (PlayerProgress.TryBuyEpicSign()) return; // Changed → Refresh

        if (buyEpicSignLabel != null) buyEpicSignLabel.text = notEnoughText;
        notEnoughUntil = Time.unscaledTime + 1.5f;
    }
}
