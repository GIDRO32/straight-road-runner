using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Character selection: character grid on the left, artwork + name in the middle,
/// and an info wall on the right switching between Bio, Stats and Moveset.
/// Characters come from ContentDatabase, so new CharacterDefinitions show up automatically.
/// Build the UI with Tools > Straight Road Runner > Build Character Select Panel.
/// </summary>
public class CharacterSelectPanel : MonoBehaviour
{
    public enum InfoTab { Bio, Stats, Moveset }

    [System.Serializable]
    public class StatDisplay
    {
        public string label;
        public CharacterStatType stat;
        [Tooltip("Value that fills the bar completely")]
        public float maxValue = 10f;
        [Tooltip("For stats where lower is better (e.g. speed lost while punching)")]
        public bool lowerIsBetter;
    }

    [Header("Grid")]
    public Transform gridContent;
    [Tooltip("Inactive tile cloned for every character")]
    public ContentEntryView characterTileTemplate;
    public Color tileNormalColor = Color.white;
    public Color tileSelectedColor = new Color(1f, 0.82f, 0.3f, 1f);

    [Header("Middle: Artwork + Name")]
    public Image artworkImage;
    public TMP_Text characterNameText;
    [Tooltip("Show the animated Menu Art prefab (copied frame by frame into the UI). Falls back to the portrait.")]
    public bool useAnimatedMenuArt = true;

    [Header("Right: Info Wall")]
    public Button bioTabButton;
    public Button statsTabButton;
    public Button movesetTabButton;
    public Color tabActiveColor = new Color(1f, 0.82f, 0.3f, 1f);
    public Color tabInactiveColor = Color.white;
    public ScrollRect infoScroll;
    public Transform infoContent;

    [Header("Info Templates (inactive)")]
    public Image portraitTemplate;
    public TMP_Text paragraphTemplate;
    public StatRowView statRowTemplate;
    public ContentEntryView moveEntryTemplate;
    public TMP_Text emptyTextTemplate;

    [Header("Stats shown")]
    public List<StatDisplay> stats = new List<StatDisplay>
    {
        new StatDisplay { label = "Speed", stat = CharacterStatType.RunSpeed, maxValue = 10f },
        new StatDisplay { label = "Super Run", stat = CharacterStatType.SuperRunSpeed, maxValue = 25f },
        new StatDisplay { label = "Jump", stat = CharacterStatType.JumpHeight, maxValue = 15f },
        new StatDisplay { label = "Super Jump", stat = CharacterStatType.SuperJumpHeight, maxValue = 25f },
        new StatDisplay { label = "Dash", stat = CharacterStatType.DashSpeedMultiplier, maxValue = 3f },
        new StatDisplay { label = "Stamina", stat = CharacterStatType.MaxStamina, maxValue = 200f },
    };

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

    // Animated art: the Menu Art prefab runs hidden, its current sprite is copied into artworkImage
    private GameObject artInstance;
    private SpriteRenderer artRenderer;

    public CharacterDefinition Selected => selected;

    // PanelManager deactivates the panel in Awake, so set up on first open instead
    void OnEnable()
    {
        if (!initialized)
            Initialize();

        Select(GameSession.SelectedCharacter);
    }

    void OnDisable()
    {
        ClearArtInstance();
    }

    void LateUpdate()
    {
        if (artRenderer != null && artworkImage != null && artworkImage.sprite != artRenderer.sprite)
        {
            artworkImage.sprite = artRenderer.sprite;
            artworkImage.enabled = artRenderer.sprite != null;
        }
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
        if (portraitTemplate != null) portraitTemplate.gameObject.SetActive(false);
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
            tile.Set(character.DisplayName, character.icon != null ? character.icon : character.portrait);

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

        bool changed = character != selected;
        selected = character;
        GameSession.SelectedCharacter = character;

        for (int i = 0; i < tiles.Count; i++)
            tiles[i].SetHighlighted(characters[i] == character, tileNormalColor, tileSelectedColor);

        if (characterNameText != null) characterNameText.text = character.DisplayName;
        if (changed || artInstance == null) ShowArtwork(character);

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
        ClearArtInstance();
        if (artworkImage == null) return;

        if (useAnimatedMenuArt && character.menuArt != null)
        {
            // Run the menu art prefab hidden; LateUpdate copies its animated sprite into the UI
            artInstance = Instantiate(character.menuArt);
            artInstance.name = $"{character.id} (Character Select art)";
            artInstance.hideFlags = HideFlags.DontSave;

            foreach (var sr in artInstance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (artRenderer == null) artRenderer = sr;
                sr.enabled = false;
            }
            foreach (var animator in artInstance.GetComponentsInChildren<Animator>(true))
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        Sprite still = character.portrait != null ? character.portrait : character.icon;
        Sprite sprite = artRenderer != null && artRenderer.sprite != null ? artRenderer.sprite : still;
        artworkImage.sprite = sprite;
        artworkImage.enabled = sprite != null;
    }

    private void ClearArtInstance()
    {
        if (artInstance != null) Destroy(artInstance);
        artInstance = null;
        artRenderer = null;
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
        if (selected.portrait != null && portraitTemplate != null)
        {
            Image portrait = Add(portraitTemplate);
            portrait.sprite = selected.portrait;
        }

        AddText(paragraphTemplate, string.IsNullOrWhiteSpace(selected.bio) ? noBioText : selected.bio);
    }

    private void FillStats()
    {
        if (statRowTemplate == null) return;

        foreach (var display in stats)
        {
            float value = selected.stats.Get(display.stat);
            float normalized = display.maxValue > 0f ? value / display.maxValue : 0f;
            if (display.lowerIsBetter) normalized = 1f - normalized;

            StatRowView row = Add(statRowTemplate);
            row.Set(display.label, value.ToString("0.##"), normalized);
        }
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
