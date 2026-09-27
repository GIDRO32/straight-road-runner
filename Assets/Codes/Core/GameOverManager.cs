using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public Text finalScoreText;
    
    [Header("Camera Bounds")]
    public Camera mainCamera;
    public float rightBoundaryOffset = 2f; // How far right of camera before game over
    
    private Transform player;
    private bool isGameOver = false;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
            
        if (mainCamera == null)
            mainCamera = Camera.main;
    }
    
    void Update()
    {
        if (isGameOver || player == null) return;
        
        CheckPlayerBounds();
    }
    
    public void RegisterPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }
    
    private void CheckPlayerBounds()
    {
        if (mainCamera == null || player == null) return;
        
        // Get camera right edge in world space
        float cameraHeight = mainCamera.orthographicSize;
        float cameraWidth = cameraHeight * mainCamera.aspect;
        float cameraRightEdge = mainCamera.transform.position.x + cameraWidth;
        
        // Check if player is too far right (fallen behind)
        if (player.position.x > cameraRightEdge - rightBoundaryOffset)
        {
            TriggerGameOver();
        }
    }
    
    public void TriggerGameOver()
    {
        if (isGameOver) return;
        
        isGameOver = true;
        Time.timeScale = 0f; // Pause game
        
        // Show game over panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            
            // Display final score
            if (finalScoreText != null && ScoreManager.Instance != null)
            {
                int finalScore = ScoreManager.Instance.GetCurrentScore();
                finalScoreText.text = $"Final Score:\n{finalScore:N0}";
            }
        }

        SubmitRun();

        Debug.Log("Game Over!");
    }

    // Sends score + character + stage to the leaderboards
    private void SubmitRun()
    {
        RunInfo run = GameSession.CurrentRun;
        if (run == null || ScoreManager.Instance == null) return;

        int finalScore = ScoreManager.Instance.GetCurrentScore();
        GameSession.EndRun();

        if (finalScore <= 0 || LeaderboardManager.Instance == null) return;

        var result = new RunResult
        {
            score = finalScore,
            characterId = run.character != null ? run.character.id : "",
            stageId = run.stage != null ? run.stage.id : "",
            stageLeaderboardId = run.stage != null ? run.stage.LeaderboardId : "",
            durationSeconds = run.Duration,
            bossesDefeated = run.bossesDefeated
        };

        LeaderboardManager.Instance.Submit(result);
    }
    
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        GameSession.EndRun();
        SceneManager.LoadScene("MainMenu"); // Change to your menu scene name
    }
}