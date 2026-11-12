using UnityEngine;
using UnityEngine.UI;
using LuckyNumber.Audio;

namespace LuckyNumber.UI
{
    public sealed class LocalTestDisclosure : MonoBehaviour
    {
        [SerializeField] private Button toggle;
        [SerializeField] private GameObject controls;

        private void OnEnable()
        {
            controls.SetActive(false);
            toggle.onClick.AddListener(Toggle);
        }

        private void OnDisable() => toggle.onClick.RemoveListener(Toggle);

        private void Toggle()
        {
            AudioManager.Instance?.PlayClick();
            controls.SetActive(!controls.activeSelf);
        }
    }
}
