using System;
using UnityEngine;

namespace TD
{
    public enum TDCombatFxClass
    {
        Routine = 0,
        Tactical = 1,
        Accent = 2
    }

    public static class TDCombatFxBudget
    {
        public const int MaxRoutine = 18;
        public const int MaxTactical = 12;
        public const int MaxAccent = 8;
        public const int MaxTotal = 32;

        private static readonly int[] ActiveByClass = new int[3];

        public static int ActiveTotal { get; private set; }
        public static int MaximumObserved { get; private set; }
        public static int SuppressedCount { get; private set; }
        public static float MaximumAcceptedDuration { get; private set; }
        public static float MaximumAcceptedAlpha { get; private set; }

        public static TDCombatFxClass Classify(string objectName)
        {
            var safeName = objectName ?? string.Empty;
            if (Contains(safeName, "Upgrade") || Contains(safeName, "Resonance") ||
                Contains(safeName, "Boss") || Contains(safeName, "Breach"))
            {
                return TDCombatFxClass.Accent;
            }

            if (Contains(safeName, "Impact") || Contains(safeName, "Aoe") ||
                Contains(safeName, "Gravity") || Contains(safeName, "Special") ||
                Contains(safeName, "Chain"))
            {
                return TDCombatFxClass.Tactical;
            }

            return TDCombatFxClass.Routine;
        }

        public static bool TryAcquire(TDCombatFxClass fxClass)
        {
            var index = Mathf.Clamp((int)fxClass, 0, ActiveByClass.Length - 1);
            var classLimit = fxClass switch
            {
                TDCombatFxClass.Accent => MaxAccent,
                TDCombatFxClass.Tactical => MaxTactical,
                _ => MaxRoutine
            };
            if (ActiveTotal >= MaxTotal || ActiveByClass[index] >= classLimit)
            {
                SuppressedCount++;
                return false;
            }

            ActiveByClass[index]++;
            ActiveTotal++;
            MaximumObserved = Mathf.Max(MaximumObserved, ActiveTotal);
            return true;
        }

        public static void Release(TDCombatFxClass fxClass)
        {
            var index = Mathf.Clamp((int)fxClass, 0, ActiveByClass.Length - 1);
            if (ActiveByClass[index] <= 0)
            {
                return;
            }

            ActiveByClass[index]--;
            ActiveTotal = Mathf.Max(0, ActiveTotal - 1);
        }

        public static float ClampDuration(TDCombatFxClass fxClass, float duration)
        {
            var maximum = fxClass switch
            {
                TDCombatFxClass.Accent => 0.90f,
                TDCombatFxClass.Tactical => 0.58f,
                _ => 0.24f
            };
            var result = Mathf.Clamp(duration, 0.01f, maximum);
            MaximumAcceptedDuration = Mathf.Max(MaximumAcceptedDuration, result);
            return result;
        }

        public static Color ClampColor(TDCombatFxClass fxClass, Color color)
        {
            var maximumAlpha = fxClass switch
            {
                TDCombatFxClass.Accent => 0.96f,
                TDCombatFxClass.Tactical => 0.88f,
                _ => 0.68f
            };
            color.a = Mathf.Min(color.a, maximumAlpha);
            MaximumAcceptedAlpha = Mathf.Max(MaximumAcceptedAlpha, color.a);
            return color;
        }

        public static void ResetDiagnostics()
        {
            MaximumObserved = ActiveTotal;
            SuppressedCount = 0;
            MaximumAcceptedDuration = 0f;
            MaximumAcceptedAlpha = 0f;
        }

        public static string BuildAuditReport()
        {
            return
                $"p13.4.fx.active={ActiveTotal}/{MaxTotal}\n" +
                $"p13.4.fx.maxObserved={MaximumObserved}/{MaxTotal}\n" +
                $"p13.4.fx.classes={ActiveByClass[0]}/{MaxRoutine},{ActiveByClass[1]}/{MaxTactical},{ActiveByClass[2]}/{MaxAccent}\n" +
                $"p13.4.fx.suppressed={SuppressedCount}\n" +
                $"p13.4.fx.maxDuration={MaximumAcceptedDuration:0.00}\n" +
                $"p13.4.fx.maxAlpha={MaximumAcceptedAlpha:0.00}";
        }

        private static bool Contains(string source, string token)
        {
            return source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
