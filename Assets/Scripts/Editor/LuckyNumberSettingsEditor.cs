using LuckyNumber.UI;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LuckyNumber.Editor
{
    public static partial class LuckyNumberSetupEditor
    {
        private static void ConfigureUiActions()
        {
            const string path = "Assets/Settings/InputSystem_Actions.inputactions";
            var module = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            var references = AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>()
                .Where(reference => reference.action?.actionMap?.name == "UI")
                .GroupBy(reference => reference.action.name)
                .ToDictionary(group => group.Key, group => group.First());
            module.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            module.move = references["Navigate"];
            module.submit = references["Submit"];
            module.cancel = references["Cancel"];
            module.point = references["Point"];
            module.leftClick = references["Click"];
            module.rightClick = references["RightClick"];
            module.middleClick = references["MiddleClick"];
            module.scrollWheel = references["ScrollWheel"];
            module.trackedDevicePosition = references["TrackedDevicePosition"];
            module.trackedDeviceOrientation = references["TrackedDeviceOrientation"];
            EditorUtility.SetDirty(module);
        }

        private static void ConfigureSettings(Transform settings, Sprite surface)
        {
            if (settings.Find("SettingsContent") != null) return;

            var controller = settings.GetComponent<SettingsModal>();
            var title = Label(settings, "SetTitle", "Settings", Gold, 48);
            var masterLabel = Label(settings, "VolTitle", "Master volume", Cream, 25);
            var master = settings.Find("VolumeSlider").GetComponent<Slider>();
            var save = settings.Find("CloseBtn").GetComponent<Button>();
            Place(settings, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1040, 735));

            var content = CreateUIElement("SettingsContent", settings);
            StretchFull(content.GetComponent<RectTransform>());
            Set(controller, "settingsContent", content.AddComponent<CanvasGroup>());
            title.transform.SetParent(content.transform, false);
            Place(title.transform, new Vector2(.5f, .91f), Vector2.zero, new Vector2(800, 70));
            var tabs = new Button[3];
            var pages = new GameObject[3];
            string[] names = { "Audio", "Display", "Controls" };
            for (int i = 0; i < names.Length; i++)
            {
                float x = .24f + i * .26f;
                tabs[i] = SettingsButton(content.transform, names[i] + "Tab", names[i], x, .79f, 235, surface);
                pages[i] = CreateUIElement(names[i], content.transform);
                StretchFull(pages[i].GetComponent<RectTransform>());
                pages[i].SetActive(i == 0);
            }
            SetArray(controller, "tabs", tabs);
            SetArray(controller, "pages", pages);

            save.transform.SetParent(content.transform, false);
            Place(save.transform, new Vector2(.68f, .10f), Vector2.zero, new Vector2(275, 52));
            Label(save.transform, "Label", "SAVE & CLOSE", Slate, 20);
            StyleButton(save, surface, true, false, false);
            Set(controller, "discardButton", SettingsButton(content.transform, "DiscardBtn", "CANCEL", .32f, .10f, 240, surface));

            var audio = pages[0].transform;
            masterLabel.transform.SetParent(audio, false);
            Place(masterLabel.transform, new Vector2(.24f, .59f), Vector2.zero, new Vector2(310, 55));
            masterLabel.alignment = TextAlignmentOptions.MidlineLeft;
            master.transform.SetParent(audio, false);
            Place(master.transform, new Vector2(.59f, .59f), Vector2.zero, new Vector2(425, 45));
            ConfigureVolumeSlider(master, surface);
            Set(controller, "masterValue", SettingText("MasterValue", audio, "100%", .86f, .59f, 100, 25, Gold));

            SettingText("EffectsTitle", audio, "Sound effects", .24f, .45f, 310, 25, Cream).alignment = TextAlignmentOptions.MidlineLeft;
            var effects = Object.Instantiate(master, audio);
            effects.name = "EffectsSlider";
            Place(effects.transform, new Vector2(.59f, .45f), Vector2.zero, new Vector2(425, 45));
            Set(controller, "effectsVolumeSlider", effects);
            Set(controller, "effectsValue", SettingText("EffectsValue", audio, "100%", .86f, .45f, 100, 25, Gold));
            Set(controller, "previewSound", SettingsButton(audio, "PreviewSound", "PLAY SAMPLE", .59f, .29f, 220, surface));

            var display = pages[1].transform;
            SettingText("ModeTitle", display, "Display mode", .24f, .59f, 310, 25, Cream).alignment = TextAlignmentOptions.MidlineLeft;
            var mode = SettingsDropdown("DisplayMode", display, .59f, surface);
            mode.AddOptions(new System.Collections.Generic.List<string> { "Windowed", "Borderless fullscreen", "Exclusive fullscreen" });
            SettingText("ResolutionTitle", display, "Resolution", .24f, .45f, 310, 25, Cream).alignment = TextAlignmentOptions.MidlineLeft;
            var resolution = SettingsDropdown("Resolution", display, .45f, surface);
            SettingText("DisplayHint", display, "Borderless uses your desktop resolution.", .5f, .30f, 850, 22, Mint);
            Set(controller, "displayMode", mode);
            Set(controller, "resolution", resolution);

            var controls = pages[2].transform;
            string[] actions = { "Navigate up", "Navigate down", "Navigate left", "Navigate right", "Activate focused control" };
            string[] alternatives = { "W", "S", "A", "D", "Numpad Enter" };
            var bindings = new Button[actions.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                float y = .67f - i * .064f;
                SettingText("Action" + i, controls, actions[i], .28f, y, 330, 22, Cream).alignment = TextAlignmentOptions.MidlineLeft;
                bindings[i] = SettingsButton(controls, "Binding" + i, "", .60f, y, 205, surface, 40);
                SettingText("Alternate" + i, controls, $"Also {alternatives[i]}", .80f, y, 185, 19, Mint);
            }
            SettingText("Mouse", controls, "Mouse  ·  Click controls  /  Scroll history and lists", .44f, .32f, 610, 20, Cream).alignment = TextAlignmentOptions.MidlineLeft;
            SettingText("Gamepad", controls, "Gamepad  ·  Stick or D-pad to move  /  A or Cross to select", .44f, .28f, 610, 20, Cream).alignment = TextAlignmentOptions.MidlineLeft;
            SettingText("CancelKey", controls, "Escape, B, or Circle dismisses open lists.", .44f, .24f, 610, 20, Cream).alignment = TextAlignmentOptions.MidlineLeft;
            SetArray(controller, "bindingButtons", bindings);
            Set(controller, "resetBindings", SettingsButton(controls, "ResetBindings", "RESET KEYS", .81f, .31f, 185, surface, 42));
            Set(controller, "controlStatus", SettingText("ControlStatus", controls, "", .5f, .18f, 820, 20, Gold));

            var confirm = CreateUIElement("DisplayConfirmation", settings);
            StretchFull(confirm.GetComponent<RectTransform>());
            SettingText("ConfirmTitle", confirm.transform, "Confirm display changes", .5f, .70f, 850, 42, Gold);
            var prompt = SettingText("ConfirmText", confirm.transform, "", .5f, .51f, 760, 30, Cream);
            prompt.rectTransform.sizeDelta = new Vector2(760, 110);
            Set(controller, "confirmationText", prompt);
            Set(controller, "keepDisplay", SettingsButton(confirm.transform, "KeepDisplay", "KEEP CHANGES", .66f, .29f, 245, surface));
            Set(controller, "revertDisplay", SettingsButton(confirm.transform, "RevertDisplay", "REVERT", .34f, .29f, 245, surface));
            Set(controller, "confirmation", confirm);
            confirm.SetActive(false);
        }

        private static TMP_Dropdown SettingsDropdown(string name, Transform parent, float y, Sprite surface)
        {
            var dropdown = CreateMatchDropdown(name, parent, new Vector2(.59f, y), 425, "");
            dropdown.GetComponent<RectTransform>().sizeDelta = new Vector2(425, 48);
            Skin(dropdown.GetComponent<Image>(), surface, Teal);
            dropdown.captionText.color = Cream;
            dropdown.captionText.fontSize = 22;
            dropdown.captionText.raycastTarget = false;
            dropdown.itemText.fontSize = 19;
            return dropdown;
        }

        private static Button SettingsButton(Transform parent, string name, string caption, float x, float y, float width, Sprite surface, float height = 52)
        {
            var button = CreateButton(name, parent, caption, new Vector2(x, y), new Vector2(width, height)).GetComponent<Button>();
            StyleButton(button, surface, false, false, false);
            return button;
        }

        private static TMP_Text SettingText(string name, Transform parent, string text, float x, float y, float width, float size, Color color)
        {
            var label = CreateText(name, parent, text, size, color, new Vector2(x, y), new Vector2(width, 48));
            label.raycastTarget = false;
            return label;
        }
    }
}
