using UnityEngine;
using UnityEngine.UI;

public class Pagination : MonoBehaviour
{
    [Header("Pages")]
    [Tooltip("Array of page GameObjects - only one will be active at a time")]
    public GameObject[] pages;
    
    [Header("Navigation Buttons")]
    public Button previousButton;
    public Button nextButton;
    
    [Header("Settings")]
    [Tooltip("Starting page index (0-based)")]
    public int startingPage = 0;
    
    [Header("Optional - Button Behavior")]
    public bool loopAround = false; // If true, goes from last page to first page
    public bool hideButtonsAtEnds = true; // If true, hides Previous on first page and Next on last page
    
    [Header("Current State (Read-Only)")]
    [SerializeField] private int currentPageIndex = 0;
    
    void Start()
    {
        // Validate pages array
        if (pages == null || pages.Length == 0)
        {
            Debug.LogError("Pagination: No pages assigned!");
            return;
        }
        
        // Clamp starting page
        currentPageIndex = Mathf.Clamp(startingPage, 0, pages.Length - 1);
        
        // Setup button listeners
        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousPage);
        
        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);
        
        // Show initial page
        ShowPage(currentPageIndex);
    }
    
    /// <summary>
    /// Go to the previous page
    /// </summary>
    public void PreviousPage()
    {
        if (currentPageIndex > 0)
        {
            ShowPage(currentPageIndex - 1);
        }
        else if (loopAround)
        {
            // Loop to last page
            ShowPage(pages.Length - 1);
        }
    }
    
    /// <summary>
    /// Go to the next page
    /// </summary>
    public void NextPage()
    {
        if (currentPageIndex < pages.Length - 1)
        {
            ShowPage(currentPageIndex + 1);
        }
        else if (loopAround)
        {
            // Loop to first page
            ShowPage(0);
        }
    }
    
    /// <summary>
    /// Show a specific page by index
    /// </summary>
    public void ShowPage(int pageIndex)
    {
        // Validate index
        if (pageIndex < 0 || pageIndex >= pages.Length)
        {
            Debug.LogWarning($"Pagination: Invalid page index {pageIndex}");
            return;
        }
        
        // Deactivate all pages
        foreach (GameObject page in pages)
        {
            if (page != null)
                page.SetActive(false);
        }
        
        // Activate selected page
        if (pages[pageIndex] != null)
        {
            pages[pageIndex].SetActive(true);
            currentPageIndex = pageIndex;
            
            Debug.Log($"Showing page {currentPageIndex + 1}/{pages.Length}");
        }
        
        // Update navigation buttons
        UpdateNavigationButtons();
        
    }
    
    /// <summary>
    /// Update the state of navigation buttons
    /// </summary>
    private void UpdateNavigationButtons()
    {
        if (hideButtonsAtEnds && !loopAround)
        {
            // Hide/show previous button
            if (previousButton != null)
            {
                previousButton.gameObject.SetActive(currentPageIndex > 0);
            }
            
            // Hide/show next button
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(currentPageIndex < pages.Length - 1);
            }
        }
        else if (!loopAround)
        {
            // Just disable buttons instead of hiding
            if (previousButton != null)
            {
                previousButton.interactable = currentPageIndex > 0;
            }
            
            if (nextButton != null)
            {
                nextButton.interactable = currentPageIndex < pages.Length - 1;
            }
        }
        else
        {
            // Loop around enabled - both buttons always active
            if (previousButton != null)
            {
                previousButton.interactable = true;
                previousButton.gameObject.SetActive(true);
            }
            
            if (nextButton != null)
            {
                nextButton.interactable = true;
                nextButton.gameObject.SetActive(true);
            }
        }
    }
    
    /// <summary>
    /// Get current page index
    /// </summary>
    public int GetCurrentPageIndex()
    {
        return currentPageIndex;
    }
    
    /// <summary>
    /// Get total number of pages
    /// </summary>
    public int GetTotalPages()
    {
        return pages.Length;
    }
    
    /// <summary>
    /// Go to first page
    /// </summary>
    public void GoToFirstPage()
    {
        ShowPage(0);
    }
    
    /// <summary>
    /// Go to last page
    /// </summary>
    public void GoToLastPage()
    {
        ShowPage(pages.Length - 1);
    }
}