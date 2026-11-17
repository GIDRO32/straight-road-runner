using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Everything the player owns. Stored encrypted, never in plain text.</summary>
[Serializable]
public class ProgressData
{
    public string game = "Straight Road Runner";
    public int version = 1;
    public int shards;
    public int epicSigns;
    [Tooltip("Ids of characters / stages bought with Shards")]
    public List<string> unlocked = new List<string>();
    public List<TaskProgress> tasks = new List<TaskProgress>();
    public string savedAt;
}

[Serializable]
public class TaskProgress
{
    public string id;
    public long progress;
    public int completions;
}

/// <summary>
/// The player's Shards, unlocks, task progress and Epic Signs.
/// - Kept on this device in an AES-256-GCM encrypted file (progress.dat), saved automatically.
/// - Settings > Save exports it to a password-protected file (Saves folder); Settings > Load imports one.
/// - Tasks: PlayerProgress.Report(type, subject, amount). Score, kills and walls are already reported.
/// </summary>
public static class PlayerProgress
{
    private const string LocalFileName = "progress.dat";
    public const string SaveExtension = ".srrsave";
    private static readonly byte[] LocalAad = Encoding.ASCII.GetBytes("SRR local progress v1");

    private static ProgressData data;
    private static bool dirty;

    /// <summary>Shards, unlocks, Epic Signs or task progress changed (menus refresh on it).</summary>
    public static event Action Changed;
    /// <summary>A task was completed: the task and the Shards it paid.</summary>
    public static event Action<TaskDefinition, int> TaskCompleted;

    private static ProgressData Data
    {
        get
        {
            if (data == null) LoadLocal();
            return data;
        }
    }

    public static int Shards => Data.shards;
    public static int EpicSigns => Data.epicSigns;
    public static string SavesFolder => Path.Combine(Application.persistentDataPath, "Saves");
    private static string LocalPath => Path.Combine(Application.persistentDataPath, LocalFileName);

    // ===== UNLOCKS =====

    public static bool IsUnlocked(ContentDefinition content)
    {
        if (content == null || content.unlockCost <= 0) return true;
        return Data.unlocked.Contains(content.id);
    }

    /// <summary>Pays the content's unlock cost. False when locked content can't be afforded.</summary>
    public static bool TryUnlock(ContentDefinition content)
    {
        if (content == null) return false;
        if (IsUnlocked(content)) return true;
        if (Data.shards < content.unlockCost) return false;

        Data.shards -= content.unlockCost;
        Data.unlocked.Add(content.id);
        Commit();
        return true;
    }

    // ===== TESTING (Tools > Straight Road Runner > Progress) =====

    public static void AddShards(int amount)
    {
        Data.shards = Mathf.Max(0, Data.shards + amount);
        Commit();
    }

    public static void ResetProgress()
    {
        data = new ProgressData();
        Commit();
    }

    // ===== EPIC SIGNS =====

    public static int EpicSignPrice
    {
        get
        {
            var db = ContentDatabase.Instance;
            return db != null ? Mathf.Max(0, db.epicSignPrice) : 3;
        }
    }

    public static bool TryBuyEpicSign()
    {
        int price = EpicSignPrice;
        if (Data.shards < price) return false;
        Data.shards -= price;
        Data.epicSigns++;
        Commit();
        return true;
    }

    /// <summary>An Epic Sign was punched: one fewer left.</summary>
    public static void UseEpicSign()
    {
        if (Data.epicSigns <= 0) return;
        Data.epicSigns--;
        Commit();
    }

    // ===== TASKS =====

    public static long GetProgress(TaskDefinition task) => Entry(task).progress;
    public static int GetCompletions(TaskDefinition task) => Entry(task).completions;

    public static bool IsFinished(TaskDefinition task) =>
        task != null && !task.repeatable && Entry(task).completions > 0;

    /// <summary>Counts toward every task of this type whose targets include the subject (null subject = no specific target).</summary>
    public static void Report(TaskType type, ContentDefinition subject = null, long amount = 1)
    {
        if (amount <= 0) return;
        var db = ContentDatabase.Instance;
        if (db == null) return;

        bool completedAny = false;
        foreach (var task in db.Tasks)
        {
            if (task == null || task.type != type || task.goal <= 0 || !task.Matches(subject)) continue;
            if (IsFinished(task)) continue;

            TaskProgress entry = Entry(task);
            entry.progress += amount;
            dirty = true;

            int earned = 0;
            while (entry.progress >= task.goal)
            {
                entry.completions++;
                earned += task.rewardShards;
                if (!task.repeatable)
                {
                    entry.progress = task.goal;
                    break;
                }
                entry.progress -= task.goal; // Counter starts again (extra progress carries over)
            }

            if (earned > 0)
            {
                Data.shards += earned;
                completedAny = true;
                TaskCompleted?.Invoke(task, earned);
            }
        }

        if (completedAny) Commit();
        else Changed?.Invoke();
    }

