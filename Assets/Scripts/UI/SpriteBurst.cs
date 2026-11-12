using UnityEngine;
using UnityEngine.UI;

namespace LuckyNumber.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class SpriteBurst : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        private Image image;
        private float started;

        private void Awake() => image = GetComponent<Image>();

        public void Play()
        {
            if (frames == null || frames.Length == 0) return;
            started = Time.unscaledTime;
            image.sprite = frames[0];
            image.enabled = true;
        }

        private void Update()
        {
            if (!image.enabled || frames == null || frames.Length == 0) return;
            int frame = Mathf.FloorToInt((Time.unscaledTime - started) * 12f);
            if (frame >= frames.Length) image.enabled = false;
            else image.sprite = frames[frame];
        }

        private void OnDisable() => image.enabled = false;
    }
}
