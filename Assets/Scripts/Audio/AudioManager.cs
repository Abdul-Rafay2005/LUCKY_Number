using UnityEngine;

namespace LuckyNumber.Audio
{
    /// <summary>
    /// Procedural audio generator and player for game feedback.
    /// Provides synthesized sound effects when no audio files are supplied.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource audioSource;
        private AudioClip clickClip;
        private AudioClip diceRollClip;
        private AudioClip positiveClip;
        private AudioClip negativeClip;
        private AudioClip abilityClip;
        private AudioClip alarmClip;
        private AudioClip victoryClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            GenerateProceduralClips();
        }

        private void GenerateProceduralClips()
        {
            clickClip = CreateTone(0.05f, 800f, 400f);
            diceRollClip = CreateDiceRoll(0.4f);
            positiveClip = CreateChime(0.3f, 523.25f, 659.25f, 783.99f); // C5, E5, G5
            negativeClip = CreateTone(0.35f, 300f, 150f);
            abilityClip = CreateNoiseTone(0.4f, 400f, 100f);
            alarmClip = CreateTone(0.3f, 900f, 600f);
            victoryClip = CreateChime(0.6f, 440f, 554.37f, 659.25f, 880f);
        }

        public void PlayClick() => Play(clickClip, 0.6f);
        public void PlayDiceRoll() => Play(diceRollClip, 0.8f);
        public void PlayPositive() => Play(positiveClip, 0.7f);
        public void PlayNegative() => Play(negativeClip, 0.7f);
        public void PlayAbility() => Play(abilityClip, 0.8f);
        public void PlayAlarm() => Play(alarmClip, 0.7f);
        public void PlayVictory() => Play(victoryClip, 0.9f);

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        private AudioClip CreateTone(float duration, float startFreq, float endFreq)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                float envelope = 1f - t;
                samples[i] = Mathf.Sin(2 * Mathf.PI * freq * (i / (float)sampleRate)) * envelope;
            }

            var clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateChime(float duration, params float[] freqs)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = Mathf.Exp(-3f * t);
                float sum = 0f;
                foreach (var f in freqs)
                {
                    sum += Mathf.Sin(2 * Mathf.PI * f * (i / (float)sampleRate));
                }
                samples[i] = (sum / freqs.Length) * envelope;
            }

            var clip = AudioClip.Create("Chime", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateDiceRoll(float duration)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(duration * sampleRate);
            float[] samples = new float[sampleCount];
            var rng = new System.Random();

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = (1f - t) * Mathf.Abs(Mathf.Sin(t * 30f));
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.7f;
                samples[i] = noise * envelope;
            }

            var clip = AudioClip.Create("DiceRoll", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateNoiseTone(float duration, float startFreq, float endFreq)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(duration * sampleRate);
            float[] samples = new float[sampleCount];
            var rng = new System.Random();

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                float tone = Mathf.Sin(2 * Mathf.PI * freq * (i / (float)sampleRate));
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.4f;
                float envelope = Mathf.Sin(Mathf.PI * t);
                samples[i] = (tone * 0.6f + noise * 0.4f) * envelope;
            }

            var clip = AudioClip.Create("NoiseTone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