    private static TaskProgress Entry(TaskDefinition task)
    {
        string id = task != null ? task.id : "";
        TaskProgress entry = Data.tasks.FirstOrDefault(t => t.id == id);
        if (entry == null)
        {
            entry = new TaskProgress { id = id };
            Data.tasks.Add(entry);
        }
        return entry;
    }

    // ===== LOCAL FILE (automatic) =====

    private static void Commit()
    {
        dirty = true;
        Save();
        Changed?.Invoke();
    }

    public static void SaveIfDirty()
    {
        if (dirty) Save();
    }

    /// <summary>Writes the encrypted progress file on this device.</summary>
    public static void Save()
    {
        if (data == null) return;
        try
        {
            data.savedAt = DateTime.UtcNow.ToString("o");
            byte[] plain = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
            byte[] encrypted = SaveCrypto.EncryptWithKey(plain, LocalKey(), LocalAad);

            Directory.CreateDirectory(Application.persistentDataPath);
            string temp = LocalPath + ".tmp";
            File.WriteAllBytes(temp, encrypted);
            if (File.Exists(LocalPath)) File.Delete(LocalPath);
            File.Move(temp, LocalPath);
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"PlayerProgress: Couldn't save progress: {e.Message}");
        }
    }

    private static void LoadLocal()
    {
        data = new ProgressData();
        if (!File.Exists(LocalPath)) return;

        try
        {
            byte[] plain = SaveCrypto.DecryptWithKey(File.ReadAllBytes(LocalPath), LocalKey(), LocalAad);
            data = Parse(plain);
        }
        catch (Exception e)
        {
            // Keep the unreadable file instead of overwriting it
            string backup = LocalPath + ".unreadable";
            try { File.Copy(LocalPath, backup, true); } catch { }
            Debug.LogWarning($"PlayerProgress: Couldn't read {LocalPath} ({e.Message}). Starting fresh; the old file was copied to {backup}.");
            data = new ProgressData();
        }
    }

    // The local file is tied to this device (players can't edit it; use Save/Load to move progress)
    private static byte[] LocalKey() =>
        SaveCrypto.Sha256("Straight Road Runner local progress|" + SystemInfo.deviceUniqueIdentifier);

    private static ProgressData Parse(byte[] json)
    {
        ProgressData parsed = JsonUtility.FromJson<ProgressData>(Encoding.UTF8.GetString(json));
        if (parsed == null) throw new FormatException("Empty save.");
        parsed.shards = Mathf.Max(0, parsed.shards);
        parsed.epicSigns = Mathf.Max(0, parsed.epicSigns);
        if (parsed.unlocked == null) parsed.unlocked = new List<string>();
        if (parsed.tasks == null) parsed.tasks = new List<TaskProgress>();
        parsed.tasks.RemoveAll(t => t == null || string.IsNullOrEmpty(t.id));
        return parsed;
    }

    // ===== SAVE FILES (password) =====

    /// <summary>Suggested name for a new save file.</summary>
    public static string DefaultSaveName => $"SRR_Save_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{SaveExtension}";

    /// <summary>
    /// Encrypts the progress with the password into a file. Path = where the player chose to save it;
    /// empty = a new file in the Saves folder. Returns the path.
    /// </summary>
    public static string ExportSave(string password, string path = null)
    {
        Data.savedAt = DateTime.UtcNow.ToString("o");
        byte[] plain = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Data));
        byte[] file = SaveCrypto.EncryptWithPassword(plain, password);

        if (string.IsNullOrEmpty(path))
        {
            Directory.CreateDirectory(SavesFolder);
            path = Path.Combine(SavesFolder, DefaultSaveName);
        }
        else
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(path, file);
        return path;
    }

    /// <summary>Replaces the current progress with a save file. Throws SaveCrypto.WrongPasswordException on a wrong password.</summary>
    public static void ImportSave(string path, string password)
    {
        byte[] plain = SaveCrypto.DecryptWithPassword(File.ReadAllBytes(path), password);
        data = Parse(plain);
        Commit();
    }

    /// <summary>Save files in the Saves folder, newest first.</summary>
    public static List<string> ListSaveFiles()
    {
        if (!Directory.Exists(SavesFolder)) return new List<string>();
        return Directory.GetFiles(SavesFolder, "*" + SaveExtension)
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToList();
    }

    public static void OpenSavesFolder()
    {
        Directory.CreateDirectory(SavesFolder);
        Application.OpenURL("file://" + SavesFolder.Replace('\\', '/'));
    }
}
