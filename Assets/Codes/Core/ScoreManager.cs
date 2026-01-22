using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Score Settings")]
    public int enemyKillScore = 100;
    public int signHitScore = 50;
    
    [Header("UI References")]
    public Text scoreText;              // For legacy Text
    
    private int currentScore = 0;
    
    void Awake()
    {
        // Singleton pattern
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
        UpdateScoreUI();
    }
    
    public void AddScore(int amount)
    {
        currentScore += amount;
        UpdateScoreUI();
        
        Debug.Log($"Score added: +{amount}. Total: {currentScore}");
    }
    
    public void AddEnemyKillScore()
    {
        AddScore(enemyKillScore);
    }
    
    public void AddSignHitScore()
    {
        AddScore(signHitScore);
    }
    
    public int GetCurrentScore()
    {
        return currentScore;
    }
    
    public void ResetScore()
    {
        currentScore = 0;
        UpdateScoreUI();
    }
    
    private void UpdateScoreUI()
    {
        string scoreString = currentScore.ToString("N0"); // Format with commas
        
        if (scoreText != null)
            scoreText.text = $"Score: {scoreString}";
    }
}