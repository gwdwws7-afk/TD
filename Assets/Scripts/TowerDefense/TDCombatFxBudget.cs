using System;
using System.Collections.Generic;
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

        // Explicit per-object-name tiers. Classification used to be pure
        // substring matching, so renaming an FX object silently moved it to
        // another budget tier; the registry pins every known spawner name and
        // unknown names fall back to the heuristic WITH a one-time warning so
        // renames/new FX surface immediately instead of re-tiering silently.
        private static readonly Dictionary<string, TDCombatFxClass> KnownClasses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Accent — boss/critical moments and upgrade feedback.
                ["Fx_BossWarning"] = TDCombatFxClass.Accent,
                ["Upgrade_Ring_Fx"] = TDCombatFxClass.Accent,
                ["Upgrade_Branch_Fx"] = TDCombatFxClass.Accent,
                ["Upgrade_Identity_Fx"] = TDCombatFxClass.Accent,
                // Tactical — per-hit impact and control field effects.
                ["Fx_ImpactSpark"] = TDCombatFxClass.Tactical,
                ["Fx_AoeIndicator"] = TDCombatFxClass.Tactical,
                ["Fx_ArcChainLink"] = TDCombatFxClass.Tactical,
                ["Fx_GravityBoundary"] = TDCombatFxClass.Tactical,
                ["Fx_GravityBoundaryCore"] = TDCombatFxClass.Tactical,
                // Routine — trails, body hits, death and ambient pulses.
                ["Fx_ProjectileTrail"] = TDCombatFxClass.Routine,
                ["Fx_EnemyHit"] = TDCombatFxClass.Routine,
                ["Fx_EnemyDeath"] = TDCombatFxClass.Routine,
                ["Fx_BurrowAmbush"] = TDCombatFxClass.Routine,
                ["Fx_SupportLink"] = TDCombatFxClass.Routine,
                ["Fx_AttritionSiphon"] = TDCombatFxClass.Routine,
                ["Fx_ElitePressure"] = TDCombatFxClass.Routine,
                ["Fx_MimicShift"] = TDCombatFxClass.Routine,
                ["Fx_SporeSplitWarning"] = TDCombatFxClass.Routine,
                ["Fx_DamageSpecPulse"] = TDCombatFxClass.Routine,
                ["Fx_UtilitySpecField"] = TDCombatFxClass.Routine,
            };

        private static readonly HashSet<string> WarnedUnknownNames = new(StringComparer.OrdinalIgnoreCase);

        private static readonly int[] ActiveByClass = new int[3];

        public static int ActiveTotal { get; private set; }
        public static int MaximumObserved { get; private set; }
        public static int SuppressedCount { get; private set; }
        public static float MaximumAcceptedDuration { get; private set; }
        public static float MaximumAcceptedAlpha { get; private set; }

        /// <summary>Pin (or re-pin) an FX object name to an explicit tier.</summary>
        public static void RegisterClass(string objectName, TDCombatFxClass fxClass)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            KnownClasses[objectName] = fxClass;
        }

        public static TDCombatFxClass Classify(string objectName)
        {
            var safeName = objectName ?? string.Empty;
            if (KnownClasses.TryGetValue(safeName, out var known))
            {
                return known;
            }

            if (WarnedUnknownNames.Add(safeName) && safeName.Length > 0)
            {
                Debug.LogWarning(
                    $"[TD] FX budget: '{safeName}' has no registered tier — falling back to the name heuristic. " +
                    "Register it via TDCombatFxBudget.RegisterClass so renames can't silently change its budget class.");
            }

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