using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class IntroControl : MonoBehaviour
{
    [Header("Intro Objects")]
    public GameObject introScreen;
    public TMP_Text stageNameText;
    public GameObject portal;
    public Slider portalTimerSlider;

    [Header("Settings")]
    [Tooltip("Fallback only: the title normally comes from the current StageDefinition")]
    public string stageName = "CITY RUINS";
    public float letterRevealDelay = 0.08f;
    public float blackFadeOutTime = 1.2f;
    public float portalFadeInTime = 1f;
    public float countdownTime = 3f;
    public float cameraMoveSpeed = 3f;

    private Camera mainCam;
    private Transform player;
    private CanvasGroup blackScreen;
    private SpriteRenderer portalCG;
    public TMP_Text insctructionsText;
    private float portalAlpha = 0f;
    private bool countdownStarted = false;
    public DifficultyManager difficultyManager;
    public ParticleSystem portalParticles;

    void Start()
    {
        insctructionsText.gameObject.SetActive(false);
        mainCam = Camera.main;

        StageDefinition stage = GameSession.CurrentStage;
        if (stage != null)
            stageName = stage.IntroTitle;
        blackScreen = introScreen.GetComponent<CanvasGroup>();
        if (blackScreen == null) blackScreen = introScreen.AddComponent<CanvasGroup>();

        portalCG = portal.GetComponent<SpriteRenderer>();
        if (portalCG == null) portalCG = portal.AddComponent<SpriteRenderer>();

        portal.SetActive(false);
        portalTimerSlider.gameObject.SetActive(false);
        blackScreen.alpha = 1f;
        portalCG.color = new Color(1f, 1f, 1f, portalAlpha);

        Time.timeScale = 10f; // Match your start condition
        mainCam.transform.position = new Vector3(mainCam.transform.position.x, 10f, mainCam.transform.position.z);

        StartCoroutine(IntroSequence());
    }

    IEnumerator IntroSequence()
    {
        // 1. Stage name letter by letter
        stageNameText.text = "";
        foreach (char c in stageName)
        {
            stageNameText.text += c;
            yield return new WaitForSecondsRealtime(letterRevealDelay);
        }
        yield return new WaitForSecondsRealtime(0.5f);

        // 2. Reset time scale
        Time.timeScale = 1f;

        // 3. Fade out black screen
        float t = 0;
        while (t < blackFadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            blackScreen.alpha = Mathf.Lerp(1f, 0f, t / blackFadeOutTime);
            yield return null;
        }
        blackScreen.alpha = 0f;
        blackScreen.gameObject.SetActive(false);

        // 4. Portal fade in
        portal.SetActive(true);

               t = 0;
        while (t < portalFadeInTime)
        {
            t += Time.unscaledDeltaTime;
            portalCG.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, t / portalFadeInTime));
            yield return null;
        }
        portalCG.color = new Color(1f, 1f, 1f, 1f);

        // 5. Start countdown
        countdownStarted = true;
        portalTimerSlider.gameObject.SetActive(true);
        insctructionsText.gameObject.SetActive(true);
        portalTimerSlider.maxValue = countdownTime;
        portalTimerSlider.value = countdownTime;

        float timer = countdownTime;
        while (timer > 0f)
        {
            timer -= Time.unscaledDeltaTime;
            portalTimerSlider.value = timer;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                timer = 0f;
            }
            yield return null;
        }

        // Player appears
        SpawnPlayerAndStartGame();
    }

    // IntroControl.cs - ONLY CHANGED PART (SpawnPlayerAndStartGame)

void SpawnPlayerAndStartGame()
{
    portalParticles.Play();
    CharacterDefinition character = GameSession.SelectedCharacter;
    GameObject prefabToSpawn = character != null ? character.prefab : null;
    difficultyManager.isDifficultyProgressing = true;
    
    if (prefabToSpawn == null)
    {
        Debug.LogError("No character available to spawn! Check ContentDatabase has a character with a prefab.");
        return;
    }

    GameObject playerObj = Instantiate(
        prefabToSpawn,
        portal.transform.position,
        Quaternion.identity
    );
    player = playerObj.transform;

    // Apply the character's stats from its CharacterDefinition
    Movement movement = playerObj.GetComponent<Movement>();
    if (movement != null)
        movement.ApplyStats(character.stats);

    PlayerData playerData = playerObj.GetComponent<PlayerData>();
    if (playerData != null)
        playerData.ApplyDefinition(character);

    GameSession.BeginRun();

    if (GameOverManager.Instance != null)
    {
        GameOverManager.Instance.RegisterPlayer(player);
    }

    portal.SetActive(false);
    insctructionsText.gameObject.SetActive(false);
    portalTimerSlider.gameObject.SetActive(false);

    StartCoroutine(SmoothCameraToPlayer());

    StageControl stageControl = FindObjectOfType<StageControl>();
    stageControl.NotifyPlayerSpawned();

    // Bosses: added automatically so stage scenes don't need extra setup
    BossDirector bossDirector = FindObjectOfType<BossDirector>();
    if (bossDirector == null)
        bossDirector = stageControl.gameObject.AddComponent<BossDirector>();
    bossDirector.Begin();
}

    IEnumerator SmoothCameraToPlayer()
    {
        Vector3 targetPos = new Vector3(mainCam.transform.position.x, 0f, mainCam.transform.position.z);

        mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, targetPos, Time.unscaledDeltaTime * cameraMoveSpeed);
        yield return null;


        // Switch to follow player
        CameraFollow camFollow = mainCam.GetComponent<CameraFollow>();
        if (camFollow != null && player != null)
        {
            camFollow.player = player;
        }

        // Done – destroy intro
        Destroy(gameObject);
    }
}