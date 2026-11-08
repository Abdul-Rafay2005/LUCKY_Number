using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LuckyNumber.Audio;

namespace LuckyNumber.UI
{
    public class SettingsModal : MonoBehaviour
    {
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = AudioListener.volume;
                masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }

            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
        }

        private void OnDisable()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnClose);
        }

        private void OnVolumeChanged(float val)
        {
            AudioListener.volume = val;
        }

        private void OnClose()
        {
            AudioManager.Instance?.PlayClick();
            gameObject.SetActive(false);
        }
    }
}

