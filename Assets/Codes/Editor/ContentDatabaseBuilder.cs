using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Assets/Resources/ContentDatabase.asset in sync with all content definition assets in the project.
/// Create a Character/Stage/Enemy/Boss/Obstacle asset anywhere (Create > SRR > ...) and it is registered automatically.
/// </summary>
public class ContentDatabaseBuilder : AssetPostprocessor
{
    private const string DatabasePath = "Assets/Resources/ContentDatabase.asset";
    private static bool rebuildQueued;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool relevant =
            imported.Concat(moved).Any(IsContentAsset) ||
            deleted.Concat(movedFrom).Any(p => p.EndsWith(".asset") && p != DatabasePath);

        if (relevant && !rebuildQueued)
        {
            rebuildQueued = true;
            // Wait until the import has finished before touching other assets
            EditorApplication.delayCall += () =>
            {
                rebuildQueued = false;
                Rebuild(false);
            };
        }
    }

    private static bool IsContentAsset(string path)
    {
        if (!path.EndsWith(".asset") || path == DatabasePath) return false;
        var type = AssetDatabase.GetMainAssetTypeAtPath(path);
        return type != null && typeof(ContentDefinition).IsAssignableFrom(type);
    }

    [MenuItem("Tools/Straight Road Runner/Rebuild Content Database")]
    private static void RebuildFromMenu()
    {
        var db = Rebuild(true);
        if (db != null)
        {
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }
    }

    public static ContentDatabase Rebuild(bool verbose)
    {
        var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
        if (db == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            db = ScriptableObject.CreateInstance<ContentDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }

        bool changed = false;

        var allDefinitions = FindAll<ContentDefinition>();
        foreach (var def in allDefinitions)
            changed |= FixId(def);

        changed |= Assign(ref db.characters, FindAll<CharacterDefinition>());
        changed |= Assign(ref db.stages, FindAll<StageDefinition>());
        changed |= Assign(ref db.enemies, FindAll<EnemyDefinition>().Where(e => !(e is BossDefinition)).ToList());
        changed |= Assign(ref db.bosses, FindAll<BossDefinition>());
        changed |= Assign(ref db.obstacles, FindAll<ObstacleDefinition>());

        if (db.defaultCharacter == null && db.characters.Count > 0)
        {
            db.defaultCharacter = db.characters[0];
            changed = true;
        }
        if (db.defaultStage == null)
        {
            db.defaultStage = db.stages.FirstOrDefault(s => !(s is ChaosStageDefinition));
            changed |= db.defaultStage != null;
        }

        if (changed)
        {
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        int warnings = Validate(db);

        if (verbose)
        {
            Debug.Log($"ContentDatabase rebuilt: {db.characters.Count} characters, {db.stages.Count} stages, " +
                      $"{db.enemies.Count} enemies, {db.bosses.Count} bosses, {db.obstacles.Count} obstacles. " +
                      $"{warnings} warning(s).", db);
        }

        return db;
    }

    private static List<T> FindAll<T>() where T : ContentDefinition
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(a => a != null)
            .OrderBy(a => a.sortOrder)
            .ThenBy(a => a.name)
            .ToList();
    }

    private static bool Assign<T>(ref List<T> target, List<T> found) where T : Object
    {
        if (target != null && target.SequenceEqual(found)) return false;
        target = found;
        return true;
    }

    // Fills empty ids and replaces ids left over from the "Create" menu default name (e.g. "new_character")
    private static bool FixId(ContentDefinition def)
    {
        string fromName = ContentDefinition.MakeId(def.name);
        bool isDefault = string.IsNullOrEmpty(def.id) || (def.id.StartsWith("new_") && def.id != fromName);
        if (!isDefault) return false;

        def.id = fromName;
        EditorUtility.SetDirty(def);
        return true;
    }

    private static int Validate(ContentDatabase db)
    {
        int warnings = 0;
        void Warn(string msg, Object context)
        {
            warnings++;
            Debug.LogWarning($"[ContentDatabase] {msg}", context);
        }

        var all = new List<ContentDefinition>();
        all.AddRange(db.characters);
        all.AddRange(db.stages);
        all.AddRange(db.enemies);
        all.AddRange(db.bosses);
        all.AddRange(db.obstacles);

        foreach (var group in all.Where(d => d != null).GroupBy(d => d.id).Where(g => g.Count() > 1))
            Warn($"Duplicate id '{group.Key}' used by: {string.Join(", ", group.Select(d => d.name))}", group.First());

        var buildScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => Path.GetFileNameWithoutExtension(s.path))
            .ToList();
        var buildSceneSet = new HashSet<string>(buildScenes);

        foreach (var c in db.characters)
            if (c.prefab == null) Warn($"Character '{c.name}' has no prefab.", c);

        foreach (var s in db.stages)
        {
            if (string.IsNullOrEmpty(s.sceneName))
                Warn($"Stage '{s.name}' has no scene name.", s);
            else if (!buildSceneSet.Contains(s.sceneName))
                Warn($"Stage '{s.name}' uses scene '{s.sceneName}' which is not in Build Settings.", s);

            if (!(s is ChaosStageDefinition) && (s.enemies == null || s.enemies.Length == 0))
                Warn($"Stage '{s.name}' has no enemies; StageControl will use its fallback enemyPrefab.", s);
        }

        foreach (var e in db.enemies.Concat(db.bosses))
        {
            if (e.prefab == null)
                Warn($"Enemy '{e.name}' has no prefab.", e);
            else if (e is BossDefinition ? e.prefab.GetComponent<BossBase>() == null : e.prefab.GetComponent<EnemyBase>() == null)
                Warn($"Prefab of '{e.name}' is missing a {(e is BossDefinition ? "BossBase" : "EnemyBase")} component.", e);

            if (e.spawnWeight <= 0f)
                Warn($"Enemy '{e.name}' has spawn weight 0 and will never spawn.", e);
        }

        foreach (var o in db.obstacles)
            if (o.prefab == null) Warn($"Obstacle '{o.name}' has no prefab.", o);

        return warnings;
    }
}
