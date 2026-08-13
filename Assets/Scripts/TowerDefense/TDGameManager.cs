using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TD
{
    public sealed partial class TDGameManager : MonoBehaviour
    {
        private static readonly Dictionary<string, Material> EnemyBodyRepairMaterials = new(StringComparer.OrdinalIgnoreCase);
        private enum TDResonanceCommand
        {
            None = 0,
            EmberSurge = 1,
            FractureMark = 2
        }

        private enum TDFirstRunTutorialStep
        {
            BuildTower = 0,
            InspectRange = 1,
            StartWave = 2,
            ReadArmor = 3,
            UpgradeTower = 4,
            UseScenario = 5,
            Complete = 6
        }

        private enum TDUiTextRole
        {
            Caption = 0,
            Body = 1,
            Metric = 2,
            PanelTitle = 3,
            SectionTitle = 4,
            ScreenTitle = 5
        }

        [Flags]
        private enum TDEnemyCodexObservation
        {
            Sighted = 1,
            ArmorBroken = 2,
            Slowed = 4,
            Leaked = 8,
            CounterKilled = 16,
            BossPhase = 32
        }

        [Flags]
        private enum TDTowerCodexObservation
        {
            Built = 1,
            DamageBranch = 2,
            UtilityBranch = 4,
            SpecializationProc = 8,
            MatrixMatch = 16
        }

        private const string CampaignResourcePath = "Data/campaign/campaign_main_v1";
        private const string DefaultWaveResourcePath = "Data/waves/grayline_junction01_m1_v1";
        private const string EnemyCatalogResourcePath = "Data/enemies/enemy_catalog_main_v1";
        private const int DefaultCampaignLevelIndex = 1;
        private const int DefaultMaxFailureReasonsShown = 3;
        private const int DefaultResonanceEnabledFromLevel = 1;
        private const bool DefaultAllowEarlyWaveDispatch = false;
        private const int DefaultDefenseBudget = 120;
        private const int DefaultLineIntegrity = 20;
        private const int MissionTacticalStarThreshold = 75;
        private const int GridWidth = 16;
        private const int GridHeight = 9;
        private const float CellSize = 1f;
        public const int RoadSegmentCount = 4;
        private static readonly bool AllowBuildAndUpgradeDuringCombat = false;
        private const float ResonanceChargeMax = 100f;
        private const float ResonanceWindowDuration = 7f;
        private const float ResonanceHitChargePerDamage = 0.22f;
        private const float ResonanceHitChargeMin = 0.75f;
        private const float ResonanceKillCharge = 8f;
        private const float ResonanceMarkPulseInterval = 0.35f;
        private const float ResonanceMarkDuration = 0.60f;
        private const float SupportAuraRadius = 1.9f;
        private const int AttritionBudgetPenalty = 12;
        private const float AttritionResonanceDrain = 10f;
        private const int ResonanceChainRequiredMatches = 2;
        private const int ResonanceChainBudgetBonusOnEmberSurge = 10;
        private const int ResonanceChainBudgetBonusOnFractureMark = 6;
        private const int ResonanceChainIntegrityBonusOnFractureMark = 1;
        private const int MatrixConvergenceRequiredMatches = 6;
        private const int MatrixConvergenceRequiredSpecializations = 2;
        private const float MatrixConvergenceEmberWindowExtension = 1.35f;
        private const float MatrixConvergenceEmberDamageMultiplier = 1.12f;
        private const float MatrixConvergenceEmberFireRateMultiplier = 1.10f;
        private const float MatrixConvergenceFractureDuration = 2.4f;
        private const float MatrixConvergenceFractureExposure = 1.18f;
        private const float AdaptiveDoctrinePowerMultiplier = 1.04f;
        private const float SpecializedDoctrinePowerMultiplier = 1.10f;
        private const float SfxDefaultVolume = 0.24f;
        private const int SfxSampleRate = 22050;
        private const string FailureTagOutputInsufficient = "output_insufficient";
        private const string FailureTagCoverageGap = "coverage_gap";
        private const string FailureTagCounterMismatch = "counter_mismatch";
        private const string CodexPlayerPrefsPrefix = "td_codex_enemy_";
        private const string P9PlaybackSpeedKey = "td_p9_playback_speed";
        private const string P9MarkersEnabledKey = "td_p9_markers_enabled";
        private const string P9LargeTextEnabledKey = "td_p9_large_text_enabled";
        private const string P9TutorialStepKeyPrefix = "td_p9_tutorial_step_slot_";
        private const string P9TutorialCompleteKeyPrefix = "td_p9_tutorial_complete_slot_";
        private const string P123TutorialTelemetryPrefix = "td_p123_tutorial_telemetry_";
        private const string P123UiScaleKey = "td_p123_ui_scale";
        private const string P123SubtitlesKey = "td_p123_subtitles";
        private const string P123CaptionsKey = "td_p123_captions";
        private const string P123MasterVolumeKey = "td_p123_master_volume";
        private const string P123MusicVolumeKey = "td_p123_music_volume";
        private const string P123EffectsVolumeKey = "td_p123_effects_volume";
        private static bool UseRuntimeBattleUi => true;

        private static readonly KeyCode[] TowerHotkeys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6,
            KeyCode.Alpha7,
            KeyCode.Alpha8
        };

        private static readonly string[] EmberSurgeThreatPatterns =
        {
            "armored",
            "heavy",
            "durability",
            "counter",
            "boss",
            "final",
            "attrition",
            "mark_focus_fire",
            "siege_break"
        };

        private static readonly string[] FractureMarkThreatPatterns =
        {
            "fast",
            "swarm",
            "gap",
            "pressure",
            "zone_control",
            "anti_fast",
            "split",
            "mixed",
            "flank",
            "arc_chain",
            "grav_well"
        };

        private static readonly string[] RoadSegmentLabels =
        {
            "Entry",
            "Approach",
            "Core",
            "Exit"
        };

        private static readonly string[] MissionIntelThreatPatterns =
        {
            "fast",
            "swarm",
            "armored",
            "heavy",
            "boss",
            "flank",
            "split",
            "support",
            "attrition",
            "mixed",
            "durability",
            "pressure",
            "gap",
            "control",
            "special",
            "light"
        };

        private static readonly Vector2Int[] GraylinePathCells =
        {
            new(0, 6), new(1, 5), new(2, 5), new(3, 5), new(4, 5),
            new(5, 5), new(6, 4), new(7, 4), new(8, 4), new(9, 3),
            new(10, 3), new(11, 3), new(12, 3), new(13, 3), new(14, 4),
            new(15, 4)
        };

        private static readonly Vector2[] GraylineRoadPathPoints =
        {
            new(-0.20f, 6.08f),
            new(0.45f, 6.02f),
            new(1.24f, 5.02f),
            new(6.24f, 5.02f),
            new(6.48f, 4.04f),
            new(8.46f, 4.04f),
            new(9.05f, 3.22f),
            new(13.46f, 3.22f),
            new(13.83f, 4.64f),
            new(16.20f, 4.64f)
        };

        private static readonly Vector2Int[] AshfallPathCells =
        {
            new(0, 4), new(1, 4), new(2, 4), new(3, 4), new(4, 4),
            new(5, 4), new(6, 4), new(7, 4), new(8, 4), new(9, 4),
            new(10, 4), new(11, 4), new(12, 4), new(13, 4), new(14, 4),
            new(15, 4)
        };

        private static readonly Vector2Int[] AshfallLeftPathCells =
        {
            new(0, 6), new(1, 6), new(2, 6), new(3, 6), new(4, 5),
            new(5, 5), new(6, 5), new(7, 5), new(8, 5), new(9, 5),
            new(10, 5), new(11, 4), new(12, 4), new(13, 4), new(14, 4),
            new(15, 4)
        };

        private static readonly Vector2Int[] AshfallRightPathCells =
        {
            new(0, 2), new(1, 2), new(2, 2), new(3, 2), new(4, 3),
            new(5, 3), new(6, 3), new(7, 3), new(8, 3), new(9, 4),
            new(10, 4), new(11, 4), new(12, 4), new(13, 4), new(14, 4),
            new(15, 4)
        };

        private static readonly Vector2Int[] AshfallCrossLanePathCells =
        {
            new(0, 5), new(1, 5), new(2, 5), new(3, 5), new(4, 5),
            new(5, 5), new(6, 5), new(7, 4), new(8, 4), new(9, 4),
            new(10, 4), new(11, 4), new(12, 4), new(13, 4), new(14, 4),
            new(15, 4)
        };

        private static readonly Vector2[] AshfallCenterRoadPathPoints =
        {
            new(-0.20f, 4.55f), new(16.20f, 4.55f)
        };

        private static readonly Vector2[] AshfallLeftRoadPathPoints =
        {
            new(-0.20f, 7.04f), new(0.80f, 7.04f), new(1.45f, 6.95f),
            new(1.85f, 6.65f), new(2.25f, 6.15f), new(2.75f, 5.78f),
            new(3.40f, 5.70f), new(7.50f, 5.70f), new(8.20f, 5.72f),
            new(8.75f, 5.95f), new(9.30f, 6.45f), new(9.85f, 6.98f),
            new(10.40f, 7.38f), new(11.10f, 7.52f), new(13.70f, 7.52f),
            new(14.30f, 7.30f), new(14.75f, 6.78f), new(15.20f, 6.25f),
            new(15.70f, 5.90f), new(16.20f, 5.75f)
        };

        private static readonly Vector2[] AshfallRightRoadPathPoints =
        {
            new(-0.20f, 0.45f), new(1.55f, 0.45f), new(1.95f, 0.58f),
            new(2.45f, 0.92f), new(3.10f, 1.55f), new(3.75f, 2.25f),
            new(4.55f, 2.88f), new(5.20f, 3.12f), new(11.55f, 3.12f),
            new(12.25f, 3.08f), new(13.15f, 3.08f), new(13.85f, 3.18f),
            new(14.35f, 3.55f), new(14.85f, 4.05f), new(15.40f, 4.42f),
            new(16.20f, 4.55f)
        };

        private static readonly Vector2[] AshfallCrossRoadPathPoints =
        {
            new(-0.20f, 5.78f), new(0.35f, 5.55f), new(0.80f, 5.08f),
            new(1.15f, 4.72f), new(1.75f, 4.55f), new(10.55f, 4.55f),
            new(11.00f, 4.72f), new(11.40f, 5.12f), new(11.75f, 5.55f),
            new(12.15f, 5.92f), new(12.70f, 6.02f), new(13.15f, 5.85f),
            new(13.55f, 5.45f), new(13.95f, 5.00f), new(14.40f, 4.68f),
            new(14.95f, 4.55f), new(16.20f, 4.55f)
        };

        private static readonly Vector2Int[] SplitSwitchPathCells =
        {
            new(0, 3), new(1, 3), new(2, 3), new(3, 3), new(4, 3),
            new(5, 3), new(6, 3), new(7, 3), new(8, 3), new(9, 3),
            new(10, 3), new(11, 3), new(12, 3), new(13, 3), new(14, 3),
            new(15, 3)
        };

        private static readonly Vector2Int[] SplitSwitchLeftPathCells =
        {
            new(0, 5), new(1, 6), new(2, 6), new(3, 6), new(4, 6),
            new(5, 5), new(6, 5), new(7, 5), new(8, 6), new(9, 6),
            new(10, 6), new(11, 6), new(12, 5), new(13, 5), new(14, 6),
            new(15, 6)
        };

        private static readonly Vector2Int[] SplitSwitchRightPathCells =
        {
            new(0, 2), new(1, 2), new(2, 2), new(3, 2), new(4, 3),
            new(5, 4), new(6, 4), new(7, 3), new(8, 2), new(9, 2),
            new(10, 2), new(11, 2), new(12, 2), new(13, 2), new(14, 3),
            new(15, 4)
        };

        private static readonly Vector2Int[] SplitSwitchCrossLanePathCells =
        {
            new(0, 4), new(1, 4), new(2, 4), new(3, 4), new(4, 4),
            new(5, 4), new(6, 4), new(7, 5), new(8, 6), new(9, 6),
            new(10, 6), new(11, 5), new(12, 4), new(13, 4), new(14, 4),
            new(15, 4)
        };

        private static readonly Vector2[] SplitSwitchCenterRoadPathPoints =
        {
            new(-0.20f, 2.88f), new(16.20f, 2.88f)
        };

        private static readonly Vector2[] SplitSwitchLeftRoadPathPoints =
        {
            new(-0.20f, 5.05f), new(0.45f, 5.18f), new(1.15f, 5.55f),
            new(1.80f, 5.82f), new(2.60f, 5.90f), new(3.55f, 5.88f),
            new(4.15f, 5.68f), new(4.70f, 5.12f), new(5.25f, 4.72f),
            new(5.85f, 4.67f), new(6.50f, 4.95f), new(7.20f, 5.30f),
            new(8.00f, 5.68f), new(8.75f, 5.80f), new(9.45f, 5.76f),
            new(10.10f, 5.90f), new(10.80f, 5.98f), new(11.50f, 5.72f),
            new(12.20f, 5.28f), new(12.85f, 5.06f), new(13.50f, 5.02f),
            new(14.20f, 5.22f), new(14.85f, 5.60f), new(15.45f, 5.88f),
            new(16.20f, 5.92f)
        };

        private static readonly Vector2[] SplitSwitchRightRoadPathPoints =
        {
            new(-0.20f, 1.84f), new(1.20f, 1.84f), new(2.60f, 1.84f),
            new(3.15f, 1.88f), new(3.70f, 2.05f), new(4.18f, 2.42f),
            new(4.62f, 3.00f), new(5.05f, 3.65f), new(5.48f, 4.15f),
            new(5.92f, 4.36f), new(6.38f, 4.18f), new(6.85f, 3.65f),
            new(7.35f, 2.98f), new(7.92f, 2.40f), new(8.55f, 2.00f),
            new(9.30f, 1.84f), new(10.70f, 1.84f), new(12.15f, 1.84f),
            new(12.75f, 1.92f), new(13.30f, 2.18f), new(13.78f, 2.67f),
            new(14.22f, 3.28f), new(14.68f, 3.80f), new(15.20f, 4.20f),
            new(15.72f, 4.36f), new(16.20f, 4.38f)
        };

        private static readonly Vector2[] SplitSwitchCrossRoadPathPoints =
        {
            new(-0.20f, 3.98f), new(1.30f, 3.98f), new(2.80f, 3.98f),
            new(4.30f, 3.98f), new(5.40f, 3.98f), new(5.85f, 4.00f),
            new(6.25f, 4.12f), new(6.80f, 4.48f), new(7.40f, 4.98f),
            new(8.05f, 5.48f), new(8.72f, 5.78f), new(9.35f, 5.90f),
            new(9.95f, 5.88f), new(10.55f, 5.63f), new(11.20f, 5.16f),
            new(11.88f, 4.58f), new(12.48f, 4.12f), new(13.05f, 3.92f),
            new(13.65f, 3.88f), new(14.90f, 3.92f), new(16.20f, 3.94f)
        };

        private static readonly Vector2Int[] HollowKilnPathCells =
        {
            new(0, 3), new(1, 3), new(2, 3), new(3, 4), new(4, 5),
            new(5, 5), new(6, 4), new(7, 3), new(8, 3), new(9, 4),
            new(10, 5), new(11, 5), new(12, 4), new(13, 3), new(14, 3),
            new(15, 4)
        };

        private static readonly Vector2Int[] HollowKilnLeftPathCells =
        {
            new(0, 6), new(1, 6), new(2, 6), new(3, 6), new(4, 5),
            new(5, 5), new(6, 5), new(7, 6), new(8, 6), new(9, 6),
            new(10, 5), new(11, 5), new(12, 5), new(13, 4), new(14, 4),
            new(15, 3)
        };

        private static readonly Vector2Int[] HollowKilnRightPathCells =
        {
            new(0, 1), new(1, 1), new(2, 2), new(3, 2), new(4, 3),
            new(5, 3), new(6, 2), new(7, 1), new(8, 1), new(9, 2),
            new(10, 3), new(11, 3), new(12, 2), new(13, 2), new(14, 2),
            new(15, 2)
        };

        private static readonly Vector2Int[] HollowKilnCrossLanePathCells =
        {
            new(0, 4), new(1, 4), new(2, 4), new(3, 3), new(4, 3),
            new(5, 4), new(6, 4), new(7, 4), new(8, 4), new(9, 4),
            new(10, 4), new(11, 4), new(12, 4), new(13, 4), new(14, 5),
            new(15, 5)
        };

        private static readonly Vector2[] HollowKilnCenterRoadPathPoints =
        {
            new(-0.20f, 4.50f), new(16.20f, 4.50f)
        };

        private static readonly Vector2[] HollowKilnLeftRoadPathPoints =
        {
            new(-0.20f, 7.35f), new(1.20f, 7.35f), new(2.00f, 7.10f),
            new(2.80f, 6.85f), new(4.00f, 6.62f), new(9.80f, 6.62f),
            new(10.80f, 6.72f), new(11.80f, 6.98f), new(12.80f, 7.33f),
            new(13.55f, 7.40f), new(14.20f, 7.15f), new(14.90f, 6.75f),
            new(15.55f, 6.55f), new(16.20f, 6.50f)
        };

        private static readonly Vector2[] HollowKilnRightRoadPathPoints =
        {
            new(-0.20f, 1.95f), new(1.60f, 1.95f), new(2.30f, 2.15f),
            new(3.00f, 2.65f), new(3.60f, 3.15f), new(4.20f, 3.32f),
            new(4.80f, 3.15f), new(5.45f, 2.50f), new(6.20f, 1.60f),
            new(6.90f, 1.05f), new(7.70f, 0.82f), new(9.00f, 0.82f),
            new(9.75f, 1.05f), new(10.50f, 1.65f), new(11.20f, 2.45f),
            new(11.90f, 3.08f), new(12.60f, 3.33f), new(13.35f, 3.28f),
            new(14.00f, 2.85f), new(14.70f, 2.30f), new(15.30f, 2.02f),
            new(16.20f, 1.95f)
        };

        private static readonly Vector2[] HollowKilnCrossRoadPathPoints =
        {
            new(-0.20f, 3.05f), new(0.70f, 3.12f), new(1.50f, 3.45f),
            new(2.25f, 4.20f), new(3.00f, 5.05f), new(3.70f, 5.55f),
            new(4.50f, 5.62f), new(5.20f, 5.40f), new(5.80f, 4.80f),
            new(6.40f, 4.00f), new(7.00f, 3.45f), new(7.70f, 3.20f),
            new(8.40f, 3.35f), new(9.00f, 3.95f), new(9.60f, 4.70f),
            new(10.20f, 5.35f), new(10.90f, 5.62f), new(11.60f, 5.55f),
            new(12.20f, 5.10f), new(12.80f, 4.45f), new(13.50f, 3.75f),
            new(14.20f, 3.30f), new(15.00f, 3.10f), new(16.20f, 3.20f)
        };

        private static readonly Vector2Int[] LastEmberPathCells =
        {
            new(0, 5), new(1, 5), new(2, 4), new(3, 4), new(4, 5),
            new(5, 6), new(6, 6), new(7, 5), new(8, 4), new(9, 3),
            new(10, 3), new(11, 4), new(12, 5), new(13, 5), new(14, 4),
            new(15, 4)
        };

        private static readonly Vector2Int[] LastEmberLeftPathCells =
        {
            new(0, 6), new(1, 6), new(2, 5), new(3, 5), new(4, 6),
            new(5, 6), new(6, 5), new(7, 5), new(8, 6), new(9, 6),
            new(10, 5), new(11, 5), new(12, 5), new(13, 6), new(14, 6),
            new(15, 5)
        };

        private static readonly Vector2Int[] LastEmberRightPathCells =
        {
            new(0, 1), new(1, 1), new(2, 2), new(3, 2), new(4, 3),
            new(5, 3), new(6, 2), new(7, 2), new(8, 3), new(9, 4),
            new(10, 4), new(11, 3), new(12, 2), new(13, 2), new(14, 3),
            new(15, 3)
        };

        private static readonly Vector2Int[] LastEmberCrossLanePathCells =
        {
            new(0, 4), new(1, 4), new(2, 4), new(3, 5), new(4, 5),
            new(5, 4), new(6, 4), new(7, 4), new(8, 4), new(9, 4),
            new(10, 4), new(11, 5), new(12, 5), new(13, 4), new(14, 4),
            new(15, 4)
        };

        private static readonly Vector2[] LastEmberCenterRoadPathPoints =
        {
            new(-0.20f, 4.48f), new(16.20f, 4.48f)
        };

        private static readonly Vector2[] LastEmberLeftRoadPathPoints =
        {
            new(-0.20f, 1.10f), new(0.35f, 1.20f), new(0.70f, 1.65f),
            new(1.10f, 2.05f), new(1.60f, 2.18f), new(6.60f, 2.18f),
            new(7.20f, 2.32f), new(7.65f, 2.75f), new(8.20f, 3.45f),
            new(8.80f, 4.15f), new(9.35f, 4.48f), new(9.90f, 4.10f),
            new(10.45f, 3.35f), new(11.00f, 2.55f), new(11.55f, 2.25f),
            new(12.20f, 2.25f), new(12.75f, 2.70f), new(13.30f, 3.50f),
            new(13.85f, 4.40f), new(14.40f, 5.10f), new(15.00f, 5.42f),
            new(16.20f, 5.42f)
        };

        private static readonly Vector2[] LastEmberRightRoadPathPoints =
        {
            new(-0.20f, 3.88f), new(0.60f, 3.85f), new(1.20f, 4.05f),
            new(1.90f, 4.55f), new(2.70f, 4.68f), new(6.90f, 4.68f),
            new(7.50f, 4.55f), new(8.10f, 4.15f), new(8.80f, 3.45f),
            new(9.40f, 2.80f), new(10.00f, 2.42f), new(10.60f, 2.38f),
            new(11.20f, 2.80f), new(11.80f, 3.60f), new(12.40f, 4.48f),
            new(13.00f, 5.30f), new(13.70f, 5.72f), new(14.50f, 5.75f),
            new(15.20f, 5.55f), new(16.20f, 4.95f)
        };

        private static readonly Vector2[] LastEmberCrossRoadPathPoints =
        {
            new(-0.20f, 4.90f), new(0.60f, 4.90f), new(1.20f, 5.10f),
            new(1.80f, 5.70f), new(2.40f, 6.35f), new(3.20f, 6.90f),
            new(4.00f, 7.15f), new(4.70f, 7.05f), new(5.20f, 6.60f),
            new(5.80f, 6.30f), new(6.60f, 6.30f), new(7.10f, 6.10f),
            new(7.60f, 5.70f), new(8.20f, 5.25f), new(8.80f, 5.15f),
            new(9.40f, 5.40f), new(10.00f, 6.00f), new(10.70f, 6.50f),
            new(11.50f, 6.60f), new(14.20f, 6.60f), new(14.90f, 6.40f),
            new(15.40f, 5.90f), new(16.20f, 5.45f)
        };

        private static readonly Vector2Int[] AshfallBuildPathCells =
            CombinePathCells(AshfallPathCells, AshfallLeftPathCells, AshfallRightPathCells, AshfallCrossLanePathCells);

        private static readonly Vector2Int[] SplitSwitchBuildPathCells =
            CombinePathCells(SplitSwitchPathCells, SplitSwitchLeftPathCells, SplitSwitchRightPathCells, SplitSwitchCrossLanePathCells);

        private static readonly Vector2Int[] HollowKilnBuildPathCells =
            CombinePathCells(HollowKilnPathCells, HollowKilnLeftPathCells, HollowKilnRightPathCells, HollowKilnCrossLanePathCells);

        private static readonly Vector2Int[] LastEmberBuildPathCells =
            CombinePathCells(LastEmberPathCells, LastEmberLeftPathCells, LastEmberRightPathCells, LastEmberCrossLanePathCells);

        private sealed class TDWaveRuntimeStat
        {
            public int waveIndex;
            public string phase;
            public string goalTag;
            public string threatTags;
            public float budgetTarget;
            public float budgetActual;
            public bool budgetInRange;
            public bool dispatchedEarly;
            public int budgetStart;
            public int budgetEnd;
            public int integrityStart;
            public int integrityEnd;
            public int kills;
            public int escapes;
            public int damageDealt;
            public int integrityDamageTaken;
            public int readinessScore;
            public string readinessGrade;
            public int combatIncome;
            public int clearIncome;
            public int reinforcementIncome;
            public int resonanceIncome;
            public int buildSpend;
            public int upgradeSpend;
            public int scenarioSpend;
            public int buildsPurchased;
            public int upgradesPurchased;
            public int scenarioUses;
            public int towersAtEnd;
            public int upgradesAtEnd;
            public bool cleared;
            public bool logged;
            public readonly Dictionary<string, int> failureReasons = new();
        }

        private sealed class TDTacticalEvent
        {
            public string message;
            public float timer;
        }

        private sealed class TDDefenseReadinessReport
        {
            public int score;
            public int coverageScore;
            public int counterScore;
            public int outputScore;
            public string grade;
            public string plan;
        }

        private sealed class TDLaneRuntimeStat
        {
            public string laneKey;
            public int spawned;
            public int spawnedHealth;
            public int kills;
            public int escapes;
            public int damageDealt;
            public int integrityDamageTaken;
            public readonly Dictionary<string, int> enemySpawns = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class TDTowerRuntimeStat
        {
            public string towerId;
            public TDTowerKind kind;
            public Vector2Int cell;
            public int buildCost;
            public int upgradeSpend;
            public int upgrades;
            public int hits;
            public int damageDealt;
            public int kills;
            public int controlApplications;
            public float controlStrengthSeconds;
            public int counterDamage;
            public int damageSpecProcs;
            public int utilitySpecProcs;
            public int ultimateAffectedTargets;
            public int matrixTraitMatches;
            public int matrixResonanceMatches;
            public int matrixFullMatches;
            public readonly Dictionary<string, int> laneDamage = new(StringComparer.OrdinalIgnoreCase);

            public int TotalSpend => buildCost + upgradeSpend;
        }

        private sealed class TDRoadSegmentRuntimeStat
        {
            public string laneKey;
            public int segmentIndex;
            public int reached;
            public int damageDealt;
            public int kills;
            public int escapes;
            public int integrityDamageTaken;
            public int unresolvedAtEnd;
            public int controlApplications;
            public int counterDamage;
        }

        private sealed class TDRoadHeatReport
        {
            public TDRoadSegmentRuntimeStat stat;
            public int coverageScore;
            public int heatScore;
            public Vector2Int suggestedCell;
            public bool hasSuggestedCell;
        }

        private sealed class TDRunScoreReport
        {
            public int total;
            public int coverage;
            public int counterMatch;
            public int output;
            public int economy;
            public int command;
            public string grade;
        }

        private sealed class TDMissionContractReport
        {
            public TDCampaignContractDefinition contract;
            public int currentValue;
            public bool targetMet;
            public bool completed;
        }

        private sealed class TDFormationFitReport
        {
            public int total;
            public int coverage;
            public int matrix;
            public int doctrine;
            public string grade;
            public string coveredCategories;
            public string gapCategories;
            public string matrixPicks;
            public string doctrineAdvice;
        }

        private readonly List<TDEnemy> _activeEnemies = new();
        private readonly Dictionary<string, TDEnemyCatalogEntry> _enemyCatalog = new();
        private readonly Dictionary<string, TDEnemyCatalogEntry> _globalEnemyCatalog = new();
        private readonly Dictionary<int, TDWaveRuntimeStat> _waveStats = new();
        private readonly Dictionary<string, int> _failureReasonCounts = new();
        private readonly Dictionary<string, TDLaneRuntimeStat> _laneStats = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TDTowerRuntimeStat> _towerStats = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TDRoadSegmentRuntimeStat> _roadSegmentStats = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _threatCategoryDamage = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _threatCategoryCounterDamage = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _ultimateProcCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _ultimateFullMatchCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _matrixWindowSpecializationIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TDTowerKind> _availableTowerKinds = new();
        private readonly List<TDTowerKind> _unlockedTowerKinds = new();
        private readonly List<TDTowerKind> _formationDraftTowerKinds = new();
        private readonly Dictionary<string, IReadOnlyList<Vector3>> _activeLanePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _currentWaveThreatTagSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _encounteredEnemyIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> _sfxClipCache = new();
        private readonly List<TDTacticalEvent> _tacticalEvents = new();
        private readonly List<LineRenderer> _routePreviewLines = new();
        private List<TDRoadHeatReport> _cachedRoadHeatReports;
        private float _nextDamageSpecialistFeedbackTime;
        private float _nextUtilitySpecialistFeedbackTime;

        private TDGridMap _gridMap;
        private Camera _mainCamera;
        private AudioSource _sfxSource;
        private AudioSource _tacticalSfxSource;
        private AudioSource _criticalSfxSource;
        private AudioSource _musicSource;
        private AudioSource _ambienceSource;
        private AudioClip _musicClip;
        private AudioClip _ambienceClip;
        private AudioMixer _emberlineMixer;
        private AudioMixerGroup _mixerMusicGroup;
        private AudioMixerGroup _mixerSfxGroup;
        private AudioMixerGroup _mixerAmbienceGroup;
        private string _activeMusicState;
        private float _nextUltimateSfxTime;
        private TDTower _lastHoverSfxTower;
        private TDTowerTooltip _towerTooltip;
        private const string AudioBasePath = "Audio";
        private TDCampaignDefinition _campaign;
        private TDCampaignRoute _campaignRoute;
        private TDWaveSet _waveSet;
        private Coroutine _waveRoutine;
        private TDWaveRuntimeStat _currentWaveStat;
        private string _loadError;
        private string _campaignError;
        private string _waveError;
        private string _enemyCatalogError;
        private string _waveResourcePath = DefaultWaveResourcePath;
        private int _maxFailureReasonsShown = DefaultMaxFailureReasonsShown;
        private int _resonanceEnabledFromLevel = DefaultResonanceEnabledFromLevel;
        private bool _allowEarlyWaveDispatch = DefaultAllowEarlyWaveDispatch;
        private bool _isResonanceSystemEnabled = true;
        private int _startingDefenseBudget = DefaultDefenseBudget;
        private int _startingLineIntegrity = DefaultLineIntegrity;
        private float _missionEnemyHpMultiplier = 1f;
        private float _missionEnemySpeedMultiplier = 1f;
        private int _missionEnemyArmorBonus;
        private float _missionRewardMultiplier = 1f;
        private float _missionResonanceGainMultiplier = 1f;
        private int _missionPrepSecondsBonus;
        private float _scenarioCostMultiplier = 1f;
        private int _chapterRewardBudgetBonus;
        private int _chapterRewardIntegrityBonus;
        private float _chapterRewardResonanceMultiplier = 1f;
        private TDCampaignDifficultyTier _activeCampaignDifficulty = TDCampaignDifficultyTier.Standard;
        private TDCampaignTacticalProtocolDefinition _activeTacticalProtocol;
        private TDCampaignScenarioMechanicDefinition _activeScenarioMechanic;
        private int _scenarioCharges;
        private int _scenarioUses;
        private int _scenarioOpportunities;
        private string _scenarioRouteBias = "center";
        private float _scenarioWaveDelayBonus;
        private bool _scenarioReinforcementPending;
        private bool _scenarioBossPhaseSuppressed;
        private int _scenarioBossPhase;
        private TDExamPresentationProfile _examPresentationProfile;
        private TDExamScenarioDeviceView _examScenarioDevice;
        private TDExamPresentationStage _examPresentationStage;
        private int _examOpeningBeatCount;
        private int _examEscalationBeatCount;
        private int _examDecisionBeatCount;
        private TDBattlePresentation _battlePresentation;
        private float _playbackSpeed = 1f;
        private float _lastActivePlaybackSpeed = 1f;
        private bool _playbackPaused;
        private bool _colorblindMarkersEnabled = true;
        private bool _largeTextEnabled;
        private bool _subtitlesEnabled = true;
        private bool _captionsEnabled = true;
        private float _uiScale = 1f;
        private float _masterVolume = 1f;
        private float _musicVolume = 0.7f;
        private float _effectsVolume = 1f;
        private bool _settingsPauseOwned;
        private int _lastUiScaleScreenHeight = -1;
        private bool _criticalDefenseCueShown;
        private int _bossWarningWave = -1;
        private float _nextHitFeedbackAudioTime;
        private float _nextCriticalHitFeedbackAudioTime;
        private float _nextBossDamageFeedbackAudioTime;
        private float _nextSlowFeedbackAudioTime;
        private float _nextArmorBreakFeedbackAudioTime;
        private float _nextLeakFeedbackAudioTime;
        private float _nextTowerFireAudioTime;
        private int _p121FixtureEnemyAnimationCount;
        private int _p121FixtureTowerAnimationCount;
        private int _p121FixtureEnemyMotionCount;
        private int _p121FixtureTowerMotionCount;
        private bool _p133FixtureActive;
        private TDFirstRunTutorialStep _tutorialStep = TDFirstRunTutorialStep.Complete;
        private bool _tutorialVisible;
        private float _tutorialRangeInspectTimer;
        private bool _tutorialSessionTracked;
        private bool _tutorialSessionEnded;
        private readonly Dictionary<Text, int> _baseUiFontSizes = new();

        private bool _gameOver;
        private bool _victory;
        private bool _runSummaryLogged;
        private bool _campaignResultRecorded;
        private bool _campaignDeploymentConfirmed;
        private bool _missionBoardOpen;
        private bool _missionBoardNeedsRefresh;
        private int _missionBoardSelectedLevel = DefaultCampaignLevelIndex;
        private int _missionBoardSelectedChapter;
        private bool _formationPanelOpen;
        private int _formationDraftLevel = DefaultCampaignLevelIndex;
        private TDResonanceDoctrine _formationDraftDoctrine = TDResonanceDoctrine.Adaptive;
        private TDCampaignDifficultyTier _formationDraftDifficulty = TDCampaignDifficultyTier.Standard;
        private string _formationDraftProtocolId = "baseline";
        private TDResonanceDoctrine _activeResonanceDoctrine = TDResonanceDoctrine.Adaptive;
        private int _currentMissionStars;
        private bool _currentMissionContractCompleted;
        private bool _contractFeedbackInitialized;
        private bool _contractFeedbackTargetMet;
        private float _nextContractFeedbackTime;
        private TDCampaignProgressUpdate _campaignProgressUpdate;
        private TDCampaignChapterRewardDefinition _newlyClaimedChapterReward;
        private int _defenseBudget = DefaultDefenseBudget;
        private int _lineIntegrity = DefaultLineIntegrity;
        private int _wave;
        private int _totalKills;
        private int _totalEscapes;
        private int _wavesCleared;
        private float _prepCountdown;
        private float _prepDuration;
        private bool _isInPrepPhase;
        private bool _waveStartRequested;
        private bool _openingGuideShown;
        private int _builtTowerCount;
        private string _currentWaveHint = "-";
        private string _currentWavePhase = "-";
        private string _currentWaveGoalTag = "-";
        private string _currentWaveThreatTags = "-";
        private float _currentWaveBudgetExpected;
        private float _currentWaveBudgetActual;
        private bool _currentWaveBudgetInRange = true;
        private bool _waveDispatchedEarly;
        private int _earlyDispatchCount;
        private string _lastStatus = "-";
        private float _statusTimer;
        private TDTowerKind _selectedTowerKind = TDTowerKind.RailLancer;
        private TDTowerUpgradeBranch _selectedUpgradeBranch = TDTowerUpgradeBranch.Damage;
        private float _resonanceCharge;
        private float _resonanceWindowTimer;
        private TDResonanceCommand _activeResonanceCommand = TDResonanceCommand.None;
        private int _resonanceWindowsTriggered;
        private int _resonanceCommandsUsed;
        private int _resonanceMatchedCommands;
        private float _resonanceBonusDamage;
        private int _emberSurgeUses;
        private int _fractureMarkUses;
        private int _resonanceChainMatchStreak;
        private int _resonanceChainBonusTriggers;
        private int _resonanceChainBudgetBonusTotal;
        private int _resonanceChainIntegrityBonusTotal;
        private int _doctrineEmpoweredCommands;
        private int _spawnSplitEvents;
        private int _attritionPenaltyEvents;
        private int _runtimeSpawnIndex;
        private float _resonanceMarkPulseTimer;
        private int _totalDamageDealt;
        private int _totalIntegrityDamageTaken;
        private int _counterOpportunityDamage;
        private int _counterMatchedDamage;
        private int _matrixOpportunities;
        private int _matrixTraitMatches;
        private int _matrixResonanceMatches;
        private int _matrixFullMatches;
        private int _matrixWindowSync;
        private int _matrixBestWindowSync;
        private int _matrixBestWindowSpecializations;
        private bool _matrixConvergenceTriggeredThisWindow;
        private int _matrixConvergenceTriggers;
        private int _matrixEmberConvergenceTriggers;
        private int _matrixFractureConvergenceTriggers;
        private int _matrixFractureConvergenceAffectedTargets;
        private float _matrixEmberConvergenceWindowSeconds;
        private int _budgetSpentOnBuilds;
        private int _budgetSpentOnUpgrades;
        private int _upgradesPurchased;
        private int _codexDiscoveriesThisRun;
        private int _lastWaveStartReadinessScore;
        private string _lastWaveStartReadinessGrade = "-";
        private GUIStyle _hudPanelStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudTextStyle;
        private GUIStyle _hudStatusStyle;
        private GUIStyle _hudMetricLabelStyle;
        private GUIStyle _hudMetricValueStyle;
        private GUIStyle _hudButtonStyle;
        private GUIStyle _hudGuideStyle;
        private Texture2D _hudPanelTexture;
        private Texture2D _hudPanelBgTexture;
        private Texture2D _hudPanelTitleTexture;
        private Texture2D _hudStatusStripTexture;
        private Texture2D _hudButtonTexture;
        private Texture2D _hudIconWaveTexture;
        private Texture2D _hudIconIntegrityTexture;
        private Texture2D _hudIconBudgetTexture;
        private Transform _rangePreviewRoot;
        private SpriteRenderer _rangePreviewRenderer;
        private Sprite _rangePreviewSprite;
        private TDWaveDefinition _currentWaveDefinition;
        private TDTower _hoveredTower;
        private TDTower _selectedTowerForUi;
        private Font _uiFont;
        private Canvas _battleCanvas;
        private TDTitleScreen _titleScreen;
        private TDPauseMenu _pauseMenu;
        private TDLoadingScreen _loadingScreen;
        private TDMissionBriefing _missionBriefing;
        private static bool _skipTitleForAutomation;
        private static bool _showBriefingNextAwake;
        private CanvasScaler _battleCanvasScaler;
        private TDP123SettingsPanel _settingsPanel;
        private RectTransform _uiTopPanel;
        private Text _uiTitleText;
        private Text _uiCampaignText;
        private Text _uiWaveMetricText;
        private Text _uiIntegrityMetricText;
        private Text _uiBudgetMetricText;
        private readonly Dictionary<string, Sprite> _uiSpriteCache = new();
        private Text _uiSelectedText;
        private Text _uiPrepText;
        private Text _uiGuideText;
        private Text _uiMissionContractText;
        private Text _uiResonanceText;
        private Image _uiResonanceFill;
        private RectTransform _uiResonanceCommandPanel;
        private Text _uiResonanceCommandTitleText;
        private Text _uiResonanceCommandForecastText;
        private Button _uiEmberCommandButton;
        private Text _uiEmberCommandButtonText;
        private Image _uiEmberCommandButtonImage;
        private Button _uiFractureCommandButton;
        private Text _uiFractureCommandButtonText;
        private Image _uiFractureCommandButtonImage;
        private Text _uiStatusText;
        private Button _uiStartWaveButton;
        private Text _uiStartWaveButtonText;
        private Image _uiStartWaveButtonImage;
        private RectTransform _uiScenarioPanel;
        private Text _uiScenarioTitleText;
        private Text _uiScenarioBodyText;
        private Button _uiScenarioCommandButton;
        private Text _uiScenarioCommandButtonText;
        private RectTransform _uiWaveIntelPanel;
        private Text _uiWaveIntelTitleText;
        private Text _uiWaveIntelBodyText;
        private Text _uiWaveIntelEnemyText;
        private Text _uiWaveIntelProfileText;
        private Text _uiWaveIntelRouteText;
        private Text _uiWaveIntelCounterText;
        private Text _uiWaveIntelReadinessText;
        private RectTransform _uiEventFeedRoot;
        private Text _uiEventFeedText;
        private RectTransform _uiTowerBarRoot;
        private readonly List<Button> _uiTowerButtons = new();
        private readonly List<Text> _uiTowerButtonTexts = new();
        private readonly List<Image> _uiTowerButtonIcons = new();
        private readonly List<Image> _uiTowerButtonAccents = new();
        private readonly List<Outline> _uiTowerButtonOutlines = new();
        private RectTransform _uiTowerPanelRoot;
        private Image _uiTowerIdentityIcon;
        private Image _uiTowerIdentityStripe;
        private Text _uiTowerTitleText;
        private Text _uiTowerStatsText;
        private Text _uiTowerUpgradeText;
        private Text _uiTowerPreviewText;
        private Button _uiDamageUpgradeButton;
        private Text _uiDamageUpgradeButtonText;
        private Button _uiUtilityUpgradeButton;
        private Text _uiUtilityUpgradeButtonText;
        private RectTransform _uiGameOverScrim;
        private RectTransform _uiGameOverRoot;
        private Text _uiGameOverTitleText;
        private Text _uiGameOverBodyText;
        private Text _uiGameOverScoreText;
        private Text _uiGameOverLaneText;
        private Text _uiGameOverTowerText;
        private RectTransform _uiGameOverScoreChartRoot;
        private RectTransform _uiGameOverLaneChartRoot;
        private RectTransform _uiGameOverTowerChartRoot;
        private readonly List<Image> _uiGameOverScoreBarFills = new();
        private readonly List<Text> _uiGameOverScoreBarValues = new();
        private readonly List<Image> _uiGameOverLaneBarFills = new();
        private readonly List<RectTransform> _uiGameOverLaneBarRows = new();
        private readonly List<Text> _uiGameOverLaneBarLabels = new();
        private readonly List<Text> _uiGameOverLaneBarValues = new();
        private readonly List<Image> _uiGameOverTowerBarFills = new();
        private readonly List<RectTransform> _uiGameOverTowerBarRows = new();
        private readonly List<Text> _uiGameOverTowerBarLabels = new();
        private readonly List<Text> _uiGameOverTowerBarValues = new();
        private Text _uiGameOverHeatText;
        private Text _uiGameOverFailureText;
        private Text _uiGameOverRecapText;
        private Text _uiGameOverRecommendationText;
        private Button _uiRestartButton;
        private Text _uiRestartButtonText;
        private Button _uiResultMissionButton;
        private Button _uiNextMissionButton;
        private Text _uiNextMissionButtonText;
        private Button _uiMissionButton;
        private Button _uiSettingsButton;
        private RectTransform _uiMissionBoardScrim;
        private RectTransform _uiMissionBoardRoot;
        private Text _uiMissionBoardTitleText;
        private Text _uiMissionBoardProgressText;
        private Text _uiMissionIntelTitleText;
        private Text _uiMissionIntelBriefText;
        private Text _uiMissionIntelThreatText;
        private Text _uiMissionIntelContractText;
        private Text _uiMissionIntelCounterText;
        private Text _uiMissionIntelRecordText;
        private Text _uiMissionChapterOverviewText;
        private Text _uiMissionChapterRewardText;
        private Button _uiMissionCloseButton;
        private Text _uiMissionCloseButtonText;
        private Button _uiMissionDeployButton;
        private Text _uiMissionDeployButtonText;
        private readonly List<Button> _uiMissionLevelButtons = new();
        private readonly List<Text> _uiMissionLevelButtonTexts = new();
        private TDWorldMap _worldMap;
        private readonly List<Button> _uiMissionChapterButtons = new();
        private readonly List<Text> _uiMissionChapterTitleTexts = new();
        private readonly List<Text> _uiMissionChapterProgressTexts = new();
        private readonly List<Button> _uiMissionChapterRewardButtons = new();
        private readonly List<Text> _uiMissionChapterRewardButtonTexts = new();
        private Button _uiCampaignProfileButton;
        private RectTransform _uiFormationRoot;
        private Text _uiFormationTitleText;
        private Text _uiFormationThreatText;
        private Text _uiFormationRosterText;
        private Text _uiFormationFitTitleText;
        private Text _uiFormationFitBodyText;
        private Text _uiFormationMatrixText;
        private Text _uiFormationLockText;
        private Text _uiFormationDifficultyText;
        private Button _uiFormationAutoFitButton;
        private Button _uiFormationBackButton;
        private Button _uiFormationDeployButton;
        private Text _uiFormationDeployButtonText;
        private readonly List<Button> _uiFormationTowerButtons = new();
        private readonly List<Text> _uiFormationTowerButtonTexts = new();
        private readonly List<Image> _uiFormationTowerIcons = new();
        private readonly List<Image> _uiFormationTowerAccents = new();
        private readonly List<Outline> _uiFormationTowerOutlines = new();
        private readonly List<Button> _uiFormationDoctrineButtons = new();
        private readonly List<Text> _uiFormationDoctrineButtonTexts = new();
        private readonly List<Button> _uiFormationDifficultyButtons = new();
        private readonly List<Text> _uiFormationDifficultyButtonTexts = new();
        private readonly List<Button> _uiFormationProtocolButtons = new();
        private readonly List<Text> _uiFormationProtocolButtonTexts = new();
        private RectTransform _uiCampaignProfileRoot;
        private Text _uiCampaignProfileTitleText;
        private Text _uiCampaignProfileSummaryText;
        private Text _uiCampaignProfileChapterText;
        private Text _uiCampaignProfileBonusText;
        private Text _uiCampaignProfileSaveText;
        private Text _uiCampaignProfileStatusText;
        private Button _uiCampaignProfileCopyButton;
        private Button _uiCampaignProfileImportButton;
        private Text _uiCampaignProfileImportButtonText;
        private Button _uiCampaignProfileResetButton;
        private Text _uiCampaignProfileResetButtonText;
        private readonly List<Button> _uiCampaignProfileSlotButtons = new();
        private readonly List<Text> _uiCampaignProfileSlotButtonTexts = new();
        private Button _uiCampaignProfileCloudCopyButton;
        private Button _uiCampaignProfileCloudMergeButton;
        private Button _uiCampaignProfileBackButton;
        private bool _campaignProfileOpen;
        private bool _campaignProfileImportArmed;
        private bool _campaignProfileResetArmed;
        private string _campaignProfilePendingImport = string.Empty;
        private string _campaignClipboardBuffer = string.Empty;
        private string _campaignProfileStatus = "PROFILE READY";
        private Transform _routePreviewRoot;
        private Material _routePreviewMaterial;
        private bool _debugRoutePreviewVisible;

        public bool IsGameOver => _gameOver;
        public float CellWorldSize => CellSize;

        private void Awake()
        {
            TDLocalization.Initialize();
            LoadP123PresentationPreferences();
            ConfigureCamera();
            ConfigureSfx();
            EnsureObjectPool();
            LoadCampaignContext();
            BuildBoard();
            LoadEnemyCatalog();
            LoadWaveConfig();
            RefreshUnlockedTowerKinds();
            BuildBattleUi();
        }

        private void EnsureObjectPool()
        {
            if (GetComponent<TDObjectPool>() == null)
            {
                gameObject.AddComponent<TDObjectPool>();
            }
        }

        private void Start()
        {
            _waveRoutine = StartCoroutine(_waveSet != null ? WaveLoopFromConfig() : FallbackWaveLoop());

            // Show the mission briefing if the deploy flow requested it.
            if (_showBriefingNextAwake)
            {
                _showBriefingNextAwake = false;
                ShowMissionBriefing();
            }
        }

        private void OnDestroy()
        {
            if (_tutorialSessionTracked && !_tutorialSessionEnded && _tutorialVisible)
            {
                IncrementTutorialTelemetry("dropoff");
                _tutorialSessionEnded = true;
            }
        }

        private void Update()
        {
            _settingsPanel?.Tick();
            UpdateMusicState();
            UpdateP124Autoplay();
            UpdateP1254ContinuousSoak();
            if (_titleScreen != null && _titleScreen.IsVisible)
            {
                // Title screen is covering everything — skip combat input and HUD.
                _gridMap?.HideBuildPreview();
                HideRangePreview();
                return;
            }

            if (_missionBriefing != null && _missionBriefing.IsVisible)
            {
                // Briefing is up — block combat input until player clicks BEGIN.
                _gridMap?.HideBuildPreview();
                HideRangePreview();
                return;
            }

            if (_pauseMenu != null && _pauseMenu.IsVisible)
            {
                // If settings is open on top of pause menu, ESC closes settings first.
                if (_settingsPanel != null && _settingsPanel.IsOpen)
                {
                    if (!_settingsPanel.IsRebinding && TDInputBindings.GetKeyDown(TDInputAction.Settings))
                    {
                        _settingsPanel.Close();
                    }

                    return;
                }

                // ESC or P closes the pause menu (resumes).
                if (TDInputBindings.GetKeyDown(TDInputAction.Settings) ||
                    TDInputBindings.GetKeyDown(TDInputAction.Pause) ||
                    TDInputCompat.GetGamepadButtonDown(TDGamepadButton.Start))
                {
                    HandlePauseResume();
                }

                return;
            }

            if (_settingsPanel != null && _settingsPanel.IsOpen)
            {
                if (!_settingsPanel.IsRebinding && TDInputBindings.GetKeyDown(TDInputAction.Settings))
                {
                    _settingsPanel.Close();
                }

                return;
            }

            if (TDInputBindings.GetKeyDown(TDInputAction.Settings) || TDInputCompat.GetGamepadButtonDown(TDGamepadButton.Select))
            {
                // ESC opens the pause menu during combat (settings accessible from there).
                TogglePauseMenu();
                return;
            }

            HandlePlaybackHotkeys();

            if (_gameOver)
            {
                _gridMap?.HideBuildPreview();
                HideRangePreview();
                if (TDInputCompat.GetKeyDown(KeyCode.R))
                {
                    RestartCurrentScene();
                }

                return;
            }

            if (_missionBoardOpen)
            {
                _gridMap?.HideBuildPreview();
                HideRangePreview();
                if (TDInputCompat.GetGamepadButtonDown(TDGamepadButton.East))
                {
                    if (_formationPanelOpen)
                    {
                        CloseFormationPanel();
                    }
                    else if (_campaignProfileOpen)
                    {
                        CloseCampaignProfile();
                    }
                    else if (_uiMissionCloseButton == null || _uiMissionCloseButton.interactable)
                    {
                        CloseMissionBoard();
                    }
                }

                return;
            }

            EnsureGamepadFocus();
            HandleHotkeys();
            UpdateResonanceState();
            UpdateScenarioBossPhases();

            var pointerOverUi = IsPointerOverBattleUi();
            if (!pointerOverUi && TDInputCompat.GetMouseButtonDown(0))
            {
                TryPlaceTowerAtCursor();
            }

            if (!pointerOverUi && TDInputCompat.GetMouseButtonDown(1))
            {
                TryUpgradeTowerAtCursor();
            }

            UpdateBuildPreviewUnderCursor();

            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f)
                {
                    _lastStatus = "-";
                }
            }

            UpdateTacticalEventTimers();
        }

        private void LateUpdate()
        {
            RefreshUiScaleForScreen();
            UpdateMissionContractFeedback();
            UpdateBattleUi();
            UpdateRoutePreview();
            UpdateFirstRunTutorial();
            _battlePresentation?.Tick(_missionBoardOpen || _formationPanelOpen || _campaignProfileOpen ||
                                      (_settingsPanel != null && _settingsPanel.IsOpen) || _gameOver);
        }

        private void OnGUI()
        {
            if (UseRuntimeBattleUi)
            {
                return;
            }

            EnsureHudStyles();

            var hudScale = Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), 0.78f, 1.08f);
            ApplyHudScale(hudScale);

            var panelWidth = Mathf.Min(680f * hudScale, Mathf.Max(360f, Screen.width - 36f));
            var panelHeight = 344f * hudScale;
            var panelRect = new Rect(18f, 18f, panelWidth, panelHeight);
            DrawHudPanel(panelRect, hudScale);

            var pad = 16f * hudScale;
            var left = panelRect.x + pad;
            var contentWidth = panelRect.width - (pad * 2f);

            var y = panelRect.y + (10f * hudScale);
            DrawShadowedLabel(
                new Rect(left, y, contentWidth, 24f * hudScale),
                "Emberline Defense",
                _hudTitleStyle,
                new Color(0.90f, 0.97f, 1f, 1f),
                new Color(0f, 0f, 0f, 0.52f));
            y += 30f * hudScale;

            DrawShadowedLabel(
                new Rect(left, y, contentWidth, 16f * hudScale),
                GetCampaignHudLabel(),
                _hudTextStyle,
                new Color(0.80f, 0.90f, 0.98f, 1f),
                new Color(0f, 0f, 0f, 0.44f));
            y += 20f * hudScale;

            var metricHeight = 38f * hudScale;
            var metricGap = 10f * hudScale;
            var metricWidth = (contentWidth - (metricGap * 2f)) / 3f;
            DrawHudMetric(new Rect(left, y, metricWidth, metricHeight), _hudIconWaveTexture, "WAVE", _wave.ToString());
            DrawHudMetric(new Rect(left + metricWidth + metricGap, y, metricWidth, metricHeight), _hudIconIntegrityTexture, "INTEGRITY", _lineIntegrity.ToString());
            DrawHudMetric(new Rect(left + ((metricWidth + metricGap) * 2f), y, metricWidth, metricHeight), _hudIconBudgetTexture, "BUDGET", _defenseBudget.ToString());
            y += 46f * hudScale;

            DrawShadowedLabel(
                new Rect(left, y, contentWidth, 18f * hudScale),
                GetBuildHotkeySummary(),
                _hudTextStyle,
                new Color(0.85f, 0.92f, 0.97f, 1f),
                new Color(0f, 0f, 0f, 0.44f));
            y += 19f * hudScale;

            DrawShadowedLabel(
                new Rect(left, y, contentWidth, 18f * hudScale),
                $"Selected {GetTowerKindLabel(_selectedTowerKind)}   Upgrade {GetUpgradeBranchLabel(_selectedUpgradeBranch)} (Q/E)",
                _hudTextStyle,
                new Color(0.84f, 0.92f, 0.97f, 1f),
                new Color(0f, 0f, 0f, 0.44f));
            y += 19f * hudScale;

            DrawShadowedLabel(
                new Rect(left, y, contentWidth, 18f * hudScale),
                $"LMB Place   RMB Upgrade   Space Start Wave   F5/F6 Level   R Restart   {(_isResonanceSystemEnabled ? "Z/X Resonance" : $"Resonance L{_resonanceEnabledFromLevel:00}+")}",
                _hudTextStyle,
                new Color(0.84f, 0.92f, 0.97f, 1f),
                new Color(0f, 0f, 0f, 0.44f));
            y += 22f * hudScale;

            var startButtonWidth = 138f * hudScale;
            var startButtonRect = new Rect(left + contentWidth - startButtonWidth, y, startButtonWidth, 34f * hudScale);
            DrawShadowedLabel(
                new Rect(left, y, contentWidth - startButtonWidth - (12f * hudScale), 34f * hudScale),
                GetPrepHudLabel(),
                _hudTextStyle,
                new Color(0.84f, 0.92f, 0.97f, 1f),
                new Color(0f, 0f, 0f, 0.44f));
            DrawStartWaveButton(startButtonRect, hudScale);
            y += 40f * hudScale;

            DrawShadowedLabel(
                new Rect(left, y, contentWidth, 40f * hudScale),
                GetGuideHudLabel(),
                _hudGuideStyle,
                new Color(0.98f, 0.91f, 0.72f, 1f),
                new Color(0f, 0f, 0f, 0.48f));
            y += 44f * hudScale;

            DrawShadowedLabel(
                new Rect(left, y, contentWidth, 18f * hudScale),
                GetResonanceHudLabel(),
                _hudTextStyle,
                new Color(0.98f, 0.91f, 0.75f, 1f),
                new Color(0f, 0f, 0f, 0.44f));
            y += 17f * hudScale;

            var resonanceBarRect = new Rect(left, y, contentWidth, 10f * hudScale);
            DrawResonanceBar(resonanceBarRect, _resonanceCharge / ResonanceChargeMax);

            var statusRect = new Rect(left, panelRect.yMax - (31f * hudScale), contentWidth, 24f * hudScale);
            if (_hudStatusStripTexture != null)
            {
                DrawTexture(statusRect, _hudStatusStripTexture, 0.90f);
            }

            DrawShadowedLabel(
                new Rect(statusRect.x + (10f * hudScale), statusRect.y + (1f * hudScale), statusRect.width - (20f * hudScale), statusRect.height),
                $"Status {_lastStatus}",
                _hudStatusStyle,
                new Color(0.98f, 0.92f, 0.78f, 1f),
                new Color(0f, 0f, 0f, 0.52f));

            if (!string.IsNullOrWhiteSpace(_loadError))
            {
                DrawShadowedLabel(
                    new Rect(statusRect.x + (10f * hudScale), statusRect.y - (16f * hudScale), statusRect.width - (20f * hudScale), 16f * hudScale),
                    $"Wave Config {_loadError}",
                    _hudTextStyle,
                    new Color(1f, 0.62f, 0.56f, 0.98f),
                    new Color(0f, 0f, 0f, 0.52f));
            }

            if (!_gameOver)
            {
                return;
            }

            UpdateRunResultResponsiveScale();

            var width = 400f * hudScale;
            var height = 244f * hudScale;
            var x = (Screen.width - width) * 0.5f;
            var y2 = (Screen.height - height) * 0.5f;
            var gameOverRect = new Rect(x, y2, width, height);

            DrawHudPanel(gameOverRect, hudScale);
            DrawShadowedLabel(
                new Rect(x + (22f * hudScale), y2 + (18f * hudScale), width - (44f * hudScale), 28f * hudScale),
                _victory ? "Mission Complete" : "Game Over",
                _hudTitleStyle,
                new Color(0.95f, 0.97f, 1f, 1f),
                new Color(0f, 0f, 0f, 0.52f));
            DrawShadowedLabel(
                new Rect(x + (22f * hudScale), y2 + (52f * hudScale), width - (44f * hudScale), 20f * hudScale),
                _victory ? $"You cleared wave set at wave {_wave}." : $"You survived to wave {_wave}.",
                _hudTextStyle,
                new Color(0.86f, 0.93f, 0.98f, 1f),
                new Color(0f, 0f, 0f, 0.45f));
            DrawShadowedLabel(
                new Rect(x + (22f * hudScale), y2 + (74f * hudScale), width - (44f * hudScale), 20f * hudScale),
                $"Cleared {_wavesCleared}/{GetConfiguredWaveCount()}   Kills {_totalKills}   Escapes {_totalEscapes}",
                _hudTextStyle,
                new Color(0.86f, 0.93f, 0.98f, 1f),
                new Color(0f, 0f, 0f, 0.45f));
            DrawShadowedLabel(
                new Rect(x + (22f * hudScale), y2 + (96f * hudScale), width - (44f * hudScale), 20f * hudScale),
                $"Failure Tags {GetTopFailureReasonSummary()}",
                _hudTextStyle,
                new Color(0.96f, 0.90f, 0.72f, 1f),
                new Color(0f, 0f, 0f, 0.45f));
            DrawShadowedLabel(
                new Rect(x + (22f * hudScale), y2 + (118f * hudScale), width - (44f * hudScale), 20f * hudScale),
                "Press R or use the button below.",
                _hudTextStyle,
                new Color(0.86f, 0.93f, 0.98f, 1f),
                new Color(0f, 0f, 0f, 0.45f));

            var restartRect = new Rect(x + (20f * hudScale), y2 + (154f * hudScale), width - (40f * hudScale), 62f * hudScale);
            if (_hudButtonTexture != null)
            {
                DrawTexture(restartRect, _hudButtonTexture, 1f);
            }

            if (GUI.Button(restartRect, "Restart", _hudButtonStyle))
            {
                RestartCurrentScene();
            }
        }

        private void BuildBattleUi()
        {
            if (!UseRuntimeBattleUi || _battleCanvas != null)
            {
                return;
            }

            EnsureUiEventSystem();
            _uiFont = TDUiWorldSkin.ResolveFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

            var canvasObject = new GameObject("TD Battle UI");
            canvasObject.transform.SetParent(transform, false);

            _battleCanvas = canvasObject.AddComponent<Canvas>();
            _battleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _battleCanvas.sortingOrder = 100;

            _battleCanvasScaler = canvasObject.AddComponent<CanvasScaler>();
            _battleCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var effectiveUiScale = GetEffectiveUiScale();
            _battleCanvasScaler.referenceResolution = new Vector2(1440f / effectiveUiScale, 900f / effectiveUiScale);
            _lastUiScaleScreenHeight = Screen.height;
            _battleCanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _battleCanvasScaler.matchWidthOrHeight = 0.48f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var root = canvasObject.transform;
            _uiTopPanel = CreateUiPanel("Primary HUD", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(400f, 122f), new Color(0.025f, 0.032f, 0.036f, 0.88f));
            AddUiPanelChrome(_uiTopPanel, new Color(0.96f, 0.62f, 0.18f, 0.94f));
            _uiTitleText = CreateUiText("Title", _uiTopPanel, new Vector2(12f, -8f), new Vector2(104f, 20f), "EMBERLINE", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.46f, 1f));
            _uiMissionButton = CreateUiButton("Mission Board Button", _uiTopPanel, new Vector2(266f, -6f), new Vector2(78f, 24f), "MISSIONS", 10, OpenMissionBoard);
            _uiSettingsButton = CreateUiButton("Settings Button", _uiTopPanel, new Vector2(352f, -6f), new Vector2(36f, 24f), string.Empty, 10, ToggleSettingsPanel);
            AddUiButtonIcon(_uiSettingsButton, "Settings Pause Icon", TDUiVisualIdentity.PauseIconPath, new Vector2(8f, -3f), new Vector2(20f, 20f), 0f);
            _uiCampaignText = CreateUiText("Campaign", _uiTopPanel, new Vector2(116f, -9f), new Vector2(140f, 18f), string.Empty, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.70f, 0.79f, 0.84f, 1f));

            _uiWaveMetricText = CreateUiMetric("Wave Metric", _uiTopPanel, new Vector2(12f, -34f), new Vector2(116f, 38f), new Color(0.16f, 0.27f, 0.32f, 0.96f), new Color(0.76f, 0.94f, 1f, 1f), TDUiP132Art.IconPath(TDUiP132Icon.Wave));
            _uiIntegrityMetricText = CreateUiMetric("Integrity Metric", _uiTopPanel, new Vector2(138f, -34f), new Vector2(116f, 38f), new Color(0.24f, 0.27f, 0.20f, 0.96f), new Color(0.84f, 0.98f, 0.72f, 1f), TDUiP132Art.IconPath(TDUiP132Icon.Integrity));
            _uiBudgetMetricText = CreateUiMetric("Budget Metric", _uiTopPanel, new Vector2(264f, -34f), new Vector2(116f, 38f), new Color(0.32f, 0.24f, 0.13f, 0.96f), new Color(1f, 0.86f, 0.46f, 1f), TDUiP132Art.IconPath(TDUiP132Icon.Budget));
            _uiSelectedText = CreateUiText("Selected Tower", _uiTopPanel, new Vector2(12f, -80f), new Vector2(220f, 18f), string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.80f, 0.90f, 0.97f, 1f));
            _uiPrepText = CreateUiText("Prep State", _uiTopPanel, new Vector2(238f, -80f), new Vector2(142f, 18f), string.Empty, 11, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.90f, 0.96f, 1f, 1f));
            _uiStartWaveButton = CreateUiButton("Start Wave Button", root, Vector2.zero, new Vector2(156f, 46f), "START WAVE", 14, () => TryRequestWaveStart());
            SetUiBottomRightLayout(_uiStartWaveButton.transform as RectTransform, new Vector2(-18f, 252f));
            _uiStartWaveButtonText = _uiStartWaveButton.GetComponentInChildren<Text>();
            _uiStartWaveButtonImage = _uiStartWaveButton.GetComponent<Image>();
            AddUiButtonIcon(_uiStartWaveButton, "Start Wave Icon", TDUiP132Art.IconPath(TDUiP132Icon.Wave), new Vector2(10f, -9f), new Vector2(28f, 28f), 34f);
            _uiGuideText = CreateUiText("Guide", _uiTopPanel, Vector2.zero, Vector2.zero, string.Empty, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.90f, 0.68f, 1f));
            _uiMissionContractText = CreateUiText("Mission Contract", _uiTopPanel, Vector2.zero, Vector2.zero, string.Empty, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.76f, 0.96f, 0.82f, 1f));
            _uiResonanceText = CreateUiText("Resonance Label", _uiTopPanel, new Vector2(12f, -98f), new Vector2(368f, 18f), string.Empty, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.78f, 0.38f, 1f));
            var resonanceTrack = CreateUiPanel("Resonance Track", _uiTopPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -117f), new Vector2(368f, 4f), new Color(1f, 1f, 1f, 0.12f));
            _uiResonanceFill = CreateUiImage("Resonance Fill", resonanceTrack, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 4f), new Color(1f, 0.56f, 0.12f, 0.96f));
            _uiStatusText = CreateUiText("Status", _uiTopPanel, Vector2.zero, Vector2.zero, string.Empty, 10, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.86f, 0.92f, 0.98f, 1f));
            _uiGuideText.gameObject.SetActive(false);
            _uiMissionContractText.gameObject.SetActive(false);
            _uiStatusText.gameObject.SetActive(false);

            _uiResonanceCommandPanel = CreateUiPanel("Resonance Command Panel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(410f, 116f), new Color(0.055f, 0.043f, 0.030f, 0.94f));
            AddUiPanelChrome(_uiResonanceCommandPanel, new Color(1f, 0.52f, 0.12f, 1f));
            _uiResonanceCommandTitleText = CreateUiText("Resonance Command Title", _uiResonanceCommandPanel, new Vector2(12f, -8f), new Vector2(386f, 20f), string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.58f, 1f));
            _uiResonanceCommandForecastText = CreateUiText("Resonance Command Forecast", _uiResonanceCommandPanel, new Vector2(12f, -31f), new Vector2(386f, 36f), string.Empty, 10, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.82f, 0.92f, 0.98f, 1f));
            _uiEmberCommandButton = CreateUiButton("Ember Command Button", _uiResonanceCommandPanel, new Vector2(12f, -72f), new Vector2(186f, 34f), "Ember Surge", 10, () => TrySelectResonanceCommand(TDResonanceCommand.EmberSurge));
            _uiEmberCommandButtonText = _uiEmberCommandButton.GetComponentInChildren<Text>();
            _uiEmberCommandButtonImage = _uiEmberCommandButton.GetComponent<Image>();
            AddUiButtonIcon(_uiEmberCommandButton, "Ember Command Icon", TDUiP132Art.IconPath(TDUiP132Icon.EmberCommand), new Vector2(7f, -5f), new Vector2(24f, 24f), 28f);
            _uiFractureCommandButton = CreateUiButton("Fracture Command Button", _uiResonanceCommandPanel, new Vector2(212f, -72f), new Vector2(186f, 34f), "Fracture Mark", 10, () => TrySelectResonanceCommand(TDResonanceCommand.FractureMark));
            _uiFractureCommandButtonText = _uiFractureCommandButton.GetComponentInChildren<Text>();
            _uiFractureCommandButtonImage = _uiFractureCommandButton.GetComponent<Image>();
            AddUiButtonIcon(_uiFractureCommandButton, "Fracture Command Icon", TDUiP132Art.IconPath(TDUiP132Icon.FractureCommand), new Vector2(7f, -5f), new Vector2(24f, 24f), 28f);
            _uiResonanceCommandPanel.gameObject.SetActive(false);

            _uiScenarioPanel = CreateUiPanel("Scenario Mechanic", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -260f), new Vector2(330f, 92f), new Color(0.035f, 0.050f, 0.040f, 0.90f));
            AddUiPanelChrome(_uiScenarioPanel, new Color(0.36f, 0.90f, 0.56f, 0.94f));
            _uiScenarioTitleText = CreateUiText("Scenario Mechanic Title", _uiScenarioPanel, new Vector2(12f, -8f), new Vector2(306f, 18f), string.Empty, 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.82f, 0.98f, 0.88f, 1f));
            _uiScenarioBodyText = CreateUiText("Scenario Mechanic Body", _uiScenarioPanel, new Vector2(12f, -30f), new Vector2(190f, 50f), string.Empty, 10, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.80f, 0.90f, 0.94f, 1f));
            _uiScenarioCommandButton = CreateUiButton("Scenario Mechanic Command", _uiScenarioPanel, new Vector2(210f, -34f), new Vector2(108f, 44f), "ACTIVATE", 10, TryActivateScenarioMechanic);
            _uiScenarioCommandButtonText = _uiScenarioCommandButton.GetComponentInChildren<Text>();
            AddUiButtonIcon(_uiScenarioCommandButton, "Scenario Command Icon", TDUiP132Art.IconPath(TDUiP132Icon.RouteSwitch), new Vector2(6f, -9f), new Vector2(26f, 26f), 29f);
            _uiScenarioPanel.gameObject.SetActive(false);

            _uiWaveIntelPanel = CreateUiPanel("Wave Intel", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(330f, 180f), new Color(0.030f, 0.038f, 0.043f, 0.88f));
            AddUiPanelChrome(_uiWaveIntelPanel, new Color(0.34f, 0.76f, 0.94f, 0.94f));
            _uiWaveIntelTitleText = CreateUiText("Wave Intel Title", _uiWaveIntelPanel, new Vector2(12f, -9f), new Vector2(306f, 20f), string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.86f, 0.96f, 1f, 1f));
            _uiWaveIntelBodyText = CreateUiText("Wave Intel Body", _uiWaveIntelPanel, new Vector2(12f, -32f), new Vector2(306f, 20f), string.Empty, 10, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.72f, 0.84f, 0.90f, 1f));
            _uiWaveIntelEnemyText = CreateUiText("Wave Intel Enemy", _uiWaveIntelPanel, new Vector2(12f, -56f), new Vector2(306f, 38f), string.Empty, 10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.86f, 0.54f, 1f));
            _uiWaveIntelProfileText = CreateUiText("Wave Intel Profile", _uiWaveIntelPanel, new Vector2(12f, -96f), new Vector2(306f, 34f), string.Empty, 10, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.86f, 0.96f, 1f, 1f));
            _uiWaveIntelRouteText = CreateUiText("Wave Intel Route", _uiWaveIntelPanel, new Vector2(12f, -132f), new Vector2(306f, 18f), string.Empty, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.78f, 0.34f, 1f));
            _uiWaveIntelCounterText = CreateUiText("Wave Intel Counter", _uiWaveIntelPanel, Vector2.zero, Vector2.zero, string.Empty, 10, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.74f, 0.94f, 0.84f, 1f));
            _uiWaveIntelReadinessText = CreateUiText("Wave Intel Readiness", _uiWaveIntelPanel, new Vector2(12f, -154f), new Vector2(306f, 18f), string.Empty, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.76f, 0.96f, 0.82f, 1f));
            _uiWaveIntelCounterText.gameObject.SetActive(false);

            _uiEventFeedRoot = CreateUiPanel("Tactical Feed", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 88f), new Vector2(392f, 42f), new Color(0.025f, 0.032f, 0.036f, 0.76f));
            AddUiPanelChrome(_uiEventFeedRoot, new Color(0.38f, 0.64f, 0.72f, 0.72f));
            CreateUiText("Tactical Feed Title", _uiEventFeedRoot, new Vector2(10f, -11f), new Vector2(72f, 18f), "TACTICAL", 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.64f, 0.84f, 0.92f, 1f));
            _uiEventFeedText = CreateUiText("Tactical Feed Body", _uiEventFeedRoot, new Vector2(88f, -8f), new Vector2(292f, 24f), string.Empty, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.90f, 0.94f, 0.96f, 1f));

            var towerBarWidth = Mathf.Clamp(58f + (Mathf.Max(1, _unlockedTowerKinds.Count) * 74f), 132f, 650f);
            _uiTowerBarRoot = CreateUiPanel("Tower Build Bar", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(towerBarWidth, 62f), new Color(0.025f, 0.032f, 0.036f, 0.88f));
            AddUiPanelChrome(_uiTowerBarRoot, new Color(0.96f, 0.62f, 0.18f, 0.88f));
            CreateUiSpriteImage("Tower Bar Icon", _uiTowerBarRoot, new Vector2(13f, -7f), new Vector2(30f, 30f), TDUiP132Art.IconPath(TDUiP132Icon.Build), Color.white);
            CreateUiText("Tower Bar Label", _uiTowerBarRoot, new Vector2(8f, -39f), new Vector2(40f, 13f), "BUILD", 10, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.86f, 0.88f, 0.86f, 1f));
            RebuildTowerBuildButtons();

            _uiTowerPanelRoot = CreateUiPanel("Tower Upgrade Panel", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(300f, 226f), new Color(0.028f, 0.036f, 0.040f, 0.92f));
            AddUiPanelChrome(_uiTowerPanelRoot, new Color(0.44f, 0.82f, 0.72f, 0.92f));
            _uiTowerTitleText = CreateUiText("Tower Title", _uiTowerPanelRoot, new Vector2(12f, -9f), new Vector2(222f, 22f), string.Empty, 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.90f, 0.97f, 1f, 1f));
            _uiTowerIdentityIcon = CreateUiSpriteImage("Tower Identity Icon", _uiTowerPanelRoot, new Vector2(246f, -8f), new Vector2(42f, 42f), TDUiVisualIdentity.GetTower(TDTowerKind.RailLancer).iconResourcePath, Color.white);
            _uiTowerIdentityStripe = CreateUiImage("Tower Identity Stripe", _uiTowerPanelRoot, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f), TDUiVisualIdentity.GetTower(TDTowerKind.RailLancer).accent);
            _uiTowerStatsText = CreateUiText("Tower Stats", _uiTowerPanelRoot, new Vector2(12f, -36f), new Vector2(276f, 54f), string.Empty, 10, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.80f, 0.90f, 0.97f, 1f));
            _uiTowerPreviewText = CreateUiText("Tower Upgrade Preview", _uiTowerPanelRoot, new Vector2(12f, -92f), new Vector2(276f, 42f), string.Empty, 10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.74f, 0.94f, 0.84f, 1f));
            _uiTowerUpgradeText = CreateUiText("Tower Upgrade Hint", _uiTowerPanelRoot, new Vector2(12f, -136f), new Vector2(276f, 34f), string.Empty, 10, FontStyle.Normal, TextAnchor.UpperLeft, new Color(1f, 0.82f, 0.46f, 1f));
            _uiDamageUpgradeButton = CreateUiButton("Damage Upgrade", _uiTowerPanelRoot, new Vector2(12f, -178f), new Vector2(132f, 36f), "Damage", 10, () => TryUpgradeSelectedTowerFromUi(TDTowerUpgradeBranch.Damage));
            _uiDamageUpgradeButtonText = _uiDamageUpgradeButton.GetComponentInChildren<Text>();
            AddUiButtonIcon(_uiDamageUpgradeButton, "Damage Branch Icon", TDUiP132Art.IconPath(TDUiP132Icon.Damage), new Vector2(7f, -6f), new Vector2(24f, 24f), 29f);
            _uiUtilityUpgradeButton = CreateUiButton("Utility Upgrade", _uiTowerPanelRoot, new Vector2(156f, -178f), new Vector2(132f, 36f), "Utility", 10, () => TryUpgradeSelectedTowerFromUi(TDTowerUpgradeBranch.Utility));
            _uiUtilityUpgradeButtonText = _uiUtilityUpgradeButton.GetComponentInChildren<Text>();
            AddUiButtonIcon(_uiUtilityUpgradeButton, "Utility Branch Icon", TDUiP132Art.IconPath(TDUiP132Icon.Resonance), new Vector2(7f, -6f), new Vector2(24f, 24f), 29f);
            _uiTowerPanelRoot.gameObject.SetActive(false);

            _uiGameOverScrim = CreateUiPanel("Run Result Scrim", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.005f, 0.008f, 0.012f, 0.82f));
            _uiGameOverRoot = CreateUiPanel("Run Result", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 620f), new Color(0.025f, 0.038f, 0.048f, 0.97f));
            AddUiPanelChrome(_uiGameOverRoot, TDUiWorldSkin.Brass);
            _uiGameOverTitleText = CreateUiText("Run Result Title", _uiGameOverRoot, new Vector2(22f, -14f), new Vector2(716f, 36f), string.Empty, 20, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.92f, 0.98f, 1f, 1f));
            _uiGameOverBodyText = CreateUiText("Run Result Body", _uiGameOverRoot, new Vector2(28f, -74f), new Vector2(704f, 20f), string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.82f, 0.91f, 0.97f, 1f));
            _uiGameOverScoreText = CreateUiText("Run Result Score", _uiGameOverRoot, new Vector2(28f, -98f), new Vector2(704f, 22f), string.Empty, 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.98f, 0.88f, 0.62f, 1f));
            BuildRunResultCharts();
            CreateUiSpriteImage("Run Result Lane Icon", _uiGameOverRoot, new Vector2(28f, -181f), new Vector2(24f, 24f), TDUiP132Art.IconPath(TDUiP132Icon.RouteSwitch), Color.white);
            _uiGameOverLaneText = CreateUiText("Run Result Lanes", _uiGameOverRoot, new Vector2(58f, -184f), new Vector2(308f, 18f), "LANE CONTROL", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.92f, 0.98f, 1f));
            CreateUiSpriteImage("Run Result Tower Icon", _uiGameOverRoot, new Vector2(394f, -181f), new Vector2(24f, 24f), TDUiP132Art.IconPath(TDUiP132Icon.Damage), Color.white);
            _uiGameOverTowerText = CreateUiText("Run Result Towers", _uiGameOverRoot, new Vector2(424f, -184f), new Vector2(308f, 18f), "TOWER CONTRIBUTION", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 0.86f, 1f));
            CreateUiSpriteImage("Run Result Hotspot Icon", _uiGameOverRoot, new Vector2(28f, -297f), new Vector2(26f, 26f), TDUiP132Art.IconPath(TDUiP132Icon.Hotspot), Color.white);
            _uiGameOverHeatText = CreateUiText("Run Result Heatmap", _uiGameOverRoot, new Vector2(58f, -296f), new Vector2(674f, 44f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperCenter, new Color(1.00f, 0.72f, 0.42f, 1f));
            _uiGameOverFailureText = CreateUiText("Run Result Failure", _uiGameOverRoot, new Vector2(28f, -344f), new Vector2(704f, 26f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.98f, 0.76f, 0.56f, 1f));
            _uiGameOverRecapText = CreateUiText("Run Result Recap", _uiGameOverRoot, new Vector2(28f, -374f), new Vector2(704f, 50f), string.Empty, 12, FontStyle.Normal, TextAnchor.UpperCenter, new Color(0.78f, 0.92f, 0.98f, 1f));
            _uiGameOverRecommendationText = CreateUiText("Run Result Recommendation", _uiGameOverRoot, new Vector2(28f, -430f), new Vector2(704f, 92f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperCenter, new Color(0.82f, 0.98f, 0.82f, 1f));
            _uiRestartButton = CreateUiButton("Restart Button", _uiGameOverRoot, new Vector2(84f, -552f), new Vector2(176f, 42f), "Retry", 14, RestartCurrentScene);
            _uiRestartButtonText = _uiRestartButton.GetComponentInChildren<Text>();
            _uiResultMissionButton = CreateUiButton("Result Mission Button", _uiGameOverRoot, new Vector2(292f, -552f), new Vector2(176f, 42f), "Missions", 14, OpenMissionBoard);
            _uiNextMissionButton = CreateUiButton("Next Mission Button", _uiGameOverRoot, new Vector2(500f, -552f), new Vector2(176f, 42f), "Next Mission", 14, GoToNextMission);
            _uiNextMissionButtonText = _uiNextMissionButton.GetComponentInChildren<Text>();
            _uiGameOverScrim.gameObject.SetActive(false);
            _uiGameOverRoot.gameObject.SetActive(false);

            BuildMissionBoardUi(root);
            BuildP9PresentationUi();
            BuildP123SettingsUi();

            UpdateBattleUi();
        }

        private void BuildP9PresentationUi()
        {
            if (_battleCanvas == null || _uiFont == null || _battlePresentation != null)
            {
                return;
            }

            _playbackSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(P9PlaybackSpeedKey, 1f), 1f, 3f);
            _lastActivePlaybackSpeed = _playbackSpeed;
            _colorblindMarkersEnabled = PlayerPrefs.GetInt(P9MarkersEnabledKey, 1) > 0;
            var largeTextDefault = Screen.height <= 768 ? 1 : 0;
            _largeTextEnabled = PlayerPrefs.GetInt(P9LargeTextEnabledKey, largeTextDefault) > 0;

            _battlePresentation = gameObject.AddComponent<TDBattlePresentation>();
            _battlePresentation.Initialize(
                _battleCanvas,
                _uiFont,
                _mainCamera,
                SetBattlePlaybackSpeed,
                ToggleColorblindMarkers,
                ToggleLargeText,
                ConfirmTutorialStep,
                SkipFirstRunTutorial,
                _colorblindMarkersEnabled,
                _largeTextEnabled);
            _battlePresentation.SetCaptionState(_subtitlesEnabled, _captionsEnabled);

            CacheBaseUiFontSizes();
            ApplyLargeTextMode();
            SetBattlePlaybackSpeed(_playbackSpeed, false);
            InitializeFirstRunTutorial();
            BuildTitleScreen();
            BuildPauseMenu();
            BuildLoadingScreen();
            BuildMissionBriefing();
        }

        private void BuildMissionBriefing()
        {
            if (_battleCanvas == null || _missionBriefing != null)
            {
                return;
            }

            var go = new GameObject("TD Mission Briefing");
            go.transform.SetParent(_battleCanvas.transform, false);
            _missionBriefing = go.AddComponent<TDMissionBriefing>();
            _missionBriefing.Build(_battleCanvas);
            _missionBriefing.OnBegin = HandleBriefingBegin;
        }

        private void ShowMissionBriefing()
        {
            if (_missionBriefing == null || _campaignRoute?.level == null)
            {
                return;
            }

            var level = _campaignRoute.level;
            var map = _campaignRoute.map;
            var levelTitle = $"{(level.bossLevel ? "BOSS MISSION" : "FIELD MISSION")}  L{level.levelIndex:00}";
            if (map != null && !string.IsNullOrWhiteSpace(map.displayName))
            {
                levelTitle += $"\n{map.displayName}";
            }

            var mapHook = map != null && !string.IsNullOrWhiteSpace(map.tacticalHook)
                ? map.tacticalHook
                : string.Empty;

            // Scenario mechanic intel
            string scenarioIntel;
            if (map?.mechanic != null)
            {
                var m = map.mechanic;
                scenarioIntel = $"TACTICAL DEVICE\n{m.displayName}\n\n{m.description}";
            }
            else
            {
                scenarioIntel = "TACTICAL DEVICE\nNo map device — pure defense.";
            }

            // Threat composition from wave intel
            BuildMissionWaveIntel(level, out var waveCount, out var laneCount, out var composition, out var threatTags, out _);
            var threatLines = $"THREAT ASSESSMENT\n{waveCount} waves / {laneCount} lane(s)\n\n{composition}";
            if (level.newEnemyUnlocks != null && level.newEnemyUnlocks.Length > 0)
            {
                threatLines += $"\n\nNEW: {string.Join(", ", level.newEnemyUnlocks)}";
            }

            // Contract
            string contractIntel;
            if (level.contract != null)
            {
                var c = level.contract;
                contractIntel = $"CONTRACT\n{c.displayName}\n\nObjective: {c.metric} {c.comparison} {c.target}";
            }
            else
            {
                contractIntel = "CONTRACT\nSurvive all 20 waves.";
            }

            // Pause the game while briefing is up
            SetBattlePlaybackSpeed(0f, false);
            _missionBriefing.Show(levelTitle, mapHook, scenarioIntel, threatIntel: threatLines, contractIntel);
            PlaySfxTone("ui_panel_open", 540f, 0.10f, 0.52f, true);
        }

        private void HandleBriefingBegin()
        {
            _missionBriefing?.Hide();
            SetBattlePlaybackSpeed(_lastActivePlaybackSpeed > 0 ? _lastActivePlaybackSpeed : 1f, false);
            PlaySfxTone("ui_panel_close", 420f, 0.08f, 0.48f, false);
        }

        private void BuildLoadingScreen()
        {
            if (_battleCanvas == null || _loadingScreen != null)
            {
                return;
            }

            var go = new GameObject("TD Loading Screen");
            go.transform.SetParent(_battleCanvas.transform, false);
            _loadingScreen = go.AddComponent<TDLoadingScreen>();
            _loadingScreen.Build(_battleCanvas);
        }

        private void BuildPauseMenu()
        {
            if (_battleCanvas == null || _pauseMenu != null)
            {
                return;
            }

            var pauseGo = new GameObject("TD Pause Menu");
            pauseGo.transform.SetParent(_battleCanvas.transform, false);
            _pauseMenu = pauseGo.AddComponent<TDPauseMenu>();
            _pauseMenu.Build(_battleCanvas);
            _pauseMenu.OnResume = HandlePauseResume;
            _pauseMenu.OnRestart = HandlePauseRestart;
            _pauseMenu.OnOpenSettings = HandlePauseSettings;
            _pauseMenu.OnQuitToTitle = HandlePauseQuitToTitle;
        }

        private void TogglePauseMenu()
        {
            if (_pauseMenu == null)
            {
                ToggleBattlePause();
                return;
            }

            if (_pauseMenu.IsVisible)
            {
                HandlePauseResume();
            }
            else
            {
                _pauseMenu.Show();
                SetBattlePlaybackSpeed(0f, false);
                PlaySfxTone("ui_panel_open", 540f, 0.10f, 0.52f, true);
            }
        }

        private void HandlePauseResume()
        {
            _pauseMenu?.Hide();
            SetBattlePlaybackSpeed(_lastActivePlaybackSpeed > 0 ? _lastActivePlaybackSpeed : 1f, false);
            PlaySfxTone("ui_panel_close", 420f, 0.08f, 0.48f, false);
        }

        private void HandlePauseRestart()
        {
            _pauseMenu?.Hide();
            RestartCurrentScene();
        }

        private void HandlePauseSettings()
        {
            _settingsPanel?.Open();
        }

        private void HandlePauseQuitToTitle()
        {
            _pauseMenu?.Hide();
            // Reload the scene — Awake will rebuild the title screen.
            // Reset deployment so the title appears.
            _campaignDeploymentConfirmed = false;
            _skipTitleForAutomation = false;
            LoadingTransition("RETURNING TO TITLE", "EMBERLINE DEFENSE");
        }

        /// <summary>Skip title screen on next Awake — used by MCP automation.</summary>
        public static void SkipTitleScreenForAutomation()
        {
            _skipTitleForAutomation = true;
        }

        /// <summary>Reset the skip flag — title screen will show on next Awake.</summary>
        public static void ResetTitleScreenSkip()
        {
            _skipTitleForAutomation = false;
        }

        private void BuildTitleScreen()
        {
            if (_battleCanvas == null || _titleScreen != null)
            {
                return;
            }

            // Skip the title screen entirely for automated smoke/autoplay probes.
            var skipTitle = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--td-skip-title") >= 0
                || TDStandaloneSmokeProbe.IsRequested()
                || TDP1254StandaloneProbe.IsRequested()
                || _skipTitleForAutomation;
            if (skipTitle)
            {
                return;
            }

            // Detect if player has any existing progress (level > 1 unlocked)
            var totalLevels = _campaign?.totalLevels ?? 20;
            var hasProgress = TDCampaignProgression.IsLevelUnlocked(2, totalLevels);
            var campaignSummary = GetCampaignProgressSummary();
            var hasClearedCampaign = campaignSummary != null &&
                campaignSummary.totalLevels > 0 &&
                campaignSummary.clearedLevels == campaignSummary.totalLevels;

            // Create the title screen on its own child GameObject so Hide()
            // doesn't disable the game manager.
            var titleGo = new GameObject("TD Title Screen");
            titleGo.transform.SetParent(_battleCanvas.transform, false);
            _titleScreen = titleGo.AddComponent<TDTitleScreen>();
            _titleScreen.Build(_battleCanvas, hasProgress, hasClearedCampaign);
            _titleScreen.OnNewGame = HandleTitleNewGame;
            _titleScreen.OnNewGamePlus = HandleTitleNewGamePlus;
            _titleScreen.OnContinue = HandleTitleContinue;
            _titleScreen.OnOpenSettings = HandleTitleSettings;

            // While the title screen is up, the game is NOT auto-deployed.
            // Selecting New Game / Continue will open the mission board.
            _campaignDeploymentConfirmed = false;
        }

        private void HandleTitleNewGame()
        {
            // Reset to level 1 for a fresh campaign
            TDCampaignRouter.SaveLevelIndex(1);
            _missionBoardSelectedLevel = 1;
            HandleTitleEnterGame();
        }

        private void HandleTitleNewGamePlus()
        {
            // NG+: reset to level 1 but flag EmberTrial difficulty for the whole campaign.
            // The player keeps their claimed chapter rewards (meta progression persists).
            TDCampaignRouter.SaveLevelIndex(1);
            _missionBoardSelectedLevel = 1;

            // Set EmberTrial preference for all levels so the mission board defaults to it.
            var totalLevels = _campaign?.totalLevels ?? 20;
            for (var lvl = 1; lvl <= totalLevels; lvl++)
            {
                TDCampaignProgression.SetDifficultyPreference(lvl, TDCampaignDifficultyTier.EmberTrial);
            }

            HandleTitleEnterGame();
        }

        private void HandleTitleContinue()
        {
            // Keep the saved level index
            _missionBoardSelectedLevel = TDCampaignRouter.GetSavedLevelIndex(DefaultCampaignLevelIndex);
            HandleTitleEnterGame();
        }

        private void HandleTitleEnterGame()
        {
            // Reload the campaign context with the selected level
            LoadCampaignContext();
            _titleScreen?.Hide();
            _campaignDeploymentConfirmed = true;
            EnsureWaveRoutineRunning();

            // Force-open the mission board (bypass the OpenMissionBoard guard which
            // blocks when _campaignDeploymentConfirmed is true).
            if (_campaignRoute?.level != null)
            {
                _missionBoardSelectedLevel = _campaignRoute.level.levelIndex;
                _missionBoardSelectedChapter = Mathf.Clamp((_missionBoardSelectedLevel - 1) / 5, 0, 3);
                _missionBoardOpen = true;
                _formationPanelOpen = false;
                _campaignProfileOpen = false;
                _missionBoardNeedsRefresh = true;
                _gridMap?.HideBuildPreview();
                HideRangePreview();
                HideRoutePreview();
                PlaySfxTone("ui_panel_open", 540f, 0.10f, 0.52f, true);
            }
        }

        /// <summary>
        /// Ensure the wave loop coroutine is running. If it died (e.g. due to a
        /// transient null during title-screen wait), restart it.
        /// </summary>
        private void EnsureWaveRoutineRunning()
        {
            if (_waveRoutine == null && _waveSet != null)
            {
                _waveRoutine = StartCoroutine(WaveLoopFromConfig());
            }
        }

        private void HandleTitleSettings()
        {
            _settingsPanel?.Open();
        }

        private void BuildP123SettingsUi()
        {
            if (_battleCanvas == null || _uiFont == null || _settingsPanel != null)
            {
                return;
            }

            _settingsPanel = gameObject.AddComponent<TDP123SettingsPanel>();
            _settingsPanel.Initialize(
                _battleCanvas,
                _uiFont,
                new TDP123SettingsBindings
                {
                    GetMarkers = () => _colorblindMarkersEnabled,
                    ToggleMarkers = ToggleColorblindMarkers,
                    GetLargeText = () => _largeTextEnabled,
                    ToggleLargeText = ToggleLargeText,
                    GetSubtitles = () => _subtitlesEnabled,
                    ToggleSubtitles = ToggleSubtitles,
                    GetCaptions = () => _captionsEnabled,
                    ToggleCaptions = ToggleCaptions,
                    GetUiScale = () => _uiScale,
                    SetUiScale = SetUiScale,
                    GetMasterVolume = () => _masterVolume,
                    SetMasterVolume = SetMasterVolume,
                    GetMusicVolume = () => _musicVolume,
                    SetMusicVolume = SetMusicVolume,
                    GetEffectsVolume = () => _effectsVolume,
                    SetEffectsVolume = SetEffectsVolume,
                    SetLanguage = SetUiLanguage,
                    OpenStateChanged = HandleSettingsOpenStateChanged,
                    ResetDefaults = ResetP123PresentationDefaults
                });
        }

        private void LoadP123PresentationPreferences()
        {
            _uiScale = Mathf.Clamp(PlayerPrefs.GetFloat(P123UiScaleKey, 1f), 1f, 1.2f);
            _subtitlesEnabled = PlayerPrefs.GetInt(P123SubtitlesKey, 1) > 0;
            _captionsEnabled = PlayerPrefs.GetInt(P123CaptionsKey, 1) > 0;
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P123MasterVolumeKey, 1f));
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P123MusicVolumeKey, 0.7f));
            _effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P123EffectsVolumeKey, 1f));
        }

        private void ToggleSettingsPanel()
        {
            _settingsPanel?.Toggle();
        }

        private void HandleSettingsOpenStateChanged(bool open)
        {
            if (open)
            {
                _settingsPauseOwned = !_playbackPaused;
                if (_settingsPauseOwned)
                {
                    SetBattlePlaybackSpeed(0f, false);
                }

                return;
            }

            if (_settingsPauseOwned)
            {
                _settingsPauseOwned = false;
                SetBattlePlaybackSpeed(_lastActivePlaybackSpeed, false);
            }

            if (EventSystem.current != null && _uiSettingsButton != null)
            {
                EventSystem.current.SetSelectedGameObject(_uiSettingsButton.gameObject);
            }
        }

        private void SetUiLanguage(TDUiLanguage language)
        {
            TDLocalization.SetLanguage(language);
            if (_battleCanvas != null)
            {
                TDLocalization.RefreshLabels(_battleCanvas.gameObject, _uiFont);
            }

            _battlePresentation?.RefreshLocalization();
            _settingsPanel?.Refresh();
            _missionBoardNeedsRefresh = true;
            RefreshTutorialUi();
            UpdateBattleUi();
        }

        private void SetUiScale(float scale)
        {
            _uiScale = Mathf.Clamp(Mathf.Round(scale * 10f) / 10f, 1f, 1.2f);
            PlayerPrefs.SetFloat(P123UiScaleKey, _uiScale);
            PlayerPrefs.Save();
            if (_battleCanvasScaler != null)
            {
                RefreshUiScaleForScreen(true);
            }
        }

        private void RefreshUiScaleForScreen(bool force = false)
        {
            if (_battleCanvasScaler == null || (!force && _lastUiScaleScreenHeight == Screen.height))
            {
                return;
            }

            var effectiveUiScale = GetEffectiveUiScale();
            _battleCanvasScaler.referenceResolution = new Vector2(1440f / effectiveUiScale, 900f / effectiveUiScale);
            _lastUiScaleScreenHeight = Screen.height;
        }

        private float GetEffectiveUiScale()
        {
            var lowResolutionAssist = Screen.height <= 600 ? 1.25f : Screen.height <= 768 ? 1.12f : 1f;
            return _uiScale * lowResolutionAssist;
        }

        private void ToggleSubtitles()
        {
            _subtitlesEnabled = !_subtitlesEnabled;
            PlayerPrefs.SetInt(P123SubtitlesKey, _subtitlesEnabled ? 1 : 0);
            PlayerPrefs.Save();
            _battlePresentation?.SetCaptionState(_subtitlesEnabled, _captionsEnabled);
        }

        private void ToggleCaptions()
        {
            _captionsEnabled = !_captionsEnabled;
            PlayerPrefs.SetInt(P123CaptionsKey, _captionsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            _battlePresentation?.SetCaptionState(_subtitlesEnabled, _captionsEnabled);
        }

        private void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(P123MasterVolumeKey, _masterVolume);
            PlayerPrefs.Save();
            ApplySfxVolumes();
        }

        private void SetMusicVolume(float value)
        {
            _musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(P123MusicVolumeKey, _musicVolume);
            PlayerPrefs.Save();
            ApplySfxVolumes();
        }

        private void SetEffectsVolume(float value)
        {
            _effectsVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(P123EffectsVolumeKey, _effectsVolume);
            PlayerPrefs.Save();
            ApplySfxVolumes();
        }

        private void ResetP123PresentationDefaults()
        {
            TDLocalization.SetLanguage(TDUiLanguage.English);
            _uiScale = 1f;
            _subtitlesEnabled = true;
            _captionsEnabled = true;
            _masterVolume = 1f;
            _musicVolume = 0.7f;
            _effectsVolume = 1f;
            _colorblindMarkersEnabled = true;
            _largeTextEnabled = Screen.height <= 768;
            PlayerPrefs.SetFloat(P123UiScaleKey, _uiScale);
            PlayerPrefs.SetInt(P123SubtitlesKey, 1);
            PlayerPrefs.SetInt(P123CaptionsKey, 1);
            PlayerPrefs.SetFloat(P123MasterVolumeKey, _masterVolume);
            PlayerPrefs.SetFloat(P123MusicVolumeKey, _musicVolume);
            PlayerPrefs.SetFloat(P123EffectsVolumeKey, _effectsVolume);
            PlayerPrefs.SetInt(P9MarkersEnabledKey, 1);
            PlayerPrefs.SetInt(P9LargeTextEnabledKey, _largeTextEnabled ? 1 : 0);
            PlayerPrefs.Save();
            SetUiScale(_uiScale);
            ApplySfxVolumes();
            ApplyLargeTextMode();
            _battlePresentation?.SetAccessibilityState(_colorblindMarkersEnabled, _largeTextEnabled);
            _battlePresentation?.SetCaptionState(_subtitlesEnabled, _captionsEnabled);
            SetUiLanguage(TDUiLanguage.English);
        }

        private void SetBattlePlaybackSpeed(float requestedSpeed)
        {
            SetBattlePlaybackSpeed(requestedSpeed, true);
        }

        private void SetBattlePlaybackSpeed(float requestedSpeed, bool persist)
        {
            if (requestedSpeed <= 0f)
            {
                _playbackPaused = true;
                Time.timeScale = 0f;
            }
            else
            {
                _playbackSpeed = Mathf.Clamp(Mathf.Round(requestedSpeed), 1f, 3f);
                _lastActivePlaybackSpeed = _playbackSpeed;
                _playbackPaused = false;
                Time.timeScale = _playbackSpeed;
                if (persist)
                {
                    PlayerPrefs.SetFloat(P9PlaybackSpeedKey, _playbackSpeed);
                    PlayerPrefs.Save();
                }
            }

            _battlePresentation?.SetPlaybackState(_lastActivePlaybackSpeed, _playbackPaused);
        }

        private void ToggleBattlePause()
        {
            SetBattlePlaybackSpeed(_playbackPaused ? _lastActivePlaybackSpeed : 0f);
        }

        private void HandlePlaybackHotkeys()
        {
            if (TDInputBindings.GetKeyDown(TDInputAction.Pause) ||
                TDInputCompat.GetKeyDown(KeyCode.Pause) ||
                TDInputCompat.GetGamepadButtonDown(TDGamepadButton.Start))
            {
                TogglePauseMenu();
                return;
            }

            if (TDInputBindings.GetKeyDown(TDInputAction.SpeedDown) ||
                TDInputCompat.GetKeyDown(KeyCode.KeypadMinus) ||
                TDInputCompat.GetGamepadButtonDown(TDGamepadButton.LeftShoulder))
            {
                SetBattlePlaybackSpeed(Mathf.Max(1f, _lastActivePlaybackSpeed - 1f));
            }
            else if (TDInputBindings.GetKeyDown(TDInputAction.SpeedUp) ||
                     TDInputCompat.GetKeyDown(KeyCode.KeypadPlus) ||
                     TDInputCompat.GetGamepadButtonDown(TDGamepadButton.RightShoulder))
            {
                SetBattlePlaybackSpeed(Mathf.Min(3f, _lastActivePlaybackSpeed + 1f));
            }
        }

        private void ToggleColorblindMarkers()
        {
            _colorblindMarkersEnabled = !_colorblindMarkersEnabled;
            PlayerPrefs.SetInt(P9MarkersEnabledKey, _colorblindMarkersEnabled ? 1 : 0);
            PlayerPrefs.Save();
            _battlePresentation?.SetAccessibilityState(_colorblindMarkersEnabled, _largeTextEnabled);
            PushTacticalEvent($"Shape markers {(_colorblindMarkersEnabled ? "enabled" : "disabled")}", 3.2f);
        }

        private void ToggleLargeText()
        {
            _largeTextEnabled = !_largeTextEnabled;
            PlayerPrefs.SetInt(P9LargeTextEnabledKey, _largeTextEnabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyLargeTextMode();
            _battlePresentation?.SetAccessibilityState(_colorblindMarkersEnabled, _largeTextEnabled);
        }

        private void CacheBaseUiFontSizes()
        {
            _baseUiFontSizes.Clear();
            if (_battleCanvas == null)
            {
                return;
            }

            var labels = _battleCanvas.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                {
                    _baseUiFontSizes[labels[i]] = labels[i].fontSize;
                }
            }
        }

        private void ApplyLargeTextMode()
        {
            foreach (var pair in _baseUiFontSizes)
            {
                var label = pair.Key;
                if (label == null)
                {
                    continue;
                }

                var targetSize = pair.Value + (_largeTextEnabled ? 1 : 0);
                label.fontSize = targetSize;
                if (label.resizeTextForBestFit)
                {
                    label.resizeTextMinSize = Mathf.Max(9, targetSize - 3);
                    label.resizeTextMaxSize = targetSize;
                }
            }
        }

        private string GetTutorialStepKey()
        {
            return $"{P9TutorialStepKeyPrefix}{TDCampaignProgression.ActiveSaveSlot}";
        }

        private string GetTutorialCompleteKey()
        {
            return $"{P9TutorialCompleteKeyPrefix}{TDCampaignProgression.ActiveSaveSlot}";
        }

        private void InitializeFirstRunTutorial()
        {
            var levelIndex = _campaignRoute?.level?.levelIndex ?? DefaultCampaignLevelIndex;
            var completed = PlayerPrefs.GetInt(GetTutorialCompleteKey(), 0) > 0;
            if (levelIndex != 1 || completed)
            {
                _tutorialStep = TDFirstRunTutorialStep.Complete;
                _tutorialVisible = false;
                return;
            }

            _tutorialStep = (TDFirstRunTutorialStep)Mathf.Clamp(
                PlayerPrefs.GetInt(GetTutorialStepKey(), 0),
                0,
                (int)TDFirstRunTutorialStep.UseScenario);
            _tutorialVisible = true;
            _tutorialSessionTracked = true;
            IncrementTutorialTelemetry("started");
            RefreshTutorialUi();
        }

        private void UpdateFirstRunTutorial()
        {
            if (!_tutorialVisible || _tutorialStep == TDFirstRunTutorialStep.Complete)
            {
                return;
            }

            if (_tutorialStep == TDFirstRunTutorialStep.InspectRange)
            {
                var rangeVisible = _rangePreviewRenderer != null && _rangePreviewRenderer.enabled && _selectedTowerForUi != null;
                _tutorialRangeInspectTimer = rangeVisible
                    ? _tutorialRangeInspectTimer + Time.unscaledDeltaTime
                    : 0f;
                if (_tutorialRangeInspectTimer >= 0.45f)
                {
                    AdvanceTutorial(TDFirstRunTutorialStep.InspectRange);
                }
            }

            RefreshTutorialUi();
        }

        private void RefreshTutorialUi()
        {
            if (_battlePresentation == null)
            {
                return;
            }

            var visible = _tutorialVisible && _campaignDeploymentConfirmed && !_missionBoardOpen && !_formationPanelOpen && !_campaignProfileOpen && !_gameOver;
            var progress = TDLocalization.IsChinese
                ? $"步骤 {(int)_tutorialStep + 1}/6"
                : $"STEP {(int)_tutorialStep + 1}/6";
            var title = string.Empty;
            var body = string.Empty;
            var confirm = false;
            var confirmLabel = "CONFIRM";
            switch (_tutorialStep)
            {
                case TDFirstRunTutorialStep.BuildTower:
                    title = TDLocalization.IsChinese ? "部署防御塔" : "DEPLOY A TOWER";
                    body = TDLocalization.IsChinese
                        ? "先选择阵容中的防御塔，再点击发光塔位。只有完成部署后，本步骤才会通过。"
                        : "Choose a formation tower, then click a glowing build pad. The action is accepted only after a tower is deployed.";
                    break;
                case TDFirstRunTutorialStep.InspectRange:
                    title = TDLocalization.IsChinese ? "查看射程" : "READ THE RANGE";
                    body = TDLocalization.IsChinese
                        ? "指向或选中防御塔，让射程圈保持显示。观察道路进入和离开射程的位置。"
                        : "Point at or select the tower until its coverage ring remains visible. Check where the road enters and exits the ring.";
                    break;
                case TDFirstRunTutorialStep.StartWave:
                    title = TDLocalization.IsChinese ? "派出敌军波次" : "DISPATCH THE WAVE";
                    body = TDLocalization.IsChinese
                        ? "防线准备完毕后点击开始波次。本步骤不会自动派出敌军。"
                        : "Use Start Wave when the defense is ready. The wave will not advance this step automatically.";
                    break;
                case TDFirstRunTutorialStep.ReadArmor:
                    title = TDLocalization.IsChinese ? "识别护甲" : "READ ARMOR";
                    body = TDLocalization.IsChinese
                        ? "[#] 护甲会固定减免伤害。[#] 破甲表示护甲已降低；先用轨枪或钻机施压，再衔接快速攻击。"
                        : "[#] Armor removes flat damage. [#] BREAK means armor is reduced; use Rail or Siege pressure before rapid hits.";
                    confirm = true;
                    confirmLabel = TDLocalization.IsChinese ? "确认" : "READ";
                    break;
                case TDFirstRunTutorialStep.UpgradeTower:
                    title = TDLocalization.IsChinese ? "选择升级分支" : "COMMIT A BRANCH";
                    body = TDLocalization.IsChinese
                        ? "下一次备战时选中防御塔，购买伤害或功能分支。预览会显示该分支的克制定位。"
                        : "During the next prep, select the tower and buy a Damage or Utility branch. The preview shows its counter identity.";
                    break;
                case TDFirstRunTutorialStep.UseScenario:
                    title = TDLocalization.IsChinese ? "使用场景机制" : "USE THE MAP MECHANIC";
                    body = TDLocalization.IsChinese
                        ? "在强化关或考试关的备战阶段启用场景指令。指令旁会显示消耗和剩余次数。"
                        : "At a Reinforce or Exam prep, activate the Scenario command. Its cost and remaining charges are shown beside the command.";
                    break;
                default:
                    visible = false;
                    break;
            }

            _battlePresentation.SetTutorial(progress, title, body, visible, confirm, confirmLabel);
        }

        private void ConfirmTutorialStep()
        {
            if (_tutorialStep == TDFirstRunTutorialStep.ReadArmor)
            {
                AdvanceTutorial(TDFirstRunTutorialStep.ReadArmor);
            }
        }

        private void AdvanceTutorial(TDFirstRunTutorialStep completedStep)
        {
            if (!_tutorialVisible || _tutorialStep != completedStep)
            {
                return;
            }

            _tutorialRangeInspectTimer = 0f;
            IncrementTutorialTelemetry($"step_{(int)completedStep + 1}");
            _tutorialStep++;
            if (_tutorialStep >= TDFirstRunTutorialStep.Complete)
            {
                CompleteFirstRunTutorial(false);
                return;
            }

            PlayerPrefs.SetInt(GetTutorialStepKey(), (int)_tutorialStep);
            PlayerPrefs.Save();
            RefreshTutorialUi();
            PlaySfxTone("ui_tutorial_advance", 580f, 0.09f, 0.52f, true);
        }

        private void SkipFirstRunTutorial()
        {
            CompleteFirstRunTutorial(true);
        }

        private void CompleteFirstRunTutorial(bool skipped)
        {
            _tutorialStep = TDFirstRunTutorialStep.Complete;
            _tutorialVisible = false;
            PlayerPrefs.SetInt(GetTutorialStepKey(), (int)TDFirstRunTutorialStep.Complete);
            PlayerPrefs.SetInt(GetTutorialCompleteKey(), 1);
            IncrementTutorialTelemetry(skipped ? "skipped" : "completed");
            _tutorialSessionEnded = true;
            PlayerPrefs.Save();
            _battlePresentation?.SetTutorial(string.Empty, string.Empty, string.Empty, false, false, string.Empty);
            PushTacticalEvent(skipped ? "Interactive tutorial skipped" : "Interactive tutorial complete", 4.6f);
            PlaySfxTone("ui_tutorial_complete", 820f, 0.20f, 0.68f, true);
        }

        private void IncrementTutorialTelemetry(string eventName)
        {
            var key = $"{P123TutorialTelemetryPrefix}{TDCampaignProgression.ActiveSaveSlot}_{eventName}";
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);
            PlayerPrefs.Save();
        }

        private void BuildMissionBoardUi(Transform root)
        {
            _uiMissionBoardScrim = CreateUiPanel(
                "Mission Board Scrim",
                root,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.004f, 0.008f, 0.012f, 0.76f));
            _uiMissionBoardRoot = CreateUiPanel(
                "Mission Board",
                root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1120f, 660f),
                new Color(0.022f, 0.034f, 0.043f, 0.985f));
            AddUiPanelChrome(_uiMissionBoardRoot, TDUiWorldSkin.Brass);

            _uiMissionBoardTitleText = CreateUiText(
                "Mission Board Title",
                _uiMissionBoardRoot,
                new Vector2(96f, -18f),
                new Vector2(288f, 30f),
                "CAMPAIGN COMMAND",
                20,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(0.91f, 0.97f, 1f, 1f));
            _uiMissionBoardProgressText = CreateUiText(
                "Mission Board Progress",
                _uiMissionBoardRoot,
                new Vector2(398f, -20f),
                new Vector2(698f, 26f),
                string.Empty,
                12,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Color(0.98f, 0.86f, 0.58f, 1f));

            CreateUiImage(
                "Mission Intel Divider",
                _uiMissionBoardRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(744f, -64f),
                new Vector2(1f, 510f),
                new Color(0.56f, 0.72f, 0.80f, 0.28f));

            _uiMissionLevelButtons.Clear();
            _uiMissionLevelButtonTexts.Clear();
            _uiMissionChapterButtons.Clear();
            _uiMissionChapterTitleTexts.Clear();
            _uiMissionChapterProgressTexts.Clear();
            _uiMissionChapterRewardButtons.Clear();
            _uiMissionChapterRewardButtonTexts.Clear();

            // World map: visual 20-node S-curve replacing the flat button grid.
            // Positioned in the left portion of the mission board (the old level button area).
            var worldMapGo = new GameObject("TD World Map");
            worldMapGo.transform.SetParent(_uiMissionBoardRoot, false);
            _worldMap = worldMapGo.AddComponent<TDWorldMap>();
            _worldMap.Build(_uiMissionBoardRoot, 372f, -290f);
            _worldMap.OnNodeClicked = SelectMissionBoardLevel;

            // Chapter tabs remain as quick-jump buttons above the map.
            for (var chapterIndex = 0; chapterIndex < 4; chapterIndex++)
            {
                var chapter = GetCampaignChapterAt(chapterIndex);
                var chapterLabel = chapter?.themeTags != null && chapter.themeTags.Length > 0
                    ? FormatCampaignTags(chapter.themeTags, 3).ToUpperInvariant()
                    : $"SECTOR {(char)('A' + chapterIndex)}";
                var capturedChapterIndex = chapterIndex;
                var chapterButton = CreateUiButton(
                    $"Mission Chapter Tab {chapterIndex + 1}",
                    _uiMissionBoardRoot,
                    new Vector2(24f + (chapterIndex * 174f), -70f),
                    new Vector2(162f, 42f),
                    $"CHAPTER {(char)('A' + chapterIndex)}  {chapterLabel}",
                    10,
                    () => SelectMissionBoardChapter(capturedChapterIndex));
                var chapterTitle = chapterButton.GetComponentInChildren<Text>();
                var chapterProgress = CreateUiText(
                    $"Mission Chapter {chapterIndex + 1} Progress",
                    _uiMissionBoardRoot,
                    new Vector2(24f, -124f),
                    new Vector2(524f, 28f),
                    string.Empty,
                    11,
                    FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Color(0.82f, 0.92f, 0.98f, 1f));
                var rewardButton = CreateUiButton(
                    $"Mission Chapter {chapterIndex + 1} Reward",
                    _uiMissionBoardRoot,
                    new Vector2(564f, -120f),
                    new Vector2(150f, 34f),
                    "LOCKED",
                    10,
                    () => TryClaimChapterReward(capturedChapterIndex));
                _uiMissionChapterButtons.Add(chapterButton);
                _uiMissionChapterTitleTexts.Add(chapterTitle);
                _uiMissionChapterProgressTexts.Add(chapterProgress);
                _uiMissionChapterRewardButtons.Add(rewardButton);
                _uiMissionChapterRewardButtonTexts.Add(rewardButton.GetComponentInChildren<Text>());

                // Level buttons are now handled by the world map (TDWorldMap).
            }

            CreateUiText("Chapter Overview Header", _uiMissionBoardRoot, new Vector2(24f, -514f), new Vector2(340f, 22f), "MISSION INTEL", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.68f, 0.28f, 1f));
            CreateUiImage("Chapter Overview Rule", _uiMissionBoardRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -344f), new Vector2(690f, 2f), new Color(0.56f, 0.72f, 0.80f, 0.34f));
            _uiMissionChapterOverviewText = CreateUiText("Chapter Overview", _uiMissionBoardRoot, new Vector2(24f, -360f), new Vector2(690f, 76f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.80f, 0.91f, 0.97f, 1f));
            _uiMissionChapterRewardText = CreateUiText("Chapter Reward Summary", _uiMissionBoardRoot, new Vector2(24f, -456f), new Vector2(690f, 84f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.80f, 0.98f, 0.82f, 1f));

            _uiMissionIntelTitleText = CreateUiText("Mission Intel Title", _uiMissionBoardRoot, new Vector2(770f, -68f), new Vector2(326f, 52f), string.Empty, 16, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.92f, 0.98f, 1f, 1f));
            _uiMissionIntelBriefText = CreateUiText("Mission Intel Brief", _uiMissionBoardRoot, new Vector2(770f, -124f), new Vector2(326f, 82f), string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.78f, 0.89f, 0.96f, 1f));
            _uiMissionIntelThreatText = CreateUiText("Mission Intel Threat", _uiMissionBoardRoot, new Vector2(770f, -210f), new Vector2(326f, 90f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.98f, 0.86f, 0.62f, 1f));
            _uiMissionIntelContractText = CreateUiText("Mission Intel Contract", _uiMissionBoardRoot, new Vector2(770f, -304f), new Vector2(326f, 82f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.80f, 0.98f, 0.82f, 1f));
            _uiMissionIntelCounterText = CreateUiText("Mission Intel Counter", _uiMissionBoardRoot, new Vector2(770f, -390f), new Vector2(326f, 82f), string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.74f, 0.96f, 0.84f, 1f));
            _uiMissionIntelRecordText = CreateUiText("Mission Intel Record", _uiMissionBoardRoot, new Vector2(770f, -476f), new Vector2(326f, 102f), string.Empty, 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.82f, 0.92f, 0.98f, 1f));

            _uiMissionCloseButton = CreateUiButton("Mission Close Button", _uiMissionBoardRoot, new Vector2(770f, -594f), new Vector2(150f, 44f), "Back", 13, CloseMissionBoard);
            _uiMissionCloseButtonText = _uiMissionCloseButton.GetComponentInChildren<Text>();
            _uiMissionDeployButton = CreateUiButton("Mission Deploy Button", _uiMissionBoardRoot, new Vector2(936f, -594f), new Vector2(160f, 44f), "Formation", 14, OpenFormationPanel);
            _uiMissionDeployButtonText = _uiMissionDeployButton.GetComponentInChildren<Text>();
            _uiCampaignProfileButton = CreateUiButton("Campaign Profile Button", _uiMissionBoardRoot, new Vector2(24f, -594f), new Vector2(176f, 44f), "Campaign Profile", 12, OpenCampaignProfile);

            BuildFormationUi();
            BuildCampaignProfileUi();

            _uiMissionBoardScrim.gameObject.SetActive(false);
            _uiMissionBoardRoot.gameObject.SetActive(false);
            _missionBoardNeedsRefresh = true;
        }

        private void BuildFormationUi()
        {
            _uiFormationRoot = CreateUiPanel(
                "Prebattle Formation",
                _uiMissionBoardRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(1120f, 660f),
                new Color(0.018f, 0.030f, 0.038f, 1f));
            AddUiPanelChrome(_uiFormationRoot, TDUiWorldSkin.Instrument);

            _uiFormationTitleText = CreateUiText("Formation Title", _uiFormationRoot, new Vector2(96f, -18f), new Vector2(648f, 30f), "PREBATTLE FORMATION", 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.98f, 1f, 1f));
            _uiFormationThreatText = CreateUiText("Formation Threat", _uiFormationRoot, new Vector2(24f, -54f), new Vector2(1072f, 44f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.98f, 0.84f, 0.58f, 1f));
            CreateUiImage("Formation Header Divider", _uiFormationRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -106f), new Vector2(1072f, 1f), new Color(0.56f, 0.72f, 0.80f, 0.28f));

            _uiFormationRosterText = CreateUiText("Formation Roster", _uiFormationRoot, new Vector2(24f, -120f), new Vector2(568f, 34f), string.Empty, 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.82f, 0.94f, 1f, 1f));
            _uiFormationTowerButtons.Clear();
            _uiFormationTowerButtonTexts.Clear();
            _uiFormationTowerIcons.Clear();
            _uiFormationTowerAccents.Clear();
            _uiFormationTowerOutlines.Clear();
            var buildOrder = TDTower.GetBuildOrder();
            for (var i = 0; i < buildOrder.Count; i++)
            {
                var towerKind = buildOrder[i];
                var column = i % 4;
                var row = i / 4;
                var button = CreateUiButton(
                    $"Formation Tower {towerKind}",
                    _uiFormationRoot,
                    new Vector2(24f + (column * 145f), -164f - (row * 90f)),
                    new Vector2(133f, 76f),
                    string.Empty,
                    11,
                    () => ToggleFormationTower(towerKind));
                var identity = TDUiVisualIdentity.GetTower(towerKind);
                var icon = CreateUiSpriteImage($"Formation {towerKind} Identity Icon", button.transform, new Vector2(8f, -12f), new Vector2(50f, 50f), identity.iconResourcePath, Color.white);
                var accent = CreateUiImage($"Formation {towerKind} Identity Accent", button.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 4f), identity.accent);
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = identity.accent;
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;
                outline.enabled = false;
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 1f);
                label.rectTransform.anchoredPosition = new Vector2(62f, -6f);
                label.rectTransform.sizeDelta = new Vector2(65f, 64f);
                label.alignment = TextAnchor.MiddleLeft;
                _uiFormationTowerButtons.Add(button);
                _uiFormationTowerButtonTexts.Add(label);
                _uiFormationTowerIcons.Add(icon);
                _uiFormationTowerAccents.Add(accent);
                _uiFormationTowerOutlines.Add(outline);
            }

            CreateUiText("Doctrine Header", _uiFormationRoot, new Vector2(24f, -348f), new Vector2(568f, 24f), "RESONANCE DOCTRINE", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.90f, 0.98f, 1f));
            _uiFormationDoctrineButtons.Clear();
            _uiFormationDoctrineButtonTexts.Clear();
            var doctrines = new[]
            {
                TDResonanceDoctrine.Adaptive,
                TDResonanceDoctrine.EmberSurge,
                TDResonanceDoctrine.FractureMark
            };
            for (var i = 0; i < doctrines.Length; i++)
            {
                var doctrine = doctrines[i];
                var button = CreateUiButton(
                    $"Doctrine {doctrine}",
                    _uiFormationRoot,
                    new Vector2(24f + (i * 194f), -380f),
                    new Vector2(180f, 54f),
                    string.Empty,
                    11,
                    () => SelectFormationDoctrine(doctrine));
                _uiFormationDoctrineButtons.Add(button);
                _uiFormationDoctrineButtonTexts.Add(button.GetComponentInChildren<Text>());
            }

            _uiFormationLockText = CreateUiText("Formation Lock State", _uiFormationRoot, new Vector2(24f, -448f), new Vector2(568f, 54f), string.Empty, 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.96f, 0.76f, 0.56f, 1f));
            CreateUiText("Difficulty Header", _uiFormationRoot, new Vector2(24f, -508f), new Vector2(568f, 18f), "CAMPAIGN DIFFICULTY", 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.90f, 0.98f, 1f));
            _uiFormationDifficultyButtons.Clear();
            _uiFormationDifficultyButtonTexts.Clear();
            var difficultyTiers = new[]
            {
                TDCampaignDifficultyTier.Standard,
                TDCampaignDifficultyTier.Veteran,
                TDCampaignDifficultyTier.EmberTrial
            };
            for (var i = 0; i < difficultyTiers.Length; i++)
            {
                var difficulty = difficultyTiers[i];
                var button = CreateUiButton(
                    $"Difficulty {difficulty}",
                    _uiFormationRoot,
                    new Vector2(24f + (i * 194f), -530f),
                    new Vector2(180f, 42f),
                    GetDifficultyShortLabel(difficulty),
                    11,
                    () => SelectFormationDifficulty(difficulty));
                _uiFormationDifficultyButtons.Add(button);
                _uiFormationDifficultyButtonTexts.Add(button.GetComponentInChildren<Text>());
            }
            CreateUiImage("Formation Intel Divider", _uiFormationRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(620f, -120f), new Vector2(1f, 398f), new Color(0.56f, 0.72f, 0.80f, 0.28f));
            _uiFormationFitTitleText = CreateUiText("Formation Fit Title", _uiFormationRoot, new Vector2(650f, -120f), new Vector2(446f, 50f), string.Empty, 18, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.94f, 0.98f, 1f, 1f));
            _uiFormationFitBodyText = CreateUiText("Formation Fit Body", _uiFormationRoot, new Vector2(650f, -176f), new Vector2(446f, 132f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.78f, 0.93f, 0.86f, 1f));
            _uiFormationMatrixText = CreateUiText("Formation Matrix", _uiFormationRoot, new Vector2(650f, -320f), new Vector2(446f, 188f), string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.80f, 0.90f, 0.97f, 1f));
            _uiFormationDifficultyText = CreateUiText("Formation Difficulty", _uiFormationRoot, new Vector2(650f, -514f), new Vector2(446f, 58f), string.Empty, 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.98f, 0.84f, 0.58f, 1f));

            _uiFormationProtocolButtons.Clear();
            _uiFormationProtocolButtonTexts.Clear();
            var protocols = _campaign?.metaProgression?.tacticalProtocols ?? Array.Empty<TDCampaignTacticalProtocolDefinition>();
            for (var i = 0; i < protocols.Length; i++)
            {
                var protocolId = protocols[i].protocolId;
                var button = CreateUiButton(
                    $"Tactical Protocol {protocolId}",
                    _uiFormationRoot,
                    new Vector2(174f + (i * 112f), -584f),
                    new Vector2(104f, 44f),
                    string.Empty,
                    10,
                    () => SelectFormationProtocol(protocolId));
                _uiFormationProtocolButtons.Add(button);
                _uiFormationProtocolButtonTexts.Add(button.GetComponentInChildren<Text>());
            }

            _uiFormationAutoFitButton = CreateUiButton("Formation Auto Fit", _uiFormationRoot, new Vector2(24f, -584f), new Vector2(134f, 44f), "Auto Fit", 13, AutoFitFormationDraft);
            _uiFormationBackButton = CreateUiButton("Formation Back", _uiFormationRoot, new Vector2(754f, -584f), new Vector2(154f, 44f), "Back", 13, CloseFormationPanel);
            _uiFormationDeployButton = CreateUiButton("Formation Deploy", _uiFormationRoot, new Vector2(924f, -584f), new Vector2(172f, 44f), "Save & Deploy", 14, ConfirmFormationAndDeploy);
            _uiFormationDeployButtonText = _uiFormationDeployButton.GetComponentInChildren<Text>();
            _uiFormationRoot.gameObject.SetActive(false);
        }

        private void BuildCampaignProfileUi()
        {
            _uiCampaignProfileRoot = CreateUiPanel(
                "Campaign Profile",
                _uiMissionBoardRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(1120f, 660f),
                new Color(0.018f, 0.030f, 0.038f, 1f));
            AddUiPanelChrome(_uiCampaignProfileRoot, TDUiWorldSkin.Brass);

            _uiCampaignProfileTitleText = CreateUiText("Campaign Profile Title", _uiCampaignProfileRoot, new Vector2(96f, -18f), new Vector2(548f, 30f), "CAMPAIGN PROFILE", 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.98f, 1f, 1f));
            _uiCampaignProfileSummaryText = CreateUiText("Campaign Profile Summary", _uiCampaignProfileRoot, new Vector2(24f, -54f), new Vector2(1072f, 40f), string.Empty, 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.98f, 0.84f, 0.58f, 1f));
            CreateUiImage("Campaign Profile Header Divider", _uiCampaignProfileRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -98f), new Vector2(1072f, 1f), new Color(0.56f, 0.72f, 0.80f, 0.28f));

            CreateUiText("Campaign Profile Chapter Header", _uiCampaignProfileRoot, new Vector2(24f, -118f), new Vector2(520f, 24f), "CHAPTER ARCHIVE", 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.80f, 0.93f, 1f, 1f));
            _uiCampaignProfileChapterText = CreateUiText("Campaign Profile Chapters", _uiCampaignProfileRoot, new Vector2(24f, -150f), new Vector2(520f, 280f), string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.80f, 0.90f, 0.97f, 1f));
            _uiCampaignProfileBonusText = CreateUiText("Campaign Profile Bonuses", _uiCampaignProfileRoot, new Vector2(24f, -446f), new Vector2(520f, 98f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.78f, 0.96f, 0.84f, 1f));

            CreateUiImage("Campaign Profile Control Divider", _uiCampaignProfileRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(572f, -118f), new Vector2(1f, 424f), new Color(0.56f, 0.72f, 0.80f, 0.28f));
            CreateUiText("Campaign Slot Header", _uiCampaignProfileRoot, new Vector2(604f, -112f), new Vector2(492f, 18f), "SAVE SLOTS", 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.80f, 0.93f, 1f, 1f));
            for (var slot = 1; slot <= TDCampaignProgression.MaxSaveSlots; slot++)
            {
                var capturedSlot = slot;
                var button = CreateUiButton(
                    $"Campaign Save Slot {slot}",
                    _uiCampaignProfileRoot,
                    new Vector2(604f + ((slot - 1) * 164f), -136f),
                    new Vector2(slot == TDCampaignProgression.MaxSaveSlots ? 164f : 148f, 38f),
                    $"SLOT {slot}",
                    11,
                    () => SwitchCampaignSaveSlot(capturedSlot));
                _uiCampaignProfileSlotButtons.Add(button);
                _uiCampaignProfileSlotButtonTexts.Add(button.GetComponentInChildren<Text>());
            }

            CreateUiText("Campaign Save Header", _uiCampaignProfileRoot, new Vector2(604f, -188f), new Vector2(492f, 20f), "PLAYER SAVE CONTROL", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.80f, 0.93f, 1f, 1f));
            _uiCampaignProfileSaveText = CreateUiText("Campaign Save Details", _uiCampaignProfileRoot, new Vector2(604f, -216f), new Vector2(492f, 112f), string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.80f, 0.90f, 0.97f, 1f));
            _uiCampaignProfileStatusText = CreateUiText("Campaign Save Status", _uiCampaignProfileRoot, new Vector2(604f, -334f), new Vector2(492f, 48f), string.Empty, 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.98f, 0.84f, 0.58f, 1f));

            _uiCampaignProfileCopyButton = CreateUiButton("Campaign Save Copy", _uiCampaignProfileRoot, new Vector2(604f, -394f), new Vector2(148f, 46f), "Copy Save", 13, CopyCampaignSaveToClipboard);
            _uiCampaignProfileImportButton = CreateUiButton("Campaign Save Import", _uiCampaignProfileRoot, new Vector2(768f, -394f), new Vector2(148f, 46f), "Import", 13, ImportCampaignSaveFromClipboard);
            _uiCampaignProfileImportButtonText = _uiCampaignProfileImportButton.GetComponentInChildren<Text>();
            _uiCampaignProfileResetButton = CreateUiButton("Campaign Save Reset", _uiCampaignProfileRoot, new Vector2(932f, -394f), new Vector2(164f, 46f), "Reset Profile", 13, ResetCampaignProfileFromUi);
            _uiCampaignProfileResetButtonText = _uiCampaignProfileResetButton.GetComponentInChildren<Text>();
            _uiCampaignProfileCloudCopyButton = CreateUiButton("Campaign Cloud Copy", _uiCampaignProfileRoot, new Vector2(604f, -456f), new Vector2(238f, 42f), "Copy Cloud", 12, CopyCampaignCloudToClipboard);
            _uiCampaignProfileCloudMergeButton = CreateUiButton("Campaign Cloud Merge", _uiCampaignProfileRoot, new Vector2(858f, -456f), new Vector2(238f, 42f), "Merge Cloud", 12, MergeCampaignCloudFromClipboard);
            _uiCampaignProfileBackButton = CreateUiButton("Campaign Profile Back", _uiCampaignProfileRoot, new Vector2(936f, -594f), new Vector2(160f, 44f), "Back", 13, CloseCampaignProfile);
            _uiCampaignProfileRoot.gameObject.SetActive(false);
        }

        private void UpdateBattleUi()
        {
            UpdateExamScenarioDevice();
            if (!UseRuntimeBattleUi || _battleCanvas == null)
            {
                return;
            }

            SetUiText(_uiCampaignText, GetCompactCampaignHudLabel());
            SetUiText(_uiWaveMetricText, $"WAVE  {_wave:00}/{GetConfiguredWaveCount():00}");
            SetUiText(_uiIntegrityMetricText, $"LINE  {_lineIntegrity:00}");
            SetUiText(_uiBudgetMetricText, $"GOLD  {_defenseBudget}");
            SetUiText(_uiSelectedText, $"{GetCompactTowerLabel(_selectedTowerKind).ToUpperInvariant()}  /  {GetUpgradeBranchLabel(_selectedUpgradeBranch).ToUpperInvariant()}");
            SetUiText(_uiPrepText, BuildCompactBattleStateLabel());
            SetUiText(_uiGuideText, GetGuideHudLabel());
            SetUiText(_uiMissionContractText, BuildCurrentMissionContractHudLabel());
            SetUiText(_uiResonanceText, GetResonanceHudLabel());
            SetUiText(_uiStatusText, $"Status {_lastStatus}");
            SetUiText(_uiEventFeedText, BuildCompactTacticalFeedLabel());

            var showPrepDetails = _isInPrepPhase && !_gameOver;
            var showResonanceMeter = _isResonanceSystemEnabled && !_gameOver;
            _uiSelectedText.gameObject.SetActive(showPrepDetails);
            _uiPrepText.gameObject.SetActive(showPrepDetails);
            _uiResonanceText.gameObject.SetActive(showResonanceMeter);
            if (_uiResonanceFill != null && _uiResonanceFill.transform.parent != null)
            {
                _uiResonanceFill.transform.parent.gameObject.SetActive(showResonanceMeter);
            }

            _uiTopPanel.sizeDelta = new Vector2(
                _uiTopPanel.sizeDelta.x,
                showResonanceMeter ? 122f : showPrepDetails ? 100f : 78f);
            _uiEventFeedRoot.gameObject.SetActive(!_gameOver && _captionsEnabled);

            if (_uiResonanceFill != null)
            {
                var fillRect = _uiResonanceFill.rectTransform;
                fillRect.anchorMax = new Vector2(Mathf.Clamp01(_resonanceCharge / ResonanceChargeMax), 1f);
            }

            UpdateStartWaveButtonUi();
            UpdateResonanceCommandPanelUi();
            UpdateScenarioMechanicUi();
            UpdateWaveIntelUi();
            UpdateTowerBuildButtonUi();
            UpdateTowerUpgradePanelUi();
            UpdateGameOverUi();
            UpdateMissionBoardUi();
        }

        private void UpdateStartWaveButtonUi()
        {
            if (_uiStartWaveButton == null)
            {
                return;
            }

            var isPrep = _isInPrepPhase && !_gameOver;
            var canStart = CanStartCurrentWave();
            _uiStartWaveButton.gameObject.SetActive(isPrep && !_missionBoardOpen && !_formationPanelOpen && !_campaignProfileOpen);
            _uiStartWaveButton.interactable = isPrep && canStart;
            SetUiText(_uiStartWaveButtonText, canStart ? $"START WAVE  {_wave:00}" : "BUILD 1 TOWER");
            if (_uiStartWaveButtonImage != null)
            {
                _uiStartWaveButtonImage.color = isPrep && canStart
                    ? Color.white
                    : new Color(0.74f, 0.72f, 0.68f, 0.90f);
            }
        }

        private void UpdateExamScenarioDevice()
        {
            if (_examScenarioDevice == null || _examPresentationProfile == null)
            {
                return;
            }

            var scenarioType = NormalizeGroupToken(_activeScenarioMechanic?.mechanicType);
            var stateValue = scenarioType == "route_switch"
                ? _scenarioRouteBias == "left" ? 1 : _scenarioRouteBias == "right" ? 2 : 0
                : _scenarioCharges;
            var active = _scenarioReinforcementPending || _scenarioBossPhaseSuppressed;
            _examScenarioDevice.SetRuntimeState(stateValue, active, _scenarioBossPhase);
            if (_gameOver)
            {
                _examScenarioDevice.SetStage(TDExamPresentationStage.Ending);
            }
        }

        private void PresentExamBeat(TDExamPresentationStage stage)
        {
            if (_examPresentationProfile == null || stage <= _examPresentationStage)
            {
                return;
            }

            _examPresentationStage = stage;
            _examScenarioDevice?.SetStage(stage);
            switch (stage)
            {
                case TDExamPresentationStage.Opening:
                    _examOpeningBeatCount++;
                    break;
                case TDExamPresentationStage.Escalation:
                    _examEscalationBeatCount++;
                    break;
                case TDExamPresentationStage.Decision:
                    _examDecisionBeatCount++;
                    break;
                default:
                    return;
            }

            ShowExamBeatVisual(stage);
        }

        private void ShowExamBeatVisual(TDExamPresentationStage stage)
        {
            if (_examPresentationProfile == null)
            {
                return;
            }

            var title = stage switch
            {
                TDExamPresentationStage.Opening => _examPresentationProfile.openingTitle,
                TDExamPresentationStage.Escalation => _examPresentationProfile.escalationTitle,
                TDExamPresentationStage.Decision => _examPresentationProfile.decisionTitle,
                _ => string.Empty
            };
            var body = stage switch
            {
                TDExamPresentationStage.Opening => _examPresentationProfile.openingBody,
                TDExamPresentationStage.Escalation => _examPresentationProfile.escalationBody,
                TDExamPresentationStage.Decision => _examPresentationProfile.decisionBody,
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            _battlePresentation?.ShowCinematic(
                _examPresentationProfile.marker,
                title,
                body,
                stage == TDExamPresentationStage.Decision ? TDBattleFeedbackTier.Critical : TDBattleFeedbackTier.Tactical,
                stage == TDExamPresentationStage.Decision ? 1.55f : 1.20f);
            PlaySfxTone(
                $"exam_{_examPresentationProfile.identityId}_{stage.ToString().ToLowerInvariant()}",
                360f + (_examPresentationProfile.levelIndex * 18f) + ((int)stage * 70f),
                stage == TDExamPresentationStage.Decision ? 0.24f : 0.16f,
                stage == TDExamPresentationStage.Decision ? 0.82f : 0.66f,
                true);
        }

        private void UpdateScenarioMechanicUi()
        {
            if (_uiScenarioPanel == null)
            {
                return;
            }

            var scenarioType = NormalizeGroupToken(_activeScenarioMechanic?.mechanicType);
            var showDuringCombat = scenarioType == "environment_device" || scenarioType == "boss_phase" || _scenarioReinforcementPending;
            var show = _activeScenarioMechanic != null && _campaignDeploymentConfirmed && !_gameOver &&
                       (_isInPrepPhase || showDuringCombat) &&
                       !_missionBoardOpen && !_formationPanelOpen && !_campaignProfileOpen;
            _uiScenarioPanel.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            _uiScenarioPanel.anchoredPosition = new Vector2(-18f, _isInPrepPhase ? -216f : -18f);
            var phase = string.IsNullOrWhiteSpace(_currentWavePhase) ? "PREP" : _currentWavePhase.ToUpperInvariant();
            var charges = _activeScenarioMechanic.maxCharges <= 0 ? "UNLIMITED" : _scenarioCharges.ToString();
            SetUiText(
                _uiScenarioTitleText,
                TDLocalization.IsChinese
                    ? $"场景机制  {TDLocalization.LocalizeRuntimeString(_activeScenarioMechanic.displayName)}  /  {GetLocalizedWavePhaseLabel(phase)}"
                    : $"SCENARIO  {_activeScenarioMechanic.displayName}  /  {phase}");
            SetUiText(_uiScenarioBodyText, BuildCompactScenarioMechanicStatusLabel(charges));
            SetUiText(
                _uiScenarioCommandButtonText,
                TDLocalization.IsChinese
                    ? $"{TDLocalization.LocalizeRuntimeString(_activeScenarioMechanic.commandLabel)}\n{(GetScenarioCommandCost() > 0 ? $"-{GetScenarioCommandCost()} 资源" : "战术指令")}"
                    : $"{_activeScenarioMechanic.commandLabel}\n{(GetScenarioCommandCost() > 0 ? $"-{GetScenarioCommandCost()} BUDGET" : "TACTICAL")}");
            _uiScenarioCommandButton.interactable = CanActivateScenarioMechanic(out _);
        }

        private string BuildScenarioMechanicStatusLabel(string charges)
        {
            var type = NormalizeGroupToken(_activeScenarioMechanic?.mechanicType);
            var state = type switch
            {
                "route_switch" => $"DIVERT  {_scenarioRouteBias.ToUpperInvariant()}  / center traffic follows switch",
                "timed_reinforcement" => _scenarioReinforcementPending
                    ? "RESERVE TRAIN EN ROUTE"
                    : $"ARRIVAL  {_activeScenarioMechanic.reinforcementDelaySeconds:0}s  / reward +{Mathf.Max(10, _activeScenarioMechanic.budgetCost * 2)}",
                "environment_device" => $"PURGE  {_activeEnemies.Count} targets  / damage, break, stagger",
                "boss_phase" => $"BOSS PHASE  {_scenarioBossPhase + 1}  / suppress next surge",
                _ => $"HOLD NEXT DEPLOYMENT  +{_activeScenarioMechanic.effectDurationSeconds:0.0}s spacing"
            };
            return $"{_activeScenarioMechanic.description}\n{state}  /  CHARGES {charges}";
        }

        private string BuildCompactScenarioMechanicStatusLabel(string charges)
        {
            var type = NormalizeGroupToken(_activeScenarioMechanic?.mechanicType);
            if (TDLocalization.IsChinese)
            {
                var localizedState = type switch
                {
                    "route_switch" => $"改道 {_scenarioRouteBias.ToUpperInvariant()} / 中央道岔",
                    "timed_reinforcement" => _scenarioReinforcementPending
                        ? "增援正在抵达"
                        : $"抵达 {_activeScenarioMechanic.reinforcementDelaySeconds:0}秒 / +{Mathf.Max(10, _activeScenarioMechanic.budgetCost * 2)}",
                    "environment_device" => $"净化 {_activeEnemies.Count} / 破甲 + 失衡",
                    "boss_phase" => $"首领阶段 {_scenarioBossPhase + 1} / 压制爆发",
                    _ => $"阻滞 +{_activeScenarioMechanic.effectDurationSeconds:0.0}秒间隔"
                };
                var localizedCharges = _activeScenarioMechanic.maxCharges <= 0 ? "不限" : charges;
                return $"{localizedState}\n次数 {localizedCharges}";
            }

            var state = type switch
            {
                "route_switch" => $"DIVERT {_scenarioRouteBias.ToUpperInvariant()} / center switch",
                "timed_reinforcement" => _scenarioReinforcementPending
                    ? "RESERVE INBOUND"
                    : $"ARRIVAL {_activeScenarioMechanic.reinforcementDelaySeconds:0}s / +{Mathf.Max(10, _activeScenarioMechanic.budgetCost * 2)}",
                "environment_device" => $"PURGE {_activeEnemies.Count} / break + stagger",
                "boss_phase" => $"BOSS PHASE {_scenarioBossPhase + 1} / suppress surge",
                _ => $"HOLD +{_activeScenarioMechanic.effectDurationSeconds:0.0}s spacing"
            };
            return $"{state}\nCHARGES {charges}";
        }

        private int GetScenarioCommandCost()
        {
            return _activeScenarioMechanic == null
                ? 0
                : TDEconomyTuning.GetScenarioCommandCost(
                    _activeScenarioMechanic.budgetCost,
                    _scenarioCostMultiplier,
                    _wave,
                    GetConfiguredWaveCount(),
                    _scenarioUses);
        }

        private bool CanActivateScenarioMechanic(out string reason)
        {
            reason = string.Empty;
            if (_activeScenarioMechanic == null || _gameOver || !_campaignDeploymentConfirmed)
            {
                reason = "Scenario command unavailable.";
                return false;
            }

            if (string.Equals(_currentWavePhase, "introduce", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Observe the mechanic during Introduce; command unlocks at Reinforce.";
                return false;
            }

            if (_activeScenarioMechanic.maxCharges > 0 && _scenarioCharges <= 0)
            {
                reason = "Scenario device has no charges remaining.";
                return false;
            }

            if (_defenseBudget < GetScenarioCommandCost())
            {
                reason = $"Scenario command needs {GetScenarioCommandCost()} budget.";
                return false;
            }

            var type = NormalizeGroupToken(_activeScenarioMechanic.mechanicType);
            if ((type == "signal_gate" || type == "route_switch" || type == "timed_reinforcement") && !_isInPrepPhase)
            {
                reason = "Scenario route commands are available during prep.";
                return false;
            }

            if (type == "signal_gate" && _scenarioWaveDelayBonus > 0f)
            {
                reason = "Signal gate is already holding this wave.";
                return false;
            }

            if (type == "timed_reinforcement" && _scenarioReinforcementPending)
            {
                reason = "Reserve reinforcement is already inbound.";
                return false;
            }

            if ((type == "environment_device" || type == "boss_phase") && (_isInPrepPhase || _activeEnemies.Count == 0))
            {
                reason = "Scenario device requires active enemies.";
                return false;
            }

            if (type == "boss_phase" && !_activeEnemies.Any(enemy => enemy != null && enemy.HasAnyTag("boss", "final", "elite")))
            {
                reason = "Phase breaker requires an active boss.";
                return false;
            }

            return true;
        }

        private void TryActivateScenarioMechanic()
        {
            if (!CanActivateScenarioMechanic(out var reason))
            {
                SetStatus(reason);
                return;
            }

            var type = NormalizeGroupToken(_activeScenarioMechanic.mechanicType);
            var commandCost = GetScenarioCommandCost();
            _defenseBudget -= commandCost;
            TrackP125ScenarioSpend(commandCost);
            if (_activeScenarioMechanic.maxCharges > 0)
            {
                _scenarioCharges = Mathf.Max(0, _scenarioCharges - 1);
            }

            _scenarioUses++;
            TrackP135ScenarioActivation(type);
            switch (type)
            {
                case "route_switch":
                    _scenarioRouteBias = _scenarioRouteBias == "center" ? "left" : _scenarioRouteBias == "left" ? "right" : "center";
                    PushTacticalEvent($"Route switch: center traffic -> {_scenarioRouteBias}", 5.2f);
                    PlaySfxTone("scenario_route_switch", 430f, 0.18f, 0.72f, true);
                    break;
                case "timed_reinforcement":
                    StartCoroutine(DeliverScenarioReinforcement());
                    PushTacticalEvent("Reserve train dispatched. Hold until arrival or start under-strength.", 6.0f);
                    PlaySfxTone("scenario_reinforcement_train", 520f, 0.22f, 0.72f, true);
                    break;
                case "environment_device":
                    ActivateScenarioEnvironmentDevice();
                    PlaySfxTone("scenario_kiln_purge", 360f, 0.20f, 0.74f, false);
                    break;
                case "boss_phase":
                    ActivateScenarioBossBreaker();
                    PlaySfxTone("scenario_boss_breaker", 240f, 0.24f, 0.80f, true);
                    break;
                default:
                    _scenarioWaveDelayBonus = Mathf.Max(1f, _activeScenarioMechanic.effectDurationSeconds);
                    PushTacticalEvent($"Signal gate armed: enemy deployment held {_scenarioWaveDelayBonus:0.0}s", 5.2f);
                    PlaySfxTone("scenario_signal_gate", 480f, 0.16f, 0.70f, true);
                    break;
            }

            _examScenarioDevice?.TriggerActivation();
            UpdateExamScenarioDevice();
            PlaySfxTone("scenario_command", 430f, 0.18f, 0.72f, true);
            SetStatus($"Scenario command active: {_activeScenarioMechanic.displayName}.");
            AdvanceTutorial(TDFirstRunTutorialStep.UseScenario);
        }

        private IEnumerator DeliverScenarioReinforcement()
        {
            _scenarioReinforcementPending = true;
            yield return new WaitForSeconds(Mathf.Max(1f, _activeScenarioMechanic?.reinforcementDelaySeconds ?? 6f));
            if (!_gameOver)
            {
                var reward = Mathf.Max(10, (_activeScenarioMechanic?.budgetCost ?? 10) * 2);
                _defenseBudget += reward;
                TrackP125ReinforcementIncome(reward);
                PushTacticalEvent($"Reserve train arrived: +{reward} budget", 5.4f);
                PlaySfxTone("scenario_reinforcement", 610f, 0.22f, 0.78f, true);
                _examScenarioDevice?.TriggerActivation();
            }

            _scenarioReinforcementPending = false;
        }

        private void ActivateScenarioEnvironmentDevice()
        {
            var affected = 0;
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                enemy.ApplyArmorBreak(3, Mathf.Max(3f, _activeScenarioMechanic.effectDurationSeconds));
                enemy.ApplyStagger(0.8f, enemy.HasTag("boss") ? 0.72f : 0.18f);
                enemy.TakeHit(Mathf.Max(6, Mathf.RoundToInt(enemy.MaxHealth * 0.08f)), 0.38f, 3.5f);
                affected++;
            }

            PushTacticalEvent($"Kiln purge: {affected} enemies scorched, broken and staggered", 5.6f);
        }

        private void ActivateScenarioBossBreaker()
        {
            var affected = 0;
            for (var i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null || !enemy.HasAnyTag("boss", "final", "elite"))
                {
                    continue;
                }

                enemy.ApplyExposed(Mathf.Max(4f, _activeScenarioMechanic.effectDurationSeconds), 1.22f);
                enemy.ApplyStagger(1.2f, 0.36f);
                affected++;
            }

            _scenarioBossPhaseSuppressed = true;
            PushTacticalEvent($"Phase breaker: {affected} boss target exposed; next surge suppressed", 6.0f);
        }

        private void UpdateScenarioBossPhases()
        {
            if (_activeScenarioMechanic == null ||
                NormalizeGroupToken(_activeScenarioMechanic.mechanicType) != "boss_phase" ||
                _isInPrepPhase || _gameOver)
            {
                return;
            }

            TDEnemy boss = null;
            for (var i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                if (enemy != null && enemy.HasAnyTag("boss", "final", "elite"))
                {
                    boss = enemy;
                    break;
                }
            }

            if (boss == null)
            {
                return;
            }

            var thresholds = _activeScenarioMechanic.bossPhaseThresholds;
            if (thresholds == null || thresholds.Length == 0)
            {
                thresholds = new[] { 0.70f, 0.35f };
            }

            while (_scenarioBossPhase < thresholds.Length && boss.HealthRatio <= thresholds[_scenarioBossPhase])
            {
                TriggerScenarioBossPhase(boss, _scenarioBossPhase + 2);
                _scenarioBossPhase++;
                _examScenarioDevice?.TriggerActivation();
            }
        }

        private void TriggerScenarioBossPhase(TDEnemy boss, int phaseNumber)
        {
            RecordEnemyCodexObservation(boss?.EnemyId, TDEnemyCodexObservation.BossPhase);
            TrackP135BossPhase(_scenarioBossPhaseSuppressed);
            if (_scenarioBossPhaseSuppressed)
            {
                _scenarioBossPhaseSuppressed = false;
                PushTacticalEvent($"Boss phase {phaseNumber}: surge cancelled by Phase Breaker", 6.2f);
                _battlePresentation?.ShowCinematic(
                    TDBattleCinematicKind.BossPhase,
                    "[B]",
                    $"BOSS PHASE {phaseNumber} CANCELLED",
                    "SURGE DENIED",
                    TDBattleFeedbackTier.Tactical,
                    1.15f);
                return;
            }

            boss.ApplyScenarioSpeed(7f, 1.12f + (_scenarioBossPhase * 0.10f));
            for (var i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                if (enemy != null && !ReferenceEquals(enemy, boss))
                {
                    enemy.ApplyScenarioSpeed(5f, 1.08f + (_scenarioBossPhase * 0.04f));
                }
            }

            if (_enemyCatalog.TryGetValue("ash_swarm", out var entry))
            {
                var count = 3 + (_scenarioBossPhase * 2);
                for (var i = 0; i < count; i++)
                {
                    var lane = ResolveAllLaneKey("pressure_mix", i);
                    _runtimeSpawnIndex++;
                    SpawnEnemy(entry, GetSpawnPathForLane(lane), _wave, 20000 + _runtimeSpawnIndex, lane);
                }
            }

            PushTacticalEvent($"BOSS PHASE {phaseNumber}: overdrive and Ash Swarm reinforcement", 6.8f);
            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.BossPhase,
                "[B!]",
                $"BOSS PHASE {phaseNumber}",
                "OVERDRIVE  /  ASH SWARM",
                TDBattleFeedbackTier.Critical,
                1.45f);
            PlayCriticalSfxTone("boss_phase", 190f + (phaseNumber * 55f), 0.34f, 0.92f, true);
        }

        private void UpdateResonanceCommandPanelUi()
        {
            if (_uiResonanceCommandPanel == null)
            {
                return;
            }

            var show = _isResonanceSystemEnabled && IsResonanceWindowActive && !_gameOver;
            _uiResonanceCommandPanel.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var emberAligned = CountOwnedSpecializationsForCommand(TDResonanceCommand.EmberSurge, out var emberThreatFit);
            var fractureAligned = CountOwnedSpecializationsForCommand(TDResonanceCommand.FractureMark, out var fractureThreatFit);
            var emberThreatMatch = IsResonanceCommandMatchForCurrentThreat(TDResonanceCommand.EmberSurge);
            var fractureThreatMatch = IsResonanceCommandMatchForCurrentThreat(TDResonanceCommand.FractureMark);
            var awaitingCommand = _activeResonanceCommand == TDResonanceCommand.None;

            SetUiText(
                _uiResonanceCommandTitleText,
                awaitingCommand
                    ? $"RESONANCE COMMAND  {_resonanceWindowTimer:0.0}s  /  {GetDoctrineShortLabel(_activeResonanceDoctrine)}"
                    : $"{GetResonanceCommandShortLabel(_activeResonanceCommand).ToUpperInvariant()}  {_resonanceWindowTimer:0.0}s  /  {GetDoctrineShortLabel(_activeResonanceDoctrine)}");
            SetUiText(
                _uiResonanceCommandForecastText,
                awaitingCommand
                    ? $"Threat  Ember {(emberThreatMatch ? "MATCH" : "off")}  |  Fracture {(fractureThreatMatch ? "MATCH" : "off")}\n" +
                      $"Owned specs  Ember {emberThreatFit}/{emberAligned} fit  |  Fracture {fractureThreatFit}/{fractureAligned} fit"
                    : BuildMatrixWindowStatusLabel());

            SetUiText(_uiEmberCommandButtonText, $"Z  EMBER SURGE\n{emberThreatFit}/{emberAligned} specs / {GetDoctrineCommandBoostLabel(TDResonanceCommand.EmberSurge)}");
            SetUiText(_uiFractureCommandButtonText, $"X  FRACTURE MARK\n{fractureThreatFit}/{fractureAligned} specs / {GetDoctrineCommandBoostLabel(TDResonanceCommand.FractureMark)}");
            _uiEmberCommandButton.interactable = awaitingCommand;
            _uiFractureCommandButton.interactable = awaitingCommand;

            if (_uiEmberCommandButtonImage != null)
            {
                _uiEmberCommandButtonImage.color = _activeResonanceCommand == TDResonanceCommand.EmberSurge
                    ? new Color(0.88f, 0.42f, 0.14f, 0.98f)
                    : awaitingCommand
                        ? new Color(0.46f, 0.25f, 0.12f, 0.94f)
                        : new Color(0.16f, 0.17f, 0.18f, 0.72f);
            }

            if (_uiFractureCommandButtonImage != null)
            {
                _uiFractureCommandButtonImage.color = _activeResonanceCommand == TDResonanceCommand.FractureMark
                    ? new Color(0.16f, 0.60f, 0.76f, 0.98f)
                    : awaitingCommand
                        ? new Color(0.12f, 0.34f, 0.46f, 0.94f)
                        : new Color(0.16f, 0.17f, 0.18f, 0.72f);
            }
        }

        private void UpdateWaveIntelUi()
        {
            if (_uiWaveIntelPanel == null)
            {
                return;
            }

            var show = _isInPrepPhase && !_gameOver && _campaignDeploymentConfirmed && _currentWaveDefinition != null &&
                       !_missionBoardOpen && !_formationPanelOpen && !_campaignProfileOpen;
            _uiWaveIntelPanel.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var normalizedPhase = NormalizeGroupToken(_currentWavePhase).ToUpperInvariant();
            SetUiText(
                _uiWaveIntelTitleText,
                TDLocalization.IsChinese
                    ? $"下一波  {_wave:00}  /  {GetLocalizedWavePhaseLabel(normalizedPhase)}"
                    : $"NEXT WAVE  {_wave:00}  /  {normalizedPhase}");
            SetUiText(_uiWaveIntelBodyText, BuildCompactWaveIntelBodyLabel());
            SetUiText(_uiWaveIntelEnemyText, BuildCompactWaveCompositionLabel(_currentWaveDefinition));
            SetUiText(_uiWaveIntelProfileText, BuildCompactEnemyProfileLabel(_currentWaveDefinition));
            SetUiText(_uiWaveIntelRouteText, BuildWaveRouteLabel(_currentWaveDefinition));
            var readiness = CalculateDefenseReadiness(_currentWaveDefinition);
            SetUiText(
                _uiWaveIntelReadinessText,
                TDLocalization.IsChinese
                    ? $"战备  {readiness.score:00} {readiness.grade}   覆盖 {readiness.coverageScore:00}   克制 {readiness.counterScore:00}"
                    : $"READINESS  {readiness.score:00} {readiness.grade}   COV {readiness.coverageScore:00}   CTR {readiness.counterScore:00}");
        }

        private void UpdateTowerBuildButtonUi()
        {
            if (_uiTowerBarRoot != null)
            {
                _uiTowerBarRoot.gameObject.SetActive(IsBuildWindowOpen() && !_gameOver &&
                                                     !_missionBoardOpen && !_formationPanelOpen && !_campaignProfileOpen);
            }

            for (var i = 0; i < _uiTowerButtons.Count; i++)
            {
                if (i >= _unlockedTowerKinds.Count)
                {
                    _uiTowerButtons[i].gameObject.SetActive(false);
                    continue;
                }

                var kind = _unlockedTowerKinds[i];
                var cost = TDTower.GetBuildCost(kind);
                var selected = kind == _selectedTowerKind;
                var affordable = _defenseBudget >= cost;
                var button = _uiTowerButtons[i];
                var label = _uiTowerButtonTexts[i];
                var identity = TDUiVisualIdentity.GetTower(kind);
                button.gameObject.SetActive(true);
                button.interactable = true;
                SetUiText(label, $"{i + 1}\n{cost}");

                if (i < _uiTowerButtonIcons.Count && _uiTowerButtonIcons[i] != null)
                {
                    _uiTowerButtonIcons[i].color = affordable
                        ? Color.white
                        : new Color(0.52f, 0.58f, 0.60f, 0.72f);
                }

                if (i < _uiTowerButtonAccents.Count && _uiTowerButtonAccents[i] != null)
                {
                    var accent = identity.accent;
                    accent.a = affordable ? 1f : 0.42f;
                    _uiTowerButtonAccents[i].color = accent;
                }

                if (i < _uiTowerButtonOutlines.Count && _uiTowerButtonOutlines[i] != null)
                {
                    _uiTowerButtonOutlines[i].enabled = selected;
                    _uiTowerButtonOutlines[i].effectColor = new Color(identity.accent.r, identity.accent.g, identity.accent.b, 0.96f);
                }

                if (button.targetGraphic is Image image)
                {
                    image.color = selected
                        ? Color.Lerp(new Color(0.10f, 0.14f, 0.16f, 0.98f), identity.accent, 0.30f)
                        : affordable && IsBuildWindowOpen()
                            ? new Color(0.11f, 0.18f, 0.22f, 0.92f)
                            : new Color(0.10f, 0.11f, 0.12f, 0.64f);
                }
            }
        }

        private void UpdateTowerUpgradePanelUi()
        {
            var tower = GetUiFocusedTower();
            if (_uiTowerPanelRoot == null)
            {
                return;
            }

            var show = tower != null && tower.gameObject != null && !_gameOver &&
                       (IsBuildWindowOpen() || _hoveredTower == tower);
            _uiTowerPanelRoot.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var identity = TDUiVisualIdentity.GetTower(tower.Kind);
            SetUiText(_uiTowerTitleText, $"{tower.DisplayName}  T{tower.Tier + 1}  /  {identity.roleLabel}");
            SetUiText(_uiTowerStatsText, BuildTowerStatsLabel(tower));
            SetUiText(_uiTowerPreviewText, BuildTowerUpgradePreviewLabel(tower));
            SetUiText(_uiTowerUpgradeText, tower.CanUpgrade
                ? BuildTowerMatrixHint(tower)
                : $"Active matrix: {tower.SpecializationLabel} | {tower.SpecializationEffectLabel}");

            if (_uiTowerIdentityIcon != null)
            {
                _uiTowerIdentityIcon.sprite = LoadUiSprite(identity.iconResourcePath);
                _uiTowerIdentityIcon.color = Color.white;
            }

            if (_uiTowerIdentityStripe != null)
            {
                _uiTowerIdentityStripe.color = identity.accent;
            }

            var canUpgrade = tower.CanUpgrade && IsBuildWindowOpen() && !_gameOver;
            var damageCost = tower.CanUpgrade ? tower.GetUpgradeCost(TDTowerUpgradeBranch.Damage) : 0;
            var utilityCost = tower.CanUpgrade ? tower.GetUpgradeCost(TDTowerUpgradeBranch.Utility) : 0;
            SetUpgradeButtonUi(_uiDamageUpgradeButton, _uiDamageUpgradeButtonText, "Damage", damageCost, canUpgrade && _defenseBudget >= damageCost, BuildUpgradeButtonPreview(tower, TDTowerUpgradeBranch.Damage));
            SetUpgradeButtonUi(_uiUtilityUpgradeButton, _uiUtilityUpgradeButtonText, "Utility", utilityCost, canUpgrade && _defenseBudget >= utilityCost, BuildUpgradeButtonPreview(tower, TDTowerUpgradeBranch.Utility));
        }

        private void UpdateGameOverUi()
        {
            if (_uiGameOverRoot == null)
            {
                return;
            }

            if (_uiGameOverScrim != null)
            {
                _uiGameOverScrim.gameObject.SetActive(_gameOver);
            }

            _uiGameOverRoot.gameObject.SetActive(_gameOver);
            if (!_gameOver)
            {
                return;
            }

            var campaignSummary = GetCampaignProgressSummary();
            var campaignComplete = IsFullCampaignCompletionResult(campaignSummary);
            if (campaignComplete)
            {
                SetRunResultChartsVisible(false);
                UpdateCampaignCompletionUi(campaignSummary);
            }
            else
            {
                SetRunResultChartsVisible(true);
                var masteryLabel = _victory
                    ? $"   {(TDLocalization.IsChinese ? "精通" : "MASTERY")} {_currentMissionStars}/3"
                    : string.Empty;
                var endingLabel = _examPresentationProfile == null
                    ? (_victory ? "Mission Complete" : "Line Broken")
                    : _victory ? _examPresentationProfile.victoryEnding : _examPresentationProfile.defeatEnding;
                var rewardLabel = _newlyClaimedChapterReward == null
                    ? string.Empty
                    : $"   Chapter reward: {_newlyClaimedChapterReward.displayName}";
                SetUiText(_uiGameOverTitleText, $"{endingLabel}   {GetDifficultyShortLabel(_activeCampaignDifficulty)}{masteryLabel}");
                SetUiText(
                    _uiGameOverBodyText,
                    TDLocalization.IsChinese
                        ? $"{(_victory ? "通关" : "结束")} W{_wave:00}{BuildLocalizedChapterRewardLabel()}   波次 {_wavesCleared}/{GetConfiguredWaveCount()}   击杀 {_totalKills}   漏怪 {_totalEscapes}   战役 {campaignSummary.clearedLevels}/{campaignSummary.totalLevels}"
                        : $"{(_victory ? "CLEARED" : "ENDED")} W{_wave:00}{rewardLabel}   WAVES {_wavesCleared}/{GetConfiguredWaveCount()}   KILLS {_totalKills}   LEAKS {_totalEscapes}   CAMPAIGN {campaignSummary.clearedLevels}/{campaignSummary.totalLevels}");
                SetUiText(_uiGameOverScoreText, BuildRunScoreHeaderLabel());
                SetUiText(_uiGameOverLaneText, TDLocalization.IsChinese ? "路线控制   击杀 / 投放" : "LANE CONTROL   KILLS / DEPLOYED");
                SetUiText(_uiGameOverTowerText, TDLocalization.IsChinese ? "防御塔贡献   伤害 / 击杀" : "TOWER CONTRIBUTION   DAMAGE / KILLS");
                UpdateRunResultCharts();
                SetUiText(_uiGameOverHeatText, BuildRoadHeatLabel());
                SetUiText(_uiGameOverFailureText, $"{(TDLocalization.IsChinese ? "失败原因" : "FAILURE")}   {BuildFailureUiLabel()}");
                SetUiText(_uiGameOverRecapText, BuildRunRecapLabel());
                SetUiText(_uiGameOverRecommendationText, BuildRunRecommendationLabel());
            }

            var hasNextMission = _campaignRoute?.level != null &&
                                 _campaignRoute.level.levelIndex < _campaignRoute.totalLevels;
            var nextUnlocked = hasNextMission &&
                               TDCampaignProgression.IsLevelUnlocked(_campaignRoute.level.levelIndex + 1, _campaignRoute.totalLevels);
            if (_uiNextMissionButton != null)
            {
                _uiNextMissionButton.interactable = campaignComplete || (_victory && nextUnlocked);
                var nextMissionLabel = campaignComplete
                    ? (TDLocalization.IsChinese ? "战役档案" : "Campaign Archive")
                    : !hasNextMission
                        ? (TDLocalization.IsChinese ? "战役完成" : "Campaign Complete")
                        : nextUnlocked
                            ? (TDLocalization.IsChinese ? "下一任务" : "Next Mission")
                            : (TDLocalization.IsChinese ? "下一任务未解锁" : "Next Locked");
                SetUiText(_uiNextMissionButtonText, nextMissionLabel);
            }

            SetUiText(_uiRestartButtonText, "Retry");
        }

        private string BuildLocalizedChapterRewardLabel()
        {
            return _newlyClaimedChapterReward == null
                ? string.Empty
                : $"   章节奖励：{TDLocalization.LocalizeRuntimeString(_newlyClaimedChapterReward.displayName)}";
        }

        private void UpdateRunResultResponsiveScale()
        {
            var scale = Screen.height <= 600
                ? 1.22f
                : Screen.height <= 760 ? 1.10f : 1f;
            _uiGameOverRoot.localScale = Vector3.one * scale;
        }

        private bool IsFullCampaignCompletionResult(TDCampaignProgressSummary summary)
        {
            return _victory && _campaignRoute?.level != null && summary != null &&
                   _campaignRoute.level.levelIndex == _campaignRoute.totalLevels &&
                   summary.clearedLevels == summary.totalLevels;
        }

        private void UpdateCampaignCompletionUi(TDCampaignProgressSummary summary)
        {
            var masteredChapters = GetMasteredChapterCount();
            var rank = BuildCampaignRank(summary, masteredChapters);
            var campaignPerfected = summary.emberTrialClears == summary.totalLevels;
            var totalAttempts = 0;
            var perfectMissions = 0;
            var totalBestScore = 0;
            for (var level = 1; level <= summary.totalLevels; level++)
            {
                var progress = TDCampaignProgression.GetLevelProgress(level);
                totalAttempts += progress.attempts;
                totalBestScore += progress.bestTacticalScore;
                if (progress.bestStars == 3)
                {
                    perfectMissions++;
                }
            }

            var averageBestScore = summary.totalLevels > 0 ? Mathf.RoundToInt(totalBestScore / (float)summary.totalLevels) : 0;
            if (TDLocalization.IsChinese)
            {
                var victoryEnding = TDLocalization.LocalizeRuntimeString(
                    _examPresentationProfile?.victoryEnding ?? (campaignPerfected ? "CAMPAIGN PERFECTED" : "CAMPAIGN COMPLETE"));
                SetUiText(
                    _uiGameOverTitleText,
                    campaignPerfected
                        ? $"{victoryEnding}   余烬试炼"
                        : $"{victoryEnding}   评级 {rank}");
                SetUiText(
                    _uiGameOverBodyText,
                    $"余烬防线已守住   {summary.clearedLevels}/{summary.totalLevels} 项任务完成\n" +
                    $"最终行动 L{_campaignRoute.level.levelIndex:00}   {TDLocalization.LocalizeRuntimeString(GetDifficultyShortLabel(_activeCampaignDifficulty))}   击杀 {_totalKills}   剩余防线 {_lineIntegrity}");
                SetUiText(
                    _uiGameOverScoreText,
                    $"战役精通   星级 {summary.earnedStars}/{summary.availableStars}   契约 {summary.completedContracts}/{summary.availableContracts}\n" +
                    $"精通章节 {masteredChapters}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   完美任务 {perfectMissions}/{summary.totalLevels}\n" +
                    $"挑战  老兵 {summary.veteranClears}/{summary.totalLevels}   余烬 {summary.emberTrialClears}/{summary.totalLevels}   图鉴  敌人 {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   防御塔 {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}");
            }
            else
            {
                SetUiText(
                    _uiGameOverTitleText,
                    campaignPerfected
                        ? $"{_examPresentationProfile?.victoryEnding ?? "CAMPAIGN PERFECTED"}   EMBER TRIAL"
                        : $"{_examPresentationProfile?.victoryEnding ?? "CAMPAIGN COMPLETE"}   RANK {rank}");
                SetUiText(
                    _uiGameOverBodyText,
                    $"EMBERLINE SECURED   {summary.clearedLevels}/{summary.totalLevels} missions cleared\n" +
                    $"Final operation L{_campaignRoute.level.levelIndex:00}   {GetDifficultyShortLabel(_activeCampaignDifficulty)}   {_totalKills} kills   {_lineIntegrity} integrity remaining");
                SetUiText(
                    _uiGameOverScoreText,
                    $"CAMPAIGN MASTERY   STARS {summary.earnedStars}/{summary.availableStars}   CONTRACTS {summary.completedContracts}/{summary.availableContracts}\n" +
                    $"MASTERED CHAPTERS {masteredChapters}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   PERFECT MISSIONS {perfectMissions}/{summary.totalLevels}\n" +
                    $"CHALLENGE V {summary.veteranClears}/{summary.totalLevels}   EMBER {summary.emberTrialClears}/{summary.totalLevels}   DOSSIERS E {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}  T {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}");
            }

            SetUiText(_uiGameOverLaneText, BuildCampaignCompletionChapterLabel());
            SetUiText(
                _uiGameOverTowerText,
                TDLocalization.IsChinese
                    ? $"战役记录\n部署次数 {totalAttempts}\n平均最佳战术评分 {averageBestScore}\n最远前线 L{summary.highestUnlockedLevel:00}\n存档版本 {TDCampaignProgression.SaveVersion}"
                    : $"CAMPAIGN RECORD\nDeployments {totalAttempts}\nAverage best tactical score {averageBestScore}\nFrontier L{summary.highestUnlockedLevel:00}\nSave version {TDCampaignProgression.SaveVersion}");
            SetUiText(_uiGameOverHeatText, BuildCampaignCompletionRewardLabel());
            SetUiText(
                _uiGameOverFailureText,
                TDLocalization.IsChinese
                    ? _newlyClaimedChapterReward == null
                        ? $"档案状态   {TDCampaignProgression.GetClaimedChapterRewardIds().Length} 项章节奖励生效"
                        : $"最终奖励已取得   {TDLocalization.LocalizeRuntimeString(_newlyClaimedChapterReward.displayName)}"
                    : _newlyClaimedChapterReward == null
                        ? $"ARCHIVE STATUS   {TDCampaignProgression.GetClaimedChapterRewardIds().Length} chapter rewards active"
                        : $"FINAL REWARD SECURED   {_newlyClaimedChapterReward.displayName}");
            SetUiText(
                _uiGameOverRecapText,
                TDLocalization.IsChinese
                    ? campaignPerfected
                        ? "全部任务均已在余烬试炼压力下完成，完整挑战档案已经解锁。"
                        : $"全战役现可使用全部已解锁防御塔、阵容与长期奖励重玩。评级 {rank} 综合星级、契约与章节精通。"
                    : campaignPerfected
                        ? "Every mission has now been cleared under Ember Trial pressure. The complete challenge archive is secured."
                        : $"The full campaign is now replayable with every unlocked tower, formation and claimed legacy bonus. Rank {rank} reflects stars, contracts and complete chapter mastery.");
            SetUiText(_uiGameOverRecommendationText, BuildCampaignCompletionRecommendationLabel());
        }

        private string BuildCampaignCompletionChapterLabel()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            var lines = new List<string> { TDLocalization.IsChinese ? "章节精通" : "CHAPTER MASTERY" };
            for (var i = 0; i < chapters.Length; i++)
            {
                var progress = TDCampaignProgression.BuildChapterSummary(chapters[i]);
                lines.Add(TDLocalization.IsChinese
                    ? $"{(char)('A' + i)} {(progress.mastered ? "已精通" : "已通关")}   星 {progress.earnedStars}/{progress.availableStars}   契 {progress.completedContracts}/{progress.availableContracts}   奖 {(progress.rewardClaimed ? "生效" : "待领取")}"
                    : $"{(char)('A' + i)} {(progress.mastered ? "MASTERED" : "CLEARED")}   S {progress.earnedStars}/{progress.availableStars}   C {progress.completedContracts}/{progress.availableContracts}   R {(progress.rewardClaimed ? "ACTIVE" : "READY")}");
            }

            return string.Join("\n", lines);
        }

        private string BuildCampaignCompletionRecommendationLabel()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            var targets = new List<string>();
            for (var i = 0; i < chapters.Length; i++)
            {
                var progress = TDCampaignProgression.BuildChapterSummary(chapters[i]);
                var missingStars = progress.availableStars - progress.earnedStars;
                var missingContracts = progress.availableContracts - progress.completedContracts;
                if (missingStars > 0 || missingContracts > 0)
                {
                    targets.Add(TDLocalization.IsChinese
                        ? $"章节 {(char)('A' + i)}：星级 +{missingStars} / 契约 +{missingContracts}"
                        : $"Chapter {(char)('A' + i)}: +{missingStars} stars / +{missingContracts} contracts");
                }
            }

            if (targets.Count == 0)
            {
                return TDLocalization.IsChinese
                    ? "下一目标   已达成全部精通。使用不同阵容、学说与 A/B 布防重玩战役。"
                    : "NEXT OBJECTIVE   Full mastery achieved. Compare alternate formations, doctrines and A/B layouts across the campaign.";
            }

            return TDLocalization.IsChinese
                ? $"下一精通目标   {string.Join("   |   ", targets.GetRange(0, Mathf.Min(3, targets.Count)))}"
                : $"NEXT MASTERY TARGETS   {string.Join("   |   ", targets.GetRange(0, Mathf.Min(3, targets.Count)))}";
        }

        private void UpdateMissionBoardUi()
        {
            if (_uiMissionBoardRoot == null)
            {
                return;
            }

            if (_uiMissionButton != null)
            {
                _uiMissionButton.interactable = _campaignRoute?.level != null &&
                                                (_gameOver || _isInPrepPhase || !_campaignDeploymentConfirmed);
            }

            if (_uiMissionBoardScrim != null)
            {
                _uiMissionBoardScrim.gameObject.SetActive(_missionBoardOpen);
            }

            _uiMissionBoardRoot.gameObject.SetActive(_missionBoardOpen);
            if (_uiFormationRoot != null)
            {
                _uiFormationRoot.gameObject.SetActive(_missionBoardOpen && _formationPanelOpen);
            }

            if (_uiCampaignProfileRoot != null)
            {
                _uiCampaignProfileRoot.gameObject.SetActive(_missionBoardOpen && _campaignProfileOpen);
            }

            if (!_missionBoardOpen || !_missionBoardNeedsRefresh)
            {
                return;
            }

            RefreshMissionBoardUi();
            _missionBoardNeedsRefresh = false;
        }

        private void RefreshMissionBoardUi()
        {
            if (_campaign == null || _campaignRoute?.level == null)
            {
                return;
            }

            var compactBoard = Screen.height <= 600;
            var summary = GetCampaignProgressSummary();
            SetUiText(
                _uiMissionBoardProgressText,
                compactBoard
                    ? $"CLEAR {summary.clearedLevels}/{summary.totalLevels}   STAR {summary.earnedStars}/{summary.availableStars}   FRONTIER L{summary.highestUnlockedLevel:00}"
                    : $"CAMPAIGN PROGRESS   CLEAR {summary.clearedLevels}/{summary.totalLevels}   STAR {summary.earnedStars}/{summary.availableStars}   CONTRACT {summary.completedContracts}/{summary.availableContracts}   FRONTIER L{summary.highestUnlockedLevel:00}");

            _missionBoardSelectedChapter = Mathf.Clamp(_missionBoardSelectedChapter, 0, Mathf.Max(0, _uiMissionChapterButtons.Count - 1));

            for (var chapterIndex = 0; chapterIndex < _uiMissionChapterProgressTexts.Count; chapterIndex++)
            {
                var chapterDefinition = GetCampaignChapterAt(chapterIndex);
                var chapterProgress = TDCampaignProgression.BuildChapterSummary(chapterDefinition);
                var title = chapterIndex < _uiMissionChapterTitleTexts.Count ? _uiMissionChapterTitleTexts[chapterIndex] : null;
                var rewardButton = chapterIndex < _uiMissionChapterRewardButtons.Count ? _uiMissionChapterRewardButtons[chapterIndex] : null;
                var rewardLabel = chapterIndex < _uiMissionChapterRewardButtonTexts.Count ? _uiMissionChapterRewardButtonTexts[chapterIndex] : null;
                var themeLabel = chapterDefinition?.themeTags != null && chapterDefinition.themeTags.Length > 0
                    ? FormatCampaignTags(chapterDefinition.themeTags, 2).ToUpperInvariant()
                    : "SECTOR";
                SetUiText(title, $"CHAPTER {(char)('A' + chapterIndex)}  {themeLabel}");
                SetUiText(
                    _uiMissionChapterProgressTexts[chapterIndex],
                    compactBoard
                        ? $"CLEAR {chapterProgress.clearedLevels}/{chapterProgress.totalLevels}   STAR {chapterProgress.earnedStars}/{chapterProgress.availableStars}   CONTRACT {chapterProgress.completedContracts}/{chapterProgress.availableContracts}"
                        : $"CHAPTER PROGRESS   CLEAR {chapterProgress.clearedLevels}/{chapterProgress.totalLevels}   STAR {chapterProgress.earnedStars}/{chapterProgress.availableStars}   CONTRACT {chapterProgress.completedContracts}/{chapterProgress.availableContracts}   V {chapterProgress.veteranClears}/{chapterProgress.totalLevels}   E {chapterProgress.emberTrialClears}/{chapterProgress.totalLevels}");
                var selectedChapter = chapterIndex == _missionBoardSelectedChapter;
                _uiMissionChapterProgressTexts[chapterIndex].gameObject.SetActive(selectedChapter);
                if (chapterIndex < _uiMissionChapterButtons.Count && _uiMissionChapterButtons[chapterIndex]?.targetGraphic is Image chapterImage)
                {
                    chapterImage.color = selectedChapter
                        ? new Color(0.28f, 0.54f, 0.60f, 1f)
                        : chapterProgress.cleared
                            ? new Color(0.16f, 0.34f, 0.28f, 0.96f)
                            : new Color(0.12f, 0.18f, 0.20f, 0.96f);
                }

                if (rewardButton != null)
                {
                    rewardButton.gameObject.SetActive(selectedChapter);
                    rewardButton.interactable = chapterProgress.cleared && !chapterProgress.rewardClaimed;
                    SetUiText(
                        rewardLabel,
                        chapterProgress.rewardClaimed
                            ? "REWARD ACTIVE"
                            : chapterProgress.cleared
                                ? "CLAIM REWARD"
                                : "REWARD LOCKED");
                    if (rewardButton.targetGraphic is Image rewardImage)
                    {
                        rewardImage.color = chapterProgress.rewardClaimed
                            ? new Color(0.16f, 0.38f, 0.30f, 0.94f)
                            : chapterProgress.cleared
                                ? new Color(0.52f, 0.34f, 0.12f, 0.98f)
                                : new Color(0.10f, 0.11f, 0.12f, 0.72f);
                    }
                }
            }

            // Refresh world map node states.
            if (_worldMap != null)
            {
                var totalLevels = _campaign.totalLevels;
                var highestUnlocked = TDCampaignProgression.GetHighestUnlockedLevel(totalLevels);
                var clearedArr = new bool[totalLevels];
                var starsArr = new int[totalLevels];
                for (var lvl = 1; lvl <= totalLevels; lvl++)
                {
                    var prog = TDCampaignProgression.GetLevelProgress(lvl);
                    clearedArr[lvl - 1] = prog.cleared;
                    starsArr[lvl - 1] = prog.bestStars;
                }

                _worldMap.Refresh(
                    _missionBoardSelectedLevel,
                    highestUnlocked,
                    clearedArr,
                    starsArr,
                    totalLevels,
                    20);
            }

            var selectedLevel = GetCampaignLevel(_missionBoardSelectedLevel) ?? _campaignRoute.level;
            var selectedMap = GetCampaignMap(selectedLevel.mapId);
            var selectedProgress = TDCampaignProgression.GetLevelProgress(selectedLevel.levelIndex);
            var selectedUnlocked = TDCampaignProgression.IsLevelUnlocked(selectedLevel.levelIndex, _campaign.totalLevels);
            var chapter = GetCampaignChapter(selectedLevel.chapterId);
            var chapterLabel = chapter != null && !string.IsNullOrWhiteSpace(chapter.displayName)
                ? chapter.displayName
                : selectedLevel.chapterId;
            var chapterThemes = chapter?.themeTags != null ? FormatCampaignTags(chapter.themeTags, 3).ToUpperInvariant() : "-";
            var chapterStart = chapter != null ? chapter.startLevel : selectedLevel.levelIndex;
            var chapterEnd = chapter != null ? chapter.endLevel : selectedLevel.levelIndex;
            SetUiText(
                _uiMissionChapterOverviewText,
                compactBoard
                    ? $"{chapterLabel.ToUpperInvariant()}   /   {chapterThemes}\n" +
                      $"L{chapterStart:00} > L{chapterStart + 1:00} > L{chapterStart + 2:00} > L{chapterStart + 3:00} > L{chapterEnd:00} EXAM"
                    : $"{chapterLabel.ToUpperInvariant()}   /   {chapterThemes}\n" +
                      $"L{chapterStart:00} INTRODUCE   >   L{chapterStart + 1:00} PRACTICE   >   L{chapterStart + 2:00} REINFORCE   >   L{chapterStart + 3:00} SYNTHESIS   >   L{chapterEnd:00} EXAM");
            var reward = chapter?.reward;
            SetUiText(
                _uiMissionChapterRewardText,
                reward == null
                    ? "CHAPTER REWARD   -"
                    : $"CHAPTER REWARD   {reward.displayName.ToUpperInvariant()}\n{reward.description}");
            var mapLabel = selectedMap != null && !string.IsNullOrWhiteSpace(selectedMap.displayName)
                ? selectedMap.displayName
                : selectedLevel.mapId;
            var missionType = selectedLevel.bossLevel ? "BOSS MISSION" : "FIELD MISSION";

            SetUiText(_uiMissionIntelTitleText, $"L{selectedLevel.levelIndex:00}  {missionType}\n{mapLabel}");

            BuildMissionWaveIntel(
                selectedLevel,
                out var waveCount,
                out var laneCount,
                out var composition,
                out var threatTags,
                out var loadError);
            var tacticalHook = selectedMap != null && !string.IsNullOrWhiteSpace(selectedMap.tacticalHook)
                ? selectedMap.tacticalHook
                : "Read the route pressure and build for the exam wave.";
            var goals = FormatCampaignTags(selectedLevel.goalTags, 3);
            SetUiText(
                _uiMissionIntelBriefText,
                compactBoard
                    ? $"{tacticalHook}\nOBJECTIVES  {goals}\n{waveCount} waves / {laneCount} {(laneCount == 1 ? "route" : "routes")}"
                    : $"{chapterLabel}\n{tacticalHook}\nOBJECTIVES  {goals}\nSCOPE  {waveCount} waves / {laneCount} {(laneCount == 1 ? "route" : "routes")}");

            var threatLabel = BuildMissionDisplayThreatLabel(threatTags);
            SetUiText(
                _uiMissionIntelThreatText,
                $"THREAT PACKAGE\n{composition}\nTRAITS  {threatLabel}{(string.IsNullOrWhiteSpace(loadError) ? string.Empty : $"\nIntel error: {loadError}")}");
            SetUiText(_uiMissionIntelContractText, BuildMissionContractBrief(selectedLevel, selectedProgress));
            SetUiText(_uiMissionIntelCounterText, BuildMissionCounterPlan(selectedLevel.levelIndex, threatTags));

            var recordLabel = selectedProgress.cleared
                ? $"CLEARED  Best {selectedProgress.bestStars}/3   Score {selectedProgress.bestTacticalScore}   Integrity {selectedProgress.bestIntegrity}"
                : selectedUnlocked
                    ? "STATUS  Ready for first deployment"
                    : $"STATUS  Locked until L{Mathf.Max(1, selectedLevel.levelIndex - 1):00} is cleared";
            var arrivals = BuildMissionArrivalLabel(selectedLevel);
            SetUiText(
                _uiMissionIntelRecordText,
                compactBoard
                    ? $"{recordLabel}\n{GetDifficultyRecordLabel(selectedProgress)}   ATTEMPTS {selectedProgress.attempts}\n{arrivals}"
                    : $"{recordLabel}\nCHALLENGE  {GetDifficultyRecordLabel(selectedProgress)}   ATTEMPTS {selectedProgress.attempts}\nMASTERIES  Clear / Integrity {GetMissionIntegrityStarThreshold(selectedLevel)}+ / Tactical {MissionTacticalStarThreshold}+\n{arrivals}");

            if (_uiMissionDeployButton != null)
            {
                _uiMissionDeployButton.interactable = selectedUnlocked;
                var currentLevel = _campaignRoute.level.levelIndex;
                var deployLabel = selectedLevel.levelIndex != currentLevel
                    ? $"Plan L{selectedLevel.levelIndex:00}"
                    : _gameOver
                        ? "Formation & Replay"
                        : _campaignDeploymentConfirmed
                            ? "Review Formation"
                            : "Set Formation";
                SetUiText(_uiMissionDeployButtonText, deployLabel);
            }

            if (_uiMissionCloseButton != null)
            {
                _uiMissionCloseButton.interactable = _gameOver || _campaignDeploymentConfirmed;
            }

            SetUiText(
                _uiMissionCloseButtonText,
                _gameOver
                    ? "Back to Result"
                    : _campaignDeploymentConfirmed
                        ? "Back"
                        : "Formation Required");
            if (_formationPanelOpen)
            {
                RefreshFormationPanelUi();
            }

            if (_campaignProfileOpen)
            {
                RefreshCampaignProfileUi();
            }
        }

        private void OpenMissionBoard()
        {
            if (_campaignRoute?.level == null)
            {
                SetStatus("Campaign route unavailable.");
                return;
            }

            if (!_gameOver && _campaignDeploymentConfirmed && !_isInPrepPhase)
            {
                SetStatus("Mission board is available during prep.");
                return;
            }

            _missionBoardSelectedLevel = _campaignRoute.level.levelIndex;
            _missionBoardSelectedChapter = Mathf.Clamp((_missionBoardSelectedLevel - 1) / 5, 0, 3);
            _missionBoardOpen = true;
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _missionBoardNeedsRefresh = true;
            _gridMap?.HideBuildPreview();
            HideRangePreview();
            HideRoutePreview();
            StartCoroutine(SelectUiNextFrame(GetMissionLevelButton(_missionBoardSelectedLevel)));
            TDUiAnimator.PanelOpen(this, _uiMissionBoardRoot);
            PlaySfxTone("ui_panel_open", 540f, 0.10f, 0.52f, true);
        }

        private void CloseMissionBoard()
        {
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _missionBoardOpen = false;
            if (!_campaignDeploymentConfirmed && !_gameOver)
            {
                _campaignDeploymentConfirmed = true;
                var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
                SetStatus($"Mission L{levelIndex:00} deployed.");
            }

            if (EventSystem.current != null && _uiMissionButton != null)
            {
                EventSystem.current.SetSelectedGameObject(_uiMissionButton.gameObject);
            }

            TDUiAnimator.PanelClose(this, _uiMissionBoardRoot);
            PlaySfxTone("ui_panel_close", 420f, 0.08f, 0.48f, false);
        }

        private void SelectMissionBoardLevel(int levelIndex)
        {
            if (_campaign == null || !TDCampaignProgression.IsLevelUnlocked(levelIndex, _campaign.totalLevels))
            {
                return;
            }

            _missionBoardSelectedLevel = Mathf.Clamp(levelIndex, 1, _campaign.totalLevels);
            _missionBoardSelectedChapter = Mathf.Clamp((_missionBoardSelectedLevel - 1) / 5, 0, 3);
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _missionBoardNeedsRefresh = true;
            PlaySfxTone("ui_level_select", 620f, 0.09f, 0.52f, true);
        }

        private void SelectMissionBoardChapter(int chapterIndex)
        {
            if (_campaign == null)
            {
                return;
            }

            _missionBoardSelectedChapter = Mathf.Clamp(chapterIndex, 0, Mathf.Max(0, _campaign.chapters.Length - 1));
            var chapter = GetCampaignChapterAt(_missionBoardSelectedChapter);
            if (chapter != null &&
                (_missionBoardSelectedLevel < chapter.startLevel || _missionBoardSelectedLevel > chapter.endLevel))
            {
                var firstUnlocked = chapter.startLevel;
                for (var level = chapter.startLevel; level <= chapter.endLevel; level++)
                {
                    if (TDCampaignProgression.IsLevelUnlocked(level, _campaign.totalLevels))
                    {
                        firstUnlocked = level;
                        break;
                    }
                }

                _missionBoardSelectedLevel = firstUnlocked;
            }

            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _missionBoardNeedsRefresh = true;
            RefreshMissionBoardUi();
            StartCoroutine(SelectUiNextFrame(GetMissionLevelButton(_missionBoardSelectedLevel)));
        }

        private Button GetMissionLevelButton(int levelIndex)
        {
            var index = levelIndex - 1;
            return index >= 0 && index < _uiMissionLevelButtons.Count ? _uiMissionLevelButtons[index] : null;
        }

        private void TryClaimChapterReward(int chapterIndex)
        {
            var chapter = GetCampaignChapterAt(chapterIndex);
            var progress = TDCampaignProgression.BuildChapterSummary(chapter);
            var reward = chapter?.reward;
            if (reward == null || !progress.cleared || progress.rewardClaimed)
            {
                return;
            }

            if (!TDCampaignProgression.ClaimChapterReward(reward.rewardId))
            {
                SetStatus("Chapter reward could not be claimed.");
                return;
            }

            _newlyClaimedChapterReward = reward;
            _campaignProfileStatus = $"REWARD ACTIVE   {reward.displayName.ToUpperInvariant()}";
            if (!_campaignDeploymentConfirmed && !_gameOver && _wave == 0 && _builtTowerCount == 0)
            {
                ResetMissionRuntimeRules();
                ApplyMissionRuntimeRules(_campaignRoute?.level);
            }

            SetStatus($"Chapter reward secured: {reward.displayName}.");
            PushTacticalEvent($"Chapter reward: {reward.displayName}", 5.8f);
            _missionBoardNeedsRefresh = true;
            RefreshMissionBoardUi();
            PlaySfxTone("ui_chapter_reward", 760f, 0.22f, 0.72f, true);
        }

        private void OpenCampaignProfile()
        {
            _formationPanelOpen = false;
            _campaignProfileOpen = true;
            _campaignProfileImportArmed = false;
            _campaignProfileResetArmed = false;
            _campaignProfilePendingImport = string.Empty;
            _campaignProfileStatus = "PROFILE READY";
            _missionBoardNeedsRefresh = true;
            StartCoroutine(SelectUiNextFrame(_uiCampaignProfileSlotButtons.FirstOrDefault()));
            TDUiAnimator.PanelOpen(this, _uiCampaignProfileRoot);
            PlaySfxTone("ui_panel_open", 540f, 0.10f, 0.52f, true);
        }

        private void CloseCampaignProfile()
        {
            _campaignProfileOpen = false;
            _campaignProfileImportArmed = false;
            _campaignProfileResetArmed = false;
            _campaignProfilePendingImport = string.Empty;
            _missionBoardNeedsRefresh = true;
            StartCoroutine(SelectUiNextFrame(GetMissionLevelButton(_missionBoardSelectedLevel)));
            TDUiAnimator.PanelClose(this, _uiCampaignProfileRoot);
            PlaySfxTone("ui_panel_close", 420f, 0.08f, 0.48f, false);
        }

        private void SwitchCampaignSaveSlot(int slotId)
        {
            var totalLevels = _campaign?.totalLevels ?? 1;
            if (slotId == TDCampaignProgression.ActiveSaveSlot)
            {
                _campaignProfileStatus = $"SLOT {slotId} ALREADY ACTIVE";
                RefreshCampaignProfileUi();
                return;
            }

            if (!TDCampaignProgression.SetActiveSaveSlot(slotId, totalLevels, out var error))
            {
                _campaignProfileStatus = $"SLOT SWITCH REJECTED   {error.ToUpperInvariant()}";
                RefreshCampaignProfileUi();
                return;
            }

            var frontier = TDCampaignProgression.GetHighestUnlockedLevel(totalLevels);
            TDCampaignRouter.SaveLevelIndex(Mathf.Clamp(_missionBoardSelectedLevel, 1, frontier));
            RestartCurrentScene();
        }

        private void CopyCampaignCloudToClipboard()
        {
            var cloudCode = TDCampaignProgression.ExportCloudEnvelope(_campaign?.totalLevels ?? 1);
            _campaignClipboardBuffer = cloudCode;
            GUIUtility.systemCopyBuffer = cloudCode;
            if (TDCampaignProgression.TryPreviewCloudEnvelope(cloudCode, _campaign?.totalLevels ?? 1, out var preview, out _))
            {
                _campaignProfileStatus = $"CLOUD SNAPSHOT COPIED   ID {preview.fingerprint}   REV {preview.revision}";
            }
            else
            {
                _campaignProfileStatus = "CLOUD SNAPSHOT FAILED";
            }

            RefreshCampaignProfileUi();
        }

        private void MergeCampaignCloudFromClipboard()
        {
            var totalLevels = _campaign?.totalLevels ?? 1;
            var cloudCode = GetCampaignClipboardText();
            if (!TDCampaignProgression.TryPreviewCloudEnvelope(cloudCode, totalLevels, out var candidate, out var error) ||
                !ArePortableRewardIdsKnown(candidate?.claimedRewardIds) ||
                !ArePortableMetaIdsKnown(candidate?.claimedMetaRewardIds, candidate?.unlockedProtocolIds) ||
                !TDCampaignProgression.TryResolveCloudEnvelope(
                    cloudCode,
                    totalLevels,
                    TDCampaignCloudConflictResolution.Merge,
                    out var merged,
                    out error))
            {
                _campaignProfileStatus = string.IsNullOrWhiteSpace(error)
                    ? "CLOUD MERGE REJECTED   UNKNOWN REWARD"
                    : $"CLOUD MERGE REJECTED   {error.ToUpperInvariant()}";
                RefreshCampaignProfileUi();
                return;
            }

            var frontier = merged.progress.highestUnlockedLevel;
            TDCampaignRouter.SaveLevelIndex(Mathf.Clamp(_missionBoardSelectedLevel, 1, frontier));
            RestartCurrentScene();
        }

        private void CopyCampaignSaveToClipboard()
        {
            var portableSave = TDCampaignProgression.ExportPortableSave(_campaign?.totalLevels ?? 1);
            _campaignClipboardBuffer = portableSave;
            GUIUtility.systemCopyBuffer = portableSave;
            _campaignProfileImportArmed = false;
            _campaignProfileResetArmed = false;
            _campaignProfilePendingImport = string.Empty;
            if (TDCampaignProgression.TryPreviewPortableSave(portableSave, _campaign?.totalLevels ?? 1, out var preview, out _))
            {
                _campaignProfileStatus = $"SAVE COPIED   ID {preview.fingerprint}   {preview.codeLength} CHARACTERS";
            }
            else
            {
                _campaignProfileStatus = "SAVE COPY FAILED";
            }

            RefreshCampaignProfileUi();
        }

        private void ImportCampaignSaveFromClipboard()
        {
            var totalLevels = _campaign?.totalLevels ?? 1;
            var clipboard = GetCampaignClipboardText();
            if (_campaignProfileImportArmed && string.Equals(clipboard, _campaignProfilePendingImport, StringComparison.Ordinal))
            {
                if (!TDCampaignProgression.TryPreviewPortableSave(clipboard, totalLevels, out var confirmedPreview, out var importError) ||
                    !ArePortableRewardIdsKnown(confirmedPreview?.claimedRewardIds) ||
                    !ArePortableMetaIdsKnown(confirmedPreview?.claimedMetaRewardIds, confirmedPreview?.unlockedProtocolIds) ||
                    !TDCampaignProgression.TryImportPortableSave(clipboard, totalLevels, out var imported, out importError))
                {
                    _campaignProfileImportArmed = false;
                    _campaignProfilePendingImport = string.Empty;
                    _campaignProfileStatus = string.IsNullOrWhiteSpace(importError) ? "IMPORT REJECTED   UNKNOWN REWARD" : $"IMPORT REJECTED   {importError.ToUpperInvariant()}";
                    RefreshCampaignProfileUi();
                    return;
                }

                var safeLevel = Mathf.Clamp(_missionBoardSelectedLevel, 1, imported.progress.highestUnlockedLevel);
                TDCampaignRouter.SaveLevelIndex(safeLevel);
                RestartCurrentScene();
                return;
            }

            _campaignProfileImportArmed = false;
            _campaignProfilePendingImport = string.Empty;
            _campaignProfileResetArmed = false;
            if (!TDCampaignProgression.TryPreviewPortableSave(clipboard, totalLevels, out var preview, out var error) ||
                !ArePortableRewardIdsKnown(preview?.claimedRewardIds) ||
                !ArePortableMetaIdsKnown(preview?.claimedMetaRewardIds, preview?.unlockedProtocolIds))
            {
                _campaignProfileStatus = string.IsNullOrWhiteSpace(error) ? "IMPORT REJECTED   UNKNOWN REWARD" : $"IMPORT REJECTED   {error.ToUpperInvariant()}";
                RefreshCampaignProfileUi();
                return;
            }

            _campaignProfileImportArmed = true;
            _campaignProfilePendingImport = clipboard;
            _campaignProfileStatus =
                $"IMPORT READY   ID {preview.fingerprint}   CLEAR {preview.progress.clearedLevels}/{preview.progress.totalLevels}   REWARDS {preview.claimedChapterRewards}";
            RefreshCampaignProfileUi();
        }

        private string GetCampaignClipboardText()
        {
            var systemClipboard = (GUIUtility.systemCopyBuffer ?? string.Empty).Trim();
            var bufferedClipboard = (_campaignClipboardBuffer ?? string.Empty).Trim();
            var systemRecognized = systemClipboard.StartsWith(TDCampaignProgression.PortableSavePrefix, StringComparison.Ordinal) ||
                                   systemClipboard.StartsWith(TDCampaignProgression.CloudSavePrefix, StringComparison.Ordinal);
            var bufferRecognized = bufferedClipboard.StartsWith(TDCampaignProgression.PortableSavePrefix, StringComparison.Ordinal) ||
                                   bufferedClipboard.StartsWith(TDCampaignProgression.CloudSavePrefix, StringComparison.Ordinal);
            if (systemRecognized || !bufferRecognized)
            {
                return systemClipboard;
            }

            return bufferedClipboard;
        }

        private void ResetCampaignProfileFromUi()
        {
            if (!_campaignProfileResetArmed)
            {
                _campaignProfileResetArmed = true;
                _campaignProfileImportArmed = false;
                _campaignProfilePendingImport = string.Empty;
                _campaignProfileStatus = "RESET ARMED   CAMPAIGN PROGRESS AND FORMATIONS";
                RefreshCampaignProfileUi();
                return;
            }

            TDCampaignProgression.ResetProgress(_campaign?.totalLevels ?? 1);
            TDCampaignRouter.SaveLevelIndex(1);
            RestartCurrentScene();
        }

        private bool ArePortableRewardIdsKnown(IEnumerable<string> rewardIds)
        {
            if (rewardIds == null)
            {
                return true;
            }

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            for (var i = 0; i < chapters.Length; i++)
            {
                var rewardId = chapters[i]?.reward?.rewardId;
                if (!string.IsNullOrWhiteSpace(rewardId))
                {
                    known.Add(rewardId);
                }
            }

            foreach (var rewardId in rewardIds)
            {
                if (string.IsNullOrWhiteSpace(rewardId) || !known.Contains(rewardId))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ArePortableMetaIdsKnown(IEnumerable<string> rewardIds, IEnumerable<string> protocolIds)
        {
            var knownRewards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var knownProtocols = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "baseline" };
            var meta = _campaign?.metaProgression;
            foreach (var reward in (meta?.ratingRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>())
                         .Concat(meta?.codexRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>()))
            {
                if (reward != null && !string.IsNullOrWhiteSpace(reward.rewardId))
                {
                    knownRewards.Add(reward.rewardId);
                }
            }

            foreach (var protocol in meta?.tacticalProtocols ?? Array.Empty<TDCampaignTacticalProtocolDefinition>())
            {
                if (protocol != null && !string.IsNullOrWhiteSpace(protocol.protocolId))
                {
                    knownProtocols.Add(protocol.protocolId);
                }
            }

            return (rewardIds ?? Array.Empty<string>()).All(knownRewards.Contains) &&
                   (protocolIds ?? Array.Empty<string>()).All(knownProtocols.Contains);
        }

        private void RefreshCampaignProfileUi()
        {
            if (_uiCampaignProfileRoot == null || _campaign == null)
            {
                return;
            }

            var summary = GetCampaignProgressSummary();
            var masteredChapters = GetMasteredChapterCount();
            var claimedRewards = TDCampaignProgression.GetClaimedChapterRewardIds().Length;
            SetUiText(
                _uiCampaignProfileSummaryText,
                $"CLEAR {summary.clearedLevels}/{summary.totalLevels}   STARS {summary.earnedStars}/{summary.availableStars}   CONTRACTS {summary.completedContracts}/{summary.availableContracts}   MASTERED CHAPTERS {masteredChapters}/{_campaign.chapters.Length}   RANK {BuildCampaignRank(summary, masteredChapters)}\n" +
                $"CHALLENGE RECORD V {summary.veteranClears}/{summary.totalLevels}   E {summary.emberTrialClears}/{summary.totalLevels}   DOSSIERS ENEMY {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   TOWER {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}");
            SetUiText(_uiCampaignProfileChapterText, BuildCampaignChapterArchiveLabel());
            SetUiText(_uiCampaignProfileBonusText, BuildCampaignRewardBonusLabel());

            var portableSave = TDCampaignProgression.ExportPortableSave(_campaign.totalLevels);
            TDCampaignProgression.TryPreviewPortableSave(portableSave, _campaign.totalLevels, out var preview, out _);
            var slots = TDCampaignProgression.GetSaveSlotSummaries(_campaign.totalLevels);
            for (var i = 0; i < _uiCampaignProfileSlotButtons.Count && i < slots.Length; i++)
            {
                var slot = slots[i];
                var active = slot.slotId == TDCampaignProgression.ActiveSaveSlot;
                SetUiText(
                    _uiCampaignProfileSlotButtonTexts[i],
                    active
                        ? $"SLOT {slot.slotId}  ACTIVE"
                        : $"SLOT {slot.slotId}  L{slot.progress.highestUnlockedLevel:00}");
                var image = _uiCampaignProfileSlotButtons[i]?.GetComponent<Image>();
                if (image != null)
                {
                    image.color = active
                        ? new Color(0.20f, 0.52f, 0.38f, 0.96f)
                        : new Color(0.10f, 0.18f, 0.22f, 0.96f);
                }
            }

            var activeSlot = slots.FirstOrDefault(slot => slot.slotId == TDCampaignProgression.ActiveSaveSlot);
            SetUiText(
                _uiCampaignProfileSaveText,
                $"ACTIVE SLOT  {TDCampaignProgression.ActiveSaveSlot}   SAVE VERSION {TDCampaignProgression.SaveVersion}\n" +
                $"PROFILE ID  {preview?.fingerprint ?? "UNAVAILABLE"}\n" +
                $"REVISION  {activeSlot?.revision ?? 0}   FRONTIER L{summary.highestUnlockedLevel:00}\n" +
                $"RECORDS  {summary.totalLevels} missions / {claimedRewards} rewards\n" +
                $"PORTABLE  {preview?.codeLength ?? 0} characters   CLOUD READY");
            SetUiText(_uiCampaignProfileStatusText, _campaignProfileStatus);
            SetUiText(_uiCampaignProfileImportButtonText, _campaignProfileImportArmed ? "Confirm Import" : "Import");
            SetUiText(_uiCampaignProfileResetButtonText, _campaignProfileResetArmed ? "Confirm Reset" : "Reset Profile");
        }

        private void OpenFormationPanel()
        {
            if (_campaign == null || _campaignRoute?.level == null)
            {
                return;
            }

            var selectedLevel = Mathf.Clamp(_missionBoardSelectedLevel, 1, _campaign.totalLevels);
            if (!TDCampaignProgression.IsLevelUnlocked(selectedLevel, _campaign.totalLevels))
            {
                SetStatus($"Mission L{selectedLevel:00} is locked.");
                return;
            }

            _campaignProfileOpen = false;

            _formationDraftLevel = selectedLevel;
            _formationDraftDoctrine = TDCampaignProgression.GetResonanceDoctrine(selectedLevel);
            _formationDraftDifficulty = ResolveAvailableDifficulty(
                selectedLevel,
                TDCampaignProgression.GetDifficultyPreference(selectedLevel));
            _formationDraftProtocolId = ResolveAvailableProtocolId(
                TDCampaignProgression.GetTacticalProtocol(selectedLevel));
            if (!IsDoctrineAvailableForLevel(selectedLevel))
            {
                _formationDraftDoctrine = TDResonanceDoctrine.Adaptive;
            }
            _formationDraftTowerKinds.Clear();
            var available = GetTowerKindsUnlockedAtLevel(selectedLevel);
            var savedIds = TDCampaignProgression.GetTowerLoadout(selectedLevel);
            for (var i = 0; i < savedIds.Length && _formationDraftTowerKinds.Count < TDCampaignProgression.MaxFormationTowers; i++)
            {
                if (TDTower.TryParseTowerId(savedIds[i], out var kind) &&
                    available.Contains(kind) &&
                    !_formationDraftTowerKinds.Contains(kind))
                {
                    _formationDraftTowerKinds.Add(kind);
                }
            }

            if (_formationDraftTowerKinds.Count == 0)
            {
                BuildAutoFitFormation(selectedLevel, available, out var fittedTowers, out var fittedDoctrine);
                _formationDraftTowerKinds.AddRange(fittedTowers);
                _formationDraftDoctrine = fittedDoctrine;
            }

            _formationPanelOpen = true;
            _uiFormationRoot?.SetAsLastSibling();
            RefreshFormationPanelUi();
            StartCoroutine(SelectUiNextFrame(_uiFormationTowerButtons.FirstOrDefault(button => button != null && button.interactable)));
            TDUiAnimator.PanelOpen(this, _uiFormationRoot);
            PlaySfxTone("ui_panel_open", 540f, 0.10f, 0.52f, true);
        }

        private void CloseFormationPanel()
        {
            _formationPanelOpen = false;
            _missionBoardNeedsRefresh = true;
            if (_uiFormationRoot != null)
            {
                TDUiAnimator.PanelClose(this, _uiFormationRoot, () => _uiFormationRoot.gameObject.SetActive(false));
            }
            StartCoroutine(SelectUiNextFrame(GetMissionLevelButton(_missionBoardSelectedLevel)));
            PlaySfxTone("ui_panel_close", 420f, 0.08f, 0.48f, false);
        }

        private void ToggleFormationTower(TDTowerKind kind)
        {
            if (!IsFormationDraftEditable())
            {
                return;
            }

            var available = GetTowerKindsUnlockedAtLevel(_formationDraftLevel);
            if (!available.Contains(kind))
            {
                return;
            }

            if (_formationDraftTowerKinds.Contains(kind))
            {
                _formationDraftTowerKinds.Remove(kind);
            }
            else if (_formationDraftTowerKinds.Count < TDCampaignProgression.MaxFormationTowers)
            {
                _formationDraftTowerKinds.Add(kind);
            }
            else
            {
                SetStatus("Formation is full. Remove one tower before adding another.");
            }

            RefreshFormationPanelUi();
        }

        private void SelectFormationDoctrine(TDResonanceDoctrine doctrine)
        {
            if (!IsFormationDraftEditable() || !IsDoctrineAvailableForLevel(_formationDraftLevel))
            {
                return;
            }

            _formationDraftDoctrine = doctrine;
            RefreshFormationPanelUi();
        }

        private void SelectFormationDifficulty(TDCampaignDifficultyTier difficulty)
        {
            if (!IsFormationDraftEditable())
            {
                return;
            }

            if (!IsDifficultyAvailableForLevel(_formationDraftLevel, difficulty))
            {
                SetStatus(GetDifficultyUnlockLabel(_formationDraftLevel, difficulty));
                return;
            }

            _formationDraftDifficulty = difficulty;
            RefreshFormationPanelUi();
        }

        private void SelectFormationProtocol(string protocolId)
        {
            if (!IsFormationDraftEditable())
            {
                return;
            }

            var resolved = ResolveAvailableProtocolId(protocolId);
            if (!string.Equals(resolved, protocolId, StringComparison.OrdinalIgnoreCase))
            {
                var protocol = GetTacticalProtocol(protocolId);
                SetStatus(protocol?.unlockHint ?? "This tactical protocol is locked.");
                return;
            }

            _formationDraftProtocolId = resolved;
            RefreshFormationPanelUi();
        }

        private void AutoFitFormationDraft()
        {
            if (!IsFormationDraftEditable())
            {
                return;
            }

            BuildAutoFitFormation(
                _formationDraftLevel,
                GetTowerKindsUnlockedAtLevel(_formationDraftLevel),
                out var towers,
                out var doctrine);
            _formationDraftTowerKinds.Clear();
            _formationDraftTowerKinds.AddRange(towers);
            _formationDraftDoctrine = doctrine;
            RefreshFormationPanelUi();
        }

        private void ConfirmFormationAndDeploy()
        {
            if (_formationDraftTowerKinds.Count == 0)
            {
                SetStatus("Select at least one tower before deployment.");
                return;
            }

            if (IsFormationDraftEditable())
            {
                var towerIds = new List<string>(_formationDraftTowerKinds.Count);
                for (var i = 0; i < _formationDraftTowerKinds.Count; i++)
                {
                    towerIds.Add(TDTower.GetTowerId(_formationDraftTowerKinds[i]));
                }

                TDCampaignProgression.SaveFormation(_formationDraftLevel, towerIds, _formationDraftDoctrine);
                TDCampaignProgression.SaveDifficultyPreference(_formationDraftLevel, _formationDraftDifficulty);
                TDCampaignProgression.SaveTacticalProtocol(_formationDraftLevel, _formationDraftProtocolId);
            }

            if (_campaignRoute?.level != null && _formationDraftLevel == _campaignRoute.level.levelIndex)
            {
                if (IsFormationDraftEditable())
                {
                    _activeCampaignDifficulty = ResolveAvailableDifficulty(_formationDraftLevel, _formationDraftDifficulty);
                    _activeTacticalProtocol = GetTacticalProtocol(ResolveAvailableProtocolId(_formationDraftProtocolId));
                    if (!_gameOver && !_campaignDeploymentConfirmed && _wave == 0 && _builtTowerCount == 0)
                    {
                        ResetMissionRuntimeRules();
                        ApplyMissionRuntimeRules(_campaignRoute.level);
                    }
                }

                RefreshUnlockedTowerKinds();
                RebuildTowerBuildButtons();
            }

            _formationPanelOpen = false;
            DeploySelectedMission();
        }

        private bool IsFormationDraftEditable()
        {
            if (_campaignRoute?.level == null || _formationDraftLevel != _campaignRoute.level.levelIndex)
            {
                return true;
            }

            return _gameOver || !_campaignDeploymentConfirmed || (_isInPrepPhase && _builtTowerCount == 0 && _wavesCleared == 0);
        }

        private void RefreshFormationPanelUi()
        {
            if (_uiFormationRoot == null || !_formationPanelOpen)
            {
                return;
            }

            var level = GetCampaignLevel(_formationDraftLevel) ?? _campaignRoute?.level;
            if (level == null)
            {
                return;
            }

            BuildMissionWaveIntel(level, out var waveCount, out var laneCount, out _, out var threatTags, out var loadError);
            var threatLabel = BuildMissionDisplayThreatLabel(threatTags);
            var report = CalculateFormationFit(level, _formationDraftTowerKinds, _formationDraftDoctrine, threatTags);
            var roster = new List<string>();
            for (var i = 0; i < _formationDraftTowerKinds.Count; i++)
            {
                roster.Add($"{i + 1}:{GetCompactTowerLabel(_formationDraftTowerKinds[i])}");
            }

            SetUiText(_uiFormationTitleText, $"PREBATTLE FORMATION  /  L{level.levelIndex:00}");
            SetUiText(
                _uiFormationThreatText,
                $"THREAT  {threatLabel}\nSCOPE  {waveCount} waves / {laneCount} {(laneCount == 1 ? "route" : "routes")}{(string.IsNullOrWhiteSpace(loadError) ? string.Empty : $" / INTEL ERROR {loadError}")}");
            SetUiText(
                _uiFormationRosterText,
                $"TOWER ROSTER  {_formationDraftTowerKinds.Count}/{TDCampaignProgression.MaxFormationTowers}\n{(roster.Count == 0 ? "No tower selected" : string.Join("  /  ", roster))}");

            var available = GetTowerKindsUnlockedAtLevel(_formationDraftLevel);
            var buildOrder = TDTower.GetBuildOrder();
            var editable = IsFormationDraftEditable();
            for (var i = 0; i < _uiFormationTowerButtons.Count && i < buildOrder.Count; i++)
            {
                var kind = buildOrder[i];
                var button = _uiFormationTowerButtons[i];
                var label = i < _uiFormationTowerButtonTexts.Count ? _uiFormationTowerButtonTexts[i] : null;
                var unlocked = available.Contains(kind);
                var slot = _formationDraftTowerKinds.IndexOf(kind);
                var identity = TDUiVisualIdentity.GetTower(kind);
                button.interactable = editable && unlocked;
                SetUiText(
                    label,
                    !unlocked
                        ? $"LOCKED\n{GetCompactTowerLabel(kind)}"
                        : slot >= 0
                            ? $"SLOT {slot + 1}\n{GetCompactTowerLabel(kind)}\n{GetFormationTowerRole(kind)}"
                            : $"ADD\n{GetCompactTowerLabel(kind)}\n{GetFormationTowerRole(kind)}");
                if (i < _uiFormationTowerIcons.Count && _uiFormationTowerIcons[i] != null)
                {
                    _uiFormationTowerIcons[i].color = unlocked
                        ? Color.white
                        : new Color(0.46f, 0.50f, 0.52f, 0.56f);
                }

                if (i < _uiFormationTowerAccents.Count && _uiFormationTowerAccents[i] != null)
                {
                    var accent = identity.accent;
                    accent.a = unlocked ? 1f : 0.32f;
                    _uiFormationTowerAccents[i].color = accent;
                }

                if (i < _uiFormationTowerOutlines.Count && _uiFormationTowerOutlines[i] != null)
                {
                    _uiFormationTowerOutlines[i].enabled = slot >= 0;
                    _uiFormationTowerOutlines[i].effectColor = identity.accent;
                }

                if (button.targetGraphic is Image image)
                {
                    image.color = !unlocked
                        ? new Color(0.09f, 0.10f, 0.11f, 0.70f)
                        : slot >= 0
                            ? Color.Lerp(new Color(0.10f, 0.15f, 0.18f, 0.98f), identity.accent, 0.30f)
                            : new Color(0.13f, 0.21f, 0.25f, 0.96f);
                }
            }

            var doctrines = new[]
            {
                TDResonanceDoctrine.Adaptive,
                TDResonanceDoctrine.EmberSurge,
                TDResonanceDoctrine.FractureMark
            };
            var doctrineAvailable = IsDoctrineAvailableForLevel(_formationDraftLevel);
            for (var i = 0; i < _uiFormationDoctrineButtons.Count && i < doctrines.Length; i++)
            {
                var doctrine = doctrines[i];
                var selected = doctrine == _formationDraftDoctrine;
                var button = _uiFormationDoctrineButtons[i];
                button.interactable = editable && doctrineAvailable;
                SetUiText(
                    _uiFormationDoctrineButtonTexts[i],
                    doctrineAvailable
                        ? GetDoctrineButtonLabel(doctrine)
                        : $"LOCKED L{_resonanceEnabledFromLevel:00}\n{GetDoctrineShortLabel(doctrine)}");
                if (button.targetGraphic is Image image)
                {
                    image.color = !doctrineAvailable
                        ? new Color(0.09f, 0.10f, 0.11f, 0.74f)
                        : selected
                        ? GetDoctrineColor(doctrine, 0.98f)
                        : new Color(0.13f, 0.20f, 0.23f, 0.96f);
                }
            }

            var difficultyTiers = new[]
            {
                TDCampaignDifficultyTier.Standard,
                TDCampaignDifficultyTier.Veteran,
                TDCampaignDifficultyTier.EmberTrial
            };
            for (var i = 0; i < _uiFormationDifficultyButtons.Count && i < difficultyTiers.Length; i++)
            {
                var difficulty = difficultyTiers[i];
                var availableDifficulty = IsDifficultyAvailableForLevel(_formationDraftLevel, difficulty);
                var selectedDifficulty = difficulty == _formationDraftDifficulty;
                var button = _uiFormationDifficultyButtons[i];
                button.interactable = editable && availableDifficulty;
                SetUiText(
                    _uiFormationDifficultyButtonTexts[i],
                    availableDifficulty
                        ? GetDifficultyShortLabel(difficulty)
                        : $"LOCKED\n{GetDifficultyShortLabel(difficulty)}");
                if (button.targetGraphic is Image image)
                {
                    image.color = !availableDifficulty
                        ? new Color(0.09f, 0.10f, 0.11f, 0.74f)
                        : selectedDifficulty
                            ? GetDifficultyColor(difficulty, 0.98f)
                            : new Color(0.13f, 0.20f, 0.23f, 0.96f);
                }
            }

            var protocols = _campaign?.metaProgression?.tacticalProtocols ?? Array.Empty<TDCampaignTacticalProtocolDefinition>();
            for (var i = 0; i < _uiFormationProtocolButtons.Count && i < protocols.Length; i++)
            {
                var protocol = protocols[i];
                var unlocked = string.Equals(protocol.protocolId, "baseline", StringComparison.OrdinalIgnoreCase) ||
                               TDCampaignProgression.IsProtocolUnlocked(protocol.protocolId);
                var selected = string.Equals(protocol.protocolId, _formationDraftProtocolId, StringComparison.OrdinalIgnoreCase);
                var button = _uiFormationProtocolButtons[i];
                button.interactable = editable && unlocked;
                SetUiText(
                    _uiFormationProtocolButtonTexts[i],
                    unlocked
                        ? $"{(selected ? "ACTIVE" : "PROTOCOL")}\n{GetCompactProtocolLabel(protocol)}"
                        : $"LOCKED\n{GetCompactProtocolLabel(protocol)}");
                if (button.targetGraphic is Image image)
                {
                    image.color = !unlocked
                        ? new Color(0.09f, 0.10f, 0.11f, 0.74f)
                        : selected
                            ? new Color(0.52f, 0.34f, 0.16f, 0.98f)
                            : new Color(0.13f, 0.20f, 0.23f, 0.96f);
                }
            }

            SetUiText(_uiFormationFitTitleText, $"COUNTER FIT  {report.total}/100  {report.grade}");
            SetUiText(
                _uiFormationFitBodyText,
                $"COVERAGE  {report.coverage}    MATRIX  {report.matrix}    {(doctrineAvailable ? $"DOCTRINE  {report.doctrine}" : $"DOCTRINE  LOCKED L{_resonanceEnabledFromLevel:00}")}\n" +
                $"COVERED  {report.coveredCategories}\nGAPS  {report.gapCategories}");
            SetUiText(
                _uiFormationMatrixText,
                $"SPECIALIZATION FIT\n{report.matrixPicks}\n\nDOCTRINE EFFECT\n{(doctrineAvailable ? GetDoctrineEffectLabel(_formationDraftDoctrine) : $"Offline until campaign L{_resonanceEnabledFromLevel:00}.")}\n{report.doctrineAdvice}");
            SetUiText(
                _uiFormationLockText,
                editable
                    ? doctrineAvailable
                        ? "FORMATION READY  Loadout and doctrine will persist for this mission."
                        : $"FORMATION READY  Four-tower loadout active; doctrine unlocks at L{_resonanceEnabledFromLevel:00}."
                    : "FORMATION LOCKED  The current run has already committed its first build.");
            SetUiText(
                _uiFormationDifficultyText,
                $"{BuildDifficultyPreviewLabel(level, _formationDraftDifficulty, report.total)}\n" +
                $"PROTOCOL  {BuildProtocolPreviewLabel(GetTacticalProtocol(_formationDraftProtocolId))}");

            if (_uiFormationAutoFitButton != null)
            {
                _uiFormationAutoFitButton.interactable = editable;
            }

            if (_uiFormationDeployButton != null)
            {
                _uiFormationDeployButton.interactable = _formationDraftTowerKinds.Count > 0;
                SetUiText(
                    _uiFormationDeployButtonText,
                    !editable && _formationDraftLevel == _campaignRoute.level.levelIndex
                        ? "Return to Battle"
                        : _formationDraftLevel == _campaignRoute.level.levelIndex && _gameOver
                            ? "Save & Replay"
                            : "Save & Deploy");
            }
        }

        private void BuildAutoFitFormation(
            int levelIndex,
            List<TDTowerKind> available,
            out List<TDTowerKind> fittedTowers,
            out TDResonanceDoctrine fittedDoctrine)
        {
            fittedTowers = new List<TDTowerKind>();
            fittedDoctrine = TDResonanceDoctrine.Adaptive;
            if (available == null || available.Count == 0)
            {
                fittedTowers.Add(TDTowerKind.RailLancer);
                return;
            }

            var level = GetCampaignLevel(levelIndex) ?? _campaignRoute?.level;
            BuildMissionWaveIntel(level, out _, out _, out _, out var threatTags, out _);
            var targetCount = Mathf.Min(TDCampaignProgression.MaxFormationTowers, available.Count);
            var doctrines = new[]
            {
                TDResonanceDoctrine.Adaptive,
                TDResonanceDoctrine.EmberSurge,
                TDResonanceDoctrine.FractureMark
            };
            var doctrineCount = IsDoctrineAvailableForLevel(levelIndex) ? doctrines.Length : 1;
            var bestScore = int.MinValue;
            var bestCoverage = int.MinValue;
            var combinations = 1 << available.Count;
            for (var mask = 1; mask < combinations; mask++)
            {
                if (CountFormationBits(mask) != targetCount)
                {
                    continue;
                }

                var candidate = new List<TDTowerKind>(targetCount);
                for (var index = 0; index < available.Count; index++)
                {
                    if ((mask & (1 << index)) != 0)
                    {
                        candidate.Add(available[index]);
                    }
                }

                for (var doctrineIndex = 0; doctrineIndex < doctrineCount; doctrineIndex++)
                {
                    var doctrine = doctrines[doctrineIndex];
                    var report = CalculateFormationFit(level, candidate, doctrine, threatTags);
                    if (report.total < bestScore ||
                        (report.total == bestScore && report.coverage <= bestCoverage))
                    {
                        continue;
                    }

                    bestScore = report.total;
                    bestCoverage = report.coverage;
                    fittedTowers = new List<TDTowerKind>(candidate);
                    fittedDoctrine = doctrine;
                }
            }

            if (fittedTowers.Count == 0)
            {
                for (var i = 0; i < targetCount; i++)
                {
                    fittedTowers.Add(available[i]);
                }
            }
        }

        private TDFormationFitReport CalculateFormationFit(
            TDCampaignLevelDefinition level,
            IReadOnlyList<TDTowerKind> formation,
            TDResonanceDoctrine doctrine,
            HashSet<string> threatTags = null)
        {
            if (threatTags == null)
            {
                BuildMissionWaveIntel(level, out _, out _, out _, out threatTags, out _);
            }

            threatTags ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeCategories = GetFormationThreatCategories(threatTags);
            var coveredCategories = new List<string>();
            var gapCategories = new List<string>();
            for (var i = 0; i < activeCategories.Count; i++)
            {
                var category = activeCategories[i];
                var covered = false;
                if (formation != null)
                {
                    for (var towerIndex = 0; towerIndex < formation.Count; towerIndex++)
                    {
                        if (IsTowerCounterForCategory(formation[towerIndex], category))
                        {
                            covered = true;
                            break;
                        }
                    }
                }

                (covered ? coveredCategories : gapCategories).Add(GetFormationCategoryLabel(category));
            }

            var coverageScore = activeCategories.Count == 0
                ? 75
                : Mathf.RoundToInt(100f * coveredCategories.Count / activeCategories.Count);
            var available = GetTowerKindsUnlockedAtLevel(level?.levelIndex ?? DefaultCampaignLevelIndex);
            var relevantMatrixTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selectedMatrixTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matchingDefinitions = new List<TDTowerSpecializationDefinition>();
            var allDefinitions = TDTower.GetSpecializationDefinitions();
            for (var i = 0; i < allDefinitions.Count; i++)
            {
                var definition = allDefinitions[i];
                var selected = formation != null && ContainsTowerKind(formation, definition.towerKind);
                var definitionMatches = false;
                if (definition.counterTags != null)
                {
                    for (var tagIndex = 0; tagIndex < definition.counterTags.Length; tagIndex++)
                    {
                        var tag = definition.counterTags[tagIndex];
                        if (!threatTags.Contains(tag))
                        {
                            continue;
                        }

                        if (available.Contains(definition.towerKind))
                        {
                            relevantMatrixTags.Add(tag);
                        }

                        if (selected)
                        {
                            selectedMatrixTags.Add(tag);
                            definitionMatches = true;
                        }
                    }
                }

                if (definitionMatches)
                {
                    matchingDefinitions.Add(definition);
                }
            }

            var matrixScore = relevantMatrixTags.Count == 0
                ? 70
                : Mathf.RoundToInt(100f * selectedMatrixTags.Count / relevantMatrixTags.Count);
            var emberFit = HasAnyCampaignThreatPattern(threatTags, EmberSurgeThreatPatterns);
            var fractureFit = HasAnyCampaignThreatPattern(threatTags, FractureMarkThreatPatterns);
            var doctrineAvailable = IsDoctrineAvailableForLevel(level?.levelIndex ?? DefaultCampaignLevelIndex);
            var threatDoctrineScore = doctrineAvailable
                ? CalculateDoctrineThreatScore(doctrine, emberFit, fractureFit)
                : 0;
            var affinityMatches = 0;
            for (var i = 0; i < matchingDefinitions.Count; i++)
            {
                if (IsDoctrineCompatibleWithAffinity(doctrine, matchingDefinitions[i].resonanceAffinity))
                {
                    affinityMatches++;
                }
            }

            var affinityScore = matchingDefinitions.Count == 0
                ? 65
                : Mathf.RoundToInt(100f * affinityMatches / matchingDefinitions.Count);
            var doctrineScore = doctrineAvailable
                ? Mathf.RoundToInt((threatDoctrineScore * 0.65f) + (affinityScore * 0.35f))
                : 0;
            var total = Mathf.Clamp(
                doctrineAvailable
                    ? Mathf.RoundToInt((coverageScore * 0.50f) + (matrixScore * 0.30f) + (doctrineScore * 0.20f))
                    : Mathf.RoundToInt((coverageScore * 0.625f) + (matrixScore * 0.375f)),
                0,
                100);
            return new TDFormationFitReport
            {
                total = total,
                coverage = coverageScore,
                matrix = matrixScore,
                doctrine = doctrineScore,
                grade = total >= 90 ? "S" : total >= 80 ? "A" : total >= 70 ? "B" : total >= 60 ? "C" : "D",
                coveredCategories = coveredCategories.Count == 0 ? "None" : string.Join(" / ", coveredCategories),
                gapCategories = gapCategories.Count == 0 ? "None" : string.Join(" / ", gapCategories),
                matrixPicks = BuildFormationMatrixPicks(matchingDefinitions, threatTags, 3),
                doctrineAdvice = doctrineAvailable
                    ? BuildDoctrineAdvice(doctrine, emberFit, fractureFit)
                    : $"Doctrine unlocks with resonance at L{_resonanceEnabledFromLevel:00}."
            };
        }

        private bool IsDoctrineAvailableForLevel(int levelIndex)
        {
            return levelIndex >= Mathf.Max(1, _resonanceEnabledFromLevel);
        }

        private static List<string> GetFormationThreatCategories(HashSet<string> threatTags)
        {
            var categories = new List<string>(4);
            if (HasAnyCampaignTag(threatTags, "fast", "flank", "anti_fast", "gap", "pressure"))
            {
                categories.Add("speed");
            }

            if (HasAnyCampaignTag(threatTags, "swarm", "split", "spawn", "mixed"))
            {
                categories.Add("swarm");
            }

            if (HasAnyCampaignTag(threatTags, "armored", "heavy", "boss", "durability"))
            {
                categories.Add("armor");
            }

            if (HasAnyCampaignTag(threatTags, "support", "attrition", "special", "zone_control"))
            {
                categories.Add("attrition");
            }

            return categories;
        }

        private static string BuildFormationMatrixPicks(
            List<TDTowerSpecializationDefinition> definitions,
            HashSet<string> threatTags,
            int maxResults)
        {
            if (definitions == null || definitions.Count == 0)
            {
                return "No P7 specialization trait match in this roster.";
            }

            definitions.Sort((a, b) =>
            {
                var delta = CountSpecializationTagMatches(b, threatTags).CompareTo(CountSpecializationTagMatches(a, threatTags));
                return delta != 0 ? delta : string.CompareOrdinal(a.displayName, b.displayName);
            });
            var labels = new List<string>();
            for (var i = 0; i < definitions.Count && labels.Count < Mathf.Max(1, maxResults); i++)
            {
                var definition = definitions[i];
                var matchedTags = new List<string>(2);
                for (var tagIndex = 0; tagIndex < definition.counterTags.Length && matchedTags.Count < 2; tagIndex++)
                {
                    if (threatTags.Contains(definition.counterTags[tagIndex]))
                    {
                        matchedTags.Add(definition.counterTags[tagIndex]);
                    }
                }

                labels.Add($"{definition.displayName}  {string.Join("/", matchedTags)}  [{TDTower.GetResonanceAffinityLabel(definition.resonanceAffinity)}]");
            }

            return string.Join("\n", labels);
        }

        private static int CalculateDoctrineThreatScore(TDResonanceDoctrine doctrine, bool emberFit, bool fractureFit)
        {
            return doctrine switch
            {
                TDResonanceDoctrine.Adaptive => emberFit && fractureFit ? 100 : emberFit || fractureFit ? 72 : 70,
                TDResonanceDoctrine.EmberSurge => emberFit ? fractureFit ? 78 : 100 : fractureFit ? 30 : 60,
                TDResonanceDoctrine.FractureMark => fractureFit ? emberFit ? 78 : 100 : emberFit ? 30 : 60,
                _ => 60
            };
        }

        private static bool IsDoctrineCompatibleWithAffinity(TDResonanceDoctrine doctrine, TDResonanceAffinity affinity)
        {
            return doctrine == TDResonanceDoctrine.Adaptive ||
                   affinity == TDResonanceAffinity.Either ||
                   doctrine == TDResonanceDoctrine.EmberSurge && affinity == TDResonanceAffinity.EmberSurge ||
                   doctrine == TDResonanceDoctrine.FractureMark && affinity == TDResonanceAffinity.FractureMark;
        }

        private static string BuildDoctrineAdvice(TDResonanceDoctrine doctrine, bool emberFit, bool fractureFit)
        {
            if (emberFit && fractureFit)
            {
                return doctrine == TDResonanceDoctrine.Adaptive
                    ? "Mixed threat package fully aligns with Adaptive doctrine."
                    : "Mixed package: the off-doctrine command remains unamplified.";
            }

            if (emberFit)
            {
                return doctrine == TDResonanceDoctrine.EmberSurge
                    ? "Durability pressure aligns with Ember specialization."
                    : "Ember doctrine scores higher against this durability package.";
            }

            if (fractureFit)
            {
                return doctrine == TDResonanceDoctrine.FractureMark
                    ? "Route pressure aligns with Fracture specialization."
                    : "Fracture doctrine scores higher against this route package.";
            }

            return "No dominant command pattern; prioritize tower coverage.";
        }

        private static bool ContainsTowerKind(IReadOnlyList<TDTowerKind> formation, TDTowerKind kind)
        {
            if (formation == null)
            {
                return false;
            }

            for (var i = 0; i < formation.Count; i++)
            {
                if (formation[i] == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountFormationBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        private static string GetFormationCategoryLabel(string category)
        {
            return category switch
            {
                "speed" => "Speed",
                "swarm" => "Swarm",
                "armor" => "Armor",
                "attrition" => "Attrition",
                _ => "Mixed"
            };
        }

        private static string GetFormationTowerRole(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => "Armor/Priority",
                TDTowerKind.CinderMortar => "Swarm/Area",
                TDTowerKind.FrostCoil => "Speed/Control",
                TDTowerKind.ArcWelder => "Swarm/Chain",
                TDTowerKind.SiegeDrill => "Armor/Heavy",
                TDTowerKind.EmberFlak => "Speed/Intercept",
                TDTowerKind.ResonanceBeacon => "Attrition/Support",
                TDTowerKind.GravSnare => "Control/Mixed",
                _ => "Mixed"
            };
        }

        private static string GetDoctrineButtonLabel(TDResonanceDoctrine doctrine)
        {
            return doctrine switch
            {
                TDResonanceDoctrine.Adaptive => "ADAPTIVE\nLive match +4%",
                TDResonanceDoctrine.EmberSurge => "EMBER\nSurge output +10%",
                TDResonanceDoctrine.FractureMark => "FRACTURE\nMarked exposure +10%",
                _ => "ADAPTIVE"
            };
        }

        private static string GetDoctrineEffectLabel(TDResonanceDoctrine doctrine)
        {
            return doctrine switch
            {
                TDResonanceDoctrine.Adaptive => "Threat-matched Ember or Fracture power +4%.",
                TDResonanceDoctrine.EmberSurge => "Ember Surge tower output +10%.",
                TDResonanceDoctrine.FractureMark => "Fracture Mark exposure damage +10%.",
                _ => "No doctrine effect."
            };
        }

        private static string GetDoctrineShortLabel(TDResonanceDoctrine doctrine)
        {
            return doctrine switch
            {
                TDResonanceDoctrine.EmberSurge => "EMBER",
                TDResonanceDoctrine.FractureMark => "FRACTURE",
                _ => "ADAPTIVE"
            };
        }

        private TDCampaignDifficultyDefinition GetDifficultyDefinition(TDCampaignDifficultyTier difficulty)
        {
            var definitions = _campaign?.difficultyTiers ?? Array.Empty<TDCampaignDifficultyDefinition>();
            for (var i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null && definitions[i].tier == (int)difficulty)
                {
                    return definitions[i];
                }
            }

            return null;
        }

        private static string GetDifficultyShortLabel(TDCampaignDifficultyTier difficulty)
        {
            return difficulty switch
            {
                TDCampaignDifficultyTier.Veteran => "VETERAN",
                TDCampaignDifficultyTier.EmberTrial => "EMBER TRIAL",
                _ => "STANDARD"
            };
        }

        private static Color GetDifficultyColor(TDCampaignDifficultyTier difficulty, float alpha)
        {
            return difficulty switch
            {
                TDCampaignDifficultyTier.Veteran => new Color(0.46f, 0.32f, 0.15f, alpha),
                TDCampaignDifficultyTier.EmberTrial => new Color(0.58f, 0.20f, 0.14f, alpha),
                _ => new Color(0.20f, 0.42f, 0.34f, alpha)
            };
        }

        private bool IsDifficultyAvailableForLevel(int levelIndex, TDCampaignDifficultyTier difficulty)
        {
            if (difficulty == TDCampaignDifficultyTier.Standard)
            {
                return true;
            }

            if (difficulty == TDCampaignDifficultyTier.EmberTrial)
            {
                var summary = GetCampaignProgressSummary();
                return summary.totalLevels > 0 && summary.clearedLevels == summary.totalLevels;
            }

            var level = GetCampaignLevel(levelIndex);
            var chapter = GetCampaignChapter(level?.chapterId);
            return chapter != null && TDCampaignProgression.BuildChapterSummary(chapter).cleared;
        }

        private TDCampaignDifficultyTier ResolveAvailableDifficulty(
            int levelIndex,
            TDCampaignDifficultyTier requested)
        {
            var safeRequested = (TDCampaignDifficultyTier)Mathf.Clamp(
                (int)requested,
                (int)TDCampaignDifficultyTier.Standard,
                (int)TDCampaignDifficultyTier.EmberTrial);
            if (IsDifficultyAvailableForLevel(levelIndex, safeRequested))
            {
                return safeRequested;
            }

            return IsDifficultyAvailableForLevel(levelIndex, TDCampaignDifficultyTier.Veteran)
                ? TDCampaignDifficultyTier.Veteran
                : TDCampaignDifficultyTier.Standard;
        }

        private string GetDifficultyUnlockLabel(int levelIndex, TDCampaignDifficultyTier difficulty)
        {
            if (difficulty == TDCampaignDifficultyTier.EmberTrial)
            {
                return "Ember Trial unlocks after all 20 campaign missions are cleared.";
            }

            var level = GetCampaignLevel(levelIndex);
            var chapter = GetCampaignChapter(level?.chapterId);
            return $"Veteran unlocks after clearing {chapter?.displayName ?? "this chapter"}.";
        }

        private string BuildDifficultyPreviewLabel(
            TDCampaignLevelDefinition level,
            TDCampaignDifficultyTier difficulty,
            int formationFit)
        {
            var definition = GetDifficultyDefinition(difficulty);
            var baseEffect = BuildCompactMutatorEffectLabel(definition?.modifiers);
            var remix = difficulty == TDCampaignDifficultyTier.Standard
                ? null
                : GetCampaignChapter(level?.chapterId)?.challengeRemix;
            var remixLabel = remix == null
                ? "REMIX  OFF"
                : $"REMIX  {remix.displayName.ToUpperInvariant()}  {BuildCompactMutatorEffectLabel(remix)}";
            return $"{GetDifficultyShortLabel(difficulty)}  ADAPT {formationFit}/100  {baseEffect}\n{remixLabel}";
        }

        private TDCampaignTacticalProtocolDefinition GetTacticalProtocol(string protocolId)
        {
            var protocols = _campaign?.metaProgression?.tacticalProtocols ?? Array.Empty<TDCampaignTacticalProtocolDefinition>();
            return protocols.FirstOrDefault(protocol =>
                       protocol != null && string.Equals(protocol.protocolId, protocolId, StringComparison.OrdinalIgnoreCase)) ??
                   protocols.FirstOrDefault(protocol =>
                       protocol != null && string.Equals(protocol.protocolId, "baseline", StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveAvailableProtocolId(string protocolId)
        {
            var protocol = GetTacticalProtocol(protocolId);
            if (protocol == null ||
                (!string.Equals(protocol.protocolId, "baseline", StringComparison.OrdinalIgnoreCase) &&
                 !TDCampaignProgression.IsProtocolUnlocked(protocol.protocolId)))
            {
                return "baseline";
            }

            return protocol.protocolId;
        }

        private static string GetCompactProtocolLabel(TDCampaignTacticalProtocolDefinition protocol)
        {
            if (protocol == null)
            {
                return "STANDARD";
            }

            return protocol.protocolId switch
            {
                "forward_recon" => "RECON",
                "salvage_mandate" => "SALVAGE",
                "field_control" => "CONTROL",
                "modular_reserve" => "RESERVE",
                _ => "STANDARD"
            };
        }

        private static string BuildProtocolPreviewLabel(TDCampaignTacticalProtocolDefinition protocol)
        {
            if (protocol == null || string.Equals(protocol.protocolId, "baseline", StringComparison.OrdinalIgnoreCase))
            {
                return "STANDARD CHARTER / NO MODIFIER";
            }

            var effects = new List<string>();
            AddSignedEffect(effects, "BUD", protocol.startingBudgetDelta);
            AddSignedEffect(effects, "PREP", protocol.prepSecondsDelta);
            AddSignedEffect(effects, "CMD", protocol.scenarioChargeDelta);
            AddMultiplierEffect(effects, "HP", protocol.enemyHpMultiplier);
            AddMultiplierEffect(effects, "REW", protocol.rewardMultiplier);
            AddMultiplierEffect(effects, "COST", protocol.scenarioCostMultiplier);
            return $"{protocol.displayName.ToUpperInvariant()} / {string.Join(" / ", effects)}";
        }

        private static string BuildCompactMutatorEffectLabel(TDCampaignMutatorDefinition mutator)
        {
            if (mutator == null)
            {
                return "BASE RULES";
            }

            var effects = new List<string>();
            AddMultiplierEffect(effects, "HP", mutator.enemyHpMultiplier);
            AddMultiplierEffect(effects, "SPD", mutator.enemySpeedMultiplier);
            if (mutator.enemyArmorBonus != 0)
            {
                effects.Add($"ARM {(mutator.enemyArmorBonus > 0 ? "+" : string.Empty)}{mutator.enemyArmorBonus}");
            }

            AddSignedEffect(effects, "BUD", mutator.startingBudgetDelta);
            AddSignedEffect(effects, "LIFE", mutator.startingIntegrityDelta);
            AddMultiplierEffect(effects, "REW", mutator.rewardMultiplier);
            AddMultiplierEffect(effects, "RES", mutator.resonanceGainMultiplier);
            AddMultiplierEffect(effects, "COST", mutator.scenarioCostMultiplier);
            return effects.Count == 0 ? "BASE RULES" : string.Join(" / ", effects);
        }

        private static bool TryParseCampaignDifficulty(
            string value,
            out TDCampaignDifficultyTier difficulty)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Replace("_", string.Empty).Trim();
            if (string.Equals(normalized, "ember", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "embertrial", StringComparison.OrdinalIgnoreCase))
            {
                difficulty = TDCampaignDifficultyTier.EmberTrial;
                return true;
            }

            return Enum.TryParse(normalized, true, out difficulty) &&
                   (int)difficulty >= (int)TDCampaignDifficultyTier.Standard &&
                   (int)difficulty <= (int)TDCampaignDifficultyTier.EmberTrial;
        }

        private string BuildCurrentDifficultyRuntimeSignature()
        {
            return
                $"budget:{_startingDefenseBudget},integrity:{_startingLineIntegrity}," +
                $"hpX:{_missionEnemyHpMultiplier:0.###},speedX:{_missionEnemySpeedMultiplier:0.###}," +
                $"armor:{_missionEnemyArmorBonus},rewardX:{_missionRewardMultiplier:0.###}," +
                $"resonanceX:{_missionResonanceGainMultiplier:0.###},costX:{_scenarioCostMultiplier:0.###}," +
                $"towerX:{GetCampaignTowerPowerMultiplier():0.###}";
        }

        private bool DoesCurrentDifficultyRuntimeMatch()
        {
            var expectedBudget = DefaultDefenseBudget + GetCampaignStartingBudgetRamp(_campaignRoute?.level?.levelIndex ?? 1);
            var expectedIntegrity = DefaultLineIntegrity + GetCampaignStartingIntegrityRamp(_campaignRoute?.level?.levelIndex ?? 1);
            var expectedHp = 1f;
            var expectedSpeed = 1f;
            var expectedArmor = 0;
            var expectedReward = 1f;
            var expectedResonance = 1f;
            var expectedScenarioCost = 1f;
            var level = _campaignRoute?.level;
            var mutators = level?.mutators ?? Array.Empty<TDCampaignMutatorDefinition>();
            for (var i = 0; i < mutators.Length; i++)
            {
                AccumulateExpectedRuntimeMutator(
                    mutators[i],
                    ref expectedBudget,
                    ref expectedIntegrity,
                    ref expectedHp,
                    ref expectedSpeed,
                    ref expectedArmor,
                    ref expectedReward,
                    ref expectedResonance,
                    ref expectedScenarioCost);
            }

            if (_activeCampaignDifficulty != TDCampaignDifficultyTier.Standard)
            {
                AccumulateExpectedRuntimeMutator(
                    GetDifficultyDefinition(_activeCampaignDifficulty)?.modifiers,
                    ref expectedBudget,
                    ref expectedIntegrity,
                    ref expectedHp,
                    ref expectedSpeed,
                    ref expectedArmor,
                    ref expectedReward,
                    ref expectedResonance,
                    ref expectedScenarioCost);
                AccumulateExpectedRuntimeMutator(
                    GetCampaignChapter(level?.chapterId)?.challengeRemix,
                    ref expectedBudget,
                    ref expectedIntegrity,
                    ref expectedHp,
                    ref expectedSpeed,
                    ref expectedArmor,
                    ref expectedReward,
                    ref expectedResonance,
                    ref expectedScenarioCost);
            }

            CalculateClaimedChapterRewardBonuses(
                out var rewardBudget,
                out var rewardIntegrity,
                out var rewardResonance,
                out _);
            if (_newlyClaimedChapterReward != null &&
                TDCampaignProgression.IsChapterRewardClaimed(_newlyClaimedChapterReward.rewardId))
            {
                rewardBudget -= Mathf.Max(0, _newlyClaimedChapterReward.startingBudgetBonus);
                rewardIntegrity -= Mathf.Max(0, _newlyClaimedChapterReward.startingIntegrityBonus);
                rewardResonance /= ResolveMutatorMultiplier(_newlyClaimedChapterReward.resonanceGainMultiplier);
            }
            expectedBudget = Mathf.Max(0, expectedBudget + rewardBudget);
            expectedIntegrity = Mathf.Max(1, expectedIntegrity + rewardIntegrity);
            expectedResonance *= rewardResonance;
            return expectedBudget == _startingDefenseBudget &&
                   expectedIntegrity == _startingLineIntegrity &&
                   Mathf.Approximately(expectedHp, _missionEnemyHpMultiplier) &&
                   Mathf.Approximately(expectedSpeed, _missionEnemySpeedMultiplier) &&
                   expectedArmor == _missionEnemyArmorBonus &&
                   Mathf.Approximately(expectedReward, _missionRewardMultiplier) &&
                   Mathf.Approximately(expectedResonance, _missionResonanceGainMultiplier) &&
                   Mathf.Approximately(expectedScenarioCost, _scenarioCostMultiplier);
        }

        private float GetDoctrineCommandPowerMultiplier(TDResonanceCommand command)
        {
            if (command == TDResonanceCommand.None)
            {
                return 1f;
            }

            return _activeResonanceDoctrine switch
            {
                TDResonanceDoctrine.Adaptive => IsResonanceCommandMatchForCurrentThreat(command)
                    ? AdaptiveDoctrinePowerMultiplier
                    : 1f,
                TDResonanceDoctrine.EmberSurge => command == TDResonanceCommand.EmberSurge
                    ? SpecializedDoctrinePowerMultiplier
                    : 1f,
                TDResonanceDoctrine.FractureMark => command == TDResonanceCommand.FractureMark
                    ? SpecializedDoctrinePowerMultiplier
                    : 1f,
                _ => 1f
            };
        }

        private string GetDoctrineCommandBoostLabel(TDResonanceCommand command)
        {
            var multiplier = GetDoctrineCommandPowerMultiplier(command);
            return multiplier > 1f
                ? $"Doctrine +{Mathf.RoundToInt((multiplier - 1f) * 100f)}%"
                : "Base";
        }

        private static Color GetDoctrineColor(TDResonanceDoctrine doctrine, float alpha)
        {
            return doctrine switch
            {
                TDResonanceDoctrine.EmberSurge => new Color(0.62f, 0.26f, 0.16f, alpha),
                TDResonanceDoctrine.FractureMark => new Color(0.18f, 0.42f, 0.50f, alpha),
                _ => new Color(0.28f, 0.42f, 0.34f, alpha)
            };
        }

        private void DeploySelectedMission()
        {
            if (_campaignRoute?.level == null || _campaign == null)
            {
                return;
            }

            var selectedLevel = Mathf.Clamp(_missionBoardSelectedLevel, 1, _campaign.totalLevels);
            if (!TDCampaignProgression.IsLevelUnlocked(selectedLevel, _campaign.totalLevels))
            {
                SetStatus($"Mission L{selectedLevel:00} is locked.");
                return;
            }

            if (selectedLevel == _campaignRoute.level.levelIndex)
            {
                if (_gameOver)
                {
                    RestartCurrentScene();
                    return;
                }

                CloseMissionBoard();
                return;
            }

            TDCampaignRouter.SaveLevelIndex(selectedLevel);
            SetStatus($"Deploying mission L{selectedLevel:00}...");
            PlaySfxTone("ui_deploy", 700f, 0.16f, 0.66f, true);
            _showBriefingNextAwake = true;
            var selectedMap = _campaign.maps?.FirstOrDefault(m => m.mapId == _campaignRoute.level.mapId);
            var deployLabel = selectedMap != null && !string.IsNullOrWhiteSpace(selectedMap.displayName)
                ? $"L{selectedLevel:00}  {selectedMap.displayName}"
                : $"MISSION L{selectedLevel:00}";
            LoadingTransition("DEPLOYING", deployLabel);
        }

        private void GoToNextMission()
        {
            if (!_victory || _campaignRoute?.level == null)
            {
                return;
            }

            var nextLevel = _campaignRoute.level.levelIndex + 1;
            if (nextLevel > _campaignRoute.totalLevels)
            {
                OpenMissionBoard();
                return;
            }

            if (!TDCampaignProgression.IsLevelUnlocked(nextLevel, _campaignRoute.totalLevels))
            {
                return;
            }

            TDCampaignRouter.SaveLevelIndex(nextLevel);
            _showBriefingNextAwake = true;
            LoadingTransition("ADVANCING", $"MISSION L{nextLevel:00}");
        }

        private TDCampaignProgressSummary GetCampaignProgressSummary()
        {
            return TDCampaignProgression.BuildSummary(_campaign?.totalLevels ?? 1);
        }

        private int GetMasteredChapterCount()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            var mastered = 0;
            for (var i = 0; i < chapters.Length; i++)
            {
                if (TDCampaignProgression.BuildChapterSummary(chapters[i]).mastered)
                {
                    mastered++;
                }
            }

            return mastered;
        }

        private string BuildCampaignRank(TDCampaignProgressSummary summary, int masteredChapters)
        {
            if (summary == null || summary.clearedLevels < summary.totalLevels)
            {
                return "IN PROGRESS";
            }

            var chapterCount = Mathf.Max(1, _campaign?.chapters?.Length ?? 0);
            var starRatio = summary.availableStars > 0 ? summary.earnedStars / (float)summary.availableStars : 0f;
            var contractRatio = summary.availableContracts > 0 ? summary.completedContracts / (float)summary.availableContracts : 0f;
            var masteryRatio = masteredChapters / (float)chapterCount;
            var rating = (starRatio * 0.60f) + (contractRatio * 0.30f) + (masteryRatio * 0.10f);
            if (rating >= 0.98f)
            {
                return "S";
            }

            if (rating >= 0.85f)
            {
                return "A";
            }

            if (rating >= 0.70f)
            {
                return "B";
            }

            return "C";
        }

        private string BuildCampaignChapterArchiveLabel()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            var lines = new List<string>(chapters.Length * 2);
            for (var i = 0; i < chapters.Length; i++)
            {
                var chapter = chapters[i];
                var progress = TDCampaignProgression.BuildChapterSummary(chapter);
                var state = progress.mastered ? "MASTERED" : progress.cleared ? "CLEARED" : "IN PROGRESS";
                var rewardState = progress.rewardClaimed ? "ACTIVE" : progress.cleared ? "READY" : "LOCKED";
                lines.Add($"CHAPTER {(char)('A' + i)}  {state}   REWARD {rewardState}   {chapter?.reward?.displayName ?? "No reward"}");
                lines.Add($"CLEAR {progress.clearedLevels}/{progress.totalLevels}   STAR {progress.earnedStars}/{progress.availableStars}   CONTRACT {progress.completedContracts}/{progress.availableContracts}   V {progress.veteranClears}/{progress.totalLevels}   E {progress.emberTrialClears}/{progress.totalLevels}");
                var exams = (_campaign?.levels ?? Array.Empty<TDCampaignLevelDefinition>())
                    .Where(level => level != null && level.chapterId == chapter?.chapterId && level.scenario?.milestoneExam == true)
                    .Select(level =>
                    {
                        var record = TDCampaignProgression.GetLevelProgress(level.levelIndex);
                        var formation = BuildArchivedFormationSignature(record.towerLoadout);
                        return $"L{level.levelIndex:00} {record.bestTacticalScore:00}P {formation} / {TDCampaignProgression.GetTacticalProtocol(level.levelIndex).ToUpperInvariant()}";
                    })
                    .ToArray();
                if (exams.Length > 0)
                {
                    lines.Add($"EXAM SIGNATURE  {string.Join("   ", exams)}");
                }
            }

            return string.Join("\n", lines);
        }

        private static string BuildArchivedFormationSignature(string rawLoadout)
        {
            if (string.IsNullOrWhiteSpace(rawLoadout))
            {
                return "UNRECORDED";
            }

            var labels = new List<string>();
            foreach (var towerId in rawLoadout.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (TDTower.TryParseTowerId(towerId, out var kind))
                {
                    labels.Add(kind switch
                    {
                        TDTowerKind.RailLancer => "RL",
                        TDTowerKind.CinderMortar => "CM",
                        TDTowerKind.FrostCoil => "FC",
                        TDTowerKind.ArcWelder => "AW",
                        TDTowerKind.SiegeDrill => "SD",
                        TDTowerKind.EmberFlak => "EF",
                        TDTowerKind.ResonanceBeacon => "RB",
                        TDTowerKind.GravSnare => "GS",
                        _ => "?"
                    });
                }
            }

            return labels.Count == 0 ? "UNRECORDED" : string.Join("-", labels);
        }

        private string BuildCampaignRewardBonusLabel()
        {
            CalculateClaimedChapterRewardBonuses(out var budget, out var integrity, out var resonance, out var rewardNames);
            var resonanceBonus = Mathf.RoundToInt((resonance - 1f) * 100f);
            var unlockedProtocols = TDCampaignProgression.GetUnlockedProtocolIds();
            var metaRewards = TDCampaignProgression.GetClaimedMetaRewardIds();
            return
                $"ACTIVE LEGACY BONUSES   Budget +{budget}   Integrity +{integrity}   Resonance +{resonanceBonus}%\n" +
                $"REWARDS {rewardNames.Count}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   {(rewardNames.Count == 0 ? "None claimed" : string.Join(" / ", rewardNames))}\n" +
                $"TACTICAL PROTOCOLS {unlockedProtocols.Length + 1}/{Mathf.Max(1, _campaign?.metaProgression?.tacticalProtocols?.Length ?? 1)}   META REWARDS {metaRewards.Length}   {BuildMetaRewardProgressLabel()}\n" +
                $"CODEX DOSSIERS   ENEMY {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   TOWER {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}";
        }

        private string BuildCampaignCompletionRewardLabel()
        {
            CalculateClaimedChapterRewardBonuses(out var budget, out var integrity, out var resonance, out var rewardNames);
            var resonanceBonus = Mathf.RoundToInt((resonance - 1f) * 100f);
            var unlockedProtocols = TDCampaignProgression.GetUnlockedProtocolIds();
            var metaRewards = TDCampaignProgression.GetClaimedMetaRewardIds();
            if (TDLocalization.IsChinese)
            {
                return
                    $"长期增益   资源 +{budget}   防线 +{integrity}   共鸣 +{resonanceBonus}%\n" +
                    $"奖励 {rewardNames.Count}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   协议 {unlockedProtocols.Length + 1}/{Mathf.Max(1, _campaign?.metaProgression?.tacticalProtocols?.Length ?? 1)}   长期奖励 {metaRewards.Length}\n" +
                    $"图鉴   敌人 {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   防御塔 {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}";
            }

            return
                $"LEGACY   BUDGET +{budget}   INTEGRITY +{integrity}   RESONANCE +{resonanceBonus}%\n" +
                $"REWARDS {rewardNames.Count}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   PROTOCOLS {unlockedProtocols.Length + 1}/{Mathf.Max(1, _campaign?.metaProgression?.tacticalProtocols?.Length ?? 1)}   META {metaRewards.Length}\n" +
                $"DOSSIERS   ENEMY {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   TOWER {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}   {BuildMetaRewardProgressLabel()}";
        }

        private string BuildMetaRewardProgressLabel()
        {
            var summary = GetCampaignProgressSummary();
            var ratingTarget = (_campaign?.metaProgression?.ratingRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>())
                .Where(reward => !TDCampaignProgression.GetClaimedMetaRewardIds().Contains(reward.rewardId))
                .Select(reward => reward.threshold)
                .DefaultIfEmpty(summary.earnedStars)
                .Min();
            return $"NEXT S {summary.earnedStars}/{ratingTarget} E {GetCompletedEnemyDossierCount()}/4 T {GetCompletedTowerDossierCount()}/4";
        }

        private void CalculateClaimedChapterRewardBonuses(
            out int budget,
            out int integrity,
            out float resonance,
            out List<string> rewardNames)
        {
            budget = 0;
            integrity = 0;
            resonance = 1f;
            rewardNames = new List<string>();
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            for (var i = 0; i < chapters.Length; i++)
            {
                var reward = chapters[i]?.reward;
                if (reward == null || !TDCampaignProgression.IsChapterRewardClaimed(reward.rewardId))
                {
                    continue;
                }

                budget += Mathf.Max(0, reward.startingBudgetBonus);
                integrity += Mathf.Max(0, reward.startingIntegrityBonus);
                resonance *= ResolveMutatorMultiplier(reward.resonanceGainMultiplier);
                rewardNames.Add(reward.displayName);
            }
        }

        private TDCampaignChapterDefinition GetCampaignChapterAt(int chapterIndex)
        {
            return _campaign?.chapters != null && chapterIndex >= 0 && chapterIndex < _campaign.chapters.Length
                ? _campaign.chapters[chapterIndex]
                : null;
        }

        private TDCampaignChapterDefinition GetCampaignChapter(string chapterId)
        {
            if (_campaign?.chapters == null)
            {
                return null;
            }

            for (var i = 0; i < _campaign.chapters.Length; i++)
            {
                var chapter = _campaign.chapters[i];
                if (chapter != null && string.Equals(chapter.chapterId, chapterId, StringComparison.OrdinalIgnoreCase))
                {
                    return chapter;
                }
            }

            return null;
        }

        private TDCampaignLevelDefinition GetCampaignLevel(int levelIndex)
        {
            if (_campaign?.levels == null)
            {
                return null;
            }

            for (var i = 0; i < _campaign.levels.Length; i++)
            {
                var level = _campaign.levels[i];
                if (level != null && level.levelIndex == levelIndex)
                {
                    return level;
                }
            }

            return null;
        }

        private static string GetDifficultyRecordLabel(TDCampaignLevelProgress progress)
        {
            if (progress == null || !progress.cleared)
            {
                return "UNTESTED";
            }

            return progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.EmberTrial
                ? "EMBER TRIAL"
                : progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.Veteran
                    ? "VETERAN"
                    : "STANDARD";
        }

        private TDCampaignMapDefinition GetCampaignMap(string mapId)
        {
            if (_campaign?.maps == null)
            {
                return null;
            }

            for (var i = 0; i < _campaign.maps.Length; i++)
            {
                var map = _campaign.maps[i];
                if (map != null && string.Equals(map.mapId, mapId, StringComparison.OrdinalIgnoreCase))
                {
                    return map;
                }
            }

            return null;
        }

        private void BuildMissionWaveIntel(
            TDCampaignLevelDefinition level,
            out int waveCount,
            out int laneCount,
            out string composition,
            out HashSet<string> threatTags,
            out string error)
        {
            waveCount = 0;
            laneCount = 0;
            composition = "No deployment data.";
            threatTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;
            if (level == null || !TDWaveLoader.TryLoadFromResources($"Data/waves/{level.waveSetId}", _globalEnemyCatalog, out var waveSet, out error))
            {
                return;
            }

            var enemyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lanes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var waves = waveSet.waves ?? Array.Empty<TDWaveDefinition>();
            waveCount = waves.Length;
            for (var w = 0; w < waves.Length; w++)
            {
                var wave = waves[w];
                if (wave == null)
                {
                    continue;
                }

                AddCampaignTags(threatTags, wave.threatTags);
                if (!string.IsNullOrWhiteSpace(wave.goalTag))
                {
                    threatTags.Add(wave.goalTag);
                }

                var groups = wave.groups ?? Array.Empty<TDWaveGroup>();
                for (var g = 0; g < groups.Length; g++)
                {
                    var group = groups[g];
                    if (group == null || string.IsNullOrWhiteSpace(group.enemyId))
                    {
                        continue;
                    }

                    IncrementCounter(enemyCounts, group.enemyId, Mathf.Max(0, group.count));
                    AddMissionLaneKeys(lanes, group.lane, group.formation);
                    if (_globalEnemyCatalog.TryGetValue(group.enemyId, out var entry))
                    {
                        AddCampaignTags(threatTags, entry.tags);
                    }
                }
            }

            laneCount = Mathf.Max(1, lanes.Count);
            var enemies = new List<KeyValuePair<string, int>>(enemyCounts);
            enemies.Sort((a, b) =>
            {
                var delta = b.Value.CompareTo(a.Value);
                return delta != 0 ? delta : string.CompareOrdinal(a.Key, b.Key);
            });
            var labels = new List<string>();
            for (var i = 0; i < enemies.Count && i < 4; i++)
            {
                labels.Add($"{GetEnemyDisplayName(enemies[i].Key)} x{enemies[i].Value}");
            }

            composition = labels.Count == 0 ? "No enemies configured." : string.Join(" / ", labels);
        }

        private string BuildMissionCounterPlan(int levelIndex, HashSet<string> threatTags)
        {
            var available = GetTowerKindsUnlockedAtLevel(levelIndex);
            var recommendations = new List<TDTowerKind>();
            if (HasAnyCampaignTag(threatTags, "armored", "heavy", "boss", "durability"))
            {
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.RailLancer);
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.SiegeDrill);
            }

            if (HasAnyCampaignTag(threatTags, "fast", "flank", "anti_fast"))
            {
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.FrostCoil);
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.EmberFlak);
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.GravSnare);
            }

            if (HasAnyCampaignTag(threatTags, "swarm", "split", "mixed"))
            {
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.CinderMortar);
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.ArcWelder);
            }

            if (HasAnyCampaignTag(threatTags, "support", "attrition"))
            {
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.ResonanceBeacon);
                AddAvailableTowerRecommendation(recommendations, available, TDTowerKind.GravSnare);
            }

            for (var i = 0; recommendations.Count < 3 && i < available.Count; i++)
            {
                AddAvailableTowerRecommendation(recommendations, available, available[i]);
            }

            var towerLabels = new List<string>();
            for (var i = 0; i < recommendations.Count && i < 4; i++)
            {
                towerLabels.Add(GetCompactTowerLabel(recommendations[i]));
            }

            var emberFit = HasAnyCampaignThreatPattern(threatTags, EmberSurgeThreatPatterns);
            var fractureFit = HasAnyCampaignThreatPattern(threatTags, FractureMarkThreatPatterns);
            var commandPlan = emberFit && fractureFit
                ? "Ember for armor peaks / Fracture for route pressure"
                : emberFit
                    ? "Favor Ember Surge on durability peaks"
                    : fractureFit
                        ? "Favor Fracture Mark on speed and control peaks"
                        : "Answer the live wave tag";
            return $"COUNTER PLAN\nTOWERS  {string.Join(" / ", towerLabels)}\nMATRIX  Match specialization traits before the exam wave\nCOMMAND  {commandPlan}";
        }

        private List<TDTowerKind> GetTowerKindsUnlockedAtLevel(int levelIndex)
        {
            var seen = new HashSet<TDTowerKind>();
            if (_campaign?.levels != null)
            {
                for (var i = 0; i < _campaign.levels.Length; i++)
                {
                    var level = _campaign.levels[i];
                    if (level == null || level.levelIndex > levelIndex || level.newTowerUnlocks == null)
                    {
                        continue;
                    }

                    for (var t = 0; t < level.newTowerUnlocks.Length; t++)
                    {
                        if (TDTower.TryParseTowerId(level.newTowerUnlocks[t], out var kind))
                        {
                            seen.Add(kind);
                        }
                    }
                }
            }

            var result = new List<TDTowerKind>();
            var buildOrder = TDTower.GetBuildOrder();
            for (var i = 0; i < buildOrder.Count; i++)
            {
                if (seen.Contains(buildOrder[i]))
                {
                    result.Add(buildOrder[i]);
                }
            }

            if (result.Count == 0)
            {
                result.Add(TDTowerKind.RailLancer);
                result.Add(TDTowerKind.CinderMortar);
                result.Add(TDTowerKind.FrostCoil);
            }

            return result;
        }

        private string BuildMissionArrivalLabel(TDCampaignLevelDefinition level)
        {
            var arrivals = new List<string>();
            if (level?.newTowerUnlocks != null)
            {
                for (var i = 0; i < level.newTowerUnlocks.Length; i++)
                {
                    if (TDTower.TryParseTowerId(level.newTowerUnlocks[i], out var kind))
                    {
                        arrivals.Add(GetCompactTowerLabel(kind));
                    }
                }
            }

            if (level?.newEnemyUnlocks != null)
            {
                for (var i = 0; i < level.newEnemyUnlocks.Length && arrivals.Count < 4; i++)
                {
                    arrivals.Add(GetEnemyDisplayName(level.newEnemyUnlocks[i]));
                }
            }

            return arrivals.Count == 0 ? "NEW INTEL  No new deployment assets" : $"NEW INTEL  {string.Join(" / ", arrivals)}";
        }

        private static void AddCampaignTags(HashSet<string> target, string[] tags)
        {
            if (target == null || tags == null)
            {
                return;
            }

            for (var i = 0; i < tags.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(tags[i]))
                {
                    target.Add(tags[i]);
                }
            }
        }

        private static void AddMissionLaneKeys(HashSet<string> lanes, string laneToken, string formationToken)
        {
            if (lanes == null)
            {
                return;
            }

            var lane = NormalizeGroupToken(laneToken);
            var formation = NormalizeGroupToken(formationToken);
            if (string.IsNullOrEmpty(lane))
            {
                lane = formation == "split_lane" || formation == "cross_lane" ? formation : "center";
            }

            switch (lane)
            {
                case "all":
                    lanes.Add("center");
                    lanes.Add("left");
                    lanes.Add("right");
                    break;
                case "split_lane":
                    lanes.Add("left");
                    lanes.Add("right");
                    break;
                case "cross_lane":
                    lanes.Add("cross");
                    break;
                case "default":
                case "center":
                    lanes.Add("center");
                    break;
                default:
                    lanes.Add(lane);
                    break;
            }
        }

        private static bool HasAnyCampaignTag(HashSet<string> tags, params string[] patterns)
        {
            return HasAnyCampaignThreatPattern(tags, patterns);
        }

        private static bool HasAnyCampaignThreatPattern(HashSet<string> tags, string[] patterns)
        {
            if (tags == null || patterns == null)
            {
                return false;
            }

            foreach (var tag in tags)
            {
                for (var i = 0; i < patterns.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(tag) && !string.IsNullOrWhiteSpace(patterns[i]) &&
                        tag.IndexOf(patterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddAvailableTowerRecommendation(
            List<TDTowerKind> recommendations,
            List<TDTowerKind> available,
            TDTowerKind kind)
        {
            if (recommendations != null && available != null && available.Contains(kind) && !recommendations.Contains(kind))
            {
                recommendations.Add(kind);
            }
        }

        private static string FormatCampaignTags(string[] tags, int maxTags)
        {
            if (tags == null || tags.Length == 0)
            {
                return "none";
            }

            var labels = new List<string>();
            for (var i = 0; i < tags.Length && labels.Count < Mathf.Max(1, maxTags); i++)
            {
                if (!string.IsNullOrWhiteSpace(tags[i]))
                {
                    labels.Add(tags[i].Trim().Replace('_', ' '));
                }
            }

            return labels.Count == 0 ? "none" : string.Join(" / ", labels);
        }

        private static string BuildMissionDisplayThreatLabel(HashSet<string> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return "unclassified";
            }

            var labels = new List<string>();
            for (var patternIndex = 0; patternIndex < MissionIntelThreatPatterns.Length && labels.Count < 6; patternIndex++)
            {
                var pattern = MissionIntelThreatPatterns[patternIndex];
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag) &&
                        tag.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        labels.Add(pattern.Replace('_', ' '));
                        break;
                    }
                }
            }

            return labels.Count == 0 ? "unclassified" : string.Join(" / ", labels);
        }

        private int GetMissionIntegrityStarThreshold()
        {
            return Mathf.CeilToInt(_startingLineIntegrity * 0.5f);
        }

        private static int GetMissionIntegrityStarThreshold(TDCampaignLevelDefinition level)
        {
            return Mathf.CeilToInt(GetMissionStartingIntegrity(level) * 0.5f);
        }

        private static int GetMissionStartingIntegrity(TDCampaignLevelDefinition level)
        {
            var integrity = DefaultLineIntegrity;
            var mutators = level?.mutators ?? Array.Empty<TDCampaignMutatorDefinition>();
            for (var i = 0; i < mutators.Length; i++)
            {
                integrity += mutators[i]?.startingIntegrityDelta ?? 0;
            }

            return Mathf.Max(1, integrity);
        }

        private string BuildMissionContractBrief(TDCampaignLevelDefinition level, TDCampaignLevelProgress progress)
        {
            var contract = level?.contract;
            if (contract == null)
            {
                return "OPTIONAL CONTRACT\nNo contract assigned\nMUTATOR  Standard conditions";
            }

            var state = progress != null && progress.contractCompleted
                ? "CONTRACT MEDAL EARNED"
                : "OPTIONAL CONTRACT";
            return $"{state}\n{contract.displayName}: {BuildContractObjectiveLabel(contract)}\nMUTATOR  {BuildMissionMutatorSummary(level)}";
        }

        private string BuildCurrentMissionContractHudLabel()
        {
            var report = EvaluateCurrentMissionContract();
            if (report?.contract == null)
            {
                return $"CONTRACT  None\nRULES  {BuildActiveMissionRulesSummary(_campaignRoute?.level)}";
            }

            var state = report.completed
                ? "SECURED"
                : _gameOver
                    ? "MISSED"
                    : report.targetMet
                        ? "ON TARGET"
                        : "IN PROGRESS";
            return $"CONTRACT  {report.contract.displayName}: {GetContractMetricLabel(report.contract.metric)} {report.currentValue}/{report.contract.target} [{state}]\n" +
                   $"RULES  {BuildActiveMissionRulesSummary(_campaignRoute?.level)}";
        }

        private string BuildActiveMissionRulesSummary(TDCampaignLevelDefinition level)
        {
            var labels = new List<string> { GetDifficultyShortLabel(_activeCampaignDifficulty) };
            var mutators = level?.mutators ?? Array.Empty<TDCampaignMutatorDefinition>();
            for (var i = 0; i < mutators.Length; i++)
            {
                if (mutators[i] != null && !string.IsNullOrWhiteSpace(mutators[i].displayName))
                {
                    labels.Add(mutators[i].displayName);
                }
            }

            if (_activeCampaignDifficulty != TDCampaignDifficultyTier.Standard)
            {
                var remix = GetCampaignChapter(level?.chapterId)?.challengeRemix;
                if (remix != null && !string.IsNullOrWhiteSpace(remix.displayName))
                {
                    labels.Add($"Remix {remix.displayName}");
                }
            }

            return string.Join(" / ", labels);
        }

        private TDMissionContractReport EvaluateCurrentMissionContract()
        {
            var contract = _campaignRoute?.level?.contract;
            if (contract == null)
            {
                return null;
            }

            var currentValue = GetContractCurrentValue(contract.metric);
            var targetMet = IsContractTargetMet(contract, currentValue);
            return new TDMissionContractReport
            {
                contract = contract,
                currentValue = currentValue,
                targetMet = targetMet,
                completed = _victory && targetMet
            };
        }

        private int GetContractCurrentValue(string metric)
        {
            return metric switch
            {
                "integrity" => _lineIntegrity,
                "budget" => _defenseBudget,
                "escapes" => _totalEscapes,
                "tower_count" => _builtTowerCount,
                "upgrades" => _upgradesPurchased,
                "tactical_score" => CalculateRunScore().total,
                "counter_score" => CalculateRunCounterScore(),
                "command_score" => CalculateRunCommandScore(),
                "matrix_full_matches" => _matrixFullMatches,
                "convergence_triggers" => _matrixConvergenceTriggers,
                _ => 0
            };
        }

        private static bool IsContractTargetMet(TDCampaignContractDefinition contract, int currentValue)
        {
            if (contract == null)
            {
                return false;
            }

            return string.Equals(contract.comparison, "at_most", StringComparison.OrdinalIgnoreCase)
                ? currentValue <= contract.target
                : currentValue >= contract.target;
        }

        private static string BuildContractObjectiveLabel(TDCampaignContractDefinition contract)
        {
            if (contract == null)
            {
                return "No target";
            }

            var comparison = string.Equals(contract.comparison, "at_most", StringComparison.OrdinalIgnoreCase)
                ? "<="
                : ">=";
            return $"Win with {GetContractMetricLabel(contract.metric)} {comparison} {contract.target}";
        }

        private static string GetContractMetricLabel(string metric)
        {
            return metric switch
            {
                "integrity" => "Integrity",
                "budget" => "Budget",
                "escapes" => "Escapes",
                "tower_count" => "Towers",
                "upgrades" => "Upgrades",
                "tactical_score" => "Tactical",
                "counter_score" => "Counter",
                "command_score" => "Command",
                "matrix_full_matches" => "Matrix Matches",
                "convergence_triggers" => "Convergences",
                _ => "Progress"
            };
        }

        private static string BuildMissionMutatorSummary(TDCampaignLevelDefinition level)
        {
            var mutators = level?.mutators ?? Array.Empty<TDCampaignMutatorDefinition>();
            if (mutators.Length == 0)
            {
                return "Standard conditions";
            }

            var labels = new List<string>();
            for (var i = 0; i < mutators.Length; i++)
            {
                var mutator = mutators[i];
                if (mutator == null)
                {
                    continue;
                }

                labels.Add($"{mutator.displayName}: {BuildMutatorEffectLabel(mutator)}");
            }

            return labels.Count == 0 ? "Standard conditions" : string.Join(" | ", labels);
        }

        private static string BuildMutatorEffectLabel(TDCampaignMutatorDefinition mutator)
        {
            if (mutator == null)
            {
                return "No effect";
            }

            var effects = new List<string>();
            AddMultiplierEffect(effects, "Enemy HP", mutator.enemyHpMultiplier);
            AddMultiplierEffect(effects, "Speed", mutator.enemySpeedMultiplier);
            if (mutator.enemyArmorBonus != 0)
            {
                effects.Add($"Armor +{mutator.enemyArmorBonus}");
            }

            AddSignedEffect(effects, "Start budget", mutator.startingBudgetDelta);
            AddSignedEffect(effects, "Integrity", mutator.startingIntegrityDelta);
            AddMultiplierEffect(effects, "Rewards", mutator.rewardMultiplier);
            AddMultiplierEffect(effects, "Resonance gain", mutator.resonanceGainMultiplier);
            AddMultiplierEffect(effects, "Scenario cost", mutator.scenarioCostMultiplier);
            return effects.Count == 0 ? "No effect" : string.Join(" / ", effects);
        }

        private static void AddMultiplierEffect(List<string> effects, string label, float multiplier)
        {
            if (multiplier > 0f && !Mathf.Approximately(multiplier, 1f))
            {
                effects.Add($"{label} x{multiplier:0.##}");
            }
        }

        private static void AddSignedEffect(List<string> effects, string label, int value)
        {
            if (value != 0)
            {
                effects.Add($"{label} {(value > 0 ? "+" : string.Empty)}{value}");
            }
        }

        private void UpdateMissionContractFeedback()
        {
            if (_gameOver || _missionBoardOpen || !_campaignDeploymentConfirmed)
            {
                return;
            }

            var report = EvaluateCurrentMissionContract();
            if (report?.contract == null)
            {
                return;
            }

            if (!_contractFeedbackInitialized)
            {
                _contractFeedbackInitialized = true;
                _contractFeedbackTargetMet = report.targetMet;
                return;
            }

            if (report.targetMet == _contractFeedbackTargetMet)
            {
                return;
            }

            _contractFeedbackTargetMet = report.targetMet;
            if (Time.unscaledTime < _nextContractFeedbackTime)
            {
                return;
            }

            _nextContractFeedbackTime = Time.unscaledTime + 2.5f;
            PushTacticalEvent(
                report.targetMet
                    ? $"Contract on target: {report.contract.displayName}"
                    : $"Contract pressure: {report.contract.displayName}",
                5.0f);
        }

        private void RebuildTowerBuildButtons()
        {
            if (_uiTowerBarRoot == null)
            {
                return;
            }

            for (var i = 0; i < _uiTowerButtons.Count; i++)
            {
                if (_uiTowerButtons[i] != null)
                {
                    Destroy(_uiTowerButtons[i].gameObject);
                }
            }

            _uiTowerButtons.Clear();
            _uiTowerButtonTexts.Clear();
            _uiTowerButtonIcons.Clear();
            _uiTowerButtonAccents.Clear();
            _uiTowerButtonOutlines.Clear();

            var towerBarWidth = Mathf.Clamp(
                58f + (Mathf.Max(1, _unlockedTowerKinds.Count) * 74f),
                132f,
                650f);
            _uiTowerBarRoot.sizeDelta = new Vector2(towerBarWidth, _uiTowerBarRoot.sizeDelta.y);

            for (var i = 0; i < _unlockedTowerKinds.Count; i++)
            {
                var kind = _unlockedTowerKinds[i];
                var button = CreateUiButton($"Build {kind}", _uiTowerBarRoot, new Vector2(54f + (i * 74f), -9f), new Vector2(66f, 44f), string.Empty, 10, () => { });
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    _selectedTowerKind = kind;
                    _selectedTowerForUi?.Readability?.SetInteractionState(
                        _selectedTowerForUi == _hoveredTower,
                        false);
                    _selectedTowerForUi = null;
                    SetStatus($"Selected {GetTowerKindLabel(kind)}.");
                });

                var identity = TDUiVisualIdentity.GetTower(kind);
                var icon = CreateUiSpriteImage($"{kind} Identity Icon", button.transform, new Vector2(4f, -5f), new Vector2(34f, 34f), identity.iconResourcePath, Color.white);
                var accent = CreateUiImage($"{kind} Identity Accent", button.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 3f), identity.accent);
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = identity.accent;
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;
                outline.enabled = false;

                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 1f);
                label.rectTransform.anchoredPosition = new Vector2(39f, -3f);
                label.rectTransform.sizeDelta = new Vector2(23f, 36f);
                label.alignment = TextAnchor.MiddleCenter;

                _uiTowerButtons.Add(button);
                _uiTowerButtonTexts.Add(label);
                _uiTowerButtonIcons.Add(icon);
                _uiTowerButtonAccents.Add(accent);
                _uiTowerButtonOutlines.Add(outline);
            }
        }

        private void SetUpgradeButtonUi(Button button, Text label, string branchLabel, int cost, bool interactable, string preview)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            var previewLabel = string.IsNullOrWhiteSpace(preview) ? string.Empty : $"\n{preview}";
            SetUiText(label, cost > 0 ? $"{branchLabel} {cost}{previewLabel}" : $"{branchLabel}\nMAX");
            if (button.targetGraphic is Image image)
            {
                image.color = interactable
                    ? new Color(0.32f, 0.44f, 0.36f, 0.96f)
                    : new Color(0.13f, 0.14f, 0.14f, 0.66f);
            }
        }

        private void UpdateTacticalEventTimers()
        {
            for (var i = _tacticalEvents.Count - 1; i >= 0; i--)
            {
                var item = _tacticalEvents[i];
                if (item == null)
                {
                    _tacticalEvents.RemoveAt(i);
                    continue;
                }

                item.timer -= Time.deltaTime;
                if (item.timer <= 0f)
                {
                    _tacticalEvents.RemoveAt(i);
                }
            }
        }

        private void PushTacticalEvent(string message, float duration = 5.2f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var normalizedMessage = message.Trim();
            var normalizedDuration = Mathf.Max(1.2f, duration);
            for (var i = 0; i < _tacticalEvents.Count; i++)
            {
                var existing = _tacticalEvents[i];
                if (existing == null || existing.message != normalizedMessage)
                {
                    continue;
                }

                existing.timer = normalizedDuration;
                if (i > 0)
                {
                    _tacticalEvents.RemoveAt(i);
                    _tacticalEvents.Insert(0, existing);
                }

                return;
            }

            _tacticalEvents.Insert(0, new TDTacticalEvent
            {
                message = normalizedMessage,
                timer = normalizedDuration
            });

            const int maxEvents = 3;
            while (_tacticalEvents.Count > maxEvents)
            {
                _tacticalEvents.RemoveAt(_tacticalEvents.Count - 1);
            }
        }

        public void NotifyUltimateEffect(TDTower sourceTower, TDEnemy target, bool utilityEffect, int affectedTargets)
        {
            if (sourceTower == null || sourceTower.ActiveSpecialization == null)
            {
                return;
            }

            var kind = sourceTower != null ? sourceTower.Kind : TDTowerKind.RailLancer;
            var definition = sourceTower.ActiveSpecialization;
            if (Time.unscaledTime >= _nextUltimateSfxTime)
            {
                _nextUltimateSfxTime = Time.unscaledTime + 0.35f;
                PlaySfxTone("specialization_ult", 840f, 0.16f, 0.66f, true);
            }

            var towerStat = GetOrCreateTowerStat(sourceTower);
            if (towerStat != null)
            {
                if (utilityEffect)
                {
                    towerStat.utilitySpecProcs++;
                }
                else
                {
                    towerStat.damageSpecProcs++;
                }

                towerStat.ultimateAffectedTargets += Mathf.Max(1, affectedTargets);
            }

            IncrementCounter(_ultimateProcCounts, definition.specializationId);
            RecordTowerCodexObservation(sourceTower.Kind, TDTowerCodexObservation.SpecializationProc);
            _matrixOpportunities++;
            var traitMatched = DoesEnemyMatchSpecialization(target, definition);
            var resonanceMatched = IsSpecializationAffinityActive(definition);
            if (traitMatched)
            {
                _matrixTraitMatches++;
                if (towerStat != null)
                {
                    towerStat.matrixTraitMatches++;
                }
            }

            if (resonanceMatched)
            {
                _matrixResonanceMatches++;
                if (towerStat != null)
                {
                    towerStat.matrixResonanceMatches++;
                }
            }

            if (traitMatched && resonanceMatched)
            {
                RecordTowerCodexObservation(sourceTower.Kind, TDTowerCodexObservation.MatrixMatch);
                _matrixFullMatches++;
                IncrementCounter(_ultimateFullMatchCounts, definition.specializationId);
                if (towerStat != null)
                {
                    towerStat.matrixFullMatches++;
                }

                var syncContribution = utilityEffect ? Mathf.Clamp(affectedTargets, 1, 3) : 1;
                _matrixWindowSync += syncContribution;
                _matrixWindowSpecializationIds.Add(definition.specializationId);
                CaptureMatrixWindowPeak();
                TryTriggerMatrixConvergence();
            }

            if (kind == TDTowerKind.ResonanceBeacon && utilityEffect)
            {
                AddResonanceCharge(Mathf.Max(1, affectedTargets) * 0.55f);
            }

            if (target != null)
            {
                var tier = traitMatched && resonanceMatched ? TDBattleFeedbackTier.Critical : TDBattleFeedbackTier.Tactical;
                _battlePresentation?.EmitFeedback(
                    TDBattleFeedbackKind.Specialization,
                    target.transform.position,
                    traitMatched && resonanceMatched ? "MATRIX" : definition.displayName,
                    tier);
            }

            var now = Time.time;
            var matrixLabel = traitMatched && resonanceMatched ? " MATRIX" : traitMatched ? " counter" : string.Empty;
            if (utilityEffect)
            {
                if (now < _nextUtilitySpecialistFeedbackTime)
                {
                    return;
                }

                _nextUtilitySpecialistFeedbackTime = now + 1.6f;
                PushTacticalEvent($"Ultimate: {definition.displayName}{matrixLabel} x{Mathf.Max(1, affectedTargets)}", 4.0f);
                PlaySfxTone("feedback_special_utility", 720f, 0.12f, 0.48f, true);
                return;
            }

            if (now < _nextDamageSpecialistFeedbackTime)
            {
                return;
            }

            _nextDamageSpecialistFeedbackTime = now + 1.4f;
            PushTacticalEvent($"Ultimate: {definition.displayName}{matrixLabel}", 4.0f);
            PlaySfxTone("feedback_special_damage", 880f, 0.11f, 0.52f, true);
        }

        public float GetSpecializationSynergyMultiplier(TDTower sourceTower, TDEnemy enemy)
        {
            var definition = sourceTower?.ActiveSpecialization;
            return definition != null && DoesEnemyMatchSpecialization(enemy, definition) && IsSpecializationAffinityActive(definition)
                ? 1.24f
                : 1f;
        }

        private int CountOwnedSpecializationsForCommand(TDResonanceCommand command, out int threatFitCount)
        {
            var alignedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var threatFitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            for (var i = 0; i < towers.Length; i++)
            {
                var definition = towers[i]?.ActiveSpecialization;
                if (definition == null || !IsSpecializationAffinityCompatible(definition, command))
                {
                    continue;
                }

                alignedIds.Add(definition.specializationId);
                if (DoesSpecializationMatchCurrentThreat(definition))
                {
                    threatFitIds.Add(definition.specializationId);
                }
            }

            threatFitCount = threatFitIds.Count;
            return alignedIds.Count;
        }

        private string BuildMatrixWindowStatusLabel()
        {
            var specializationCount = _matrixWindowSpecializationIds.Count;
            if (_matrixConvergenceTriggeredThisWindow)
            {
                var effect = _activeResonanceCommand == TDResonanceCommand.EmberSurge
                    ? "Overdrive: damage +12%, rate +10%, window extended"
                    : $"Lockdown: {_matrixFractureConvergenceAffectedTargets} enemies exposed and pinned";
                return $"CONVERGENCE ACTIVE  Sync {_matrixWindowSync}  |  Specs {specializationCount}\n{effect}";
            }

            var matchNeed = Mathf.Max(0, MatrixConvergenceRequiredMatches - _matrixWindowSync);
            var specializationNeed = Mathf.Max(0, MatrixConvergenceRequiredSpecializations - specializationCount);
            return $"SYNC {_matrixWindowSync}/{MatrixConvergenceRequiredMatches}   SPECS {specializationCount}/{MatrixConvergenceRequiredSpecializations}\n" +
                   $"Need +{matchNeed} sync, +{specializationNeed} specs for Convergence";
        }

        private void CaptureMatrixWindowPeak()
        {
            _matrixBestWindowSync = Mathf.Max(_matrixBestWindowSync, _matrixWindowSync);
            _matrixBestWindowSpecializations = Mathf.Max(_matrixBestWindowSpecializations, _matrixWindowSpecializationIds.Count);
        }

        private void TryTriggerMatrixConvergence()
        {
            if (_matrixConvergenceTriggeredThisWindow || _activeResonanceCommand == TDResonanceCommand.None ||
                _matrixWindowSync < MatrixConvergenceRequiredMatches ||
                _matrixWindowSpecializationIds.Count < MatrixConvergenceRequiredSpecializations)
            {
                return;
            }

            _matrixConvergenceTriggeredThisWindow = true;
            _matrixConvergenceTriggers++;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                _matrixEmberConvergenceTriggers++;
                var before = _resonanceWindowTimer;
                _resonanceWindowTimer = Mathf.Min(
                    ResonanceWindowDuration + MatrixConvergenceEmberWindowExtension,
                    _resonanceWindowTimer + MatrixConvergenceEmberWindowExtension);
                _matrixEmberConvergenceWindowSeconds += Mathf.Max(0f, _resonanceWindowTimer - before);
                PushTacticalEvent("MATRIX CONVERGENCE: Ember Overdrive", 5.2f);
                SetStatus("Matrix Convergence: Ember Overdrive engaged");
                PlaySfxTone("matrix_convergence_ember", 920f, 0.30f, 1.0f, true);
                return;
            }

            _matrixFractureConvergenceTriggers++;
            var affected = 0;
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                enemy.SetResonanceMark(MatrixConvergenceFractureDuration);
                enemy.ApplyExposed(MatrixConvergenceFractureDuration, MatrixConvergenceFractureExposure);
                enemy.ApplyStagger(enemy.HasTag("boss") ? 0.10f : 0.28f, enemy.HasTag("boss") ? 0.72f : 0.18f);
                affected++;
            }

            _matrixFractureConvergenceAffectedTargets += affected;
            PushTacticalEvent($"MATRIX CONVERGENCE: Fracture Lockdown x{affected}", 5.2f);
            SetStatus($"Matrix Convergence: Fracture Lockdown pinned {affected} enemies");
            PlaySfxTone("matrix_convergence_fracture", 760f, 0.30f, 1.0f, false);
        }

        private static bool DoesEnemyMatchSpecialization(TDEnemy enemy, TDTowerSpecializationDefinition definition)
        {
            if (enemy == null || definition?.counterTags == null)
            {
                return false;
            }

            for (var i = 0; i < definition.counterTags.Length; i++)
            {
                if (enemy.HasTag(definition.counterTags[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSpecializationAffinityActive(TDTowerSpecializationDefinition definition)
        {
            if (definition == null || !IsResonanceWindowActive || _activeResonanceCommand == TDResonanceCommand.None)
            {
                return false;
            }

            return IsSpecializationAffinityCompatible(definition, _activeResonanceCommand);
        }

        private static bool IsSpecializationAffinityCompatible(TDTowerSpecializationDefinition definition, TDResonanceCommand command)
        {
            if (definition == null || command == TDResonanceCommand.None)
            {
                return false;
            }

            return definition.resonanceAffinity switch
            {
                TDResonanceAffinity.EmberSurge => command == TDResonanceCommand.EmberSurge,
                TDResonanceAffinity.FractureMark => command == TDResonanceCommand.FractureMark,
                _ => true
            };
        }

        private bool DoesSpecializationMatchCurrentThreat(TDTowerSpecializationDefinition definition)
        {
            if (definition?.counterTags == null || _currentWaveThreatTagSet.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < definition.counterTags.Length; i++)
            {
                if (_currentWaveThreatTagSet.Contains(definition.counterTags[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildTacticalFeedLabel()
        {
            if (_tacticalEvents.Count == 0)
            {
                return _isInPrepPhase
                    ? $"Prep: {BuildWaveRouteLabel(_currentWaveDefinition)}"
                    : "Watching leaks, split spawns, counters, and wave clears.";
            }

            var labels = new List<string>();
            for (var i = 0; i < _tacticalEvents.Count && i < 3; i++)
            {
                if (_tacticalEvents[i] != null && !string.IsNullOrWhiteSpace(_tacticalEvents[i].message))
                {
                    labels.Add(_tacticalEvents[i].message);
                }
            }

            return labels.Count == 0 ? "-" : string.Join("\n", labels);
        }

        private string BuildCompactTacticalFeedLabel()
        {
            for (var i = 0; i < _tacticalEvents.Count; i++)
            {
                var message = _tacticalEvents[i]?.message;
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                var clean = message.Replace("\n", "  ").Trim();
                return clean.Length <= 58 ? clean : clean.Substring(0, 55) + "...";
            }

            return _isInPrepPhase ? "Place towers, inspect routes, then dispatch." : "Defense line engaged.";
        }

        private string BuildCompactBattleStateLabel()
        {
            if (!_isInPrepPhase)
            {
                return "COMBAT";
            }

            return IsOpeningWaveBuildRequired()
                ? "PREP  HOLD"
                : $"PREP  {Mathf.Max(0f, _prepCountdown):0.0}s";
        }

        private string BuildWaveIntelBodyLabel()
        {
            var budgetState = _currentWaveBudgetInRange ? "stable" : "outlier";
            var countdown = _isInPrepPhase ? (IsOpeningWaveBuildRequired() ? "hold" : $"{Mathf.Max(0f, _prepCountdown):0.0}s") : "live";
            var goal = string.IsNullOrWhiteSpace(_currentWaveGoalTag) ? "unknown" : _currentWaveGoalTag;
            return $"W{_wave:00}  {_currentWavePhase}  {countdown}\nGoal {goal}  Budget {_currentWaveBudgetActual:0.##}/{_currentWaveBudgetExpected:0.##} {budgetState}";
        }

        private string BuildCompactWaveIntelBodyLabel()
        {
            var countdown = IsOpeningWaveBuildRequired() ? "HOLD" : $"{Mathf.Max(0f, _prepCountdown):0.0}s";
            if (TDLocalization.IsChinese)
            {
                var localizedCountdown = IsOpeningWaveBuildRequired()
                    ? "等待"
                    : $"{Mathf.Max(0f, _prepCountdown):0.0}秒";
                return $"{localizedCountdown}   目标：{BuildPlayerFacingWaveGoal()}";
            }

            return $"{countdown}   GOAL {BuildPlayerFacingWaveGoal()}";
        }

        private string BuildPlayerFacingWaveGoal()
        {
            var phase = NormalizeGroupToken(_currentWavePhase);
            var mechanic = NormalizeGroupToken(_activeScenarioMechanic?.mechanicType);
            if (TDLocalization.IsChinese)
            {
                if (mechanic == "route_switch")
                {
                    return phase switch
                    {
                        "introduce" => "观察道岔",
                        "reinforce" => "锁定路线",
                        "exam" => "守住分路",
                        _ => "控制枢纽"
                    };
                }

                return phase switch
                {
                    "introduce" => "识别威胁",
                    "reinforce" => "调整防线",
                    "exam" => "通过压力测试",
                    _ => "守住防线"
                };
            }

            if (mechanic == "route_switch")
            {
                return phase switch
                {
                    "introduce" => "READ THE SWITCH",
                    "reinforce" => "COMMIT A ROUTE",
                    "exam" => "HOLD THE SPLIT",
                    _ => "CONTROL THE JUNCTION"
                };
            }

            return phase switch
            {
                "introduce" => "READ THE THREAT",
                "reinforce" => "ADAPT THE LINE",
                "exam" => "PASS THE PRESSURE TEST",
                _ => "HOLD THE LINE"
            };
        }

        private string BuildWaveCompositionLabel(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return "Enemies: fallback pressure";
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0)
                {
                    continue;
                }

                var label = GetEnemyDisplayName(group.enemyId);
                counts.TryGetValue(label, out var current);
                counts[label] = current + group.count;
            }

            if (counts.Count == 0)
            {
                return "Enemies: none declared";
            }

            var parts = new List<string>();
            foreach (var pair in counts)
            {
                parts.Add($"{pair.Key} x{pair.Value}");
                if (parts.Count >= 3)
                {
                    break;
                }
            }

            var suffix = counts.Count > parts.Count ? " +" : string.Empty;
            return $"Enemies: {string.Join("  ", parts)}{suffix}\n{BuildWaveCodexLabel(wave)}";
        }

        private string BuildCompactWaveCompositionLabel(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return TDLocalization.IsChinese ? "敌群  备用压力" : "ENEMIES  FALLBACK PRESSURE";
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0)
                {
                    continue;
                }

                var label = GetEnemyDisplayName(group.enemyId);
                counts.TryGetValue(label, out var current);
                counts[label] = current + group.count;
            }

            var parts = new List<string>(3);
            foreach (var pair in counts)
            {
                var label = TDLocalization.IsChinese
                    ? TDLocalization.LocalizeRuntimeString(pair.Key)
                    : pair.Key;
                parts.Add($"{label} x{pair.Value}");
                if (parts.Count >= 3)
                {
                    break;
                }
            }

            var suffix = counts.Count > parts.Count ? "  +" : string.Empty;
            if (parts.Count == 0)
            {
                return TDLocalization.IsChinese ? "敌群  无" : "ENEMIES  NONE";
            }

            return TDLocalization.IsChinese
                ? $"敌群  {string.Join("  ", parts)}{suffix}"
                : string.Join("  ", parts) + suffix;
        }

        private string BuildWaveCodexLabel(TDWaveDefinition wave)
        {
            var progress = $"{GetCodexDiscoveredCount()}/{Mathf.Max(1, GetCodexTotalCount())}";
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return $"Codex {progress}: fallback profile";
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newEntries = new List<string>();
            var knownCount = 0;
            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || string.IsNullOrWhiteSpace(group.enemyId) || !seen.Add(group.enemyId))
                {
                    continue;
                }

                if (_encounteredEnemyIds.Contains(group.enemyId))
                {
                    knownCount++;
                    continue;
                }

                newEntries.Add(BuildEnemyCodexEntryLabel(group.enemyId));
                if (newEntries.Count >= 2)
                {
                    break;
                }
            }

            if (newEntries.Count > 0)
            {
                var suffix = seen.Count > newEntries.Count ? " +" : string.Empty;
                return $"Codex {progress}: NEW {string.Join("  ", newEntries)}{suffix}";
            }

            return $"Codex {progress}: Known {Mathf.Max(knownCount, seen.Count)} profile{(Mathf.Max(knownCount, seen.Count) == 1 ? string.Empty : "s")}";
        }

        private string BuildEnemyCodexEntryLabel(string enemyId)
        {
            var label = GetEnemyDisplayName(enemyId);
            if (!_enemyCatalog.TryGetValue(enemyId, out var entry))
            {
                return label;
            }

            var tagSummary = BuildEnemyTagSummary(entry, 2);
            return string.IsNullOrWhiteSpace(tagSummary) ? label : $"{label} [{tagSummary}]";
        }

        private static string BuildEnemyTagSummary(TDEnemyCatalogEntry entry, int maxTags)
        {
            if (entry?.tags == null || entry.tags.Length == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(Mathf.Max(1, maxTags));
            for (var i = 0; i < entry.tags.Length && parts.Count < maxTags; i++)
            {
                var tag = entry.tags[i];
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    parts.Add(tag.Trim().ToLowerInvariant());
                }
            }

            return string.Join("/", parts);
        }

        private string BuildEnemyProfileLabel(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return "Profile: fallback pressure\nWeak: Rail coverage";
            }

            var totalCount = 0;
            var totalHp = 0;
            var weightedSpeed = 0f;
            var armorPressure = 0;

            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0 || !_enemyCatalog.TryGetValue(group.enemyId, out var entry))
                {
                    continue;
                }

                var count = Mathf.Max(0, group.count);
                totalCount += count;
                totalHp += Mathf.Max(1, entry.hp) * count;
                weightedSpeed += Mathf.Max(0.01f, entry.speed) * count;
                armorPressure += Mathf.Max(0, entry.armorFlat) * count;
            }

            var averageSpeed = totalCount > 0 ? weightedSpeed / totalCount : 0f;
            var tags = CollectWaveAndEnemyTags(wave);
            return $"Profile: HP {totalHp}  AvgSpd {averageSpeed:0.00}  Armor {armorPressure}\n{BuildResistanceWeaknessLabel(tags)}";
        }

        private string BuildCompactEnemyProfileLabel(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return TDLocalization.IsChinese ? "[标准]  备用敌群\n弱点  轨枪" : "[STD]  FALLBACK\nWEAK  RAIL";
            }

            var totalCount = 0;
            var totalHp = 0;
            var weightedSpeed = 0f;
            var armorPressure = 0;
            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0 || !_enemyCatalog.TryGetValue(group.enemyId, out var entry))
                {
                    continue;
                }

                var count = Mathf.Max(0, group.count);
                totalCount += count;
                totalHp += Mathf.Max(1, entry.hp) * count;
                weightedSpeed += Mathf.Max(0.01f, entry.speed) * count;
                armorPressure += Mathf.Max(0, entry.armorFlat) * count;
            }

            var averageSpeed = totalCount > 0 ? weightedSpeed / totalCount : 0f;
            var tags = CollectWaveAndEnemyTags(wave);
            if (TDLocalization.IsChinese)
            {
                return $"{TDLocalization.LocalizeRuntimeString(BuildThreatMarkLabel(tags))}  生命 {totalHp}  速度 {averageSpeed:0.0}  护甲 {armorPressure}\n" +
                       $"弱点  {TDLocalization.LocalizeRuntimeString(BuildCompactWeaknessLabel(tags))}";
            }

            return $"{BuildThreatMarkLabel(tags)}  HP {totalHp}  SPD {averageSpeed:0.0}  ARM {armorPressure}\n" +
                   $"WEAK  {BuildCompactWeaknessLabel(tags)}";
        }

        private string BuildCompactWeaknessLabel(HashSet<string> tags)
        {
            var weak = new List<string>(3);
            if (HasAnyTag(tags, "armored", "heavy", "boss", "elite", "durability"))
            {
                weak.Add("SIEGE/RAIL");
            }

            if (HasAnyTag(tags, "fast", "flank"))
            {
                weak.Add("FROST/FLAK");
            }

            if (HasAnyTag(tags, "swarm", "spawn", "split"))
            {
                weak.Add("MORTAR/ARC");
            }

            if (weak.Count == 0 && HasAnyTag(tags, "support", "attrition", "zone_control"))
            {
                weak.Add("BEACON/SNARE");
            }

            return weak.Count == 0 ? "RAIL" : string.Join("  ", weak.Take(2));
        }

        private string BuildResistanceWeaknessLabel(HashSet<string> tags)
        {
            var weak = new List<string>();

            if (HasAnyTag(tags, "armored", "heavy", "boss", "elite", "durability"))
            {
                weak.Add("Siege/Rail");
            }

            if (HasAnyTag(tags, "fast", "flank"))
            {
                weak.Add("Frost/Flak");
            }

            if (HasAnyTag(tags, "swarm", "spawn", "split"))
            {
                weak.Add("Mortar/Arc");
            }

            if (HasAnyTag(tags, "support", "attrition", "zone_control"))
            {
                weak.Add("Beacon/Snare");
            }

            if (weak.Count == 0)
            {
                weak.Add("Rail base");
            }

            return $"Marks: {BuildThreatMarkLabel(tags)}  Weak: {string.Join(", ", weak)}";
        }

        private string BuildThreatMarkLabel(HashSet<string> tags)
        {
            var marks = new List<string>(4);

            if (HasAnyTag(tags, "boss", "final", "elite"))
            {
                marks.Add("[ELT]");
            }

            if (HasAnyTag(tags, "armored", "heavy", "durability"))
            {
                marks.Add("[ARM]");
            }

            if (HasAnyTag(tags, "fast", "flank", "special"))
            {
                marks.Add("[SPD]");
            }

            if (HasAnyTag(tags, "swarm", "split", "spawn", "mixed"))
            {
                marks.Add("[SWM]");
            }

            if (HasAnyTag(tags, "support", "attrition", "zone_control"))
            {
                marks.Add("[SUP]");
            }

            return marks.Count == 0 ? "[STD]" : string.Join(" ", marks);
        }

        private string BuildWaveRouteLabel(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return TDLocalization.IsChinese ? "路线：默认路线" : "Routes: default lane";
            }

            var laneCounts = BuildWaveLanePressureMap(wave);
            if (laneCounts.Count == 0)
            {
                return TDLocalization.IsChinese ? "路线：默认路线" : "Routes: default lane";
            }

            var pairs = new List<KeyValuePair<string, int>>(laneCounts);
            pairs.Sort((a, b) =>
            {
                var delta = b.Value.CompareTo(a.Value);
                return delta != 0 ? delta : string.CompareOrdinal(a.Key, b.Key);
            });

            var labels = new List<string>();
            for (var i = 0; i < pairs.Count && i < 4; i++)
            {
                labels.Add(
                    TDLocalization.IsChinese
                        ? $"{GetLocalizedLaneLabel(pairs[i].Key)} x{pairs[i].Value}"
                        : $"{FormatLaneLabel(pairs[i].Key)} x{pairs[i].Value}");
            }

            return TDLocalization.IsChinese
                ? $"路线：{string.Join("  ", labels)}"
                : $"Routes: {string.Join("  ", labels)}";
        }

        private static string GetLocalizedWavePhaseLabel(string phase)
        {
            return NormalizeGroupToken(phase) switch
            {
                "introduce" => "引入",
                "practice" => "练习",
                "reinforce" => "强化",
                "synthesis" => "综合",
                "exam" => "考试",
                "finale" => "终局",
                "prep" => "备战",
                _ => TDLocalization.LocalizeRuntimeString(phase)
            };
        }

        private string BuildGroupRouteEventLabel(TDWaveGroup group, string formation)
        {
            if (group == null || group.count <= 0)
            {
                return string.Empty;
            }

            var lanes = ResolvePreviewLaneKeys(group);
            var laneLabels = new List<string>();
            for (var i = 0; i < lanes.Count && i < 3; i++)
            {
                laneLabels.Add(FormatLaneLabel(lanes[i]));
            }

            if (laneLabels.Count == 0)
            {
                laneLabels.Add("Main");
            }

            var enemyLabel = GetEnemyDisplayName(group.enemyId);
            var formationLabel = string.IsNullOrWhiteSpace(formation) ? "stream" : formation.Replace('_', ' ');
            return $"Route: {string.Join("/", laneLabels)} {formationLabel} - {enemyLabel} x{group.count}";
        }

        private Dictionary<string, int> BuildWaveLanePressureMap(TDWaveDefinition wave)
        {
            var laneCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (wave?.groups == null)
            {
                return laneCounts;
            }

            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0)
                {
                    continue;
                }

                var laneKeys = ResolvePreviewLaneKeys(group);
                if (laneKeys.Count == 0)
                {
                    AddLanePressure(laneCounts, "default", group.count);
                    continue;
                }

                for (var k = 0; k < laneKeys.Count; k++)
                {
                    AddLanePressure(laneCounts, laneKeys[k], group.count);
                }
            }

            return laneCounts;
        }

        private void AddLanePressure(Dictionary<string, int> laneCounts, string lane, int count)
        {
            var key = string.IsNullOrWhiteSpace(lane) ? "default" : lane.Trim().ToLowerInvariant();
            laneCounts.TryGetValue(key, out var current);
            laneCounts[key] = current + Mathf.Max(0, count);
        }

        private static int GetLanePressure(Dictionary<string, int> laneCounts, string lane)
        {
            if (laneCounts == null || laneCounts.Count == 0)
            {
                return 0;
            }

            var key = string.IsNullOrWhiteSpace(lane) ? "default" : lane.Trim().ToLowerInvariant();
            if (laneCounts.TryGetValue(key, out var pressure))
            {
                return pressure;
            }

            if (key == "default" && laneCounts.TryGetValue("center", out pressure))
            {
                return pressure;
            }

            if (key == "center" && laneCounts.TryGetValue("default", out pressure))
            {
                return pressure;
            }

            return 0;
        }

        private List<string> ResolvePreviewLaneKeys(TDWaveGroup group)
        {
            var lanes = new List<string>();
            if (group == null)
            {
                return lanes;
            }

            var formation = NormalizeGroupToken(group.formation);
            var lane = NormalizeGroupToken(group.lane);
            if (string.IsNullOrEmpty(lane))
            {
                lane = formation switch
                {
                    "split_lane" => "split_lane",
                    "cross_lane" => "cross_lane",
                    _ => "default"
                };
            }

            if (_activeScenarioMechanic != null &&
                NormalizeGroupToken(_activeScenarioMechanic.mechanicType) == "route_switch" &&
                !string.Equals(_scenarioRouteBias, "center", StringComparison.Ordinal) &&
                (lane == "default" || lane == "center" || lane == "all" || lane == "split_lane" || lane == "cross_lane"))
            {
                lane = _scenarioRouteBias;
            }

            if (lane == "all")
            {
                AddAvailablePreviewLane(lanes, "left");
                AddAvailablePreviewLane(lanes, "right");
                AddAvailablePreviewLane(lanes, "center");
                if (lanes.Count == 0)
                {
                    AddAvailablePreviewLane(lanes, "default");
                }

                return lanes;
            }

            if (lane == "split_lane")
            {
                AddAvailablePreviewLane(lanes, _activeLanePaths.ContainsKey("split_lane") ? "split_lane" : "left");
                AddAvailablePreviewLane(lanes, "right");
                return lanes;
            }

            if (lane == "cross_lane")
            {
                AddAvailablePreviewLane(lanes, _activeLanePaths.ContainsKey("cross_lane") ? "cross_lane" : "right");
                return lanes;
            }

            AddAvailablePreviewLane(lanes, lane);
            return lanes;
        }

        private void AddAvailablePreviewLane(List<string> lanes, string lane)
        {
            var key = string.IsNullOrWhiteSpace(lane) ? "default" : lane.Trim().ToLowerInvariant();
            if (!_activeLanePaths.ContainsKey(key) && key != "default")
            {
                return;
            }

            if (!lanes.Contains(key))
            {
                lanes.Add(key);
            }
        }

        private static string FormatLaneLabel(string lane)
        {
            return lane switch
            {
                "left" => "Left",
                "right" => "Right",
                "center" => "Center",
                "split_lane" => "Split",
                "cross_lane" => "Cross",
                "default" => "Main",
                _ => string.IsNullOrWhiteSpace(lane) ? "Main" : lane.Replace('_', ' ')
            };
        }

        private static string GetLocalizedLaneLabel(string lane)
        {
            return lane switch
            {
                "left" => "左路",
                "right" => "右路",
                "center" => "中路",
                "split_lane" => "分路",
                "cross_lane" => "交叉路",
                "switch" => "切换路",
                "default" => "主路",
                _ => string.IsNullOrWhiteSpace(lane) ? "主路" : lane.Replace('_', ' ')
            };
        }

        private string BuildCounterRecommendationLabel(TDWaveDefinition wave)
        {
            var tags = CollectWaveAndEnemyTags(wave);
            var picks = new List<string>();

            if (HasAnyTag(tags, "fast", "flank", "gap", "pressure"))
            {
                picks.Add("Frost/Flak vs speed");
            }

            if (HasAnyTag(tags, "swarm", "split", "mixed"))
            {
                picks.Add("Mortar/Arc for groups");
            }

            if (HasAnyTag(tags, "armored", "heavy", "durability", "boss"))
            {
                picks.Add("Rail/Siege for armor");
            }

            if (HasAnyTag(tags, "support", "attrition", "zone_control"))
            {
                picks.Add("Beacon/Snare control");
            }

            if (picks.Count == 0)
            {
                picks.Add("Rail coverage, then one control tower");
            }

            var tagLabel = tags.Count > 0 ? $"Traits: {BuildTagSummary(tags, 5)}" : "Traits: none";
            var matrixPicks = BuildSpecializationMatrixRecommendation(tags, 2);
            return string.IsNullOrWhiteSpace(matrixPicks)
                ? $"{tagLabel}\nCounter: {string.Join("  |  ", picks)}"
                : $"{tagLabel}\nMatrix: {matrixPicks}";
        }

        private string BuildSpecializationMatrixRecommendation(HashSet<string> threatTags, int maxResults)
        {
            if (threatTags == null || threatTags.Count == 0)
            {
                return string.Empty;
            }

            var definitions = new List<TDTowerSpecializationDefinition>();
            var all = TDTower.GetSpecializationDefinitions();
            for (var i = 0; i < all.Count; i++)
            {
                var definition = all[i];
                if (!_unlockedTowerKinds.Contains(definition.towerKind) || CountSpecializationTagMatches(definition, threatTags) <= 0)
                {
                    continue;
                }

                definitions.Add(definition);
            }

            definitions.Sort((a, b) =>
            {
                var delta = CountSpecializationTagMatches(b, threatTags).CompareTo(CountSpecializationTagMatches(a, threatTags));
                if (delta != 0)
                {
                    return delta;
                }

                delta = a.towerKind.CompareTo(b.towerKind);
                return delta != 0 ? delta : a.branch.CompareTo(b.branch);
            });

            var labels = new List<string>();
            var max = Mathf.Min(Mathf.Max(1, maxResults), definitions.Count);
            for (var i = 0; i < max; i++)
            {
                var definition = definitions[i];
                labels.Add($"{definition.displayName}[{TDTower.GetResonanceAffinityLabel(definition.resonanceAffinity)}]");
            }

            return string.Join(" | ", labels);
        }

        private static int CountSpecializationTagMatches(TDTowerSpecializationDefinition definition, HashSet<string> threatTags)
        {
            if (definition?.counterTags == null || threatTags == null)
            {
                return 0;
            }

            var matches = 0;
            for (var i = 0; i < definition.counterTags.Length; i++)
            {
                if (threatTags.Contains(definition.counterTags[i]))
                {
                    matches++;
                }
            }

            return matches;
        }

        private string BuildDefenseReadinessLabel(TDWaveDefinition wave)
        {
            var report = CalculateDefenseReadiness(wave);
            return $"Ready {report.score} {report.grade}  Cov {report.coverageScore}  Ctr {report.counterScore}  DPS {report.outputScore}\nPlan: {report.plan}";
        }

        private TDDefenseReadinessReport CalculateDefenseReadiness(TDWaveDefinition wave)
        {
            var towers = UnityEngine.Object.FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            if (towers == null || towers.Length == 0)
            {
                return new TDDefenseReadinessReport
                {
                    score = 0,
                    coverageScore = 0,
                    counterScore = 0,
                    outputScore = 0,
                    grade = "D",
                    plan = "Build first tower on the hottest route."
                };
            }

            var coverage = CalculateRouteCoverageScore(wave, towers);
            var counter = CalculateCounterScore(wave, towers);
            var output = CalculateOutputScore(wave, towers);
            var score = Mathf.RoundToInt((coverage * 0.36f) + (counter * 0.32f) + (output * 0.32f));
            return new TDDefenseReadinessReport
            {
                score = Mathf.Clamp(score, 0, 100),
                coverageScore = coverage,
                counterScore = counter,
                outputScore = output,
                grade = GetReadinessGrade(score),
                plan = BuildReadinessPlan(wave, towers, coverage, counter, output)
            };
        }

        private TDDefenseReadinessReport CaptureWaveStartReadiness()
        {
            var report = CalculateDefenseReadiness(_currentWaveDefinition);
            _lastWaveStartReadinessScore = report.score;
            _lastWaveStartReadinessGrade = report.grade;
            if (_currentWaveStat != null)
            {
                _currentWaveStat.readinessScore = report.score;
                _currentWaveStat.readinessGrade = report.grade;
            }

            return report;
        }

        private int CalculateRouteCoverageScore(TDWaveDefinition wave, TDTower[] towers)
        {
            if (towers == null || towers.Length == 0 || _activeLanePaths.Count == 0)
            {
                return 0;
            }

            var lanes = BuildWavePreviewLaneKeys(wave);
            if (lanes.Count == 0)
            {
                lanes.Add("default");
            }

            var lanePressure = BuildWaveLanePressureMap(wave);
            var weightedScore = 0f;
            var totalWeight = 0f;
            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                if (!_activeLanePaths.TryGetValue(lane, out var path) || path == null || path.Count <= 1)
                {
                    if (!_activeLanePaths.TryGetValue("default", out path) || path == null || path.Count <= 1)
                    {
                        continue;
                    }
                }

                var samples = 0;
                var covered = 0;
                for (var p = 0; p < path.Count - 1; p++)
                {
                    for (var s = 0; s < 3; s++)
                    {
                        var point = Vector3.Lerp(path[p], path[p + 1], s / 2f);
                        samples++;
                        if (IsRoutePointCoveredByTower(point, towers))
                        {
                            covered++;
                        }
                    }
                }

                if (samples <= 0)
                {
                    continue;
                }

                var weight = Mathf.Max(1, GetLanePressure(lanePressure, lane));
                weightedScore += (covered / (float)samples) * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt((weightedScore / totalWeight) * 100f), 0, 100);
        }

        private bool IsRoutePointCoveredByTower(Vector3 point, TDTower[] towers)
        {
            for (var i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                if (tower == null || tower.gameObject == null || tower.AttackRange <= 0f)
                {
                    continue;
                }

                var range = tower.AttackRange;
                if ((tower.transform.position - point).sqrMagnitude <= range * range)
                {
                    return true;
                }
            }

            return false;
        }

        private int CalculateCounterScore(TDWaveDefinition wave, TDTower[] towers)
        {
            if (towers == null || towers.Length == 0)
            {
                return 0;
            }

            var tags = CollectWaveAndEnemyTags(wave);
            var needScores = new List<int>(4);
            if (HasAnyTag(tags, "fast", "flank", "gap", "pressure"))
            {
                needScores.Add(ScoreCounterNeed(
                    towers,
                    new[] { TDTowerKind.FrostCoil, TDTowerKind.EmberFlak, TDTowerKind.GravSnare },
                    new[] { TDTowerKind.RailLancer, TDTowerKind.ArcWelder }));
            }

            if (HasAnyTag(tags, "swarm", "split", "mixed", "spawn"))
            {
                needScores.Add(ScoreCounterNeed(
                    towers,
                    new[] { TDTowerKind.CinderMortar, TDTowerKind.ArcWelder, TDTowerKind.EmberFlak, TDTowerKind.GravSnare },
                    new[] { TDTowerKind.RailLancer, TDTowerKind.FrostCoil }));
            }

            if (HasAnyTag(tags, "armored", "heavy", "durability", "boss", "elite"))
            {
                needScores.Add(ScoreCounterNeed(
                    towers,
                    new[] { TDTowerKind.RailLancer, TDTowerKind.SiegeDrill },
                    new[] { TDTowerKind.ArcWelder, TDTowerKind.ResonanceBeacon }));
            }

            if (HasAnyTag(tags, "support", "attrition", "zone_control"))
            {
                needScores.Add(ScoreCounterNeed(
                    towers,
                    new[] { TDTowerKind.ResonanceBeacon, TDTowerKind.GravSnare, TDTowerKind.FrostCoil },
                    new[] { TDTowerKind.CinderMortar, TDTowerKind.ArcWelder }));
            }

            if (needScores.Count == 0)
            {
                return Mathf.Clamp(55 + (CountLiveTowers(towers) * 10), 55, 85);
            }

            var total = 0;
            for (var i = 0; i < needScores.Count; i++)
            {
                total += needScores[i];
            }

            return Mathf.Clamp(Mathf.RoundToInt(total / (float)needScores.Count), 0, 100);
        }

        private static int ScoreCounterNeed(TDTower[] towers, TDTowerKind[] exactCounters, TDTowerKind[] fallbackCounters)
        {
            if (HasAnyTowerKind(towers, exactCounters))
            {
                return 100;
            }

            return HasAnyTowerKind(towers, fallbackCounters) ? 45 : 0;
        }

        private int CalculateOutputScore(TDWaveDefinition wave, TDTower[] towers)
        {
            if (towers == null || towers.Length == 0)
            {
                return 0;
            }

            var waveHp = EstimateWaveEffectiveHp(wave);
            var towerOutput = 0f;
            for (var i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                if (tower == null || tower.gameObject == null)
                {
                    continue;
                }

                var areaFactor = tower.AoeRadius > 0f ? Mathf.Lerp(1.12f, 1.45f, Mathf.Clamp01(tower.AoeRadius / 1.6f)) : 1f;
                var targetFactor = tower.AoeMaxTargets > 1 ? Mathf.Lerp(1f, 1.34f, Mathf.Clamp01((tower.AoeMaxTargets - 1) / 5f)) : 1f;
                var controlFactor = 1f + (tower.SlowPct * 0.28f) + (tower.SlowDuration > 0f ? 0.06f : 0f);
                towerOutput += Mathf.Max(0, tower.Damage) * Mathf.Max(0.01f, tower.ShotsPerSecond) * areaFactor * targetFactor * controlFactor;
            }

            var targetDps = Mathf.Max(12f, waveHp / 12f);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(towerOutput / targetDps) * 100f), 0, 100);
        }

        private int EstimateWaveEffectiveHp(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return 120 + Mathf.Max(0, _wave * 18);
            }

            var total = 0;
            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0)
                {
                    continue;
                }

                if (_enemyCatalog.TryGetValue(group.enemyId, out var entry))
                {
                    var hp = Mathf.Max(1, entry.hp) + (Mathf.Max(0, entry.armorFlat) * 6);
                    total += hp * Mathf.Max(1, group.count);
                }
                else
                {
                    total += 35 * Mathf.Max(1, group.count);
                }
            }

            return Mathf.Max(60, total);
        }

        private string BuildReadinessPlan(TDWaveDefinition wave, TDTower[] towers, int coverage, int counter, int output)
        {
            var tags = CollectWaveAndEnemyTags(wave);
            if (towers == null || CountLiveTowers(towers) == 0)
            {
                return "Build first tower near the hot route.";
            }

            if (coverage < 58)
            {
                return $"Cover {GetHottestLaneLabel(wave)} with range/slow.";
            }

            if (counter < 58)
            {
                return BuildCounterPlan(tags);
            }

            if (output < 58)
            {
                return "Add Damage branch or Rail/Siege output.";
            }

            if (HasUpgradeableTower(towers) && _defenseBudget >= 40)
            {
                return "Buy a 2-branch spec before dispatch.";
            }

            return "Ready to start; watch split events.";
        }

        private string BuildCounterPlan(HashSet<string> tags)
        {
            if (HasAnyTag(tags, "fast", "flank", "gap", "pressure"))
            {
                return "Add Frost/Flak for speed control.";
            }

            if (HasAnyTag(tags, "swarm", "split", "mixed", "spawn"))
            {
                return "Add Mortar/Arc for group damage.";
            }

            if (HasAnyTag(tags, "armored", "heavy", "durability", "boss", "elite"))
            {
                return "Add Rail/Siege for armor pressure.";
            }

            if (HasAnyTag(tags, "support", "attrition", "zone_control"))
            {
                return "Add Beacon/Snare against attrition.";
            }

            return "Mix one damage and one control tower.";
        }

        private string GetHottestLaneLabel(TDWaveDefinition wave)
        {
            var lanePressure = BuildWaveLanePressureMap(wave);
            if (lanePressure.Count == 0)
            {
                return "Main";
            }

            var bestLane = "default";
            var bestPressure = int.MinValue;
            foreach (var pair in lanePressure)
            {
                if (pair.Value > bestPressure)
                {
                    bestLane = pair.Key;
                    bestPressure = pair.Value;
                }
            }

            return FormatLaneLabel(bestLane);
        }

        private static string GetReadinessGrade(int score)
        {
            if (score >= 85)
            {
                return "S";
            }

            if (score >= 70)
            {
                return "A";
            }

            if (score >= 55)
            {
                return "B";
            }

            return score >= 40 ? "C" : "D";
        }

        private static bool HasAnyTowerKind(TDTower[] towers, TDTowerKind[] kinds)
        {
            if (towers == null || kinds == null)
            {
                return false;
            }

            for (var i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                if (tower == null || tower.gameObject == null)
                {
                    continue;
                }

                for (var k = 0; k < kinds.Length; k++)
                {
                    if (tower.Kind == kinds[k])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CountLiveTowers(TDTower[] towers)
        {
            if (towers == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < towers.Length; i++)
            {
                if (towers[i] != null && towers[i].gameObject != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasUpgradeableTower(TDTower[] towers)
        {
            if (towers == null)
            {
                return false;
            }

            for (var i = 0; i < towers.Length; i++)
            {
                if (towers[i] != null && towers[i].gameObject != null && towers[i].CanUpgrade)
                {
                    return true;
                }
            }

            return false;
        }

        private HashSet<string> CollectWaveAndEnemyTags(TDWaveDefinition wave)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (wave?.threatTags != null)
            {
                for (var i = 0; i < wave.threatTags.Length; i++)
                {
                    AddNormalizedTag(tags, wave.threatTags[i]);
                }
            }

            if (wave?.groups == null)
            {
                return tags;
            }

            for (var g = 0; g < wave.groups.Length; g++)
            {
                var group = wave.groups[g];
                if (group == null || !_enemyCatalog.TryGetValue(group.enemyId, out var entry) || entry.tags == null)
                {
                    continue;
                }

                for (var t = 0; t < entry.tags.Length; t++)
                {
                    AddNormalizedTag(tags, entry.tags[t]);
                }
            }

            return tags;
        }

        private static void AddNormalizedTag(HashSet<string> tags, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                tags.Add(value.Trim().ToLowerInvariant());
            }
        }

        private static bool HasAnyTag(HashSet<string> tags, params string[] candidates)
        {
            if (tags == null || candidates == null)
            {
                return false;
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                if (tags.Contains(candidates[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildTagSummary(HashSet<string> tags, int maxTags)
        {
            var parts = new List<string>();
            foreach (var tag in tags)
            {
                parts.Add(tag);
                if (parts.Count >= maxTags)
                {
                    break;
                }
            }

            return string.Join(", ", parts);
        }

        private string BuildTowerStatsLabel(TDTower tower)
        {
            if (tower == null)
            {
                return string.Empty;
            }

            var aoeLabel = tower.AoeRadius > 0f ? $"{tower.AoeRadius:0.0}/{tower.AoeMaxTargets}" : "-";
            var slowLabel = tower.SlowPct > 0f ? $"{tower.SlowPct * 100f:0}% {tower.SlowDuration:0.0}s" : "-";
            if (Screen.height <= 600)
            {
                return $"DMG {tower.Damage}   RNG {tower.AttackRange:0.0}   RATE {tower.ShotsPerSecond:0.00}/s\n" +
                       $"AOE {aoeLabel}   SLOW {slowLabel}   SPEC {tower.SpecializationLabel}";
            }

            return $"DMG {tower.Damage}    RNG {tower.AttackRange:0.0}    RATE {tower.ShotsPerSecond:0.00}/s\n" +
                   $"AOE {aoeLabel}    SLOW {slowLabel}    HEAVY x{tower.HeavyMultiplier:0.00}\n" +
                   $"SPEC {tower.SpecializationLabel}    D{tower.DamageBranchCount}/U{tower.UtilityBranchCount}";
        }

        private string BuildTowerUpgradePreviewLabel(TDTower tower)
        {
            if (tower == null)
            {
                return string.Empty;
            }

            if (!tower.CanUpgrade)
            {
                return $"Ultimate: {tower.SpecializationLabel}\n{tower.SpecializationEffectLabel}";
            }

            var damageDefinition = TDTower.GetSpecializationDefinition(tower.Kind, TDTowerUpgradeBranch.Damage);
            var utilityDefinition = TDTower.GetSpecializationDefinition(tower.Kind, TDTowerUpgradeBranch.Utility);
            var damage = tower.GetUpgradeStatDeltaSummary(TDTowerUpgradeBranch.Damage);
            var utility = tower.GetUpgradeStatDeltaSummary(TDTowerUpgradeBranch.Utility);
            return $"D {damageDefinition?.displayName ?? "Damage"} [{TDTower.GetResonanceAffinityLabel(damageDefinition?.resonanceAffinity ?? TDResonanceAffinity.EmberSurge)}]: {damage}\n" +
                   $"U {utilityDefinition?.displayName ?? "Utility"} [{TDTower.GetResonanceAffinityLabel(utilityDefinition?.resonanceAffinity ?? TDResonanceAffinity.FractureMark)}]: {utility}";
        }

        private static string BuildUpgradeButtonPreview(TDTower tower, TDTowerUpgradeBranch branch)
        {
            if (tower == null || !tower.CanUpgrade)
            {
                return "MAX";
            }

            var branchCount = branch == TDTowerUpgradeBranch.Damage ? tower.DamageBranchCount : tower.UtilityBranchCount;
            if (branchCount == 1)
            {
                return TDTower.GetSpecializationDefinition(tower.Kind, branch)?.displayName ?? "Ultimate";
            }

            return tower.GetUpgradeStatDeltaSummary(branch);
        }

        private static string BuildTowerMatrixHint(TDTower tower)
        {
            if (tower == null)
            {
                return string.Empty;
            }

            var damage = TDTower.GetSpecializationDefinition(tower.Kind, TDTowerUpgradeBranch.Damage);
            var utility = TDTower.GetSpecializationDefinition(tower.Kind, TDTowerUpgradeBranch.Utility);
            var damageTags = damage?.counterTags == null ? "-" : string.Join("/", damage.counterTags);
            var utilityTags = utility?.counterTags == null ? "-" : string.Join("/", utility.counterTags);
            return $"Matrix D {damageTags} > {TDTower.GetResonanceAffinityLabel(damage?.resonanceAffinity ?? TDResonanceAffinity.EmberSurge)}\n" +
                   $"Matrix U {utilityTags} > {TDTower.GetResonanceAffinityLabel(utility?.resonanceAffinity ?? TDResonanceAffinity.FractureMark)}";
        }

        private TDRunScoreReport CalculateRunScore()
        {
            var report = new TDRunScoreReport
            {
                coverage = CalculateRunCoverageScore(),
                counterMatch = CalculateRunCounterScore(),
                output = CalculateRunOutputScore(),
                economy = CalculateRunEconomyScore(),
                command = CalculateRunCommandScore()
            };
            var rawTotal = Mathf.Clamp(Mathf.RoundToInt(
                (report.coverage + report.counterMatch + report.output + report.economy + report.command) / 5f), 0, 100);
            report.total = ApplyRunSurvivalScoreCap(
                rawTotal,
                _gameOver,
                _victory,
                _startingLineIntegrity,
                _lineIntegrity,
                _totalIntegrityDamageTaken);
            report.grade = GetRunScoreGrade(report.total);
            return report;
        }

        private static int ApplyRunSurvivalScoreCap(
            int rawScore,
            bool gameOver,
            bool victory,
            int startingIntegrity,
            int remainingIntegrity,
            int integrityDamageTaken)
        {
            var score = Mathf.Clamp(rawScore, 0, 100);
            if (!gameOver)
            {
                return score;
            }

            if (!victory)
            {
                return Mathf.Min(score, 59);
            }

            var safeStartingIntegrity = Mathf.Max(1, startingIntegrity);
            var finalRetention = Mathf.Clamp01(remainingIntegrity / (float)safeStartingIntegrity);
            var pressureRetention = Mathf.Clamp01(1f - (Mathf.Max(0, integrityDamageTaken) / (float)safeStartingIntegrity));
            var survivalQuality = Mathf.Min(finalRetention, pressureRetention);
            if (survivalQuality < 0.25f)
            {
                return Mathf.Min(score, 69);
            }

            if (survivalQuality < 0.50f)
            {
                return Mathf.Min(score, 79);
            }

            if (survivalQuality < 0.80f)
            {
                return Mathf.Min(score, 89);
            }

            return score;
        }

        private int CalculateRunCoverageScore()
        {
            var totalSpawned = 0;
            var totalKills = 0;
            var weakestLaneClear = 100f;
            var laneCount = 0;
            foreach (var pair in _laneStats)
            {
                var stat = pair.Value;
                if (stat == null || stat.spawned <= 0)
                {
                    continue;
                }

                totalSpawned += stat.spawned;
                totalKills += stat.kills;
                weakestLaneClear = Mathf.Min(weakestLaneClear, stat.kills / (float)stat.spawned * 100f);
                laneCount++;
            }

            if (totalSpawned <= 0 || laneCount <= 0)
            {
                return 0;
            }

            var overallClear = totalKills / (float)totalSpawned * 100f;
            var integrityRetention = Mathf.Clamp01(_lineIntegrity / (float)Mathf.Max(1, _startingLineIntegrity)) * 100f;
            return Mathf.Clamp(Mathf.RoundToInt(
                (overallClear * 0.35f) + (weakestLaneClear * 0.20f) + (integrityRetention * 0.45f)), 0, 100);
        }

        private int CalculateRunCounterScore()
        {
            if (GetTotalLaneSpawned() <= 0)
            {
                return 0;
            }

            var actionableDamage = 0;
            var matchedActionableDamage = 0;
            foreach (var pair in _threatCategoryDamage)
            {
                if (!IsCounterCategoryActionable(pair.Key))
                {
                    continue;
                }

                actionableDamage += Mathf.Max(0, pair.Value);
                if (_threatCategoryCounterDamage.TryGetValue(pair.Key, out var matched))
                {
                    matchedActionableDamage += Mathf.Max(0, matched);
                }
            }

            if (actionableDamage <= 0)
            {
                return 100;
            }

            var matchRate = Mathf.Clamp01(matchedActionableDamage / (float)actionableDamage);
            return Mathf.Clamp(Mathf.RoundToInt(20f + (matchRate * 80f)), 0, 100);
        }

        private int CalculateRunOutputScore()
        {
            var totalSpawned = GetTotalLaneSpawned();
            var totalSpawnedHealth = GetTotalLaneSpawnedHealth();
            if (totalSpawned <= 0 || totalSpawnedHealth <= 0)
            {
                return 0;
            }

            var killRate = Mathf.Clamp01(GetTotalLaneKills() / (float)totalSpawned);
            var damageCompletion = Mathf.Clamp01(_totalDamageDealt / (float)totalSpawnedHealth);
            return Mathf.Clamp(Mathf.RoundToInt(((damageCompletion * 0.55f) + (killRate * 0.45f)) * 100f), 0, 100);
        }

        private int CalculateRunEconomyScore()
        {
            var totalSpend = 0;
            var engagedSpend = 0;
            foreach (var pair in _towerStats)
            {
                var stat = pair.Value;
                if (stat == null)
                {
                    continue;
                }

                totalSpend += stat.TotalSpend;
                if (stat.damageDealt > 0 || stat.controlApplications > 0 || stat.utilitySpecProcs > 0)
                {
                    engagedSpend += stat.TotalSpend;
                }
            }

            if (totalSpend <= 0)
            {
                return 0;
            }

            const float targetDamagePerBudget = 6f;
            var efficiency = Mathf.Clamp01(_totalDamageDealt / Mathf.Max(1f, totalSpend * targetDamagePerBudget));
            var utilization = Mathf.Clamp01(engagedSpend / (float)totalSpend);
            var upgradeConversion = _budgetSpentOnUpgrades <= 0
                ? 0f
                : Mathf.Clamp01(_budgetSpentOnUpgrades / Mathf.Max(1f, totalSpend * 0.35f));
            return Mathf.Clamp(Mathf.RoundToInt(
                ((efficiency * 0.55f) + (utilization * 0.30f) + (upgradeConversion * 0.15f)) * 100f), 0, 100);
        }

        private int CalculateRunCommandScore()
        {
            if (GetTotalLaneSpawned() <= 0)
            {
                return 0;
            }

            if (!_isResonanceSystemEnabled)
            {
                return 100;
            }

            if (_resonanceWindowsTriggered <= 0)
            {
                return 60;
            }

            var useRate = Mathf.Clamp01(_resonanceCommandsUsed / (float)_resonanceWindowsTriggered);
            var matchRate = _resonanceCommandsUsed <= 0
                ? 0f
                : Mathf.Clamp01(_resonanceMatchedCommands / (float)_resonanceCommandsUsed);
            var bonusImpact = _totalDamageDealt <= 0
                ? 0f
                : Mathf.Clamp01(_resonanceBonusDamage / Mathf.Max(1f, _totalDamageDealt * 0.12f));
            return Mathf.Clamp(Mathf.RoundToInt(
                ((useRate * 0.45f) + (matchRate * 0.35f) + (bonusImpact * 0.20f)) * 100f), 0, 100);
        }

        private int GetTotalLaneSpawned()
        {
            var total = 0;
            foreach (var pair in _laneStats)
            {
                total += Mathf.Max(0, pair.Value?.spawned ?? 0);
            }

            return total;
        }

        private int GetTotalLaneSpawnedHealth()
        {
            var total = 0;
            foreach (var pair in _laneStats)
            {
                total += Mathf.Max(0, pair.Value?.spawnedHealth ?? 0);
            }

            return total;
        }

        private int GetTotalLaneKills()
        {
            var total = 0;
            foreach (var pair in _laneStats)
            {
                total += Mathf.Max(0, pair.Value?.kills ?? 0);
            }

            return total;
        }

        private static string GetRunScoreGrade(int score)
        {
            if (score >= 90)
            {
                return "S";
            }

            if (score >= 80)
            {
                return "A";
            }

            if (score >= 70)
            {
                return "B";
            }

            if (score >= 60)
            {
                return "C";
            }

            return score >= 45 ? "D" : "F";
        }

        private int CalculateCurrentMissionStars()
        {
            if (!_victory)
            {
                return 0;
            }

            var stars = 1;
            if (_lineIntegrity >= GetMissionIntegrityStarThreshold())
            {
                stars++;
            }

            if (CalculateRunScore().total >= MissionTacticalStarThreshold)
            {
                stars++;
            }

            return Mathf.Clamp(stars, 1, 3);
        }

        private void RecordCampaignResultIfNeeded()
        {
            if (_campaignResultRecorded)
            {
                return;
            }

            _campaignResultRecorded = true;
            _currentMissionStars = CalculateCurrentMissionStars();
            if (_campaignRoute?.level == null)
            {
                return;
            }

            var score = CalculateRunScore();
            var contract = EvaluateCurrentMissionContract();
            _currentMissionContractCompleted = contract?.completed ?? false;
            _campaignProgressUpdate = TDCampaignProgression.RecordResult(
                _campaignRoute.level.levelIndex,
                _victory,
                _currentMissionStars,
                score.total,
                _lineIntegrity,
                _campaignRoute.totalLevels,
                _currentMissionContractCompleted,
                _activeCampaignDifficulty);
            _newlyClaimedChapterReward = TryAutoClaimCompletedChapterReward();
            RefreshMetaProgressionRewards(true);
            _missionBoardNeedsRefresh = true;
            var summary = GetCampaignProgressSummary();
            Debug.Log(
                $"[TD][CampaignProgress] level={_campaignRoute.level.levelIndex} victory={_victory} stars={_currentMissionStars} " +
                $"bestStars={_campaignProgressUpdate.bestStars} score={score.total} bestScore={_campaignProgressUpdate.bestTacticalScore} " +
                $"firstClear={_campaignProgressUpdate.firstClear} nextUnlocked={_campaignProgressUpdate.nextLevelUnlocked} " +
                $"contract={_currentMissionContractCompleted} firstContract={_campaignProgressUpdate.firstContractCompletion} " +
                $"cleared={summary.clearedLevels}/{summary.totalLevels} totalStars={summary.earnedStars}/{summary.availableStars} " +
                $"contracts={summary.completedContracts}/{summary.availableContracts} frontier={summary.highestUnlockedLevel} " +
                $"difficulty={_activeCampaignDifficulty} bestDifficulty={_campaignProgressUpdate.highestDifficultyCleared} " +
                $"chapterReward={_newlyClaimedChapterReward?.rewardId ?? "none"}");
        }

        private TDCampaignChapterRewardDefinition TryAutoClaimCompletedChapterReward()
        {
            if (!_victory || _campaignRoute?.level == null)
            {
                return null;
            }

            var chapter = GetCampaignChapter(_campaignRoute.level.chapterId);
            var chapterProgress = TDCampaignProgression.BuildChapterSummary(chapter);
            var reward = chapter?.reward;
            if (reward == null || !chapterProgress.cleared || chapterProgress.rewardClaimed ||
                !TDCampaignProgression.ClaimChapterReward(reward.rewardId))
            {
                return null;
            }

            PushTacticalEvent($"Chapter reward secured: {reward.displayName}", 6.4f);
            return reward;
        }

        private string BuildRunScoreHeaderLabel()
        {
            var score = CalculateRunScore();
            var contract = EvaluateCurrentMissionContract();
            if (TDLocalization.IsChinese)
            {
                var localizedContractState = contract?.contract == null
                    ? "无契约"
                    : contract.completed ? "契约达成" : "契约未达成";
                return $"战术 {score.total}  评级 {score.grade}     {TDLocalization.LocalizeRuntimeString(GetDifficultyShortLabel(_activeCampaignDifficulty))}     {localizedContractState}";
            }

            var contractState = contract?.contract == null
                ? "NO CONTRACT"
                : contract.completed ? "CONTRACT SECURED" : "CONTRACT MISSED";
            return $"TACTICAL {score.total}  GRADE {score.grade}     {GetDifficultyShortLabel(_activeCampaignDifficulty)}     {contractState}";
        }

        private void SetRunResultChartsVisible(bool visible)
        {
            _uiGameOverScoreChartRoot?.gameObject.SetActive(visible);
            _uiGameOverLaneChartRoot?.gameObject.SetActive(visible);
            _uiGameOverTowerChartRoot?.gameObject.SetActive(visible);
            if (visible)
            {
                SetRunResultTextSize(_uiGameOverBodyText, 12);
                SetRunResultTextSize(_uiGameOverScoreText, 14);
                SetRunResultTextSize(_uiGameOverLaneText, 12);
                SetRunResultTextSize(_uiGameOverTowerText, 12);
                SetRunResultTextSize(_uiGameOverHeatText, 12);
                SetRunResultTextSize(_uiGameOverFailureText, 12);
                SetRunResultTextSize(_uiGameOverRecapText, 12);
                SetRunResultTextSize(_uiGameOverRecommendationText, 12);
                SetRunResultTextRect(_uiGameOverBodyText, new Vector2(28f, -74f), new Vector2(704f, 20f));
                SetRunResultTextRect(_uiGameOverScoreText, new Vector2(28f, -98f), new Vector2(704f, 22f));
                SetRunResultTextRect(_uiGameOverLaneText, new Vector2(28f, -184f), new Vector2(338f, 18f));
                SetRunResultTextRect(_uiGameOverTowerText, new Vector2(394f, -184f), new Vector2(338f, 18f));
                SetRunResultTextRect(_uiGameOverHeatText, new Vector2(28f, -296f), new Vector2(704f, 44f));
                SetRunResultTextRect(_uiGameOverFailureText, new Vector2(28f, -344f), new Vector2(704f, 26f));
                SetRunResultTextRect(_uiGameOverRecapText, new Vector2(28f, -374f), new Vector2(704f, 50f));
                SetRunResultTextRect(_uiGameOverRecommendationText, new Vector2(28f, -430f), new Vector2(704f, 92f));
                return;
            }

            SetRunResultTextSize(_uiGameOverBodyText, 12);
            SetRunResultTextSize(_uiGameOverScoreText, 14);
            SetRunResultTextSize(_uiGameOverLaneText, 11);
            SetRunResultTextSize(_uiGameOverTowerText, 11);
            SetRunResultTextSize(_uiGameOverHeatText, 11);
            SetRunResultTextSize(_uiGameOverFailureText, 11);
            SetRunResultTextSize(_uiGameOverRecapText, 11);
            SetRunResultTextSize(_uiGameOverRecommendationText, 11);
            SetRunResultTextRect(_uiGameOverBodyText, new Vector2(28f, -52f), new Vector2(704f, 42f));
            SetRunResultTextRect(_uiGameOverScoreText, new Vector2(28f, -100f), new Vector2(704f, 70f));
            SetRunResultTextRect(_uiGameOverLaneText, new Vector2(28f, -178f), new Vector2(338f, 104f));
            SetRunResultTextRect(_uiGameOverTowerText, new Vector2(394f, -178f), new Vector2(338f, 104f));
            SetRunResultTextRect(_uiGameOverHeatText, new Vector2(28f, -290f), new Vector2(704f, 58f));
            SetRunResultTextRect(_uiGameOverFailureText, new Vector2(28f, -352f), new Vector2(704f, 30f));
            SetRunResultTextRect(_uiGameOverRecapText, new Vector2(28f, -388f), new Vector2(704f, 54f));
            SetRunResultTextRect(_uiGameOverRecommendationText, new Vector2(28f, -448f), new Vector2(704f, 78f));
        }

        private static void SetRunResultTextRect(Text text, Vector2 topLeft, Vector2 size)
        {
            if (text == null)
            {
                return;
            }

            text.rectTransform.anchoredPosition = topLeft;
            text.rectTransform.sizeDelta = size;
        }

        private static void SetRunResultTextSize(Text text, int fontSize)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.resizeTextMinSize = Mathf.Max(9, fontSize - 2);
            text.resizeTextMaxSize = fontSize;
        }

        private void UpdateRunResultCharts()
        {
            var score = CalculateRunScore();
            var scoreValues = new[] { score.coverage, score.counterMatch, score.output, score.economy, score.command };
            for (var i = 0; i < _uiGameOverScoreBarFills.Count && i < scoreValues.Length; i++)
            {
                SetRunResultBar(_uiGameOverScoreBarFills[i], scoreValues[i], 100f, 128f);
                SetUiText(_uiGameOverScoreBarValues[i], scoreValues[i].ToString());
            }

            var lanes = _laneStats.Values
                .Where(lane => lane != null && lane.spawned > 0)
                .OrderByDescending(lane => lane.spawned)
                .ThenBy(lane => lane.laneKey)
                .Take(_uiGameOverLaneBarRows.Count)
                .ToArray();
            for (var i = 0; i < _uiGameOverLaneBarRows.Count; i++)
            {
                var visible = i < lanes.Length;
                _uiGameOverLaneBarRows[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var lane = lanes[i];
                var clearPct = Mathf.RoundToInt(lane.kills / Mathf.Max(1f, lane.spawned) * 100f);
                var color = lane.escapes > 0
                    ? new Color(1f, 0.48f, 0.22f, 1f)
                    : new Color(0.28f, 0.82f, 1f, 1f);
                SetRunResultBar(_uiGameOverLaneBarFills[i], clearPct, 100f, 194f, color);
                _uiGameOverLaneBarLabels[i].color = color;
                SetUiText(
                    _uiGameOverLaneBarLabels[i],
                    TDLocalization.IsChinese
                        ? GetLocalizedLaneLabel(lane.laneKey)
                        : FormatLaneLabel(lane.laneKey).ToUpperInvariant());
                _uiGameOverLaneBarValues[i].color = color;
                SetUiText(
                    _uiGameOverLaneBarValues[i],
                    TDLocalization.IsChinese
                        ? $"{lane.kills}/{lane.spawned}  漏{lane.escapes}"
                        : $"{lane.kills}/{lane.spawned}  L{lane.escapes}");
            }

            var towers = GetSortedTowerStats().Take(_uiGameOverTowerBarRows.Count).ToArray();
            for (var i = 0; i < _uiGameOverTowerBarRows.Count; i++)
            {
                var visible = i < towers.Length;
                _uiGameOverTowerBarRows[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var tower = towers[i];
                var share = _totalDamageDealt <= 0
                    ? 0
                    : Mathf.RoundToInt(tower.damageDealt / (float)_totalDamageDealt * 100f);
                var color = i == 0
                    ? new Color(0.98f, 0.78f, 0.28f, 1f)
                    : new Color(0.34f, 0.90f, 0.58f, 1f);
                SetRunResultBar(_uiGameOverTowerBarFills[i], share, 100f, 194f, color);
                _uiGameOverTowerBarLabels[i].color = color;
                SetUiText(
                    _uiGameOverTowerBarLabels[i],
                    TDLocalization.IsChinese
                        ? GetLocalizedCompactTowerLabel(tower.kind)
                        : GetCompactTowerLabel(tower.kind).ToUpperInvariant());
                _uiGameOverTowerBarValues[i].color = color;
                SetUiText(
                    _uiGameOverTowerBarValues[i],
                    $"{share}% / {tower.kills}");
            }
        }

        private static void SetRunResultBar(Image fill, float value, float maximum, float width, Color? color = null)
        {
            if (fill == null)
            {
                return;
            }

            var ratio = Mathf.Clamp01(value / Mathf.Max(1f, maximum));
            fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, width * ratio), fill.rectTransform.sizeDelta.y);
            if (color.HasValue)
            {
                fill.color = color.Value;
            }
        }

        private string BuildRunScoreLabel()
        {
            var score = CalculateRunScore();
            var contract = EvaluateCurrentMissionContract();
            var contractLabel = contract?.contract == null
                ? "CONTRACT  None"
                : $"CONTRACT  {(contract.completed ? "SECURED" : "MISSED")}  {contract.contract.displayName}  " +
                  $"{GetContractMetricLabel(contract.contract.metric)} {contract.currentValue}/{contract.contract.target}";
            return $"TACTICAL SCORE {score.total}  GRADE {score.grade}\n" +
                   $"Coverage {score.coverage}   Counter {score.counterMatch}   Output {score.output}   Economy {score.economy}   Command {score.command}\n" +
                   $"DIFFICULTY  {GetDifficultyShortLabel(_activeCampaignDifficulty)}\n" +
                   contractLabel;
        }

        private string BuildLaneBreakdownLabel()
        {
            var lanes = new List<TDLaneRuntimeStat>();
            foreach (var pair in _laneStats)
            {
                if (pair.Value != null && pair.Value.spawned > 0)
                {
                    lanes.Add(pair.Value);
                }
            }

            lanes.Sort((a, b) =>
            {
                var delta = b.spawned.CompareTo(a.spawned);
                return delta != 0 ? delta : string.CompareOrdinal(a.laneKey, b.laneKey);
            });

            if (lanes.Count == 0)
            {
                return "LANE PERFORMANCE\nNo enemies deployed.";
            }

            var labels = new List<string> { "LANE PERFORMANCE  Killed / Spawned" };
            var max = Mathf.Min(4, lanes.Count);
            for (var i = 0; i < max; i++)
            {
                var lane = lanes[i];
                var clearPct = Mathf.RoundToInt(lane.kills / Mathf.Max(1f, lane.spawned) * 100f);
                labels.Add($"{FormatLaneLabel(lane.laneKey),-7} {lane.kills}/{lane.spawned}  Leak {lane.escapes}  Dmg {lane.damageDealt}  {clearPct}%");
            }

            if (lanes.Count > max)
            {
                labels.Add($"+{lanes.Count - max} more lanes in MCP report");
            }

            return string.Join("\n", labels);
        }

        private string BuildTowerContributionLabel()
        {
            var towers = GetSortedTowerStats();
            if (towers.Count == 0)
            {
                return "TOWER CONTRIBUTION\nNo towers built.";
            }

            var labels = new List<string> { "TOWER CONTRIBUTION  Damage Share" };
            var max = Mathf.Min(4, towers.Count);
            for (var i = 0; i < max; i++)
            {
                var tower = towers[i];
                var share = _totalDamageDealt <= 0 ? 0 : Mathf.RoundToInt(tower.damageDealt / (float)_totalDamageDealt * 100f);
                var ultimateProcs = tower.damageSpecProcs + tower.utilitySpecProcs;
                labels.Add($"{i + 1} {GetCompactTowerLabel(tower.kind)} @{tower.cell.x},{tower.cell.y} D{tower.damageDealt} K{tower.kills} C{tower.controlApplications} U{ultimateProcs} M{tower.matrixFullMatches} {share}%");
            }

            if (towers.Count > max)
            {
                labels.Add($"+{towers.Count - max} more towers in MCP report");
            }

            return string.Join("\n", labels);
        }

        private string BuildRoadHeatLabel()
        {
            var reports = BuildRoadHeatReports();
            if (reports.Count == 0)
            {
                return TDLocalization.IsChinese
                    ? "道路热区\n未记录路线压力。"
                    : "ROAD HEAT\nNo route pressure recorded.";
            }

            var labels = new List<string>();
            var firstLine = new List<string> { TDLocalization.IsChinese ? "道路热区" : "ROAD HEAT" };
            var max = Mathf.Min(3, reports.Count);
            for (var i = 0; i < max; i++)
            {
                var report = reports[i];
                var laneLabel = TDLocalization.IsChinese
                    ? GetLocalizedLaneLabel(report.stat.laneKey)
                    : FormatLaneLabel(report.stat.laneKey);
                var segmentLabel = TDLocalization.IsChinese
                    ? GetLocalizedRoadSegmentLabel(report.stat.segmentIndex)
                    : GetRoadSegmentLabel(report.stat.segmentIndex);
                var token = $"{i + 1} {laneLabel}/{segmentLabel} H{report.heatScore} C{report.coverageScore}";
                if (i < 2)
                {
                    firstLine.Add(token);
                }
                else
                {
                    labels.Add(token);
                }
            }

            labels.Insert(0, string.Join("   ", firstLine));
            return string.Join("\n", labels);
        }

        private List<TDRoadHeatReport> BuildRoadHeatReports()
        {
            if (_gameOver && _cachedRoadHeatReports != null)
            {
                return _cachedRoadHeatReports;
            }

            var reports = new List<TDRoadHeatReport>();
            var towers = UnityEngine.Object.FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            foreach (var pair in _laneStats)
            {
                var lane = pair.Value;
                if (lane == null || lane.spawned <= 0)
                {
                    continue;
                }

                for (var segment = 0; segment < RoadSegmentCount; segment++)
                {
                    var stat = GetOrCreateRoadSegmentStat(lane.laneKey, segment);
                    var coverage = CalculateRoadSegmentCoverageScore(lane.laneKey, segment, towers);
                    var nextReached = segment >= RoadSegmentCount - 1
                        ? 0
                        : GetOrCreateRoadSegmentStat(lane.laneKey, segment + 1).reached;
                    var heat = CalculateRoadSegmentHeatScore(lane, stat, nextReached, coverage);
                    var report = new TDRoadHeatReport
                    {
                        stat = stat,
                        coverageScore = coverage,
                        heatScore = heat
                    };
                    report.hasSuggestedCell = TryFindSuggestedBuildCell(lane.laneKey, segment, out report.suggestedCell);
                    reports.Add(report);
                }
            }

            reports.Sort((a, b) =>
            {
                var delta = b.heatScore.CompareTo(a.heatScore);
                if (delta != 0)
                {
                    return delta;
                }

                delta = string.CompareOrdinal(a.stat.laneKey, b.stat.laneKey);
                return delta != 0 ? delta : b.stat.segmentIndex.CompareTo(a.stat.segmentIndex);
            });
            if (_gameOver)
            {
                _cachedRoadHeatReports = reports;
            }

            return reports;
        }

        private int CalculateRoadSegmentHeatScore(TDLaneRuntimeStat lane, TDRoadSegmentRuntimeStat segment, int nextReached, int coverageScore)
        {
            if (lane == null || segment == null || lane.spawned <= 0 || segment.reached <= 0)
            {
                return 0;
            }

            var pressure = Mathf.Clamp01(segment.reached / (float)lane.spawned);
            var passThrough = segment.segmentIndex >= RoadSegmentCount - 1
                ? Mathf.Clamp01((segment.escapes + segment.unresolvedAtEnd) / (float)segment.reached)
                : Mathf.Clamp01(nextReached / (float)segment.reached);
            var localFailure = Mathf.Clamp01((segment.escapes + segment.unresolvedAtEnd) / (float)segment.reached);
            var laneLeak = Mathf.Clamp01(lane.escapes / (float)lane.spawned);
            var coverageGap = 1f - Mathf.Clamp01(coverageScore / 100f);
            var lowDamage = 1f - Mathf.Clamp01(segment.damageDealt / Mathf.Max(1f, lane.damageDealt));
            var progressWeight = Mathf.Lerp(0.78f, 1.12f, segment.segmentIndex / Mathf.Max(1f, RoadSegmentCount - 1f));
            var heat = pressure * progressWeight *
                       ((coverageGap * 0.28f) +
                        (passThrough * 0.20f) +
                        (lowDamage * 0.10f) +
                        (laneLeak * 0.12f) +
                        (localFailure * 0.30f));
            return Mathf.Clamp(Mathf.RoundToInt(heat * 100f), 0, 100);
        }

        private int CalculateRoadSegmentCoverageScore(string laneKey, int segmentIndex, TDTower[] towers)
        {
            if (towers == null || towers.Length == 0 ||
                !_activeLanePaths.TryGetValue(laneKey, out var path) || path == null || path.Count <= 1)
            {
                return 0;
            }

            const int samples = 6;
            var covered = 0;
            var segmentStart = Mathf.Clamp01(segmentIndex / (float)RoadSegmentCount);
            var segmentEnd = Mathf.Clamp01((segmentIndex + 1f) / RoadSegmentCount);
            for (var i = 0; i < samples; i++)
            {
                var t = Mathf.Lerp(segmentStart, segmentEnd, (i + 0.5f) / samples);
                if (IsRoutePointCoveredByTower(GetPathPointAtNormalizedProgress(path, t), towers))
                {
                    covered++;
                }
            }

            return Mathf.RoundToInt(covered / (float)samples * 100f);
        }

        private bool TryFindSuggestedBuildCell(string laneKey, int segmentIndex, out Vector2Int cell)
        {
            cell = default;
            if (_gridMap == null || !_activeLanePaths.TryGetValue(laneKey, out var path) || path == null || path.Count <= 1)
            {
                return false;
            }

            var targetProgress = (segmentIndex + 0.5f) / RoadSegmentCount;
            var target = GetPathPointAtNormalizedProgress(path, targetProgress);
            var found = false;
            var bestDistance = float.MaxValue;
            for (var x = 0; x < GridWidth; x++)
            {
                for (var y = 0; y < GridHeight; y++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (!_gridMap.IsBuildable(candidate))
                    {
                        continue;
                    }

                    var distance = (_gridMap.CellToBuildWorld(candidate) - target).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        cell = candidate;
                        found = true;
                    }
                }
            }

            return found;
        }

        private static Vector3 GetPathPointAtNormalizedProgress(IReadOnlyList<Vector3> path, float progress)
        {
            if (path == null || path.Count == 0)
            {
                return Vector3.zero;
            }

            if (path.Count == 1)
            {
                return path[0];
            }

            var totalLength = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                totalLength += Vector3.Distance(path[i], path[i + 1]);
            }

            if (totalLength <= 0.0001f)
            {
                return path[0];
            }

            var targetDistance = Mathf.Clamp01(progress) * totalLength;
            var traversed = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var segmentLength = Vector3.Distance(path[i], path[i + 1]);
                if (traversed + segmentLength >= targetDistance)
                {
                    var local = segmentLength <= 0.0001f ? 0f : (targetDistance - traversed) / segmentLength;
                    return Vector3.Lerp(path[i], path[i + 1], Mathf.Clamp01(local));
                }

                traversed += segmentLength;
            }

            return path[path.Count - 1];
        }

        private static IReadOnlyList<Vector3> BuildRemainingPathFromNormalizedProgress(IReadOnlyList<Vector3> path, float progress)
        {
            if (path == null || path.Count <= 1)
            {
                return path ?? Array.Empty<Vector3>();
            }

            var clampedProgress = Mathf.Clamp(progress, 0f, 0.90f);
            if (clampedProgress <= 0.0001f)
            {
                return path;
            }

            var totalLength = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                totalLength += Vector3.Distance(path[i], path[i + 1]);
            }

            if (totalLength <= 0.0001f)
            {
                return path;
            }

            var targetDistance = clampedProgress * totalLength;
            var traversed = 0f;
            var nextPathIndex = 1;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var segmentLength = Vector3.Distance(path[i], path[i + 1]);
                if (traversed + segmentLength >= targetDistance)
                {
                    nextPathIndex = i + 1;
                    break;
                }

                traversed += segmentLength;
            }

            var remaining = new List<Vector3>(path.Count - nextPathIndex + 1)
            {
                GetPathPointAtNormalizedProgress(path, clampedProgress)
            };
            for (var i = nextPathIndex; i < path.Count; i++)
            {
                if ((remaining[remaining.Count - 1] - path[i]).sqrMagnitude > 0.0001f)
                {
                    remaining.Add(path[i]);
                }
            }

            if (remaining.Count == 1)
            {
                remaining.Add(path[path.Count - 1]);
            }

            return remaining;
        }

        private static string GetRoadSegmentLabel(int segmentIndex)
        {
            var safeIndex = Mathf.Clamp(segmentIndex, 0, RoadSegmentLabels.Length - 1);
            return RoadSegmentLabels[safeIndex];
        }

        private static string GetLocalizedRoadSegmentLabel(int segmentIndex)
        {
            return Mathf.Clamp(segmentIndex, 0, RoadSegmentCount - 1) switch
            {
                0 => "入口",
                1 => "接近段",
                2 => "核心段",
                _ => "出口"
            };
        }

        private List<TDTowerRuntimeStat> GetSortedTowerStats()
        {
            var towers = new List<TDTowerRuntimeStat>();
            foreach (var pair in _towerStats)
            {
                if (pair.Value != null)
                {
                    towers.Add(pair.Value);
                }
            }

            towers.Sort((a, b) =>
            {
                var delta = b.damageDealt.CompareTo(a.damageDealt);
                if (delta != 0)
                {
                    return delta;
                }

                delta = b.kills.CompareTo(a.kills);
                return delta != 0 ? delta : string.CompareOrdinal(a.towerId, b.towerId);
            });
            return towers;
        }

        private string BuildRunRecapLabel()
        {
            var waves = Mathf.Max(1, GetConfiguredWaveCount());
            var clearPct = Mathf.RoundToInt((_wavesCleared / (float)waves) * 100f);
            var leakPressure = Mathf.Max(0, _totalIntegrityDamageTaken);
            var economySpent = _budgetSpentOnBuilds + _budgetSpentOnUpgrades;
            var damagePerLeak = _totalEscapes <= 0 ? _totalDamageDealt : Mathf.RoundToInt(_totalDamageDealt / Mathf.Max(1f, _totalEscapes));
            var counterPct = _counterOpportunityDamage <= 0
                ? 100
                : Mathf.RoundToInt(_counterMatchedDamage / (float)_counterOpportunityDamage * 100f);

            if (TDLocalization.IsChinese)
            {
                return $"通关 {clearPct}%   伤害 {_totalDamageDealt}   每次漏怪伤害 {damagePerLeak}   克制 {counterPct}%\n" +
                       $"支出 {economySpent}   建造 {_budgetSpentOnBuilds}   升级 {_budgetSpentOnUpgrades} ({_upgradesPurchased})\n" +
                       $"防线 -{leakPressure}   装置 {_scenarioUses}/{_scenarioOpportunities}   指令 {_resonanceMatchedCommands}/{_resonanceCommandsUsed}   矩阵 {_matrixFullMatches}/{_matrixOpportunities}   汇聚 {_matrixConvergenceTriggers}";
            }

            return $"CLEAR {clearPct}%   DAMAGE {_totalDamageDealt}   PER LEAK {damagePerLeak}   COUNTER {counterPct}%\n" +
                   $"SPEND {economySpent}   BUILD {_budgetSpentOnBuilds}   UPGRADE {_budgetSpentOnUpgrades} ({_upgradesPurchased})\n" +
                   $"INTEGRITY -{leakPressure}   DEVICE {_scenarioUses}/{_scenarioOpportunities}   COMMAND {_resonanceMatchedCommands}/{_resonanceCommandsUsed}   MATRIX {_matrixFullMatches}/{_matrixOpportunities}   CONV {_matrixConvergenceTriggers}";
        }

        private string BuildRunRecommendationLabel()
        {
            var heatReports = BuildRoadHeatReports();
            var hotspot = heatReports
                .Where(report => report?.stat != null && report.stat.escapes + report.stat.unresolvedAtEnd > 0)
                .OrderByDescending(report => report.stat.escapes + report.stat.unresolvedAtEnd)
                .ThenByDescending(report => report.heatScore)
                .FirstOrDefault();
            hotspot ??= heatReports
                .Where(report => report?.stat != null && report.stat.reached > 0)
                .OrderBy(report => report.coverageScore)
                .ThenByDescending(report => report.heatScore)
                .FirstOrDefault();
            return $"1. {BuildHotspotRecommendation(hotspot)}\n" +
                   $"2. {BuildCounterCategoryRecommendation(hotspot)}\n" +
                   $"3. {BuildOperationalRecommendation(hotspot)}";
        }

        private string BuildHotspotRecommendation(TDRoadHeatReport hotspot)
        {
            if (hotspot?.stat == null)
            {
                return TDLocalization.IsChinese
                    ? "把第一座塔部署在压力最高的路线旁。"
                    : "Build the first tower beside the highest-pressure route.";
            }

            var failureCount = hotspot.stat.escapes + hotspot.stat.unresolvedAtEnd;
            var cellLabel = hotspot.hasSuggestedCell
                ? $" @{hotspot.suggestedCell.x},{hotspot.suggestedCell.y}"
                : string.Empty;
            if (TDLocalization.IsChinese)
            {
                var segment = $"{GetLocalizedLaneLabel(hotspot.stat.laneKey)}/{GetLocalizedRoadSegmentLabel(hotspot.stat.segmentIndex)}";
                if (failureCount <= 0)
                {
                    return hotspot.coverageScore >= 90
                        ? $"{segment} H{hotspot.heatScore}：到达 {hotspot.stat.reached}，漏怪/存活 0，覆盖 C{hotspot.coverageScore}；覆盖已充足，把低收益火力转向更薄弱的路段。"
                        : $"{segment} H{hotspot.heatScore}：到达 {hotspot.stat.reached}，漏怪/存活 0，覆盖 C{hotspot.coverageScore}；保持当前防线，仅在后续压力出现时增援{cellLabel}。";
                }

                return hotspot.coverageScore >= 90
                    ? $"{segment} H{hotspot.heatScore}：漏怪/存活 {failureCount}，覆盖 C{hotspot.coverageScore}；升级或把火力移至该路段{cellLabel}。"
                    : $"{segment} H{hotspot.heatScore}：漏怪/存活 {failureCount}，覆盖 C{hotspot.coverageScore}；在建议塔位补充覆盖{cellLabel}。";
            }

            if (failureCount <= 0)
            {
                return hotspot.coverageScore >= 90
                    ? $"{FormatLaneLabel(hotspot.stat.laneKey)}/{GetRoadSegmentLabel(hotspot.stat.segmentIndex)} H{hotspot.heatScore}: " +
                      $"{hotspot.stat.reached} reached, 0 leak/live, C{hotspot.coverageScore}; coverage sufficient, shift low-value firepower toward a weaker segment."
                    : $"{FormatLaneLabel(hotspot.stat.laneKey)}/{GetRoadSegmentLabel(hotspot.stat.segmentIndex)} H{hotspot.heatScore}: " +
                      $"{hotspot.stat.reached} reached, 0 leak/live, C{hotspot.coverageScore}; hold this line and reinforce only if later pressure appears{cellLabel}.";
            }

            if (hotspot.coverageScore >= 90)
            {
                return $"{FormatLaneLabel(hotspot.stat.laneKey)}/{GetRoadSegmentLabel(hotspot.stat.segmentIndex)} H{hotspot.heatScore}: " +
                       $"{hotspot.stat.reached} reached, {failureCount} leak/live, C{hotspot.coverageScore}; coverage saturated, upgrade or relocate output toward this segment{cellLabel}.";
            }

            return $"{FormatLaneLabel(hotspot.stat.laneKey)}/{GetRoadSegmentLabel(hotspot.stat.segmentIndex)} H{hotspot.heatScore}: " +
                   $"{hotspot.stat.reached} reached, {failureCount} leak/live, C{hotspot.coverageScore}; add coverage{cellLabel}.";
        }

        private string BuildCounterCategoryRecommendation(TDRoadHeatReport hotspot)
        {
            var category = GetHighestCounterGapCategory(out var matchedDamage, out var totalDamage);
            if (string.IsNullOrWhiteSpace(category) || totalDamage <= 0)
            {
                return TDLocalization.IsChinese
                    ? "没有明显的克制缺口；用一座输出塔搭配一座控制塔。"
                    : "No dominant counter gap; pair one damage tower with one control tower.";
            }

            var matchPct = Mathf.RoundToInt(matchedDamage / Mathf.Max(1f, totalDamage) * 100f);
            if (TDLocalization.IsChinese)
            {
                var localizedLane = hotspot?.stat == null ? "高压路线" : GetLocalizedLaneLabel(hotspot.stat.laneKey);
                return $"{GetLocalizedCounterCategoryLabel(category)}匹配 {matchPct}%（{matchedDamage}/{totalDamage} 伤害）；" +
                       $"在{localizedLane}补充 {GetAvailableCounterCategoryTowerSuggestion(category, true)}。";
            }

            var laneLabel = hotspot?.stat == null ? "the hot route" : FormatLaneLabel(hotspot.stat.laneKey);
            return $"{GetCounterCategoryLabel(category)} match {matchPct}% ({matchedDamage}/{totalDamage} dmg); " +
                   $"add {GetAvailableCounterCategoryTowerSuggestion(category)} on {laneLabel}.";
        }

        private string BuildOperationalRecommendation(TDRoadHeatReport hotspot)
        {
            var score = CalculateRunScore();
            if (_campaignRoute?.level?.scenario?.milestoneExam == true &&
                _activeScenarioMechanic != null &&
                _scenarioOpportunities > 0 &&
                _scenarioUses / (float)_scenarioOpportunities < 0.35f)
            {
                var examDecision = _examPresentationProfile?.decisionBody ??
                                   _campaignRoute.level.scenario.failureFocus.Replace('_', ' ');
                if (TDLocalization.IsChinese)
                {
                    return $"{TDLocalization.LocalizeRuntimeString(_activeScenarioMechanic.displayName)} {_scenarioUses}/{_scenarioOpportunities}：" +
                           $"{TDLocalization.LocalizeRuntimeString(examDecision)}。";
                }

                return $"{_activeScenarioMechanic.displayName} {_scenarioUses}/{_scenarioOpportunities}: {examDecision}.";
            }

            if (_isResonanceSystemEnabled && score.command < 55)
            {
                if (TDLocalization.IsChinese)
                {
                    return $"指令转化：已使用 {_resonanceCommandsUsed}/{_resonanceWindowsTriggered} 个窗口，" +
                           $"其中 {_resonanceMatchedCommands} 次匹配；下一次按威胁标签选择指令。";
                }

                return $"Command conversion: {_resonanceCommandsUsed}/{_resonanceWindowsTriggered} windows used, " +
                       $"{_resonanceMatchedCommands} matched; answer the next threat tag.";
            }

            if (_isResonanceSystemEnabled && _matrixOpportunities > 0 &&
                _matrixFullMatches / (float)_matrixOpportunities < 0.45f)
            {
                var matrixPct = Mathf.RoundToInt(_matrixFullMatches / (float)_matrixOpportunities * 100f);
                if (TDLocalization.IsChinese)
                {
                    return $"矩阵转化 {matrixPct}%（{_matrixFullMatches}/{_matrixOpportunities}）；让敌人特性与专精的指令倾向形成匹配。";
                }

                return $"Matrix conversion {matrixPct}% ({_matrixFullMatches}/{_matrixOpportunities}); pair enemy traits with the specialization's command affinity.";
            }

            if (_isResonanceSystemEnabled && _matrixFullMatches > 0 && _matrixConvergenceTriggers == 0)
            {
                if (_matrixBestWindowSpecializations < MatrixConvergenceRequiredSpecializations)
                {
                    if (TDLocalization.IsChinese)
                    {
                        return $"矩阵同步峰值 {_matrixBestWindowSync}，但只有 {_matrixBestWindowSpecializations} 种不同专精；部署两座倾向一致的终极塔触发汇聚。";
                    }

                    return $"Matrix sync peaked {_matrixBestWindowSync}, but only {_matrixBestWindowSpecializations} unique spec; field two aligned capstones for Convergence.";
                }

                if (TDLocalization.IsChinese)
                {
                    return $"矩阵同步峰值 {_matrixBestWindowSync}/{MatrixConvergenceRequiredMatches}；在两座倾向一致的终极塔同时攻击时释放指令。";
                }

                return $"Matrix sync peaked {_matrixBestWindowSync}/{MatrixConvergenceRequiredMatches}; time the command while both aligned capstones are firing.";
            }

            var towers = GetSortedTowerStats();
            if (towers.Count == 0)
            {
                return TDLocalization.IsChinese
                    ? "出兵前先投入备战资源；本局没有记录到防御塔贡献。"
                    : "Spend prep budget before dispatch; no tower contribution was recorded.";
            }

            var cellLabel = hotspot != null && hotspot.hasSuggestedCell
                ? $" @{hotspot.suggestedCell.x},{hotspot.suggestedCell.y}"
                : string.Empty;
            if (towers.Count == 1)
            {
                var only = towers[0];
                if (TDLocalization.IsChinese)
                {
                    return $"{GetLocalizedCompactTowerLabel(only.kind)}承担了 100% 伤害；在建议塔位增加第二个克制支点{cellLabel}。";
                }

                return $"{GetCompactTowerLabel(only.kind)} carries 100% dmg; add a second counter anchor{cellLabel}.";
            }

            var weakest = GetLeastProductiveTowerStat();
            if (weakest != null)
            {
                var value = weakest.damageDealt / Mathf.Max(1f, weakest.TotalSpend);
                var share = _totalDamageDealt <= 0 ? 0 : Mathf.RoundToInt(weakest.damageDealt / (float)_totalDamageDealt * 100f);
                if (share < 18 || value < 2f)
                {
                    if (TDLocalization.IsChinese)
                    {
                        return $"{GetLocalizedCompactTowerLabel(weakest.kind)} @{weakest.cell.x},{weakest.cell.y}：效率 {value:0.0}，伤害占比 {share}%；移至高压塔位{cellLabel}。";
                    }

                    return $"{GetCompactTowerLabel(weakest.kind)} @{weakest.cell.x},{weakest.cell.y}: {value:0.0} dmg/budget, {share}% share; move toward hot cell{cellLabel}.";
                }
            }

            var top = towers[0];
            var topShare = _totalDamageDealt <= 0 ? 0 : Mathf.RoundToInt(top.damageDealt / (float)_totalDamageDealt * 100f);
            if (TDLocalization.IsChinese)
            {
                return _upgradesPurchased <= 0
                    ? $"为 {GetLocalizedCompactTowerLabel(top.kind)} @{top.cell.x},{top.cell.y} 选择专精；它已经承担 {topShare}% 伤害。"
                    : $"主力支点 {GetLocalizedCompactTowerLabel(top.kind)} 承担 {topShare}% 伤害；继续增援高压塔位{cellLabel}。";
            }

            return _upgradesPurchased <= 0
                ? $"Specialize {GetCompactTowerLabel(top.kind)} @{top.cell.x},{top.cell.y}; it already carries {topShare}% damage."
                : $"Top anchor {GetCompactTowerLabel(top.kind)} carries {topShare}% damage; reinforce the hot cell{cellLabel}.";
        }

        private string GetHighestCounterGapCategory(out int matchedDamage, out int totalDamage)
        {
            var bestCategory = string.Empty;
            var bestGap = 0;
            matchedDamage = 0;
            totalDamage = 0;
            foreach (var pair in _threatCategoryDamage)
            {
                if (!IsCounterCategoryActionable(pair.Key))
                {
                    continue;
                }

                var matched = _threatCategoryCounterDamage.TryGetValue(pair.Key, out var value) ? value : 0;
                var gap = Mathf.Max(0, pair.Value - matched);
                if (gap > bestGap ||
                    (gap == bestGap && pair.Value > totalDamage) ||
                    (gap == bestGap && pair.Value == totalDamage && string.CompareOrdinal(pair.Key, bestCategory) < 0))
                {
                    bestCategory = pair.Key;
                    bestGap = gap;
                    matchedDamage = matched;
                    totalDamage = pair.Value;
                }
            }

            return bestCategory;
        }

        private bool IsCounterCategoryActionable(string category)
        {
            for (var i = 0; i < _availableTowerKinds.Count; i++)
            {
                if (IsTowerCounterForCategory(_availableTowerKinds[i], category))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetAvailableCounterCategoryTowerSuggestion(string category, bool localized = false)
        {
            var labels = new List<string>();
            for (var i = 0; i < _availableTowerKinds.Count; i++)
            {
                var kind = _availableTowerKinds[i];
                if (IsTowerCounterForCategory(kind, category))
                {
                    labels.Add(localized ? GetLocalizedCompactTowerLabel(kind) : GetCompactTowerLabel(kind));
                }
            }

            return labels.Count > 0
                ? string.Join("/", labels)
                : localized ? "已解锁的克制塔" : "an unlocked counter";
        }

        private static string GetCounterCategoryLabel(string category)
        {
            return category switch
            {
                "speed" => "Speed counter",
                "swarm" => "Swarm counter",
                "armor" => "Armor counter",
                "attrition" => "Attrition counter",
                _ => "Threat counter"
            };
        }

        private static string GetLocalizedCounterCategoryLabel(string category)
        {
            return category switch
            {
                "speed" => "高速克制",
                "swarm" => "群体克制",
                "armor" => "护甲克制",
                "attrition" => "消耗克制",
                _ => "威胁克制"
            };
        }

        private TDTowerRuntimeStat GetLeastProductiveTowerStat()
        {
            TDTowerRuntimeStat weakest = null;
            var weakestValue = float.MaxValue;
            foreach (var pair in _towerStats)
            {
                var stat = pair.Value;
                if (stat == null || stat.TotalSpend <= 0)
                {
                    continue;
                }

                var value = (stat.damageDealt + (stat.controlApplications * 8f)) / stat.TotalSpend;
                if (value < weakestValue ||
                    (Mathf.Approximately(value, weakestValue) && weakest != null && string.CompareOrdinal(stat.towerId, weakest.towerId) < 0))
                {
                    weakest = stat;
                    weakestValue = value;
                }
            }

            return weakest;
        }

        private string GetEnemyDisplayName(string enemyId)
        {
            if (!string.IsNullOrWhiteSpace(enemyId) && _enemyCatalog.TryGetValue(enemyId, out var entry) && !string.IsNullOrWhiteSpace(entry.displayName))
            {
                return entry.displayName;
            }

            return string.IsNullOrWhiteSpace(enemyId) ? "Unknown" : enemyId.Replace('_', ' ');
        }

        private static string GetCompactTowerLabel(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => "Rail",
                TDTowerKind.CinderMortar => "Mortar",
                TDTowerKind.FrostCoil => "Frost",
                TDTowerKind.ArcWelder => "Arc",
                TDTowerKind.SiegeDrill => "Siege",
                TDTowerKind.EmberFlak => "Flak",
                TDTowerKind.ResonanceBeacon => "Beacon",
                TDTowerKind.GravSnare => "Snare",
                _ => kind.ToString()
            };
        }

        private static string GetLocalizedCompactTowerLabel(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => "轨枪",
                TDTowerKind.CinderMortar => "迫击炮",
                TDTowerKind.FrostCoil => "霜冻",
                TDTowerKind.ArcWelder => "电弧",
                TDTowerKind.SiegeDrill => "钻机",
                TDTowerKind.EmberFlak => "高射炮",
                TDTowerKind.ResonanceBeacon => "信标",
                TDTowerKind.GravSnare => "重力阱",
                _ => kind.ToString()
            };
        }

        private string GetSelectedTowerSlotLabel()
        {
            var slot = _unlockedTowerKinds.IndexOf(_selectedTowerKind);
            return slot >= 0 ? $"[{slot + 1}]" : "[?]";
        }

        private void TryUpgradeSelectedTowerFromUi(TDTowerUpgradeBranch branch)
        {
            var tower = GetUiFocusedTower();
            if (tower == null)
            {
                SetStatus("Select a tower before upgrading.");
                return;
            }

            _selectedTowerForUi = tower;
            _selectedUpgradeBranch = branch;
            TryUpgradeTower(tower, branch);
        }

        private TDTower GetUiFocusedTower()
        {
            if (_selectedTowerForUi != null && _selectedTowerForUi.gameObject != null)
            {
                return _selectedTowerForUi;
            }

            return _hoveredTower != null && _hoveredTower.gameObject != null ? _hoveredTower : null;
        }

        private void SelectTowerForUi(TDTower tower)
        {
            if (tower == null)
            {
                return;
            }

            var previous = _selectedTowerForUi;
            _selectedTowerForUi = tower;
            if (previous != null && previous != tower)
            {
                previous.Readability?.SetInteractionState(previous == _hoveredTower, false);
            }

            tower.Readability?.SetInteractionState(tower == _hoveredTower, true);
        }

        private bool IsPointerOverBattleUi()
        {
            return UseRuntimeBattleUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void EnsureUiEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private IEnumerator SelectUiNextFrame(Selectable selectable)
        {
            yield return null;
            if (EventSystem.current != null && selectable != null && selectable.gameObject.activeInHierarchy && selectable.interactable)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        private void EnsureGamepadFocus()
        {
            if (!TDInputCompat.GetGamepadNavigationDown() || EventSystem.current == null)
            {
                return;
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                var selectable = selected.GetComponent<Selectable>();
                if (selectable != null && selectable.interactable)
                {
                    return;
                }
            }

            Selectable fallback = null;
            if (_uiStartWaveButton != null && _uiStartWaveButton.gameObject.activeInHierarchy &&
                _uiStartWaveButton.interactable)
            {
                fallback = _uiStartWaveButton;
            }
            else
            {
                fallback = _uiTowerButtons.FirstOrDefault(button =>
                    button != null && button.gameObject.activeInHierarchy && button.interactable);
            }

            if (fallback != null)
            {
                EventSystem.current.SetSelectedGameObject(fallback.gameObject);
            }
        }

        private RectTransform CreateUiPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var rect = CreateUiRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private void BuildRunResultCharts()
        {
            _uiGameOverScoreChartRoot = CreateUiRect(
                "Run Result Five Axis Chart",
                _uiGameOverRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -124f),
                new Vector2(704f, 52f));

            var scoreLabels = new[] { "COVER", "COUNTER", "OUTPUT", "ECON", "COMMAND" };
            var scoreIcons = new[]
            {
                TDUiP132Icon.Hotspot,
                TDUiP132Icon.ArmorBreak,
                TDUiP132Icon.Damage,
                TDUiP132Icon.Budget,
                TDUiP132Icon.Resonance
            };
            var scoreColors = new[]
            {
                new Color(0.28f, 0.82f, 1f, 1f),
                new Color(1f, 0.54f, 0.20f, 1f),
                new Color(0.98f, 0.78f, 0.28f, 1f),
                new Color(0.36f, 0.88f, 0.54f, 1f),
                new Color(0.88f, 0.54f, 0.96f, 1f)
            };
            for (var i = 0; i < scoreLabels.Length; i++)
            {
                var x = i * 140f;
                CreateUiSpriteImage(
                    $"Score Axis {scoreLabels[i]} Icon",
                    _uiGameOverScoreChartRoot,
                    new Vector2(x, 0f),
                    new Vector2(18f, 18f),
                    TDUiP132Art.IconPath(scoreIcons[i]),
                    Color.white);
                CreateUiText($"Score Axis {scoreLabels[i]}", _uiGameOverScoreChartRoot, new Vector2(x + 22f, 0f), new Vector2(74f, 18f), scoreLabels[i], 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.76f, 0.84f, 0.88f, 1f));
                var value = CreateUiText($"Score Axis {scoreLabels[i]} Value", _uiGameOverScoreChartRoot, new Vector2(x + 98f, 0f), new Vector2(30f, 18f), "0", 12, FontStyle.Bold, TextAnchor.MiddleRight, scoreColors[i]);
                var back = CreateUiImage(
                    $"Score Axis {scoreLabels[i]} Back",
                    _uiGameOverScoreChartRoot,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(x, -26f),
                    new Vector2(128f, 10f),
                    new Color(0.08f, 0.12f, 0.14f, 0.92f));
                var fill = CreateUiImage(
                    $"Score Axis {scoreLabels[i]} Fill",
                    back.transform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    Vector2.zero,
                    new Vector2(1f, 6f),
                    scoreColors[i]);
                _uiGameOverScoreBarFills.Add(fill);
                _uiGameOverScoreBarValues.Add(value);
            }

            _uiGameOverLaneChartRoot = CreateUiRect(
                "Run Result Lane Chart",
                _uiGameOverRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -207f),
                new Vector2(338f, 80f));
            _uiGameOverTowerChartRoot = CreateUiRect(
                "Run Result Tower Chart",
                _uiGameOverRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(394f, -207f),
                new Vector2(338f, 80f));
            BuildRunResultBreakdownRows(
                "Lane",
                _uiGameOverLaneChartRoot,
                _uiGameOverLaneBarRows,
                _uiGameOverLaneBarFills,
                _uiGameOverLaneBarLabels,
                _uiGameOverLaneBarValues,
                new Color(0.28f, 0.80f, 1f, 1f));
            BuildRunResultBreakdownRows(
                "Tower",
                _uiGameOverTowerChartRoot,
                _uiGameOverTowerBarRows,
                _uiGameOverTowerBarFills,
                _uiGameOverTowerBarLabels,
                _uiGameOverTowerBarValues,
                new Color(0.34f, 0.90f, 0.58f, 1f));
        }

        private void BuildRunResultBreakdownRows(
            string prefix,
            Transform parent,
            List<RectTransform> rows,
            List<Image> fills,
            List<Text> labels,
            List<Text> values,
            Color fillColor)
        {
            for (var i = 0; i < 4; i++)
            {
                var row = CreateUiRect(
                    $"{prefix} Chart Row {i + 1}",
                    parent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, -(i * 20f)),
                    new Vector2(338f, 18f));
                var label = CreateUiText($"{prefix} Chart Label {i + 1}", row, Vector2.zero, new Vector2(66f, 18f), "-", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.86f, 0.90f, 1f));
                var back = CreateUiImage(
                    $"{prefix} Chart Back {i + 1}",
                    row,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(68f, -5f),
                    new Vector2(194f, 8f),
                    new Color(0.08f, 0.12f, 0.14f, 0.92f));
                var fill = CreateUiImage(
                    $"{prefix} Chart Fill {i + 1}",
                    back.transform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    Vector2.zero,
                    new Vector2(1f, 5f),
                    fillColor);
                var value = CreateUiText($"{prefix} Chart Value {i + 1}", row, new Vector2(268f, 0f), new Vector2(70f, 18f), "0%", 12, FontStyle.Bold, TextAnchor.MiddleRight, fillColor);
                rows.Add(row);
                fills.Add(fill);
                labels.Add(label);
                values.Add(value);
            }
        }

        private Text CreateUiMetric(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, Color background, Color foreground, string iconResourcePath)
        {
            var root = CreateUiPanel(name + " Backdrop", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta, background);
            TDUiWorldSkin.ApplyMetric(root, foreground);
            var accent = CreateUiImage(name + " Accent", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f), foreground);
            accent.raycastTarget = false;
            CreateUiSpriteImage(name + " Icon", root, new Vector2(8f, -6f), new Vector2(26f, 26f), iconResourcePath, Color.white);
            var label = CreateUiText(name, root, new Vector2(38f, 0f), new Vector2(sizeDelta.x - 42f, sizeDelta.y), string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleLeft, foreground);
            return label;
        }

        private void AddUiPanelChrome(RectTransform panel, Color accentColor)
        {
            if (panel == null)
            {
                return;
            }

            if (panel.parent is RectTransform parentRect &&
                Mathf.Abs(parentRect.rect.width - panel.rect.width) <= 2f &&
                Mathf.Abs(parentRect.rect.height - panel.rect.height) <= 2f &&
                parentRect.GetComponent<Image>() != null)
            {
                TDUiWorldSkin.ApplyPanel(panel, accentColor, true);
                return;
            }

            var compact = panel.sizeDelta.y <= 72f;
            TDUiWorldSkin.ApplyPanel(panel, accentColor, compact);
        }

        private static void SetUiBottomRightLayout(RectTransform rect, Vector2 anchoredPosition)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
        }

        private Image CreateUiImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var rect = CreateUiRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (name.Contains("Rule") || name.Contains("Divider") || name.Contains("Chart Back"))
            {
                TDUiWorldSkin.ApplyRule(image, color, sizeDelta.y > sizeDelta.x);
            }
            return image;
        }

        private Image CreateUiSpriteImage(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, string resourcePath, Color color)
        {
            var rect = CreateUiRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadUiSprite(resourcePath);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private Sprite LoadUiSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            if (_uiSpriteCache.TryGetValue(resourcePath, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = TDUiP132Art.LoadVirtualSprite(resourcePath);
            sprite ??= Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = $"P11 UI {texture.name}";
                }
            }

            if (sprite != null)
            {
                _uiSpriteCache[resourcePath] = sprite;
            }

            return sprite;
        }

        private Image AddUiButtonIcon(Button button, string name, string resourcePath, Vector2 topLeft, Vector2 sizeDelta, float labelLeftInset)
        {
            if (button == null)
            {
                return null;
            }

            var icon = CreateUiSpriteImage(name, button.transform, topLeft, sizeDelta, resourcePath, Color.white);
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.rectTransform.offsetMin = new Vector2(labelLeftInset, label.rectTransform.offsetMin.y);
            }

            return icon;
        }

        private Text CreateUiText(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var rect = CreateUiRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta);
            var label = rect.gameObject.AddComponent<Text>();
            var role = ResolveUiTextRole(fontSize);
            var resolvedFontSize = GetUiRoleFontSize(role);
            label.font = TDLocalization.ResolveFont(_uiFont);
            label.fontSize = resolvedFontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color;
            TDLocalization.SetLabel(label, text, _uiFont);
            label.lineSpacing = role == TDUiTextRole.Body ? 0.94f : 1f;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = resolvedFontSize <= 17;
            label.resizeTextMinSize = Mathf.Max(9, resolvedFontSize - 3);
            label.resizeTextMaxSize = resolvedFontSize;
            label.raycastTarget = false;
            TDUiWorldSkin.ApplyText(label, fontStyle == FontStyle.Bold);
            _baseUiFontSizes[label] = resolvedFontSize;
            if (_largeTextEnabled)
            {
                label.fontSize = resolvedFontSize + 1;
                label.resizeTextMinSize = Mathf.Max(9, label.fontSize - 3);
                label.resizeTextMaxSize = label.fontSize;
            }
            return label;
        }

        private static TDUiTextRole ResolveUiTextRole(int requestedSize)
        {
            if (requestedSize >= 20)
            {
                return TDUiTextRole.ScreenTitle;
            }

            if (requestedSize >= 16)
            {
                return TDUiTextRole.SectionTitle;
            }

            if (requestedSize >= 13)
            {
                return TDUiTextRole.PanelTitle;
            }

            if (requestedSize >= 12)
            {
                return TDUiTextRole.Metric;
            }

            return requestedSize >= 11 ? TDUiTextRole.Body : TDUiTextRole.Caption;
        }

        private static int GetUiRoleFontSize(TDUiTextRole role)
        {
            return role switch
            {
                TDUiTextRole.ScreenTitle => 20,
                TDUiTextRole.SectionTitle => 17,
                TDUiTextRole.PanelTitle => 15,
                TDUiTextRole.Metric => 13,
                TDUiTextRole.Body => 12,
                _ => 11
            };
        }

        private Button CreateUiButton(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, string text, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateUiRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.28f, 0.31f, 0.94f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var strongAction = name.Contains("Start Wave") || name.Contains("Deploy") ||
                               name.Contains("Command") || name.Contains("Next Mission") ||
                               name.Contains("Restart");
            TDUiWorldSkin.ApplyButton(button, strongAction ? TDUiWorldSkin.Ember : TDUiWorldSkin.Brass, strongAction);
            TDUiFocusVisual.Attach(button);

            var label = CreateUiText("Label", rect, Vector2.zero, sizeDelta, text, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.96f, 0.95f, 0.90f, 1f));
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            return button;
        }

        private RectTransform CreateUiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private void SetUiText(Text label, string text)
        {
            if (label != null)
            {
                TDLocalization.SetLabel(label, text ?? string.Empty, _uiFont);
            }
        }

        private void UpdateRoutePreview()
        {
            if (!_debugRoutePreviewVisible || _gameOver || _missionBoardOpen || !_isInPrepPhase ||
                _currentWaveDefinition == null || _activeLanePaths.Count == 0)
            {
                HideRoutePreview();
                return;
            }

            var lanes = BuildWavePreviewLaneKeys(_currentWaveDefinition);
            if (lanes.Count == 0)
            {
                HideRoutePreview();
                return;
            }

            EnsureRoutePreviewRoot();
            var lanePressure = BuildWaveLanePressureMap(_currentWaveDefinition);
            var maxPressure = 1;
            for (var i = 0; i < lanes.Count; i++)
            {
                maxPressure = Mathf.Max(maxPressure, GetLanePressure(lanePressure, lanes[i]));
            }

            var visible = 0;
            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                if (!_activeLanePaths.TryGetValue(lane, out var path) || path == null || path.Count <= 1)
                {
                    continue;
                }

                var line = GetOrCreateRoutePreviewLine(visible);
                ConfigureRoutePreviewLine(line, path, visible, GetLanePressure(lanePressure, lane), maxPressure);
                visible++;
            }

            for (var i = visible; i < _routePreviewLines.Count; i++)
            {
                if (_routePreviewLines[i] != null)
                {
                    _routePreviewLines[i].gameObject.SetActive(false);
                }
            }
        }

        private void HideRoutePreview()
        {
            for (var i = 0; i < _routePreviewLines.Count; i++)
            {
                if (_routePreviewLines[i] != null)
                {
                    _routePreviewLines[i].gameObject.SetActive(false);
                }
            }
        }

        private List<string> BuildWavePreviewLaneKeys(TDWaveDefinition wave)
        {
            var lanes = new List<string>();
            if (wave?.groups != null)
            {
                for (var i = 0; i < wave.groups.Length; i++)
                {
                    var keys = ResolvePreviewLaneKeys(wave.groups[i]);
                    for (var k = 0; k < keys.Count; k++)
                    {
                        if (!lanes.Contains(keys[k]))
                        {
                            lanes.Add(keys[k]);
                        }
                    }
                }
            }

            if (lanes.Count == 0 && _activeLanePaths.ContainsKey("default"))
            {
                lanes.Add("default");
            }

            return lanes;
        }

        private void EnsureRoutePreviewRoot()
        {
            if (_routePreviewRoot != null)
            {
                return;
            }

            var root = new GameObject("RoutePreview");
            root.transform.SetParent(transform, false);
            _routePreviewRoot = root.transform;
        }

        private LineRenderer GetOrCreateRoutePreviewLine(int index)
        {
            while (_routePreviewLines.Count <= index)
            {
                EnsureRoutePreviewRoot();
                var lineObject = new GameObject($"RoutePreview_{_routePreviewLines.Count:00}");
                lineObject.transform.SetParent(_routePreviewRoot, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = false;
                line.numCapVertices = 4;
                line.numCornerVertices = 3;
                line.textureMode = LineTextureMode.Stretch;
                line.alignment = LineAlignment.View;
                line.sharedMaterial = GetRoutePreviewMaterial();
                line.sortingOrder = 8;
                _routePreviewLines.Add(line);
            }

            return _routePreviewLines[index];
        }

        private void ConfigureRoutePreviewLine(LineRenderer line, IReadOnlyList<Vector3> path, int laneIndex, int pressure, int maxPressure)
        {
            if (line == null || path == null || path.Count <= 1)
            {
                return;
            }

            line.gameObject.SetActive(true);
            line.positionCount = path.Count;
            for (var i = 0; i < path.Count; i++)
            {
                var point = path[i];
                point.z = -0.05f;
                line.SetPosition(i, point);
            }

            var color = GetRoutePreviewColor(laneIndex);
            var pressureT = Mathf.Clamp01(pressure / (float)Mathf.Max(1, maxPressure));
            color.a = Mathf.Lerp(color.a * 0.44f, 0.42f, pressureT);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.48f);
            line.startWidth = Mathf.Lerp(0.035f, 0.075f, pressureT);
            line.endWidth = Mathf.Lerp(0.024f, 0.052f, pressureT);
        }

        private Color GetRoutePreviewColor(int index)
        {
            if (_colorblindMarkersEnabled)
            {
                return (index % 4) switch
                {
                    0 => new Color(1f, 0.76f, 0.16f, 0.52f),
                    1 => new Color(0.20f, 0.72f, 1f, 0.48f),
                    2 => new Color(0.24f, 0.94f, 0.58f, 0.46f),
                    _ => new Color(0.94f, 0.42f, 0.82f, 0.48f)
                };
            }

            return (index % 4) switch
            {
                0 => new Color(0.98f, 0.82f, 0.32f, 0.42f),
                1 => new Color(0.44f, 0.88f, 1f, 0.38f),
                2 => new Color(0.66f, 1f, 0.62f, 0.36f),
                _ => new Color(1f, 0.56f, 0.38f, 0.38f)
            };
        }

        private Material GetRoutePreviewMaterial()
        {
            if (_routePreviewMaterial != null)
            {
                return _routePreviewMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default");
            if (shader == null)
            {
                return null;
            }

            _routePreviewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _routePreviewMaterial;
        }

        private void EnsureHudStyles()
        {
            if (_hudPanelStyle != null)
            {
                return;
            }

            _hudPanelBgTexture = Resources.Load<Texture2D>("Art/hud_panel_bg");
            _hudPanelTitleTexture = Resources.Load<Texture2D>("Art/hud_panel_titlebar");
            _hudStatusStripTexture = Resources.Load<Texture2D>("Art/hud_status_strip");
            _hudButtonTexture = Resources.Load<Texture2D>("Art/hud_button_restart");
            _hudIconWaveTexture = Resources.Load<Texture2D>(TDUiVisualIdentity.WaveIconPath) ?? Resources.Load<Texture2D>("Art/hud_icon_wave");
            _hudIconIntegrityTexture = Resources.Load<Texture2D>(TDUiVisualIdentity.IntegrityIconPath) ?? Resources.Load<Texture2D>("Art/hud_icon_integrity");
            _hudIconBudgetTexture = Resources.Load<Texture2D>(TDUiVisualIdentity.BudgetIconPath) ?? Resources.Load<Texture2D>("Art/hud_icon_budget");

            _hudPanelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _hudPanelTexture.SetPixel(0, 0, new Color(0.03f, 0.06f, 0.09f, 0.84f));
            _hudPanelTexture.Apply();

            _hudPanelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _hudPanelTexture, textColor = new Color(1f, 1f, 1f, 0f) },
                border = new RectOffset(0, 0, 0, 0)
            };

            _hudTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.88f, 0.97f, 1f, 1f) }
            };

            _hudTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.84f, 0.92f, 0.96f, 0.96f) }
            };

            _hudStatusStyle = new GUIStyle(_hudTextStyle)
            {
                normal = { textColor = new Color(0.96f, 0.90f, 0.72f, 0.98f) }
            };

            _hudGuideStyle = new GUIStyle(_hudTextStyle)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.98f, 0.91f, 0.72f, 0.98f) }
            };

            _hudMetricLabelStyle = new GUIStyle(_hudTextStyle)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.76f, 0.86f, 0.94f, 0.98f) }
            };

            _hudMetricValueStyle = new GUIStyle(_hudTextStyle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.95f, 0.98f, 1f, 1f) }
            };

            _hudButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = new Color(0.98f, 0.95f, 0.88f, 1f) },
                hover = { textColor = new Color(1f, 0.97f, 0.92f, 1f) },
                active = { textColor = new Color(1f, 0.88f, 0.72f, 1f) }
            };
            _hudButtonStyle.normal.background = null;
            _hudButtonStyle.hover.background = null;
            _hudButtonStyle.active.background = null;
            _hudButtonStyle.focused.background = null;
        }

        private void ApplyHudScale(float hudScale)
        {
            _hudTitleStyle.fontSize = Mathf.RoundToInt(19f * hudScale);
            _hudTextStyle.fontSize = Mathf.RoundToInt(14f * hudScale);
            _hudStatusStyle.fontSize = Mathf.RoundToInt(14f * hudScale);
            _hudGuideStyle.fontSize = Mathf.RoundToInt(14f * hudScale);
            _hudMetricLabelStyle.fontSize = Mathf.RoundToInt(10f * hudScale);
            _hudMetricValueStyle.fontSize = Mathf.RoundToInt(18f * hudScale);
            _hudButtonStyle.fontSize = Mathf.RoundToInt(20f * hudScale);
        }

        private void DrawHudPanel(Rect panelRect, float hudScale)
        {
            GUI.Box(panelRect, string.Empty, _hudPanelStyle);

            if (_hudPanelTitleTexture == null)
            {
                return;
            }

            var titleHeight = Mathf.Min(panelRect.height * 0.38f, 74f * hudScale);
            var titleRect = new Rect(panelRect.x, panelRect.y, panelRect.width, titleHeight);
            DrawTexture(titleRect, _hudPanelTitleTexture, 0.16f);
        }

        private void DrawHudMetric(Rect rect, Texture2D icon, string label, string value)
        {
            var iconSize = rect.height * 0.72f;
            var iconRect = new Rect(rect.x, rect.y + ((rect.height - iconSize) * 0.5f), iconSize, iconSize);
            if (icon != null)
            {
                DrawTexture(iconRect, icon, 1f);
            }

            var textX = iconRect.xMax + 6f;
            var textWidth = rect.width - (iconSize + 8f);
            DrawShadowedLabel(
                new Rect(textX, rect.y + 1f, textWidth, rect.height * 0.44f),
                label,
                _hudMetricLabelStyle,
                new Color(0.78f, 0.88f, 0.95f, 1f),
                new Color(0f, 0f, 0f, 0.52f));
            DrawShadowedLabel(
                new Rect(textX, rect.y + (rect.height * 0.36f), textWidth, rect.height * 0.64f),
                value,
                _hudMetricValueStyle,
                new Color(0.96f, 0.98f, 1f, 1f),
                new Color(0f, 0f, 0f, 0.54f));
        }

        private void DrawStartWaveButton(Rect rect, float hudScale)
        {
            var canStart = CanStartCurrentWave();
            var isPrep = _isInPrepPhase && !_gameOver;
            var label = !isPrep ? "Combat" : canStart ? "Start Wave" : "Build First";

            if (_hudButtonTexture != null)
            {
                DrawTexture(rect, _hudButtonTexture, isPrep && canStart ? 0.92f : 0.42f);
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = isPrep && canStart;
            if (GUI.Button(rect, label, _hudButtonStyle))
            {
                TryRequestWaveStart();
            }

            GUI.enabled = previousEnabled;
        }

        private string GetPrepHudLabel()
        {
            if (_playbackPaused)
            {
                return $"PAUSED  Wave {_wave:00}  /  {_lastActivePlaybackSpeed:0}x resumes from the playback controls";
            }

            if (!_isInPrepPhase)
            {
                return _activeEnemies.Count > 0
                    ? $"Combat Wave {_wave:00}   Phase {_currentWavePhase}   Enemies {_activeEnemies.Count}"
                    : $"Wave {_wave:00} resolving   Phase {_currentWavePhase}";
            }

            var countdown = IsOpeningWaveBuildRequired() ? "hold" : $"{Mathf.Max(0f, _prepCountdown):0.0}s";
            var budgetState = _currentWaveBudgetInRange ? "OK" : "OUT";
            return $"Prep Wave {_wave:00}   {countdown}   Phase {_currentWavePhase}   Budget {_currentWaveBudgetActual:0.##}/{_currentWaveBudgetExpected:0.##} {budgetState}";
        }

        private string GetGuideHudLabel()
        {
            if (_tutorialVisible && _tutorialStep != TDFirstRunTutorialStep.Complete)
            {
                var stepLabel = _tutorialStep switch
                {
                    TDFirstRunTutorialStep.BuildTower => "Deploy a tower",
                    TDFirstRunTutorialStep.InspectRange => "Inspect coverage",
                    TDFirstRunTutorialStep.StartWave => "Dispatch the wave",
                    TDFirstRunTutorialStep.ReadArmor => "Read armor",
                    TDFirstRunTutorialStep.UpgradeTower => "Commit an upgrade",
                    _ => "Use the map mechanic"
                };
                return $"INTERACTIVE TRAINING  {(int)_tutorialStep + 1}/6  /  {stepLabel}";
            }

            if (_isInPrepPhase && IsOpeningWaveBuildRequired())
            {
                return "Build one Rail Lancer on a glowing pad. Check the range ring, then press Start Wave.";
            }

            if (_isInPrepPhase)
            {
                return string.IsNullOrWhiteSpace(_currentWaveHint)
                    ? "Adjust towers during prep, then press Start Wave."
                    : _currentWaveHint;
            }

            if (_activeEnemies.Count > 0)
            {
                return $"Threat tags: {_currentWaveThreatTags}. Watch leaks and coverage gaps.";
            }

            return _lastStatus;
        }

        private static void DrawResonanceBar(Rect rect, float normalized)
        {
            var clamped = Mathf.Clamp01(normalized);
            DrawTexture(rect, Texture2D.whiteTexture, 0.22f);

            if (clamped <= 0f)
            {
                return;
            }

            var fillRect = new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, (rect.width - 2f) * clamped), Mathf.Max(0f, rect.height - 2f));
            var prev = GUI.color;
            GUI.color = new Color(1f, 0.66f, 0.22f, 0.95f);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        private static void DrawTexture(Rect rect, Texture texture, float alpha)
        {
            if (texture == null)
            {
                return;
            }

            var prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * Mathf.Clamp01(alpha));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style, Color color, Color shadow)
        {
            var prevColor = style.normal.textColor;

            style.normal.textColor = shadow;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);

            style.normal.textColor = color;
            GUI.Label(rect, text, style);

            style.normal.textColor = prevColor;
        }

        public TDEnemy GetClosestEnemy(Vector3 origin, float maxRange)
        {
            TDEnemy closest = null;
            var bestSqrDistance = maxRange * maxRange;

            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                var sqrDistance = (enemy.transform.position - origin).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    closest = enemy;
                }
            }

            return closest;
        }

        public TDEnemy GetPriorityEnemy(Vector3 origin, float maxRange, TDTowerKind towerKind)
        {
            TDEnemy best = null;
            var rangeSqr = maxRange * maxRange;
            var bestScore = float.MinValue;
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                var sqrDistance = (enemy.transform.position - origin).sqrMagnitude;
                if (sqrDistance > rangeSqr)
                {
                    continue;
                }

                var score = enemy.RouteProgress01 * 100f;
                score += ResolveTowerTargetCounterBonus(towerKind, enemy);
                score += (1f - enemy.HealthRatio) * 5f;
                score -= sqrDistance / Mathf.Max(0.01f, rangeSqr) * 2f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = enemy;
            }

            return best;
        }

        private static float ResolveTowerTargetCounterBonus(TDTowerKind towerKind, TDEnemy enemy)
        {
            if (enemy == null)
            {
                return 0f;
            }

            return towerKind switch
            {
                TDTowerKind.FrostCoil or TDTowerKind.GravSnare =>
                    enemy.HasTag("fast") || enemy.HasTag("flank") ? (enemy.IsSlowed ? 8f : 24f) : 0f,
                TDTowerKind.EmberFlak =>
                    enemy.HasTag("fast") || enemy.HasTag("flank") ? 22f :
                    enemy.HasTag("swarm") ? 12f : 0f,
                TDTowerKind.RailLancer or TDTowerKind.SiegeDrill =>
                    enemy.HasTag("armored") || enemy.HasTag("heavy") || enemy.HasTag("boss") ? 20f :
                    enemy.HasTag("special") ? 12f : 0f,
                TDTowerKind.CinderMortar or TDTowerKind.ArcWelder =>
                    enemy.HasTag("swarm") || enemy.HasTag("spawn") || enemy.HasTag("split") ? 12f : 0f,
                _ => 0f
            };
        }

        public List<TDEnemy> GetEnemiesInRange(Vector3 origin, float radius, int maxTargets)
        {
            var radiusSqr = radius * radius;
            var candidates = new List<TDEnemy>();

            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                var sqrDistance = (enemy.transform.position - origin).sqrMagnitude;
                if (sqrDistance <= radiusSqr)
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort((a, b) =>
            {
                var aDist = (a.transform.position - origin).sqrMagnitude;
                var bDist = (b.transform.position - origin).sqrMagnitude;
                return aDist.CompareTo(bDist);
            });

            if (candidates.Count > maxTargets)
            {
                candidates.RemoveRange(maxTargets, candidates.Count - maxTargets);
            }

            return candidates;
        }

        private bool HasSupportAuraNearby(TDEnemy target, float radius)
        {
            if (target == null || radius <= 0f)
            {
                return false;
            }

            var radiusSqr = radius * radius;
            var origin = target.transform.position;
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var other = _activeEnemies[i];
                if (other == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(other, target) || !other.HasTag("support"))
                {
                    continue;
                }

                if ((other.transform.position - origin).sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        public float GetTowerDamageMultiplier(TDTowerKind towerKind)
        {
            var multiplier = GetCampaignTowerPowerMultiplier();
            if (!IsResonanceWindowActive)
            {
                return multiplier;
            }

            multiplier *= 1.10f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.16f;
                multiplier *= GetDoctrineCommandPowerMultiplier(TDResonanceCommand.EmberSurge);
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark &&
                (towerKind == TDTowerKind.FrostCoil || towerKind == TDTowerKind.SiegeDrill))
            {
                multiplier *= 1.08f;
            }

            return multiplier;
        }

        private float GetCampaignTowerPowerMultiplier()
        {
            var perLevelPct = Mathf.Max(0f, _campaign?.globalRules?.towerPowerPerLevelPct ?? 0f);
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
            return 1f + Mathf.Max(0, levelIndex - 1) * perLevelPct * 0.01f;
        }

        public float GetTowerFireRateMultiplier(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var multiplier = 1.12f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.20f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark &&
                (towerKind == TDTowerKind.RailLancer || towerKind == TDTowerKind.ArcWelder))
            {
                multiplier *= 1.08f;
            }

            if (_matrixConvergenceTriggeredThisWindow && _activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= MatrixConvergenceEmberFireRateMultiplier;
            }

            return multiplier;
        }

        public float GetProjectileSpeedMultiplier(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var multiplier = 1.06f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.12f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark &&
                (towerKind == TDTowerKind.RailLancer || towerKind == TDTowerKind.EmberFlak))
            {
                multiplier *= 1.10f;
            }

            return multiplier;
        }

        public float GetAoeRadiusMultiplier(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var supportsAoe =
                towerKind == TDTowerKind.CinderMortar ||
                towerKind == TDTowerKind.ArcWelder ||
                towerKind == TDTowerKind.EmberFlak ||
                towerKind == TDTowerKind.ResonanceBeacon ||
                towerKind == TDTowerKind.GravSnare;
            if (!supportsAoe)
            {
                return 1f;
            }

            var multiplier = 1.08f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.18f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
            {
                multiplier *= 1.08f;
            }

            if (towerKind == TDTowerKind.CinderMortar || towerKind == TDTowerKind.GravSnare)
            {
                multiplier *= 1.04f;
            }

            return multiplier;
        }

        public float GetSlowStrengthMultiplier(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var supportsSlow =
                towerKind == TDTowerKind.FrostCoil ||
                towerKind == TDTowerKind.GravSnare ||
                towerKind == TDTowerKind.ResonanceBeacon;
            if (!supportsSlow)
            {
                return 1f;
            }

            var multiplier = 1.10f;
            if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
            {
                multiplier *= 1.25f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.08f;
            }

            return multiplier;
        }

        public float GetSlowDurationBonus(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 0f;
            }

            var supportsSlow =
                towerKind == TDTowerKind.FrostCoil ||
                towerKind == TDTowerKind.GravSnare ||
                towerKind == TDTowerKind.ResonanceBeacon;
            if (!supportsSlow)
            {
                return 0f;
            }

            if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
            {
                return 0.55f;
            }

            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                return 0.20f;
            }

            return 0f;
        }

        public int GetModifiedDamageForEnemy(TDTower sourceTower, TDEnemy enemy, int rawDamage)
        {
            if (enemy == null || rawDamage <= 0)
            {
                return Mathf.Max(1, rawDamage);
            }

            var sourceTowerKind = sourceTower != null ? sourceTower.Kind : TDTowerKind.RailLancer;
            var multiplier = 1f;
            if (enemy.HasTag("armored") && HasSupportAuraNearby(enemy, SupportAuraRadius))
            {
                multiplier *= 0.84f;
            }

            if (enemy.HasTag("boss") && HasSupportAuraNearby(enemy, SupportAuraRadius + 0.4f))
            {
                multiplier *= 0.90f;
            }

            if (enemy.IsMarked && sourceTowerKind != TDTowerKind.ResonanceBeacon)
            {
                multiplier *= 1.10f;
                if (sourceTowerKind == TDTowerKind.ArcWelder)
                {
                    multiplier *= 1.06f;
                }
            }

            if (sourceTowerKind == TDTowerKind.GravSnare && (enemy.HasTag("fast") || enemy.HasTag("flank")))
            {
                multiplier *= 1.12f;
            }

            if (IsResonanceWindowActive)
            {
                multiplier *= 1.08f;
                if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
                {
                    if (enemy.HasTag("armored"))
                    {
                        multiplier *= 1.32f;
                    }
                    else if (enemy.HasTag("heavy"))
                    {
                        multiplier *= 1.28f;
                    }
                    else if (enemy.HasTag("fast"))
                    {
                        multiplier *= 1.24f;
                    }
                    else
                    {
                        multiplier *= 1.16f;
                    }

                    if (sourceTowerKind == TDTowerKind.FrostCoil)
                    {
                        multiplier *= 1.08f;
                    }

                    if (sourceTowerKind == TDTowerKind.SiegeDrill && enemy.HasTag("armored"))
                    {
                        multiplier *= 1.08f;
                    }

                    if (sourceTowerKind == TDTowerKind.ResonanceBeacon)
                    {
                        multiplier *= 1.07f;
                    }

                    multiplier *= GetDoctrineCommandPowerMultiplier(TDResonanceCommand.FractureMark);
                }
            }

            multiplier *= GetSpecializationSynergyMultiplier(sourceTower, enemy);
            if (_matrixConvergenceTriggeredThisWindow && _activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= MatrixConvergenceEmberDamageMultiplier;
            }

            var adjusted = Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier));
            if (adjusted > rawDamage)
            {
                _resonanceBonusDamage += adjusted - rawDamage;
            }

            return adjusted;
        }

        private void RegisterTowerForAnalytics(TDTower tower)
        {
            GetOrCreateTowerStat(tower);
        }

        private TDTowerRuntimeStat GetOrCreateTowerStat(TDTower tower)
        {
            if (tower == null)
            {
                return null;
            }

            var towerId = tower.AnalyticsId;
            if (_towerStats.TryGetValue(towerId, out var stat))
            {
                return stat;
            }

            stat = new TDTowerRuntimeStat
            {
                towerId = towerId,
                kind = tower.Kind,
                cell = tower.GridCell,
                buildCost = TDTower.GetBuildCost(tower.Kind)
            };
            _towerStats[towerId] = stat;
            return stat;
        }

        private void RecordTowerUpgradeForAnalytics(TDTower tower, int cost)
        {
            var stat = GetOrCreateTowerStat(tower);
            if (stat == null)
            {
                return;
            }

            stat.upgrades++;
            stat.upgradeSpend += Mathf.Max(0, cost);
        }

        private void RegisterEnemySpawnForAnalytics(TDEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            var laneStat = GetOrCreateLaneStat(enemy.LaneKey);
            laneStat.spawned++;
            laneStat.spawnedHealth += Mathf.Max(1, enemy.MaxHealth);
            IncrementCounter(laneStat.enemySpawns, enemy.EnemyId);
        }

        private TDLaneRuntimeStat GetOrCreateLaneStat(string laneKey)
        {
            var normalized = string.IsNullOrWhiteSpace(laneKey)
                ? "default"
                : laneKey.Trim().ToLowerInvariant();
            if (_laneStats.TryGetValue(normalized, out var stat))
            {
                return stat;
            }

            stat = new TDLaneRuntimeStat
            {
                laneKey = normalized
            };
            _laneStats[normalized] = stat;
            return stat;
        }

        private TDRoadSegmentRuntimeStat GetOrCreateRoadSegmentStat(string laneKey, int segmentIndex)
        {
            var lane = string.IsNullOrWhiteSpace(laneKey) ? "default" : laneKey.Trim().ToLowerInvariant();
            var segment = Mathf.Clamp(segmentIndex, 0, RoadSegmentCount - 1);
            var key = $"{lane}:{segment}";
            if (_roadSegmentStats.TryGetValue(key, out var stat))
            {
                return stat;
            }

            stat = new TDRoadSegmentRuntimeStat
            {
                laneKey = lane,
                segmentIndex = segment
            };
            _roadSegmentStats[key] = stat;
            return stat;
        }

        private TDRoadSegmentRuntimeStat GetEnemyRoadSegmentStat(TDEnemy enemy)
        {
            var lane = enemy != null ? enemy.LaneKey : "default";
            var segment = enemy != null ? enemy.GetRoadSegmentIndex(RoadSegmentCount) : 0;
            return GetOrCreateRoadSegmentStat(lane, segment);
        }

        public void NotifyEnemyReachedRoadSegment(TDEnemy enemy, int segmentIndex)
        {
            if (_gameOver || enemy == null)
            {
                return;
            }

            GetOrCreateRoadSegmentStat(enemy.LaneKey, segmentIndex).reached++;
        }

        private void RecordUnresolvedEnemyAtRunEnd(TDEnemy enemy)
        {
            if (enemy != null)
            {
                GetEnemyRoadSegmentStat(enemy).unresolvedAtEnd++;
            }
        }

        private void RecordThreatCategoryDamage(TDTowerKind sourceTowerKind, TDEnemy enemy, int damageTaken)
        {
            if (enemy == null || damageTaken <= 0)
            {
                return;
            }

            RecordThreatCategoryDamage("speed", enemy.HasAnyTag("fast", "flank"), sourceTowerKind, damageTaken);
            RecordThreatCategoryDamage("swarm", enemy.HasAnyTag("swarm", "split"), sourceTowerKind, damageTaken);
            RecordThreatCategoryDamage("armor", enemy.HasAnyTag("armored", "heavy", "boss"), sourceTowerKind, damageTaken);
            RecordThreatCategoryDamage("attrition", enemy.HasAnyTag("support", "attrition"), sourceTowerKind, damageTaken);
        }

        private void RecordThreatCategoryDamage(string category, bool applies, TDTowerKind sourceTowerKind, int damageTaken)
        {
            if (!applies)
            {
                return;
            }

            IncrementCounter(_threatCategoryDamage, category, damageTaken);
            if (IsTowerCounterForCategory(sourceTowerKind, category))
            {
                IncrementCounter(_threatCategoryCounterDamage, category, damageTaken);
            }
        }

        private static bool IsTowerCounterForCategory(TDTowerKind kind, string category)
        {
            return category switch
            {
                "speed" => kind == TDTowerKind.FrostCoil || kind == TDTowerKind.EmberFlak || kind == TDTowerKind.GravSnare,
                "swarm" => kind == TDTowerKind.CinderMortar || kind == TDTowerKind.ArcWelder || kind == TDTowerKind.EmberFlak || kind == TDTowerKind.GravSnare,
                "armor" => kind == TDTowerKind.RailLancer || kind == TDTowerKind.SiegeDrill,
                "attrition" => kind == TDTowerKind.ResonanceBeacon || kind == TDTowerKind.GravSnare || kind == TDTowerKind.FrostCoil,
                _ => false
            };
        }

        private static bool IsCounterOpportunity(TDEnemy enemy)
        {
            return enemy != null && enemy.HasAnyTag(
                "fast", "flank", "swarm", "split", "armored", "heavy", "boss", "support", "attrition");
        }

        private static bool IsTowerCounterForEnemy(TDTowerKind kind, TDEnemy enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            if (enemy.HasAnyTag("fast", "flank") &&
                (kind == TDTowerKind.FrostCoil || kind == TDTowerKind.EmberFlak || kind == TDTowerKind.GravSnare))
            {
                return true;
            }

            if (enemy.HasAnyTag("swarm", "split") &&
                (kind == TDTowerKind.CinderMortar || kind == TDTowerKind.ArcWelder || kind == TDTowerKind.EmberFlak || kind == TDTowerKind.GravSnare))
            {
                return true;
            }

            if (enemy.HasAnyTag("armored", "heavy", "boss") &&
                (kind == TDTowerKind.RailLancer || kind == TDTowerKind.SiegeDrill))
            {
                return true;
            }

            return enemy.HasAnyTag("support", "attrition") &&
                   (kind == TDTowerKind.ResonanceBeacon || kind == TDTowerKind.GravSnare || kind == TDTowerKind.FrostCoil);
        }

        public void NotifyEnemyDamaged(TDTower sourceTower, TDEnemy enemy, int damageTaken, float appliedSlowPct, float appliedSlowDuration)
        {
            if (_gameOver || damageTaken <= 0)
            {
                return;
            }

            var sourceTowerKind = sourceTower != null ? sourceTower.Kind : TDTowerKind.RailLancer;
            _totalDamageDealt += damageTaken;
            if (_currentWaveStat != null)
            {
                _currentWaveStat.damageDealt += damageTaken;
            }

            var laneStat = GetOrCreateLaneStat(enemy?.LaneKey);
            laneStat.damageDealt += damageTaken;
            var roadSegmentStat = GetEnemyRoadSegmentStat(enemy);
            roadSegmentStat.damageDealt += damageTaken;

            var towerStat = GetOrCreateTowerStat(sourceTower);
            if (towerStat != null)
            {
                towerStat.hits++;
                towerStat.damageDealt += damageTaken;
                IncrementCounter(towerStat.laneDamage, laneStat.laneKey, damageTaken);
                if (appliedSlowPct > 0f && appliedSlowDuration > 0f)
                {
                    towerStat.controlApplications++;
                    towerStat.controlStrengthSeconds += appliedSlowPct * appliedSlowDuration;
                    roadSegmentStat.controlApplications++;
                }
            }

            var matchedCounter = false;
            if (IsCounterOpportunity(enemy))
            {
                _counterOpportunityDamage += damageTaken;
                if (IsTowerCounterForEnemy(sourceTowerKind, enemy))
                {
                    matchedCounter = true;
                    _counterMatchedDamage += damageTaken;
                    if (towerStat != null)
                    {
                        towerStat.counterDamage += damageTaken;
                    }
                }
            }

            if (matchedCounter)
            {
                roadSegmentStat.counterDamage += damageTaken;
            }

            RecordThreatCategoryDamage(sourceTowerKind, enemy, damageTaken);

            if (enemy != null)
            {
                var isBossDamage = enemy.HasAnyTag("boss", "final", "elite");
                var isCriticalHit = !isBossDamage &&
                                    ((sourceTower != null && sourceTower.IsDamageSpecialist &&
                                      (matchedCounter || enemy.IsMarked)) ||
                                     damageTaken >= Mathf.Max(18, Mathf.RoundToInt(enemy.MaxHealth * 0.14f)));
                var feedbackKind = isBossDamage
                    ? TDBattleFeedbackKind.BossDamage
                    : isCriticalHit ? TDBattleFeedbackKind.CriticalHit : TDBattleFeedbackKind.Hit;
                var feedbackTier = isBossDamage || isCriticalHit
                    ? TDBattleFeedbackTier.Tactical
                    : TDBattleFeedbackTier.Routine;
                _battlePresentation?.EmitFeedback(
                    feedbackKind,
                    enemy.transform.position,
                    damageTaken.ToString(),
                    feedbackTier);
                if (isBossDamage && Time.unscaledTime >= _nextBossDamageFeedbackAudioTime)
                {
                    _nextBossDamageFeedbackAudioTime = Time.unscaledTime + 0.28f;
                    var bossFrequency = Mathf.Lerp(210f, 310f, Mathf.Clamp01(damageTaken / 160f));
                    PlaySfxTone($"feedback_boss_damage_{Mathf.RoundToInt(bossFrequency / 25f)}", bossFrequency, 0.12f, 0.54f, false);
                }
                else if (isCriticalHit && Time.unscaledTime >= _nextCriticalHitFeedbackAudioTime)
                {
                    _nextCriticalHitFeedbackAudioTime = Time.unscaledTime + 0.20f;
                    var criticalFrequency = Mathf.Lerp(820f, 1080f, Mathf.Clamp01(damageTaken / 120f));
                    PlaySfxTone($"feedback_critical_{Mathf.RoundToInt(criticalFrequency / 40f)}", criticalFrequency, 0.09f, 0.58f, true);
                }
                else if (!isBossDamage && !isCriticalHit && Time.unscaledTime >= _nextHitFeedbackAudioTime)
                {
                    _nextHitFeedbackAudioTime = Time.unscaledTime + 0.10f;
                    var hitFrequency = Mathf.Lerp(560f, 820f, Mathf.Clamp01(damageTaken / 80f));
                    PlaySfxTone($"feedback_hit_{Mathf.RoundToInt(hitFrequency / 40f)}", hitFrequency, 0.055f, 0.20f, true);
                }
            }

            var towerFactor = sourceTowerKind switch
            {
                TDTowerKind.CinderMortar => 0.85f,
                TDTowerKind.FrostCoil => 1.12f,
                TDTowerKind.ArcWelder => 0.95f,
                TDTowerKind.SiegeDrill => 1.05f,
                TDTowerKind.EmberFlak => 0.92f,
                TDTowerKind.ResonanceBeacon => 1.42f,
                TDTowerKind.GravSnare => 1.18f,
                _ => 1f
            };

            var gain = Mathf.Max(ResonanceHitChargeMin, damageTaken * ResonanceHitChargePerDamage) * towerFactor;
            if (appliedSlowPct > 0f)
            {
                gain += 0.35f;
            }

            if (enemy != null && enemy.IsMarked && sourceTowerKind != TDTowerKind.ResonanceBeacon)
            {
                gain += 0.30f;
            }

            if (sourceTowerKind == TDTowerKind.ResonanceBeacon)
            {
                gain += 0.45f;
            }

            AddResonanceCharge(gain);
        }

        public void NotifyTowerFired(TDTowerKind kind)
        {
            if (_gameOver || Time.unscaledTime < _nextTowerFireAudioTime)
            {
                return;
            }

            _nextTowerFireAudioTime = Time.unscaledTime + 0.055f;
            var profile = kind switch
            {
                TDTowerKind.RailLancer => (frequency: 760f, duration: 0.055f, volume: 0.30f, rising: true),
                TDTowerKind.CinderMortar => (frequency: 240f, duration: 0.115f, volume: 0.42f, rising: false),
                TDTowerKind.FrostCoil => (frequency: 610f, duration: 0.085f, volume: 0.32f, rising: false),
                TDTowerKind.ArcWelder => (frequency: 920f, duration: 0.060f, volume: 0.28f, rising: true),
                TDTowerKind.SiegeDrill => (frequency: 180f, duration: 0.130f, volume: 0.46f, rising: false),
                TDTowerKind.EmberFlak => (frequency: 520f, duration: 0.045f, volume: 0.27f, rising: true),
                TDTowerKind.ResonanceBeacon => (frequency: 680f, duration: 0.105f, volume: 0.34f, rising: true),
                TDTowerKind.GravSnare => (frequency: 155f, duration: 0.145f, volume: 0.40f, rising: false),
                _ => (frequency: 520f, duration: 0.070f, volume: 0.30f, rising: true)
            };
            PlaySfxTone(
                $"tower_fire_{kind.ToString().ToLowerInvariant()}",
                profile.frequency,
                profile.duration,
                profile.volume,
                profile.rising);
        }

        public void NotifyEnemyArmorBroken(TDEnemy enemy, int breakAmount)
        {
            if (_gameOver || enemy == null)
            {
                return;
            }

            RecordEnemyCodexObservation(enemy.EnemyId, TDEnemyCodexObservation.ArmorBroken);

            _battlePresentation?.EmitFeedback(
                TDBattleFeedbackKind.ArmorBreak,
                enemy.transform.position,
                $"-{Mathf.Max(1, breakAmount)}",
                TDBattleFeedbackTier.Tactical);
            if (Time.unscaledTime >= _nextArmorBreakFeedbackAudioTime)
            {
                _nextArmorBreakFeedbackAudioTime = Time.unscaledTime + 0.22f;
                PlaySfxTone("feedback_armor_break", 330f, 0.14f, 0.62f, false);
            }
        }

        public void NotifyEnemySlowed(TDEnemy enemy, float slowPct)
        {
            if (_gameOver || enemy == null)
            {
                return;
            }

            RecordEnemyCodexObservation(enemy.EnemyId, TDEnemyCodexObservation.Slowed);

            _battlePresentation?.EmitFeedback(
                TDBattleFeedbackKind.Slow,
                enemy.transform.position,
                $"{Mathf.RoundToInt(Mathf.Clamp01(slowPct) * 100f)}%",
                TDBattleFeedbackTier.Tactical);
            if (Time.unscaledTime >= _nextSlowFeedbackAudioTime)
            {
                _nextSlowFeedbackAudioTime = Time.unscaledTime + 0.36f;
                PlaySfxTone("feedback_slow", 460f, 0.12f, 0.46f, false);
            }
        }

        public void PlayEnemySfx(string key, float volumeScale = 1f)
        {
            PlaySfxTone(key, 440f, 0.12f, volumeScale, false);
        }

        public void NotifyEnemyKilled(TDEnemy enemy, int reward, TDTower sourceTower)
        {
            _activeEnemies.Remove(enemy);
            if (_gameOver)
            {
                return;
            }

            if (enemy != null && sourceTower?.ActiveSpecialization != null &&
                (DoesEnemyMatchSpecialization(enemy, sourceTower.ActiveSpecialization) || enemy.IsMarked))
            {
                RecordEnemyCodexObservation(enemy.EnemyId, TDEnemyCodexObservation.CounterKilled);
            }

            var combatReward = TDEconomyTuning.ScaleCombatBounty(reward, _wave, GetConfiguredWaveCount());
            _defenseBudget += combatReward;
            TrackP125CombatIncome(combatReward);
            _totalKills++;
            GetOrCreateLaneStat(enemy?.LaneKey).kills++;
            GetEnemyRoadSegmentStat(enemy).kills++;
            var towerStat = GetOrCreateTowerStat(sourceTower);
            if (towerStat != null)
            {
                towerStat.kills++;
            }

            PlaySfxTone("enemy_death", 380f, 0.10f, 0.22f, false);

            if (enemy != null && enemy.EnemyId == "spore_carrier")
            {
                _spawnSplitEvents++;
                PushTacticalEvent("Split spawn: Spore Carrier released Ash Swarm x2", 5.0f);
                StartCoroutine(SpawnSplitChildren("ash_swarm", 2, 0.22f, enemy.LaneKey));
                PlaySfxTone("enemy_spore_split", 300f, 0.22f, 0.62f, false);
            }
            else if (enemy != null && enemy.EnemyId == "furnace_matriarch")
            {
                _spawnSplitEvents++;
                PushTacticalEvent("Boss split: Furnace Matriarch released Ash Swarm x6", 5.4f);
                StartCoroutine(SpawnSplitChildren("ash_swarm", 6, 0.16f, enemy.LaneKey));
                PlaySfxTone("enemy_spore_split", 280f, 0.26f, 0.68f, false);
            }

            AddResonanceCharge(ResonanceKillCharge);

            if (IsResonanceWindowActive && _activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                _resonanceWindowTimer = Mathf.Min(ResonanceWindowDuration, _resonanceWindowTimer + 0.28f);
            }

            if (_currentWaveStat != null)
            {
                _currentWaveStat.kills++;
            }
        }

        public void NotifyEnemyEscaped(TDEnemy enemy, int lineDamage, string enemyId)
        {
            _activeEnemies.Remove(enemy);
            if (_gameOver)
            {
                return;
            }

            RecordEnemyCodexObservation(enemyId, TDEnemyCodexObservation.Leaked);

            _totalEscapes++;
            if (_currentWaveStat != null)
            {
                _currentWaveStat.escapes++;
            }

            var laneStat = GetOrCreateLaneStat(enemy?.LaneKey);
            laneStat.escapes++;
            var roadSegmentStat = GetEnemyRoadSegmentStat(enemy);
            roadSegmentStat.escapes++;

            var failureReason = ClassifyFailureReason(enemy);
            IncrementCounter(_failureReasonCounts, failureReason);
            if (_currentWaveStat != null)
            {
                IncrementCounter(_currentWaveStat.failureReasons, failureReason);
            }

            var extraBudgetLoss = 0;
            var resonanceDrain = 0f;
            if (enemy != null && enemy.HasTag("attrition"))
            {
                extraBudgetLoss = AttritionBudgetPenalty;
                resonanceDrain = _isResonanceSystemEnabled ? AttritionResonanceDrain : 0f;
                _attritionPenaltyEvents++;
            }

            var requestedIntegrityDamage = Mathf.Max(1, lineDamage);
            var integrityBefore = _lineIntegrity;
            _lineIntegrity = Mathf.Max(0, _lineIntegrity - requestedIntegrityDamage);
            var appliedIntegrityDamage = integrityBefore - _lineIntegrity;
            _totalIntegrityDamageTaken += appliedIntegrityDamage;
            laneStat.integrityDamageTaken += appliedIntegrityDamage;
            roadSegmentStat.integrityDamageTaken += appliedIntegrityDamage;
            if (_currentWaveStat != null)
            {
                _currentWaveStat.integrityDamageTaken += appliedIntegrityDamage;
            }
            if (extraBudgetLoss > 0)
            {
                _defenseBudget = Mathf.Max(0, _defenseBudget - extraBudgetLoss);
            }

            if (resonanceDrain > 0f)
            {
                _resonanceCharge = Mathf.Max(0f, _resonanceCharge - resonanceDrain);
            }

            var attritionLabel = extraBudgetLoss > 0
                ? (_isResonanceSystemEnabled
                    ? $" | Attrition -{extraBudgetLoss} budget, -{resonanceDrain:0} resonance"
                    : $" | Attrition -{extraBudgetLoss} budget")
                : string.Empty;
            SetStatus($"Leak: {enemyId} dealt {appliedIntegrityDamage} integrity damage [{failureReason}]{attritionLabel}");
            PushTacticalEvent($"Leak: {GetEnemyDisplayName(enemyId)} -{appliedIntegrityDamage} integrity [{failureReason}]", 5.8f);
            _battlePresentation?.EmitFeedback(
                TDBattleFeedbackKind.Leak,
                enemy != null ? enemy.transform.position : Vector3.zero,
                $"-{appliedIntegrityDamage}",
                TDBattleFeedbackTier.Critical);
            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.DefenseBreach,
                "[!]",
                "DEFENSE BREACH",
                $"{GetEnemyDisplayName(enemyId)}  /  INTEGRITY {_lineIntegrity}",
                TDBattleFeedbackTier.Critical,
                1.25f);
            if (Time.unscaledTime >= _nextLeakFeedbackAudioTime)
            {
                _nextLeakFeedbackAudioTime = Time.unscaledTime + 0.18f;
                PlayCriticalSfxTone(extraBudgetLoss > 0 ? "leak_attrition" : "leak_default", extraBudgetLoss > 0 ? 180f : 240f, 0.18f, 0.74f, false);
            }

            if (!_criticalDefenseCueShown && _lineIntegrity > 0 && _lineIntegrity <= Mathf.CeilToInt(_startingLineIntegrity * 0.35f))
            {
                _criticalDefenseCueShown = true;
                _battlePresentation?.ShowCinematic(
                    TDBattleCinematicKind.DefenseBreach,
                    "[!!]",
                    "CRITICAL DEFENSE",
                    $"INTEGRITY {_lineIntegrity}/{_startingLineIntegrity}  /  HOLD EXIT",
                    TDBattleFeedbackTier.Critical,
                    1.45f);
                PlayCriticalSfxTone("critical_defense", 145f, 0.32f, 0.92f, false);
            }

            if (_lineIntegrity > 0)
            {
                return;
            }

            FinalizeCurrentWaveStat(false);
            _gameOver = true;
            _victory = false;
            ResetResonanceState();
            ClearActiveEnemiesAfterRun();
            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
            }

            PlayCriticalSfxTone("run_defeat", 150f, 0.28f, 0.90f, false);
            RecordCampaignResultIfNeeded();
            LogRunSummary();
        }

        private void ClearActiveEnemiesAfterRun()
        {
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy != null && enemy.gameObject != null)
                {
                    RecordUnresolvedEnemyAtRunEnd(enemy);
                    enemy.gameObject.SetActive(false);
                    Destroy(enemy.gameObject);
                }
            }

            _activeEnemies.Clear();
        }

        private bool IsResonanceWindowActive => _isResonanceSystemEnabled && _resonanceWindowTimer > 0f;

        private void UpdateResonanceState()
        {
            if (!_isResonanceSystemEnabled)
            {
                ResetResonanceState();
                return;
            }

            if (!IsResonanceWindowActive)
            {
                return;
            }

            _resonanceWindowTimer = Mathf.Max(0f, _resonanceWindowTimer - Time.deltaTime);
            _resonanceCharge = Mathf.Clamp(
                ResonanceChargeMax * (_resonanceWindowTimer / ResonanceWindowDuration),
                0f,
                ResonanceChargeMax);

            if (_resonanceWindowTimer <= 0f)
            {
                EndResonanceWindow();
                return;
            }

            if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
            {
                _resonanceMarkPulseTimer -= Time.deltaTime;
                if (_resonanceMarkPulseTimer <= 0f)
                {
                    PulseResonanceMarks();
                    _resonanceMarkPulseTimer = ResonanceMarkPulseInterval;
                }
            }
        }

        private void AddResonanceCharge(float amount)
        {
            if (!_isResonanceSystemEnabled || amount <= 0f || IsResonanceWindowActive || _gameOver)
            {
                return;
            }

            _resonanceCharge = Mathf.Clamp(
                _resonanceCharge + (amount * _missionResonanceGainMultiplier),
                0f,
                ResonanceChargeMax);
            if (_resonanceCharge >= ResonanceChargeMax)
            {
                BeginResonanceWindow();
            }
        }

        private void BeginResonanceWindow()
        {
            if (!_isResonanceSystemEnabled)
            {
                return;
            }

            _resonanceWindowsTriggered++;
            ResetMatrixWindowState();
            _resonanceWindowTimer = ResonanceWindowDuration;
            _resonanceCharge = ResonanceChargeMax;
            _activeResonanceCommand = TDResonanceCommand.None;
            _resonanceMarkPulseTimer = 0f;
            SetStatus("Resonance ready: press [Z] Ember Surge or [X] Fracture Mark");
            PlaySfxTone("resonance_ready", 700f, 0.22f, 0.90f, true);
        }

        private void EndResonanceWindow()
        {
            var missedCommand = _activeResonanceCommand == TDResonanceCommand.None;
            CaptureMatrixWindowPeak();
            _resonanceWindowTimer = 0f;
            _resonanceCharge = 0f;
            _activeResonanceCommand = TDResonanceCommand.None;
            _resonanceMarkPulseTimer = 0f;
            if (missedCommand)
            {
                _resonanceChainMatchStreak = 0;
            }

            ResetMatrixWindowState();

            SetStatus("Resonance window ended");
            PlaySfxTone("resonance_end", 290f, 0.17f, 0.60f, false);
        }

        private void ResetMatrixWindowState()
        {
            _matrixWindowSync = 0;
            _matrixWindowSpecializationIds.Clear();
            _matrixConvergenceTriggeredThisWindow = false;
        }

        private void TrySelectResonanceCommand(TDResonanceCommand command)
        {
            if (!_isResonanceSystemEnabled)
            {
                return;
            }

            if (!IsResonanceWindowActive || command == TDResonanceCommand.None)
            {
                return;
            }

            if (_activeResonanceCommand != TDResonanceCommand.None)
            {
                SetStatus($"Resonance command locked: {GetResonanceCommandLabel(_activeResonanceCommand)}");
                PlaySfxTone("resonance_locked", 220f, 0.10f, 0.42f, false);
                return;
            }

            _activeResonanceCommand = command;
            _resonanceCommandsUsed++;
            if (command == TDResonanceCommand.EmberSurge)
            {
                _emberSurgeUses++;
                PlaySfxTone("resonance_ember_surge", 550f, 0.18f, 0.78f, true);
            }
            else if (command == TDResonanceCommand.FractureMark)
            {
                _fractureMarkUses++;
                PulseResonanceMarks();
                _resonanceMarkPulseTimer = ResonanceMarkPulseInterval;
                PlaySfxTone("resonance_fracture_mark", 420f, 0.19f, 0.76f, false);
            }

            var threatMatched = IsResonanceCommandMatchForCurrentThreat(command);
            if (threatMatched)
            {
                _resonanceMatchedCommands++;
            }

            var resonanceTarget = _activeEnemies.FirstOrDefault(enemy => enemy != null);
            _battlePresentation?.EmitFeedback(
                TDBattleFeedbackKind.Resonance,
                resonanceTarget != null ? resonanceTarget.transform.position : Vector3.zero,
                threatMatched ? "MATCH" : GetResonanceCommandShortLabel(command),
                threatMatched ? TDBattleFeedbackTier.Critical : TDBattleFeedbackTier.Tactical);

            var doctrinePower = GetDoctrineCommandPowerMultiplier(command);
            if (doctrinePower > 1f)
            {
                _doctrineEmpoweredCommands++;
                PushTacticalEvent(
                    $"{GetDoctrineShortLabel(_activeResonanceDoctrine)} doctrine amplified {GetResonanceCommandShortLabel(command)} +{Mathf.RoundToInt((doctrinePower - 1f) * 100f)}%",
                    5.0f);
            }

            var chainBonusTriggered = TryApplyResonanceChainBonus(command);
            if (!chainBonusTriggered)
            {
                var chainLabel = threatMatched
                    ? $"Match {_resonanceChainMatchStreak}/{ResonanceChainRequiredMatches}"
                    : "NoMatch (streak reset)";
                SetStatus($"Resonance command engaged: {GetResonanceCommandLabel(command)} [{chainLabel}]");
            }
        }

        private bool TryApplyResonanceChainBonus(TDResonanceCommand command)
        {
            if (command == TDResonanceCommand.None || !IsResonanceCommandMatchForCurrentThreat(command))
            {
                _resonanceChainMatchStreak = 0;
                return false;
            }

            _resonanceChainMatchStreak++;
            if (_resonanceChainMatchStreak < ResonanceChainRequiredMatches)
            {
                return false;
            }

            _resonanceChainMatchStreak = 0;
            _resonanceChainBonusTriggers++;

            var budgetBonus = command == TDResonanceCommand.EmberSurge
                ? ResonanceChainBudgetBonusOnEmberSurge
                : ResonanceChainBudgetBonusOnFractureMark;
            var integrityBonus = command == TDResonanceCommand.FractureMark
                ? ResonanceChainIntegrityBonusOnFractureMark
                : 0;
            var integrityDelta = 0;

            if (budgetBonus > 0)
            {
                _defenseBudget += budgetBonus;
                _resonanceChainBudgetBonusTotal += budgetBonus;
            }

            if (integrityBonus > 0)
            {
                var before = _lineIntegrity;
                _lineIntegrity = Mathf.Min(_startingLineIntegrity, _lineIntegrity + integrityBonus);
                integrityDelta = _lineIntegrity - before;
                _resonanceChainIntegrityBonusTotal += integrityDelta;
            }

            var integrityLabel = integrityDelta > 0 ? $" +{integrityDelta} integrity" : string.Empty;
            SetStatus($"Resonance Chain Bonus triggered: +{budgetBonus} budget{integrityLabel}");
            PlaySfxTone("resonance_chain_bonus", 840f, 0.26f, 1.0f, true);
            return true;
        }

        private bool IsResonanceCommandMatchForCurrentThreat(TDResonanceCommand command)
        {
            if (_currentWaveThreatTagSet.Count == 0)
            {
                return false;
            }

            return command switch
            {
                TDResonanceCommand.EmberSurge => HasAnyThreatPattern(EmberSurgeThreatPatterns),
                TDResonanceCommand.FractureMark => HasAnyThreatPattern(FractureMarkThreatPatterns),
                _ => false
            };
        }

        private bool HasAnyThreatPattern(string[] patterns)
        {
            if (patterns == null || patterns.Length == 0 || _currentWaveThreatTagSet.Count == 0)
            {
                return false;
            }

            foreach (var tag in _currentWaveThreatTagSet)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                for (var i = 0; i < patterns.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(patterns[i]) &&
                        tag.IndexOf(patterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void PulseResonanceMarks()
        {
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                enemy.SetResonanceMark(ResonanceMarkDuration);
            }
        }

        private string GetResonanceHudLabel()
        {
            if (!_isResonanceSystemEnabled)
            {
                return $"Resonance locked until level {_resonanceEnabledFromLevel:00}";
            }

            if (IsResonanceWindowActive)
            {
                if (_activeResonanceCommand == TDResonanceCommand.None)
                {
                    return $"Resonance {_resonanceWindowTimer:0.0}s  Choose Ember [Z] or Fracture [X]";
                }

                var convergence = _matrixConvergenceTriggeredThisWindow ? "  CONVERGENCE" : string.Empty;
                return $"Resonance {_resonanceWindowTimer:0.0}s  {GetResonanceCommandShortLabel(_activeResonanceCommand)}  Sync {_matrixWindowSync}/{MatrixConvergenceRequiredMatches} S{_matrixWindowSpecializationIds.Count}/{MatrixConvergenceRequiredSpecializations}{convergence}";
            }

            return $"Resonance Charge {_resonanceCharge:0}/{ResonanceChargeMax:0}  Doctrine {GetDoctrineShortLabel(_activeResonanceDoctrine)}  Chain {_resonanceChainBonusTriggers}";
        }

        private void ResetResonanceState()
        {
            CaptureMatrixWindowPeak();
            _resonanceWindowTimer = 0f;
            _resonanceCharge = 0f;
            _activeResonanceCommand = TDResonanceCommand.None;
            _resonanceMarkPulseTimer = 0f;
            _resonanceChainMatchStreak = 0;
            ResetMatrixWindowState();
        }

        private static string GetResonanceCommandLabel(TDResonanceCommand command)
        {
            return command switch
            {
                TDResonanceCommand.EmberSurge => "Ember Surge (all towers burst fire)",
                TDResonanceCommand.FractureMark => "Fracture Mark (vulnerability by enemy tags)",
                _ => "None"
            };
        }

        private void ConfigureCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                _mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            _mainCamera.orthographic = true;
            var safeAspect = Mathf.Max(1f, Screen.width / (float)Mathf.Max(1, Screen.height));
            var widthFitSize = 8.5f / safeAspect;
            _mainCamera.orthographicSize = Mathf.Max(4.8f, widthFitSize);
            _mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = new Color(0.035f, 0.038f, 0.040f);
        }

        private void ConfigureSfx()
        {
            LoadAudioMixer();

            _sfxSource = GetComponent<AudioSource>();
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
            }

            _tacticalSfxSource = gameObject.AddComponent<AudioSource>();
            _criticalSfxSource = gameObject.AddComponent<AudioSource>();
            ConfigureSfxSource(_sfxSource, 0f);
            ConfigureSfxSource(_tacticalSfxSource, 0f);
            ConfigureSfxSource(_criticalSfxSource, 0f);

            _musicSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource = gameObject.AddComponent<AudioSource>();
            ConfigureLoopSource(_musicSource);
            ConfigureLoopSource(_ambienceSource);

            RouteAudioSourcesToMixer();
            ApplySfxVolumes();
        }

        private void LoadAudioMixer()
        {
            _emberlineMixer = Resources.Load<AudioMixer>(AudioBasePath + "/EmberlineMixer");
            if (_emberlineMixer == null)
            {
                return;
            }

            var groups = _emberlineMixer.FindMatchingGroups(string.Empty);
            foreach (var group in groups)
            {
                if (group.name == "Music") _mixerMusicGroup = group;
                else if (group.name == "SFX") _mixerSfxGroup = group;
                else if (group.name == "Ambience") _mixerAmbienceGroup = group;
            }
        }

        private void RouteAudioSourcesToMixer()
        {
            if (_emberlineMixer == null)
            {
                return;
            }

            if (_mixerMusicGroup != null && _musicSource != null)
            {
                _musicSource.outputAudioMixerGroup = _mixerMusicGroup;
            }

            if (_mixerAmbienceGroup != null && _ambienceSource != null)
            {
                _ambienceSource.outputAudioMixerGroup = _mixerAmbienceGroup;
            }

            // Route SFX sources to sub-groups if they exist, otherwise the main SFX group.
            if (_mixerSfxGroup != null)
            {
                // GetChildGroups returns the immediate child AudioMixerGroups of this group.
                var subGroups = _emberlineMixer.FindMatchingGroups("SFX");
                RouteSfxSource(_sfxSource, subGroups, "Routine");
                RouteSfxSource(_tacticalSfxSource, subGroups, "Tactical");
                RouteSfxSource(_criticalSfxSource, subGroups, "Critical");
            }
        }

        private void RouteSfxSource(AudioSource source, AudioMixerGroup[] subGroups, string groupName)
        {
            if (source == null)
            {
                return;
            }

            foreach (var sg in subGroups)
            {
                if (sg.name == groupName)
                {
                    source.outputAudioMixerGroup = sg;
                    return;
                }
            }

            source.outputAudioMixerGroup = _mixerSfxGroup;
        }

        private void ApplySfxVolumes()
        {
            var mix = Mathf.Clamp01(_masterVolume) * Mathf.Clamp01(_effectsVolume);
            if (_sfxSource != null)
            {
                _sfxSource.volume = SfxDefaultVolume * 0.78f * mix;
            }

            if (_tacticalSfxSource != null)
            {
                _tacticalSfxSource.volume = SfxDefaultVolume * mix;
            }

            if (_criticalSfxSource != null)
            {
                _criticalSfxSource.volume = SfxDefaultVolume * 1.12f * mix;
            }

            var musicMix = Mathf.Clamp01(_masterVolume) * Mathf.Clamp01(_musicVolume);
            if (_musicSource != null)
            {
                _musicSource.volume = 0.42f * musicMix;
            }

            if (_ambienceSource != null)
            {
                _ambienceSource.volume = 0.55f * musicMix;
            }
        }

        private static void ConfigureSfxSource(AudioSource source, float volume)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = Mathf.Clamp01(volume);
        }

        private static void ConfigureLoopSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
        }

        private static string ResolveMapAmbiencePath(string mapId)
        {
            return mapId switch
            {
                "grayline_junction" => "Ambience/grayline_junction",
                "ashfall_depot" => "Ambience/ashfall_depot",
                "split_switch_canyon" => "Ambience/split_switch_canyon",
                "hollow_kiln_basin" => "Ambience/hollow_kiln_basin",
                "last_ember_terminus" => "Ambience/last_ember_terminus",
                _ => "Ambience/grayline_junction",
            };
        }

        private string ResolveChapterMusicPath()
        {
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
            var chapter = Mathf.Clamp((levelIndex - 1) / 5, 0, 3);
            return chapter switch
            {
                0 => "Music/combat_chapter_a",
                1 => "Music/combat_chapter_b",
                2 => "Music/combat_chapter_c",
                _ => "Music/combat_chapter_d",
            };
        }

        private void StartAmbienceForMap(string mapId)
        {
            if (_ambienceSource == null)
            {
                return;
            }

            var path = ResolveMapAmbiencePath(mapId);
            if (_ambienceClip == null || _ambienceClip.name != System.IO.Path.GetFileNameWithoutExtension(path))
            {
                _ambienceClip = Resources.Load<AudioClip>(AudioBasePath + "/" + path);
            }

            if (_ambienceClip != null && _ambienceSource.clip != _ambienceClip)
            {
                _ambienceSource.clip = _ambienceClip;
                _ambienceSource.Play();
            }
        }

        private void UpdateMusicState()
        {
            if (_musicSource == null)
            {
                return;
            }

            string targetState;
            string targetPath;

            if (_gameOver)
            {
                targetState = _victory ? "victory" : "defeat";
                targetPath = _victory ? "Music/victory_stinger" : "Music/defeat_stinger";
            }
            else if (_missionBoardOpen)
            {
                targetState = "menu";
                targetPath = "Music/menu_theme";
            }
            else if (IsResonanceWindowActive)
            {
                targetState = "resonance";
                targetPath = "Music/resonance_window";
            }
            else
            {
                targetState = "combat";
                targetPath = ResolveChapterMusicPath();
            }

            if (_activeMusicState == targetState && _musicClip != null)
            {
                return;
            }

            _activeMusicState = targetState;
            TransitionMusicSnapshot(targetState);
            var newClip = Resources.Load<AudioClip>(AudioBasePath + "/" + targetPath);
            if (newClip == null)
            {
                return;
            }

            // Stingers (victory/defeat) play once and do not loop; everything else loops.
            var isStinger = targetState == "victory" || targetState == "defeat";
            _musicSource.loop = !isStinger;
            _musicClip = newClip;
            _musicSource.clip = newClip;
            _musicSource.Play();
        }

        /// <summary>
        /// Transition the AudioMixer snapshot based on the current music state.
        /// Ducking: boss/resonance states lower music volume so SFX cut through.
        /// Falls back gracefully if no mixer or snapshots are configured.
        /// </summary>
        private void TransitionMusicSnapshot(string state)
        {
            if (_emberlineMixer == null)
            {
                return;
            }

            var snapshotName = state switch
            {
                "resonance" => "Resonance",
                "victory" => "Victory",
                "defeat" => "Defeat",
                "menu" => "Normal",
                _ => "Normal",
            };

            var snapshot = _emberlineMixer.FindSnapshot(snapshotName);
            snapshot?.TransitionTo(0.8f);
        }

        private void PlaySfxTone(string key, float frequency, float duration, float volumeScale = 1f, bool rising = false)
        {
            if (_sfxSource == null || volumeScale <= 0f || duration <= 0f || frequency <= 0f)
            {
                return;
            }

            if (!_sfxClipCache.TryGetValue(key, out var clip) || clip == null)
            {
                var resourcePath = ResolveSfxResourcePath(key);
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    clip = Resources.Load<AudioClip>(AudioBasePath + "/" + resourcePath);
                }

                if (clip == null)
                {
                    clip = CreateSfxClip(key, frequency, duration, rising);
                }

                if (clip == null)
                {
                    return;
                }

                _sfxClipCache[key] = clip;
            }

            var source = IsRoutineSfxKey(key) ? _sfxSource : _tacticalSfxSource;
            source?.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private void PlayCriticalSfxTone(string key, float frequency, float duration, float volumeScale = 1f, bool rising = false)
        {
            if (_criticalSfxSource == null || volumeScale <= 0f || duration <= 0f || frequency <= 0f)
            {
                return;
            }

            if (!_sfxClipCache.TryGetValue(key, out var clip) || clip == null)
            {
                var resourcePath = ResolveSfxResourcePath(key);
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    clip = Resources.Load<AudioClip>(AudioBasePath + "/" + resourcePath);
                }

                if (clip == null)
                {
                    clip = CreateSfxClip(key, frequency, duration, rising);
                }

                if (clip == null)
                {
                    return;
                }

                _sfxClipCache[key] = clip;
            }

            _criticalSfxSource.Stop();
            _criticalSfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private string ResolveSfxResourcePath(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            // Exact-key matches first (most specific).
            switch (key)
            {
                case "tower_build":
                    return "SFX/UI/tower_place";
                case "wave_start":
                case "wave_transition":
                    return "SFX/UI/wave_start";
                case "wave_clear":
                    return "SFX/UI/wave_clear";
                case "run_victory":
                    return "Music/victory_stinger";
                case "run_defeat":
                    return "Music/defeat_stinger";
                case "resonance_ready":
                    return "SFX/Resonance/window_open";
                case "resonance_end":
                    return "SFX/Resonance/window_close";
                case "resonance_locked":
                    return "SFX/Resonance/window_close";
                case "resonance_ember_surge":
                    return "SFX/Resonance/ember_surge";
                case "resonance_fracture_mark":
                    return "SFX/Resonance/fracture_mark";
                case "resonance_chain_bonus":
                    return "SFX/Resonance/matrix_convergence";
                case "boss_phase":
                    return "SFX/Enemy/boss_phase_shift";
                case "boss_warning":
                    return "SFX/Enemy/boss_spawn";
                case "critical_defense":
                    return "SFX/Hit/boss_hit";
                case "enemy_death":
                    return "SFX/Enemy/death_generic";
                case "enemy_spore_split":
                    return "SFX/Enemy/spore_split";
                case "enemy_mimic_shift":
                    return "SFX/Enemy/mimic_shift";
                case "enemy_burrow_ambush":
                    return "SFX/Enemy/burrow_ambush";
                case "enemy_elite_pressure":
                    return "SFX/Enemy/elite_pressure";
                case "enemy_attrition":
                    return "SFX/Enemy/attrition_siphon";
                case "enemy_support_link":
                    return "SFX/Enemy/support_link";
                case "status_expose":
                    return "SFX/Status/expose_mark";
                case "specialization_ult":
                    return "SFX/Status/specialization_ult";
                case "feedback_armor_break":
                case "p121_armor_break":
                case "p134_armor_break":
                    return "SFX/Status/armor_break";
                case "feedback_slow":
                case "p121_slow_control":
                case "p134_slow":
                    return "SFX/Status/slow_apply";
                case "feedback_special_damage":
                case "feedback_special_utility":
                case "p134_specialization":
                case "p121_specialization":
                    return "SFX/Status/specialization_ult";
                case "leak_default":
                case "leak_attrition":
                case "p121_leak":
                case "p134_leak":
                    return "SFX/Enemy/enemy_leak";
                case "p134_boss_phase":
                    return "SFX/Enemy/boss_phase_shift";
                case "p134_boss_damage":
                    return "SFX/Hit/boss_hit";
                case "p134_defense_breach":
                    return "SFX/Hit/boss_hit";
                case "p134_danger_lane":
                case "danger_lane":
                    return "SFX/Scenario/route_switch";
                case "p134_critical_hit":
                case "feedback_critical":
                    return "SFX/Hit/critical_hit";
                case "p134_hit":
                case "p121_feedback_hit":
                case "feedback_hit":
                    return "SFX/Hit/routine_hit";
                case "p134_resonance":
                case "p121_resonance":
                    return "SFX/Resonance/window_open";
                case "p134_wave_transition":
                    return "SFX/UI/wave_start";
                case "scenario_command":
                    return "SFX/Scenario/route_switch";
                case "scenario_reinforcement":
                    return "SFX/Scenario/reinforcement_train";
                case "ui_hover":
                    return "SFX/UI/hover";
                case "ui_click":
                    return "SFX/UI/click_confirm";
                case "ui_panel_open":
                    return "SFX/UI/panel_open";
                case "ui_panel_close":
                    return "SFX/UI/panel_close";
                case "ui_level_select":
                    return "SFX/UI/level_select";
                case "ui_deploy":
                    return "SFX/UI/deploy_confirm";
                case "ui_early_dispatch":
                    return "SFX/UI/early_dispatch";
                case "ui_tutorial_advance":
                    return "SFX/UI/tutorial_advance";
                case "ui_tutorial_complete":
                    return "SFX/UI/tutorial_complete";
                case "ui_chapter_reward":
                    return "SFX/UI/chapter_reward";
                case "scenario_route_switch":
                    return "SFX/Scenario/route_switch";
                case "scenario_reinforcement_train":
                    return "SFX/Scenario/reinforcement_train";
                case "scenario_kiln_purge":
                    return "SFX/Scenario/kiln_purge";
                case "scenario_boss_breaker":
                    return "SFX/Scenario/boss_breaker";
                case "scenario_signal_gate":
                    return "SFX/Scenario/signal_gate";
            }

            // Dynamic-key families: tower fire / upgrade / exam beats / matrix convergence.
            var lower = key.ToLowerInvariant();

            if (lower.StartsWith("tower_fire_", StringComparison.Ordinal))
            {
                return ResolveTowerFirePath(lower);
            }

            if (lower.StartsWith("tower_upgrade_", StringComparison.Ordinal))
            {
                return "SFX/UI/tower_upgrade";
            }

            if (lower.StartsWith("feedback_hit", StringComparison.Ordinal) ||
                lower.StartsWith("p121_feedback_hit", StringComparison.Ordinal))
            {
                return "SFX/Hit/routine_hit";
            }

            if (lower.StartsWith("feedback_critical", StringComparison.Ordinal) ||
                lower.StartsWith("p134_critical", StringComparison.Ordinal))
            {
                return "SFX/Hit/critical_hit";
            }

            if (lower.StartsWith("feedback_boss_damage", StringComparison.Ordinal) ||
                lower.StartsWith("p134_boss_damage", StringComparison.Ordinal))
            {
                return "SFX/Hit/boss_hit";
            }

            if (lower.StartsWith("matrix_convergence", StringComparison.Ordinal))
            {
                return "SFX/Resonance/matrix_convergence";
            }

            // Exam presentation beats map to scenario/level-select for now.
            if (lower.StartsWith("exam_", StringComparison.Ordinal))
            {
                return "SFX/UI/level_select";
            }

            return null;
        }

        private static string ResolveTowerFirePath(string lowerKey)
        {
            // tower_fire_<kind> -> SFX/Tower/fire_<snake_kind>
            var token = lowerKey.Substring("tower_fire_".Length);
            return token switch
            {
                "raillancer" => "SFX/Tower/fire_rail_lancer",
                "cindermortar" => "SFX/Tower/fire_cinder_mortar",
                "frostcoil" => "SFX/Tower/fire_frost_coil",
                "arcwelder" => "SFX/Tower/fire_arc_welder",
                "siegedrill" => "SFX/Tower/fire_siege_drill",
                "emberflak" => "SFX/Tower/fire_ember_flak",
                "resonancebeacon" => "SFX/Tower/fire_resonance_beacon",
                "gravsnare" => "SFX/Tower/fire_grav_snare",
                _ => null,
            };
        }

        private static AudioClip CreateSfxClip(string key, float frequency, float duration, bool rising)
        {
            var sampleCount = Mathf.Max(64, Mathf.CeilToInt(duration * SfxSampleRate));
            var data = new float[sampleCount];
            var phase = 0f;
            var metallic = SfxKeyContains(key, "hit") || SfxKeyContains(key, "armor") || SfxKeyContains(key, "build");
            var controlled = SfxKeyContains(key, "slow") || SfxKeyContains(key, "fracture");
            var percussive = SfxKeyContains(key, "mortar") || SfxKeyContains(key, "flak") ||
                             SfxKeyContains(key, "siege");
            var energized = SfxKeyContains(key, "arcwelder") || SfxKeyContains(key, "frostcoil") ||
                            SfxKeyContains(key, "resonancebeacon") || SfxKeyContains(key, "gravsnare");
            var alarm = SfxKeyContains(key, "boss") || SfxKeyContains(key, "leak") ||
                        SfxKeyContains(key, "defeat") || SfxKeyContains(key, "critical");
            var harmonic = SfxKeyContains(key, "resonance") || SfxKeyContains(key, "upgrade") ||
                           SfxKeyContains(key, "victory") || SfxKeyContains(key, "convergence");
            var noiseState = 2166136261u;
            for (var i = 0; i < key.Length; i++)
            {
                noiseState = (noiseState ^ key[i]) * 16777619u;
            }

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / Mathf.Max(1f, sampleCount - 1f);
                var currentFrequency = rising
                    ? Mathf.Lerp(frequency * 0.75f, frequency * 1.25f, t)
                    : Mathf.Lerp(frequency * 1.12f, frequency * 0.88f, t);
                phase += (2f * Mathf.PI * currentFrequency) / SfxSampleRate;

                noiseState = (noiseState * 1664525u) + 1013904223u;
                var noise = (((noiseState >> 8) & 0x00FFFFFF) / 8388607.5f) - 1f;
                var voice = Mathf.Sin(phase);
                if (harmonic)
                {
                    voice = (voice * 0.66f) + (Mathf.Sin(phase * 2.01f) * 0.23f) +
                            (Mathf.Sin(phase * 0.5f) * 0.18f);
                }

                if (metallic)
                {
                    voice = (voice * 0.50f) + (Mathf.Sin(phase * 2.73f) * 0.20f) +
                            (noise * (1f - t) * 0.38f);
                }

                if (controlled)
                {
                    voice = (voice * 0.62f) + (Mathf.Sin(phase * 0.52f) * 0.28f) + (noise * 0.06f);
                }

                if (percussive)
                {
                    voice = (voice * 0.46f) + (Mathf.Sin(phase * 0.31f) * 0.22f) +
                            (noise * (1f - t) * 0.32f);
                }

                if (energized)
                {
                    voice = (voice * 0.58f) + (Mathf.Sin(phase * 1.73f) * 0.20f) +
                            (Mathf.Sin(phase * 3.11f) * 0.12f);
                }

                if (alarm)
                {
                    var pulse = 0.72f + (Mathf.Sin(t * Mathf.PI * 6f) * 0.18f);
                    voice = ((Mathf.Sin(phase * 0.50f) * 0.58f) + (voice * 0.34f) + (noise * 0.08f)) * pulse;
                }

                var attack = Mathf.Clamp01(t / (metallic ? 0.025f : 0.08f));
                var release = Mathf.Clamp01((1f - t) / (alarm ? 0.34f : 0.22f));
                var envelope = attack * release;
                data[i] = Mathf.Clamp(voice * envelope * 0.42f, -0.92f, 0.92f);
            }

            var clip = AudioClip.Create($"td_sfx_{key}", sampleCount, 1, SfxSampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static bool IsRoutineSfxKey(string key)
        {
            return SfxKeyContains(key, "feedback_hit") || SfxKeyContains(key, "tower_build") ||
                   SfxKeyContains(key, "tower_fire");
        }

        private static bool SfxKeyContains(string key, string token)
        {
            return !string.IsNullOrEmpty(key) && key.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BuildBoard()
        {
            var boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(transform, false);

            var mapId = _campaignRoute?.level?.mapId ?? "grayline_junction";
            ConfigureActiveLanePaths(mapId);
            StartAmbienceForMap(mapId);
            var roadPaths = _activeLanePaths.Values
                .Where(path => path != null && path.Count > 1)
                .Distinct()
                .ToArray();
            _gridMap = new TDGridMap(
                GridWidth,
                GridHeight,
                CellSize,
                GetPathCellsForMap(mapId),
                boardRoot,
                mapId,
                roadPaths);
            BuildExamScenarioDevice(boardRoot);
        }

        private void BuildExamScenarioDevice(Transform boardRoot)
        {
            _examPresentationProfile = null;
            _examScenarioDevice = null;
            _examPresentationStage = TDExamPresentationStage.Dormant;
            var level = _campaignRoute?.level;
            if (level?.scenario?.milestoneExam != true ||
                !TDExamPresentationCatalog.TryGet(level.levelIndex, out _examPresentationProfile))
            {
                return;
            }

            var deviceObject = new GameObject("Exam Scenario Device");
            deviceObject.transform.SetParent(boardRoot, false);
            _examScenarioDevice = deviceObject.AddComponent<TDExamScenarioDeviceView>();
            var devicePosition = _gridMap.CellToWorld(_examPresentationProfile.deviceCell);
            var maximumCharges = _campaignRoute?.map?.mechanic?.maxCharges ?? 0;
            _examScenarioDevice.Initialize(_examPresentationProfile, devicePosition, maximumCharges);
        }

        private static IReadOnlyList<Vector2Int> GetPathCellsForMap(string mapId)
        {
            return ConvertLayoutCellsToUnityCells(GetLayoutPathCellsForMap(mapId));
        }

        private static IReadOnlyList<Vector2Int> GetLayoutPathCellsForMap(string mapId)
        {
            return mapId switch
            {
                "ashfall_depot" => AshfallBuildPathCells,
                "split_switch_canyon" => SplitSwitchBuildPathCells,
                "hollow_kiln_basin" => HollowKilnBuildPathCells,
                "last_ember_terminus" => LastEmberBuildPathCells,
                _ => GraylinePathCells
            };
        }

        private void ConfigureActiveLanePaths(string mapId)
        {
            _activeLanePaths.Clear();
            var basePath = BuildWorldPathFromLayoutCells(GetLayoutPathCellsForMap(mapId));
            _activeLanePaths["default"] = basePath;
            _activeLanePaths["center"] = basePath;
            _activeLanePaths["all"] = basePath;

            if (string.Equals(mapId, "grayline_junction", StringComparison.OrdinalIgnoreCase))
            {
                var roadPath = BuildWorldPathFromLayoutPoints(GraylineRoadPathPoints);
                if (roadPath.Count > 1)
                {
                    _activeLanePaths["default"] = roadPath;
                    _activeLanePaths["center"] = roadPath;
                    _activeLanePaths["all"] = roadPath;
                }

                return;
            }

            if (string.Equals(mapId, "split_switch_canyon", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromLayoutPoints(SplitSwitchCenterRoadPathPoints);
                var leftPath = BuildWorldPathFromLayoutPoints(SplitSwitchLeftRoadPathPoints);
                var rightPath = BuildWorldPathFromLayoutPoints(SplitSwitchRightRoadPathPoints);
                var crossPath = BuildWorldPathFromLayoutPoints(SplitSwitchCrossRoadPathPoints);

                _activeLanePaths["default"] = centerPath;
                _activeLanePaths["center"] = centerPath;
                _activeLanePaths["left"] = leftPath;
                _activeLanePaths["right"] = rightPath;
                _activeLanePaths["split_lane"] = leftPath;
                _activeLanePaths["cross_lane"] = crossPath;
                _activeLanePaths["all"] = centerPath;
                return;
            }

            if (string.Equals(mapId, "ashfall_depot", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromLayoutPoints(AshfallCenterRoadPathPoints);
                var leftPath = BuildWorldPathFromLayoutPoints(AshfallLeftRoadPathPoints);
                var rightPath = BuildWorldPathFromLayoutPoints(AshfallRightRoadPathPoints);
                var crossPath = BuildWorldPathFromLayoutPoints(AshfallCrossRoadPathPoints);

                _activeLanePaths["default"] = centerPath;
                _activeLanePaths["center"] = centerPath;
                _activeLanePaths["left"] = leftPath;
                _activeLanePaths["right"] = rightPath;
                _activeLanePaths["split_lane"] = leftPath;
                _activeLanePaths["cross_lane"] = crossPath;
                _activeLanePaths["all"] = centerPath;
                return;
            }

            if (string.Equals(mapId, "hollow_kiln_basin", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromLayoutPoints(HollowKilnCenterRoadPathPoints);
                var leftPath = BuildWorldPathFromLayoutPoints(HollowKilnLeftRoadPathPoints);
                var rightPath = BuildWorldPathFromLayoutPoints(HollowKilnRightRoadPathPoints);
                var crossPath = BuildWorldPathFromLayoutPoints(HollowKilnCrossRoadPathPoints);

                _activeLanePaths["default"] = centerPath;
                _activeLanePaths["center"] = centerPath;
                _activeLanePaths["left"] = leftPath;
                _activeLanePaths["right"] = rightPath;
                _activeLanePaths["split_lane"] = leftPath;
                _activeLanePaths["cross_lane"] = crossPath;
                _activeLanePaths["all"] = centerPath;
                return;
            }

            if (string.Equals(mapId, "last_ember_terminus", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromLayoutPoints(LastEmberCenterRoadPathPoints);
                var leftPath = BuildWorldPathFromLayoutPoints(LastEmberLeftRoadPathPoints);
                var rightPath = BuildWorldPathFromLayoutPoints(LastEmberRightRoadPathPoints);
                var crossPath = BuildWorldPathFromLayoutPoints(LastEmberCrossRoadPathPoints);

                _activeLanePaths["default"] = centerPath;
                _activeLanePaths["center"] = centerPath;
                _activeLanePaths["left"] = leftPath;
                _activeLanePaths["right"] = rightPath;
                _activeLanePaths["split_lane"] = leftPath;
                _activeLanePaths["cross_lane"] = crossPath;
                _activeLanePaths["all"] = centerPath;
            }
        }

        private static Vector2Int[] CombinePathCells(params Vector2Int[][] pathSets)
        {
            var combined = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            if (pathSets == null)
            {
                return combined.ToArray();
            }

            for (var p = 0; p < pathSets.Length; p++)
            {
                var set = pathSets[p];
                if (set == null)
                {
                    continue;
                }

                for (var i = 0; i < set.Length; i++)
                {
                    var cell = set[i];
                    if (seen.Add(cell))
                    {
                        combined.Add(cell);
                    }
                }
            }

            return combined.ToArray();
        }

        private static Vector2Int[] ConvertLayoutCellsToUnityCells(IReadOnlyList<Vector2Int> layoutCells)
        {
            var converted = new List<Vector2Int>(layoutCells?.Count ?? 0);
            if (layoutCells == null)
            {
                return converted.ToArray();
            }

            for (var i = 0; i < layoutCells.Count; i++)
            {
                converted.Add(LayoutCellToUnityCell(layoutCells[i]));
            }

            return converted.ToArray();
        }

        private static Vector2Int LayoutCellToUnityCell(Vector2Int cell)
        {
            return new Vector2Int(cell.x, GridHeight - 1 - cell.y);
        }

        private static Vector3 LayoutPointToWorld(Vector2 point)
        {
            var worldX = (-(GridWidth * CellSize) * 0.5f) + (point.x * CellSize);
            var worldY = ((GridHeight * CellSize) * 0.5f) - (point.y * CellSize);
            return new Vector3(worldX, worldY, 0f);
        }

        private static IReadOnlyList<Vector3> BuildWorldPathFromLayoutPoints(Vector2[] pathPoints)
        {
            var anchors = new List<Vector3>(pathPoints?.Length ?? 0);
            if (pathPoints == null)
            {
                return anchors;
            }

            for (var i = 0; i < pathPoints.Length; i++)
            {
                anchors.Add(LayoutPointToWorld(pathPoints[i]));
            }

            return BuildSmoothPath(anchors, 0.05f);
        }

        private static List<Vector3> BuildWorldPathFromLayoutCells(IReadOnlyList<Vector2Int> pathCells)
        {
            var points = new List<Vector3>(pathCells?.Count ?? 0);
            if (pathCells == null)
            {
                return points;
            }

            for (var i = 0; i < pathCells.Count; i++)
            {
                var cell = pathCells[i];
                points.Add(LayoutPointToWorld(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
            }

            return BuildSmoothPath(points, 0.05f).ToList();
        }

        private static IReadOnlyList<Vector3> BuildSmoothPath(IReadOnlyList<Vector3> anchors, float targetSpacing)
        {
            var result = new List<Vector3>();
            if (anchors == null || anchors.Count == 0)
            {
                return result;
            }

            if (anchors.Count == 1)
            {
                result.Add(anchors[0]);
                return result;
            }

            var spacing = Mathf.Clamp(targetSpacing, 0.04f, 0.25f);
            for (var i = 0; i < anchors.Count - 1; i++)
            {
                var p0 = anchors[Mathf.Max(0, i - 1)];
                var p1 = anchors[i];
                var p2 = anchors[i + 1];
                var p3 = anchors[Mathf.Min(anchors.Count - 1, i + 2)];
                var samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / spacing));
                for (var sample = 0; sample < samples; sample++)
                {
                    var t = sample / (float)samples;
                    var t2 = t * t;
                    var t3 = t2 * t;
                    var point = 0.5f *
                                ((2f * p1) +
                                 ((-p0 + p2) * t) +
                                 (((2f * p0) - (5f * p1) + (4f * p2) - p3) * t2) +
                                 ((-p0 + (3f * p1) - (3f * p2) + p3) * t3));
                    AppendPathPointWithMaximumSpacing(result, point, spacing);
                }
            }

            AppendPathPointWithMaximumSpacing(result, anchors[anchors.Count - 1], spacing);
            return result;
        }

        private static void AppendPathPointWithMaximumSpacing(List<Vector3> result, Vector3 point, float maximumSpacing)
        {
            if (result.Count == 0)
            {
                result.Add(point);
                return;
            }

            var previous = result[result.Count - 1];
            var distance = Vector3.Distance(previous, point);
            if (distance <= 0.0001f)
            {
                return;
            }

            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.01f, maximumSpacing)));
            for (var step = 1; step <= steps; step++)
            {
                result.Add(Vector3.Lerp(previous, point, step / (float)steps));
            }
        }

        private IReadOnlyList<Vector3> GetDefaultSpawnPath()
        {
            if (_activeLanePaths.TryGetValue("default", out var defaultPath) && defaultPath != null && defaultPath.Count > 1)
            {
                return defaultPath;
            }

            return _gridMap?.PathWorldPoints ?? Array.Empty<Vector3>();
        }

        private string ResolveSpawnLaneKey(TDWaveGroup group, string formation, int spawnIndex)
        {
            var lane = NormalizeGroupToken(group?.lane);
            if (string.IsNullOrEmpty(lane))
            {
                lane = formation switch
                {
                    "split_lane" => "split_lane",
                    "cross_lane" => "cross_lane",
                    _ => "default"
                };
            }

            if (_activeScenarioMechanic != null &&
                NormalizeGroupToken(_activeScenarioMechanic.mechanicType) == "route_switch" &&
                !string.Equals(_scenarioRouteBias, "center", StringComparison.Ordinal) &&
                (lane == "default" || lane == "center" || lane == "all" || lane == "split_lane" || lane == "cross_lane"))
            {
                lane = _scenarioRouteBias;
            }

            if (lane == "all")
            {
                lane = ResolveAllLaneKey(formation, spawnIndex);
            }

            return ResolveExistingLaneKey(lane);
        }

        private string ResolveExistingLaneKey(string laneKey)
        {
            var lane = NormalizeGroupToken(laneKey);
            if (string.IsNullOrEmpty(lane))
            {
                lane = "default";
            }

            if (lane == "split_lane" && !_activeLanePaths.ContainsKey(lane) && _activeLanePaths.ContainsKey("left"))
            {
                lane = "left";
            }
            else if (lane == "cross_lane" && !_activeLanePaths.ContainsKey(lane) && _activeLanePaths.ContainsKey("right"))
            {
                lane = "right";
            }

            if (_activeLanePaths.ContainsKey(lane))
            {
                return lane;
            }

            return "default";
        }

        private IReadOnlyList<Vector3> GetSpawnPathForLane(string laneKey)
        {
            var lane = ResolveExistingLaneKey(laneKey);
            if (_activeLanePaths.TryGetValue(lane, out var path) && path != null && path.Count > 1)
            {
                return path;
            }

            return GetDefaultSpawnPath();
        }

        private string ResolveAllLaneKey(string formation, int spawnIndex)
        {
            if (!_activeLanePaths.ContainsKey("left") || !_activeLanePaths.ContainsKey("right"))
            {
                return _activeLanePaths.ContainsKey("center") ? "center" : "default";
            }

            if (string.Equals(formation, "pressure_mix", StringComparison.Ordinal))
            {
                if (_activeLanePaths.ContainsKey("cross_lane"))
                {
                    switch (spawnIndex % 3)
                    {
                        case 0:
                            return "left";
                        case 1:
                            return "right";
                        default:
                            return "cross_lane";
                    }
                }

                return spawnIndex % 2 == 0 ? "left" : "right";
            }

            if (string.Equals(formation, "adaptive", StringComparison.Ordinal) && _activeLanePaths.ContainsKey("cross_lane"))
            {
                return spawnIndex % 2 == 0 ? "cross_lane" : "left";
            }

            return spawnIndex % 2 == 0 ? "left" : "right";
        }

        private static float GetFormationStartDelayOffset(string formation)
        {
            return formation switch
            {
                "flank_strike" => -0.28f,
                "flank_stream" => -0.12f,
                "burst" => -0.08f,
                "elite_drop" => 0.24f,
                "boss_entry" => 0.48f,
                _ => 0f
            };
        }

        private float ResolveSpawnCadence(float baseInterval, string formation, int spawnIndex, int count)
        {
            var interval = Mathf.Max(baseInterval, _waveSet.globalDefaults.spawnMinSpacing);
            var safeCount = Mathf.Max(1, count);

            switch (formation)
            {
                case "pack":
                    interval *= (spawnIndex % 3 == 2) ? 1.30f : 0.72f;
                    break;
                case "burst":
                    interval *= (spawnIndex % 4 == 3) ? 1.45f : 0.58f;
                    break;
                case "stagger":
                    interval *= (spawnIndex % 2 == 0) ? 0.70f : 1.42f;
                    break;
                case "flank_stream":
                    interval *= 0.86f;
                    break;
                case "flank_strike":
                    interval *= (spawnIndex % 3 == 0) ? 0.62f : 0.92f;
                    break;
                case "pressure_mix":
                    switch (spawnIndex % 4)
                    {
                        case 0:
                            interval *= 0.78f;
                            break;
                        case 1:
                            interval *= 1.12f;
                            break;
                        case 2:
                            interval *= 0.90f;
                            break;
                        default:
                            interval *= 1.26f;
                            break;
                    }
                    break;
                case "adaptive":
                    var progress = safeCount <= 1 ? 1f : (float)spawnIndex / (safeCount - 1);
                    interval *= Mathf.Lerp(1.14f, 0.78f, progress);
                    break;
                case "spawn_chain":
                    interval *= spawnIndex % 2 == 0 ? 0.68f : 1.08f;
                    break;
                case "escort":
                    interval *= spawnIndex < safeCount / 2 ? 1.18f : 0.86f;
                    break;
                case "elite_drop":
                    interval *= spawnIndex % 2 == 0 ? 1.22f : 0.88f;
                    break;
                case "boss_entry":
                    interval *= 1.32f;
                    break;
                case "split_lane":
                    interval *= 0.94f;
                    break;
                case "cross_lane":
                    interval *= 0.90f;
                    break;
            }

            return Mathf.Max(_waveSet.globalDefaults.spawnMinSpacing, interval);
        }

        private static string NormalizeGroupToken(string token)
        {
            return string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim().ToLowerInvariant();
        }

        private void LoadEnemyCatalog()
        {
            _enemyCatalogError = string.Empty;
            _globalEnemyCatalog.Clear();

            if (!TDEnemyCatalogLoader.TryLoadFromResources(EnemyCatalogResourcePath, out var catalog, out var error))
            {
                _enemyCatalogError = error;
                Debug.LogWarning($"[TD] {error}");
                RefreshLoadError();
                return;
            }

            for (var i = 0; i < catalog.enemies.Length; i++)
            {
                var entry = catalog.enemies[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId) || _globalEnemyCatalog.ContainsKey(entry.enemyId))
                {
                    continue;
                }

                _globalEnemyCatalog.Add(entry.enemyId, CloneEnemyEntry(entry));
            }

            RefreshLoadError();
        }

        private void LoadCampaignContext()
        {
            ResetMissionRuntimeRules();
            _campaign = null;
            _campaignRoute = null;
            _campaignError = string.Empty;
            _waveResourcePath = DefaultWaveResourcePath;
            _missionBoardOpen = false;
            _campaignDeploymentConfirmed = true;
            ApplyCampaignGlobalRules(null, DefaultCampaignLevelIndex);

            if (!TDCampaignLoader.TryLoadFromResources(CampaignResourcePath, out var campaign, out var error))
            {
                _campaignError = error;
                RefreshUnlockedTowerKinds();
                RefreshLoadError();
                return;
            }

            var requestedLevel = TDCampaignRouter.GetSavedLevelIndex(DefaultCampaignLevelIndex);
            if (!TDCampaignRouter.TryResolveRoute(campaign, requestedLevel, out var route, out error))
            {
                _campaignError = error;
                RefreshUnlockedTowerKinds();
                RefreshLoadError();
                return;
            }

            _campaign = campaign;
            _campaignRoute = route;
            _waveResourcePath = route.waveResourcePath;
            TDCampaignProgression.EnsureInitialized(route.level.levelIndex, route.totalLevels);
            MigrateLegacyCodexDiscoveries();
            RefreshMetaProgressionRewards(false);
            _activeCampaignDifficulty = ResolveAvailableDifficulty(
                route.level.levelIndex,
                TDCampaignProgression.GetDifficultyPreference(route.level.levelIndex));
            _activeTacticalProtocol = GetTacticalProtocol(
                ResolveAvailableProtocolId(TDCampaignProgression.GetTacticalProtocol(route.level.levelIndex)));
            ConfigureScenarioMechanic(route.map?.mechanic, route.level.scenario);
            ApplyMissionRuntimeRules(route.level);
            _missionBoardSelectedLevel = route.level.levelIndex;
            _missionBoardOpen = true;
            _missionBoardNeedsRefresh = true;
            _campaignDeploymentConfirmed = false;
            ApplyCampaignGlobalRules(campaign, route.level.levelIndex);
            TDCampaignRouter.SaveLevelIndex(route.level.levelIndex);
            _campaignError = string.Empty;
            RefreshUnlockedTowerKinds();
            Debug.Log(
                $"[TD][Campaign] level={route.level.levelIndex} levelId={route.level.levelId} map={route.level.mapId} waveSet={route.level.waveSetId} " +
                $"difficulty={_activeCampaignDifficulty} contract={route.level.contract?.contractId ?? "none"} budget={_startingDefenseBudget} integrity={_startingLineIntegrity} " +
                $"hpX={_missionEnemyHpMultiplier:0.##} speedX={_missionEnemySpeedMultiplier:0.##} armor+={_missionEnemyArmorBonus} " +
                $"rewardX={_missionRewardMultiplier:0.##} resonanceX={_missionResonanceGainMultiplier:0.##}");
            RefreshLoadError();
        }

        private void ResetMissionRuntimeRules()
        {
            _startingDefenseBudget = DefaultDefenseBudget;
            _startingLineIntegrity = DefaultLineIntegrity;
            _defenseBudget = _startingDefenseBudget;
            _lineIntegrity = _startingLineIntegrity;
            _missionEnemyHpMultiplier = 1f;
            _missionEnemySpeedMultiplier = 1f;
            _missionEnemyArmorBonus = 0;
            _missionRewardMultiplier = 1f;
            _missionResonanceGainMultiplier = 1f;
            _missionPrepSecondsBonus = 0;
            _scenarioCostMultiplier = 1f;
            ResetP125EconomyTelemetry();
            _chapterRewardBudgetBonus = 0;
            _chapterRewardIntegrityBonus = 0;
            _chapterRewardResonanceMultiplier = 1f;
            _currentMissionContractCompleted = false;
            _newlyClaimedChapterReward = null;
            _contractFeedbackInitialized = false;
            _contractFeedbackTargetMet = false;
            _nextContractFeedbackTime = 0f;
            _criticalDefenseCueShown = false;
            _bossWarningWave = -1;
            _examPresentationStage = TDExamPresentationStage.Dormant;
            _examOpeningBeatCount = 0;
            _examEscalationBeatCount = 0;
            _examDecisionBeatCount = 0;
        }

        private void ConfigureScenarioMechanic(
            TDCampaignScenarioMechanicDefinition mechanic,
            TDCampaignScenarioPlan scenario)
        {
            _activeScenarioMechanic = mechanic;
            var intensity = Mathf.Clamp(scenario?.intensity ?? 1, 1, 3);
            _scenarioCharges = mechanic == null || mechanic.maxCharges <= 0
                ? mechanic?.maxCharges ?? 0
                : mechanic.maxCharges + (scenario?.milestoneExam == true ? 1 : 0) + Mathf.Max(0, intensity - 2);
            _scenarioRouteBias = "center";
            _scenarioUses = 0;
            _scenarioOpportunities = 0;
            _scenarioWaveDelayBonus = 0f;
            _scenarioReinforcementPending = false;
            _scenarioBossPhaseSuppressed = false;
            _scenarioBossPhase = 0;
        }

        private void ApplyMissionRuntimeRules(TDCampaignLevelDefinition level)
        {
            _startingDefenseBudget += GetCampaignStartingBudgetRamp(level?.levelIndex ?? 1);
            _startingLineIntegrity += GetCampaignStartingIntegrityRamp(level?.levelIndex ?? 1);
            var mutators = level?.mutators ?? Array.Empty<TDCampaignMutatorDefinition>();
            for (var i = 0; i < mutators.Length; i++)
            {
                ApplyRuntimeMutator(mutators[i]);
            }

            if (_activeCampaignDifficulty != TDCampaignDifficultyTier.Standard)
            {
                ApplyRuntimeMutator(GetDifficultyDefinition(_activeCampaignDifficulty)?.modifiers);
                ApplyRuntimeMutator(GetCampaignChapter(level?.chapterId)?.challengeRemix);
            }

            ApplyClaimedChapterRewardEffects();
            ApplyTacticalProtocolEffects(_activeTacticalProtocol);

            _startingDefenseBudget = Mathf.Max(0, _startingDefenseBudget);
            _startingLineIntegrity = Mathf.Max(1, _startingLineIntegrity);
            _defenseBudget = _startingDefenseBudget;
            _lineIntegrity = _startingLineIntegrity;
        }

        private int GetCampaignStartingBudgetRamp(int levelIndex)
        {
            var perLevel = Mathf.Max(0, _campaign?.globalRules?.startingBudgetPerLevel ?? 0);
            return Mathf.Max(0, levelIndex - 1) * perLevel;
        }

        private int GetCampaignStartingIntegrityRamp(int levelIndex)
        {
            var perChapter = Mathf.Max(0, _campaign?.globalRules?.startingIntegrityPerChapter ?? 0);
            return Mathf.Max(0, levelIndex - 1) / 5 * perChapter;
        }

        private void ApplyTacticalProtocolEffects(TDCampaignTacticalProtocolDefinition protocol)
        {
            if (protocol == null || string.Equals(protocol.protocolId, "baseline", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _startingDefenseBudget += protocol.startingBudgetDelta;
            _missionPrepSecondsBonus += Mathf.Max(0, protocol.prepSecondsDelta);
            _missionEnemyHpMultiplier *= ResolveMutatorMultiplier(protocol.enemyHpMultiplier);
            _missionRewardMultiplier *= ResolveMutatorMultiplier(protocol.rewardMultiplier);
            _scenarioCostMultiplier *= ResolveMutatorMultiplier(protocol.scenarioCostMultiplier);
            if (_activeScenarioMechanic != null && _activeScenarioMechanic.maxCharges > 0)
            {
                _scenarioCharges += Mathf.Max(0, protocol.scenarioChargeDelta);
            }
        }

        private void ApplyRuntimeMutator(TDCampaignMutatorDefinition mutator)
        {
            if (mutator == null)
            {
                return;
            }

            _startingDefenseBudget += mutator.startingBudgetDelta;
            _startingLineIntegrity += mutator.startingIntegrityDelta;
            _missionEnemyHpMultiplier *= ResolveMutatorMultiplier(mutator.enemyHpMultiplier);
            _missionEnemySpeedMultiplier *= ResolveMutatorMultiplier(mutator.enemySpeedMultiplier);
            _missionEnemyArmorBonus += mutator.enemyArmorBonus;
            _missionRewardMultiplier *= ResolveMutatorMultiplier(mutator.rewardMultiplier);
            _missionResonanceGainMultiplier *= ResolveMutatorMultiplier(mutator.resonanceGainMultiplier);
            _scenarioCostMultiplier *= ResolveMutatorMultiplier(mutator.scenarioCostMultiplier);
        }

        private void ApplyClaimedChapterRewardEffects()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            for (var i = 0; i < chapters.Length; i++)
            {
                var reward = chapters[i]?.reward;
                if (reward == null || !TDCampaignProgression.IsChapterRewardClaimed(reward.rewardId))
                {
                    continue;
                }

                _chapterRewardBudgetBonus += Mathf.Max(0, reward.startingBudgetBonus);
                _chapterRewardIntegrityBonus += Mathf.Max(0, reward.startingIntegrityBonus);
                _chapterRewardResonanceMultiplier *= ResolveMutatorMultiplier(reward.resonanceGainMultiplier);
            }

            _startingDefenseBudget += _chapterRewardBudgetBonus;
            _startingLineIntegrity += _chapterRewardIntegrityBonus;
            _missionResonanceGainMultiplier *= _chapterRewardResonanceMultiplier;
        }

        private static float ResolveMutatorMultiplier(float value)
        {
            return value > 0f ? value : 1f;
        }

        private void ApplyCampaignGlobalRules(TDCampaignDefinition campaign, int currentLevelIndex)
        {
            _maxFailureReasonsShown = DefaultMaxFailureReasonsShown;
            _resonanceEnabledFromLevel = DefaultResonanceEnabledFromLevel;
            _allowEarlyWaveDispatch = DefaultAllowEarlyWaveDispatch;

            var rules = campaign?.globalRules;
            if (rules != null)
            {
                if (rules.maxFailureReasonsShown > 0)
                {
                    _maxFailureReasonsShown = rules.maxFailureReasonsShown;
                }

                if (rules.resonanceEnabledFromLevel > 0)
                {
                    _resonanceEnabledFromLevel = rules.resonanceEnabledFromLevel;
                }

                _allowEarlyWaveDispatch = rules.allowEarlyWaveDispatch;
            }

            _isResonanceSystemEnabled = currentLevelIndex >= _resonanceEnabledFromLevel;
            if (!_isResonanceSystemEnabled)
            {
                ResetResonanceState();
            }
        }

        private void LoadWaveConfig()
        {
            if (!TDWaveLoader.TryLoadFromResources(_waveResourcePath, _globalEnemyCatalog, out _waveSet, out _waveError))
            {
                Debug.LogWarning($"[TD] {_waveError}");
                RefreshLoadError();
                return;
            }

            _enemyCatalog.Clear();

            foreach (var pair in _globalEnemyCatalog)
            {
                RegisterEnemyEntry(pair.Value);
            }

            for (var i = 0; i < _waveSet.enemyCatalog.Length; i++)
            {
                RegisterEnemyEntry(_waveSet.enemyCatalog[i]);
            }

            LoadPersistentCodexDiscoveries();
            _waveError = string.Empty;
            RefreshLoadError();
        }

        private void RegisterEnemyEntry(TDEnemyCatalogEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId))
            {
                return;
            }

            var normalized = CloneEnemyEntry(entry);
            if (normalized.lineDamage <= 0)
            {
                normalized.lineDamage = Mathf.Max(1, _waveSet.globalDefaults.lineDamageDefault);
            }

            _enemyCatalog[normalized.enemyId] = normalized;
        }

        private static TDEnemyCatalogEntry CloneEnemyEntry(TDEnemyCatalogEntry entry)
        {
            return new TDEnemyCatalogEntry
            {
                enemyId = entry.enemyId,
                displayName = entry.displayName,
                hp = entry.hp,
                speed = entry.speed,
                armorFlat = entry.armorFlat,
                rewardGold = entry.rewardGold,
                lineDamage = entry.lineDamage,
                threatCost = entry.threatCost,
                tags = entry.tags != null ? (string[])entry.tags.Clone() : Array.Empty<string>()
            };
        }

        private TDEnemyCatalogEntry BuildMissionEnemyEntry(TDEnemyCatalogEntry entry)
        {
            var runtimeEntry = CloneEnemyEntry(entry);
            runtimeEntry.hp = Mathf.Max(1, Mathf.RoundToInt(runtimeEntry.hp * _missionEnemyHpMultiplier));
            runtimeEntry.speed = Mathf.Max(0.05f, runtimeEntry.speed * _missionEnemySpeedMultiplier);
            runtimeEntry.armorFlat = Mathf.Max(0, runtimeEntry.armorFlat + _missionEnemyArmorBonus);
            runtimeEntry.rewardGold = ScaleMissionReward(runtimeEntry.rewardGold);
            return runtimeEntry;
        }

        private int ScaleMissionReward(int reward)
        {
            return reward <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(reward * _missionRewardMultiplier));
        }

        private static string GetResonanceCommandShortLabel(TDResonanceCommand command)
        {
            return command switch
            {
                TDResonanceCommand.EmberSurge => "Ember Surge",
                TDResonanceCommand.FractureMark => "Fracture Mark",
                _ => "Choose Command"
            };
        }

        private void LoadPersistentCodexDiscoveries()
        {
            _encounteredEnemyIds.Clear();
            foreach (var pair in _enemyCatalog)
            {
                if (TDCampaignProgression.GetEnemyObservationFlags(pair.Key) != 0 ||
                    PlayerPrefs.GetInt(BuildCodexPlayerPrefsKey(pair.Key), 0) > 0)
                {
                    _encounteredEnemyIds.Add(pair.Key);
                }
            }
        }

        private void MigrateLegacyCodexDiscoveries()
        {
            foreach (var pair in _globalEnemyCatalog)
            {
                if (PlayerPrefs.GetInt(BuildCodexPlayerPrefsKey(pair.Key), 0) > 0)
                {
                    TDCampaignProgression.RecordEnemyObservation(pair.Key, (int)TDEnemyCodexObservation.Sighted);
                }
            }
        }

        private void RecordEnemyCodexObservation(string enemyId, TDEnemyCodexObservation observation)
        {
            if (TDCampaignProgression.RecordEnemyObservation(enemyId, (int)observation))
            {
                RefreshMetaProgressionRewards(true);
            }
        }

        private void RecordTowerCodexObservation(TDTowerKind kind, TDTowerCodexObservation observation)
        {
            if (TDCampaignProgression.RecordTowerObservation(TDTower.GetTowerId(kind), (int)observation))
            {
                RefreshMetaProgressionRewards(true);
            }
        }

        private int GetCompletedEnemyDossierCount()
        {
            var count = 0;
            foreach (var pair in _globalEnemyCatalog)
            {
                var required = GetRequiredEnemyDossierFlags(pair.Value);
                if ((TDCampaignProgression.GetEnemyObservationFlags(pair.Key) & required) == required)
                {
                    count++;
                }
            }

            return count;
        }

        private static int GetRequiredEnemyDossierFlags(TDEnemyCatalogEntry entry)
        {
            var required = TDEnemyCodexObservation.Sighted | TDEnemyCodexObservation.CounterKilled;
            var tags = entry?.tags ?? Array.Empty<string>();
            if (tags.Any(tag => string.Equals(tag, "boss", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(tag, "final", StringComparison.OrdinalIgnoreCase)))
            {
                required |= TDEnemyCodexObservation.BossPhase;
            }
            else if (tags.Any(tag => string.Equals(tag, "armored", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(tag, "heavy", StringComparison.OrdinalIgnoreCase)))
            {
                required |= TDEnemyCodexObservation.ArmorBroken;
            }
            else
            {
                required |= TDEnemyCodexObservation.Slowed;
            }

            return (int)required;
        }

        private int GetCompletedTowerDossierCount()
        {
            var required = (int)(TDTowerCodexObservation.Built | TDTowerCodexObservation.DamageBranch |
                                 TDTowerCodexObservation.UtilityBranch | TDTowerCodexObservation.SpecializationProc);
            return TDTower.GetBuildOrder().Count(kind =>
                (TDCampaignProgression.GetTowerObservationFlags(TDTower.GetTowerId(kind)) & required) == required);
        }

        private void RefreshMetaProgressionRewards(bool showFeedback)
        {
            var meta = _campaign?.metaProgression;
            if (meta == null)
            {
                return;
            }

            var summary = GetCampaignProgressSummary();
            var enemyDossiers = GetCompletedEnemyDossierCount();
            var towerDossiers = GetCompletedTowerDossierCount();
            foreach (var reward in (meta.ratingRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>())
                         .Concat(meta.codexRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>()))
            {
                var current = reward.sourceType switch
                {
                    "campaign_stars" => summary.earnedStars,
                    "enemy_dossiers" => enemyDossiers,
                    "tower_dossiers" => towerDossiers,
                    _ => 0
                };
                if (current < reward.threshold || !TDCampaignProgression.ClaimMetaReward(reward.rewardId, reward.unlockProtocolId))
                {
                    continue;
                }

                if (showFeedback)
                {
                    PushTacticalEvent($"Meta reward: {reward.displayName} -> {GetTacticalProtocol(reward.unlockProtocolId)?.displayName}", 6.4f);
                }
            }
        }

        private static string BuildCodexPlayerPrefsKey(string enemyId)
        {
            return $"{CodexPlayerPrefsPrefix}{(string.IsNullOrWhiteSpace(enemyId) ? "unknown" : enemyId.Trim().ToLowerInvariant())}";
        }

        private int GetCodexDiscoveredCount()
        {
            var count = 0;
            foreach (var pair in _enemyCatalog)
            {
                if (_encounteredEnemyIds.Contains(pair.Key))
                {
                    count++;
                }
            }

            return count;
        }

        private int GetCodexTotalCount()
        {
            return Mathf.Max(0, _enemyCatalog.Count);
        }

        private void RefreshUnlockedTowerKinds()
        {
            _availableTowerKinds.Clear();
            _unlockedTowerKinds.Clear();
            var currentLevel = _campaignRoute?.level?.levelIndex ?? DefaultCampaignLevelIndex;
            _availableTowerKinds.AddRange(GetTowerKindsUnlockedAtLevel(currentLevel));
            _activeResonanceDoctrine = _campaignRoute?.level == null
                ? TDResonanceDoctrine.Adaptive
                : TDCampaignProgression.GetResonanceDoctrine(currentLevel);
            if (!IsDoctrineAvailableForLevel(currentLevel))
            {
                _activeResonanceDoctrine = TDResonanceDoctrine.Adaptive;
            }

            if (_campaignRoute?.level != null)
            {
                var savedIds = TDCampaignProgression.GetTowerLoadout(currentLevel);
                for (var i = 0; i < savedIds.Length && _unlockedTowerKinds.Count < TDCampaignProgression.MaxFormationTowers; i++)
                {
                    if (!TDTower.TryParseTowerId(savedIds[i], out var kind) ||
                        !_availableTowerKinds.Contains(kind) ||
                        _unlockedTowerKinds.Contains(kind))
                    {
                        continue;
                    }

                    _unlockedTowerKinds.Add(kind);
                }
            }

            if (_unlockedTowerKinds.Count == 0)
            {
                BuildAutoFitFormation(
                    currentLevel,
                    _availableTowerKinds,
                    out var fittedTowers,
                    out var fittedDoctrine);
                for (var i = 0; i < fittedTowers.Count && i < TDCampaignProgression.MaxFormationTowers; i++)
                {
                    _unlockedTowerKinds.Add(fittedTowers[i]);
                }

                _activeResonanceDoctrine = fittedDoctrine;
            }

            if (_unlockedTowerKinds.Count == 0)
            {
                _unlockedTowerKinds.Add(TDTowerKind.RailLancer);
            }

            if (!_unlockedTowerKinds.Contains(_selectedTowerKind))
            {
                _selectedTowerKind = _unlockedTowerKinds[0];
            }
        }

        private bool IsTowerUnlocked(TDTowerKind kind)
        {
            return _unlockedTowerKinds.Contains(kind);
        }

        private string GetBuildHotkeySummary()
        {
            var limit = Mathf.Min(_unlockedTowerKinds.Count, TowerHotkeys.Length);
            if (limit <= 0)
            {
                return "Build unavailable.";
            }

            var labels = new List<string>(limit);
            for (var i = 0; i < limit; i++)
            {
                var kind = _unlockedTowerKinds[i];
                var label = $"[{i + 1}] {TDTower.GetDisplayName(kind)} ({TDTower.GetBuildCost(kind)})";
                labels.Add(label);
            }

            return $"Build {string.Join("   ", labels)}";
        }

        private void RefreshLoadError()
        {
            var errors = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(_campaignError))
            {
                errors.Add(_campaignError);
            }

            if (!string.IsNullOrWhiteSpace(_enemyCatalogError))
            {
                errors.Add(_enemyCatalogError);
            }

            if (!string.IsNullOrWhiteSpace(_waveError))
            {
                errors.Add(_waveError);
            }

            if (errors.Count > 0)
            {
                _loadError = string.Join(" | ", errors);
                return;
            }

            _loadError = string.Empty;
        }

        private void HandleHotkeys()
        {
            for (var i = 0; i < TowerHotkeys.Length; i++)
            {
                if (!TDInputCompat.GetKeyDown(TowerHotkeys[i]))
                {
                    continue;
                }

                if (i >= _unlockedTowerKinds.Count)
                {
                    SetStatus($"Tower slot [{i + 1}] is locked.");
                    break;
                }

                _selectedTowerKind = _unlockedTowerKinds[i];
                break;
            }

            if (TDInputCompat.GetKeyDown(KeyCode.Q))
            {
                _selectedUpgradeBranch = TDTowerUpgradeBranch.Damage;
            }
            else if (TDInputCompat.GetKeyDown(KeyCode.E))
            {
                _selectedUpgradeBranch = TDTowerUpgradeBranch.Utility;
            }

            if (TDInputCompat.GetKeyDown(KeyCode.U))
            {
                TryUpgradeSelectedTowerFromUi(_selectedUpgradeBranch);
            }

            if (TDInputCompat.GetKeyDown(KeyCode.F5))
            {
                TryStepCampaignLevel(-1);
            }
            else if (TDInputCompat.GetKeyDown(KeyCode.F6))
            {
                TryStepCampaignLevel(1);
            }

            if (TDInputBindings.GetKeyDown(TDInputAction.StartWave) ||
                TDInputCompat.GetGamepadButtonDown(TDGamepadButton.North))
            {
                TryRequestWaveStart();
            }

            if (TDInputBindings.GetKeyDown(TDInputAction.ScenarioCommand) ||
                TDInputCompat.GetGamepadButtonDown(TDGamepadButton.West))
            {
                TryActivateScenarioMechanic();
            }

            if (!IsResonanceWindowActive)
            {
                return;
            }

            if (TDInputCompat.GetKeyDown(KeyCode.Z))
            {
                TrySelectResonanceCommand(TDResonanceCommand.EmberSurge);
            }
            else if (TDInputCompat.GetKeyDown(KeyCode.X))
            {
                TrySelectResonanceCommand(TDResonanceCommand.FractureMark);
            }
        }

        private void TryStepCampaignLevel(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            if (_campaignRoute?.level == null)
            {
                SetStatus("Campaign route unavailable. Using fallback wave set.");
                return;
            }

            var currentLevel = _campaignRoute.level.levelIndex;
            var nextLevel = Mathf.Clamp(currentLevel + delta, 1, _campaignRoute.totalLevels);
            if (nextLevel == currentLevel)
            {
                SetStatus($"Already at level {currentLevel:00}.");
                return;
            }

            if (nextLevel > currentLevel && !TDCampaignProgression.IsLevelUnlocked(nextLevel, _campaignRoute.totalLevels))
            {
                SetStatus($"Mission L{nextLevel:00} is locked. Clear L{currentLevel:00} first.");
                return;
            }

            TDCampaignRouter.SaveLevelIndex(nextLevel);
            SetStatus($"Switching to level {nextLevel:00}...");
            RestartCurrentScene();
        }

        private bool CanStartCurrentWave()
        {
            return _isInPrepPhase && !_gameOver && !IsOpeningWaveBuildRequired();
        }

        private bool IsOpeningWaveBuildRequired()
        {
            return _wave <= 1 && _wavesCleared == 0 && _builtTowerCount <= 0;
        }

        private void TryRequestWaveStart()
        {
            if (_gameOver || !_isInPrepPhase)
            {
                return;
            }

            if (IsOpeningWaveBuildRequired())
            {
                SetStatus("Build one tower before starting the first wave.");
                return;
            }

            if (_waveStartRequested)
            {
                return;
            }

            if (_prepDuration > 0f && _prepCountdown > 0.05f)
            {
                _waveDispatchedEarly = true;
                _earlyDispatchCount++;
                PlaySfxTone("ui_early_dispatch", 660f, 0.12f, 0.58f, true);
            }

            _waveStartRequested = true;
            _prepCountdown = 0f;
            SetStatus($"Wave {_wave} starting.");
            var readiness = CaptureWaveStartReadiness();
            PushTacticalEvent($"Dispatch W{_wave:00}: Ready {readiness.score} {readiness.grade} | {BuildWaveRouteLabel(_currentWaveDefinition)}", 5.6f);
            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.WaveTransition,
                "[W]",
                $"WAVE {_wave:00}  {_currentWavePhase}",
                $"READY {readiness.grade}  /  {BuildWaveRouteLabel(_currentWaveDefinition)}",
                TDBattleFeedbackTier.Tactical,
                1.05f);
            PlaySfxTone("wave_transition", 590f + Mathf.Min(180f, _wave * 12f), 0.13f, 0.58f, true);
            PlaySfxTone("wave_start", 640f, 0.11f, 0.58f, true);
            AdvanceTutorial(TDFirstRunTutorialStep.StartWave);
        }

        private string GetCampaignHudLabel()
        {
            if (_campaignRoute?.level == null)
            {
                return "Campaign route: fallback wave set (single-map mode)";
            }

            var level = _campaignRoute.level;
            var mapLabel = _campaignRoute.map != null && !string.IsNullOrWhiteSpace(_campaignRoute.map.displayName)
                ? _campaignRoute.map.displayName
                : level.mapId;

            return $"Campaign L{level.levelIndex:00}/{_campaignRoute.totalLevels:00}  {level.levelId}  Map {mapLabel}";
        }

        private string GetCompactCampaignHudLabel()
        {
            if (_campaignRoute?.level == null)
            {
                return "SKIRMISH";
            }

            var level = _campaignRoute.level;
            var mapLabel = _campaignRoute.map != null && !string.IsNullOrWhiteSpace(_campaignRoute.map.displayName)
                ? _campaignRoute.map.displayName
                : level.mapId;
            var cleanMap = string.IsNullOrWhiteSpace(mapLabel) ? "MAP" : mapLabel.ToUpperInvariant();
            return $"L{level.levelIndex:00}  {cleanMap}";
        }

        private void TryPlaceTowerAtCursor()
        {
            if (!IsBuildWindowOpen())
            {
                SetStatus("Build is disabled during combat. Wait for prep phase.");
                return;
            }

            if (!IsTowerUnlocked(_selectedTowerKind))
            {
                SetStatus($"{GetTowerKindLabel(_selectedTowerKind)} is not in the active formation.");
                return;
            }

            var towerCost = TDTower.GetBuildCost(_selectedTowerKind);
            if (_defenseBudget < towerCost)
            {
                SetStatus("Insufficient defense budget for this tower.");
                return;
            }

            var mouse = TDInputCompat.MousePosition;
            mouse.z = -_mainCamera.transform.position.z;
            var world = _mainCamera.ScreenToWorldPoint(mouse);
            world.z = 0f;

            if (!_gridMap.TryWorldToCell(world, out var cell))
            {
                return;
            }

            if (TryGetTowerUnderCursor(world, out var existingTower))
            {
                SelectTowerForUi(existingTower);
                SetStatus($"Selected {existingTower.DisplayName}.");
                return;
            }

            if (!_gridMap.IsBuildable(cell))
            {
                SetStatus("This cell is not buildable.");
                return;
            }

            _defenseBudget -= towerCost;
            _budgetSpentOnBuilds += towerCost;
            _gridMap.SetTower(cell, true);
            var tower = SpawnTower(cell, _selectedTowerKind);
            SelectTowerForUi(tower);
            _builtTowerCount++;
            PushTacticalEvent($"Build: {GetTowerKindLabel(_selectedTowerKind)} at {cell.x},{cell.y} (-{towerCost})", 4.2f);
            SetStatus(_wave <= 1 && _wavesCleared == 0 && _builtTowerCount == 1
                ? $"Built {GetTowerKindLabel(_selectedTowerKind)} (-{towerCost}). Press Start Wave."
                : $"Built {GetTowerKindLabel(_selectedTowerKind)} (-{towerCost} budget)");
            PlaySfxTone("tower_build", 420f, 0.10f, 0.55f, true);
            AdvanceTutorial(TDFirstRunTutorialStep.BuildTower);
        }

        private void TryUpgradeTowerAtCursor()
        {
            var mouse = TDInputCompat.MousePosition;
            mouse.z = -_mainCamera.transform.position.z;
            var world = _mainCamera.ScreenToWorldPoint(mouse);
            world.z = 0f;

            if (!TryGetTowerUnderCursor(world, out var tower))
            {
                return;
            }

            SelectTowerForUi(tower);
            TryUpgradeTower(tower, _selectedUpgradeBranch);
        }

        private void TryUpgradeTower(TDTower tower, TDTowerUpgradeBranch branch)
        {
            if (!IsBuildWindowOpen())
            {
                SetStatus("Upgrade is disabled during combat. Wait for prep phase.");
                return;
            }

            if (tower == null)
            {
                SetStatus("Select a tower before upgrading.");
                return;
            }

            if (!tower.CanUpgrade)
            {
                SetStatus("Tower is already at max tier.");
                return;
            }

            var upgradeCost = tower.GetUpgradeCost(branch);
            if (_defenseBudget < upgradeCost)
            {
                SetStatus($"Insufficient defense budget. Upgrade needs {upgradeCost}.");
                return;
            }

            if (!tower.ApplyUpgrade(branch))
            {
                SetStatus("Upgrade failed.");
                return;
            }

            RecordTowerCodexObservation(
                tower.Kind,
                branch == TDTowerUpgradeBranch.Damage
                    ? TDTowerCodexObservation.DamageBranch
                    : TDTowerCodexObservation.UtilityBranch);
            _defenseBudget -= upgradeCost;
            _budgetSpentOnUpgrades += upgradeCost;
            _upgradesPurchased++;
            RecordTowerUpgradeForAnalytics(tower, upgradeCost);
            TrackP135TowerUpgrade(tower);
            SelectTowerForUi(tower);
            PushTacticalEvent($"Upgrade: {tower.DisplayName} {GetUpgradeBranchLabel(branch)} ({tower.SpecializationLabel}) (-{upgradeCost})", 4.6f);
            SetStatus($"Upgraded {tower.DisplayName} [{GetUpgradeBranchLabel(branch)}] {tower.SpecializationLabel} (-{upgradeCost} budget)");
            var upgradeFrequency = 500f + ((int)tower.Kind * 34f) + (branch == TDTowerUpgradeBranch.Utility ? 42f : 0f);
            PlaySfxTone($"tower_upgrade_{tower.Kind.ToString().ToLowerInvariant()}", upgradeFrequency, 0.12f, 0.60f, true);
            AdvanceTutorial(TDFirstRunTutorialStep.UpgradeTower);
        }

        private bool TryGetTowerUnderCursor(Vector3 world, out TDTower tower)
        {
            tower = null;
            var hit = Physics2D.OverlapPoint(world);
            if (hit == null)
            {
                return false;
            }

            tower = hit.GetComponent<TDTower>() ?? hit.GetComponentInParent<TDTower>();
            return tower != null;
        }

        private TDTower SpawnTower(Vector2Int cell, TDTowerKind kind)
        {
            var towerObject = new GameObject($"Tower_{cell.x}_{cell.y}");
            towerObject.transform.position = _gridMap.CellToBuildWorld(cell);
            towerObject.transform.localScale = Vector3.one;
            towerObject.transform.SetParent(transform, true);

            var collider = towerObject.AddComponent<BoxCollider2D>();
            collider.size = GetTowerColliderSize(kind);
            collider.offset = GetTowerColliderOffset(kind);

            var tower = towerObject.AddComponent<TDTower>();
            tower.Initialize(this, kind, cell);
            RegisterTowerForAnalytics(tower);
            TrackP135TowerBuilt(tower);
            RecordTowerCodexObservation(kind, TDTowerCodexObservation.Built);
            return tower;
        }

        private void SpawnEnemy(
            TDEnemyCatalogEntry entry,
            IReadOnlyList<Vector3> path,
            int waveNumber,
            int enemyIndex,
            string laneKey = "default",
            bool registerEncounter = true)
        {
            if (registerEncounter)
            {
                RegisterEnemyEncounter(entry);
            }

            var runtimeEntry = BuildMissionEnemyEntry(entry);

            var enemyObject = new GameObject($"Enemy_{runtimeEntry.enemyId}_{waveNumber}_{enemyIndex}");
            enemyObject.transform.SetParent(transform, true);

            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(enemyObject.transform, false);

            var shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
            shadowRenderer.sortingOrder = Mathf.Max(0, GetEnemySortingOrder(runtimeEntry.enemyId) - 3);
            shadowRenderer.sprite = TDArtLibrary.GetSoftShadowSprite();
            shadowRenderer.color = new Color(0.05f, 0.04f, 0.06f, GetEnemyShadowAlpha(runtimeEntry.enemyId));

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(enemyObject.transform, false);
            visualObject.transform.localPosition = GetEnemyVisualOffset(runtimeEntry.enemyId);

            var renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = GetEnemySortingOrder(runtimeEntry.enemyId);
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback(GetEnemySpritePath(runtimeEntry.enemyId), GetEnemyFallbackColor(runtimeEntry.enemyId));
            var enemyMaterial = GetEnemyVisualMaterial(runtimeEntry.enemyId);
            if (enemyMaterial != null)
            {
                renderer.sharedMaterial = enemyMaterial;
            }
            visualObject.transform.localScale = ResolveSpriteScale(renderer.sprite, GetEnemyCellCoverage(runtimeEntry.enemyId));
            AlignEnemyVisualToRouteAnchor(renderer, visualObject.transform, runtimeEntry.enemyId);
            shadowObject.transform.localPosition = ResolveEnemyFootShadowOffset(
                renderer,
                visualObject.transform,
                runtimeEntry.enemyId);
            var shadowScale = ResolveSpriteScale(shadowRenderer.sprite, GetEnemyShadowCoverage(runtimeEntry.enemyId));
            shadowObject.transform.localScale = new Vector3(shadowScale.x, shadowScale.y * 0.42f, shadowScale.z);

            var animator = visualObject.AddComponent<TDSpriteAnimator>();
            animator.Configure(GetEnemyAnimationPrefix(runtimeEntry.enemyId), GetEnemyAnimationFrames(runtimeEntry.enemyId), GetEnemyAnimationFps(runtimeEntry.enemyId), true, true);

            var collider = enemyObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = GetEnemyColliderSize(runtimeEntry.enemyId);
            collider.offset = GetEnemyColliderOffset(runtimeEntry.enemyId);

            var enemy = enemyObject.AddComponent<TDEnemy>();
            enemy.Initialize(this, path ?? GetDefaultSpawnPath(), runtimeEntry, laneKey);
            _activeEnemies.Add(enemy);
            RegisterEnemySpawnForAnalytics(enemy);

            var isBossThreat = runtimeEntry.tags != null && runtimeEntry.tags.Any(tag =>
                string.Equals(tag, "boss", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "final", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "elite", StringComparison.OrdinalIgnoreCase));
            if (isBossThreat && _bossWarningWave != _wave)
            {
                _bossWarningWave = _wave;
                _battlePresentation?.ShowCinematic(
                    TDBattleCinematicKind.BossPhase,
                    "[B!]",
                    "BOSS THREAT ENTERING",
                    $"{runtimeEntry.displayName}  /  {FormatLaneLabel(laneKey)}",
                    TDBattleFeedbackTier.Critical,
                    1.45f);
                PlayCriticalSfxTone("boss_warning", 175f, 0.36f, 0.96f, true);
            }
        }

        private void RegisterEnemyEncounter(TDEnemyCatalogEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId))
            {
                return;
            }

            var firstEncounter = _encounteredEnemyIds.Add(entry.enemyId);
            RecordEnemyCodexObservation(entry.enemyId, TDEnemyCodexObservation.Sighted);
            if (!firstEncounter)
            {
                return;
            }

            PlayerPrefs.SetInt(BuildCodexPlayerPrefsKey(entry.enemyId), 1);
            PlayerPrefs.Save();
            _codexDiscoveriesThisRun++;

            var label = !string.IsNullOrWhiteSpace(entry.displayName) ? entry.displayName : GetEnemyDisplayName(entry.enemyId);
            var tagSummary = BuildEnemyTagSummary(entry, 3);
            var suffix = string.IsNullOrWhiteSpace(tagSummary) ? string.Empty : $" [{tagSummary}]";
            PushTacticalEvent($"Codex +1: First sighting {label}{suffix}", 6.0f);
        }

        private IEnumerator SpawnSplitChildren(string enemyId, int count, float interval, string laneKey)
        {
            if (_gameOver || count <= 0 || string.IsNullOrWhiteSpace(enemyId))
            {
                yield break;
            }

            if (!_enemyCatalog.TryGetValue(enemyId, out var entry))
            {
                yield break;
            }

            var safeInterval = Mathf.Max(0.05f, interval);
            var resolvedLane = ResolveExistingLaneKey(laneKey);
            for (var i = 0; i < count && !_gameOver; i++)
            {
                _runtimeSpawnIndex++;
                SpawnEnemy(entry, GetSpawnPathForLane(resolvedLane), _wave, 10000 + _runtimeSpawnIndex, resolvedLane);
                if (i < count - 1)
                {
                    yield return new WaitForSeconds(safeInterval);
                }
            }
        }

        private IEnumerator WaveLoopFromConfig()
        {
            // Wait for deployment confirmation with a timeout safeguard.
            // The title screen sets _campaignDeploymentConfirmed=false until the player
            // picks New Game / Continue. Automation (P124) may set it via reflection
            // after a delay. A 5s timeout prevents a permanent deadlock if the flag
            // is never set (e.g. coroutine started before title screen exists).
            var waitStart = Time.realtimeSinceStartup;
            while (!_campaignDeploymentConfirmed && !_gameOver)
            {
                if (Time.realtimeSinceStartup - waitStart > 5f)
                {
                    Debug.LogWarning("[TD] WaveLoop waited >5s for deployment confirmation — forcing resume (automation/title path).");
                    _campaignDeploymentConfirmed = true;
                }

                yield return null;
            }

            // Ensure wave data is ready before entering the loop.
            if (_waveSet == null || _waveSet.waves == null || _waveSet.waves.Length == 0)
            {
                Debug.LogWarning("[TD] WaveLoopFromConfig: _waveSet null/empty — falling back.");
                yield return FallbackWaveLoop();
                yield break;
            }

            // Use real-time delay so timeScale (e.g. 0 during pause) doesn't stall the loop.
            yield return new WaitForSecondsRealtime(1f);

            var waves = _waveSet.waves;
            Array.Sort(waves, (a, b) => a.waveIndex.CompareTo(b.waveIndex));

            for (var w = 0; w < waves.Length && !_gameOver; w++)
            {
                var wave = waves[w];
                _wave = wave.waveIndex;
                _currentWaveDefinition = wave;
                ApplyConfiguredWaveRuntimeContext(wave);
                BeginWaveStat(_wave);
                _currentWaveHint = string.IsNullOrWhiteSpace(wave.hint) ? "(no hint)" : wave.hint;

                _prepCountdown = wave.prepSeconds > 0f ? wave.prepSeconds : _waveSet.globalDefaults.prepSeconds;
                yield return WaitForPrepStart(_prepCountdown);
                if (_waveDispatchedEarly)
                {
                    SetStatus($"Wave {_wave} starting now.");
                }

                var groups = wave.groups ?? Array.Empty<TDWaveGroup>();
                PresentDangerousLaneWarning(wave);
                var remainingGroups = groups.Length;
                for (var g = 0; g < groups.Length; g++)
                {
                    StartCoroutine(SpawnGroup(wave, groups[g], () => remainingGroups--));
                }

                while (remainingGroups > 0 && !_gameOver)
                {
                    yield return null;
                }

                while (_activeEnemies.Count > 0 && !_gameOver)
                {
                    _activeEnemies.RemoveAll(enemy => enemy == null);
                    yield return null;
                }

                if (_gameOver)
                {
                    break;
                }

                var baseReward = wave.rewardGold > 0 ? wave.rewardGold : _waveSet.globalDefaults.baseRewardGold;
                var reward = TDEconomyTuning.ScaleWaveClearReward(
                    ScaleMissionReward(baseReward),
                    _wave,
                    GetConfiguredWaveCount());
                _defenseBudget += reward;
                TrackP125ClearIncome(reward);
                SetStatus($"Wave {_wave} cleared, reward +{reward} budget");
                PushTacticalEvent($"Clear W{_wave:00}: kills {_currentWaveStat?.kills ?? 0}, leaks {_currentWaveStat?.escapes ?? 0}, +{reward} budget", 6.0f);
                _battlePresentation?.ShowCinematic(
                    TDBattleCinematicKind.WaveTransition,
                    "[W+]",
                    $"WAVE {_wave:00} SECURED",
                    $"+{reward} BUDGET  /  PREPARE NEXT LINE",
                    TDBattleFeedbackTier.Routine,
                    0.78f);
                PlaySfxTone("wave_clear", 500f, 0.16f, 0.66f, true);
                FinalizeCurrentWaveStat(true);
                yield return new WaitForSeconds(0.6f);
            }

            if (_gameOver)
            {
                yield break;
            }

            _victory = true;
            _gameOver = true;
            ResetResonanceState();
            PlaySfxTone("run_victory", 760f, 0.34f, 0.95f, true);
            RecordCampaignResultIfNeeded();
            LogRunSummary();
        }

        private void PresentDangerousLaneWarning(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return;
            }

            var lanePressure = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var total = 0;
            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0)
                {
                    continue;
                }

                var lane = string.IsNullOrWhiteSpace(group.lane) ? "center" : group.lane.Trim();
                lanePressure[lane] = lanePressure.TryGetValue(lane, out var current)
                    ? current + group.count
                    : group.count;
                total += group.count;
            }

            if (lanePressure.Count == 0 || total < 4)
            {
                return;
            }

            var dominant = lanePressure
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .First();
            var share = Mathf.RoundToInt((dominant.Value / (float)Mathf.Max(1, total)) * 100f);
            var hasDangerTag = wave.threatTags != null && wave.threatTags.Any(tag =>
                string.Equals(tag, "pressure", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "fast", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "flank", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "boss", StringComparison.OrdinalIgnoreCase));
            if (!hasDangerTag && lanePressure.Count > 1 && share < 45)
            {
                return;
            }

            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.DangerousLane,
                "[R!]",
                $"DANGER LANE  {FormatLaneLabel(dominant.Key)}",
                $"{dominant.Value}/{total} CONTACTS  /  {share}% PRESSURE",
                hasDangerTag ? TDBattleFeedbackTier.Critical : TDBattleFeedbackTier.Tactical,
                1.12f);
            PlaySfxTone("danger_lane", 370f, 0.18f, 0.68f, true);
        }

        private IEnumerator WaitForPrepStart(float prepSeconds)
        {
            _prepDuration = Mathf.Max(0f, prepSeconds + _missionPrepSecondsBonus);
            _prepCountdown = _prepDuration;
            _waveStartRequested = false;
            _isInPrepPhase = true;

            if (IsOpeningWaveBuildRequired() && !_openingGuideShown)
            {
                _openingGuideShown = true;
                SetStatus("Build one tower, check range, then start the wave.");
            }

            while (!_gameOver)
            {
                if (_missionBoardOpen)
                {
                    yield return null;
                    continue;
                }

                if (_waveStartRequested && !IsOpeningWaveBuildRequired())
                {
                    break;
                }

                if (!IsOpeningWaveBuildRequired())
                {
                    if (_prepCountdown <= 0f)
                    {
                        break;
                    }

                    _prepCountdown = Mathf.Max(0f, _prepCountdown - Time.deltaTime);
                }

                yield return null;
            }

            CaptureWaveStartReadiness();
            _prepCountdown = 0f;
            _isInPrepPhase = false;
            _waveStartRequested = false;
        }

        private IEnumerator SpawnGroup(TDWaveDefinition wave, TDWaveGroup group, Action onCompleted)
        {
            if (group == null)
            {
                onCompleted?.Invoke();
                yield break;
            }

            var formation = NormalizeGroupToken(group.formation);
            var delayedStart = Mathf.Max(0f, group.startDelay + GetFormationStartDelayOffset(formation) + _scenarioWaveDelayBonus);
            if (delayedStart > 0f)
            {
                yield return new WaitForSeconds(delayedStart);
            }

            var interval = Mathf.Max(group.spawnInterval, _waveSet.globalDefaults.spawnMinSpacing);
            var routeLabel = BuildGroupRouteEventLabel(group, formation);
            if (!string.IsNullOrWhiteSpace(routeLabel))
            {
                PushTacticalEvent(routeLabel, 4.8f);
            }

            for (var i = 0; i < group.count && !_gameOver; i++)
            {
                if (_enemyCatalog.TryGetValue(group.enemyId, out var entry))
                {
                    var laneKey = ResolveSpawnLaneKey(group, formation, i);
                    var path = GetSpawnPathForLane(laneKey);
                    SpawnEnemy(entry, path, wave.waveIndex, i + 1, laneKey);
                }
                else if (i == 0)
                {
                    Debug.LogWarning($"[TD] Missing enemy config for group enemyId={group.enemyId} in wave={wave.waveIndex}");
                }

                if (i < group.count - 1)
                {
                    yield return new WaitForSeconds(ResolveSpawnCadence(interval, formation, i, group.count));
                }
            }

            onCompleted?.Invoke();
        }

        private IEnumerator FallbackWaveLoop()
        {
            _currentWaveDefinition = null;
            _currentWaveHint = "Wave config missing: fallback mode enabled.";
            while (!_campaignDeploymentConfirmed && !_gameOver)
            {
                yield return null;
            }

            yield return new WaitForSeconds(1f);

            while (!_gameOver)
            {
                _wave++;
                var enemyCount = 5 + (_wave * 2);
                ApplyFallbackWaveRuntimeContext(enemyCount);
                BeginWaveStat(_wave);
                yield return WaitForPrepStart(8f);
                var spawnDelay = Mathf.Max(0.35f, 1f - (_wave * 0.04f));

                for (var i = 0; i < enemyCount && !_gameOver; i++)
                {
                    if (!_enemyCatalog.TryGetValue("skitter_runner", out var entry))
                    {
                        entry = new TDEnemyCatalogEntry
                        {
                            enemyId = "skitter_runner",
                            displayName = "Skitter Runner",
                            hp = 30 + (_wave * 2),
                            speed = 1.6f,
                            armorFlat = 0,
                            rewardGold = 8 + (_wave / 2),
                            lineDamage = 1,
                            tags = new[] { "fast", "light" }
                        };
                    }

                    SpawnEnemy(entry, GetDefaultSpawnPath(), _wave, i + 1, "default");
                    yield return new WaitForSeconds(spawnDelay);
                }

                while (_activeEnemies.Count > 0 && !_gameOver)
                {
                    _activeEnemies.RemoveAll(enemy => enemy == null);
                    yield return null;
                }

                if (_gameOver)
                {
                    yield break;
                }

                _defenseBudget += ScaleMissionReward(20 + _wave);
                FinalizeCurrentWaveStat(true);
                yield return new WaitForSeconds(1.2f);
            }
        }

        private void RestartCurrentScene()
        {
            LoadingTransition("RESTARTING", null);
        }

        /// <summary>
        /// Show the loading screen, yield one frame to render it, then reload the scene.
        /// The loadingVerb controls the text ("DEPLOYING" / "RESTARTING" / etc.).
        /// The levelLabel shows the target level name (null = use current).
        /// </summary>
        private void LoadingTransition(string loadingVerb, string levelLabel)
        {
            var label = levelLabel;
            if (string.IsNullOrEmpty(label) && _campaignRoute?.level != null)
            {
                var map = _campaignRoute.map;
                label = map != null && !string.IsNullOrWhiteSpace(map.displayName)
                    ? map.displayName
                    : _campaignRoute.level.mapId;
                label = $"L{_campaignRoute.level.levelIndex:00}  {label}";
            }

            if (_loadingScreen != null)
            {
                StartCoroutine(LoadingTransitionRoutine(loadingVerb, label));
            }
            else
            {
                DoSceneReload();
            }
        }

        private IEnumerator LoadingTransitionRoutine(string loadingVerb, string label)
        {
            _loadingScreen.Show(label, loadingVerb);
            // Yield twice: once to ensure the Canvas renders the overlay,
            // once more for safety margin before the synchronous LoadScene blocks.
            yield return null;
            yield return null;
            DoSceneReload();
        }

        private void DoSceneReload()
        {
            Time.timeScale = Mathf.Max(1f, _lastActivePlaybackSpeed);
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }

            SceneManager.LoadScene(scene.name);
        }

        private static string GetTowerKindLabel(TDTowerKind kind)
        {
            return TDTower.GetDisplayName(kind);
        }

        private static string GetUpgradeBranchLabel(TDTowerUpgradeBranch branch)
        {
            return branch == TDTowerUpgradeBranch.Damage ? "Damage" : "Utility";
        }

        private static string GetEnemySpritePath(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => "Art/anim/enemy_skitter_runner_00",
                "carapace_brute" => "Art/anim/enemy_carapace_brute_00",
                "ash_swarm" => "Art/anim/enemy_ash_swarm_00",
                "plated_spore" => "Art/anim/enemy_plated_spore_00",
                "burrow_sapper" => "Art/anim/enemy_burrow_sapper_00",
                "ember_leech" => "Art/anim/enemy_ember_leech_00",
                "spore_carrier" => "Art/anim/enemy_spore_carrier_00",
                "rail_warden" => "Art/anim/enemy_rail_warden_00",
                "cinder_glider" => "Art/anim/enemy_cinder_glider_00",
                "husk_titan" => "Art/anim/enemy_husk_titan_00",
                "echo_mimic" => "Art/anim/enemy_echo_mimic_00",
                "furnace_matriarch" => "Art/anim/enemy_furnace_matriarch_00",
                _ => "Art/enemy_slime"
            };
        }

        private static Material GetEnemyVisualMaterial(string enemyId)
        {
            var repairMode = enemyId?.ToLowerInvariant() switch
            {
                "ember_leech" => 0f,
                "furnace_matriarch" => 1f,
                "cinder_glider" => 2f,
                _ => -1f
            };
            if (repairMode < 0f)
            {
                return null;
            }

            if (EnemyBodyRepairMaterials.TryGetValue(enemyId, out var cachedMaterial) && cachedMaterial != null)
            {
                return cachedMaterial;
            }

            var shader = Shader.Find("TD/EnemyBodyRepair");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetFloat("_RepairMode", repairMode);
            EnemyBodyRepairMaterials[enemyId] = material;
            return material;
        }

        private static string GetEnemyAnimationPrefix(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => "Art/anim/enemy_skitter_runner",
                "carapace_brute" => "Art/anim/enemy_carapace_brute",
                "ash_swarm" => "Art/anim/enemy_ash_swarm",
                "plated_spore" => "Art/anim/enemy_plated_spore",
                "burrow_sapper" => "Art/anim/enemy_burrow_sapper",
                "ember_leech" => "Art/anim/enemy_ember_leech",
                "spore_carrier" => "Art/anim/enemy_spore_carrier",
                "rail_warden" => "Art/anim/enemy_rail_warden",
                "cinder_glider" => "Art/anim/enemy_cinder_glider",
                "husk_titan" => "Art/anim/enemy_husk_titan",
                "echo_mimic" => "Art/anim/enemy_echo_mimic",
                "furnace_matriarch" => "Art/anim/enemy_furnace_matriarch",
                _ => string.Empty
            };
        }

        private static int GetEnemyAnimationFrames(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 8,
                "carapace_brute" => 6,
                "ash_swarm" => 8,
                "plated_spore" => 6,
                "burrow_sapper" => 8,
                "ember_leech" => 6,
                "spore_carrier" => 6,
                "rail_warden" => 6,
                "cinder_glider" => 8,
                "husk_titan" => 6,
                "echo_mimic" => 8,
                "furnace_matriarch" => 6,
                _ => 1
            };
        }

        private static float GetEnemyAnimationFps(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 10f,
                "carapace_brute" => 7f,
                "ash_swarm" => 12f,
                "plated_spore" => 7f,
                "burrow_sapper" => 11f,
                "ember_leech" => 7f,
                "spore_carrier" => 6.5f,
                "rail_warden" => 6.2f,
                "cinder_glider" => 13f,
                "husk_titan" => 5.8f,
                "echo_mimic" => 9f,
                "furnace_matriarch" => 5.4f,
                _ => 6f
            };
        }

        private static Color GetEnemyFallbackColor(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Color(0.93f, 0.54f, 0.27f),
                "carapace_brute" => new Color(0.59f, 0.33f, 0.23f),
                "ash_swarm" => new Color(0.77f, 0.76f, 0.67f),
                "plated_spore" => new Color(0.46f, 0.68f, 0.42f),
                "burrow_sapper" => new Color(0.88f, 0.47f, 0.22f),
                "ember_leech" => new Color(0.92f, 0.30f, 0.26f),
                "spore_carrier" => new Color(0.74f, 0.85f, 0.50f),
                "rail_warden" => new Color(0.58f, 0.63f, 0.70f),
                "cinder_glider" => new Color(0.97f, 0.58f, 0.18f),
                "husk_titan" => new Color(0.42f, 0.38f, 0.34f),
                "echo_mimic" => new Color(0.56f, 0.44f, 0.82f),
                "furnace_matriarch" => new Color(0.66f, 0.22f, 0.18f),
                _ => new Color(0.82f, 0.29f, 0.26f)
            };
        }

        private static Vector2 GetTowerColliderSize(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => new Vector2(0.44f, 0.44f),
                TDTowerKind.CinderMortar => new Vector2(0.48f, 0.48f),
                TDTowerKind.FrostCoil => new Vector2(0.42f, 0.42f),
                TDTowerKind.ArcWelder => new Vector2(0.44f, 0.44f),
                TDTowerKind.SiegeDrill => new Vector2(0.47f, 0.47f),
                TDTowerKind.EmberFlak => new Vector2(0.43f, 0.43f),
                TDTowerKind.ResonanceBeacon => new Vector2(0.46f, 0.46f),
                TDTowerKind.GravSnare => new Vector2(0.48f, 0.48f),
                _ => new Vector2(0.45f, 0.45f)
            };
        }

        private static Vector2 GetTowerColliderOffset(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => new Vector2(0f, -0.04f),
                TDTowerKind.CinderMortar => new Vector2(0f, -0.03f),
                TDTowerKind.FrostCoil => new Vector2(0f, -0.02f),
                TDTowerKind.ArcWelder => new Vector2(0f, -0.03f),
                TDTowerKind.SiegeDrill => new Vector2(0f, -0.03f),
                TDTowerKind.EmberFlak => new Vector2(0f, -0.03f),
                TDTowerKind.ResonanceBeacon => new Vector2(0f, -0.03f),
                TDTowerKind.GravSnare => new Vector2(0f, -0.03f),
                _ => Vector2.zero
            };
        }

        private static float GetEnemyCellCoverage(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 0.70f,
                "carapace_brute" => 0.88f,
                "ash_swarm" => 0.62f,
                "plated_spore" => 0.76f,
                "burrow_sapper" => 0.68f,
                "ember_leech" => 0.74f,
                "spore_carrier" => 0.76f,
                "rail_warden" => 0.82f,
                "cinder_glider" => 0.66f,
                "husk_titan" => 1.05f,
                "echo_mimic" => 0.80f,
                "furnace_matriarch" => 1.22f,
                _ => 0.68f
            };
        }

        private static Vector3 GetEnemyVisualOffset(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Vector3(0f, -0.06f, 0f),
                "carapace_brute" => new Vector3(0f, -0.05f, 0f),
                "ash_swarm" => new Vector3(0f, -0.03f, 0f),
                "plated_spore" => new Vector3(0f, -0.04f, 0f),
                "burrow_sapper" => new Vector3(0f, -0.05f, 0f),
                "ember_leech" => new Vector3(0f, -0.04f, 0f),
                "spore_carrier" => new Vector3(0f, -0.04f, 0f),
                "rail_warden" => new Vector3(0f, -0.04f, 0f),
                "cinder_glider" => new Vector3(0f, -0.05f, 0f),
                "husk_titan" => new Vector3(0f, -0.04f, 0f),
                "echo_mimic" => new Vector3(0f, -0.04f, 0f),
                "furnace_matriarch" => new Vector3(0f, -0.03f, 0f),
                _ => new Vector3(0f, -0.04f, 0f)
            };
        }

        private static Vector2 GetEnemyColliderSize(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Vector2(0.34f, 0.34f),
                "carapace_brute" => new Vector2(0.46f, 0.46f),
                "ash_swarm" => new Vector2(0.32f, 0.32f),
                "plated_spore" => new Vector2(0.40f, 0.40f),
                "burrow_sapper" => new Vector2(0.36f, 0.36f),
                "ember_leech" => new Vector2(0.40f, 0.40f),
                "spore_carrier" => new Vector2(0.42f, 0.42f),
                "rail_warden" => new Vector2(0.44f, 0.44f),
                "cinder_glider" => new Vector2(0.34f, 0.34f),
                "husk_titan" => new Vector2(0.52f, 0.52f),
                "echo_mimic" => new Vector2(0.44f, 0.44f),
                "furnace_matriarch" => new Vector2(0.64f, 0.64f),
                _ => new Vector2(0.38f, 0.38f)
            };
        }

        private static Vector2 GetEnemyColliderOffset(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Vector2(0f, -0.05f),
                "carapace_brute" => new Vector2(0f, -0.03f),
                "ash_swarm" => new Vector2(0f, -0.03f),
                "plated_spore" => new Vector2(0f, -0.04f),
                "burrow_sapper" => new Vector2(0f, -0.04f),
                "ember_leech" => new Vector2(0f, -0.04f),
                "spore_carrier" => new Vector2(0f, -0.04f),
                "rail_warden" => new Vector2(0f, -0.03f),
                "cinder_glider" => new Vector2(0f, -0.05f),
                "husk_titan" => new Vector2(0f, -0.03f),
                "echo_mimic" => new Vector2(0f, -0.03f),
                "furnace_matriarch" => new Vector2(0f, -0.02f),
                _ => new Vector2(0f, -0.03f)
            };
        }

        private static int GetEnemySortingOrder(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 16,
                "carapace_brute" => 16,
                "ash_swarm" => 16,
                "plated_spore" => 16,
                "burrow_sapper" => 16,
                "ember_leech" => 16,
                "spore_carrier" => 16,
                "rail_warden" => 16,
                "cinder_glider" => 16,
                "husk_titan" => 17,
                "echo_mimic" => 16,
                "furnace_matriarch" => 18,
                _ => 16
            };
        }

        private static float GetEnemyShadowCoverage(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 0.62f,
                "carapace_brute" => 0.80f,
                "ash_swarm" => 0.56f,
                "plated_spore" => 0.68f,
                "burrow_sapper" => 0.60f,
                "ember_leech" => 0.66f,
                "spore_carrier" => 0.68f,
                "rail_warden" => 0.72f,
                "cinder_glider" => 0.58f,
                "husk_titan" => 0.92f,
                "echo_mimic" => 0.70f,
                "furnace_matriarch" => 1.04f,
                _ => 0.62f
            };
        }

        private static float GetEnemyShadowAlpha(string enemyId)
        {
            // Contact shadows lifted across the board (+0.22) so bodies read as grounded
            // rather than floating above a faint tint. Relative ordering between enemies preserved.
            return enemyId switch
            {
                "skitter_runner" => 0.52f,
                "carapace_brute" => 0.56f,
                "ash_swarm" => 0.50f,
                "plated_spore" => 0.54f,
                "burrow_sapper" => 0.52f,
                "ember_leech" => 0.52f,
                "spore_carrier" => 0.53f,
                "rail_warden" => 0.55f,
                "cinder_glider" => 0.52f,
                "husk_titan" => 0.57f,
                "echo_mimic" => 0.54f,
                "furnace_matriarch" => 0.58f,
                _ => 0.52f
            };
        }

        private static Vector3 GetEnemyShadowOffset(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Vector3(0f, -0.21f, 0f),
                "carapace_brute" => new Vector3(0f, -0.19f, 0f),
                "ash_swarm" => new Vector3(0f, -0.18f, 0f),
                "plated_spore" => new Vector3(0f, -0.20f, 0f),
                "burrow_sapper" => new Vector3(0f, -0.20f, 0f),
                "ember_leech" => new Vector3(0f, -0.20f, 0f),
                "spore_carrier" => new Vector3(0f, -0.20f, 0f),
                "rail_warden" => new Vector3(0f, -0.20f, 0f),
                "cinder_glider" => new Vector3(0f, -0.21f, 0f),
                "husk_titan" => new Vector3(0f, -0.18f, 0f),
                "echo_mimic" => new Vector3(0f, -0.20f, 0f),
                "furnace_matriarch" => new Vector3(0f, -0.16f, 0f),
                _ => new Vector3(0f, -0.20f, 0f)
            };
        }

        private static Vector3 ResolveEnemyFootShadowOffset(
            SpriteRenderer visualRenderer,
            Transform visualTransform,
            string enemyId)
        {
            if (visualRenderer == null || visualRenderer.sprite == null || visualTransform == null)
            {
                return GetEnemyShadowOffset(enemyId);
            }

            var visualBottom = visualTransform.localPosition.y +
                               (visualRenderer.sprite.bounds.min.y * Mathf.Abs(visualTransform.localScale.y));
            var lift = GetEnemyFootShadowLift(enemyId);
            return new Vector3(visualTransform.localPosition.x, visualBottom + lift, 0f);
        }

        private static void AlignEnemyVisualToRouteAnchor(
            SpriteRenderer visualRenderer,
            Transform visualTransform,
            string enemyId)
        {
            if (visualRenderer == null || visualRenderer.sprite == null || visualTransform == null)
            {
                return;
            }

            var authoredOffset = GetEnemyVisualOffset(enemyId);
            var scaledBottom = visualRenderer.sprite.bounds.min.y * Mathf.Abs(visualTransform.localScale.y);
            visualTransform.localPosition = new Vector3(
                authoredOffset.x,
                -scaledBottom - GetEnemyFootShadowLift(enemyId),
                authoredOffset.z);
        }

        private static float GetEnemyFootShadowLift(string enemyId)
        {
            return enemyId switch
            {
                "husk_titan" => 0.075f,
                "furnace_matriarch" => 0.085f,
                "carapace_brute" => 0.065f,
                _ => 0.050f
            };
        }

        private Vector3 ResolveSpriteScale(Sprite sprite, float targetCellCoverage)
        {
            if (sprite == null)
            {
                return Vector3.one;
            }

            var spriteWidth = Mathf.Max(0.0001f, sprite.bounds.size.x);
            var targetWidth = Mathf.Max(0.1f, CellSize * Mathf.Clamp(targetCellCoverage, 0.1f, 2f));
            return Vector3.one * (targetWidth / spriteWidth);
        }

        private void ApplyConfiguredWaveRuntimeContext(TDWaveDefinition wave)
        {
            _waveDispatchedEarly = false;
            _currentWavePhase = NormalizeWaveLabel(wave?.phase, "unknown");
            _currentWaveGoalTag = NormalizeWaveLabel(wave?.goalTag, "none");
            CaptureCurrentWaveThreatTags(wave);
            _currentWaveThreatTags = wave?.threatTags == null || wave.threatTags.Length == 0
                ? "none"
                : string.Join("/", wave.threatTags);

            _currentWaveBudgetExpected = Mathf.Max(0f, wave?.budgetTarget ?? 0f);
            _currentWaveBudgetActual = CalculateWaveBudgetActual(wave);
            _currentWaveBudgetInRange = IsWaveBudgetInRange(_currentWaveBudgetExpected, wave?.budgetTolerance ?? 1f, _currentWaveBudgetActual);
            PrepareScenarioForWave();
            if (!_currentWaveBudgetInRange && wave != null)
            {
                Debug.LogWarning(
                    $"[TD][WaveGrammar] budget out-of-range wave={wave.waveIndex} goal={_currentWaveGoalTag} " +
                    $"target={_currentWaveBudgetExpected:0.##} actual={_currentWaveBudgetActual:0.##} tolerance={wave.budgetTolerance:0.##}");
            }
        }

        private void ApplyFallbackWaveRuntimeContext(int enemyCount)
        {
            _waveDispatchedEarly = false;
            _currentWavePhase = "fallback";
            _currentWaveGoalTag = "fallback_scaling";
            _currentWaveThreatTagSet.Clear();
            _currentWaveThreatTags = "none";
            _currentWaveBudgetExpected = Mathf.Max(0f, enemyCount);
            _currentWaveBudgetActual = Mathf.Max(0f, enemyCount);
            _currentWaveBudgetInRange = true;
        }

        private void CaptureCurrentWaveThreatTags(TDWaveDefinition wave)
        {
            _currentWaveThreatTagSet.Clear();
            if (wave == null)
            {
                return;
            }

            AddCurrentWaveThreatTag(wave.goalTag);
            var sourceTags = wave.threatTags ?? Array.Empty<string>();
            for (var i = 0; i < sourceTags.Length; i++)
            {
                AddCurrentWaveThreatTag(sourceTags[i]);
            }

            var groups = wave.groups ?? Array.Empty<TDWaveGroup>();
            for (var i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                if (group == null)
                {
                    continue;
                }

                AddCurrentWaveThreatTag(group.lane);
                AddCurrentWaveThreatTag(group.formation);
                if (!_enemyCatalog.TryGetValue(group.enemyId, out var entry) || entry.tags == null)
                {
                    continue;
                }

                for (var t = 0; t < entry.tags.Length; t++)
                {
                    AddCurrentWaveThreatTag(entry.tags[t]);
                }
            }
        }

        private void PrepareScenarioForWave()
        {
            _scenarioWaveDelayBonus = 0f;
            _scenarioBossPhaseSuppressed = false;
            _scenarioBossPhase = 0;
            if (string.Equals(_currentWavePhase, "introduce", StringComparison.OrdinalIgnoreCase))
            {
                PresentExamBeat(TDExamPresentationStage.Opening);
            }
            else if (string.Equals(_currentWavePhase, "reinforce", StringComparison.OrdinalIgnoreCase))
            {
                PresentExamBeat(TDExamPresentationStage.Escalation);
            }
            else if (string.Equals(_currentWavePhase, "exam", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(_currentWavePhase, "boss", StringComparison.OrdinalIgnoreCase))
            {
                PresentExamBeat(TDExamPresentationStage.Decision);
            }

            if (_activeScenarioMechanic == null)
            {
                return;
            }

            if (string.Equals(_currentWavePhase, "reinforce", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_currentWavePhase, "exam", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_currentWavePhase, "boss", StringComparison.OrdinalIgnoreCase))
            {
                _scenarioOpportunities++;
                PushTacticalEvent(
                    $"Scenario {_currentWavePhase.ToUpperInvariant()}: {_activeScenarioMechanic.displayName} decision available",
                    5.4f);
            }
        }

        private void AddCurrentWaveThreatTag(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _currentWaveThreatTagSet.Add(value.Trim().ToLowerInvariant());
            }
        }

        private float CalculateWaveBudgetActual(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return 0f;
            }

            var actual = 0f;
            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0 || string.IsNullOrWhiteSpace(group.enemyId))
                {
                    continue;
                }

                if (_enemyCatalog.TryGetValue(group.enemyId, out var entry))
                {
                    var threatCost = entry != null && entry.threatCost > 0f ? entry.threatCost : 0f;
                    actual += group.count * threatCost;
                }
            }

            return actual;
        }

        private static bool IsWaveBudgetInRange(float target, float tolerance, float actual)
        {
            if (target <= 0f)
            {
                return true;
            }

            var safeTolerance = Mathf.Clamp(tolerance <= 0f ? 1f : tolerance, 0.5f, 1.5f);
            var upperBound = target * safeTolerance;
            var lowerBound = target * (2f - safeTolerance);
            if (lowerBound > upperBound)
            {
                var temp = lowerBound;
                lowerBound = upperBound;
                upperBound = temp;
            }

            return actual >= lowerBound - 0.01f && actual <= upperBound + 0.01f;
        }

        private static string NormalizeWaveLabel(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private void BeginWaveStat(int waveIndex)
        {
            _currentWaveStat = new TDWaveRuntimeStat
            {
                waveIndex = waveIndex,
                phase = _currentWavePhase,
                goalTag = _currentWaveGoalTag,
                threatTags = _currentWaveThreatTags,
                budgetTarget = _currentWaveBudgetExpected,
                budgetActual = _currentWaveBudgetActual,
                budgetInRange = _currentWaveBudgetInRange,
                dispatchedEarly = _waveDispatchedEarly,
                budgetStart = _defenseBudget,
                integrityStart = _lineIntegrity
            };

            _waveStats[waveIndex] = _currentWaveStat;
        }

        private void FinalizeCurrentWaveStat(bool cleared)
        {
            if (_currentWaveStat == null || _currentWaveStat.logged)
            {
                return;
            }

            _currentWaveStat.cleared = cleared;
            _currentWaveStat.phase = _currentWavePhase;
            _currentWaveStat.goalTag = _currentWaveGoalTag;
            _currentWaveStat.threatTags = _currentWaveThreatTags;
            _currentWaveStat.budgetTarget = _currentWaveBudgetExpected;
            _currentWaveStat.budgetActual = _currentWaveBudgetActual;
            _currentWaveStat.budgetInRange = _currentWaveBudgetInRange;
            _currentWaveStat.dispatchedEarly = _waveDispatchedEarly;
            _currentWaveStat.budgetEnd = _defenseBudget;
            _currentWaveStat.integrityEnd = _lineIntegrity;
            FinalizeP125WaveEconomy(_currentWaveStat);
            _currentWaveStat.logged = true;

            if (cleared)
            {
                _wavesCleared++;
            }

            LogWaveStat(_currentWaveStat);
        }

        private static string ClassifyFailureReason(TDEnemy enemy)
        {
            if (enemy == null)
            {
                return FailureTagOutputInsufficient;
            }

            if (enemy.HasTag("armored"))
            {
                return FailureTagCounterMismatch;
            }

            if (enemy.HasTag("support") || enemy.HasTag("attrition"))
            {
                return FailureTagCounterMismatch;
            }

            if (enemy.HasTag("fast"))
            {
                return FailureTagCoverageGap;
            }

            if (enemy.HasTag("heavy"))
            {
                return FailureTagOutputInsufficient;
            }

            return FailureTagOutputInsufficient;
        }

        private static void IncrementCounter(Dictionary<string, int> counter, string key, int amount = 1)
        {
            if (counter == null || string.IsNullOrWhiteSpace(key) || amount <= 0)
            {
                return;
            }

            if (counter.TryGetValue(key, out var value))
            {
                counter[key] = value + amount;
                return;
            }

            counter[key] = amount;
        }

        private static string GetTopReasonFromCounter(Dictionary<string, int> counter)
        {
            if (counter == null || counter.Count == 0)
            {
                return "-";
            }

            var pairs = new List<KeyValuePair<string, int>>(counter);
            pairs.Sort((a, b) =>
            {
                var delta = b.Value.CompareTo(a.Value);
                return delta != 0 ? delta : string.CompareOrdinal(a.Key, b.Key);
            });

            var top = pairs[0];
            return $"{top.Key} x{top.Value}";
        }

        private string GetTopFailureReasonSummary()
        {
            if (_failureReasonCounts.Count == 0)
            {
                return "none";
            }

            var pairs = new List<KeyValuePair<string, int>>(_failureReasonCounts);
            pairs.Sort((a, b) =>
            {
                var delta = b.Value.CompareTo(a.Value);
                return delta != 0 ? delta : string.CompareOrdinal(a.Key, b.Key);
            });

            var maxShown = Mathf.Max(1, _maxFailureReasonsShown);
            var max = Mathf.Min(maxShown, pairs.Count);
            var labels = new List<string>(max);
            for (var i = 0; i < max; i++)
            {
                labels.Add($"{pairs[i].Key} x{pairs[i].Value}");
            }

            return string.Join(" | ", labels);
        }

        private string BuildFailureUiLabel()
        {
            var runtimeFailure = GetTopFailureReasonSummary()
                .Replace(FailureTagCoverageGap, "COVERAGE")
                .Replace(FailureTagCounterMismatch, "COUNTER")
                .Replace(FailureTagOutputInsufficient, "OUTPUT");
            return !_victory && _examPresentationProfile != null
                ? $"{_examPresentationProfile.failureSignature}  |  {runtimeFailure}"
                : runtimeFailure;
        }

        private string GetTopFailureReasonKey()
        {
            if (_failureReasonCounts.Count == 0)
            {
                return string.Empty;
            }

            var topKey = string.Empty;
            var topCount = int.MinValue;
            foreach (var pair in _failureReasonCounts)
            {
                if (pair.Value > topCount)
                {
                    topKey = pair.Key;
                    topCount = pair.Value;
                }
            }

            return topKey;
        }

        private int GetConfiguredWaveCount()
        {
            return _waveSet?.waves?.Length ?? _wave;
        }

        private void LogWaveStat(TDWaveRuntimeStat stat)
        {
            if (stat == null)
            {
                return;
            }

            Debug.Log(
                $"[TD][WaveStat] wave={stat.waveIndex} phase={stat.phase} goal={stat.goalTag} threatTags={stat.threatTags} " +
                $"budgetPlan={stat.budgetTarget:0.##} budgetActual={stat.budgetActual:0.##} budgetInRange={stat.budgetInRange} earlyDispatch={stat.dispatchedEarly} " +
                $"readiness={stat.readinessScore}{(string.IsNullOrWhiteSpace(stat.readinessGrade) ? string.Empty : stat.readinessGrade)} " +
                $"cleared={stat.cleared} kills={stat.kills} escapes={stat.escapes} damage={stat.damageDealt} integrityDamage={stat.integrityDamageTaken} " +
                $"budget={stat.budgetStart}->{stat.budgetEnd} integrity={stat.integrityStart}->{stat.integrityEnd} " +
                $"economy=in:{stat.combatIncome + stat.clearIncome + stat.reinforcementIncome + stat.resonanceIncome}" +
                $"/out:{stat.buildSpend + stat.upgradeSpend + stat.scenarioSpend}" +
                $"/buy:{stat.buildsPurchased + stat.upgradesPurchased + stat.scenarioUses} " +
                $"topFailure={GetTopReasonFromCounter(stat.failureReasons)}");
        }

        private void LogRunSummary()
        {
            if (_runSummaryLogged)
            {
                return;
            }

            _runSummaryLogged = true;
            var result = _victory ? "victory" : "defeat";
            var levelId = _campaignRoute?.level?.levelId ?? "fallback_level";
            var mapId = _campaignRoute?.level?.mapId ?? "fallback_map";
            var waveSetId = _campaignRoute?.level?.waveSetId ?? _waveResourcePath;
            var score = CalculateRunScore();
            var contract = EvaluateCurrentMissionContract();
            var campaignSummary = GetCampaignProgressSummary();
            Debug.Log(
                $"[TD][RunSummary] result={result} level={levelId} map={mapId} waveSet={waveSetId} reachedWave={_wave} cleared={_wavesCleared}/{GetConfiguredWaveCount()} " +
                $"kills={_totalKills} escapes={_totalEscapes} damage={_totalDamageDealt} integrityDamage={_totalIntegrityDamageTaken} failures={GetTopFailureReasonSummary()} " +
                $"missionStars={_currentMissionStars}/3 contract={contract?.contract?.contractId ?? "none"}:{contract?.currentValue ?? 0}/{contract?.contract?.target ?? 0}:{contract?.completed ?? false} " +
                $"campaignCleared={campaignSummary.clearedLevels}/{campaignSummary.totalLevels} campaignStars={campaignSummary.earnedStars}/{campaignSummary.availableStars} campaignContracts={campaignSummary.completedContracts}/{campaignSummary.availableContracts} frontier={campaignSummary.highestUnlockedLevel} " +
                $"missionRules=budget:{_startingDefenseBudget},integrity:{_startingLineIntegrity},hpX:{_missionEnemyHpMultiplier:0.##},speedX:{_missionEnemySpeedMultiplier:0.##},armor:{_missionEnemyArmorBonus},rewardX:{_missionRewardMultiplier:0.##},resonanceX:{_missionResonanceGainMultiplier:0.##} " +
                $"tacticalScore={score.total}{score.grade} coverage={score.coverage} counter={score.counterMatch} output={score.output} economy={score.economy} command={score.command} " +
                $"counterDamage={_counterMatchedDamage}/{_counterOpportunityDamage} " +
                $"buildSpend={_budgetSpentOnBuilds} upgradeSpend={_budgetSpentOnUpgrades} upgrades={_upgradesPurchased} " +
                $"codexKnown={GetCodexDiscoveredCount()}/{GetCodexTotalCount()} codexNewRun={_codexDiscoveriesThisRun} lastReadiness={_lastWaveStartReadinessScore}{_lastWaveStartReadinessGrade} " +
                $"earlyDispatches={_earlyDispatchCount} earlyDispatchEnabled={_allowEarlyWaveDispatch} " +
                $"resonanceEnabledFrom={_resonanceEnabledFromLevel} resonanceEnabled={_isResonanceSystemEnabled} " +
                $"resonanceWindows={_resonanceWindowsTriggered} resonanceCommands={_resonanceCommandsUsed} resonanceMatches={_resonanceMatchedCommands} " +
                $"matrixFull={_matrixFullMatches}/{_matrixOpportunities} matrixTrait={_matrixTraitMatches} matrixResonance={_matrixResonanceMatches} " +
                $"matrixConvergence={_matrixConvergenceTriggers} emberConvergence={_matrixEmberConvergenceTriggers} fractureConvergence={_matrixFractureConvergenceTriggers} " +
                $"matrixBestSync={_matrixBestWindowSync} matrixBestSpecs={_matrixBestWindowSpecializations} fractureConvergenceTargets={_matrixFractureConvergenceAffectedTargets} emberExtension={_matrixEmberConvergenceWindowSeconds:0.00} " +
                $"emberSurgeUses={_emberSurgeUses} fractureMarkUses={_fractureMarkUses} resonanceBonusDmg={Mathf.RoundToInt(_resonanceBonusDamage)} " +
                $"chainBonusTriggers={_resonanceChainBonusTriggers} chainBudgetBonus={_resonanceChainBudgetBonusTotal} chainIntegrityBonus={_resonanceChainIntegrityBonusTotal} " +
                $"splitSpawnEvents={_spawnSplitEvents} attritionPenaltyEvents={_attritionPenaltyEvents}");
            LogP6AnalyticsStats();
        }

        private void LogP6AnalyticsStats()
        {
            var lanes = new List<TDLaneRuntimeStat>();
            foreach (var pair in _laneStats)
            {
                if (pair.Value != null)
                {
                    lanes.Add(pair.Value);
                }
            }

            lanes.Sort((a, b) => string.CompareOrdinal(a.laneKey, b.laneKey));
            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                Debug.Log(
                    $"[TD][LaneStat] lane={lane.laneKey} spawned={lane.spawned} kills={lane.kills} escapes={lane.escapes} " +
                    $"damage={lane.damageDealt} spawnedHp={lane.spawnedHealth} integrityDamage={lane.integrityDamageTaken}");
            }

            var towers = GetSortedTowerStats();
            for (var i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                Debug.Log(
                    $"[TD][TowerStat] rank={i + 1} id={tower.towerId} kind={tower.kind} cell={tower.cell.x},{tower.cell.y} " +
                    $"damage={tower.damageDealt} kills={tower.kills} hits={tower.hits} controls={tower.controlApplications} " +
                    $"controlStrengthSeconds={tower.controlStrengthSeconds:0.00} counterDamage={tower.counterDamage} " +
                    $"spend={tower.TotalSpend} upgrades={tower.upgrades} damageSpec={tower.damageSpecProcs} utilitySpec={tower.utilitySpecProcs} " +
                    $"ultimateAffected={tower.ultimateAffectedTargets} matrix={tower.matrixFullMatches}/{tower.matrixTraitMatches}/{tower.matrixResonanceMatches}");
            }

            var hotspots = BuildRoadHeatReports();
            for (var i = 0; i < hotspots.Count && i < 3; i++)
            {
                var hotspot = hotspots[i];
                var suggested = hotspot.hasSuggestedCell ? $"{hotspot.suggestedCell.x},{hotspot.suggestedCell.y}" : "none";
                Debug.Log(
                    $"[TD][RoadHeat] rank={i + 1} lane={hotspot.stat.laneKey} segment={GetRoadSegmentLabel(hotspot.stat.segmentIndex)} " +
                    $"heat={hotspot.heatScore} coverage={hotspot.coverageScore} reached={hotspot.stat.reached} " +
                    $"escapes={hotspot.stat.escapes} unresolved={hotspot.stat.unresolvedAtEnd} suggested={suggested}");
            }
        }

        private void SetStatus(string message)
        {
            _lastStatus = message;
            _statusTimer = 2.5f;
        }

        public string DebugGetP6AnalyticsReport()
        {
            var score = CalculateRunScore();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"p6.score.total={score.total}");
            sb.AppendLine($"p6.score.grade={score.grade}");
            sb.AppendLine($"p6.score.coverage={score.coverage}");
            sb.AppendLine($"p6.score.counter={score.counterMatch}");
            sb.AppendLine($"p6.score.output={score.output}");
            sb.AppendLine($"p6.score.economy={score.economy}");
            sb.AppendLine($"p6.score.command={score.command}");
            sb.AppendLine($"p6.counter.damage={_counterMatchedDamage}/{_counterOpportunityDamage}");
            foreach (var category in new[] { "speed", "swarm", "armor", "attrition" })
            {
                var total = _threatCategoryDamage.TryGetValue(category, out var categoryTotal) ? categoryTotal : 0;
                var matched = _threatCategoryCounterDamage.TryGetValue(category, out var categoryMatched) ? categoryMatched : 0;
                sb.AppendLine($"p6.counter.{category}={matched}/{total}");
            }

            var lanes = new List<TDLaneRuntimeStat>();
            var laneDamageTotal = 0;
            var laneKillTotal = 0;
            var laneEscapeTotal = 0;
            var laneResolvedWithinSpawn = true;
            foreach (var pair in _laneStats)
            {
                if (pair.Value != null)
                {
                    lanes.Add(pair.Value);
                    laneDamageTotal += pair.Value.damageDealt;
                    laneKillTotal += pair.Value.kills;
                    laneEscapeTotal += pair.Value.escapes;
                    laneResolvedWithinSpawn &= pair.Value.kills + pair.Value.escapes <= pair.Value.spawned;
                }
            }

            lanes.Sort((a, b) => string.CompareOrdinal(a.laneKey, b.laneKey));
            sb.AppendLine($"p6.lane.count={lanes.Count}");
            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                sb.AppendLine(
                    $"p6.lane.{i}=key:{lane.laneKey},spawned:{lane.spawned},kills:{lane.kills},escapes:{lane.escapes}," +
                    $"damage:{lane.damageDealt},spawnedHp:{lane.spawnedHealth},integrityDamage:{lane.integrityDamageTaken}");
            }

            var towers = GetSortedTowerStats();
            var towerDamageTotal = 0;
            for (var i = 0; i < towers.Count; i++)
            {
                towerDamageTotal += towers[i].damageDealt;
            }

            var heatReports = BuildRoadHeatReports();
            var segments = new List<TDRoadSegmentRuntimeStat>();
            var segmentDamageTotal = 0;
            var segmentKillTotal = 0;
            var segmentEscapeTotal = 0;
            foreach (var pair in _roadSegmentStats)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                segments.Add(pair.Value);
                segmentDamageTotal += pair.Value.damageDealt;
                segmentKillTotal += pair.Value.kills;
                segmentEscapeTotal += pair.Value.escapes;
            }

            segments.Sort((a, b) =>
            {
                var delta = string.CompareOrdinal(a.laneKey, b.laneKey);
                return delta != 0 ? delta : a.segmentIndex.CompareTo(b.segmentIndex);
            });

            var analyticsConsistent = laneDamageTotal == _totalDamageDealt &&
                                      towerDamageTotal == _totalDamageDealt &&
                                      segmentDamageTotal == _totalDamageDealt &&
                                      laneKillTotal == _totalKills &&
                                      segmentKillTotal == _totalKills &&
                                      laneEscapeTotal == _totalEscapes &&
                                      segmentEscapeTotal == _totalEscapes &&
                                      laneResolvedWithinSpawn;
            sb.AppendLine($"p6.audit.consistent={analyticsConsistent}");
            sb.AppendLine($"p6.audit.laneDamage={laneDamageTotal}/{_totalDamageDealt}");
            sb.AppendLine($"p6.audit.towerDamage={towerDamageTotal}/{_totalDamageDealt}");
            sb.AppendLine($"p6.audit.segmentDamage={segmentDamageTotal}/{_totalDamageDealt}");
            sb.AppendLine($"p6.audit.kills={laneKillTotal}/{_totalKills}");
            sb.AppendLine($"p6.audit.escapes={laneEscapeTotal}/{_totalEscapes}");
            sb.AppendLine($"p6.audit.segmentKills={segmentKillTotal}/{_totalKills}");
            sb.AppendLine($"p6.audit.segmentEscapes={segmentEscapeTotal}/{_totalEscapes}");
            sb.AppendLine($"p6.tower.count={towers.Count}");
            for (var i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                var towerObject = transform.Find(tower.towerId);
                var liveTower = towerObject != null ? towerObject.GetComponent<TDTower>() : null;
                var ultimateId = liveTower?.ActiveSpecialization?.specializationId ?? "none";
                sb.AppendLine(
                    $"p6.tower.{i}=id:{tower.towerId},kind:{tower.kind},cell:{tower.cell.x},{tower.cell.y},damage:{tower.damageDealt}," +
                    $"kills:{tower.kills},hits:{tower.hits},controls:{tower.controlApplications},counterDamage:{tower.counterDamage}," +
                    $"spend:{tower.TotalSpend},upgrades:{tower.upgrades},damageSpec:{tower.damageSpecProcs},utilitySpec:{tower.utilitySpecProcs}," +
                    $"ultimate:{ultimateId},affected:{tower.ultimateAffectedTargets},matrixFull:{tower.matrixFullMatches}");
            }

            sb.AppendLine($"p6.segment.count={segments.Count}");
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var report = FindRoadHeatReport(heatReports, segment.laneKey, segment.segmentIndex);
                var suggestedCell = report != null && report.hasSuggestedCell
                    ? $"{report.suggestedCell.x},{report.suggestedCell.y}"
                    : "none";
                sb.AppendLine(
                    $"p6.segment.{i}=lane:{segment.laneKey},index:{segment.segmentIndex},label:{GetRoadSegmentLabel(segment.segmentIndex)}," +
                    $"reached:{segment.reached},damage:{segment.damageDealt},kills:{segment.kills},escapes:{segment.escapes}," +
                    $"unresolved:{segment.unresolvedAtEnd},controls:{segment.controlApplications},counterDamage:{segment.counterDamage}," +
                    $"coverage:{report?.coverageScore ?? 0},heat:{report?.heatScore ?? 0},suggested:{suggestedCell}");
            }

            var hotspotCount = Mathf.Min(3, heatReports.Count);
            sb.AppendLine($"p6.hotspot.count={hotspotCount}");
            for (var i = 0; i < hotspotCount; i++)
            {
                var hotspot = heatReports[i];
                var suggestedCell = hotspot.hasSuggestedCell
                    ? $"{hotspot.suggestedCell.x},{hotspot.suggestedCell.y}"
                    : "none";
                sb.AppendLine(
                    $"p6.hotspot.{i}=lane:{hotspot.stat.laneKey},segment:{GetRoadSegmentLabel(hotspot.stat.segmentIndex)}," +
                    $"heat:{hotspot.heatScore},coverage:{hotspot.coverageScore},reached:{hotspot.stat.reached}," +
                    $"escapes:{hotspot.stat.escapes},unresolved:{hotspot.stat.unresolvedAtEnd},suggested:{suggestedCell}");
            }

            var recommendations = BuildRunRecommendationLabel().Split('\n');
            sb.AppendLine($"p6.recommendation.count={recommendations.Length}");
            for (var i = 0; i < recommendations.Length; i++)
            {
                sb.AppendLine($"p6.recommendation.{i}={recommendations[i]}");
            }

            var definitions = TDTower.GetSpecializationDefinitions();
            var specializationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var specializationBranches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            sb.AppendLine($"p7.matrix.count={definitions.Count}");
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                specializationIds.Add(definition.specializationId);
                specializationBranches.Add($"{definition.towerKind}:{definition.branch}");
                var traits = definition.counterTags == null ? string.Empty : string.Join("|", definition.counterTags);
                sb.AppendLine(
                    $"p7.matrix.{i}=id:{definition.specializationId},tower:{definition.towerKind},branch:{definition.branch}," +
                    $"name:{definition.displayName},traits:{traits},resonance:{TDTower.GetResonanceAffinityLabel(definition.resonanceAffinity)}");

                var procs = _ultimateProcCounts.TryGetValue(definition.specializationId, out var procCount) ? procCount : 0;
                var fullMatches = _ultimateFullMatchCounts.TryGetValue(definition.specializationId, out var fullCount) ? fullCount : 0;
                sb.AppendLine($"p7.ultimate.{i}=id:{definition.specializationId},procs:{procs},fullMatches:{fullMatches}");
            }

            sb.AppendLine($"p7.matrix.runtime=opportunities:{_matrixOpportunities},traitMatches:{_matrixTraitMatches},resonanceMatches:{_matrixResonanceMatches},fullMatches:{_matrixFullMatches}");
            sb.AppendLine(
                $"p7.convergence=triggers:{_matrixConvergenceTriggers},ember:{_matrixEmberConvergenceTriggers},fracture:{_matrixFractureConvergenceTriggers}," +
                $"fractureTargets:{_matrixFractureConvergenceAffectedTargets},emberExtension:{_matrixEmberConvergenceWindowSeconds:0.00}");
            sb.AppendLine($"p7.window.best=sync:{_matrixBestWindowSync},specializations:{_matrixBestWindowSpecializations}");
            sb.AppendLine($"p7.window.live=sync:{_matrixWindowSync},specializations:{_matrixWindowSpecializationIds.Count},convergence:{_matrixConvergenceTriggeredThisWindow}");
            sb.AppendLine($"p7.audit.uniqueIds={specializationIds.Count == definitions.Count}");
            sb.AppendLine($"p7.audit.allBranches={specializationBranches.Count == 16}");

            return sb.ToString();
        }

        private static TDRoadHeatReport FindRoadHeatReport(List<TDRoadHeatReport> reports, string laneKey, int segmentIndex)
        {
            if (reports == null)
            {
                return null;
            }

            for (var i = 0; i < reports.Count; i++)
            {
                var report = reports[i];
                if (report?.stat != null && report.stat.segmentIndex == segmentIndex &&
                    string.Equals(report.stat.laneKey, laneKey, StringComparison.OrdinalIgnoreCase))
                {
                    return report;
                }
            }

            return null;
        }

        public string DebugBuildTowerAtCell(int x, int y, TDTowerKind kind)
        {
            if (_gridMap == null)
            {
                return "skip: grid unavailable";
            }

            if (!IsBuildWindowOpen())
            {
                return "skip: build window closed";
            }

            if (!IsTowerUnlocked(kind))
            {
                return $"skip: {kind} is not in the active formation";
            }

            var cell = new Vector2Int(x, y);
            if (!_gridMap.IsBuildable(cell))
            {
                return $"skip: {kind} at {x},{y} is not buildable";
            }

            var cost = TDTower.GetBuildCost(kind);
            if (_defenseBudget < cost)
            {
                return $"skip: {kind} at {x},{y} needs {cost}, budget {_defenseBudget}";
            }

            _defenseBudget -= cost;
            _budgetSpentOnBuilds += cost;
            _gridMap.SetTower(cell, true);
            var tower = SpawnTower(cell, kind);
            SelectTowerForUi(tower);
            _builtTowerCount++;
            PushTacticalEvent($"Build: {GetTowerKindLabel(kind)} at {x},{y} (-{cost})", 4.2f);
            SetStatus($"Debug built {GetTowerKindLabel(kind)} at {x},{y}");
            return $"built {kind} at {x},{y} cost={cost} budget={_defenseBudget}";
        }

        public string DebugRequestStartWave()
        {
            if (_gameOver)
            {
                return "skip: game over";
            }

            if (!_isInPrepPhase)
            {
                return "skip: not in prep";
            }

            if (IsOpeningWaveBuildRequired())
            {
                return "skip: first tower required";
            }

            TryRequestWaveStart();
            return _waveStartRequested
                ? $"start requested wave={_wave} readiness={_lastWaveStartReadinessScore}{_lastWaveStartReadinessGrade}"
                : $"skip: start request rejected wave={_wave}";
        }

        public string DebugPauseConfiguredWavesForTest()
        {
            var stopped = _waveRoutine != null;
            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
                _waveRoutine = null;
            }

            _isInPrepPhase = false;
            _waveStartRequested = false;
            HideRoutePreview();
            return $"configuredWavesPaused={stopped} wave={_wave}";
        }

        public string DebugDeployCurrentMissionForTest()
        {
            if (_gameOver)
            {
                return "skip: game over";
            }

            if (_campaignRoute?.level == null)
            {
                _campaignDeploymentConfirmed = true;
                _missionBoardOpen = false;
                return "deployed fallback mission";
            }

            _missionBoardSelectedLevel = _campaignRoute.level.levelIndex;
            _missionBoardOpen = false;
            _campaignDeploymentConfirmed = true;
            _missionBoardNeedsRefresh = true;
            return $"deployed level={_campaignRoute.level.levelIndex} boardOpen={_missionBoardOpen}";
        }

        public string DebugConfigureFormationForTest(
            string towerNames,
            string doctrineName = "Adaptive",
            string difficultyName = "Standard")
        {
            var available = GetTowerKindsUnlockedAtLevel(_campaignRoute?.level?.levelIndex ?? DefaultCampaignLevelIndex);
            var parsed = new List<TDTowerKind>(TDCampaignProgression.MaxFormationTowers);
            var tokens = string.IsNullOrWhiteSpace(towerNames)
                ? Array.Empty<string>()
                : towerNames.Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length && parsed.Count < TDCampaignProgression.MaxFormationTowers; i++)
            {
                if (Enum.TryParse(tokens[i].Trim(), true, out TDTowerKind kind) &&
                    available.Contains(kind) &&
                    !parsed.Contains(kind))
                {
                    parsed.Add(kind);
                }
            }

            if (parsed.Count == 0)
            {
                return "skip: no available formation towers parsed";
            }

            if (!Enum.TryParse(doctrineName, true, out TDResonanceDoctrine doctrine))
            {
                doctrine = TDResonanceDoctrine.Adaptive;
            }

            _unlockedTowerKinds.Clear();
            _unlockedTowerKinds.AddRange(parsed);
            _activeResonanceDoctrine = doctrine;
            if (!TryParseCampaignDifficulty(difficultyName, out var difficulty))
            {
                return $"skip: unknown campaign difficulty {difficultyName}";
            }

            if (!IsDifficultyAvailableForLevel(_campaignRoute?.level?.levelIndex ?? DefaultCampaignLevelIndex, difficulty))
            {
                return $"skip: campaign difficulty {difficulty} locked";
            }

            _activeCampaignDifficulty = difficulty;
            if (_campaignRoute?.level != null)
            {
                TDCampaignProgression.SaveDifficultyPreference(_campaignRoute.level.levelIndex, difficulty);
                ResetMissionRuntimeRules();
                ApplyMissionRuntimeRules(_campaignRoute.level);
            }

            if (!_unlockedTowerKinds.Contains(_selectedTowerKind))
            {
                _selectedTowerKind = _unlockedTowerKinds[0];
            }

            RebuildTowerBuildButtons();
            return $"formation={string.Join(",", _unlockedTowerKinds)} doctrine={_activeResonanceDoctrine} difficulty={_activeCampaignDifficulty} slots={_unlockedTowerKinds.Count}/{TDCampaignProgression.MaxFormationTowers}";
        }

        public string DebugOpenFormationForTest(int levelIndex = 0)
        {
            if (_campaignRoute?.level == null || _campaign == null)
            {
                return "skip: campaign unavailable";
            }

            var selectedLevel = levelIndex <= 0 ? _campaignRoute.level.levelIndex : levelIndex;
            if (!TDCampaignProgression.IsLevelUnlocked(selectedLevel, _campaign.totalLevels))
            {
                return $"skip: level {selectedLevel} locked";
            }

            _missionBoardOpen = true;
            _missionBoardSelectedLevel = selectedLevel;
            _missionBoardNeedsRefresh = true;
            OpenFormationPanel();
            return $"formationOpen={_formationPanelOpen} level={selectedLevel} towers={_formationDraftTowerKinds.Count} doctrine={_formationDraftDoctrine} difficulty={_formationDraftDifficulty}";
        }

        public string DebugPrepareP85DifficultyForTest(string difficultyName = "Standard")
        {
            if (_campaignRoute?.level == null || _campaign == null ||
                !TryParseCampaignDifficulty(difficultyName, out var difficulty))
            {
                return $"skip: invalid P8.5 difficulty {difficultyName}";
            }

            if (difficulty == TDCampaignDifficultyTier.Veteran)
            {
                var chapter = GetCampaignChapter(_campaignRoute.level.chapterId);
                for (var level = chapter.startLevel; level <= chapter.endLevel; level++)
                {
                    TDCampaignProgression.RecordResult(level, true, 2, 82, 16, _campaign.totalLevels);
                }
            }
            else if (difficulty == TDCampaignDifficultyTier.EmberTrial)
            {
                for (var level = 1; level <= _campaign.totalLevels; level++)
                {
                    TDCampaignProgression.RecordResult(level, true, 2, 82, 16, _campaign.totalLevels);
                }
            }

            if (!IsDifficultyAvailableForLevel(_campaignRoute.level.levelIndex, difficulty))
            {
                return $"skip: P8.5 difficulty {difficulty} remained locked";
            }

            _activeCampaignDifficulty = difficulty;
            _formationDraftDifficulty = difficulty;
            TDCampaignProgression.SaveDifficultyPreference(_campaignRoute.level.levelIndex, difficulty);
            ResetMissionRuntimeRules();
            ApplyMissionRuntimeRules(_campaignRoute.level);
            _missionBoardNeedsRefresh = true;
            return $"p8.5.fixture.difficulty={difficulty} runtime={BuildCurrentDifficultyRuntimeSignature()}";
        }

        public string DebugPrepareP85CampaignPerfectedForTest()
        {
            if (_campaignRoute?.level == null || _campaign == null ||
                _campaignRoute.level.levelIndex != _campaign.totalLevels)
            {
                return "skip: final campaign level required";
            }

            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
                _waveRoutine = null;
            }

            for (var level = 1; level <= _campaign.totalLevels; level++)
            {
                TDCampaignProgression.RecordResult(
                    level,
                    true,
                    3,
                    96,
                    20,
                    _campaign.totalLevels,
                    true,
                    TDCampaignDifficultyTier.EmberTrial);
            }

            for (var i = 0; i < _campaign.chapters.Length; i++)
            {
                TDCampaignProgression.ClaimChapterReward(_campaign.chapters[i]?.reward?.rewardId);
            }

            _activeCampaignDifficulty = TDCampaignDifficultyTier.EmberTrial;
            TDCampaignProgression.SaveDifficultyPreference(
                _campaignRoute.level.levelIndex,
                TDCampaignDifficultyTier.EmberTrial);
            _newlyClaimedChapterReward = null;
            ResetMissionRuntimeRules();
            ApplyMissionRuntimeRules(_campaignRoute.level);
            _gameOver = true;
            _victory = true;
            _campaignResultRecorded = true;
            _currentMissionStars = 3;
            _currentMissionContractCompleted = true;
            _missionBoardOpen = false;
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _missionBoardNeedsRefresh = true;
            UpdateBattleUi();
            var summary = GetCampaignProgressSummary();
            return $"campaignPerfected={summary.emberTrialClears == summary.totalLevels} ember={summary.emberTrialClears}/{summary.totalLevels}";
        }

        public string DebugShowRunResultForTest(bool victory = false, bool persistCampaignProgress = false)
        {
            if (_gameOver)
            {
                return "skip: run result already active";
            }

            FinalizeCurrentWaveStat(victory);
            _gameOver = true;
            _victory = victory;
            ResetResonanceState();
            ClearActiveEnemiesAfterRun();
            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
                _waveRoutine = null;
            }

            _currentMissionStars = CalculateCurrentMissionStars();
            _currentMissionContractCompleted = EvaluateCurrentMissionContract()?.completed ?? false;
            if (persistCampaignProgress)
            {
                RecordCampaignResultIfNeeded();
            }

            LogRunSummary();
            return $"runResult active victory={victory} wave={_wave} stars={_currentMissionStars} contract={_currentMissionContractCompleted} persisted={persistCampaignProgress}";
        }

        public string DebugGetP8CampaignReport()
        {
            var totalLevels = _campaign?.totalLevels ?? 1;
            var summary = GetCampaignProgressSummary();
            var routeLevel = _campaignRoute?.level?.levelIndex ?? 1;
            var selectedLevel = GetCampaignLevel(_missionBoardSelectedLevel) ?? _campaignRoute?.level;
            var selectedProgress = TDCampaignProgression.GetLevelProgress(selectedLevel?.levelIndex ?? routeLevel);
            var unlockedButtons = 0;
            for (var i = 0; i < _uiMissionLevelButtons.Count; i++)
            {
                if (_uiMissionLevelButtons[i] != null && _uiMissionLevelButtons[i].interactable)
                {
                    unlockedButtons++;
                }
            }

            BuildMissionWaveIntel(selectedLevel, out var waves, out var lanes, out _, out var tags, out var intelError);
            var contractReport = EvaluateCurrentMissionContract();
            var progressConsistent = summary.clearedLevels >= 0 &&
                                     summary.clearedLevels <= summary.totalLevels &&
                                     summary.earnedStars >= summary.clearedLevels &&
                                     summary.earnedStars <= summary.availableStars &&
                                     summary.completedContracts >= 0 &&
                                     summary.completedContracts <= summary.availableContracts &&
                                     summary.availableContracts == summary.totalLevels &&
                                     summary.highestUnlockedLevel >= 1 &&
                                     summary.highestUnlockedLevel <= summary.totalLevels;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"p8.save.version={TDCampaignProgression.SaveVersion}");
            sb.AppendLine($"p8.progress.cleared={summary.clearedLevels}/{summary.totalLevels}");
            sb.AppendLine($"p8.progress.stars={summary.earnedStars}/{summary.availableStars}");
            sb.AppendLine($"p8.progress.contracts={summary.completedContracts}/{summary.availableContracts}");
            sb.AppendLine($"p8.progress.frontier={summary.highestUnlockedLevel}");
            sb.AppendLine($"p8.route.level={routeLevel}");
            sb.AppendLine($"p8.route.unlocked={TDCampaignProgression.IsLevelUnlocked(routeLevel, totalLevels)}");
            sb.AppendLine($"p8.ui.open={_missionBoardOpen}");
            sb.AppendLine($"p8.ui.deploymentConfirmed={_campaignDeploymentConfirmed}");
            sb.AppendLine($"p8.ui.selected={_missionBoardSelectedLevel}");
            sb.AppendLine($"p8.ui.levelButtons={_uiMissionLevelButtons.Count}");
            sb.AppendLine($"p8.ui.unlockedButtons={unlockedButtons}");
            sb.AppendLine($"p8.intel.waves={waves}");
            sb.AppendLine($"p8.intel.lanes={lanes}");
            sb.AppendLine($"p8.intel.tags={tags.Count}");
            sb.AppendLine($"p8.intel.valid={string.IsNullOrWhiteSpace(intelError)}");
            sb.AppendLine($"p8.selected.cleared={selectedProgress.cleared}");
            sb.AppendLine($"p8.selected.bestStars={selectedProgress.bestStars}");
            sb.AppendLine($"p8.selected.bestScore={selectedProgress.bestTacticalScore}");
            sb.AppendLine($"p8.selected.contract={selectedProgress.contractCompleted}");
            sb.AppendLine($"p8.result.recorded={_campaignResultRecorded}");
            sb.AppendLine($"p8.result.stars={_currentMissionStars}");
            sb.AppendLine($"p8.result.contract={_currentMissionContractCompleted}");
            sb.AppendLine($"p8.2.contract.id={contractReport?.contract?.contractId ?? "none"}");
            sb.AppendLine($"p8.2.contract.metric={contractReport?.contract?.metric ?? "none"}");
            sb.AppendLine($"p8.2.contract.value={contractReport?.currentValue ?? 0}/{contractReport?.contract?.target ?? 0}");
            sb.AppendLine($"p8.2.contract.targetMet={contractReport?.targetMet ?? false}");
            sb.AppendLine($"p8.2.contract.completed={contractReport?.completed ?? false}");
            sb.AppendLine($"p8.2.mutators={_campaignRoute?.level?.mutators?.Length ?? 0}");
            sb.AppendLine($"p8.2.runtime.start=budget:{_startingDefenseBudget},integrity:{_startingLineIntegrity}");
            sb.AppendLine($"p8.2.runtime.enemy=hpX:{_missionEnemyHpMultiplier:0.##},speedX:{_missionEnemySpeedMultiplier:0.##},armor:{_missionEnemyArmorBonus}");
            sb.AppendLine($"p8.2.runtime.economy=rewardX:{_missionRewardMultiplier:0.##},resonanceX:{_missionResonanceGainMultiplier:0.##}");
            sb.AppendLine($"p8.audit.progressConsistent={progressConsistent}");
            sb.AppendLine($"p8.audit.currentUnlocked={TDCampaignProgression.IsLevelUnlocked(routeLevel, totalLevels)}");
            sb.Append(DebugGetP83FormationReport());
            sb.Append(DebugGetP84CampaignReport());
            sb.Append(DebugGetP85DifficultyReport());
            sb.Append(DebugGetP86ScenarioReport());
            return sb.ToString();
        }

        public string DebugGetP84CampaignReport()
        {
            var summary = GetCampaignProgressSummary();
            var masteredChapters = GetMasteredChapterCount();
            var claimedRewards = TDCampaignProgression.GetClaimedChapterRewardIds();
            CalculateClaimedChapterRewardBonuses(out var budget, out var integrity, out var resonance, out _);
            var portableSave = TDCampaignProgression.ExportPortableSave(_campaign?.totalLevels ?? 1);
            var previewValid = TDCampaignProgression.TryPreviewPortableSave(
                portableSave,
                _campaign?.totalLevels ?? 1,
                out var preview,
                out _);
            return
                $"p8.4.chapters.total={_campaign?.chapters?.Length ?? 0}\n" +
                $"p8.4.chapters.mastered={masteredChapters}\n" +
                $"p8.4.rewards.claimed={claimedRewards.Length}\n" +
                $"p8.4.rewards.ids={(claimedRewards.Length == 0 ? "none" : string.Join(",", claimedRewards))}\n" +
                $"p8.4.runtime.legacy=budget:{budget},integrity:{integrity},resonanceX:{resonance:0.00}\n" +
                $"p8.4.campaign.rank={BuildCampaignRank(summary, masteredChapters)}\n" +
                $"p8.4.campaign.complete={summary.clearedLevels == summary.totalLevels}\n" +
                $"p8.4.result.archive={IsFullCampaignCompletionResult(summary)}\n" +
                $"p8.4.profile.open={_campaignProfileOpen}\n" +
                $"p8.4.profile.previewValid={previewValid}\n" +
                $"p8.4.profile.id={preview?.fingerprint ?? "none"}\n" +
                $"p8.4.profile.codeLength={preview?.codeLength ?? 0}\n";
        }

        public string DebugGetP85DifficultyReport()
        {
            var level = _campaignRoute?.level;
            var progress = TDCampaignProgression.GetLevelProgress(level?.levelIndex ?? 1);
            var summary = GetCampaignProgressSummary();
            var remixCount = 0;
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            for (var i = 0; i < chapters.Length; i++)
            {
                if (chapters[i]?.challengeRemix != null)
                {
                    remixCount++;
                }
            }

            return
                $"p8.5.config.tiers={_campaign?.difficultyTiers?.Length ?? 0}\n" +
                $"p8.5.config.remixes={remixCount}/{chapters.Length}\n" +
                $"p8.5.active={_activeCampaignDifficulty}\n" +
                $"p8.5.preference={TDCampaignProgression.GetDifficultyPreference(level?.levelIndex ?? 1)}\n" +
                $"p8.5.record={GetDifficultyRecordLabel(progress)}\n" +
                $"p8.5.available.veteran={IsDifficultyAvailableForLevel(level?.levelIndex ?? 1, TDCampaignDifficultyTier.Veteran)}\n" +
                $"p8.5.available.ember={IsDifficultyAvailableForLevel(level?.levelIndex ?? 1, TDCampaignDifficultyTier.EmberTrial)}\n" +
                $"p8.5.progress.veteran={summary.veteranClears}/{summary.totalLevels}\n" +
                $"p8.5.progress.ember={summary.emberTrialClears}/{summary.totalLevels}\n" +
                $"p8.5.runtime={BuildCurrentDifficultyRuntimeSignature()}\n" +
                $"p8.5.audit.runtimeMatches={DoesCurrentDifficultyRuntimeMatch()}\n";
        }

        public string DebugGetP86ScenarioReport()
        {
            var totalLevels = _campaign?.totalLevels ?? 1;
            var slots = TDCampaignProgression.GetSaveSlotSummaries(totalLevels);
            var examCount = 0;
            var levels = _campaign?.levels ?? Array.Empty<TDCampaignLevelDefinition>();
            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[i]?.scenario?.milestoneExam == true)
                {
                    examCount++;
                }
            }

            return
                $"p8.6.save.activeSlot={TDCampaignProgression.ActiveSaveSlot}\n" +
                $"p8.6.save.slots={slots.Length}\n" +
                $"p8.6.save.revision={slots.FirstOrDefault(slot => slot.slotId == TDCampaignProgression.ActiveSaveSlot)?.revision ?? 0}\n" +
                $"p8.6.cloud.prefix={TDCampaignProgression.CloudSavePrefix}\n" +
                $"p8.6.maps.mechanics={_campaign?.maps?.Length ?? 0}\n" +
                $"p8.6.exams={examCount}\n" +
                $"p8.6.runtime.mechanic={_activeScenarioMechanic?.mechanicId ?? "none"}\n" +
                $"p8.6.runtime.type={_activeScenarioMechanic?.mechanicType ?? "none"}\n" +
                $"p8.6.runtime.uses={_scenarioUses}/{_scenarioOpportunities}\n" +
                $"p8.6.runtime.routeBias={_scenarioRouteBias}\n";
        }

        public string DebugActivateP86ScenarioForTest()
        {
            if (_activeScenarioMechanic == null)
            {
                return "p8.6.fixture.applied=False type=none error=mechanic unavailable";
            }

            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
                _waveRoutine = null;
            }

            _campaignDeploymentConfirmed = true;
            _missionBoardOpen = false;
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _currentWavePhase = "reinforce";
            _defenseBudget += 100;
            var type = NormalizeGroupToken(_activeScenarioMechanic.mechanicType);
            if (type == "environment_device" || type == "boss_phase")
            {
                _isInPrepPhase = false;
                var fixtureId = type == "boss_phase" ? "husk_titan" : "skitter_runner";
                if (_enemyCatalog.TryGetValue(fixtureId, out var entry))
                {
                    SpawnEnemy(entry, GetDefaultSpawnPath(), Mathf.Max(1, _wave), 30001, "default");
                }
            }
            else
            {
                _isInPrepPhase = true;
            }

            TryActivateScenarioMechanic();
            var applied = type switch
            {
                "signal_gate" => _scenarioWaveDelayBonus > 0f,
                "timed_reinforcement" => _scenarioReinforcementPending,
                "route_switch" => !string.Equals(_scenarioRouteBias, "center", StringComparison.Ordinal),
                "environment_device" => _activeEnemies.Any(enemy => enemy != null && enemy.IsArmorBroken && enemy.IsSlowed),
                "boss_phase" => _scenarioBossPhaseSuppressed && _activeEnemies.Any(enemy => enemy != null && enemy.IsExposed),
                _ => false
            };
            UpdateBattleUi();
            return $"p8.6.fixture.applied={applied} type={type} uses={_scenarioUses} charges={_scenarioCharges} route={_scenarioRouteBias} enemies={_activeEnemies.Count}";
        }

        public string DebugOpenCampaignProfileForTest()
        {
            if (_campaign == null)
            {
                return "skip: campaign unavailable";
            }

            _missionBoardOpen = true;
            _formationPanelOpen = false;
            OpenCampaignProfile();
            RefreshMissionBoardUi();
            return $"profileOpen={_campaignProfileOpen} rewards={TDCampaignProgression.GetClaimedChapterRewardIds().Length}";
        }

        public string DebugPrepareP84ChapterBoardForTest()
        {
            if (_campaign?.chapters == null || _campaign.chapters.Length == 0)
            {
                return "skip: campaign unavailable";
            }

            var chapter = _campaign.chapters[0];
            for (var level = chapter.startLevel; level <= chapter.endLevel; level++)
            {
                TDCampaignProgression.RecordResult(level, true, 3, 92, 20, _campaign.totalLevels, true);
            }

            TDCampaignProgression.ClaimChapterReward(chapter.reward.rewardId);
            ResetMissionRuntimeRules();
            ApplyMissionRuntimeRules(_campaignRoute?.level);
            _missionBoardOpen = true;
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _missionBoardSelectedLevel = chapter.endLevel;
            _missionBoardNeedsRefresh = true;
            RefreshMissionBoardUi();
            var progress = TDCampaignProgression.BuildChapterSummary(chapter);
            return $"chapterBoard=A cleared={progress.clearedLevels}/{progress.totalLevels} stars={progress.earnedStars}/{progress.availableStars} contracts={progress.completedContracts}/{progress.availableContracts} reward={progress.rewardClaimed}";
        }

        public string DebugPrepareP84CampaignCompletionForTest()
        {
            if (_campaignRoute?.level == null || _campaign == null ||
                _campaignRoute.level.levelIndex != _campaign.totalLevels)
            {
                return "skip: final campaign level required";
            }

            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
                _waveRoutine = null;
            }

            for (var level = 1; level <= _campaign.totalLevels; level++)
            {
                TDCampaignProgression.RecordResult(level, true, 3, 94, 20, _campaign.totalLevels, true);
            }

            for (var i = 0; i < _campaign.chapters.Length; i++)
            {
                TDCampaignProgression.ClaimChapterReward(_campaign.chapters[i]?.reward?.rewardId);
            }

            _newlyClaimedChapterReward = null;
            ResetMissionRuntimeRules();
            ApplyMissionRuntimeRules(_campaignRoute.level);
            _gameOver = true;
            _victory = true;
            _campaignResultRecorded = true;
            _currentMissionStars = 3;
            _currentMissionContractCompleted = true;
            _missionBoardOpen = false;
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _missionBoardNeedsRefresh = true;
            UpdateBattleUi();
            var summary = GetCampaignProgressSummary();
            return $"campaignCompletion={IsFullCampaignCompletionResult(summary)} rank={BuildCampaignRank(summary, GetMasteredChapterCount())} rewards={TDCampaignProgression.GetClaimedChapterRewardIds().Length}";
        }

        public string DebugGetP83FormationReport()
        {
            var level = _campaignRoute?.level;
            if (level == null)
            {
                return "p8.3.available=false\n";
            }

            BuildMissionWaveIntel(level, out _, out _, out _, out var threatTags, out _);
            var report = CalculateFormationFit(level, _unlockedTowerKinds, _activeResonanceDoctrine, threatTags);
            var persisted = TDCampaignProgression.GetTowerLoadout(level.levelIndex);
            return
                $"p8.3.available=true\n" +
                $"p8.3.level={level.levelIndex}\n" +
                $"p8.3.pool={string.Join(",", _availableTowerKinds)}\n" +
                $"p8.3.formation={string.Join(",", _unlockedTowerKinds)}\n" +
                $"p8.3.formation.slots={_unlockedTowerKinds.Count}/{TDCampaignProgression.MaxFormationTowers}\n" +
                $"p8.3.formation.persisted={(persisted.Length == 0 ? "auto" : string.Join(",", persisted))}\n" +
                $"p8.3.doctrine={_activeResonanceDoctrine}\n" +
                $"p8.3.doctrine.available={IsDoctrineAvailableForLevel(level.levelIndex)}\n" +
                $"p8.3.doctrine.livePower={GetDoctrineCommandPowerMultiplier(_activeResonanceCommand):0.00}\n" +
                $"p8.3.doctrine.empoweredCommands={_doctrineEmpoweredCommands}\n" +
                $"p8.3.fit.total={report.total}\n" +
                $"p8.3.fit.grade={report.grade}\n" +
                $"p8.3.fit.coverage={report.coverage}\n" +
                $"p8.3.fit.matrix={report.matrix}\n" +
                $"p8.3.fit.doctrine={report.doctrine}\n" +
                $"p8.3.fit.covered={report.coveredCategories}\n" +
                $"p8.3.fit.gaps={report.gapCategories}\n";
        }

        public string DebugAuditP86ForTest()
        {
            if (_campaign?.levels == null || _campaign.maps == null)
            {
                return "p8.6.audit.pass=False\np8.6.audit.error=campaign unavailable\n";
            }

            var totalLevels = _campaign.totalLevels;
            var originalSlot = TDCampaignProgression.ActiveSaveSlot;
            var originalClipboard = GUIUtility.systemCopyBuffer;
            var snapshots = new string[TDCampaignProgression.MaxSaveSlots];
            for (var slot = 1; slot <= TDCampaignProgression.MaxSaveSlots; slot++)
            {
                TDCampaignProgression.SetActiveSaveSlot(slot, totalLevels, out _);
                snapshots[slot - 1] = TDCampaignProgression.ExportSnapshot(totalLevels);
            }

            var mechanicConfigPass = false;
            var grammarPass = false;
            var examsPass = false;
            var slotIsolationPass = false;
            var cloudPreviewPass = false;
            var cloudMergePass = false;
            var keepLocalPass = false;
            var useCloudPass = false;
            var legacyMigrationPass = false;
            try
            {
                var mechanicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var mechanicTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < _campaign.maps.Length; i++)
                {
                    var mechanic = _campaign.maps[i]?.mechanic;
                    if (mechanic != null)
                    {
                        mechanicIds.Add(mechanic.mechanicId);
                        mechanicTypes.Add(mechanic.mechanicType);
                    }
                }

                mechanicConfigPass = _campaign.maps.Length == 5 && mechanicIds.Count == 5 && mechanicTypes.Count == 5;

                var grammarValid = 0;
                var examLevels = new List<int>();
                for (var i = 0; i < _campaign.levels.Length; i++)
                {
                    var level = _campaign.levels[i];
                    if (level?.scenario?.milestoneExam == true)
                    {
                        examLevels.Add(level.levelIndex);
                    }

                    if (!TDWaveLoader.TryLoadFromResources(
                            $"Data/waves/{level.waveSetId}",
                            _globalEnemyCatalog,
                            out var waveSet,
                            out _))
                    {
                        continue;
                    }

                    var phases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var w = 0; w < waveSet.waves.Length; w++)
                    {
                        phases.Add(waveSet.waves[w].phase);
                    }

                    if (phases.Contains("introduce") && phases.Contains("reinforce") &&
                        (phases.Contains("exam") || phases.Contains("boss")))
                    {
                        grammarValid++;
                    }
                }

                grammarPass = grammarValid == _campaign.levels.Length && grammarValid == 20;
                examsPass = string.Join(",", examLevels) == "5,9,13,17,20";

                TDCampaignProgression.SetActiveSaveSlot(1, totalLevels, out _);
                TDCampaignProgression.ResetProgress(totalLevels);
                TDCampaignProgression.RecordResult(1, true, 2, 72, 14, totalLevels);
                var cloudCode = TDCampaignProgression.ExportCloudEnvelope(totalLevels);
                cloudPreviewPass = TDCampaignProgression.TryPreviewCloudEnvelope(
                    cloudCode,
                    totalLevels,
                    out var cloudPreview,
                    out _) && cloudPreview.slotId == 1 && cloudPreview.progress.clearedLevels == 1;

                TDCampaignProgression.ResetProgress(totalLevels);
                TDCampaignProgression.RecordResult(2, true, 3, 91, 18, totalLevels, true);
                cloudMergePass = TDCampaignProgression.TryResolveCloudEnvelope(
                                     cloudCode,
                                     totalLevels,
                                     TDCampaignCloudConflictResolution.Merge,
                                     out _,
                                     out _) &&
                                 TDCampaignProgression.GetLevelProgress(1).cleared &&
                                 TDCampaignProgression.GetLevelProgress(2).cleared &&
                                 TDCampaignProgression.GetLevelProgress(1).bestStars == 2 &&
                                 TDCampaignProgression.GetLevelProgress(2).bestStars == 3;

                TDCampaignProgression.ResetProgress(totalLevels);
                TDCampaignProgression.RecordResult(2, true, 2, 70, 12, totalLevels);
                keepLocalPass = TDCampaignProgression.TryResolveCloudEnvelope(
                                    cloudCode,
                                    totalLevels,
                                    TDCampaignCloudConflictResolution.KeepLocal,
                                    out _,
                                    out _) &&
                                !TDCampaignProgression.GetLevelProgress(1).cleared &&
                                TDCampaignProgression.GetLevelProgress(2).cleared;
                useCloudPass = TDCampaignProgression.TryResolveCloudEnvelope(
                                   cloudCode,
                                   totalLevels,
                                   TDCampaignCloudConflictResolution.UseCloud,
                                   out _,
                                   out _) &&
                               TDCampaignProgression.GetLevelProgress(1).cleared &&
                               !TDCampaignProgression.GetLevelProgress(2).cleared;

                var legacyCode = TDCampaignProgression.DebugExportLegacyPortableSaveForTest(totalLevels);
                TDCampaignProgression.ResetProgress(totalLevels);
                legacyMigrationPass = TDCampaignProgression.TryImportPortableSave(
                                          legacyCode,
                                          totalLevels,
                                          out var migrated,
                                          out _) &&
                                      migrated.saveVersion == TDCampaignProgression.SaveVersion &&
                                      TDCampaignProgression.GetLevelProgress(1).cleared;

                TDCampaignProgression.SetActiveSaveSlot(2, totalLevels, out _);
                TDCampaignProgression.ResetProgress(totalLevels);
                var slotTwoEmpty = !TDCampaignProgression.GetLevelProgress(1).cleared;
                TDCampaignProgression.SetActiveSaveSlot(1, totalLevels, out _);
                slotIsolationPass = slotTwoEmpty && TDCampaignProgression.GetLevelProgress(1).cleared &&
                                    TDCampaignProgression.GetSaveSlotSummaries(totalLevels).Length == 3;
            }
            finally
            {
                for (var slot = 1; slot <= TDCampaignProgression.MaxSaveSlots; slot++)
                {
                    TDCampaignProgression.SetActiveSaveSlot(slot, totalLevels, out _);
                    TDCampaignProgression.ImportSnapshot(snapshots[slot - 1], totalLevels);
                }

                TDCampaignProgression.SetActiveSaveSlot(originalSlot, totalLevels, out _);
                GUIUtility.systemCopyBuffer = originalClipboard;
                _missionBoardNeedsRefresh = true;
            }

            var pass = mechanicConfigPass && grammarPass && examsPass && slotIsolationPass &&
                       cloudPreviewPass && cloudMergePass && keepLocalPass && useCloudPass && legacyMigrationPass;
            return
                $"p8.6.audit.mechanics={mechanicConfigPass}\n" +
                $"p8.6.audit.grammar20={grammarPass}\n" +
                $"p8.6.audit.exams={examsPass}\n" +
                $"p8.6.audit.slotIsolation={slotIsolationPass}\n" +
                $"p8.6.audit.cloudPreview={cloudPreviewPass}\n" +
                $"p8.6.audit.cloudMerge={cloudMergePass}\n" +
                $"p8.6.audit.keepLocal={keepLocalPass}\n" +
                $"p8.6.audit.useCloud={useCloudPass}\n" +
                $"p8.6.audit.legacyMigration={legacyMigrationPass}\n" +
                $"p8.6.audit.pass={pass}\n";
        }

        public string DebugAuditP83FormationForTest()
        {
            var levels = _campaign?.levels ?? Array.Empty<TDCampaignLevelDefinition>();
            var totalLevels = Mathf.Max(1, _campaign?.totalLevels ?? levels.Length);
            var progressionSnapshot = TDCampaignProgression.ExportSnapshot(totalLevels);
            var originalDraftLevel = _formationDraftLevel;
            var originalDraftDoctrine = _formationDraftDoctrine;
            var originalDraftTowers = new List<TDTowerKind>(_formationDraftTowerKinds);
            var originalFormationOpen = _formationPanelOpen;
            var originalDoctrine = _activeResonanceDoctrine;
            var originalThreatTags = new List<string>(_currentWaveThreatTagSet);
            var validAutoFits = 0;
            var boundedScores = 0;
            var autoNotWorse = 0;
            var textOverflow = new List<string>();
            var persistencePass = false;
            var snapshotPass = false;
            var doctrinePowerPass = false;
            try
            {
                for (var i = 0; i < levels.Length; i++)
                {
                    var level = levels[i];
                    if (level == null)
                    {
                        continue;
                    }

                    var available = GetTowerKindsUnlockedAtLevel(level.levelIndex);
                    BuildAutoFitFormation(level.levelIndex, available, out var fitted, out var doctrine);
                    BuildMissionWaveIntel(level, out _, out _, out _, out var tags, out _);
                    var fit = CalculateFormationFit(level, fitted, doctrine, tags);
                    var formationValid = fitted.Count > 0 &&
                                         fitted.Count <= TDCampaignProgression.MaxFormationTowers;
                    for (var towerIndex = 0; towerIndex < fitted.Count; towerIndex++)
                    {
                        formationValid &= available.Contains(fitted[towerIndex]);
                    }

                    if (formationValid)
                    {
                        validAutoFits++;
                    }

                    if (fit.total >= 0 && fit.total <= 100 &&
                        fit.coverage >= 0 && fit.coverage <= 100 &&
                        fit.matrix >= 0 && fit.matrix <= 100 &&
                        fit.doctrine >= 0 && fit.doctrine <= 100)
                    {
                        boundedScores++;
                    }

                    var baseline = new List<TDTowerKind> { available[0] };
                    var baselineFit = CalculateFormationFit(level, baseline, TDResonanceDoctrine.Adaptive, tags);
                    if (fit.total >= baselineFit.total)
                    {
                        autoNotWorse++;
                    }

                    _formationDraftLevel = level.levelIndex;
                    _formationDraftTowerKinds.Clear();
                    _formationDraftTowerKinds.AddRange(fitted);
                    _formationDraftDoctrine = doctrine;
                    _formationPanelOpen = true;
                    RefreshFormationPanelUi();
                    Canvas.ForceUpdateCanvases();
                    var criticalLabels = new[]
                    {
                        _uiFormationTitleText,
                        _uiFormationThreatText,
                        _uiFormationRosterText,
                        _uiFormationFitTitleText,
                        _uiFormationFitBodyText,
                        _uiFormationMatrixText,
                        _uiFormationLockText
                    };
                    for (var labelIndex = 0; labelIndex < criticalLabels.Length; labelIndex++)
                    {
                        var label = criticalLabels[labelIndex];
                        if (label != null && label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                        {
                            textOverflow.Add($"L{level.levelIndex:00}:{label.name}");
                        }
                    }

                    for (var labelIndex = 0; labelIndex < _uiFormationTowerButtonTexts.Count; labelIndex++)
                    {
                        var label = _uiFormationTowerButtonTexts[labelIndex];
                        if (label != null && label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                        {
                            textOverflow.Add($"L{level.levelIndex:00}:{label.transform.parent.name}");
                        }
                    }
                }

                var testIds = new List<string>();
                var buildOrder = TDTower.GetBuildOrder();
                for (var i = 0; i < buildOrder.Count && i < 5; i++)
                {
                    testIds.Add(TDTower.GetTowerId(buildOrder[i]));
                }

                testIds.Add(TDTower.GetTowerId(buildOrder[0]));
                var testLevel = _campaignRoute?.level?.levelIndex ?? 1;
                TDCampaignProgression.SaveFormation(testLevel, testIds, TDResonanceDoctrine.FractureMark);
                var stored = TDCampaignProgression.GetTowerLoadout(testLevel);
                persistencePass = stored.Length == TDCampaignProgression.MaxFormationTowers &&
                                  new HashSet<string>(stored, StringComparer.OrdinalIgnoreCase).Count == stored.Length &&
                                  TDCampaignProgression.GetResonanceDoctrine(testLevel) == TDResonanceDoctrine.FractureMark;
                var roundTripSnapshot = TDCampaignProgression.ExportSnapshot(totalLevels);
                TDCampaignProgression.ResetProgress(totalLevels);
                TDCampaignProgression.ImportSnapshot(roundTripSnapshot, totalLevels);
                var roundTripStored = TDCampaignProgression.GetTowerLoadout(testLevel);
                snapshotPass = roundTripStored.Length == stored.Length &&
                               string.Join(",", roundTripStored) == string.Join(",", stored) &&
                               TDCampaignProgression.GetResonanceDoctrine(testLevel) == TDResonanceDoctrine.FractureMark;

                _currentWaveThreatTagSet.Clear();
                _currentWaveThreatTagSet.Add("armored");
                _activeResonanceDoctrine = TDResonanceDoctrine.Adaptive;
                var adaptivePower = GetDoctrineCommandPowerMultiplier(TDResonanceCommand.EmberSurge);
                _activeResonanceDoctrine = TDResonanceDoctrine.EmberSurge;
                var emberPower = GetDoctrineCommandPowerMultiplier(TDResonanceCommand.EmberSurge);
                var emberOffPower = GetDoctrineCommandPowerMultiplier(TDResonanceCommand.FractureMark);
                _activeResonanceDoctrine = TDResonanceDoctrine.FractureMark;
                var fracturePower = GetDoctrineCommandPowerMultiplier(TDResonanceCommand.FractureMark);
                doctrinePowerPass = Mathf.Approximately(adaptivePower, AdaptiveDoctrinePowerMultiplier) &&
                                    Mathf.Approximately(emberPower, SpecializedDoctrinePowerMultiplier) &&
                                    Mathf.Approximately(emberOffPower, 1f) &&
                                    Mathf.Approximately(fracturePower, SpecializedDoctrinePowerMultiplier);
            }
            finally
            {
                TDCampaignProgression.ImportSnapshot(progressionSnapshot, totalLevels);
                _formationDraftLevel = originalDraftLevel;
                _formationDraftDoctrine = originalDraftDoctrine;
                _formationDraftTowerKinds.Clear();
                _formationDraftTowerKinds.AddRange(originalDraftTowers);
                _formationPanelOpen = originalFormationOpen;
                _activeResonanceDoctrine = originalDoctrine;
                _currentWaveThreatTagSet.Clear();
                for (var i = 0; i < originalThreatTags.Count; i++)
                {
                    _currentWaveThreatTagSet.Add(originalThreatTags[i]);
                }

                RefreshUnlockedTowerKinds();
                RebuildTowerBuildButtons();
                if (_uiFormationRoot != null)
                {
                    _uiFormationRoot.gameObject.SetActive(_missionBoardOpen && _formationPanelOpen);
                }

                _missionBoardNeedsRefresh = true;
            }

            var allContentValid = levels.Length > 0 && validAutoFits == levels.Length;
            var allScoresBounded = levels.Length > 0 && boundedScores == levels.Length;
            var autoFitPass = levels.Length > 0 && autoNotWorse == levels.Length;
            var textFitPass = textOverflow.Count == 0;
            var activeFormationPass = _unlockedTowerKinds.Count > 0 &&
                                      _unlockedTowerKinds.Count <= TDCampaignProgression.MaxFormationTowers &&
                                      _uiTowerButtons.Count == _unlockedTowerKinds.Count;
            var pass = allContentValid && allScoresBounded && autoFitPass && textFitPass &&
                       persistencePass && snapshotPass && doctrinePowerPass && activeFormationPass;
            return
                $"p8.3.audit.autoFits={validAutoFits}/{levels.Length}\n" +
                $"p8.3.audit.scoresBounded={boundedScores}/{levels.Length}\n" +
                $"p8.3.audit.autoNotWorse={autoNotWorse}/{levels.Length}\n" +
                $"p8.3.audit.persistence={persistencePass}\n" +
                $"p8.3.audit.snapshotRoundTrip={snapshotPass}\n" +
                $"p8.3.audit.doctrinePower={doctrinePowerPass}\n" +
                $"p8.3.audit.activeFormationLimit={activeFormationPass}\n" +
                $"p8.3.audit.allFormationTextFit={textFitPass}\n" +
                $"p8.3.audit.formationTextOverflow={(textFitPass ? "none" : string.Join(",", textOverflow))}\n" +
                $"p8.3.audit.pass={pass}\n";
        }

        public string DebugAuditP84CampaignForTest()
        {
            if (_campaign?.chapters == null || _campaign.chapters.Length == 0)
            {
                return "p8.4.audit.pass=False\np8.4.audit.error=campaign unavailable\n";
            }

            var totalLevels = _campaign.totalLevels;
            var originalSnapshot = TDCampaignProgression.ExportSnapshot(totalLevels);
            var originalMissionBoardOpen = _missionBoardOpen;
            var originalFormationOpen = _formationPanelOpen;
            var originalProfileOpen = _campaignProfileOpen;
            var originalSelection = _missionBoardSelectedLevel;
            var originalProfileStatus = _campaignProfileStatus;
            var originalImportArmed = _campaignProfileImportArmed;
            var originalResetArmed = _campaignProfileResetArmed;
            var originalPendingImport = _campaignProfilePendingImport;
            var originalClipboard = GUIUtility.systemCopyBuffer;
            var originalClipboardBuffer = _campaignClipboardBuffer;
            var originalStartingBudget = _startingDefenseBudget;
            var originalStartingIntegrity = _startingLineIntegrity;
            var originalResonanceMultiplier = _missionResonanceGainMultiplier;
            var originalRewardBudget = _chapterRewardBudgetBonus;
            var originalRewardIntegrity = _chapterRewardIntegrityBonus;
            var originalRewardResonance = _chapterRewardResonanceMultiplier;
            var chapterMasteryPass = false;
            var autoClaimPass = false;
            var rewardPersistencePass = false;
            var rewardRuntimePass = false;
            var portablePreviewPass = false;
            var portableRoundTripPass = false;
            var tamperRejectedPass = false;
            var unknownRewardRejectedPass = false;
            var resetPass = false;
            var campaignCompletionPass = false;
            var uiTextFitPass = false;
            var clipboardPass = false;
            var doubleConfirmPass = false;
            var textOverflow = new List<string>();
            try
            {
                TDCampaignProgression.ResetProgress(totalLevels);
                var fixtureChapter = GetCampaignChapter(_campaignRoute?.level?.chapterId) ?? _campaign.chapters[0];
                for (var level = fixtureChapter.startLevel; level <= fixtureChapter.endLevel; level++)
                {
                    TDCampaignProgression.RecordResult(level, true, 3, 92, 20, totalLevels, true);
                }

                var fixtureProgress = TDCampaignProgression.BuildChapterSummary(fixtureChapter);
                chapterMasteryPass = fixtureProgress.cleared && fixtureProgress.mastered &&
                                     fixtureProgress.clearedLevels == fixtureProgress.totalLevels &&
                                     fixtureProgress.earnedStars == fixtureProgress.availableStars &&
                                     fixtureProgress.completedContracts == fixtureProgress.availableContracts;
                var victoryBeforeAutoClaim = _victory;
                _victory = true;
                var autoClaimedReward = TryAutoClaimCompletedChapterReward();
                _victory = victoryBeforeAutoClaim;
                var rewardClaimed = autoClaimedReward != null &&
                                    string.Equals(autoClaimedReward.rewardId, fixtureChapter.reward.rewardId, StringComparison.OrdinalIgnoreCase) &&
                                    TDCampaignProgression.IsChapterRewardClaimed(fixtureChapter.reward.rewardId);
                autoClaimPass = rewardClaimed;
                TDCampaignProgression.SaveFormation(
                    fixtureChapter.startLevel,
                    new[] { TDTower.GetTowerId(TDTowerKind.RailLancer) },
                    TDResonanceDoctrine.Adaptive);

                _startingDefenseBudget = DefaultDefenseBudget;
                _startingLineIntegrity = DefaultLineIntegrity;
                _missionResonanceGainMultiplier = 1f;
                _chapterRewardBudgetBonus = 0;
                _chapterRewardIntegrityBonus = 0;
                _chapterRewardResonanceMultiplier = 1f;
                ApplyClaimedChapterRewardEffects();
                rewardRuntimePass = rewardClaimed &&
                                    _chapterRewardBudgetBonus == fixtureChapter.reward.startingBudgetBonus &&
                                    _chapterRewardIntegrityBonus == fixtureChapter.reward.startingIntegrityBonus &&
                                    Mathf.Approximately(_chapterRewardResonanceMultiplier, ResolveMutatorMultiplier(fixtureChapter.reward.resonanceGainMultiplier)) &&
                                    _startingDefenseBudget == DefaultDefenseBudget + fixtureChapter.reward.startingBudgetBonus;
                _startingDefenseBudget = originalStartingBudget;
                _startingLineIntegrity = originalStartingIntegrity;
                _missionResonanceGainMultiplier = originalResonanceMultiplier;
                _chapterRewardBudgetBonus = originalRewardBudget;
                _chapterRewardIntegrityBonus = originalRewardIntegrity;
                _chapterRewardResonanceMultiplier = originalRewardResonance;

                var portableSave = TDCampaignProgression.ExportPortableSave(totalLevels);
                portablePreviewPass = portableSave.StartsWith(TDCampaignProgression.PortableSavePrefix, StringComparison.Ordinal) &&
                                      TDCampaignProgression.TryPreviewPortableSave(portableSave, totalLevels, out var preview, out _) &&
                                      preview.progress.clearedLevels == fixtureProgress.totalLevels &&
                                      preview.progress.earnedStars == fixtureProgress.availableStars &&
                                      preview.claimedChapterRewards == 1 &&
                                      preview.codeLength == portableSave.Length &&
                                      preview.fingerprint.Length == 8 &&
                                      ArePortableRewardIdsKnown(preview.claimedRewardIds);

                var tamperedSave = portableSave.Substring(0, portableSave.Length - 1) + "!";
                tamperRejectedPass = !TDCampaignProgression.TryPreviewPortableSave(tamperedSave, totalLevels, out _, out _);

                TDCampaignProgression.ClaimChapterReward("unknown_reward");
                var unknownSave = TDCampaignProgression.ExportPortableSave(totalLevels);
                unknownRewardRejectedPass = TDCampaignProgression.TryPreviewPortableSave(unknownSave, totalLevels, out var unknownPreview, out _) &&
                                            !ArePortableRewardIdsKnown(unknownPreview.claimedRewardIds);

                TDCampaignProgression.ResetProgress(totalLevels);
                resetPass = GetCampaignProgressSummary().clearedLevels == 0 &&
                            TDCampaignProgression.GetClaimedChapterRewardIds().Length == 0 &&
                            TDCampaignProgression.GetTowerLoadout(fixtureChapter.startLevel).Length == 0;
                portableRoundTripPass = TDCampaignProgression.TryImportPortableSave(portableSave, totalLevels, out var imported, out _) &&
                                        imported.progress.clearedLevels == fixtureProgress.totalLevels &&
                                        TDCampaignProgression.IsChapterRewardClaimed(fixtureChapter.reward.rewardId) &&
                                        TDCampaignProgression.GetTowerLoadout(fixtureChapter.startLevel).Length == 1;
                rewardPersistencePass = portableRoundTripPass &&
                                        TDCampaignProgression.BuildChapterSummary(fixtureChapter).rewardClaimed;

                for (var level = 1; level <= totalLevels; level++)
                {
                    TDCampaignProgression.RecordResult(level, true, 3, 95, 20, totalLevels, true);
                }

                for (var i = 0; i < _campaign.chapters.Length; i++)
                {
                    TDCampaignProgression.ClaimChapterReward(_campaign.chapters[i].reward.rewardId);
                }

                var completeSummary = GetCampaignProgressSummary();
                var masteredChapters = GetMasteredChapterCount();
                CalculateClaimedChapterRewardBonuses(out var fullBudget, out var fullIntegrity, out var fullResonance, out _);
                campaignCompletionPass = completeSummary.clearedLevels == totalLevels &&
                                         completeSummary.earnedStars == completeSummary.availableStars &&
                                         completeSummary.completedContracts == completeSummary.availableContracts &&
                                         masteredChapters == _campaign.chapters.Length &&
                                         TDCampaignProgression.GetClaimedChapterRewardIds().Length == _campaign.chapters.Length &&
                                         fullBudget == 20 && fullIntegrity == 2 && Mathf.Approximately(fullResonance, 1.05f) &&
                                         string.Equals(BuildCampaignRank(completeSummary, masteredChapters), "S", StringComparison.Ordinal);

                _missionBoardOpen = true;
                _formationPanelOpen = false;
                _campaignProfileOpen = true;
                _missionBoardSelectedLevel = totalLevels;
                RefreshMissionBoardUi();
                if (_uiCampaignProfileRoot != null)
                {
                    _uiCampaignProfileRoot.gameObject.SetActive(true);
                }

                RefreshCampaignProfileUi();
                Canvas.ForceUpdateCanvases();
                CopyCampaignSaveToClipboard();
                var copiedSave = GetCampaignClipboardText();
                clipboardPass = !string.IsNullOrWhiteSpace(copiedSave) &&
                                copiedSave.StartsWith(TDCampaignProgression.PortableSavePrefix, StringComparison.Ordinal);
                ImportCampaignSaveFromClipboard();
                var importArmedPass = _campaignProfileImportArmed &&
                                      string.Equals(_campaignProfilePendingImport, copiedSave, StringComparison.Ordinal) &&
                                      string.Equals(_uiCampaignProfileImportButtonText?.text, "Confirm Import", StringComparison.Ordinal);
                ResetCampaignProfileFromUi();
                var resetArmedPass = _campaignProfileResetArmed && !_campaignProfileImportArmed &&
                                     string.Equals(_uiCampaignProfileResetButtonText?.text, "Confirm Reset", StringComparison.Ordinal);
                doubleConfirmPass = importArmedPass && resetArmedPass;
                var criticalLabels = new List<Text>
                {
                    _uiMissionBoardProgressText,
                    _uiCampaignProfileTitleText,
                    _uiCampaignProfileSummaryText,
                    _uiCampaignProfileChapterText,
                    _uiCampaignProfileBonusText,
                    _uiCampaignProfileSaveText,
                    _uiCampaignProfileStatusText
                };
                criticalLabels.AddRange(_uiMissionChapterTitleTexts);
                criticalLabels.AddRange(_uiMissionChapterProgressTexts);
                criticalLabels.AddRange(_uiMissionChapterRewardButtonTexts);
                for (var i = 0; i < criticalLabels.Count; i++)
                {
                    var label = criticalLabels[i];
                    if (label != null && label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                    {
                        textOverflow.Add(label.name);
                    }
                }

                uiTextFitPass = textOverflow.Count == 0;
            }
            finally
            {
                TDCampaignProgression.ImportSnapshot(originalSnapshot, totalLevels);
                _missionBoardOpen = originalMissionBoardOpen;
                _formationPanelOpen = originalFormationOpen;
                _campaignProfileOpen = originalProfileOpen;
                _missionBoardSelectedLevel = originalSelection;
                _campaignProfileStatus = originalProfileStatus;
                _campaignProfileImportArmed = originalImportArmed;
                _campaignProfileResetArmed = originalResetArmed;
                _campaignProfilePendingImport = originalPendingImport;
                GUIUtility.systemCopyBuffer = originalClipboard;
                _campaignClipboardBuffer = originalClipboardBuffer;
                _startingDefenseBudget = originalStartingBudget;
                _startingLineIntegrity = originalStartingIntegrity;
                _missionResonanceGainMultiplier = originalResonanceMultiplier;
                _chapterRewardBudgetBonus = originalRewardBudget;
                _chapterRewardIntegrityBonus = originalRewardIntegrity;
                _chapterRewardResonanceMultiplier = originalRewardResonance;
                if (_uiFormationRoot != null)
                {
                    _uiFormationRoot.gameObject.SetActive(_missionBoardOpen && _formationPanelOpen);
                }

                if (_uiCampaignProfileRoot != null)
                {
                    _uiCampaignProfileRoot.gameObject.SetActive(_missionBoardOpen && _campaignProfileOpen);
                }

                _missionBoardNeedsRefresh = true;
            }

            var pass = chapterMasteryPass && autoClaimPass && rewardPersistencePass && rewardRuntimePass &&
                       portablePreviewPass && portableRoundTripPass && tamperRejectedPass &&
                       unknownRewardRejectedPass && resetPass && campaignCompletionPass && uiTextFitPass &&
                       clipboardPass && doubleConfirmPass;
            return
                $"p8.4.audit.chapterMastery={chapterMasteryPass}\n" +
                $"p8.4.audit.autoClaim={autoClaimPass}\n" +
                $"p8.4.audit.rewardPersistence={rewardPersistencePass}\n" +
                $"p8.4.audit.rewardRuntime={rewardRuntimePass}\n" +
                $"p8.4.audit.portablePreview={portablePreviewPass}\n" +
                $"p8.4.audit.portableRoundTrip={portableRoundTripPass}\n" +
                $"p8.4.audit.tamperRejected={tamperRejectedPass}\n" +
                $"p8.4.audit.unknownRewardRejected={unknownRewardRejectedPass}\n" +
                $"p8.4.audit.reset={resetPass}\n" +
                $"p8.4.audit.campaignCompletion={campaignCompletionPass}\n" +
                $"p8.4.audit.clipboard={clipboardPass}\n" +
                $"p8.4.audit.doubleConfirm={doubleConfirmPass}\n" +
                $"p8.4.audit.allTextFit={uiTextFitPass}\n" +
                $"p8.4.audit.textOverflow={(uiTextFitPass ? "none" : string.Join(",", textOverflow))}\n" +
                $"p8.4.audit.pass={pass}\n";
        }

        public string DebugAuditP85DifficultyForTest()
        {
            if (_campaign?.levels == null || _campaign.difficultyTiers == null || _campaignRoute?.level == null)
            {
                return "p8.5.audit.pass=False\np8.5.audit.error=campaign unavailable\n";
            }

            var totalLevels = _campaign.totalLevels;
            var currentLevel = _campaignRoute.level;
            var originalSnapshot = TDCampaignProgression.ExportSnapshot(totalLevels);
            var originalDifficulty = _activeCampaignDifficulty;
            var originalDraftDifficulty = _formationDraftDifficulty;
            var originalDraftLevel = _formationDraftLevel;
            var originalDraftDoctrine = _formationDraftDoctrine;
            var originalDraftTowers = new List<TDTowerKind>(_formationDraftTowerKinds);
            var originalMissionBoardOpen = _missionBoardOpen;
            var originalFormationOpen = _formationPanelOpen;
            var originalProfileOpen = _campaignProfileOpen;
            var originalSelection = _missionBoardSelectedLevel;
            var originalStartingBudget = _startingDefenseBudget;
            var originalStartingIntegrity = _startingLineIntegrity;
            var originalBudget = _defenseBudget;
            var originalIntegrity = _lineIntegrity;
            var originalHp = _missionEnemyHpMultiplier;
            var originalSpeed = _missionEnemySpeedMultiplier;
            var originalArmor = _missionEnemyArmorBonus;
            var originalReward = _missionRewardMultiplier;
            var originalResonance = _missionResonanceGainMultiplier;
            var originalRewardBudget = _chapterRewardBudgetBonus;
            var originalRewardIntegrity = _chapterRewardIntegrityBonus;
            var originalRewardResonance = _chapterRewardResonanceMultiplier;
            var originalContractCompleted = _currentMissionContractCompleted;
            var originalNewReward = _newlyClaimedChapterReward;
            var originalFeedbackInitialized = _contractFeedbackInitialized;
            var originalFeedbackTargetMet = _contractFeedbackTargetMet;
            var originalNextFeedback = _nextContractFeedbackTime;
            var contentPass = false;
            var initialLocksPass = false;
            var veteranUnlockPass = false;
            var emberUnlockPass = false;
            var standardRuntimePass = false;
            var veteranRuntimePass = false;
            var emberRuntimePass = false;
            var preferencePass = false;
            var recordMonotonicPass = false;
            var portableRoundTripPass = false;
            var uiPass = false;
            var fullChallengePass = false;
            var standardSignature = string.Empty;
            var veteranSignature = string.Empty;
            var emberSignature = string.Empty;
            var textOverflow = new List<string>();
            try
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                contentPass = _campaign.difficultyTiers.Length == 3;
                for (var i = 0; i < _campaign.difficultyTiers.Length; i++)
                {
                    var definition = _campaign.difficultyTiers[i];
                    contentPass &= definition != null && definition.tier == i &&
                                   !string.IsNullOrWhiteSpace(definition.difficultyId) &&
                                   ids.Add(definition.difficultyId) &&
                                   (i == 0 || !string.Equals(
                                       BuildCompactMutatorEffectLabel(definition.modifiers),
                                       "BASE RULES",
                                       StringComparison.Ordinal));
                }

                var chapters = _campaign.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
                for (var i = 0; i < chapters.Length; i++)
                {
                    contentPass &= chapters[i]?.challengeRemix != null &&
                                   !string.Equals(
                                       BuildCompactMutatorEffectLabel(chapters[i].challengeRemix),
                                       "BASE RULES",
                                       StringComparison.Ordinal);
                }

                TDCampaignProgression.ResetProgress(totalLevels);
                _activeCampaignDifficulty = TDCampaignDifficultyTier.Standard;
                ResetMissionRuntimeRules();
                ApplyMissionRuntimeRules(currentLevel);
                standardRuntimePass = DoesCurrentDifficultyRuntimeMatch();
                standardSignature = BuildCurrentDifficultyRuntimeSignature();
                initialLocksPass = !IsDifficultyAvailableForLevel(currentLevel.levelIndex, TDCampaignDifficultyTier.Veteran) &&
                                   !IsDifficultyAvailableForLevel(currentLevel.levelIndex, TDCampaignDifficultyTier.EmberTrial);

                var currentChapter = GetCampaignChapter(currentLevel.chapterId);
                for (var level = currentChapter.startLevel; level <= currentChapter.endLevel; level++)
                {
                    TDCampaignProgression.RecordResult(level, true, 2, 82, 16, totalLevels);
                }

                veteranUnlockPass = IsDifficultyAvailableForLevel(currentLevel.levelIndex, TDCampaignDifficultyTier.Veteran) &&
                                    !IsDifficultyAvailableForLevel(currentLevel.levelIndex, TDCampaignDifficultyTier.EmberTrial);
                TDCampaignProgression.SaveDifficultyPreference(currentLevel.levelIndex, TDCampaignDifficultyTier.Veteran);
                _activeCampaignDifficulty = TDCampaignDifficultyTier.Veteran;
                ResetMissionRuntimeRules();
                ApplyMissionRuntimeRules(currentLevel);
                veteranRuntimePass = DoesCurrentDifficultyRuntimeMatch();
                veteranSignature = BuildCurrentDifficultyRuntimeSignature();
                TDCampaignProgression.RecordResult(
                    currentLevel.levelIndex,
                    true,
                    3,
                    90,
                    18,
                    totalLevels,
                    true,
                    TDCampaignDifficultyTier.Veteran);

                for (var level = 1; level <= totalLevels; level++)
                {
                    TDCampaignProgression.RecordResult(level, true, 2, 84, 16, totalLevels);
                }

                emberUnlockPass = IsDifficultyAvailableForLevel(currentLevel.levelIndex, TDCampaignDifficultyTier.EmberTrial);
                TDCampaignProgression.SaveDifficultyPreference(currentLevel.levelIndex, TDCampaignDifficultyTier.EmberTrial);
                _activeCampaignDifficulty = TDCampaignDifficultyTier.EmberTrial;
                ResetMissionRuntimeRules();
                ApplyMissionRuntimeRules(currentLevel);
                emberRuntimePass = DoesCurrentDifficultyRuntimeMatch();
                emberSignature = BuildCurrentDifficultyRuntimeSignature();
                TDCampaignProgression.RecordResult(
                    currentLevel.levelIndex,
                    true,
                    3,
                    94,
                    18,
                    totalLevels,
                    true,
                    TDCampaignDifficultyTier.EmberTrial);
                TDCampaignProgression.RecordResult(
                    currentLevel.levelIndex,
                    true,
                    1,
                    60,
                    8,
                    totalLevels,
                    false,
                    TDCampaignDifficultyTier.Standard);
                var challengeProgress = TDCampaignProgression.GetLevelProgress(currentLevel.levelIndex);
                preferencePass = TDCampaignProgression.GetDifficultyPreference(currentLevel.levelIndex) == TDCampaignDifficultyTier.EmberTrial;
                recordMonotonicPass = challengeProgress.highestDifficultyCleared == (int)TDCampaignDifficultyTier.EmberTrial;

                var portableSave = TDCampaignProgression.ExportPortableSave(totalLevels);
                var portablePreviewPass = TDCampaignProgression.TryPreviewPortableSave(
                    portableSave,
                    totalLevels,
                    out var preview,
                    out _) &&
                                          preview.progress.veteranClears == 1 &&
                                          preview.progress.emberTrialClears == 1;
                TDCampaignProgression.ResetProgress(totalLevels);
                portableRoundTripPass = portablePreviewPass &&
                                        TDCampaignProgression.TryImportPortableSave(portableSave, totalLevels, out _, out _) &&
                                        TDCampaignProgression.GetDifficultyPreference(currentLevel.levelIndex) == TDCampaignDifficultyTier.EmberTrial &&
                                        TDCampaignProgression.GetLevelProgress(currentLevel.levelIndex).highestDifficultyCleared ==
                                        (int)TDCampaignDifficultyTier.EmberTrial;

                _missionBoardOpen = true;
                _campaignProfileOpen = false;
                _missionBoardSelectedLevel = currentLevel.levelIndex;
                OpenFormationPanel();
                _formationDraftDifficulty = TDCampaignDifficultyTier.EmberTrial;
                RefreshFormationPanelUi();
                Canvas.ForceUpdateCanvases();
                var labels = new List<Text>
                {
                    _uiFormationTitleText,
                    _uiFormationDifficultyText
                };
                labels.AddRange(_uiFormationDifficultyButtonTexts);
                for (var i = 0; i < labels.Count; i++)
                {
                    var label = labels[i];
                    if (label != null && label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                    {
                        textOverflow.Add(label.name);
                    }
                }

                _formationPanelOpen = false;
                _campaignProfileOpen = true;
                _missionBoardNeedsRefresh = true;
                RefreshMissionBoardUi();
                RefreshCampaignProfileUi();
                Canvas.ForceUpdateCanvases();
                var archiveLabels = new List<Text>
                {
                    _uiMissionBoardProgressText,
                    _uiCampaignProfileSummaryText,
                    _uiCampaignProfileChapterText
                };
                archiveLabels.AddRange(_uiMissionChapterProgressTexts);
                for (var i = 0; i < archiveLabels.Count; i++)
                {
                    var label = archiveLabels[i];
                    if (label != null && label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                    {
                        textOverflow.Add(label.name);
                    }
                }

                uiPass = _uiFormationDifficultyButtons.Count == 3 &&
                         _uiFormationDifficultyText != null &&
                         _uiFormationDifficultyText.text.Contains("EMBER TRIAL") &&
                         _uiFormationDifficultyText.text.Contains("REMIX") &&
                         _uiCampaignProfileSummaryText.text.Contains("CHALLENGE RECORD") &&
                         textOverflow.Count == 0;

                for (var level = 1; level <= totalLevels; level++)
                {
                    TDCampaignProgression.RecordResult(
                        level,
                        true,
                        3,
                        96,
                        20,
                        totalLevels,
                        true,
                        TDCampaignDifficultyTier.EmberTrial);
                }

                var fullSummary = GetCampaignProgressSummary();
                fullChallengePass = fullSummary.veteranClears == totalLevels &&
                                    fullSummary.emberTrialClears == totalLevels &&
                                    BuildCampaignChapterArchiveLabel().Contains("Challenge V");
            }
            finally
            {
                TDCampaignProgression.ImportSnapshot(originalSnapshot, totalLevels);
                _activeCampaignDifficulty = originalDifficulty;
                _formationDraftDifficulty = originalDraftDifficulty;
                _formationDraftLevel = originalDraftLevel;
                _formationDraftDoctrine = originalDraftDoctrine;
                _formationDraftTowerKinds.Clear();
                _formationDraftTowerKinds.AddRange(originalDraftTowers);
                _missionBoardOpen = originalMissionBoardOpen;
                _formationPanelOpen = originalFormationOpen;
                _campaignProfileOpen = originalProfileOpen;
                _missionBoardSelectedLevel = originalSelection;
                _startingDefenseBudget = originalStartingBudget;
                _startingLineIntegrity = originalStartingIntegrity;
                _defenseBudget = originalBudget;
                _lineIntegrity = originalIntegrity;
                _missionEnemyHpMultiplier = originalHp;
                _missionEnemySpeedMultiplier = originalSpeed;
                _missionEnemyArmorBonus = originalArmor;
                _missionRewardMultiplier = originalReward;
                _missionResonanceGainMultiplier = originalResonance;
                _chapterRewardBudgetBonus = originalRewardBudget;
                _chapterRewardIntegrityBonus = originalRewardIntegrity;
                _chapterRewardResonanceMultiplier = originalRewardResonance;
                _currentMissionContractCompleted = originalContractCompleted;
                _newlyClaimedChapterReward = originalNewReward;
                _contractFeedbackInitialized = originalFeedbackInitialized;
                _contractFeedbackTargetMet = originalFeedbackTargetMet;
                _nextContractFeedbackTime = originalNextFeedback;
                RefreshUnlockedTowerKinds();
                RebuildTowerBuildButtons();
                if (_uiFormationRoot != null)
                {
                    _uiFormationRoot.gameObject.SetActive(_missionBoardOpen && _formationPanelOpen);
                }

                if (_uiCampaignProfileRoot != null)
                {
                    _uiCampaignProfileRoot.gameObject.SetActive(_missionBoardOpen && _campaignProfileOpen);
                }

                _missionBoardNeedsRefresh = true;
            }

            var runtimeProgressionPass = standardRuntimePass && veteranRuntimePass && emberRuntimePass &&
                                         !string.Equals(standardSignature, veteranSignature, StringComparison.Ordinal) &&
                                         !string.Equals(veteranSignature, emberSignature, StringComparison.Ordinal);
            var pass = contentPass && initialLocksPass && veteranUnlockPass && emberUnlockPass &&
                       runtimeProgressionPass && preferencePass && recordMonotonicPass &&
                       portableRoundTripPass && uiPass && fullChallengePass;
            return
                $"p8.5.audit.content={contentPass}\n" +
                $"p8.5.audit.initialLocks={initialLocksPass}\n" +
                $"p8.5.audit.veteranUnlock={veteranUnlockPass}\n" +
                $"p8.5.audit.emberUnlock={emberUnlockPass}\n" +
                $"p8.5.audit.standardRuntime={standardRuntimePass} [{standardSignature}]\n" +
                $"p8.5.audit.veteranRuntime={veteranRuntimePass} [{veteranSignature}]\n" +
                $"p8.5.audit.emberRuntime={emberRuntimePass} [{emberSignature}]\n" +
                $"p8.5.audit.preference={preferencePass}\n" +
                $"p8.5.audit.recordMonotonic={recordMonotonicPass}\n" +
                $"p8.5.audit.portableRoundTrip={portableRoundTripPass}\n" +
                $"p8.5.audit.ui={uiPass}\n" +
                $"p8.5.audit.textOverflow={(textOverflow.Count == 0 ? "none" : string.Join(",", textOverflow))}\n" +
                $"p8.5.audit.fullChallenge={fullChallengePass}\n" +
                $"p8.5.audit.pass={pass}\n";
        }

        public string DebugAuditCampaignProgressionForTest()
        {
            var totalLevels = Mathf.Max(3, _campaign?.totalLevels ?? 20);
            var snapshot = TDCampaignProgression.ExportSnapshot(totalLevels);
            string report;
            try
            {
                TDCampaignProgression.ResetProgress(totalLevels);
                var initial = TDCampaignProgression.BuildSummary(totalLevels);
                var initialSecondLevelLocked = !TDCampaignProgression.IsLevelUnlocked(2, totalLevels);
                TDCampaignProgression.RecordResult(1, false, 0, 48, 0, totalLevels, true);
                var afterDefeat = TDCampaignProgression.BuildSummary(totalLevels);
                var firstClear = TDCampaignProgression.RecordResult(1, true, 2, 72, 14, totalLevels, true);
                TDCampaignProgression.RecordResult(1, true, 1, 55, 8, totalLevels, false);
                var levelOne = TDCampaignProgression.GetLevelProgress(1);
                var secondClear = TDCampaignProgression.RecordResult(2, true, 3, 91, 20, totalLevels);
                var final = TDCampaignProgression.BuildSummary(totalLevels);

                var initialLockPass = initial.highestUnlockedLevel == 1 &&
                                      TDCampaignProgression.IsLevelUnlocked(1, totalLevels) &&
                                      initialSecondLevelLocked;
                var defeatLockPass = afterDefeat.highestUnlockedLevel == 1 &&
                                     afterDefeat.clearedLevels == 0 &&
                                     afterDefeat.completedContracts == 0;
                var firstClearPass = firstClear.firstClear && firstClear.nextLevelUnlocked && firstClear.bestStars == 2;
                var replayMonotonicPass = levelOne.bestStars == 2 &&
                                          levelOne.bestTacticalScore == 72 &&
                                          levelOne.attempts == 3 &&
                                          levelOne.contractCompleted;
                var contractPersistencePass = firstClear.firstContractCompletion &&
                                              firstClear.contractCompleted &&
                                              final.completedContracts == 1;
                var secondClearPass = secondClear.firstClear && secondClear.nextLevelUnlocked &&
                                      final.highestUnlockedLevel == 3 && final.clearedLevels == 2 && final.earnedStars == 5;
                var pass = initialLockPass && defeatLockPass && firstClearPass && replayMonotonicPass &&
                           contractPersistencePass && secondClearPass;
                report =
                    $"p8.audit.initialLock={initialLockPass}\n" +
                    $"p8.audit.defeatKeepsLock={defeatLockPass}\n" +
                    $"p8.audit.firstClearUnlocks={firstClearPass}\n" +
                    $"p8.audit.bestIsMonotonic={replayMonotonicPass}\n" +
                    $"p8.2.audit.contractPersists={contractPersistencePass}\n" +
                    $"p8.audit.secondClearUnlocks={secondClearPass}\n" +
                    $"p8.audit.fixture=cleared:{final.clearedLevels},stars:{final.earnedStars},contracts:{final.completedContracts},frontier:{final.highestUnlockedLevel}\n" +
                    $"p8.audit.pass={pass}\n";
            }
            finally
            {
                TDCampaignProgression.ImportSnapshot(snapshot, totalLevels);
                _missionBoardNeedsRefresh = true;
            }

            return report;
        }

        public string DebugAuditCampaignContentForTest()
        {
            if (_campaign?.levels == null || _campaign.levels.Length == 0)
            {
                return "p8.content.valid=0/0\np8.audit.allMissionIntel=False\np8.audit.allMissionTextFit=False\n";
            }

            var originalSelection = _missionBoardSelectedLevel;
            var validIntel = 0;
            var bossMissions = 0;
            var validContracts = 0;
            var validMutators = 0;
            var textOverflow = new List<string>();
            var criticalLabels = new[]
            {
                _uiMissionIntelTitleText,
                _uiMissionIntelBriefText,
                _uiMissionIntelThreatText,
                _uiMissionIntelContractText,
                _uiMissionIntelCounterText,
                _uiMissionIntelRecordText
            };

            for (var i = 0; i < _campaign.levels.Length; i++)
            {
                var level = _campaign.levels[i];
                if (level == null)
                {
                    continue;
                }

                if (level.bossLevel)
                {
                    bossMissions++;
                }

                if (level.contract != null && !string.IsNullOrWhiteSpace(level.contract.contractId))
                {
                    validContracts++;
                }

                if (level.mutators != null && level.mutators.Length > 0)
                {
                    validMutators++;
                }

                BuildMissionWaveIntel(level, out var waves, out var lanes, out var composition, out var tags, out var error);
                if (string.IsNullOrWhiteSpace(error) && waves > 0 && lanes > 0 && tags.Count > 0 &&
                    !string.IsNullOrWhiteSpace(composition) && !string.IsNullOrWhiteSpace(BuildMissionCounterPlan(level.levelIndex, tags)))
                {
                    validIntel++;
                }

                _missionBoardSelectedLevel = level.levelIndex;
                RefreshMissionBoardUi();
                Canvas.ForceUpdateCanvases();
                for (var labelIndex = 0; labelIndex < criticalLabels.Length; labelIndex++)
                {
                    var label = criticalLabels[labelIndex];
                    if (label != null && label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                    {
                        textOverflow.Add($"L{level.levelIndex:00}:{label.name}");
                    }
                }
            }

            _missionBoardSelectedLevel = originalSelection;
            RefreshMissionBoardUi();
            Canvas.ForceUpdateCanvases();
            var allIntelValid = validIntel == _campaign.levels.Length;
            var allTextFits = textOverflow.Count == 0;
            return
                $"p8.content.valid={validIntel}/{_campaign.levels.Length}\n" +
                $"p8.content.bossMissions={bossMissions}\n" +
                $"p8.2.content.contracts={validContracts}/{_campaign.levels.Length}\n" +
                $"p8.2.content.mutators={validMutators}/{_campaign.levels.Length}\n" +
                $"p8.audit.allMissionIntel={allIntelValid}\n" +
                $"p8.audit.allMissionTextFit={allTextFits}\n" +
                $"p8.audit.missionTextOverflow={(allTextFits ? "none" : string.Join(",", textOverflow))}\n";
        }

        public string DebugAuditP82MissionRulesForTest()
        {
            var levels = _campaign?.levels ?? Array.Empty<TDCampaignLevelDefinition>();
            var contractIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mutatorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validRules = 0;
            for (var i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                var levelContract = level?.contract;
                var mutators = level?.mutators ?? Array.Empty<TDCampaignMutatorDefinition>();
                var levelValid = levelContract != null &&
                                 !string.IsNullOrWhiteSpace(levelContract.contractId) &&
                                 contractIds.Add(levelContract.contractId) &&
                                 mutators.Length > 0;
                for (var mutatorIndex = 0; mutatorIndex < mutators.Length; mutatorIndex++)
                {
                    var mutator = mutators[mutatorIndex];
                    levelValid &= mutator != null &&
                                  !string.IsNullOrWhiteSpace(mutator.mutatorId) &&
                                  mutatorIds.Add(mutator.mutatorId) &&
                                  !string.Equals(BuildMutatorEffectLabel(mutator), "No effect", StringComparison.Ordinal);
                }

                if (levelValid)
                {
                    validRules++;
                }
            }

            var currentLevel = _campaignRoute?.level;
            var expectedBudget = DefaultDefenseBudget + GetCampaignStartingBudgetRamp(currentLevel?.levelIndex ?? 1);
            var expectedIntegrity = DefaultLineIntegrity + GetCampaignStartingIntegrityRamp(currentLevel?.levelIndex ?? 1);
            var expectedHpMultiplier = 1f;
            var expectedSpeedMultiplier = 1f;
            var expectedArmorBonus = 0;
            var expectedRewardMultiplier = 1f;
            var expectedResonanceMultiplier = 1f;
            var expectedScenarioCostMultiplier = 1f;
            var currentMutators = currentLevel?.mutators ?? Array.Empty<TDCampaignMutatorDefinition>();
            for (var i = 0; i < currentMutators.Length; i++)
            {
                AccumulateExpectedRuntimeMutator(
                    currentMutators[i],
                    ref expectedBudget,
                    ref expectedIntegrity,
                    ref expectedHpMultiplier,
                    ref expectedSpeedMultiplier,
                    ref expectedArmorBonus,
                    ref expectedRewardMultiplier,
                    ref expectedResonanceMultiplier,
                    ref expectedScenarioCostMultiplier);
            }

            if (_activeCampaignDifficulty != TDCampaignDifficultyTier.Standard)
            {
                AccumulateExpectedRuntimeMutator(
                    GetDifficultyDefinition(_activeCampaignDifficulty)?.modifiers,
                    ref expectedBudget,
                    ref expectedIntegrity,
                    ref expectedHpMultiplier,
                    ref expectedSpeedMultiplier,
                    ref expectedArmorBonus,
                    ref expectedRewardMultiplier,
                    ref expectedResonanceMultiplier,
                    ref expectedScenarioCostMultiplier);
                AccumulateExpectedRuntimeMutator(
                    GetCampaignChapter(currentLevel?.chapterId)?.challengeRemix,
                    ref expectedBudget,
                    ref expectedIntegrity,
                    ref expectedHpMultiplier,
                    ref expectedSpeedMultiplier,
                    ref expectedArmorBonus,
                    ref expectedRewardMultiplier,
                    ref expectedResonanceMultiplier,
                    ref expectedScenarioCostMultiplier);
            }

            CalculateClaimedChapterRewardBonuses(
                out var expectedRewardBudget,
                out var expectedRewardIntegrity,
                out var expectedRewardResonance,
                out _);
            if (_newlyClaimedChapterReward != null &&
                TDCampaignProgression.IsChapterRewardClaimed(_newlyClaimedChapterReward.rewardId))
            {
                expectedRewardBudget -= Mathf.Max(0, _newlyClaimedChapterReward.startingBudgetBonus);
                expectedRewardIntegrity -= Mathf.Max(0, _newlyClaimedChapterReward.startingIntegrityBonus);
                expectedRewardResonance /= ResolveMutatorMultiplier(_newlyClaimedChapterReward.resonanceGainMultiplier);
            }
            expectedBudget += expectedRewardBudget;
            expectedIntegrity += expectedRewardIntegrity;
            expectedResonanceMultiplier *= expectedRewardResonance;

            expectedBudget = Mathf.Max(0, expectedBudget);
            expectedIntegrity = Mathf.Max(1, expectedIntegrity);
            var runtimeMatches = expectedBudget == _startingDefenseBudget &&
                                 expectedIntegrity == _startingLineIntegrity &&
                                 Mathf.Approximately(expectedHpMultiplier, _missionEnemyHpMultiplier) &&
                                 Mathf.Approximately(expectedSpeedMultiplier, _missionEnemySpeedMultiplier) &&
                                  expectedArmorBonus == _missionEnemyArmorBonus &&
                                  Mathf.Approximately(expectedRewardMultiplier, _missionRewardMultiplier) &&
                                  Mathf.Approximately(expectedResonanceMultiplier, _missionResonanceGainMultiplier) &&
                                  Mathf.Approximately(expectedScenarioCostMultiplier, _scenarioCostMultiplier);

            TDEnemyCatalogEntry sourceEntry = null;
            foreach (var pair in _enemyCatalog)
            {
                sourceEntry = pair.Value;
                break;
            }

            var enemyClonePass = false;
            if (sourceEntry != null)
            {
                var sourceHp = sourceEntry.hp;
                var sourceSpeed = sourceEntry.speed;
                var sourceArmor = sourceEntry.armorFlat;
                var sourceReward = sourceEntry.rewardGold;
                var runtimeEntry = BuildMissionEnemyEntry(sourceEntry);
                enemyClonePass = !ReferenceEquals(runtimeEntry, sourceEntry) &&
                                 sourceEntry.hp == sourceHp &&
                                 Mathf.Approximately(sourceEntry.speed, sourceSpeed) &&
                                 sourceEntry.armorFlat == sourceArmor &&
                                 sourceEntry.rewardGold == sourceReward &&
                                 runtimeEntry.hp == Mathf.Max(1, Mathf.RoundToInt(sourceHp * _missionEnemyHpMultiplier)) &&
                                 Mathf.Approximately(runtimeEntry.speed, sourceSpeed * _missionEnemySpeedMultiplier) &&
                                 runtimeEntry.armorFlat == Mathf.Max(0, sourceArmor + _missionEnemyArmorBonus) &&
                                 runtimeEntry.rewardGold == ScaleMissionReward(sourceReward);
            }

            var contract = currentLevel?.contract;
            var contractBoundaryPass = contract != null &&
                                       IsContractTargetMet(contract, contract.target) &&
                                       !IsContractTargetMet(
                                           contract,
                                           string.Equals(contract.comparison, "at_most", StringComparison.OrdinalIgnoreCase)
                                               ? contract.target + 1
                                               : contract.target - 1);
            var rewardScalingPass = ScaleMissionReward(100) == Mathf.RoundToInt(100f * _missionRewardMultiplier);
            var pass = levels.Length == 20 && validRules == levels.Length && runtimeMatches &&
                       enemyClonePass && contractBoundaryPass && rewardScalingPass;
            return
                $"p8.2.audit.rules={validRules}/{levels.Length}\n" +
                $"p8.2.audit.uniqueContracts={contractIds.Count == levels.Length}\n" +
                $"p8.2.audit.uniqueMutators={mutatorIds.Count == levels.Length}\n" +
                $"p8.2.audit.runtimeMatches={runtimeMatches}\n" +
                $"p8.2.audit.enemyCloneIsolation={enemyClonePass}\n" +
                $"p8.2.audit.contractBoundary={contractBoundaryPass}\n" +
                $"p8.2.audit.rewardScaling={rewardScalingPass}\n" +
                $"p8.2.audit.pass={pass}\n";
        }

        private static void AccumulateExpectedRuntimeMutator(
            TDCampaignMutatorDefinition mutator,
            ref int budget,
            ref int integrity,
            ref float hpMultiplier,
            ref float speedMultiplier,
            ref int armorBonus,
            ref float rewardMultiplier,
            ref float resonanceMultiplier,
            ref float scenarioCostMultiplier)
        {
            if (mutator == null)
            {
                return;
            }

            budget += mutator.startingBudgetDelta;
            integrity += mutator.startingIntegrityDelta;
            hpMultiplier *= ResolveMutatorMultiplier(mutator.enemyHpMultiplier);
            speedMultiplier *= ResolveMutatorMultiplier(mutator.enemySpeedMultiplier);
            armorBonus += mutator.enemyArmorBonus;
            rewardMultiplier *= ResolveMutatorMultiplier(mutator.rewardMultiplier);
            resonanceMultiplier *= ResolveMutatorMultiplier(mutator.resonanceGainMultiplier);
            scenarioCostMultiplier *= ResolveMutatorMultiplier(mutator.scenarioCostMultiplier);
        }

        public string DebugPrepareP9PresentationForTest()
        {
            if (_battlePresentation == null || !_battlePresentation.IsInitialized)
            {
                return "p9.fixture.ready=False";
            }

            _missionBoardOpen = false;
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _campaignDeploymentConfirmed = true;
            _tutorialVisible = true;
            _tutorialStep = TDFirstRunTutorialStep.ReadArmor;
            _colorblindMarkersEnabled = true;
            _largeTextEnabled = true;
            ApplyLargeTextMode();
            _battlePresentation.SetAccessibilityState(true, true);
            RefreshTutorialUi();
            SetBattlePlaybackSpeed(1f, false);

            var kinds = new[]
            {
                TDBattleFeedbackKind.Hit,
                TDBattleFeedbackKind.ArmorBreak,
                TDBattleFeedbackKind.Slow,
                TDBattleFeedbackKind.Specialization,
                TDBattleFeedbackKind.Resonance,
                TDBattleFeedbackKind.Leak
            };
            var details = new[] { "24", "-6", "35%", "MATRIX", "MATCH", "-2" };
            for (var i = 0; i < kinds.Length; i++)
            {
                var tier = i == kinds.Length - 1
                    ? TDBattleFeedbackTier.Critical
                    : i >= 1 ? TDBattleFeedbackTier.Tactical : TDBattleFeedbackTier.Routine;
                _battlePresentation.EmitFeedback(kinds[i], new Vector3(-3.4f + (i * 1.35f), 0.35f, 0f), details[i], tier);
            }

            _battlePresentation.ShowCinematic(
                "[B!]",
                "BOSS PHASE 2",
                "Overdrive active  /  reinforcement inbound",
                TDBattleFeedbackTier.Critical,
                1.5f);
            Canvas.ForceUpdateCanvases();
            return "p9.fixture.ready=True\n" + _battlePresentation.BuildAuditReport();
        }

        public string DebugGetP9PresentationReport()
        {
            var tutorialComplete = PlayerPrefs.GetInt(GetTutorialCompleteKey(), 0) > 0;
            var skinReport = TDUiWorldSkin.BuildAuditReport(_battleCanvas?.gameObject, out _);
            return (_battlePresentation?.BuildAuditReport() ?? "p9.presentation.initialized=False") + "\n" +
                   $"p9.playback.speed={_lastActivePlaybackSpeed:0}\n" +
                   $"p9.playback.paused={_playbackPaused}\n" +
                   $"p9.tutorial.step={_tutorialStep}\n" +
                   $"p9.tutorial.visible={_tutorialVisible}\n" +
                   $"p9.tutorial.persisted={tutorialComplete}\n" +
                   skinReport;
        }

        public string DebugAuditP9ForTest()
        {
            if (_battlePresentation == null || !_battlePresentation.IsInitialized || _battleCanvas == null)
            {
                return "p9.audit.presentation=False\np9.audit.pass=False\n";
            }

            var tutorialStepKey = GetTutorialStepKey();
            var tutorialCompleteKey = GetTutorialCompleteKey();
            var hadTutorialStep = PlayerPrefs.HasKey(tutorialStepKey);
            var hadTutorialComplete = PlayerPrefs.HasKey(tutorialCompleteKey);
            var originalTutorialStepPref = PlayerPrefs.GetInt(tutorialStepKey, 0);
            var originalTutorialCompletePref = PlayerPrefs.GetInt(tutorialCompleteKey, 0);
            var originalTimeScale = Time.timeScale;
            var originalSpeed = _playbackSpeed;
            var originalLastSpeed = _lastActivePlaybackSpeed;
            var originalPaused = _playbackPaused;
            var originalMarkers = _colorblindMarkersEnabled;
            var originalLargeText = _largeTextEnabled;
            var originalTutorialStep = _tutorialStep;
            var originalTutorialVisible = _tutorialVisible;
            var originalMissionBoardOpen = _missionBoardOpen;
            var originalFormationOpen = _formationPanelOpen;
            var originalProfileOpen = _campaignProfileOpen;
            var originalDeploymentConfirmed = _campaignDeploymentConfirmed;
            var playbackPass = false;
            var feedbackPass = false;
            var cinematicPass = false;
            var accessibilityPass = false;
            var tutorialPass = false;
            var tutorialSkipPass = false;
            var uiPass = false;
            var textFitPass = false;
            var overflow = new List<string>();

            try
            {
                _missionBoardOpen = false;
                _formationPanelOpen = false;
                _campaignProfileOpen = false;
                _campaignDeploymentConfirmed = true;

                SetBattlePlaybackSpeed(0f, false);
                var pausedPass = _playbackPaused && Mathf.Approximately(Time.timeScale, 0f);
                SetBattlePlaybackSpeed(1f, false);
                var onePass = !_playbackPaused && Mathf.Approximately(Time.timeScale, 1f);
                SetBattlePlaybackSpeed(2f, false);
                var twoPass = Mathf.Approximately(Time.timeScale, 2f);
                SetBattlePlaybackSpeed(3f, false);
                var threePass = Mathf.Approximately(Time.timeScale, 3f);
                playbackPass = pausedPass && onePass && twoPass && threePass;

                _colorblindMarkersEnabled = true;
                _largeTextEnabled = true;
                ApplyLargeTextMode();
                _battlePresentation.SetAccessibilityState(true, true);
                accessibilityPass = _battlePresentation.MarkersEnabled && _battlePresentation.LargeTextEnabled;

                var kinds = new[]
                {
                    TDBattleFeedbackKind.Hit,
                    TDBattleFeedbackKind.ArmorBreak,
                    TDBattleFeedbackKind.Slow,
                    TDBattleFeedbackKind.Specialization,
                    TDBattleFeedbackKind.Resonance,
                    TDBattleFeedbackKind.Leak
                };
                for (var i = 0; i < kinds.Length; i++)
                {
                    _battlePresentation.EmitFeedback(
                        kinds[i],
                        new Vector3(-3f + (i * 1.2f), 0f, 0f),
                        i.ToString(),
                        i == kinds.Length - 1 ? TDBattleFeedbackTier.Critical : TDBattleFeedbackTier.Tactical);
                }

                var feedbackReport = _battlePresentation.BuildAuditReport();
                feedbackPass = _battlePresentation.ActiveSignalCount >= 6 &&
                               !feedbackReport.Contains("p9.presentation.feedback.hit=0") &&
                               !feedbackReport.Contains("p9.presentation.feedback.break=0") &&
                               !feedbackReport.Contains("p9.presentation.feedback.slow=0") &&
                               !feedbackReport.Contains("p9.presentation.feedback.specialization=0") &&
                               !feedbackReport.Contains("p9.presentation.feedback.resonance=0") &&
                               !feedbackReport.Contains("p9.presentation.feedback.leak=0");

                var cinematicBefore = _battlePresentation.CinematicCount;
                _battlePresentation.ShowCinematic("[W]", "WAVE TRANSITION", "Test", TDBattleFeedbackTier.Critical, 1.0f);
                _battlePresentation.ShowCinematic("[B!]", "BOSS WARNING", "Test", TDBattleFeedbackTier.Critical, 1.2f);
                _battlePresentation.ShowCinematic("[!!]", "CRITICAL DEFENSE", "Test", TDBattleFeedbackTier.Critical, 1.4f);
                cinematicPass = _battlePresentation.CinematicCount >= cinematicBefore + 3;

                _tutorialVisible = true;
                _tutorialStep = TDFirstRunTutorialStep.BuildTower;
                PlayerPrefs.DeleteKey(tutorialCompleteKey);
                AdvanceTutorial(TDFirstRunTutorialStep.BuildTower);
                AdvanceTutorial(TDFirstRunTutorialStep.InspectRange);
                AdvanceTutorial(TDFirstRunTutorialStep.StartWave);
                ConfirmTutorialStep();
                AdvanceTutorial(TDFirstRunTutorialStep.UpgradeTower);
                AdvanceTutorial(TDFirstRunTutorialStep.UseScenario);
                tutorialPass = _tutorialStep == TDFirstRunTutorialStep.Complete &&
                               PlayerPrefs.GetInt(tutorialCompleteKey, 0) == 1 &&
                               PlayerPrefs.GetInt(tutorialStepKey, -1) == (int)TDFirstRunTutorialStep.Complete;

                PlayerPrefs.DeleteKey(tutorialCompleteKey);
                _tutorialVisible = true;
                _tutorialStep = TDFirstRunTutorialStep.BuildTower;
                SkipFirstRunTutorial();
                tutorialSkipPass = _tutorialStep == TDFirstRunTutorialStep.Complete &&
                                   PlayerPrefs.GetInt(tutorialCompleteKey, 0) == 1;

                _tutorialVisible = true;
                _tutorialStep = TDFirstRunTutorialStep.ReadArmor;
                RefreshTutorialUi();
                _battlePresentation.Tick(false);
                Canvas.ForceUpdateCanvases();
                var requiredNames = new[]
                {
                    "Playback And Accessibility",
                    "Playback II",
                    "Playback 1x",
                    "Playback 2x",
                    "Playback 3x",
                    "Colorblind Markers",
                    "Large Text",
                    "Interactive Tutorial",
                    "Tutorial Confirm",
                    "Tutorial Skip",
                    "Combat Feedback Signals",
                    "Combat Cinematic"
                };
                var transforms = _battleCanvas.GetComponentsInChildren<Transform>(true);
                var names = new HashSet<string>(transforms.Where(item => item != null).Select(item => item.name), StringComparer.Ordinal);
                uiPass = requiredNames.All(names.Contains);

                var layoutNames = new[] { "Playback And Accessibility", "Interactive Tutorial" };
                for (var i = 0; i < layoutNames.Length; i++)
                {
                    var target = transforms.FirstOrDefault(item => item != null && item.name == layoutNames[i]) as RectTransform;
                    if (target == null)
                    {
                        uiPass = false;
                        continue;
                    }

                    var corners = new Vector3[4];
                    target.GetWorldCorners(corners);
                    uiPass &= corners[0].x >= -1f && corners[0].y >= -1f &&
                              corners[2].x <= Screen.width + 1f && corners[2].y <= Screen.height + 1f;
                }

                var criticalTexts = new[] { "Tutorial Progress", "Tutorial Title", "Tutorial Body", "Signal Title", "Signal Body" };
                var labels = _battleCanvas.GetComponentsInChildren<Text>(true);
                for (var i = 0; i < labels.Length; i++)
                {
                    var label = labels[i];
                    if (label != null && criticalTexts.Contains(label.name) &&
                        label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                    {
                        overflow.Add(label.name);
                    }
                }

                textFitPass = overflow.Count == 0;
            }
            finally
            {
                if (hadTutorialStep)
                {
                    PlayerPrefs.SetInt(tutorialStepKey, originalTutorialStepPref);
                }
                else
                {
                    PlayerPrefs.DeleteKey(tutorialStepKey);
                }

                if (hadTutorialComplete)
                {
                    PlayerPrefs.SetInt(tutorialCompleteKey, originalTutorialCompletePref);
                }
                else
                {
                    PlayerPrefs.DeleteKey(tutorialCompleteKey);
                }

                PlayerPrefs.Save();
                _playbackSpeed = originalSpeed;
                _lastActivePlaybackSpeed = originalLastSpeed;
                _playbackPaused = originalPaused;
                _colorblindMarkersEnabled = originalMarkers;
                _largeTextEnabled = originalLargeText;
                _tutorialStep = originalTutorialStep;
                _tutorialVisible = originalTutorialVisible;
                _missionBoardOpen = originalMissionBoardOpen;
                _formationPanelOpen = originalFormationOpen;
                _campaignProfileOpen = originalProfileOpen;
                _campaignDeploymentConfirmed = originalDeploymentConfirmed;
                Time.timeScale = originalTimeScale;
                ApplyLargeTextMode();
                _battlePresentation.SetAccessibilityState(originalMarkers, originalLargeText);
                _battlePresentation.SetPlaybackState(originalLastSpeed, originalPaused);
                RefreshTutorialUi();
            }

            var pass = playbackPass && feedbackPass && cinematicPass && accessibilityPass &&
                       tutorialPass && tutorialSkipPass && uiPass && textFitPass;
            return
                $"p9.audit.playback={playbackPass}\n" +
                $"p9.audit.feedback6={feedbackPass}\n" +
                $"p9.audit.cinematics={cinematicPass}\n" +
                $"p9.audit.accessibility={accessibilityPass}\n" +
                $"p9.audit.tutorialFlow={tutorialPass}\n" +
                $"p9.audit.tutorialSkip={tutorialSkipPass}\n" +
                $"p9.audit.ui={uiPass}\n" +
                $"p9.audit.textOverflow={(textFitPass ? "none" : string.Join(",", overflow))}\n" +
                $"p9.audit.pass={pass}\n";
        }

        public string DebugAuditP111ForTest()
        {
            var hudPaths = new[]
            {
                TDUiVisualIdentity.WaveIconPath,
                TDUiVisualIdentity.IntegrityIconPath,
                TDUiVisualIdentity.BudgetIconPath,
                TDUiVisualIdentity.BuildIconPath,
                TDUiVisualIdentity.DamageIconPath,
                TDUiVisualIdentity.UtilityIconPath,
                TDUiVisualIdentity.RouteIconPath,
                TDUiVisualIdentity.EnemyIconPath,
                TDUiVisualIdentity.SpeedIconPath,
                TDUiVisualIdentity.PauseIconPath,
                TDUiWorldSkin.CommandFramePath,
                TDUiWorldSkin.CompactFramePath,
                TDUiWorldSkin.ActionFramePath
            };
            var towerKinds = (TDTowerKind[])Enum.GetValues(typeof(TDTowerKind));
            var missingResources = new List<string>();
            var iconPaths = new HashSet<string>(StringComparer.Ordinal);
            var roleLabels = new HashSet<string>(StringComparer.Ordinal);
            var identityColors = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < hudPaths.Length; i++)
            {
                if (Resources.Load<Texture2D>(hudPaths[i]) == null && Resources.Load<Sprite>(hudPaths[i]) == null)
                {
                    missingResources.Add(hudPaths[i]);
                }
            }

            for (var i = 0; i < towerKinds.Length; i++)
            {
                var identity = TDUiVisualIdentity.GetTower(towerKinds[i]);
                iconPaths.Add(identity.iconResourcePath);
                roleLabels.Add(identity.roleLabel);
                identityColors.Add(ColorUtility.ToHtmlStringRGB(identity.accent));
                if (Resources.Load<Texture2D>(identity.iconResourcePath) == null && Resources.Load<Sprite>(identity.iconResourcePath) == null)
                {
                    missingResources.Add(identity.iconResourcePath);
                }
            }

            Canvas.ForceUpdateCanvases();
            var metricIconsReady = _battleCanvas != null &&
                                   _battleCanvas.GetComponentsInChildren<Image>(true).Any(image => image.name == "Wave Metric Icon" && image.sprite != null) &&
                                   _battleCanvas.GetComponentsInChildren<Image>(true).Any(image => image.name == "Integrity Metric Icon" && image.sprite != null) &&
                                   _battleCanvas.GetComponentsInChildren<Image>(true).Any(image => image.name == "Budget Metric Icon" && image.sprite != null);
            var towerIconsReady = _uiTowerButtonIcons.Count == _unlockedTowerKinds.Count &&
                                  _uiTowerButtonIcons.All(image => image != null && image.sprite != null);
            var formationIconsReady = _uiFormationTowerIcons.Count == towerKinds.Length &&
                                      _uiFormationTowerIcons.All(image => image != null && image.sprite != null);
            var identityPass = iconPaths.Count == towerKinds.Length &&
                               roleLabels.Count == towerKinds.Length &&
                               identityColors.Count == towerKinds.Length;

            var expectedBaseSizes = new HashSet<int> { 10, 11, 12, 14, 16, 20 };
            var typographyLabels = new List<Text>
            {
                _uiTitleText,
                _uiWaveMetricText,
                _uiIntegrityMetricText,
                _uiBudgetMetricText,
                _uiTowerTitleText,
                _uiDamageUpgradeButtonText,
                _uiUtilityUpgradeButtonText
            };
            typographyLabels.AddRange(_uiTowerButtonTexts);
            typographyLabels.AddRange(_uiFormationTowerButtonTexts);
            var worldFont = Resources.Load<Font>(TDUiWorldSkin.FontPath);
            var worldFontReady = worldFont != null && typographyLabels
                .Where(label => label != null)
                .All(label => label.font == worldFont);
            var typographyPass = true;
            var typographyIssues = new List<string>();
            var overflow = new List<string>();
            for (var i = 0; i < typographyLabels.Count; i++)
            {
                var label = typographyLabels[i];
                if (label == null)
                {
                    continue;
                }

                var baseSize = label.fontSize - (_largeTextEnabled ? 1 : 0);
                if (!expectedBaseSizes.Contains(baseSize))
                {
                    typographyPass = false;
                    typographyIssues.Add($"{label.name}:{label.fontSize}");
                }
                if (label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                {
                    overflow.Add(label.name);
                }
            }

            var pass = missingResources.Count == 0 && metricIconsReady && towerIconsReady && formationIconsReady &&
                       identityPass && worldFontReady && typographyPass && overflow.Count == 0;
            return
                $"p11.1.audit.resources={(missingResources.Count == 0 ? "ready" : string.Join(",", missingResources))}\n" +
                $"p11.1.audit.metricIcons={metricIconsReady}\n" +
                $"p11.1.audit.towerIcons={towerIconsReady} [{_uiTowerButtonIcons.Count}/{_unlockedTowerKinds.Count}]\n" +
                $"p11.1.audit.formationIcons={formationIconsReady} [{_uiFormationTowerIcons.Count}/{towerKinds.Length}]\n" +
                $"p11.1.audit.identities={identityPass} [icons={iconPaths.Count},roles={roleLabels.Count},colors={identityColors.Count}]\n" +
                $"p11.1.audit.worldFont={worldFontReady}\n" +
                $"p11.1.audit.typography={typographyPass} [{(typographyIssues.Count == 0 ? "canonical" : string.Join(",", typographyIssues))}]\n" +
                $"p11.1.audit.textOverflow={(overflow.Count == 0 ? "none" : string.Join(",", overflow))}\n" +
                $"p11.1.audit.pass={pass}\n";
        }

        public string DebugPrepareP112PresentationForTest()
        {
            _activeEnemies.RemoveAll(enemy => enemy == null);
            if (_activeEnemies.Count < 8)
            {
                var enemyIds = new[]
                {
                    "skitter_runner",
                    "ash_swarm",
                    "carapace_brute",
                    "ember_leech",
                    "echo_mimic",
                    "cinder_glider",
                    "husk_titan",
                    "furnace_matriarch"
                };
                var lanes = new[] { "left", "center", "left", "center", "left", "center", "center", "left" };
                var progress = new[] { 0.14f, 0.23f, 0.31f, 0.40f, 0.49f, 0.52f, 0.58f, 0.67f };
                for (var i = 0; i < enemyIds.Length; i++)
                {
                    DebugSpawnEnemyForTest(enemyIds[i], 1, lanes[i], progress[i], 24f);
                }
            }

            var focus = _activeEnemies.FirstOrDefault(enemy => enemy != null && enemy.EnemyId == "carapace_brute") ??
                        _activeEnemies.FirstOrDefault(enemy => enemy != null);
            if (focus != null)
            {
                focus.ApplyArmorBreak(6, 30f);
                focus.TakeHit(1, 0.42f, 30f);
                focus.ApplyStagger(30f, 0.46f);
                focus.ApplyExposed(30f, 1.16f);
                focus.SetResonanceMark(30f);
            }

            return $"p11.2.fixture.enemies={_activeEnemies.Count} statusFocus={(focus != null ? focus.EnemyId : "none")}";
        }

        public string DebugSetRoutePreviewForTest(bool visible)
        {
            _debugRoutePreviewVisible = visible;
            UpdateRoutePreview();
            var activeLines = _routePreviewLines.Count(line =>
                line != null && line.enabled && line.gameObject.activeInHierarchy);
            return $"routePreview.debug={visible} activeLines={activeLines}";
        }

        public string DebugPrepareP112CombatForTest()
        {
            var presentation = DebugPrepareP112PresentationForTest();
            var existingKinds = new HashSet<TDTowerKind>(FindObjectsByType<TDTower>(FindObjectsSortMode.None).Select(tower => tower.Kind));
            var candidates = new List<KeyValuePair<Vector2Int, float>>();
            var pathPoints = _activeLanePaths.Values
                .Where(path => path != null)
                .SelectMany(path => path)
                .ToArray();

            if (_gridMap != null)
            {
                for (var y = 0; y < _gridMap.Height; y++)
                {
                    for (var x = 0; x < _gridMap.Width; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!_gridMap.IsBuildable(cell))
                        {
                            continue;
                        }

                        var world = _gridMap.CellToBuildWorld(cell);
                        var pathDistance = pathPoints.Length == 0
                            ? world.sqrMagnitude
                            : pathPoints.Min(point => (point - world).sqrMagnitude);
                        var centerBias = Mathf.Abs(world.x) * 0.015f;
                        candidates.Add(new KeyValuePair<Vector2Int, float>(cell, pathDistance + centerBias));
                    }
                }
            }

            candidates.Sort((left, right) => left.Value.CompareTo(right.Value));
            var selectedCells = new List<Vector2Int>();
            for (var i = 0; i < candidates.Count && selectedCells.Count < 8; i++)
            {
                var cell = candidates[i].Key;
                if (selectedCells.Any(other => Mathf.Abs(other.x - cell.x) + Mathf.Abs(other.y - cell.y) < 2))
                {
                    continue;
                }

                selectedCells.Add(cell);
            }

            var towerKinds = TDTower.GetBuildOrder();
            var spawnedTowers = 0;
            for (var i = 0; i < towerKinds.Count && i < selectedCells.Count; i++)
            {
                var kind = towerKinds[i];
                if (existingKinds.Contains(kind))
                {
                    continue;
                }

                var cell = selectedCells[i];
                _gridMap.SetTower(cell, true);
                SpawnTower(cell, kind);
                spawnedTowers++;
            }

            return $"{presentation} towers={FindObjectsByType<TDTower>(FindObjectsSortMode.None).Length} spawned={spawnedTowers}";
        }

        public string DebugAuditP112ForTest()
        {
            const string root = "Art/Combat/P11/";
            var threatPaths = new[]
            {
                root + "threat_fast",
                root + "threat_swarm",
                root + "threat_armored",
                root + "threat_support",
                root + "threat_special",
                root + "threat_boss",
                root + "threat_pip"
            };
            var statusPaths = new[]
            {
                root + "status_slow",
                root + "status_armor_break",
                root + "status_stagger",
                root + "status_exposed",
                root + "status_resonance"
            };
            var towerKinds = (TDTowerKind[])Enum.GetValues(typeof(TDTowerKind));
            var projectilePaths = new HashSet<string>(StringComparer.Ordinal);
            var impactPaths = new HashSet<string>(StringComparer.Ordinal);
            var missing = new List<string>();

            foreach (var path in threatPaths.Concat(statusPaths))
            {
                if (Resources.Load<Sprite>(path) == null)
                {
                    missing.Add(path);
                }
            }

            for (var i = 0; i < towerKinds.Length; i++)
            {
                var projectilePath = TDProjectile.GetProjectileResourcePath(towerKinds[i]);
                var impactPath = TDProjectile.GetImpactResourcePath(towerKinds[i]);
                projectilePaths.Add(projectilePath);
                impactPaths.Add(impactPath);
                if (Resources.Load<Sprite>(projectilePath) == null)
                {
                    missing.Add(projectilePath);
                }
                if (Resources.Load<Sprite>(impactPath) == null)
                {
                    missing.Add(impactPath);
                }
            }

            _activeEnemies.RemoveAll(enemy => enemy == null);
            var readability = _activeEnemies
                .Select(enemy => enemy.Readability)
                .Where(item => item != null)
                .ToArray();
            var levels = new HashSet<TDEnemyThreatLevel>(readability.Select(item => item.ThreatLevel));
            var categories = new HashSet<TDEnemyThreatCategory>(readability.Select(item => item.ThreatCategory));
            var outlinesPass = readability.Length == _activeEnemies.Count && readability.All(item => item.HasOutline);
            var markersPass = readability
                .Where(item => item.ThreatLevel >= TDEnemyThreatLevel.Tactical)
                .All(item => item.ThreatMarkerVisible);
            var statusPass = readability.Any(item => item.VisibleStatusCount >= 5);
            var resourcePass = missing.Count == 0;
            var projectilePass = projectilePaths.Count == towerKinds.Length && impactPaths.Count == towerKinds.Length;
            var threatPass = levels.Count == 4 && categories.Count == 6;
            var shaderPass = Shader.Find("TD/EnemySilhouette") != null;
            var pass = resourcePass && projectilePass && threatPass && outlinesPass && markersPass && statusPass && shaderPass;

            return
                $"p11.2.audit.resources={(resourcePass ? "ready" : string.Join(",", missing))}\n" +
                $"p11.2.audit.projectiles={projectilePass} [projectiles={projectilePaths.Count},impacts={impactPaths.Count}]\n" +
                $"p11.2.audit.threatMatrix={threatPass} [levels={levels.Count},categories={categories.Count}]\n" +
                $"p11.2.audit.outlines={outlinesPass} [{readability.Count(item => item.HasOutline)}/{_activeEnemies.Count}]\n" +
                $"p11.2.audit.markers={markersPass} [{readability.Count(item => item.ThreatMarkerVisible)}]\n" +
                $"p11.2.audit.statusStrip={statusPass} [max={readability.Select(item => item.VisibleStatusCount).DefaultIfEmpty(0).Max()}]\n" +
                $"p11.2.audit.shader={shaderPass}\n" +
                $"p11.2.audit.pass={pass}\n";
        }

        public string DebugPrepareP113ForTest()
        {
            var presentation = DebugPrepareP112PresentationForTest();
            var towerKinds = TDTower.GetBuildOrder();
            var existing = FindObjectsByType<TDTower>(FindObjectsSortMode.None).ToList();
            if (_gridMap != null && existing.Count < towerKinds.Count)
            {
                var selectedCells = new List<Vector2Int>();
                foreach (var cell in _gridMap.RecommendedBuildCells)
                {
                    if (_gridMap.IsBuildable(cell))
                    {
                        selectedCells.Add(cell);
                    }
                }

                for (var y = 0; y < _gridMap.Height && selectedCells.Count < towerKinds.Count; y++)
                {
                    for (var x = 0; x < _gridMap.Width && selectedCells.Count < towerKinds.Count; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!_gridMap.IsBuildable(cell) || selectedCells.Contains(cell))
                        {
                            continue;
                        }

                        if (selectedCells.Any(other => Mathf.Abs(other.x - cell.x) + Mathf.Abs(other.y - cell.y) < 2))
                        {
                            continue;
                        }

                        selectedCells.Add(cell);
                    }
                }

                var existingKinds = new HashSet<TDTowerKind>(existing.Select(tower => tower.Kind));
                for (var i = 0; i < towerKinds.Count && i < selectedCells.Count; i++)
                {
                    var kind = towerKinds[i];
                    if (existingKinds.Contains(kind))
                    {
                        continue;
                    }

                    var cell = selectedCells[i];
                    _gridMap.SetTower(cell, true);
                    existing.Add(SpawnTower(cell, kind));
                }
            }

            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None)
                .OrderBy(tower => tower.GridCell.y)
                .ThenBy(tower => tower.GridCell.x)
                .ToArray();
            for (var i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                var targetTier = 1 + (i % 3);
                var branch = i % 2 == 0 ? TDTowerUpgradeBranch.Damage : TDTowerUpgradeBranch.Utility;
                while (tower.Tier < targetTier && tower.CanUpgrade)
                {
                    tower.ApplyUpgrade(branch);
                }

                tower.Readability?.DebugHoldCharge(Mathf.Lerp(0.25f, 1f, (i + 1f) / Mathf.Max(1f, towers.Length)));
            }

            if (towers.Length > 0)
            {
                SelectTowerForUi(towers[0]);
                UpdateTowerUpgradePanelUi();
            }

            return $"{presentation} towers={towers.Length} upgraded={towers.Count(tower => tower.Tier > 0)} " +
                   $"buildSpots={_gridMap?.RecommendedBuildSpotCount ?? 0} hidden={_gridMap?.HiddenBuildSpotCount ?? 0}";
        }

        public string DebugAuditP113ForTest()
        {
            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            _activeEnemies.RemoveAll(enemy => enemy == null);
            var towerReadability = towers.Select(tower => tower.Readability).Where(item => item != null).ToArray();
            var towerBodyOrders = towerReadability.Select(item => item.BodySortingOrder).ToArray();
            var enemyBodyOrders = new List<int>();
            var enemySortingPass = true;
            for (var i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                var visual = enemy != null ? enemy.transform.Find("Visual") : null;
                var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                if (renderer == null)
                {
                    enemySortingPass = false;
                    continue;
                }

                enemyBodyOrders.Add(renderer.sortingOrder);
                enemySortingPass &= renderer.sortingOrder == TDWorldVisualOrder.ResolveBodyOrder(enemy.transform.position.y);
            }

            var towerSortingPass = towers.All(tower =>
                tower.Readability != null &&
                tower.Readability.BodySortingOrder == TDWorldVisualOrder.ResolveBodyOrder(tower.transform.position.y));
            var allBodyOrders = towerBodyOrders.Concat(enemyBodyOrders).ToArray();
            var orderingPass = towerSortingPass && enemySortingPass && allBodyOrders.Distinct().Count() >= 2 &&
                               TDWorldVisualOrder.RangePreview < allBodyOrders.DefaultIfEmpty(int.MaxValue).Min() &&
                               TDWorldVisualOrder.Projectile > allBodyOrders.DefaultIfEmpty(0).Max() &&
                               TDWorldVisualOrder.ProjectileFx < TDWorldVisualOrder.EnemyTrait &&
                               TDWorldVisualOrder.EnemyTrait < TDWorldVisualOrder.EnemyStatus &&
                               TDWorldVisualOrder.EnemyStatus < TDWorldVisualOrder.EnemyThreat;
            var foundationResource = Resources.Load<Sprite>("Art/tower_base_plate") != null;
            var foundationPass = foundationResource && towers.Length >= 8 && towers.All(tower => tower.HasFoundation);
            var minimumBuildClearance = _gridMap != null && _gridMap.RecommendedBuildCells.Count > 0
                ? _gridMap.RecommendedBuildCells.Min(cell => _gridMap.GetRoadClearance(cell))
                : 0f;
            var safelyPlacedTowerCount = _gridMap == null
                ? 0
                : towers.Count(tower => _gridMap.IsRecommendedBuildCell(tower.GridCell) &&
                                        _gridMap.GetRoadClearance(tower.GridCell) >= _gridMap.RequiredRoadClearance);
            var buildSpotPass = _gridMap != null && _gridMap.PreviewUsesFoundation &&
                                _gridMap.PreviewHasLegalityOutline &&
                                _gridMap.UsesAuthoredBuildCells && _gridMap.RecommendedBuildSpotCount == 12 &&
                                _gridMap.HiddenBuildSpotCount > 0 &&
                                minimumBuildClearance >= _gridMap.RequiredRoadClearance &&
                                safelyPlacedTowerCount == towers.Length;
            var distinctLanePaths = _activeLanePaths.Values
                .Where(path => path != null && path.Count > 1)
                .Distinct()
                .ToArray();
            var maxRouteStep = distinctLanePaths
                .SelectMany(path => Enumerable.Range(0, path.Count - 1)
                    .Select(index => Vector3.Distance(path[index], path[index + 1])))
                .DefaultIfEmpty(0f)
                .Max();
            var maxEnemyRouteDeviation = _activeEnemies
                .Where(enemy => enemy != null)
                .Select(enemy => enemy.RouteDeviationWorld)
                .DefaultIfEmpty(0f)
                .Max();
            var maxGroundContactDeviation = _activeEnemies
                .Where(enemy => enemy != null)
                .Select(enemy => enemy.GroundContactRouteDeviationWorld)
                .DefaultIfEmpty(0f)
                .Max();
            var visibleRouteLines = _routePreviewLines.Count(line =>
                line != null && line.enabled && line.gameObject.activeInHierarchy);
            var routeIntegrityPass = distinctLanePaths.Length >= 4 && maxRouteStep <= 0.14f &&
                                     maxEnemyRouteDeviation <= 0.01f &&
                                     maxGroundContactDeviation <= 0.01f && visibleRouteLines == 0;
            var repairModes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["ember_leech"] = 0f,
                ["furnace_matriarch"] = 1f,
                ["cinder_glider"] = 2f
            };
            var bodyRepairShader = Shader.Find("TD/EnemyBodyRepair");
            var repairedEnemyCount = 0;
            foreach (var repair in repairModes)
            {
                var enemy = _activeEnemies.FirstOrDefault(item => item != null && item.EnemyId == repair.Key);
                var visual = enemy != null ? enemy.transform.Find("Visual") : null;
                var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                var material = renderer != null ? renderer.sharedMaterial : null;
                if (material != null && material.shader != null && material.shader.name == "TD/EnemyBodyRepair" &&
                    material.HasProperty("_RepairMode") && Mathf.Approximately(material.GetFloat("_RepairMode"), repair.Value))
                {
                    repairedEnemyCount++;
                }
            }

            var silhouetteRepairPass = bodyRepairShader != null && bodyRepairShader.isSupported &&
                                       repairedEnemyCount == repairModes.Count;
            var bossReadability = _activeEnemies
                .Select(enemy => enemy != null ? enemy.Readability : null)
                .FirstOrDefault(item => item != null && item.ThreatLevel == TDEnemyThreatLevel.Boss);
            var threatMarkerIntegrationPass = bossReadability != null && bossReadability.ThreatMarkerVisible &&
                                              bossReadability.ThreatMarkerGapWorld <= 0.36f &&
                                              bossReadability.ThreatMarkerScale <= 0.25f;
            var chargePass = _gridMap != null && towerReadability.Length == towers.Length && towerReadability.All(item =>
                item.ChargeVisible && item.ChargeProgress > 0f &&
                item.ChargeDiameterWorld <= (_gridMap.CellSize * 0.62f) + 0.001f);
            var upgradePass = towers.Length >= 8 && towers.All(tower =>
                tower.Tier > 0 && tower.Readability != null &&
                tower.Readability.VisibleTierPips == tower.Tier &&
                tower.Readability.UpgradePresentationCount >= tower.Tier) &&
                              towers.Max(tower => tower.Tier) == 3;
            var pass = foundationPass && buildSpotPass && routeIntegrityPass && silhouetteRepairPass &&
                       threatMarkerIntegrationPass && chargePass && upgradePass && orderingPass;

            return
                $"p11.3.audit.foundation={foundationPass} [{towers.Count(tower => tower.HasFoundation)}/{towers.Length}]\n" +
                $"p11.3.audit.buildSpots={buildSpotPass} [total={_gridMap?.RecommendedBuildSpotCount ?? 0},authored={_gridMap?.UsesAuthoredBuildCells ?? false},minClearance={minimumBuildClearance:0.00},towersSafe={safelyPlacedTowerCount}/{towers.Length}]\n" +
                $"p11.3.audit.routeIntegrity={routeIntegrityPass} [lanes={distinctLanePaths.Length},maxStep={maxRouteStep:0.000},rootDeviation={maxEnemyRouteDeviation:0.000},groundDeviation={maxGroundContactDeviation:0.000},forecastLines={visibleRouteLines}]\n" +
                $"p11.3.audit.silhouetteRepair={silhouetteRepairPass} [{repairedEnemyCount}/{repairModes.Count},shader={bodyRepairShader?.name ?? "missing"}]\n" +
                $"p11.3.audit.threatMarkerIntegration={threatMarkerIntegrationPass} [gap={bossReadability?.ThreatMarkerGapWorld ?? 0f:0.00},scale={bossReadability?.ThreatMarkerScale ?? 0f:0.00}]\n" +
                $"p11.3.audit.charge={chargePass} [visible={towerReadability.Count(item => item.ChargeVisible)}/{towers.Length},maxDiameter={towerReadability.Select(item => item.ChargeDiameterWorld).DefaultIfEmpty(0f).Max():0.00}]\n" +
                $"p11.3.audit.upgrade={upgradePass} [tier3={towers.Count(tower => tower.Tier == 3)},presentations={towerReadability.Sum(item => item.UpgradePresentationCount)}]\n" +
                $"p11.3.audit.ordering={orderingPass} [body={string.Join(",", allBodyOrders.Distinct().OrderBy(value => value))},projectile={TDWorldVisualOrder.Projectile},range={TDWorldVisualOrder.RangePreview}]\n" +
                $"p11.3.audit.pass={pass}\n";
        }

        public string DebugAuditP120GeometryForTest()
        {
            var mapId = _campaignRoute?.level?.mapId ?? "unknown";
            var distinctPaths = _activeLanePaths.Values
                .Where(path => path != null && path.Count > 1)
                .Distinct()
                .ToArray();
            var expectedPathCount = string.Equals(mapId, "grayline_junction", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 4;
            var maxRouteStep = distinctPaths
                .SelectMany(path => Enumerable.Range(0, path.Count - 1)
                    .Select(index => Vector3.Distance(path[index], path[index + 1])))
                .DefaultIfEmpty(0f)
                .Max();
            var boardHalfWidth = GridWidth * CellSize * 0.5f;
            var boardHalfHeight = GridHeight * CellSize * 0.5f;
            var routeBoundsPass = distinctPaths.SelectMany(path => path).All(point =>
                point.x >= -boardHalfWidth - CellSize && point.x <= boardHalfWidth + CellSize &&
                point.y >= -boardHalfHeight - CellSize && point.y <= boardHalfHeight + CellSize);
            var routeEndpointsPass = distinctPaths.All(path =>
                path[0].x <= -boardHalfWidth + CellSize &&
                path[path.Count - 1].x >= boardHalfWidth - CellSize);
            var routeContinuityPass = distinctPaths.Length >= expectedPathCount &&
                                      maxRouteStep <= (CellSize * 1.45f) &&
                                      routeBoundsPass && routeEndpointsPass;

            var cells = _gridMap?.RecommendedBuildCells?.ToArray() ?? Array.Empty<Vector2Int>();
            var clearances = cells
                .Select(cell => _gridMap.GetRoadClearance(cell))
                .ToArray();
            var minimumClearance = clearances.DefaultIfEmpty(0f).Min();
            var maximumClearance = clearances.DefaultIfEmpty(float.MaxValue).Max();
            var maximumBaseRange = TDTower.GetBuildOrder()
                .Select(TDTower.GetBaseRange)
                .DefaultIfEmpty(0f)
                .Max();
            var buildableCount = _gridMap == null ? 0 : cells.Count(_gridMap.IsBuildable);
            var maximumUsefulClearance = maximumBaseRange + 0.10f;
            var usefulCoverageCount = clearances.Count(clearance => clearance <= maximumUsefulClearance);
            var outOfRangeCells = _gridMap == null
                ? Array.Empty<string>()
                : cells.Where(cell => _gridMap.GetRoadClearance(cell) > maximumUsefulClearance)
                    .Select(cell => $"{cell.x},{cell.y}:{_gridMap.GetRoadClearance(cell):0.00}")
                    .ToArray();
            var safeBoundsCount = _gridMap == null
                ? 0
                : cells.Count(cell =>
                {
                    var world = _gridMap.CellToBuildWorld(cell);
                    return Mathf.Abs(world.x) <= boardHalfWidth - 0.20f &&
                           Mathf.Abs(world.y) <= boardHalfHeight - 0.20f;
                });
            var minimumSiteSpacing = float.MaxValue;
            if (_gridMap != null)
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    for (var j = i + 1; j < cells.Length; j++)
                    {
                        minimumSiteSpacing = Mathf.Min(
                            minimumSiteSpacing,
                            Vector3.Distance(
                                _gridMap.CellToBuildWorld(cells[i]),
                                _gridMap.CellToBuildWorld(cells[j])));
                    }
                }
            }

            if (cells.Length < 2)
            {
                minimumSiteSpacing = 0f;
            }

            var authoredCells = _gridMap?.AuthoredBuildCells?.ToArray() ?? Array.Empty<Vector2Int>();
            var authoredValidity = _gridMap == null
                ? Array.Empty<TDBuildSiteValidity>()
                : authoredCells.Select(_gridMap.GetBuildSiteValidity).ToArray();
            var buildSitesPass = _gridMap != null && cells.Length == 12 &&
                                 authoredCells.Length == 12 &&
                                 buildableCount == cells.Length &&
                                 authoredValidity.All(validity => validity == TDBuildSiteValidity.Valid) &&
                                 usefulCoverageCount == cells.Length &&
                                 safeBoundsCount == cells.Length &&
                                 minimumClearance >= _gridMap.RequiredRoadClearance &&
                                 minimumSiteSpacing >= CellSize * 1.20f;
            var visibleRouteLines = _routePreviewLines.Count(line =>
                line != null && line.enabled && line.gameObject.activeInHierarchy);
            var maxEnemyDeviation = _activeEnemies
                .Where(enemy => enemy != null)
                .Select(enemy => enemy.RouteDeviationWorld)
                .DefaultIfEmpty(0f)
                .Max();
            var maxGroundContactDeviation = _activeEnemies
                .Where(enemy => enemy != null)
                .Select(enemy => enemy.GroundContactRouteDeviationWorld)
                .DefaultIfEmpty(0f)
                .Max();
            var runtimePass = visibleRouteLines == 0 && maxEnemyDeviation <= 0.01f &&
                              maxGroundContactDeviation <= 0.01f;
            var pass = routeContinuityPass && buildSitesPass && runtimePass;

            return
                $"p12.0.geometry.map={mapId}\n" +
                $"p12.0.geometry.routes={routeContinuityPass} [paths={distinctPaths.Length}/{expectedPathCount},maxStep={maxRouteStep:0.000},bounds={routeBoundsPass},endpoints={routeEndpointsPass}]\n" +
                $"p12.0.geometry.buildSites={buildSitesPass} [count={cells.Length},buildable={buildableCount},useful={usefulCoverageCount},bounds={safeBoundsCount},clearance={minimumClearance:0.00}-{maximumClearance:0.00},spacing={minimumSiteSpacing:0.00},outOfRange={(outOfRangeCells.Length == 0 ? "none" : string.Join(";", outOfRangeCells))}]\n" +
                $"p12.0.geometry.runtime={runtimePass} [forecastLines={visibleRouteLines},rootDeviation={maxEnemyDeviation:0.000},groundDeviation={maxGroundContactDeviation:0.000}]\n" +
                $"p12.0.geometry.cells={(cells.Length == 0 ? "none" : string.Join(";", cells.Select(cell => $"{cell.x},{cell.y}")))}\n" +
                $"p12.0.geometry.pass={pass}\n";
        }

        public string DebugPrepareP133ForTest()
        {
            _p133FixtureActive = true;
            var baseFixture = DebugPrepareP113ForTest();
            _activeEnemies.RemoveAll(enemy => enemy == null);

            var enemyIds = new[]
            {
                "skitter_runner",
                "ash_swarm",
                "carapace_brute",
                "ember_leech",
                "echo_mimic",
                "cinder_glider",
                "husk_titan",
                "furnace_matriarch",
                "skitter_runner",
                "carapace_brute"
            };
            var lanes = new[] { "left", "center", "right", "cross" };
            for (var i = _activeEnemies.Count; i < 18; i++)
            {
                var fixtureIndex = i % enemyIds.Length;
                var progress = 0.24f + (0.035f * (i % 11));
                DebugSpawnEnemyForTest(
                    enemyIds[fixtureIndex],
                    1,
                    lanes[i % lanes.Length],
                    progress,
                    24f);
            }

            var focus = _activeEnemies.FirstOrDefault(enemy =>
                            enemy != null && enemy.EnemyId == "carapace_brute") ??
                        _activeEnemies.FirstOrDefault(enemy => enemy != null);
            if (focus != null)
            {
                focus.ApplyArmorBreak(6, 30f);
                focus.TakeHit(1, 0.42f, 30f);
                focus.ApplyStagger(30f, 0.46f);
                focus.ApplyExposed(30f, 1.16f);
                focus.SetResonanceMark(30f);
            }

            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None)
                .OrderBy(tower => tower.GridCell.y)
                .ThenBy(tower => tower.GridCell.x)
                .ToArray();
            if (towers.Length > 0)
            {
                SelectTowerForUi(towers[0]);
            }

            if (towers.Length > 1)
            {
                _hoveredTower = towers[1];
                towers[1].Readability?.SetInteractionState(true, false);
            }

            HideRangePreview();
            DebugSetRoutePreviewForTest(false);
            var invalidPreviewPoint = _activeLanePaths.Values
                .Where(path => path != null && path.Count > 0)
                .SelectMany(path => path)
                .OrderBy(point => Mathf.Abs(point.x) + Mathf.Abs(point.y))
                .FirstOrDefault();
            _gridMap?.UpdateBuildPreview(invalidPreviewPoint);
            Canvas.ForceUpdateCanvases();

            return $"{baseFixture} p13.3.fixture.enemies={_activeEnemies.Count} towers={towers.Length} " +
                   $"selected={(towers.Length > 0 ? towers[0].GridCell.ToString() : "none")} " +
                   $"hovered={(towers.Length > 1 ? towers[1].GridCell.ToString() : "none")} " +
                   $"preview={_gridMap?.LastPreviewValidity.ToString() ?? "missing"}";
        }

        public string DebugAuditP133ForTest()
        {
            _activeEnemies.RemoveAll(enemy => enemy == null);
            var mapId = _campaignRoute?.level?.mapId ?? "unknown";
            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            var towerReadability = towers
                .Select(tower => tower.Readability)
                .Where(readability => readability != null)
                .ToArray();
            var enemies = _activeEnemies.Where(enemy => enemy != null).ToArray();
            var enemyReadability = enemies
                .Select(enemy => enemy.Readability)
                .Where(readability => readability != null)
                .ToArray();

            var distinctPaths = _activeLanePaths.Values
                .Where(path => path != null && path.Count > 1)
                .Distinct()
                .ToArray();
            var expectedPathCount = string.Equals(mapId, "grayline_junction", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 4;
            var maxRouteStep = distinctPaths
                .SelectMany(path => Enumerable.Range(0, path.Count - 1)
                    .Select(index => Vector3.Distance(path[index], path[index + 1])))
                .DefaultIfEmpty(0f)
                .Max();
            var maxCurrentDeviation = enemies
                .Select(enemy => enemy.RouteDeviationWorld)
                .DefaultIfEmpty(0f)
                .Max();
            var maxObservedDeviation = enemies
                .Select(enemy => enemy.MaximumRouteDeviationObserved)
                .DefaultIfEmpty(0f)
                .Max();
            var maxGroundContactDeviation = enemies
                .Select(enemy => enemy.GroundContactRouteDeviationWorld)
                .DefaultIfEmpty(0f)
                .Max();
            var visibleRouteLines = _routePreviewLines.Count(line =>
                line != null && line.enabled && line.gameObject.activeInHierarchy);
            var routePass = distinctPaths.Length == expectedPathCount &&
                            maxRouteStep <= 0.14f &&
                            maxCurrentDeviation <= 0.01f &&
                            maxObservedDeviation <= 0.01f &&
                            maxGroundContactDeviation <= 0.01f &&
                            visibleRouteLines == 0;

            var authoredCells = _gridMap?.AuthoredBuildCells?.ToArray() ?? Array.Empty<Vector2Int>();
            var invalidAuthored = _gridMap == null
                ? new List<string> { "grid:missing" }
                : authoredCells
                    .Select(cell => new
                    {
                        Cell = cell,
                        Validity = _gridMap.GetBuildSiteValidity(cell),
                        Clearance = _gridMap.GetRoadClearance(cell),
                        Bounds = _gridMap.IsBuildFootprintInsideBoard(cell)
                    })
                    .Where(item =>
                        (item.Validity != TDBuildSiteValidity.Valid &&
                         item.Validity != TDBuildSiteValidity.Occupied) ||
                        item.Clearance < _gridMap.RequiredRoadClearance ||
                        !item.Bounds)
                    .Select(item =>
                        $"{item.Cell.x},{item.Cell.y}:{item.Validity}:{item.Clearance:0.00}:{item.Bounds}")
                    .ToList();
            var uiObscuredAuthored = authoredCells
                .Where(cell => !IsP133BuildSiteClearOfPersistentUi(cell))
                .Select(cell => $"{cell.x},{cell.y}")
                .ToArray();
            var buildSitePass = _gridMap != null &&
                                _gridMap.UsesAuthoredBuildCells &&
                                authoredCells.Length == 12 &&
                                _gridMap.RecommendedBuildSpotCount == 12 &&
                                invalidAuthored.Count == 0 &&
                                uiObscuredAuthored.Length == 0 &&
                                _gridMap.FoundationDiameterWorld <= (CellSize * 0.80f) + 0.001f;
            var maximumBaseRange = TDTower.GetBuildOrder()
                .Select(TDTower.GetBaseRange)
                .DefaultIfEmpty(0f)
                .Max();
            var geometryCandidates = new List<string>();
            if (_gridMap != null)
            {
                for (var y = _gridMap.Height - 1; y >= 0; y--)
                {
                    for (var x = 0; x < _gridMap.Width; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        var clearance = _gridMap.GetRoadClearance(cell);
                        if (_gridMap.IsBuildFootprintInsideBoard(cell) &&
                            IsP133BuildSiteClearOfPersistentUi(cell) &&
                            clearance >= _gridMap.RequiredRoadClearance &&
                            clearance <= maximumBaseRange + 0.10f)
                        {
                            geometryCandidates.Add($"{x},{y}:{clearance:0.00}");
                        }
                    }
                }
            }
            var towerPlacementPass = _gridMap != null && towers.Length >= 8 &&
                                     towers.All(tower =>
                                         tower.HasFoundation &&
                                         _gridMap.IsRecommendedBuildCell(tower.GridCell) &&
                                         _gridMap.IsBuildFootprintInsideBoard(tower.GridCell) &&
                                         _gridMap.GetRoadClearance(tower.GridCell) >= _gridMap.RequiredRoadClearance);

            var maximumTurnPose = enemies
                .Select(enemy => Mathf.Abs(enemy.TurnPoseDegrees))
                .DefaultIfEmpty(0f)
                .Max();
            var shadowPass = enemies.Length >= 18 &&
                             enemies.All(enemy => enemy.MotionReady &&
                                                  enemy.FootShadowAligned &&
                                                  Mathf.Abs(enemy.TurnPoseDegrees) <= 4.30f &&
                                                  Mathf.Abs(enemy.FacingSign) == 1);
            var minimumShadowGap = enemies.Select(enemy => enemy.FootShadowGapWorld).DefaultIfEmpty(0f).Min();
            var maximumShadowGap = enemies.Select(enemy => enemy.FootShadowGapWorld).DefaultIfEmpty(0f).Max();
            var minimumShadowAspect = enemies.Select(enemy => enemy.ShadowAspectRatio).DefaultIfEmpty(0f).Min();
            var maximumShadowAspect = enemies.Select(enemy => enemy.ShadowAspectRatio).DefaultIfEmpty(0f).Max();

            var selectedCount = towerReadability.Count(readability =>
                readability.InteractionVisible && readability.IsSelected);
            var hoveredCount = towerReadability.Count(readability =>
                readability.InteractionVisible && readability.IsHovered && !readability.IsSelected);
            var maximumInteractionDiameter = towerReadability
                .Where(readability => readability.InteractionVisible)
                .Select(readability => readability.InteractionDiameterWorld)
                .DefaultIfEmpty(0f)
                .Max();
            var interactionPass = _gridMap != null &&
                                  selectedCount == 1 &&
                                  hoveredCount == 1 &&
                                  maximumInteractionDiameter <= (CellSize * 0.90f) + 0.001f &&
                                  _gridMap.PreviewUsesFoundation &&
                                  _gridMap.PreviewHasLegalityOutline &&
                                  _gridMap.BuildPreviewVisible &&
                                  _gridMap.LastPreviewValidity != TDBuildSiteValidity.Valid;

            var tacticalReadability = enemyReadability
                .Where(readability => readability.ThreatLevel >= TDEnemyThreatLevel.Tactical)
                .ToArray();
            var traitPass = enemyReadability.Length == enemies.Length &&
                            tacticalReadability.Length > 0 &&
                            tacticalReadability.All(readability =>
                                readability.WeaknessVisible &&
                                readability.VisibleTraitCount >= 1 &&
                                readability.VisualPriorityValid) &&
                            tacticalReadability
                                .Where(readability =>
                                    readability.ThreatCategory == TDEnemyThreatCategory.Armored ||
                                    readability.ThreatLevel == TDEnemyThreatLevel.Boss)
                                .All(readability => readability.ResistanceVisible);
            var statusPass = enemyReadability.Any(readability => readability.VisibleStatusCount >= 5);

            var allBodyOrders = towers
                .Where(tower => tower.Readability != null)
                .Select(tower => tower.Readability.BodySortingOrder)
                .Concat(enemies.Select(enemy =>
                {
                    var visual = enemy.transform.Find("Visual");
                    var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                    return renderer != null ? renderer.sortingOrder : int.MinValue;
                }))
                .Where(order => order != int.MinValue)
                .ToArray();
            var occlusionPass = allBodyOrders.Length == towers.Length + enemies.Length &&
                                TDWorldVisualOrder.BuildSpot < TDWorldVisualOrder.RangePreview &&
                                TDWorldVisualOrder.RangePreview < allBodyOrders.DefaultIfEmpty(int.MaxValue).Min() &&
                                TDWorldVisualOrder.GroundInteraction < allBodyOrders.DefaultIfEmpty(int.MaxValue).Min() &&
                                TDWorldVisualOrder.Projectile > allBodyOrders.DefaultIfEmpty(0).Max() &&
                                TDWorldVisualOrder.ProjectileFx < TDWorldVisualOrder.EnemyTrait &&
                                TDWorldVisualOrder.EnemyTrait < TDWorldVisualOrder.EnemyStatus &&
                                TDWorldVisualOrder.EnemyStatus < TDWorldVisualOrder.EnemyThreat &&
                                TDWorldVisualOrder.EnemyThreat <= TDWorldVisualOrder.EnemyCritical;
            var densityPass = towers.Length >= 8 && enemies.Length >= 18;
            var pass = routePass && buildSitePass && towerPlacementPass && shadowPass &&
                       interactionPass && traitPass && statusPass && occlusionPass && densityPass;

            return
                $"p13.3.audit.map={mapId}\n" +
                $"p13.3.audit.route={routePass} [paths={distinctPaths.Length}/{expectedPathCount},maxStep={maxRouteStep:0.000},rootDeviation={maxCurrentDeviation:0.000},observedDeviation={maxObservedDeviation:0.000},groundDeviation={maxGroundContactDeviation:0.000},forecastLines={visibleRouteLines}]\n" +
                $"p13.3.audit.buildSites={buildSitePass} [authored={authoredCells.Length},recommended={_gridMap?.RecommendedBuildSpotCount ?? 0},foundation={_gridMap?.FoundationDiameterWorld ?? 0f:0.00},invalid={(invalidAuthored.Count == 0 ? "none" : string.Join(";", invalidAuthored))},uiObscured={(uiObscuredAuthored.Length == 0 ? "none" : string.Join(";", uiObscuredAuthored))}]\n" +
                $"p13.3.audit.buildCandidates={(geometryCandidates.Count == 0 ? "none" : string.Join(";", geometryCandidates))}\n" +
                $"p13.3.audit.towerPlacement={towerPlacementPass} [safe={towers.Count(tower => _gridMap != null && tower.HasFoundation && _gridMap.IsRecommendedBuildCell(tower.GridCell) && _gridMap.IsBuildFootprintInsideBoard(tower.GridCell) && _gridMap.GetRoadClearance(tower.GridCell) >= _gridMap.RequiredRoadClearance)}/{towers.Length}]\n" +
                $"p13.3.audit.enemyMotion={shadowPass} [count={enemies.Length},turn={maximumTurnPose:0.00},shadowGap={minimumShadowGap:0.000}-{maximumShadowGap:0.000},shadowAspect={minimumShadowAspect:0.00}-{maximumShadowAspect:0.00}]\n" +
                $"p13.3.audit.interaction={interactionPass} [selected={selectedCount},hovered={hoveredCount},maxDiameter={maximumInteractionDiameter:0.00},preview={_gridMap?.LastPreviewValidity.ToString() ?? "missing"}]\n" +
                $"p13.3.audit.traits={traitPass} [tactical={tacticalReadability.Length},weakness={tacticalReadability.Count(readability => readability.WeaknessVisible)},resistance={tacticalReadability.Count(readability => readability.ResistanceVisible)}]\n" +
                $"p13.3.audit.status={statusPass} [max={enemyReadability.Select(readability => readability.VisibleStatusCount).DefaultIfEmpty(0).Max()}]\n" +
                $"p13.3.audit.occlusion={occlusionPass} [body={string.Join(",", allBodyOrders.Distinct().OrderBy(order => order))},range={TDWorldVisualOrder.RangePreview},projectile={TDWorldVisualOrder.Projectile},trait={TDWorldVisualOrder.EnemyTrait},status={TDWorldVisualOrder.EnemyStatus},threat={TDWorldVisualOrder.EnemyThreat}]\n" +
                $"p13.3.audit.density={densityPass} [towers={towers.Length},enemies={enemies.Length}]\n" +
                $"p13.3.audit.pass={pass}\n";
        }

        private bool IsP133BuildSiteClearOfPersistentUi(Vector2Int cell)
        {
            if (_gridMap == null || _mainCamera == null)
            {
                return false;
            }

            var screenPoint = _mainCamera.WorldToScreenPoint(_gridMap.CellToBuildWorld(cell));
            var blockers = new[]
            {
                _uiTopPanel,
                _uiWaveIntelPanel,
                _uiScenarioPanel,
                _uiTowerPanelRoot,
                _uiTowerBarRoot,
                _uiEventFeedRoot,
                _uiStartWaveButton != null ? _uiStartWaveButton.transform as RectTransform : null
            };
            const float interactionMarginPixels = 34f;
            var corners = new Vector3[4];
            for (var i = 0; i < blockers.Length; i++)
            {
                var blocker = blockers[i];
                if (blocker == null || !blocker.gameObject.activeInHierarchy)
                {
                    continue;
                }

                blocker.GetWorldCorners(corners);
                var minimumX = Mathf.Min(corners[0].x, corners[2].x) - interactionMarginPixels;
                var maximumX = Mathf.Max(corners[0].x, corners[2].x) + interactionMarginPixels;
                var minimumY = Mathf.Min(corners[0].y, corners[2].y) - interactionMarginPixels;
                var maximumY = Mathf.Max(corners[0].y, corners[2].y) + interactionMarginPixels;
                if (screenPoint.x >= minimumX && screenPoint.x <= maximumX &&
                    screenPoint.y >= minimumY && screenPoint.y <= maximumY)
                {
                    return false;
                }
            }

            return true;
        }

        public string DebugPrepareP121ForTest()
        {
            var baseFixture = DebugPrepareP113ForTest();
            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None)
                .OrderBy(tower => tower.GridCell.y)
                .ThenBy(tower => tower.GridCell.x)
                .ToArray();
            _activeEnemies.RemoveAll(enemy => enemy == null);
            var enemies = _activeEnemies.Where(enemy => enemy != null).ToArray();

            _p121FixtureTowerAnimationCount = 0;
            _p121FixtureTowerMotionCount = 0;
            for (var i = 0; i < towers.Length; i++)
            {
                var visual = towers[i].transform.Find("Visual");
                var animator = visual != null ? visual.GetComponent<TDSpriteAnimator>() : null;
                if (animator != null && animator.IsConfigured && animator.FrameCount >= 6)
                {
                    _p121FixtureTowerAnimationCount++;
                }

                if (towers[i].Readability != null && towers[i].Readability.MotionReady)
                {
                    towers[i].Readability.DebugPlayAttack();
                    _p121FixtureTowerMotionCount++;
                }
            }

            _p121FixtureEnemyAnimationCount = 0;
            _p121FixtureEnemyMotionCount = 0;
            for (var i = 0; i < enemies.Length; i++)
            {
                var visual = enemies[i].transform.Find("Visual");
                var animator = visual != null ? visual.GetComponent<TDSpriteAnimator>() : null;
                if (animator != null && animator.IsConfigured && animator.FrameCount >= 6)
                {
                    _p121FixtureEnemyAnimationCount++;
                }

                if (enemies[i].MotionReady)
                {
                    enemies[i].TakeHit(1, 0f, 0f);
                    _p121FixtureEnemyMotionCount++;
                }
            }

            var feedbackKinds = new[]
            {
                TDBattleFeedbackKind.Hit,
                TDBattleFeedbackKind.ArmorBreak,
                TDBattleFeedbackKind.Slow,
                TDBattleFeedbackKind.Specialization,
                TDBattleFeedbackKind.Resonance,
                TDBattleFeedbackKind.Leak
            };
            var feedbackDetails = new[] { "32", "-7", "40%", "MATRIX", "MATCH", "-2" };
            for (var i = 0; i < feedbackKinds.Length; i++)
            {
                _battlePresentation?.EmitFeedback(
                    feedbackKinds[i],
                    new Vector3(-3.2f + (i * 1.25f), 0.4f, 0f),
                    feedbackDetails[i],
                    i == feedbackKinds.Length - 1 ? TDBattleFeedbackTier.Critical : i == 0 ? TDBattleFeedbackTier.Routine : TDBattleFeedbackTier.Tactical);
            }

            PlaySfxTone("p121_feedback_hit", 720f, 0.07f, 0.44f, true);
            PlaySfxTone("p121_armor_break", 330f, 0.16f, 0.62f, false);
            PlaySfxTone("p121_slow_control", 440f, 0.15f, 0.54f, false);
            PlaySfxTone("p121_specialization", 820f, 0.16f, 0.62f, true);
            PlaySfxTone("p121_resonance", 680f, 0.24f, 0.76f, true);
            PlayCriticalSfxTone("p121_boss_warning", 180f, 0.34f, 0.82f, true);
            PlayCriticalSfxTone("p121_leak", 220f, 0.20f, 0.72f, false);
            SeedP121RunResultAnalytics(towers);
            Canvas.ForceUpdateCanvases();
            return $"p12.1.fixture.ready=True enemies={enemies.Length} towers={towers.Length} audio={_sfxClipCache.Count}\n{baseFixture}";
        }

        public string DebugPrepareP122ExamForTest()
        {
            var baseFixture = DebugPrepareP121ForTest();
            if (_examPresentationProfile == null || _examScenarioDevice == null)
            {
                return $"p12.2.fixture.ready=False level={_campaignRoute?.level?.levelIndex ?? 0} error=exam profile unavailable\n{baseFixture}";
            }

            _campaignDeploymentConfirmed = true;
            _missionBoardOpen = false;
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            PresentExamBeat(TDExamPresentationStage.Opening);
            PresentExamBeat(TDExamPresentationStage.Escalation);
            PresentExamBeat(TDExamPresentationStage.Decision);
            _scenarioOpportunities = Mathf.Max(_scenarioOpportunities, 3);
            var scenarioFixture = DebugActivateP86ScenarioForTest();
            _examScenarioDevice.TriggerActivation();
            UpdateExamScenarioDevice();
            ShowExamBeatVisual(TDExamPresentationStage.Decision);
            Canvas.ForceUpdateCanvases();
            return
                $"p12.2.fixture.ready=True level={_examPresentationProfile.levelIndex} identity={_examPresentationProfile.identityId} stage={_examPresentationStage} " +
                $"device={_examScenarioDevice.IsReady} activations={_examScenarioDevice.ActivationCount}\n" +
                $"p12.2.fixture.beats={_examOpeningBeatCount}/{_examEscalationBeatCount}/{_examDecisionBeatCount}\n" +
                $"{scenarioFixture}\n{baseFixture}";
        }

        private void SeedP121RunResultAnalytics(IReadOnlyList<TDTower> towers)
        {
            _laneStats.Clear();
            _towerStats.Clear();
            _roadSegmentStats.Clear();
            _failureReasonCounts.Clear();
            _cachedRoadHeatReports = null;

            var laneKeys = string.Equals(_campaignRoute?.level?.mapId, "split_switch_canyon", StringComparison.OrdinalIgnoreCase)
                ? new[] { "center", "left", "right", "switch" }
                : new[] { "center", "left", "right", "cross_lane" };
            var spawned = new[] { 16, 15, 12, 10 };
            var kills = new[] { 15, 14, 11, 10 };
            var escapes = new[] { 1, 1, 1, 0 };
            var laneDamage = new[] { 2000, 1600, 1200, 800 };
            for (var i = 0; i < laneKeys.Length; i++)
            {
                var lane = GetOrCreateLaneStat(laneKeys[i]);
                lane.spawned = spawned[i];
                lane.spawnedHealth = laneDamage[i] + 260;
                lane.kills = kills[i];
                lane.escapes = escapes[i];
                lane.damageDealt = laneDamage[i];
                lane.integrityDamageTaken = escapes[i];
                for (var segment = 0; segment < RoadSegmentCount; segment++)
                {
                    var stat = GetOrCreateRoadSegmentStat(laneKeys[i], segment);
                    stat.reached = Mathf.Max(1, spawned[i] - (segment * 2));
                    stat.damageDealt = Mathf.RoundToInt(laneDamage[i] / (float)RoadSegmentCount);
                    stat.kills = segment == RoadSegmentCount - 1 ? kills[i] : 0;
                    stat.escapes = segment == RoadSegmentCount - 1 ? escapes[i] : 0;
                    stat.integrityDamageTaken = segment == RoadSegmentCount - 1 ? escapes[i] : 0;
                    stat.controlApplications = Mathf.Max(0, 8 - segment);
                    stat.counterDamage = Mathf.RoundToInt(stat.damageDealt * 0.72f);
                }
            }

            var damageFixture = new[] { 1450, 1100, 850, 700, 550, 400, 300, 250 };
            var killFixture = new[] { 13, 10, 8, 7, 5, 3, 2, 2 };
            for (var i = 0; i < towers.Count; i++)
            {
                var stat = GetOrCreateTowerStat(towers[i]);
                var fixtureIndex = i % damageFixture.Length;
                stat.damageDealt = damageFixture[fixtureIndex];
                stat.kills = killFixture[fixtureIndex];
                stat.hits = 24 + (fixtureIndex * 3);
                stat.controlApplications = fixtureIndex % 2 == 0 ? 8 + fixtureIndex : 2;
                stat.controlStrengthSeconds = stat.controlApplications * 0.8f;
                stat.counterDamage = Mathf.RoundToInt(stat.damageDealt * (0.48f + (fixtureIndex * 0.04f)));
                stat.upgrades = Mathf.Max(1, towers[i].Tier);
                stat.upgradeSpend = Mathf.RoundToInt(stat.buildCost * 0.72f);
                stat.damageSpecProcs = fixtureIndex % 2 == 0 ? 3 : 0;
                stat.utilitySpecProcs = fixtureIndex % 2 == 1 ? 3 : 0;
                stat.matrixFullMatches = fixtureIndex < 3 ? 2 : 0;
            }

            _totalDamageDealt = damageFixture.Take(Mathf.Min(damageFixture.Length, towers.Count)).Sum();
            _totalKills = kills.Sum();
            _totalEscapes = escapes.Sum();
            _totalIntegrityDamageTaken = _totalEscapes;
            _counterOpportunityDamage = 2600;
            _counterMatchedDamage = 2100;
            _budgetSpentOnBuilds = towers.Sum(tower => TDTower.GetBuildCost(tower.Kind));
            _budgetSpentOnUpgrades = _towerStats.Values.Sum(stat => stat.upgradeSpend);
            _upgradesPurchased = _towerStats.Values.Sum(stat => stat.upgrades);
            _resonanceWindowsTriggered = 4;
            _resonanceCommandsUsed = 4;
            _resonanceMatchedCommands = 3;
            _matrixOpportunities = 8;
            _matrixFullMatches = 6;
            _matrixConvergenceTriggers = 2;
            _resonanceBonusDamage = 520;
            _wavesCleared = Mathf.Max(_wavesCleared, 3);
            _wave = Mathf.Max(_wave, 4);
            _lineIntegrity = Mathf.Max(_lineIntegrity, 12);
            _failureReasonCounts[FailureTagCoverageGap] = 2;
            _failureReasonCounts[FailureTagCounterMismatch] = 1;
        }

        public string DebugAuditP130ForTest()
        {
            var perfectScore = ApplyRunSurvivalScoreCap(100, true, true, 20, 20, 0);
            var strainedScore = ApplyRunSurvivalScoreCap(95, true, true, 28, 10, 18);
            var criticalScore = ApplyRunSurvivalScoreCap(95, true, true, 20, 3, 17);
            var strongScore = ApplyRunSurvivalScoreCap(95, true, true, 29, 25, 4);
            var defeatScore = ApplyRunSurvivalScoreCap(95, true, false, 20, 0, 20);
            var ratingPass = perfectScore == 100 && GetRunScoreGrade(perfectScore) == "S" &&
                             strainedScore == 79 && GetRunScoreGrade(strainedScore) == "B" &&
                             criticalScore == 69 && GetRunScoreGrade(criticalScore) == "C" &&
                             strongScore == 95 && GetRunScoreGrade(strongScore) == "S" &&
                             defeatScore == 59 && GetRunScoreGrade(defeatScore) == "D";

            var cleanReport = new TDRoadHeatReport
            {
                stat = new TDRoadSegmentRuntimeStat
                {
                    laneKey = "audit",
                    segmentIndex = 0,
                    reached = 100
                },
                coverageScore = 100,
                heatScore = 18
            };
            var failingReport = new TDRoadHeatReport
            {
                stat = new TDRoadSegmentRuntimeStat
                {
                    laneKey = "audit",
                    segmentIndex = RoadSegmentCount - 1,
                    reached = 20,
                    escapes = 4
                },
                coverageScore = 50,
                heatScore = 72,
                hasSuggestedCell = true,
                suggestedCell = new Vector2Int(4, 4)
            };
            var auditLanguage = TDLocalization.CurrentLanguage;
            TDLocalization.SetLanguage(TDUiLanguage.English, false);
            var cleanAdvice = BuildHotspotRecommendation(cleanReport);
            var failingAdvice = BuildHotspotRecommendation(failingReport);
            TDLocalization.SetLanguage(auditLanguage, false);
            var currentRecommendations = BuildRunRecommendationLabel()
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var contradictoryCurrentAdvice = currentRecommendations.Any(line =>
                line.IndexOf("add coverage", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (line.IndexOf(", 0 leak/live,", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("C100; add coverage", StringComparison.OrdinalIgnoreCase) >= 0));
            var recommendationPass = currentRecommendations.Length == 3 &&
                                     !contradictoryCurrentAdvice &&
                                     cleanAdvice.IndexOf("coverage sufficient", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                     cleanAdvice.IndexOf("add coverage", StringComparison.OrdinalIgnoreCase) < 0 &&
                                     failingAdvice.IndexOf("add coverage", StringComparison.OrdinalIgnoreCase) >= 0;

            _battlePresentation?.Tick(true);
            var eventFeedSuppressed = _uiEventFeedRoot == null || !_uiEventFeedRoot.gameObject.activeInHierarchy;
            var presentationSuppressed = _battlePresentation != null &&
                                         !_battlePresentation.SignalLayerVisible &&
                                         !_battlePresentation.CinematicVisible &&
                                         _battlePresentation.ActiveSignalCount == 0;
            var modalSuppressionPass = eventFeedSuppressed && presentationSuppressed;
            var pass = ratingPass && recommendationPass && modalSuppressionPass;
            return
                $"p13.0.audit.rating={ratingPass} [perfect={perfectScore}:S,strained={strainedScore}:B,critical={criticalScore}:C,strong={strongScore}:S,defeat={defeatScore}:D]\n" +
                $"p13.0.audit.recommendations={recommendationPass} [count={currentRecommendations.Length},contradiction={contradictoryCurrentAdvice},clean={cleanAdvice},failure={failingAdvice}]\n" +
                $"p13.0.audit.modalSuppression={modalSuppressionPass} [feedHidden={eventFeedSuppressed},signals={_battlePresentation?.ActiveSignalCount ?? -1},signalLayer={_battlePresentation?.SignalLayerVisible ?? true},cinematic={_battlePresentation?.CinematicVisible ?? true}]\n" +
                $"p13.0.audit.pass={pass}\n";
        }

        public string DebugAuditP121ForTest()
        {
            Canvas.ForceUpdateCanvases();
            var towerCount = FindObjectsByType<TDTower>(FindObjectsSortMode.None).Length;
            var animationPass = _p121FixtureEnemyAnimationCount >= 6 &&
                                _p121FixtureTowerAnimationCount >= 8 &&
                                _p121FixtureEnemyMotionCount >= 6 &&
                                _p121FixtureTowerMotionCount >= 8;
            var audioPass = _sfxSource != null && _tacticalSfxSource != null && _criticalSfxSource != null &&
                            _sfxClipCache.Count >= 7;
            var feedbackPass = _battlePresentation != null && _battlePresentation.MaxSignalCharacters <= 14;
            var scoreChartPass = _uiGameOverScoreChartRoot != null &&
                                 _uiGameOverScoreBarFills.Count == 5 &&
                                 _uiGameOverScoreBarFills.All(fill => fill != null && fill.rectTransform.sizeDelta.x > 1f);
            var visibleLaneRows = _uiGameOverLaneBarRows.Count(row => row != null && row.gameObject.activeSelf);
            var visibleTowerRows = _uiGameOverTowerBarRows.Count(row => row != null && row.gameObject.activeSelf);
            var breakdownPass = visibleLaneRows >= 3 && visibleTowerRows >= 3;
            var commandFrames = _battleCanvas == null
                ? Array.Empty<RectTransform>()
                : _battleCanvas.GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect != null && rect.name == "Emberline Command Frame")
                    .ToArray();
            var maximumCornerAspectError = 0f;
            var frameAspectPass = commandFrames.Length >= 3;
            var cornerNames = new[] { "Frame Corner TL", "Frame Corner TR", "Frame Corner BL", "Frame Corner BR" };
            var cornerAspects = new[] { 175f / 78f, 190f / 83f, 180f / 82f, 180f / 88f };
            for (var frameIndex = 0; frameIndex < commandFrames.Length; frameIndex++)
            {
                for (var cornerIndex = 0; cornerIndex < cornerNames.Length; cornerIndex++)
                {
                    var corner = commandFrames[frameIndex].Find(cornerNames[cornerIndex]) as RectTransform;
                    if (corner == null || corner.rect.height <= 0.1f)
                    {
                        frameAspectPass = false;
                        continue;
                    }

                    var actualAspect = corner.rect.width / corner.rect.height;
                    maximumCornerAspectError = Mathf.Max(
                        maximumCornerAspectError,
                        Mathf.Abs(actualAspect - cornerAspects[cornerIndex]));
                }
            }
            frameAspectPass &= maximumCornerAspectError <= 0.015f;
            var overflow = new List<string>();
            if (_uiGameOverRoot != null && _uiGameOverRoot.gameObject.activeInHierarchy)
            {
                var labels = _uiGameOverRoot.GetComponentsInChildren<Text>(true);
                for (var i = 0; i < labels.Length; i++)
                {
                    var label = labels[i];
                    if (label == null || !label.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (label.preferredWidth > label.rectTransform.rect.width + 1.5f &&
                        label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                    {
                        overflow.Add(label.name);
                    }
                }
            }

            var textPass = overflow.Count == 0;
            var pass = animationPass && audioPass && feedbackPass && scoreChartPass && breakdownPass && frameAspectPass && textPass;
            return
                $"p12.1.audit.animation={animationPass} [enemyFrames={_p121FixtureEnemyAnimationCount},towerFrames={_p121FixtureTowerAnimationCount},enemyMotion={_p121FixtureEnemyMotionCount},towerMotion={_p121FixtureTowerMotionCount},liveTowers={towerCount}]\n" +
                $"p12.1.audit.audio={audioPass} [voices={(_sfxSource != null ? 1 : 0) + (_tacticalSfxSource != null ? 1 : 0) + (_criticalSfxSource != null ? 1 : 0)},clips={_sfxClipCache.Count}]\n" +
                $"p12.1.audit.feedbackReduced={feedbackPass} [maxChars={_battlePresentation?.MaxSignalCharacters ?? -1}]\n" +
                $"p12.1.audit.scoreChart={scoreChartPass} [bars={_uiGameOverScoreBarFills.Count}]\n" +
                $"p12.1.audit.breakdowns={breakdownPass} [lanes={visibleLaneRows},towers={visibleTowerRows}]\n" +
                $"p12.1.audit.frameAspect={frameAspectPass} [frames={commandFrames.Length},maxError={maximumCornerAspectError:0.000}]\n" +
                $"p12.1.audit.textOverflow={(textPass ? "none" : string.Join(",", overflow))}\n" +
                $"p12.1.audit.pass={pass}\n";
        }

        public string DebugAuditP122ForTest()
        {
            Canvas.ForceUpdateCanvases();
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 0;
            var expectedTypes = new Dictionary<int, string>
            {
                [5] = "timed_reinforcement",
                [9] = "route_switch",
                [13] = "environment_device",
                [17] = "boss_phase",
                [20] = "boss_phase"
            };
            var profiles = new List<TDExamPresentationProfile>();
            foreach (var examLevel in new[] { 5, 9, 13, 17, 20 })
            {
                if (TDExamPresentationCatalog.TryGet(examLevel, out var profile))
                {
                    profiles.Add(profile);
                }
            }

            var catalogPass = profiles.Count == 5 &&
                              profiles.Select(profile => profile.identityId).Distinct(StringComparer.Ordinal).Count() == 5 &&
                              profiles.Select(profile => profile.openingTitle).Distinct(StringComparer.Ordinal).Count() == 5 &&
                              profiles.Select(profile => profile.failureSignature).Distinct(StringComparer.Ordinal).Count() == 5 &&
                              profiles.Select(profile => profile.victoryEnding).Distinct(StringComparer.Ordinal).Count() == 5;
            var currentProfilePass = expectedTypes.TryGetValue(levelIndex, out var expectedType) &&
                                     _examPresentationProfile != null &&
                                     _examPresentationProfile.levelIndex == levelIndex &&
                                     NormalizeGroupToken(_activeScenarioMechanic?.mechanicType) == expectedType;
            var deviceClearance = _gridMap != null && _examPresentationProfile != null
                ? _gridMap.GetRoadClearance(_examPresentationProfile.deviceCell)
                : 0f;
            var placementPass = levelIndex == 9
                ? deviceClearance <= 0.60f
                : deviceClearance >= 0.42f;
            var devicePass = _examScenarioDevice != null && _examScenarioDevice.IsReady &&
                             _examScenarioDevice.VisibleRendererCount >= 5 &&
                             _examScenarioDevice.ActivationCount >= 1 && placementPass;
            var beatsPass = _examPresentationStage >= TDExamPresentationStage.Decision &&
                            _examOpeningBeatCount == 1 &&
                            _examEscalationBeatCount == 1 &&
                            _examDecisionBeatCount == 1;
            var localizedVictoryEnding = TDLocalization.LocalizeRuntimeString(_examPresentationProfile?.victoryEnding ?? string.Empty);
            var localizedDefeatEnding = TDLocalization.LocalizeRuntimeString(_examPresentationProfile?.defeatEnding ?? string.Empty);
            var localizedFailureSignature = TDLocalization.LocalizeRuntimeString(_examPresentationProfile?.failureSignature ?? string.Empty);
            var resultIdentityPass = _uiGameOverRoot != null && _uiGameOverRoot.gameObject.activeInHierarchy &&
                                     _uiGameOverTitleText != null && _uiGameOverFailureText != null &&
                                     (_uiGameOverTitleText.text.Contains(localizedVictoryEnding) ||
                                      _uiGameOverTitleText.text.Contains(localizedDefeatEnding)) &&
                                     (_victory || _uiGameOverFailureText.text.Contains(localizedFailureSignature));

            var matrix = TDBalanceSimulator.RunMatrix();
            var examSummaries = matrix.examSummaries ?? Array.Empty<TDBalanceExamSummary>();
            var strategiesPass = examSummaries.Length == 5 && examSummaries.All(summary =>
                summary.strategyCount == 3 &&
                summary.standardVictories >= 3 &&
                summary.distinctSuccessfulSignatures >= 3);
            var overflow = CollectTextOverflowNames(_uiGameOverRoot);
            var textPass = overflow.Count == 0;
            var pass = catalogPass && currentProfilePass && devicePass && beatsPass && resultIdentityPass && strategiesPass && textPass;
            return
                $"p12.2.audit.catalog={catalogPass} [profiles={profiles.Count},identities={profiles.Select(profile => profile.identityId).Distinct().Count()}]\n" +
                $"p12.2.audit.currentProfile={currentProfilePass} [level={levelIndex},identity={_examPresentationProfile?.identityId ?? "none"},type={_activeScenarioMechanic?.mechanicType ?? "none"}]\n" +
                $"p12.2.audit.device={devicePass} [ready={_examScenarioDevice?.IsReady ?? false},renderers={_examScenarioDevice?.VisibleRendererCount ?? 0},activations={_examScenarioDevice?.ActivationCount ?? 0},clearance={deviceClearance:0.00}]\n" +
                $"p12.2.audit.beats={beatsPass} [opening={_examOpeningBeatCount},escalation={_examEscalationBeatCount},decision={_examDecisionBeatCount},stage={_examPresentationStage}]\n" +
                $"p12.2.audit.resultIdentity={resultIdentityPass}\n" +
                $"p12.2.audit.strategies={strategiesPass} [exams={examSummaries.Length},tripleWins={examSummaries.Count(summary => summary.standardVictories >= 3 && summary.distinctSuccessfulSignatures >= 3)}]\n" +
                $"p12.2.audit.textOverflow={(textPass ? "none" : string.Join(",", overflow))}\n" +
                $"p12.2.audit.pass={pass}\n";
        }

        public string DebugPrepareP123ForTest(bool chinese, bool openSettings)
        {
            return DebugPrepareP123ForTest(chinese, openSettings ? "settings" : "campaign");
        }

        public string DebugPrepareP123ForTest(bool chinese, string surface)
        {
            SetUiLanguage(chinese ? TDUiLanguage.SimplifiedChinese : TDUiLanguage.English);
            var surfaceToken = NormalizeGroupToken(surface);
            _missionBoardSelectedLevel = _campaignRoute?.level?.levelIndex ?? DefaultCampaignLevelIndex;
            _missionBoardSelectedChapter = Mathf.Clamp((_missionBoardSelectedLevel - 1) / 5, 0, 3);
            _missionBoardOpen = surfaceToken != "settings";
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            _missionBoardNeedsRefresh = true;
            UpdateMissionBoardUi();
            _settingsPanel?.Close();
            if (surfaceToken == "settings")
            {
                _settingsPanel?.Open();
            }
            else if (surfaceToken == "formation")
            {
                OpenFormationPanel();
                UpdateMissionBoardUi();
            }
            else if (surfaceToken == "profile")
            {
                OpenCampaignProfile();
                UpdateMissionBoardUi();
            }

            Canvas.ForceUpdateCanvases();
            return $"p12.3.fixture.ready={_settingsPanel?.IsInitialized ?? false}\n" +
                   $"p12.3.fixture.language={TDLocalization.CurrentLanguage}\n" +
                   $"p12.3.fixture.surface={surfaceToken}";
        }

        public string DebugAuditP123ForTest()
        {
            var originalLanguage = TDLocalization.CurrentLanguage;
            TDLocalization.SetLanguage(TDUiLanguage.SimplifiedChinese, false);
            var localizedSample = TDLocalization.LocalizeRuntimeString("CAMPAIGN COMMAND / START WAVE / Grayline Junction");
            var chineseFont = TDLocalization.ResolveFont(_uiFont);
            var localizationPass = localizedSample.Contains("战役指挥") && localizedSample.Contains("开始波次") &&
                                   localizedSample.Contains("灰线枢纽");
            var fontPass = chineseFont != null && chineseFont.HasCharacter('战') &&
                           Resources.Load<Font>(TDLocalization.ChineseFontPath) != null;
            TDLocalization.SetLanguage(originalLanguage, false);
            TDLocalization.RefreshLabels(_battleCanvas?.gameObject, _uiFont);
            UpdateBattleUi();
            Canvas.ForceUpdateCanvases();

            var activeChapterTabs = _uiMissionChapterButtons.Count(button => button != null && button.gameObject.activeSelf);
            var activeChapterProgress = _uiMissionChapterProgressTexts.Count(label => label != null && label.gameObject.activeSelf);
            var activeLevelNodes = _uiMissionLevelButtons.Count(button => button != null && button.gameObject.activeSelf);
            var activeRewardButtons = _uiMissionChapterRewardButtons.Count(button => button != null && button.gameObject.activeSelf);
            var campaignSurfacePass = _uiMissionChapterButtons.Count == 4 && activeChapterTabs == 4 &&
                                      activeChapterProgress == 1 && activeLevelNodes == 5 && activeRewardButtons == 1;

            var selectables = _battleCanvas != null ? _battleCanvas.GetComponentsInChildren<Selectable>(true) : Array.Empty<Selectable>();
            var focusVisuals = _battleCanvas != null ? _battleCanvas.GetComponentsInChildren<TDUiFocusVisual>(true) : Array.Empty<TDUiFocusVisual>();
            var uiInputModule = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            var uiActionsPass = uiInputModule != null && uiInputModule.move?.action != null &&
                                uiInputModule.submit?.action != null && uiInputModule.cancel?.action != null;
            var focusPass = EventSystem.current != null && uiActionsPass &&
                            selectables.Length > 0 && focusVisuals.Length == selectables.Length;
            var inputPass = TDInputBindings.RebindableActions.Count == 6 &&
                            TDInputBindings.GetKey(TDInputAction.StartWave) != KeyCode.None &&
                            TDInputBindings.GetKey(TDInputAction.Settings) != KeyCode.None;
            var accessibilityPass = _settingsPanel != null && _settingsPanel.IsInitialized &&
                                    _battlePresentation != null && _colorblindMarkersEnabled == _battlePresentation.MarkersEnabled &&
                                    _largeTextEnabled == _battlePresentation.LargeTextEnabled;
            var overflow = CollectTextOverflowNames(_missionBoardOpen ? _uiMissionBoardRoot : null);
            var textPass = overflow.Count == 0;
            var resolutionPass = Screen.width >= 960 && Screen.height >= 540;
            var skinReport = TDUiWorldSkin.BuildAuditReport(_battleCanvas?.gameObject, out var skinPass);
            var pass = localizationPass && fontPass && campaignSurfacePass && focusPass && inputPass &&
                       accessibilityPass && textPass && resolutionPass && skinPass;
            return
                $"p12.3.audit.localization={localizationPass} [sample={localizedSample}]\n" +
                $"p12.3.audit.font={fontPass} [font={chineseFont?.name ?? "none"}]\n" +
                $"p12.3.audit.campaignSurface={campaignSurfacePass} [tabs={activeChapterTabs}/4,progress={activeChapterProgress},nodes={activeLevelNodes},reward={activeRewardButtons}]\n" +
                $"p12.3.audit.focus={focusPass} [selectables={selectables.Length},visuals={focusVisuals.Length},uiActions={uiActionsPass}]\n" +
                $"p12.3.audit.input={inputPass} [bindings={TDInputBindings.RebindableActions.Count},gamepad={TDInputCompat.HasGamepad}]\n" +
                $"p12.3.audit.accessibility={accessibilityPass} [markers={_colorblindMarkersEnabled},largeText={_largeTextEnabled},subtitles={_subtitlesEnabled},captions={_captionsEnabled}]\n" +
                $"p12.3.audit.resolution={resolutionPass} [{Screen.width}x{Screen.height},scale={_uiScale:0.0},effective={GetEffectiveUiScale():0.00}]\n" +
                $"p12.3.audit.textOverflow={(textPass ? "none" : string.Join(",", overflow))}\n" +
                skinReport + "\n" +
                (_settingsPanel != null ? _settingsPanel.BuildAuditReport() + "\n" : string.Empty) +
                $"p12.3.audit.pass={pass}\n";
        }

        private static List<string> CollectTextOverflowNames(RectTransform root)
        {
            var overflow = new List<string>();
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                return overflow;
            }

            var labels = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null || !label.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (label.preferredWidth > label.rectTransform.rect.width + 1.5f &&
                    label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                {
                    var sample = (label.text ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " | ");
                    if (sample.Length > 48)
                    {
                        sample = sample.Substring(0, 48) + "...";
                    }

                    var parentName = label.transform.parent != null ? label.transform.parent.name : "root";
                    overflow.Add($"{parentName}/{label.name}:{sample}");
                }
            }

            return overflow;
        }

        public string DebugPrepareP101MetaForTest()
        {
            if (_campaign?.metaProgression == null)
            {
                return "p10.1.fixture.ready=False";
            }

            var levelsToRate = Mathf.Min(10, _campaign.totalLevels);
            for (var level = 1; level <= levelsToRate; level++)
            {
                TDCampaignProgression.RecordResult(level, true, 3, 90, 20, _campaign.totalLevels, true);
            }

            foreach (var entry in _globalEnemyCatalog.Values.Take(4))
            {
                TDCampaignProgression.RecordEnemyObservation(entry.enemyId, GetRequiredEnemyDossierFlags(entry));
            }

            var requiredTowerFlags = (int)(TDTowerCodexObservation.Built | TDTowerCodexObservation.DamageBranch |
                                           TDTowerCodexObservation.UtilityBranch | TDTowerCodexObservation.SpecializationProc);
            foreach (var kind in TDTower.GetBuildOrder().Take(4))
            {
                TDCampaignProgression.RecordTowerObservation(TDTower.GetTowerId(kind), requiredTowerFlags);
            }

            RefreshMetaProgressionRewards(false);
            var currentLevel = _campaignRoute?.level?.levelIndex ?? 1;
            TDCampaignProgression.SaveTacticalProtocol(currentLevel, "field_control");
            _activeTacticalProtocol = GetTacticalProtocol("field_control");
            ResetMissionRuntimeRules();
            ConfigureScenarioMechanic(_campaignRoute?.map?.mechanic, _campaignRoute?.level?.scenario);
            ApplyMissionRuntimeRules(_campaignRoute?.level);
            _missionBoardOpen = true;
            _campaignProfileOpen = false;
            _missionBoardSelectedLevel = currentLevel;
            OpenFormationPanel();
            _formationDraftProtocolId = ResolveAvailableProtocolId("field_control");
            RefreshFormationPanelUi();
            Canvas.ForceUpdateCanvases();
            return "p10.1.fixture.ready=True\n" + DebugGetP101MetaReport();
        }

        public string DebugGetP101MetaReport()
        {
            var meta = _campaign?.metaProgression;
            var protocols = meta?.tacticalProtocols ?? Array.Empty<TDCampaignTacticalProtocolDefinition>();
            var rewards = (meta?.ratingRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>())
                .Concat(meta?.codexRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>())
                .ToArray();
            var summary = GetCampaignProgressSummary();
            return
                $"p10.1.config.protocols={protocols.Length}\n" +
                $"p10.1.config.rewards={rewards.Length}\n" +
                $"p10.1.progress.stars={summary.earnedStars}/{summary.availableStars}\n" +
                $"p10.1.progress.enemyDossiers={GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}\n" +
                $"p10.1.progress.towerDossiers={GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}\n" +
                $"p10.1.progress.metaRewards={TDCampaignProgression.GetClaimedMetaRewardIds().Length}\n" +
                $"p10.1.progress.unlockedProtocols={TDCampaignProgression.GetUnlockedProtocolIds().Length + 1}\n" +
                $"p10.1.active.protocol={_activeTacticalProtocol?.protocolId ?? "baseline"}\n" +
                $"p10.1.runtime=budget:{_startingDefenseBudget},prep+:{_missionPrepSecondsBonus},hpX:{_missionEnemyHpMultiplier:0.##},rewardX:{_missionRewardMultiplier:0.##},commandCharges:{_scenarioCharges},commandCostX:{_scenarioCostMultiplier:0.##}";
        }

        public string DebugAuditP101ForTest()
        {
            if (_campaign?.metaProgression == null)
            {
                return "p10.1.audit.content=False\np10.1.audit.pass=False\n";
            }

            var meta = _campaign.metaProgression;
            var protocols = meta.tacticalProtocols ?? Array.Empty<TDCampaignTacticalProtocolDefinition>();
            var rewards = (meta.ratingRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>())
                .Concat(meta.codexRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>()).ToArray();
            var protocolIds = new HashSet<string>(protocols.Select(item => item.protocolId), StringComparer.OrdinalIgnoreCase);
            var contentPass = protocols.Length == 5 && rewards.Length == 4 && protocolIds.Count == protocols.Length &&
                              rewards.All(reward => !string.IsNullOrWhiteSpace(reward.sourceType) &&
                                                    reward.threshold > 0 && protocolIds.Contains(reward.unlockProtocolId));
            var sidegradePass = protocols.Where(item => !string.Equals(item.protocolId, "baseline", StringComparison.OrdinalIgnoreCase))
                .All(item =>
                {
                    var benefit = item.startingBudgetDelta > 0 || item.prepSecondsDelta > 0 ||
                                  item.scenarioChargeDelta > 0 || item.rewardMultiplier > 1f;
                    var cost = item.startingBudgetDelta < 0 || item.enemyHpMultiplier > 1f || item.scenarioCostMultiplier > 1f;
                    return benefit && cost;
                });
            var signatures = protocols.Select(item =>
                    $"{item.startingBudgetDelta}:{item.prepSecondsDelta}:{item.scenarioChargeDelta}:{item.enemyHpMultiplier:0.###}:{item.rewardMultiplier:0.###}:{item.scenarioCostMultiplier:0.###}")
                .Distinct().Count();
            var distinctRuntimePass = signatures == protocols.Length;
            var originalBudget = _startingDefenseBudget;
            var originalHp = _missionEnemyHpMultiplier;
            var originalReward = _missionRewardMultiplier;
            var originalPrep = _missionPrepSecondsBonus;
            var originalScenarioCost = _scenarioCostMultiplier;
            var originalScenarioCharges = _scenarioCharges;
            var runtimeApplicationPass = false;
            try
            {
                _startingDefenseBudget = 100;
                _missionEnemyHpMultiplier = 1f;
                _missionRewardMultiplier = 1f;
                _missionPrepSecondsBonus = 0;
                _scenarioCostMultiplier = 1f;
                _scenarioCharges = 3;
                ApplyTacticalProtocolEffects(GetTacticalProtocol("forward_recon"));
                var reconPass = _startingDefenseBudget == 92 && _missionPrepSecondsBonus == 4;

                _startingDefenseBudget = 100;
                _missionEnemyHpMultiplier = 1f;
                _missionRewardMultiplier = 1f;
                _missionPrepSecondsBonus = 0;
                _scenarioCostMultiplier = 1f;
                _scenarioCharges = 3;
                ApplyTacticalProtocolEffects(GetTacticalProtocol("salvage_mandate"));
                var salvagePass = Mathf.Approximately(_missionEnemyHpMultiplier, 1.06f) &&
                                  Mathf.Approximately(_missionRewardMultiplier, 1.12f);

                _startingDefenseBudget = 100;
                _missionEnemyHpMultiplier = 1f;
                _missionRewardMultiplier = 1f;
                _missionPrepSecondsBonus = 0;
                _scenarioCostMultiplier = 1f;
                _scenarioCharges = 3;
                ApplyTacticalProtocolEffects(GetTacticalProtocol("field_control"));
                var controlPass = _scenarioCharges == 4 && Mathf.Approximately(_scenarioCostMultiplier, 1.25f);

                _startingDefenseBudget = 100;
                _missionEnemyHpMultiplier = 1f;
                _missionRewardMultiplier = 1f;
                _missionPrepSecondsBonus = 0;
                _scenarioCostMultiplier = 1f;
                _scenarioCharges = 3;
                ApplyTacticalProtocolEffects(GetTacticalProtocol("modular_reserve"));
                var reservePass = _startingDefenseBudget == 112 && Mathf.Approximately(_missionEnemyHpMultiplier, 1.08f);
                runtimeApplicationPass = reconPass && salvagePass && controlPass && reservePass;
            }
            finally
            {
                _startingDefenseBudget = originalBudget;
                _missionEnemyHpMultiplier = originalHp;
                _missionRewardMultiplier = originalReward;
                _missionPrepSecondsBonus = originalPrep;
                _scenarioCostMultiplier = originalScenarioCost;
                _scenarioCharges = originalScenarioCharges;
            }
            var exams = (_campaign.levels ?? Array.Empty<TDCampaignLevelDefinition>())
                .Count(level => level?.scenario?.milestoneExam == true);
            var replayPlansPass = exams == 5 && protocols.Length >= 3;
            var importWhitelistPass = ArePortableMetaIdsKnown(
                                          rewards.Select(reward => reward.rewardId),
                                          protocols.Select(protocol => protocol.protocolId)) &&
                                      !ArePortableMetaIdsKnown(new[] { "unknown_meta_reward" }, new[] { "forward_recon" }) &&
                                      !ArePortableMetaIdsKnown(Array.Empty<string>(), new[] { "unknown_protocol" });

            var originalSnapshot = TDCampaignProgression.ExportSnapshot(_campaign.totalLevels);
            var duplicateClaimPass = false;
            var observationOrPass = false;
            var roundTripPass = false;
            var cloudMergePass = false;
            try
            {
                TDCampaignProgression.ResetProgress(_campaign.totalLevels);
                var firstClaim = TDCampaignProgression.ClaimMetaReward("p101_fixture_a", "forward_recon");
                var secondClaim = TDCampaignProgression.ClaimMetaReward("p101_fixture_a", "forward_recon");
                duplicateClaimPass = firstClaim && !secondClaim;
                TDCampaignProgression.RecordEnemyObservation("p101_enemy", (int)TDEnemyCodexObservation.Sighted);
                TDCampaignProgression.RecordEnemyObservation("p101_enemy", (int)TDEnemyCodexObservation.Slowed);
                observationOrPass = TDCampaignProgression.GetEnemyObservationFlags("p101_enemy") ==
                                    (int)(TDEnemyCodexObservation.Sighted | TDEnemyCodexObservation.Slowed);
                TDCampaignProgression.RecordTowerObservation("rail_lancer", (int)TDTowerCodexObservation.Built);
                TDCampaignProgression.SaveTacticalProtocol(1, "forward_recon");
                var fixtureSnapshot = TDCampaignProgression.ExportSnapshot(_campaign.totalLevels);
                TDCampaignProgression.ResetProgress(_campaign.totalLevels);
                TDCampaignProgression.ImportSnapshot(fixtureSnapshot, _campaign.totalLevels);
                roundTripPass = TDCampaignProgression.IsProtocolUnlocked("forward_recon") &&
                                TDCampaignProgression.GetTacticalProtocol(1) == "forward_recon" &&
                                TDCampaignProgression.GetEnemyObservationFlags("p101_enemy") == 5 &&
                                TDCampaignProgression.GetTowerObservationFlags("rail_lancer") == 1;

                var cloud = TDCampaignProgression.ExportCloudEnvelope(_campaign.totalLevels);
                TDCampaignProgression.RecordEnemyObservation("p101_enemy", (int)TDEnemyCodexObservation.Leaked);
                TDCampaignProgression.RecordTowerObservation("frost_coil", (int)TDTowerCodexObservation.UtilityBranch);
                TDCampaignProgression.ClaimMetaReward("p101_fixture_b", "salvage_mandate");
                TDCampaignProgression.SaveTacticalProtocol(1, "salvage_mandate");
                var merged = TDCampaignProgression.TryResolveCloudEnvelope(
                    cloud,
                    _campaign.totalLevels,
                    TDCampaignCloudConflictResolution.Merge,
                    out _,
                    out _);
                cloudMergePass = merged && TDCampaignProgression.IsProtocolUnlocked("forward_recon") &&
                                 TDCampaignProgression.IsProtocolUnlocked("salvage_mandate") &&
                                 TDCampaignProgression.GetEnemyObservationFlags("p101_enemy") == 13 &&
                                 TDCampaignProgression.GetTowerObservationFlags("rail_lancer") == 1 &&
                                 TDCampaignProgression.GetTowerObservationFlags("frost_coil") == 4 &&
                                 TDCampaignProgression.GetTacticalProtocol(1) == "salvage_mandate";
            }
            finally
            {
                TDCampaignProgression.ImportSnapshot(originalSnapshot, _campaign.totalLevels);
            }

            var uiPass = _uiFormationProtocolButtons.Count == protocols.Length &&
                         _uiFormationProtocolButtonTexts.Count == protocols.Length &&
                         _uiCampaignProfileBonusText != null && _uiCampaignProfileChapterText != null;
            var textOverflow = new List<string>();
            Canvas.ForceUpdateCanvases();
            foreach (var text in _uiFormationProtocolButtonTexts.Concat(new[] { _uiFormationDifficultyText, _uiCampaignProfileBonusText }))
            {
                if (text == null)
                {
                    uiPass = false;
                    continue;
                }

                var rect = text.rectTransform.rect;
                if (text.preferredHeight > rect.height + 2f ||
                    text.horizontalOverflow == HorizontalWrapMode.Overflow && text.preferredWidth > rect.width + 2f)
                {
                    textOverflow.Add(text.name);
                }
            }

            var textFitPass = textOverflow.Count == 0;
            var archivePass = BuildCampaignChapterArchiveLabel().Contains("EXAM SIGNATURE") &&
                              BuildCampaignRewardBonusLabel().Contains("TACTICAL PROTOCOLS");
            var pass = contentPass && sidegradePass && distinctRuntimePass && runtimeApplicationPass && replayPlansPass && importWhitelistPass &&
                       duplicateClaimPass && observationOrPass && roundTripPass && cloudMergePass &&
                       uiPass && textFitPass && archivePass;
            return
                $"p10.1.audit.content={contentPass}\n" +
                $"p10.1.audit.sidegrades={sidegradePass}\n" +
                $"p10.1.audit.runtimeSignatures={distinctRuntimePass}\n" +
                $"p10.1.audit.runtimeApplication={runtimeApplicationPass}\n" +
                $"p10.1.audit.examReplayPlans={replayPlansPass}\n" +
                $"p10.1.audit.importWhitelist={importWhitelistPass}\n" +
                $"p10.1.audit.duplicateClaim={duplicateClaimPass}\n" +
                $"p10.1.audit.observationOr={observationOrPass}\n" +
                $"p10.1.audit.snapshotRoundTrip={roundTripPass}\n" +
                $"p10.1.audit.cloudMerge={cloudMergePass}\n" +
                $"p10.1.audit.archive={archivePass}\n" +
                $"p10.1.audit.ui={uiPass}\n" +
                $"p10.1.audit.textOverflow={(textFitPass ? "none" : string.Join(",", textOverflow))}\n" +
                $"p10.1.audit.pass={pass}\n";
        }

        public string DebugAuditP102ForTest()
        {
            return TDBalanceSimulator.BuildAuditText(TDBalanceSimulator.RunMatrix());
        }

        public string DebugUpgradeTowerAtCell(int x, int y, TDTowerUpgradeBranch branch)
        {
            var towerTransform = transform.Find($"Tower_{x}_{y}");
            var tower = towerTransform != null ? towerTransform.GetComponent<TDTower>() : null;
            if (tower == null)
            {
                return $"skip: no tower at {x},{y}";
            }

            var tierBefore = tower.Tier;
            var budgetBefore = _defenseBudget;
            TryUpgradeTower(tower, branch);
            if (tower.Tier <= tierBefore)
            {
                return $"skip: upgrade rejected at {x},{y} branch={branch} tier={tierBefore} budget={_defenseBudget}";
            }

            return $"upgraded Tower_{x}_{y} branch={branch} tier={tower.Tier} spec={tower.SpecializationLabel} cost={budgetBefore - _defenseBudget} budget={_defenseBudget}";
        }

        public string DebugActivateResonanceCommand(string commandName)
        {
            if (!_isResonanceSystemEnabled)
            {
                return "skip: resonance disabled";
            }

            if (!Enum.TryParse(commandName, true, out TDResonanceCommand command) || command == TDResonanceCommand.None)
            {
                return $"skip: unknown resonance command {commandName}";
            }

            _resonanceCharge = ResonanceChargeMax;
            BeginResonanceWindow();
            TrySelectResonanceCommand(command);
            return _activeResonanceCommand == command
                ? $"resonanceCommand={command} window={_resonanceWindowTimer:0.0}s"
                : $"skip: resonance command {command} rejected";
        }

        public string DebugSpawnEnemyForTest(
            string enemyId,
            int count,
            string laneKey = "default",
            float routeProgress01 = 0.30f,
            float healthMultiplier = 8f)
        {
            if (_gameOver)
            {
                return "skip: game over";
            }

            if (string.IsNullOrWhiteSpace(enemyId) || !_enemyCatalog.TryGetValue(enemyId.Trim(), out var sourceEntry))
            {
                return $"skip: unknown enemy {enemyId}";
            }

            var spawnCount = Mathf.Clamp(count, 1, 64);
            var resolvedLane = ResolveExistingLaneKey(laneKey);
            var sourcePath = GetSpawnPathForLane(resolvedLane);
            var progress = Mathf.Clamp(routeProgress01, 0f, 0.90f);
            var testPath = BuildRemainingPathFromNormalizedProgress(sourcePath, progress);
            var testEntry = CloneEnemyEntry(sourceEntry);
            testEntry.hp = Mathf.Max(1, Mathf.RoundToInt(testEntry.hp * Mathf.Clamp(healthMultiplier, 1f, 50f)));
            testEntry.rewardGold = 0;

            for (var i = 0; i < spawnCount; i++)
            {
                _runtimeSpawnIndex++;
                SpawnEnemy(testEntry, testPath, _wave, 20000 + _runtimeSpawnIndex, resolvedLane, false);
            }

            return $"spawned enemy={testEntry.enemyId} count={spawnCount} lane={resolvedLane} progress={progress:0.00} hp={testEntry.hp}";
        }

        public string DebugResetCodexDiscoveries()
        {
            var removed = 0;
            foreach (var pair in _enemyCatalog)
            {
                var key = BuildCodexPlayerPrefsKey(pair.Key);
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                    removed++;
                }
            }

            TDCampaignProgression.ResetCodexObservations();
            PlayerPrefs.Save();
            _encounteredEnemyIds.Clear();
            _codexDiscoveriesThisRun = 0;
            SetStatus("Codex discoveries reset.");
            return $"codex reset removed={removed}";
        }

        private bool IsBuildWindowOpen()
        {
            if (AllowBuildAndUpgradeDuringCombat)
            {
                return true;
            }

            if (_waveSet == null)
            {
                return true;
            }

            return _wave == 0 || _isInPrepPhase;
        }

        private void UpdateBuildPreviewUnderCursor()
        {
            if (_p133FixtureActive)
            {
                return;
            }

            var previousHoveredTower = _hoveredTower;
            _hoveredTower = null;
            if (previousHoveredTower != null)
            {
                previousHoveredTower.Readability?.SetInteractionState(
                    false,
                    previousHoveredTower == _selectedTowerForUi);
            }

            if (_mainCamera == null || _gridMap == null)
            {
                HideRangePreview();
                return;
            }

            if (IsPointerOverBattleUi())
            {
                _gridMap.HideBuildPreview();
                HideRangePreview();
                return;
            }

            var mouse = TDInputCompat.MousePosition;
            mouse.z = -_mainCamera.transform.position.z;
            var world = _mainCamera.ScreenToWorldPoint(mouse);
            world.z = 0f;

            if (TryGetTowerUnderCursor(world, out var tower))
            {
                if (_hoveredTower != tower && _lastHoverSfxTower != tower)
                {
                    _lastHoverSfxTower = tower;
                    PlaySfxTone("ui_hover", 620f, 0.045f, 0.22f, true);
                }

                _hoveredTower = tower;
                tower.Readability?.SetInteractionState(true, tower == _selectedTowerForUi);
                UpdateTowerTooltip(tower);
                _gridMap.HideBuildPreview();
                if (tower == _selectedTowerForUi)
                {
                    ShowRangePreview(
                        tower.transform.position,
                        tower.AttackRange,
                        new Color(1f, 0.68f, 0.28f, 0.38f));
                }
                else
                {
                    HideRangePreview();
                }
                return;
            }

            if (!IsBuildWindowOpen() || !IsTowerUnlocked(_selectedTowerKind))
            {
                _gridMap.HideBuildPreview();
                HideRangePreview();
                return;
            }

            _gridMap.UpdateBuildPreview(world);
            if (_gridMap.TryWorldToCell(world, out var cell) && _gridMap.IsBuildable(cell))
            {
                ShowRangePreview(
                    _gridMap.CellToBuildWorld(cell),
                    TDTower.GetBaseRange(_selectedTowerKind),
                    new Color(0.42f, 0.86f, 0.66f, 0.26f));
                return;
            }

            HideRangePreview();
            UpdateTowerTooltip(null);
        }

        private void UpdateTowerTooltip(TDTower tower)
        {
            if (_towerTooltip == null && tower != null && _battleCanvas != null)
            {
                _towerTooltip = TDTowerTooltip.Create(_battleCanvas.transform);
            }

            if (_towerTooltip != null)
            {
                if (tower != null)
                {
                    _towerTooltip.HoverTower(tower);
                }
                else
                {
                    _towerTooltip.ClearHover();
                }
            }
        }

        private void EnsureRangePreview()
        {
            if (_rangePreviewRenderer != null)
            {
                return;
            }

            var root = new GameObject("RangePreview");
            root.transform.SetParent(transform, false);
            root.transform.position = Vector3.zero;
            _rangePreviewRoot = root.transform;

            _rangePreviewRenderer = root.AddComponent<SpriteRenderer>();
            _rangePreviewRenderer.sprite = GetOrCreateRangePreviewSprite();
            _rangePreviewRenderer.sortingOrder = TDWorldVisualOrder.RangePreview;
            _rangePreviewRenderer.enabled = false;
        }

        private void ShowRangePreview(Vector3 center, float radius, Color color)
        {
            if (radius <= 0f)
            {
                HideRangePreview();
                return;
            }

            EnsureRangePreview();
            if (_rangePreviewRoot == null || _rangePreviewRenderer == null)
            {
                return;
            }

            _rangePreviewRoot.position = center;
            _rangePreviewRoot.localScale = Vector3.one * radius;
            _rangePreviewRenderer.color = color;
            _rangePreviewRenderer.enabled = true;
        }

        private void HideRangePreview()
        {
            if (_rangePreviewRenderer != null)
            {
                _rangePreviewRenderer.enabled = false;
            }
        }

        private Sprite GetOrCreateRangePreviewSprite()
        {
            if (_rangePreviewSprite != null)
            {
                return _rangePreviewSprite;
            }

            const int size = 256;
            const float center = (size - 1) * 0.5f;
            const float outerRadius = 121f;
            const float ringHalfWidth = 0.55f;
            const float featherWidth = 0.75f;
            const float segmentCount = 32f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var ringDistance = Mathf.Abs(distance - outerRadius);
                    var ringAlpha = 1f - Mathf.Clamp01((ringDistance - ringHalfWidth) / featherWidth);
                    var angle01 = Mathf.Repeat((Mathf.Atan2(dy, dx) + Mathf.PI) / (Mathf.PI * 2f), 1f);
                    var dashPhase = Mathf.Repeat(angle01 * segmentCount, 1f);
                    var dashAlpha = dashPhase <= 0.52f ? 1f : 0.12f;
                    var alpha = ringAlpha * dashAlpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _rangePreviewSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
            return _rangePreviewSprite;
        }
    }
}

