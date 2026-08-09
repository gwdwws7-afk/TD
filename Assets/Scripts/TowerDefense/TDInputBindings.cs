using System;
using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public enum TDInputAction
    {
        StartWave = 0,
        ScenarioCommand = 1,
        Pause = 2,
        SpeedDown = 3,
        SpeedUp = 4,
        Settings = 5
    }

    public static class TDInputBindings
    {
        private const string PlayerPrefsPrefix = "td_p123_binding_";

        private static readonly Dictionary<TDInputAction, KeyCode> Defaults = new()
        {
            { TDInputAction.StartWave, KeyCode.Space },
            { TDInputAction.ScenarioCommand, KeyCode.C },
            { TDInputAction.Pause, KeyCode.P },
            { TDInputAction.SpeedDown, KeyCode.Minus },
            { TDInputAction.SpeedUp, KeyCode.Equals },
            { TDInputAction.Settings, KeyCode.Escape }
        };

        public static IReadOnlyList<TDInputAction> RebindableActions { get; } = new[]
        {
            TDInputAction.StartWave,
            TDInputAction.ScenarioCommand,
            TDInputAction.Pause,
            TDInputAction.SpeedDown,
            TDInputAction.SpeedUp,
            TDInputAction.Settings
        };

        public static KeyCode GetKey(TDInputAction action)
        {
            var fallback = Defaults.TryGetValue(action, out var defaultKey) ? defaultKey : KeyCode.None;
            var stored = PlayerPrefs.GetInt(PlayerPrefsPrefix + action, (int)fallback);
            return Enum.IsDefined(typeof(KeyCode), stored) ? (KeyCode)stored : fallback;
        }

        public static bool GetKeyDown(TDInputAction action)
        {
            return TDInputCompat.GetKeyDown(GetKey(action));
        }

        public static void SetKey(TDInputAction action, KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return;
            }

            PlayerPrefs.SetInt(PlayerPrefsPrefix + action, (int)key);
            PlayerPrefs.Save();
        }

        public static void ResetDefaults()
        {
            foreach (var pair in Defaults)
            {
                PlayerPrefs.SetInt(PlayerPrefsPrefix + pair.Key, (int)pair.Value);
            }

            PlayerPrefs.Save();
        }

        public static string GetActionLabel(TDInputAction action)
        {
            return action switch
            {
                TDInputAction.StartWave => "START WAVE ACTION",
                TDInputAction.ScenarioCommand => "SCENARIO ACTION",
                TDInputAction.Pause => "PAUSE ACTION",
                TDInputAction.SpeedDown => "SPEED DOWN ACTION",
                TDInputAction.SpeedUp => "SPEED UP ACTION",
                TDInputAction.Settings => "SETTINGS ACTION",
                _ => action.ToString().ToUpperInvariant()
            };
        }

        public static string GetKeyLabel(TDInputAction action)
        {
            return GetKey(action) switch
            {
                KeyCode.Space => "SPACE",
                KeyCode.Escape => "ESC",
                KeyCode.Minus => "-",
                KeyCode.Equals => "+",
                KeyCode.KeypadMinus => "NUM -",
                KeyCode.KeypadPlus => "NUM +",
                KeyCode.Return => "ENTER",
                KeyCode.KeypadEnter => "NUM ENTER",
                _ => GetKey(action).ToString().ToUpperInvariant()
            };
        }
    }
}
