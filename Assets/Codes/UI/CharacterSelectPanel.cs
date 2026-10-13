using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Character selection: character grid on the left, artwork + name in the middle,
/// and an info wall on the right switching between Bio, Stats and Moveset.
/// The animated Menu Art is only for the title screen (MenuControl); this panel shows selectArtwork.
/// Characters come from ContentDatabase, so new CharacterDefinitions show up automatically.
/// Build the UI with Tools > Straight Road Runner > Build Character Select Panel.
/// </summary>
public class CharacterSelectPanel : MonoBehaviour
{
    public enum InfoTab { Bio, Stats, Moveset }

    [Header("Grid")]
    public Transform gridContent;
    [Tooltip("Inactive tile cloned for every character")]
    public ContentEntryView characterTileTemplate;
    public Color tileNormalColor = Color.white;
    public Color tileSelectedColor = new Color(1f, 0.82f, 0.3f, 1f);

    [Header("Middle: Artwork + Name")]
    public Image artworkImage;
    public TMP_Text characterNameText;

    [Header("Right: Info Wall")]
    public Button bioTabButton;
    public Button statsTabButton;
    public Button movesetTabButton;
    public Color tabActiveColor = new Color(1f, 0.82f, 0.3f, 1f);
    public Color tabInactiveColor = Color.white;
    public ScrollRect infoScroll;
    public Transform infoContent;

    [Header("Info Templates (inactive)")]
    public TMP_Text paragraphTemplate;
    public StatRowView statRowTemplate;
    public ContentEntryView moveEntryTemplate;
    public TMP_Text emptyTextTemplate;

    [Header("Stat Labels")]
    public string speedLabel = "Speed";
    public string jumpLabel = "Jump";
    public string sizeLabel = "Size";
    public string weightLabel = "Weight";
    public string staminaLabel = "Stamina";

    [Header("Texts")]
    public string noBioText = "No bio yet.";
    public string noMovesText = "No moves listed yet.";
    [Tooltip("{0} = move name, {1} = input")]
    public string moveNameFormat = "{0}  <color=#FFD04D>[{1}]</color>";

    [Header("Confirm")]
    [Tooltip("Optional: closes the panel (the selection is already saved on click)")]
    public Button confirmButton;
    public PanelManager panelManager;

    private readonly List<CharacterDefinition> characters = new List<CharacterDefinition>();
    private readonly List<ContentEntryView> tiles = new List<ContentEntryView>();
    private readonly List<GameObject> infoRows = new List<GameObject>();
    private CharacterDefinition selected;
    private InfoTab currentTab = InfoTab.Bio;
    private bool initialized = false;

    public CharacterDefinition Selected => selected;

    // PanelManager deactivates the panel in Awake, so set up on first open instead
    void OnEnable()
    {
        if (!initialized)
            Initialize();

        Select(GameSession.SelectedCharacter);
    }

    private void Initialize()
    {
        initialized = true;

        if (bioTabButton != null) bioTabButton.onClick.AddListener(() => ShowTab(InfoTab.Bio));
        if (statsTabButton != null) statsTabButton.onClick.AddListener(() => ShowTab(InfoTab.Stats));
        if (movesetTabButton != null) movesetTabButton.onClick.AddListener(() => ShowTab(InfoTab.Moveset));
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);

        if (panelManager == null) panelManager = GetComponent<PanelManager>();

