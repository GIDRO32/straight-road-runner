using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings > Load: first the player picks the save file in the system's file dialog (opens right away),
/// then types its password, and the current progress is replaced with the file's.
/// Without a file dialog (e.g. Mac/Linux builds) it lists the files in the Saves folder instead.
/// </summary>
public class LoadGamePanel : MonoBehaviour
{
    [Header("Step 1: File")]
    public Button chooseFileButton;
    public TMP_Text selectedFileText;
    public string noFileText = "No save file chosen.";

    [Header("Fallback list (no file dialog)")]
    public GameObject fileListRoot;
    public Transform fileListContent;
    [Tooltip("Inactive button cloned for every save file")]
    public Button fileButtonTemplate;
    public TMP_Text noFilesText;
    public Color fileNormalColor = Color.white;
    public Color fileSelectedColor = new Color(1f, 0.82f, 0.3f, 1f);
    public Button openFolderButton;
    public Button refreshButton;

    [Header("Step 2: Password")]
    [Tooltip("Shown once a file is chosen")]
    public GameObject passwordStep;
    public TMP_InputField passwordInput;
    public Button loadButton;

    [Header("Status")]
    public TMP_Text statusText;
    public Color errorColor = new Color(1f, 0.45f, 0.45f, 1f);
    public Color okColor = new Color(0.55f, 1f, 0.55f, 1f);

    private readonly List<Button> fileButtons = new List<Button>();
    private readonly List<string> files = new List<string>();
    private string selectedFile;
    private bool busy;

    private bool UseDialog => NativeFileDialog.IsSupported;

    void Awake()
    {
        if (fileButtonTemplate != null) fileButtonTemplate.gameObject.SetActive(false);
        if (chooseFileButton != null) chooseFileButton.onClick.AddListener(ChooseFile);
        if (loadButton != null) loadButton.onClick.AddListener(Load);
        if (openFolderButton != null) openFolderButton.onClick.AddListener(PlayerProgress.OpenSavesFolder);
        if (refreshButton != null) refreshButton.onClick.AddListener(RefreshFiles);
    }

    void OnEnable()
    {
        if (passwordInput != null) passwordInput.text = "";
        SetStatus("Loading a save replaces your current progress.", Color.white);

        if (chooseFileButton != null) chooseFileButton.gameObject.SetActive(UseDialog);
        if (selectedFileText != null) selectedFileText.gameObject.SetActive(UseDialog);
        if (fileListRoot != null) fileListRoot.SetActive(!UseDialog);
        if (openFolderButton != null) openFolderButton.gameObject.SetActive(!UseDialog);
        if (refreshButton != null) refreshButton.gameObject.SetActive(!UseDialog);

        SelectFile(null);
        if (UseDialog) StartCoroutine(ChooseFileNextFrame()); // Step 1 right away
        else RefreshFiles();
    }

    private IEnumerator ChooseFileNextFrame()
    {
        yield return null; // Let the panel appear first
        ChooseFile();
    }

    // ===== STEP 1 =====

    public void ChooseFile()
    {
        if (busy) return;
        if (NativeFileDialog.TryOpenFile("Load Game", PlayerProgress.SaveExtension, out string path))
        {
            SelectFile(path);
            SetStatus("Enter the password of this save.", Color.white);
            if (passwordInput != null) passwordInput.ActivateInputField();
        }
    }

    public void RefreshFiles()
    {
        foreach (var b in fileButtons) if (b != null) Destroy(b.gameObject);
        fileButtons.Clear();
        files.Clear();
        files.AddRange(PlayerProgress.ListSaveFiles());

        if (fileButtonTemplate != null && fileListContent != null)
        {
            foreach (string file in files)
            {
                Button button = Instantiate(fileButtonTemplate, fileListContent);
                button.gameObject.SetActive(true);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = $"{Path.GetFileNameWithoutExtension(file)}  <size=70%>{File.GetLastWriteTime(file):g}</size>";
                string captured = file;
                button.onClick.AddListener(() => SelectFile(captured));
                fileButtons.Add(button);
            }
        }

        if (noFilesText != null) noFilesText.gameObject.SetActive(files.Count == 0);
        SelectFile(files.Count > 0 ? files[0] : null);
    }

    private void SelectFile(string file)
    {
        selectedFile = file;
        bool chosen = !string.IsNullOrEmpty(file);

        if (selectedFileText != null) selectedFileText.text = chosen ? file : noFileText;
        if (passwordStep != null) passwordStep.SetActive(chosen);

        for (int i = 0; i < fileButtons.Count; i++)
            if (fileButtons[i].targetGraphic != null)
                fileButtons[i].targetGraphic.color = files[i] == file ? fileSelectedColor : fileNormalColor;
    }

    // ===== STEP 2 =====

    public void Load()
    {
        if (busy) return;
        if (string.IsNullOrEmpty(selectedFile) || !File.Exists(selectedFile))
        {
            SetStatus("Choose a save file first.", errorColor);
            return;
        }
        StartCoroutine(LoadRoutine(selectedFile, passwordInput != null ? passwordInput.text : ""));
    }

    private IEnumerator LoadRoutine(string file, string password)
    {
        busy = true;
        SetStatus("Decrypting...", Color.white);
        yield return null;

        try
        {
            PlayerProgress.ImportSave(file, password);
            GameSession.RefreshSelection(); // In case the selected character / stage is locked in this save
            if (passwordInput != null) passwordInput.text = "";
            SetStatus($"Loaded! Shards: {PlayerProgress.Shards}, Epic Signs: {PlayerProgress.EpicSigns}", okColor);
        }
        catch (SaveCrypto.WrongPasswordException)
        {
            SetStatus("Wrong password, or the file was changed.", errorColor);
        }
        catch (System.Exception e)
        {
            SetStatus("Couldn't load: " + e.Message, errorColor);
        }
        busy = false;
    }

    private void SetStatus(string text, Color color)
    {
        if (statusText == null) return;
        statusText.text = text;
        statusText.color = color;
    }
}
