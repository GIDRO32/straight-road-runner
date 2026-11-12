using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put on a volume Slider (range 0-1). Shows the saved volume when it appears and saves it when moved.
/// Works in every scene, no manager references needed.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    public VolumeChannel channel = VolumeChannel.Music;

    private Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    void OnEnable()
    {
        if (slider == null) slider = GetComponent<Slider>();
        slider.SetValueWithoutNotify(VolumeSettings.Get(channel));
        VolumeSettings.Changed += OnVolumeChanged;
    }

    void OnDisable()
    {
        VolumeSettings.Changed -= OnVolumeChanged;
        VolumeSettings.Save();
    }

    private void OnSliderChanged(float value)
    {
        VolumeSettings.Set(channel, value);
    }

    // Keep in sync if the same volume is changed elsewhere
    private void OnVolumeChanged(VolumeChannel changedChannel, float value)
    {
        if (changedChannel == channel)
            slider.SetValueWithoutNotify(value);
    }
}
