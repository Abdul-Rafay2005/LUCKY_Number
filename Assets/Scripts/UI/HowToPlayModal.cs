using UnityEngine;
using UnityEngine.UI;
using LuckyNumber.Audio;

namespace LuckyNumber.UI
{
    public class HowToPlayModal : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
        }

        private void OnDisable()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(OnClose);
        }

        private void OnClose()
        {
            AudioManager.Instance?.PlayClick();
            LuckyUIManager.Instance?.ToggleHowToPlay(false);
        }
    }
}

