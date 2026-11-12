using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LuckyNumber.UI
{
    internal static class DisplayPreferences
    {
        private const string ModeKey = "DisplayMode";
        private const string WidthKey = "DisplayWidth";
        private const string HeightKey = "DisplayHeight";

        internal static readonly FullScreenMode[] Modes =
        {
            FullScreenMode.Windowed,
            FullScreenMode.FullScreenWindow,
            FullScreenMode.ExclusiveFullScreen
        };

        internal static Vector2Int DesktopSize => new(Display.main.systemWidth, Display.main.systemHeight);

        internal static List<Vector2Int> Resolutions(FullScreenMode mode)
        {
            var desktop = DesktopSize;
            var sizes = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height));
            if (mode == FullScreenMode.Windowed)
            {
                var standard = new[] { new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1600, 900) };
                sizes = sizes.Concat(standard).Where(size => size.x <= desktop.x && size.y <= desktop.y);
            }
            return sizes.Where(size => size.x >= 800 && size.y >= 600).Append(desktop).Distinct()
                .OrderBy(size => size.x).ThenBy(size => size.y).ToList();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Restore()
        {
            if (Application.isEditor || Application.isBatchMode || !PlayerPrefs.HasKey(ModeKey) ||
                Environment.GetCommandLineArgs().Any(arg => arg is "-screen-width" or "-screen-height" or "-screen-fullscreen"))
                return;

            var mode = (FullScreenMode)PlayerPrefs.GetInt(ModeKey);
            if (!Modes.Contains(mode)) return;
            var size = new Vector2Int(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey));
            if (mode == FullScreenMode.FullScreenWindow || !Resolutions(mode).Contains(size)) size = DesktopSize;
            Apply(mode, size);
        }

        internal static void Apply(FullScreenMode mode, Vector2Int size) =>
            Screen.SetResolution(size.x, size.y, mode);

        internal static void Save(FullScreenMode mode, Vector2Int size)
        {
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.SetInt(WidthKey, size.x);
            PlayerPrefs.SetInt(HeightKey, size.y);
        }
    }
}
