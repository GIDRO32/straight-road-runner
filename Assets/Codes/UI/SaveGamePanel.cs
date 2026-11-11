using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings > Save: the player picks a password (typed twice) and the progress is written to an
/// AES-256-GCM encrypted file in the Saves folder. The password isn't stored anywhere.
/// </summary>
public class SaveGamePanel : MonoBehaviour
{
    public TMP_InputField passwordInput;
    public TMP_InputField confirmInput;
    public TMP_Text warningText;
    public TMP_Text statusText;
    public Button saveButton;
    public Button openFolderButton;
    public int minPasswordLength = 4;

    [TextArea] public string warning = "If you forget your password, your save data will become unrecoverable.";
    public Color errorColor = new Color(1f, 0.45f, 0.45f, 1f);
    public Color okColor = new Color(0.55f, 1f, 0.55f, 1f);

    private bool busy;

    void Awake()
    {
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (openFolderButton != null) openFolderButton.onClick.AddListener(PlayerProgress.OpenSavesFolder);
    }

    void OnEnable()
    {
        if (passwordInput != null) passwordInput.text = "";
        if (confirmInput != null) confirmInput.text = "";
        if (warningText != null) warningText.text = warning;
        SetStatus("", Color.white);
    }

    public void Save()
    {
        if (busy) return;
        string password = passwordInput != null ? passwordInput.text : "";
        string confirm = confirmInput != null ? confirmInput.text : password;

        if (password.Length < minPasswordLength)
        {
            SetStatus($"The password needs at least {minPasswordLength} characters.", errorColor);
            return;
        }
        if (password != confirm)
        {
            SetStatus("The passwords don't match.", errorColor);
            return;
        }

        StartCoroutine(SaveRoutine(password));
    }

    private IEnumerator SaveRoutine(string password)
    {
        busy = true;
        SetStatus("Encrypting...", Color.white);
        yield return null; // Show the text before the key derivation freezes the frame for a moment

        try
        {
            string path = PlayerProgress.ExportSave(password);
            SetStatus($"Saved as {Path.GetFileName(path)}", okColor);
            if (passwordInput != null) passwordInput.text = "";
            if (confirmInput != null) confirmInput.text = "";
        }
        catch (System.Exception e)
        {
            SetStatus("Couldn't save: " + e.Message, errorColor);
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
