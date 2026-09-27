// PlayerData.cs - NEW SCRIPT (attach to Player prefab)

using UnityEngine;
using UnityEngine.UI;

public class PlayerData : MonoBehaviour
{
    [Header("Player Stats")]
    public float maxStamina = 100f;
    public float currentStamina;

    [Header("UI References")]
    public Sprite uiIcon;           // Icon near health/stamina bar
    public Image staminaIconImage;  // Assign in prefab (optional)
    public Slider staminaSlider;    // Assign in prefab (optional)

    void Awake()
    {
        currentStamina = maxStamina;

        // Auto-find UI if not assigned (optional fallback)
        if (staminaSlider == null)
            staminaSlider = GameObject.FindWithTag("StaminaSlider")?.GetComponent<Slider>();
        if (staminaIconImage == null)
            staminaIconImage = GameObject.FindWithTag("StaminaIcon")?.GetComponent<Image>();
    }

    // Call this to update UI
    public void UpdateStaminaUI()
    {
        if (staminaSlider != null)
            staminaSlider.value = currentStamina;
        if (staminaIconImage != null && uiIcon != null)
            staminaIconImage.sprite = uiIcon;
    }
}