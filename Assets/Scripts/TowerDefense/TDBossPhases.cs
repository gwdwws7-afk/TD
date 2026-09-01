using UnityEngine;

namespace TD
{
    /// <summary>
    /// Pure logic for the four exam bosses (expansion batch 2,
    /// boss-design-spec-v1). Timers and state live on TDEnemy / the
    /// manager's boss services — everything here is scene-free and pinned
    /// by TDBossPhasesTests.
    /// </summary>
    public static class TDBossPhases
    {
        // ── Shared ──
        public const float PhaseEventDurationSeconds = 5.4f;

        /// <summary>
        /// One-step-per-call threshold descent: thresholds are descending
        /// health ratios ([0.70, 0.35]); each crossing advances exactly one
        /// phase, so a burst from 80% to 20% still walks the ladder and fires
        /// every transition event in order.
        /// </summary>
        public static int ResolvePhaseIndex(float healthRatio, int currentIndex, float[] thresholds)
        {
            if (thresholds == null || currentIndex >= thresholds.Length)
            {
                return currentIndex;
            }

            return healthRatio < thresholds[currentIndex] ? currentIndex + 1 : currentIndex;
        }

        // ── Containermaw (L05) ──
        public const float ContainerThrowInterval = 12f;
        public const float ContainerBlockDuration = 10f;
        public const float ContainerPhaseTwoHealthRatio = 0.50f;
        public const int ContainerPhaseTwoArmor = 2;
        public const float ContainerPhaseTwoSpeedMultiplier = 1.6f;
        public const int ContainerPhaseTwoLineDamage = 5;

        // ── Junction Tyrant (L09) ──
        public const float TyrantRerouteInterval = 15f;
        public const float TyrantSplitHealthRatio = 0.35f;
        public const int TyrantTwinArmor = 4;
        public const float TyrantTwinSpeed = 0.85f;

        // ── Kiln Custodian (L13) ──
        public const float CustodianStackInterval = 10f;
        public const int CustodianMaxStacks = 8;
        public const int CustodianPurgeArmorCut = 5;
        public const float CustodianPurgePauseSeconds = 8f;
        public const int CustodianSummonAshSwarm = 6;
        public const int CustodianSummonPlatedSpore = 2;

        public static int ClampStacks(int stacks)
        {
            return Mathf.Clamp(stacks, 0, CustodianMaxStacks);
        }

        // ── Echo Harbinger (L17) ──
        public const float HarbingerMimicInterval = 6f;

        /// <summary>
        /// Mimic families (spec table): what the harbinger gains from each
        /// tower kind, every 6s, as self-effects only.
        /// </summary>
        public enum MimicCategory
        {
            None = 0,
            Surge = 1,        // lance/rail: speed x1.8 for 2s
            BurnCloud = 2,    // mortar/burner: nearby towers -12% rate
            Slipstream = 3,   // coil/snare: slow-immune 5s
            Reforge = 4,      // drill: armor +3, stacking
            Barrage = 5,      // flak/welder: next 3 hits at half damage
            SignalJam = 6     // beacon/derrick/barricade: resonance frozen 3s
        }

        public static MimicCategory ResolveMimicCategory(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => MimicCategory.Surge,
                TDTowerKind.LongRailCannon => MimicCategory.Surge,
                TDTowerKind.CinderMortar => MimicCategory.BurnCloud,
                TDTowerKind.SlagBurner => MimicCategory.BurnCloud,
                TDTowerKind.FrostCoil => MimicCategory.Slipstream,
                TDTowerKind.GravSnare => MimicCategory.Slipstream,
                TDTowerKind.SiegeDrill => MimicCategory.Reforge,
                TDTowerKind.EmberFlak => MimicCategory.Barrage,
                TDTowerKind.ArcWelder => MimicCategory.Barrage,
                TDTowerKind.ResonanceBeacon => MimicCategory.SignalJam,
                TDTowerKind.SalvageDerrick => MimicCategory.SignalJam,
                TDTowerKind.RailBarricade => MimicCategory.SignalJam,
                _ => MimicCategory.None
            };
        }
    }
}
