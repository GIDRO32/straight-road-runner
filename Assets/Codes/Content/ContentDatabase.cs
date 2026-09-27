using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Registry of all game content. Lives at Assets/Resources/ContentDatabase.asset.
/// The lists are filled automatically in the Editor (see ContentDatabaseBuilder):
/// create a definition asset anywhere and it shows up here.
/// </summary>
[CreateAssetMenu(fileName = "ContentDatabase", menuName = "SRR/Content Database")]
public class ContentDatabase : ScriptableObject
{
    public const string ResourcePath = "ContentDatabase";

    [Header("Defaults")]
    public CharacterDefinition defaultCharacter;
    public StageDefinition defaultStage;

    [Header("Registered Content (auto-filled)")]
    public List<CharacterDefinition> characters = new List<CharacterDefinition>();
    public List<StageDefinition> stages = new List<StageDefinition>();
    public List<EnemyDefinition> enemies = new List<EnemyDefinition>();
    public List<BossDefinition> bosses = new List<BossDefinition>();
    public List<ObstacleDefinition> obstacles = new List<ObstacleDefinition>();

    private static ContentDatabase instance;

    public static ContentDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<ContentDatabase>(ResourcePath);
                if (instance == null)
                    Debug.LogError($"ContentDatabase not found at Resources/{ResourcePath}. Run Tools > Straight Road Runner > Rebuild Content Database.");
            }
            return instance;
        }
    }

    // ===== LISTS FOR MENUS (demo-filtered, sorted) =====

    public IReadOnlyList<CharacterDefinition> Characters => Visible(characters);
    /// <summary>Regular stages, without Chaos Mode.</summary>
    public IReadOnlyList<StageDefinition> Stages => Visible(stages).Where(s => !(s is ChaosStageDefinition)).ToList();
    public ChaosStageDefinition ChaosStage => Visible(stages).OfType<ChaosStageDefinition>().FirstOrDefault();
    public IReadOnlyList<EnemyDefinition> Enemies => Visible(enemies);
    public IReadOnlyList<BossDefinition> Bosses => Visible(bosses);
    public IReadOnlyList<ObstacleDefinition> Obstacles => Visible(obstacles);

    // ===== LOOKUPS =====

    public CharacterDefinition GetCharacter(string id) => Find(characters, id);
    public StageDefinition GetStage(string id) => Find(stages, id);
    public EnemyDefinition GetEnemy(string id) => Find(enemies, id) ?? Find(bosses, id);
    public ObstacleDefinition GetObstacle(string id) => Find(obstacles, id);

    /// <summary>First regular stage using this scene (used when a scene is opened directly in the Editor).</summary>
    public StageDefinition GetStageForScene(string sceneName)
    {
        return stages.FirstOrDefault(s => s != null && !(s is ChaosStageDefinition) && s.sceneName == sceneName)
            ?? stages.FirstOrDefault(s => s != null && s.sceneName == sceneName);
    }

    public CharacterDefinition DefaultCharacter => defaultCharacter != null ? defaultCharacter : Characters.FirstOrDefault();
    public StageDefinition DefaultStage => defaultStage != null ? defaultStage : Stages.FirstOrDefault();

    private static T Find<T>(List<T> list, string id) where T : ContentDefinition
    {
        if (string.IsNullOrEmpty(id)) return null;
        return list.FirstOrDefault(c => c != null && c.id == id);
    }

    private static List<T> Visible<T>(List<T> list) where T : ContentDefinition
    {
        return list.Where(c => c != null && c.includeInDemo)
                   .OrderBy(c => c.sortOrder)
                   .ThenBy(c => c.DisplayName)
                   .ToList();
    }
}
