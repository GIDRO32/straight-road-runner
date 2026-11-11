using UnityEditor;
using UnityEngine;

/// <summary>Testing helpers for Shards and unlocks (work in and out of Play mode).</summary>
public static class ProgressDebugMenu
{
    private const string Menu = "Tools/Straight Road Runner/Progress/";

    [MenuItem(Menu + "Add 10 Shards")]
    private static void AddShards()
    {
        PlayerProgress.AddShards(10);
        Debug.Log($"Shards: {PlayerProgress.Shards}");
    }

    [MenuItem(Menu + "Show Progress")]
    private static void Show()
    {
        Debug.Log($"Shards: {PlayerProgress.Shards}, Epic Signs: {PlayerProgress.EpicSigns}. File: {Application.persistentDataPath}");
    }

    [MenuItem(Menu + "Open Progress Folder")]
    private static void OpenFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    [MenuItem(Menu + "Reset Progress")]
    private static void ResetProgress()
    {
        if (!EditorUtility.DisplayDialog("Reset Progress",
                "Delete all Shards, unlocks, task progress and Epic Signs on this computer?", "Reset", "Cancel"))
            return;
        PlayerProgress.ResetProgress();
        Debug.Log("Progress reset.");
    }
}