        HideTemplates();
        BuildGrid();
    }

    private void HideTemplates()
    {
        if (characterTileTemplate != null) characterTileTemplate.gameObject.SetActive(false);
        if (paragraphTemplate != null) paragraphTemplate.gameObject.SetActive(false);
        if (statRowTemplate != null) statRowTemplate.gameObject.SetActive(false);
        if (moveEntryTemplate != null) moveEntryTemplate.gameObject.SetActive(false);
        if (emptyTextTemplate != null) emptyTextTemplate.gameObject.SetActive(false);
    }

    // ===== GRID =====

    private void BuildGrid()
    {
        var db = ContentDatabase.Instance;
        if (db == null || gridContent == null || characterTileTemplate == null) return;

        characters.AddRange(db.Characters);

        foreach (var character in characters)
        {
            ContentEntryView tile = Instantiate(characterTileTemplate, gridContent);
            tile.gameObject.SetActive(true);
            tile.name = character.id;
            tile.Set(character.DisplayName, character.icon != null ? character.icon : character.selectArtwork);

            CharacterDefinition captured = character;
            Button button = tile.button != null ? tile.button : tile.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => Select(captured));

            tiles.Add(tile);
        }
    }

    // ===== SELECTION =====

    public void Select(CharacterDefinition character)
    {
        if (characters.Count == 0) return;
        if (character == null || !characters.Contains(character)) character = characters[0];

        selected = character;
        GameSession.SelectedCharacter = character;

        for (int i = 0; i < tiles.Count; i++)
            tiles[i].SetHighlighted(characters[i] == character, tileNormalColor, tileSelectedColor);

        if (characterNameText != null) characterNameText.text = character.DisplayName;
        ShowArtwork(character);

        ShowTab(currentTab);
    }

    public void SelectById(string characterId)
    {
        var db = ContentDatabase.Instance;
        if (db != null) Select(db.GetCharacter(characterId));
    }

    public void Confirm()
    {
        if (panelManager != null) panelManager.ClosePanel();
    }

    // ===== MIDDLE: ARTWORK =====

    private void ShowArtwork(CharacterDefinition character)
    {
        if (artworkImage == null) return;

        Sprite art = character.selectArtwork != null ? character.selectArtwork : character.icon;
        artworkImage.sprite = art;
        artworkImage.enabled = art != null;
    }

    // ===== RIGHT: INFO WALL =====

    public void ShowBio() => ShowTab(InfoTab.Bio);
    public void ShowStats() => ShowTab(InfoTab.Stats);
    public void ShowMoveset() => ShowTab(InfoTab.Moveset);

    public void ShowTab(InfoTab tab)
    {
        currentTab = tab;
        TintTab(bioTabButton, tab == InfoTab.Bio);
        TintTab(statsTabButton, tab == InfoTab.Stats);
        TintTab(movesetTabButton, tab == InfoTab.Moveset);

        ClearInfo();
        if (selected == null || infoContent == null) return;

        switch (tab)
        {
            case InfoTab.Bio: FillBio(); break;
            case InfoTab.Stats: FillStats(); break;
            case InfoTab.Moveset: FillMoveset(); break;
        }

        if (infoScroll != null)
            infoScroll.verticalNormalizedPosition = 1f; // Back to top
    }

    private void FillBio()
    {
        AddText(paragraphTemplate, string.IsNullOrWhiteSpace(selected.bio) ? noBioText : selected.bio);
    }

    private void FillStats()
    {
        if (statRowTemplate == null) return;

        CharacterProfile profile = selected.profile ?? new CharacterProfile();
        AddStat(speedLabel, profile.speed.ToString());
        AddStat(jumpLabel, profile.jump.ToString());
        AddStat(sizeLabel, profile.size.ToString());
        AddStat(weightLabel, profile.weight.ToString());
        AddStat(staminaLabel, Mathf.RoundToInt(selected.stats.maxStamina).ToString());
    }

    private void AddStat(string label, string value)
    {
        Add(statRowTemplate).Set(label, value);
    }

    private void FillMoveset()
    {
        bool any = false;
        if (selected.moveset != null && moveEntryTemplate != null)
        {
            foreach (var move in selected.moveset)
            {
                if (move == null) continue;

                string title = string.IsNullOrEmpty(move.input)
                    ? move.moveName
                    : string.Format(moveNameFormat, move.moveName, move.input);

                ContentEntryView row = Add(moveEntryTemplate);
                row.Set(title, move.icon, move.description);
                if (row.icon != null) row.icon.gameObject.SetActive(move.icon != null);
                any = true;
            }
        }

        if (!any) AddText(emptyTextTemplate, noMovesText);
    }

    private T Add<T>(T template) where T : Component
    {
        T item = Instantiate(template, infoContent);
        item.gameObject.SetActive(true);
        infoRows.Add(item.gameObject);
        return item;
    }

    private void AddText(TMP_Text template, string text)
    {
        if (template == null) return;
        Add(template).text = text;
    }

    private void ClearInfo()
    {
        foreach (var row in infoRows)
            Destroy(row);
        infoRows.Clear();
    }

    private void TintTab(Button tab, bool active)
    {
        if (tab != null && tab.targetGraphic != null)
            tab.targetGraphic.color = active ? tabActiveColor : tabInactiveColor;
    }
}
