using UnityEngine;

namespace LuckyNumber.Audio
{
    public class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const string MasterVolumeKey = "MasterVolume";
        private const string EffectsVolumeKey = "EffectsVolume";
        public static float EffectsVolume { get; set; } = 1f;

        public static AudioManager Instance { get; private set; }

        private AudioSource audioSource;
        private AudioClip clickClip;
        private AudioClip selectClip;
        private AudioClip diceRollClip;
        private AudioClip positiveClip;
        private AudioClip skipClip;
        private AudioClip abilityClip;
        private AudioClip voteClip;
        private AudioClip targetChangedClip;
        private AudioClip ejectionClip;
        private AudioClip victoryClip;
        private AudioClip defeatClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsVolumeKey, 1f));
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            clickClip = CreateChime("Button Click", 0.055f, 0f, 920f);
            selectClip = CreateChime("Die Selected", 0.09f, 0f, 1175f);
            diceRollClip = CreateDiceRoll();
            positiveClip = CreateChime("Move Accepted", 0.17f, 0.075f, 587.33f, 880f);
            skipClip = CreateChime("Turn Skipped", 0.13f, 0.065f, 440f, 329.63f);
            abilityClip = CreateChime("Ability Accepted", 0.18f, 0.055f, 293.66f, 369.99f, 293.66f);
            voteClip = CreateChime("Ballot Accepted", 0.12f, 0.047f, 659.25f, 783.99f);
            targetChangedClip = CreateChime("Target Changed", 0.17f, 0.09f, 523.25f, 698.46f, 622.25f);
            ejectionClip = CreateChime("Ejection Reveal", 0.27f, 0.12f, 392f, 261.63f, 196f);
            victoryClip = CreateChime("Victory", 0.3f, 0.105f, 523.25f, 659.25f, 783.99f, 1046.5f);
            defeatClip = CreateChime("Defeat", 0.27f, 0.11f, 493.88f, 392f, 329.63f);
        }

        public static void SaveVolumes()
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, AudioListener.volume);
            PlayerPrefs.SetFloat(EffectsVolumeKey, EffectsVolume);
        }

        public void PlayClick() => Play(clickClip, 0.26f);
        public void PlaySelect() => Play(selectClip, 0.30f);
        public void PlayDiceRoll() => Play(diceRollClip, 0.70f);
        public void PlayPositive() => Play(positiveClip, 0.44f);
        public void PlaySkip() => Play(skipClip, 0.40f);
        public void PlayAbility() => Play(abilityClip, 0.49f);
        public void PlayVote() => Play(voteClip, 0.40f);
        public void PlayTargetChanged() => Play(targetChangedClip, 0.49f);
        public void PlayEjection() => Play(ejectionClip, 0.58f);
        public void PlayVictory() => Play(victoryClip, 0.65f);
        public void PlayDefeat() => Play(defeatClip, 0.58f);

        private void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip, volume * EffectsVolume);
        }

        private static AudioClip CreateChime(string name, float noteDuration, float spacing, params float[] frequencies)
        {
            int noteSamples = Mathf.CeilToInt(noteDuration * SampleRate);
            int spacingSamples = Mathf.RoundToInt(spacing * SampleRate);
            var samples = new float[noteSamples + spacingSamples * (frequencies.Length - 1)];

            for (int note = 0; note < frequencies.Length; note++)
            {
                int offset = note * spacingSamples;
                float frequency = frequencies[note];
                for (int i = 0; i < noteSamples; i++)
                {
                    float time = i / (float)SampleRate;
                    float progress = i / (float)(noteSamples - 1);
                    float attack = Mathf.Clamp01(time / 0.004f);
                    float envelope = attack * Mathf.Exp(-3.8f * progress) * (1f - progress);
                    float phase = 2f * Mathf.PI * frequency * time;
                    float tone = 0.60f * Mathf.Sin(phase) +
                        0.24f * Mathf.Sin(phase * 2f) * Mathf.Exp(-10f * progress) +
                        0.10f * Mathf.Sin(phase * 3f) * Mathf.Exp(-14f * progress);
                    samples[offset + i] += tone * envelope;
                }
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreateDiceRoll()
        {
            float[] contactTimes = { 0f, 0.047f, 0.084f, 0.115f, 0.139f, 0.158f, 0.173f, 0.219f, 0.245f };
            const float contactDuration = 0.041f;
            int contactSamples = Mathf.CeilToInt(contactDuration * SampleRate);
            var samples = new float[Mathf.RoundToInt(contactTimes[contactTimes.Length - 1] * SampleRate) + contactSamples];
            var random = new System.Random(8071);

            for (int contact = 0; contact < contactTimes.Length; contact++)
            {
                int offset = Mathf.RoundToInt(contactTimes[contact] * SampleRate);
                float strength = Mathf.Lerp(0.94f, 0.38f, contact / (float)(contactTimes.Length - 1));
                float frequency = Mathf.Lerp(680f, 1130f, (float)random.NextDouble());
                float filteredNoise = 0f;

                for (int i = 0; i < contactSamples; i++)
                {
                    float time = i / (float)SampleRate;
                    float progress = i / (float)(contactSamples - 1);
                    float envelope = Mathf.Clamp01(time / 0.001f) * Mathf.Exp(-5.5f * progress) * (1f - progress);
                    filteredNoise = Mathf.Lerp(filteredNoise, 2f * (float)random.NextDouble() - 1f, 0.28f);
                    float knock = 0.40f * Mathf.Sin(2f * Mathf.PI * frequency * time) + 0.60f * filteredNoise;
                    samples[offset + i] += strength * envelope * knock;
                }
            }

            return CreateClip("Dice on Felt", samples);
        }

        private static AudioClip CreateClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
