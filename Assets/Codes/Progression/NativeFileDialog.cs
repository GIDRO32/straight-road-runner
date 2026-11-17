using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// "Save as" / "Open" file dialogs of the operating system.
/// - In the Editor: Unity's own file panels.
/// - Windows builds: the standard Windows dialog (comdlg32), no plugin needed.
/// - Other platforms: not supported (IsSupported = false); the save panels fall back to the Saves folder.
/// The last folder used is remembered.
/// </summary>
public static class NativeFileDialog
{
    private const string LastFolderKey = "SaveFileDialogFolder";

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    public static bool IsSupported => true;
#else
    public static bool IsSupported => false;
#endif

    public static string LastFolder
    {
        get
        {
            string folder = PlayerPrefs.GetString(LastFolderKey, "");
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) return folder;
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Directory.Exists(documents) ? documents : Application.persistentDataPath;
        }
        private set
        {
            if (string.IsNullOrEmpty(value)) return;
            PlayerPrefs.SetString(LastFolderKey, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Asks where to save and under which name. False if cancelled or not supported.</summary>
    public static bool TrySaveFile(string title, string defaultName, string extension, out string path)
    {
        path = null;
        string ext = extension.TrimStart('.');
#if UNITY_EDITOR
        path = UnityEditor.EditorUtility.SaveFilePanel(title, LastFolder, defaultName, ext);
#elif UNITY_STANDALONE_WIN
        path = WindowsDialog(title, defaultName, ext, save: true);
#endif
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase)) path += "." + ext;
        LastFolder = Path.GetDirectoryName(path);
        return true;
    }

    /// <summary>Asks for an existing file. False if cancelled or not supported.</summary>
    public static bool TryOpenFile(string title, string extension, out string path)
    {
        path = null;
        string ext = extension.TrimStart('.');
#if UNITY_EDITOR
        path = UnityEditor.EditorUtility.OpenFilePanel(title, LastFolder, ext);
#elif UNITY_STANDALONE_WIN
        path = WindowsDialog(title, null, ext, save: false);
#endif
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        LastFolder = Path.GetDirectoryName(path);
        return true;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileNameW(ref OpenFileName ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    private const int OFN_OVERWRITEPROMPT = 0x2, OFN_NOCHANGEDIR = 0x8, OFN_PATHMUSTEXIST = 0x800,
                      OFN_FILEMUSTEXIST = 0x1000, OFN_EXPLORER = 0x80000;
    private const int MaxPath = 1024;

    private static string WindowsDialog(string title, string defaultName, string ext, bool save)
    {
        IntPtr buffer = Marshal.AllocHGlobal(MaxPath * 2);
        try
        {
            // File name box: the default name (save) or empty (open)
            char[] initial = new char[MaxPath];
            if (!string.IsNullOrEmpty(defaultName))
                defaultName.CopyTo(0, initial, 0, Math.Min(defaultName.Length, MaxPath - 1));
            Marshal.Copy(initial, 0, buffer, MaxPath);

            var ofn = new OpenFileName
            {
                hwndOwner = GetActiveWindow(),
                lpstrFilter = $"Straight Road Runner save (*.{ext})\0*.{ext}\0All files (*.*)\0*.*\0\0",
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = MaxPath,
                lpstrInitialDir = LastFolder,
                lpstrTitle = title,
                lpstrDefExt = ext,
                Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_PATHMUSTEXIST |
                        (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST)
            };
            ofn.lStructSize = Marshal.SizeOf(typeof(OpenFileName));

            bool ok = save ? GetSaveFileNameW(ref ofn) : GetOpenFileNameW(ref ofn);
            return ok ? Marshal.PtrToStringUni(buffer) : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"File dialog failed: {e.Message}");
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
#endif
}
