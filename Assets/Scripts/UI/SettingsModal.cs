using System;
using System.Collections.Generic;
using System.Linq;
using LuckyNumber.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LuckyNumber.UI
{
    public class SettingsModal : MonoBehaviour
    {
        private const string BindingsKey = "UI.KeyboardOverrides";
        private const float DisplayConfirmationSeconds = 12f;
        private static readonly string[] RebindPaths = { "upArrow", "downArrow", "leftArrow", "rightArrow", "enter" };

        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider effectsVolumeSlider;
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private TMP_Text effectsValue;
        [SerializeField] private Button previewSound;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Button[] tabs;
        [SerializeField] private GameObject[] pages;
        [SerializeField] private CanvasGroup settingsContent;
        [SerializeField] private TMP_Dropdown displayMode;
        [SerializeField] private TMP_Dropdown resolution;
        [SerializeField] private GameObject confirmation;
        [SerializeField] private TMP_Text confirmationText;
        [SerializeField] private Button keepDisplay;
        [SerializeField] private Button revertDisplay;
        [SerializeField] private Button[] bindingButtons;
        [SerializeField] private Button resetBindings;
        [SerializeField] private TMP_Text controlStatus;

        private InputActionMap uiActions;
        private InputAction navigation;
        private InputAction submit;
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private readonly int[] bindingIndices = new int[RebindPaths.Length];
        private List<Vector2Int> resolutions;
        private string savedBindings;
        private float savedVolume;
        private float savedEffects;
        private int activePage;
        private FullScreenMode oldMode;
        private Vector2Int oldSize;
        private FullScreenMode pendingMode;
        private Vector2Int pendingSize;
        private float displayDeadline;

        internal static void RestoreBindings()
        {
            var actions = GetInputModule()?.move?.action?.actionMap;
            if (actions != null && PlayerPrefs.HasKey(BindingsKey))
                actions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(BindingsKey));
        }

        private static InputSystemUIInputModule GetInputModule() =>
            EventSystem.current?.GetComponent<InputSystemUIInputModule>();

        private void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].onClick.AddListener(() => SelectPage(index));
            }
            for (int i = 0; i < bindingButtons.Length; i++)
            {
                int index = i;
                bindingButtons[i].onClick.AddListener(() => BeginRebind(index));
            }

            masterVolumeSlider.onValueChanged.AddListener(PreviewMaster);
            effectsVolumeSlider.onValueChanged.AddListener(PreviewEffects);
            previewSound.onClick.AddListener(() => AudioManager.Instance?.PlayDiceRoll());
            displayMode.onValueChanged.AddListener(_ => RefreshResolutions(SelectedSize()));
            resetBindings.onClick.AddListener(ResetBindings);
            closeButton.onClick.AddListener(Save);
            discardButton.onClick.AddListener(Close);
            keepDisplay.onClick.AddListener(Commit);
            revertDisplay.onClick.AddListener(RevertDisplay);
        }

        private void OnEnable()
        {
            savedVolume = AudioListener.volume;
            savedEffects = AudioManager.EffectsVolume;
            masterVolumeSlider.SetValueWithoutNotify(savedVolume);
            effectsVolumeSlider.SetValueWithoutNotify(savedEffects);
            PreviewMaster(savedVolume);
            PreviewEffects(savedEffects);

            var module = GetInputModule();
            navigation = module?.move?.action;
            submit = module?.submit?.action;
            uiActions = navigation?.actionMap;
            savedBindings = uiActions?.SaveBindingOverridesAsJson();
            for (int i = 0; i < RebindPaths.Length; i++)
                bindingIndices[i] = FindBinding(BindingAction(i), RebindPaths[i]);
            RefreshBindingLabels();
            controlStatus.text = string.Empty;
            ResetDisplayFields();
            SelectPage(0);
        }

        private void OnDisable()
        {
            rebind?.Cancel();
            if (displayDeadline > 0f) RevertDisplay();
            AudioListener.volume = savedVolume;
            AudioManager.EffectsVolume = savedEffects;
            if (uiActions != null && savedBindings != null)
                uiActions.LoadBindingOverridesFromJson(savedBindings);
        }

        private void Update()
        {
            if (displayDeadline <= 0f) return;
            float remaining = displayDeadline - Time.unscaledTime;
            if (remaining <= 0f || Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                RevertDisplay();
                return;
            }
            UpdateConfirmationText(remaining);
        }

        private void UpdateConfirmationText(float remaining) =>
            confirmationText.text = $"Keep these display settings?\nReverting in {Mathf.CeilToInt(remaining)} seconds.";

        private void SelectPage(int index)
        {
            rebind?.Cancel();
            activePage = index;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                tabs[i].GetComponentInChildren<TMP_Text>().text = i == index ? $"[{pages[i].name}]" : pages[i].name;
            }
        }

        private void PreviewMaster(float value)
        {
            AudioListener.volume = value;
            masterValue.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private void PreviewEffects(float value)
        {
            AudioManager.EffectsVolume = value;
            effectsValue.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private FullScreenMode SelectedMode => DisplayPreferences.Modes[displayMode.value];

        private Vector2Int SelectedSize() => resolutions == null || resolutions.Count == 0
            ? new Vector2Int(Screen.width, Screen.height)
            : resolutions[resolution.value];

        private void ResetDisplayFields()
        {
            oldMode = Screen.fullScreenMode;
            oldSize = new Vector2Int(Screen.width, Screen.height);
            displayMode.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(DisplayPreferences.Modes, oldMode)));
            RefreshResolutions(oldSize);
        }

        private void RefreshResolutions(Vector2Int preferred)
        {
            bool borderless = SelectedMode == FullScreenMode.FullScreenWindow;
            resolutions = borderless ? new List<Vector2Int> { DisplayPreferences.DesktopSize } : DisplayPreferences.Resolutions(SelectedMode);
            if (!borderless && SelectedMode == oldMode && !resolutions.Contains(oldSize))
                resolutions.Add(oldSize);
            resolution.ClearOptions();
            resolution.AddOptions(resolutions.Select(size => $"{size.x} × {size.y}").ToList());
            int index = resolutions.IndexOf(preferred);
            resolution.SetValueWithoutNotify(index >= 0 ? index : resolutions.Count - 1);
            resolution.interactable = !borderless;
        }

        private void Save()
        {
            rebind?.Cancel();
            pendingMode = SelectedMode;
            pendingSize = SelectedSize();
            if (!Application.isEditor && (pendingMode != oldMode || pendingSize != oldSize))
            {
                displayDeadline = Time.unscaledTime + DisplayConfirmationSeconds;
                UpdateConfirmationText(DisplayConfirmationSeconds);
                SetConfirmation(true);
                DisplayPreferences.Apply(pendingMode, pendingSize);
                return;
            }
            Commit();
        }

        private void Commit()
        {
            if (displayDeadline > 0f)
            {
                displayDeadline = 0f;
                SetConfirmation(false);
                DisplayPreferences.Save(pendingMode, pendingSize);
            }
            savedVolume = AudioListener.volume;
            savedEffects = AudioManager.EffectsVolume;
            AudioManager.SaveVolumes();
            if (uiActions != null)
            {
                savedBindings = uiActions.SaveBindingOverridesAsJson();
                PlayerPrefs.SetString(BindingsKey, savedBindings);
            }
            PlayerPrefs.Save();
            AudioManager.Instance?.PlayClick();
            Close();
        }

        private void RevertDisplay()
        {
            displayDeadline = 0f;
            DisplayPreferences.Apply(oldMode, oldSize);
            displayMode.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(DisplayPreferences.Modes, oldMode)));
            RefreshResolutions(oldSize);
            SetConfirmation(false);
        }

        private void SetConfirmation(bool visible)
        {
            settingsContent.alpha = visible ? 0f : 1f;
            settingsContent.interactable = settingsContent.blocksRaycasts = !visible;
            confirmation.SetActive(visible);
            if (isActiveAndEnabled)
                EventSystem.current?.SetSelectedGameObject(visible ? revertDisplay.gameObject : tabs[activePage].gameObject);
        }

        private void Close() => LuckyUIManager.Instance?.ToggleSettings(false);

        private InputAction BindingAction(int index) => index == RebindPaths.Length - 1 ? submit : navigation;

        private static int FindBinding(InputAction action, string key)
        {
            if (action == null) return -1;
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].path == $"<Keyboard>/{key}") return i;
            return -1;
        }

        private void RefreshBindingLabels()
        {
            for (int i = 0; i < bindingButtons.Length; i++)
            {
                var action = BindingAction(i);
                int index = bindingIndices[i];
                bindingButtons[i].interactable = index >= 0;
                bindingButtons[i].GetComponentInChildren<TMP_Text>().text =
                    index < 0 ? "Unavailable" : action.GetBindingDisplayString(index);
            }
        }

        private void BeginRebind(int row)
        {
            rebind?.Cancel();
            var action = BindingAction(row);
            int index = bindingIndices[row];
            if (index < 0) return;
            navigation.Disable();
            submit.Disable();
            controlStatus.text = "Press a key. Escape cancels.";
            rebind = action.PerformInteractiveRebinding(index)
                .WithExpectedControlType("Button")
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithMatchingEventsBeingSuppressed()
                .OnMatchWaitForAnother(0f)
                .OnApplyBinding((_, path) =>
                {
                    if (BindingInUse(action, index, path))
                        controlStatus.text = "That key is already in use.";
                    else
                    {
                        action.ApplyBindingOverride(index, path);
                        controlStatus.text = "Binding updated. Save to keep it.";
                    }
                })
                .OnCancel(operation => FinishRebind(operation, true))
                .OnComplete(operation => FinishRebind(operation, false));
            rebind.Start();
        }

        private bool BindingInUse(InputAction action, int index, string path)
        {
            foreach (var candidate in new[] { navigation, submit })
                for (int i = 0; i < candidate.bindings.Count; i++)
                    if ((candidate != action || i != index) &&
                        string.Equals(candidate.bindings[i].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }

        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation, bool cancelled)
        {
            rebind = null;
            operation.Dispose();
            navigation.Enable();
            submit.Enable();
            if (cancelled) controlStatus.text = "Rebinding cancelled.";
            RefreshBindingLabels();
        }

        private void ResetBindings()
        {
            rebind?.Cancel();
            uiActions?.RemoveAllBindingOverrides();
            RefreshBindingLabels();
            controlStatus.text = "Defaults restored. Save to keep them.";
        }
    }
}
