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
        // Run baselines live in TDBalanceConfig (value externalization);
        // constants remain for field initializer defaults, but the runtime
        // source of truth is the config asset.
        private const int DefaultDefenseBudget = 120;
        private const int DefaultLineIntegrity = 20;
        private static int ConfigDefaultDefenseBudget => TDBalanceConfig.Instance.defaultDefenseBudget;
        private static int ConfigDefaultLineIntegrity => TDBalanceConfig.Instance.defaultLineIntegrity;
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
            KeyCode.Alpha8,
            // Expansion batch 1 (SlagBurner/SalvageDerrick/RailBarricade/
            // LongRailCannon). Formation caps at 4 kinds per run, so keys
            // beyond 4 are capacity, not a normal-play binding.
            KeyCode.Alpha9,
            KeyCode.Alpha0,
            KeyCode.Q,
            KeyCode.E
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
            new(0, 5), new(1, 4), new(2, 4), new(3, 4), new(4, 4),
            new(5, 4), new(6, 4), new(6, 3), new(7, 3), new(8, 3),
            new(9, 2), new(10, 2), new(11, 2), new(12, 2), new(13, 3),
            new(13, 4), new(14, 4), new(15, 4)
        };

        // Road path constants mirror the art pipeline's single source of truth:
        // grayline follows build_campaign_map_guides.py MAP_PATHS, then was
        // pixel-corrected against the final painted surface (the AI art drifts
        // from the guide on the right half: upper straight sits at row ~2.6,
        // the climb starts at x~8.5, and x13-14.5 descends diagonally); the
        // other maps follow td_layout_data.py lanes (which batch15 painted
        // onto the surfaces). Points are layout coords, y down, cell centers
        // at +0.5.
        private static readonly Vector2[] GraylineRoadPathPoints =
        {
            new(-0.35f, 5.50f),
            new(0.50f, 5.50f),
            new(1.50f, 4.50f),
            new(6.50f, 4.50f),
            new(6.50f, 3.50f),
            new(8.50f, 3.50f),
            new(9.30f, 3.10f),
            new(10.00f, 2.60f),
            new(13.00f, 2.60f),
            new(13.50f, 3.10f),
            new(14.00f, 3.60f),
            new(14.50f, 4.50f),
            new(15.50f, 4.50f),
            new(16.35f, 4.50f)
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
            new(-0.35f, 4.50f), new(0.50f, 4.50f),
            new(15.50f, 4.50f), new(16.35f, 4.50f)
        };

        private static readonly Vector2[] AshfallLeftRoadPathPoints =
        {
            new(-0.35f, 6.50f), new(0.50f, 6.50f), new(3.50f, 6.50f),
            new(4.50f, 5.50f), new(10.50f, 5.50f), new(11.50f, 4.50f),
            new(15.50f, 4.50f), new(16.35f, 4.50f)
        };

        private static readonly Vector2[] AshfallRightRoadPathPoints =
        {
            new(-0.35f, 2.50f), new(0.50f, 2.50f), new(3.50f, 2.50f),
            new(4.50f, 3.50f), new(8.50f, 3.50f), new(9.50f, 4.50f),
            new(15.50f, 4.50f), new(16.35f, 4.50f)
        };

        private static readonly Vector2[] AshfallCrossRoadPathPoints =
        {
            new(-0.35f, 5.50f), new(0.50f, 5.50f), new(6.50f, 5.50f),
            new(7.50f, 4.50f), new(15.50f, 4.50f), new(16.35f, 4.50f)
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
            new(-0.35f, 3.50f), new(0.50f, 3.50f),
            new(15.50f, 3.50f), new(16.35f, 3.50f)
        };

        private static readonly Vector2[] SplitSwitchLeftRoadPathPoints =
        {
            new(-0.35f, 5.50f), new(0.50f, 5.50f), new(1.50f, 6.50f),
            new(4.50f, 6.50f), new(5.50f, 5.50f), new(7.50f, 5.50f),
            new(8.50f, 6.50f), new(11.50f, 6.50f), new(12.50f, 5.50f),
            new(13.50f, 5.50f), new(14.50f, 6.50f), new(15.50f, 6.50f),
            new(16.35f, 6.50f)
        };

        private static readonly Vector2[] SplitSwitchRightRoadPathPoints =
        {
            new(-0.35f, 2.50f), new(0.50f, 2.50f), new(3.50f, 2.50f),
            new(5.50f, 4.50f), new(6.50f, 4.50f), new(8.50f, 2.50f),
            new(13.50f, 2.50f), new(15.50f, 4.50f), new(16.35f, 4.50f)
        };

        private static readonly Vector2[] SplitSwitchCrossRoadPathPoints =
        {
            new(-0.35f, 4.50f), new(0.50f, 4.50f), new(6.50f, 4.50f),
            new(8.50f, 6.50f), new(10.50f, 6.50f), new(12.50f, 4.50f),
            new(15.50f, 4.50f), new(16.35f, 4.50f)
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
            new(-0.35f, 3.50f), new(0.50f, 3.50f), new(2.50f, 3.50f),
            new(4.50f, 5.50f), new(5.50f, 5.50f), new(7.50f, 3.50f),
            new(8.50f, 3.50f), new(10.50f, 5.50f), new(11.50f, 5.50f),
            new(13.50f, 3.50f), new(14.50f, 3.50f), new(15.50f, 4.50f),
            new(16.35f, 4.50f)
        };

        private static readonly Vector2[] HollowKilnLeftRoadPathPoints =
        {
            new(-0.35f, 6.50f), new(0.50f, 6.50f), new(3.50f, 6.50f),
            new(4.50f, 5.50f), new(6.50f, 5.50f), new(7.50f, 6.50f),
            new(9.50f, 6.50f), new(10.50f, 5.50f), new(12.50f, 5.50f),
            new(13.50f, 4.50f), new(14.50f, 4.50f), new(15.50f, 3.50f),
            new(16.35f, 3.50f)
        };

        private static readonly Vector2[] HollowKilnRightRoadPathPoints =
        {
            new(-0.35f, 1.50f), new(0.50f, 1.50f), new(1.50f, 1.50f),
            new(2.50f, 2.50f), new(3.50f, 2.50f), new(4.50f, 3.50f),
            new(5.50f, 3.50f), new(7.50f, 1.50f), new(8.50f, 1.50f),
            new(10.50f, 3.50f), new(11.50f, 3.50f), new(12.50f, 2.50f),
            new(15.50f, 2.50f), new(16.35f, 2.50f)
        };

        private static readonly Vector2[] HollowKilnCrossRoadPathPoints =
        {
            new(-0.35f, 4.50f), new(0.50f, 4.50f), new(2.50f, 4.50f),
            new(3.50f, 3.50f), new(4.50f, 3.50f), new(5.50f, 4.50f),
            new(13.50f, 4.50f), new(14.50f, 5.50f), new(15.50f, 5.50f),
            new(16.35f, 5.50f)
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
            new(-0.35f, 5.50f), new(0.50f, 5.50f), new(1.50f, 5.50f),
            new(2.50f, 4.50f), new(3.50f, 4.50f), new(5.50f, 6.50f),
            new(6.50f, 6.50f), new(9.50f, 3.50f), new(10.50f, 3.50f),
            new(12.50f, 5.50f), new(13.50f, 5.50f), new(14.50f, 4.50f),
            new(15.50f, 4.50f), new(16.35f, 4.50f)
        };

        private static readonly Vector2[] LastEmberLeftRoadPathPoints =
        {
            new(-0.35f, 6.50f), new(0.50f, 6.50f), new(1.50f, 6.50f),
            new(2.50f, 5.50f), new(3.50f, 5.50f), new(4.50f, 6.50f),
            new(5.50f, 6.50f), new(6.50f, 5.50f), new(7.50f, 5.50f),
            new(8.50f, 6.50f), new(9.50f, 6.50f), new(10.50f, 5.50f),
            new(12.50f, 5.50f), new(13.50f, 6.50f), new(14.50f, 6.50f),
            new(15.50f, 5.50f), new(16.35f, 5.50f)
        };

        private static readonly Vector2[] LastEmberRightRoadPathPoints =
        {
            new(-0.35f, 1.50f), new(0.50f, 1.50f), new(1.50f, 1.50f),
            new(2.50f, 2.50f), new(3.50f, 2.50f), new(4.50f, 3.50f),
            new(5.50f, 3.50f), new(6.50f, 2.50f), new(7.50f, 2.50f),
            new(9.50f, 4.50f), new(10.50f, 4.50f), new(12.50f, 2.50f),
            new(13.50f, 2.50f), new(14.50f, 3.50f), new(15.50f, 3.50f),
            new(16.35f, 3.50f)
        };

        private static readonly Vector2[] LastEmberCrossRoadPathPoints =
        {
            new(-0.35f, 4.50f), new(0.50f, 4.50f), new(2.50f, 4.50f),
            new(3.50f, 5.50f), new(4.50f, 5.50f), new(5.50f, 4.50f),
            new(10.50f, 4.50f), new(11.50f, 5.50f), new(12.50f, 5.50f),
            new(13.50f, 4.50f), new(15.50f, 4.50f), new(16.35f, 4.50f)
        };

        private static readonly Vector2Int[] AshfallBuildPathCells =
            CombinePathCells(AshfallPathCells, AshfallLeftPathCells, AshfallRightPathCells, AshfallCrossLanePathCells);

        private static readonly Vector2Int[] SplitSwitchBuildPathCells =
            CombinePathCells(SplitSwitchPathCells, SplitSwitchLeftPathCells, SplitSwitchRightPathCells, SplitSwitchCrossLanePathCells);

        private static readonly Vector2Int[] HollowKilnBuildPathCells =
            CombinePathCells(HollowKilnPathCells, HollowKilnLeftPathCells, HollowKilnRightPathCells, HollowKilnCrossLanePathCells);

        private static readonly Vector2Int[] LastEmberBuildPathCells =
            CombinePathCells(LastEmberPathCells, LastEmberLeftPathCells, LastEmberRightPathCells, LastEmberCrossLanePathCells);

        private sealed class TDTacticalEvent
        {
            public string message;
            public float timer;
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
        private readonly List<TDEnemy> _enemiesInRangeBuffer = new(32);
        private readonly List<float> _enemiesInRangeDistances = new(32);
        private readonly List<TDEnemy> _supportEnemiesCache = new();
        private float _supportEnemiesCacheRefreshTime = -1f;
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
        private string _activeMusicPath;
        private readonly Dictionary<string, float> _musicResumePositions = new();
        private float _nextUltimateSfxTime;
        private TDTower _lastHoverSfxTower;
        private TDTowerTooltip _towerTooltip;
        private TDRadialTowerMenu _radialTowerMenu;
        private const string AudioBasePath = "Audio";
        private TDCampaignDefinition _campaign;
        private TDCampaignRoute _campaignRoute;
        private TDWaveSet _waveSet;
        private Coroutine _waveRoutine;
        private Coroutine _scenarioReinforcementRoutine;
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
        private float _battleUiTextTimer;
        private int _subsidyEntitledHundredths;
        private int _subsidyPaidTotal;
        private TDRunScoreReport _runScoreFrameCache;
        private int _runScoreFrameCacheFrame = -1;
        private TDDefenseReadinessReport _readinessCacheReport;
        private int _readinessCacheTowerCount = -1;
        private int _readinessCacheWave = -1;
        private int _readinessCacheUpgrades = -1;
        private float _resonanceSpecCacheTime = -1f;
        private int _resonanceSpecEmberAligned;
        private int _resonanceSpecEmberFit;
        private int _resonanceSpecFractureAligned;
        private int _resonanceSpecFractureFit;
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
        private RectTransform _uiTowerPanelRoot;
        private Button _uiSellTowerButton;
        private Text _uiSellTowerButtonText;
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
            var pool = GetComponent<TDObjectPool>();
            if (pool == null)
            {
                pool = gameObject.AddComponent<TDObjectPool>();
            }

            pool.Prewarm();
            if (GetComponent<TDEnemyPool>() == null)
            {
                gameObject.AddComponent<TDEnemyPool>();
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
            UpdateP124Autoplay();
            UpdateP1254ContinuousSoak();
#endif
            if (_worldMap != null && _worldMap.IsVisible)
            {
                _gridMap?.HideBuildPreview();
                HideRangePreview();
                return;
            }

            if (_titleScreen != null && _titleScreen.IsVisible)
            {
                // Title screen is covering everything — hide all game UI and skip combat input.
                _gridMap?.HideBuildPreview();
                HideRangePreview();
                if (_battleCanvas != null && _battleCanvas.transform.childCount > 0)
                {
                    // Hide the HUD root (first child of the canvas is typically the HUD).
                    // The title screen is a separate child with its own sorting.
                    for (var i = 0; i < _battleCanvas.transform.childCount; i++)
                    {
                        var child = _battleCanvas.transform.GetChild(i);
                        // Don't hide the title screen itself or its siblings.
                        if (child.name.Contains("Title") || child.name.Contains("Settings") ||
                            child.name.Contains("Emberline"))
                        {
                            continue;
                        }

                        // Hide the main HUD panel and board visuals.
                        if (child.name.Contains("TD Battle UI") || child.name.Contains("Board"))
                        {
                            child.gameObject.SetActive(false);
                        }
                    }
                }
                return;
            }
            else if (_battleCanvas != null)
            {
                // Re-show the HUD when title is gone.
                for (var i = 0; i < _battleCanvas.transform.childCount; i++)
                {
                    var child = _battleCanvas.transform.GetChild(i);
                    if (child.name.Contains("TD Battle UI") || child.name.Contains("Board"))
                    {
                        if (!child.gameObject.activeSelf)
                        {
                            child.gameObject.SetActive(true);
                        }
                    }
                }
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
            UpdateGamepadCursorInput();

            // Keep an open radial menu honest: dismiss it once the build window
            // closes (prep timeout, manual wave start) and re-evaluate slot
            // affordability when the budget shifts while it is up.
            if (_radialTowerMenu != null && _radialTowerMenu.IsVisible)
            {
                if (!IsBuildWindowOpen())
                {
                    _radialTowerMenu.Hide();
                    SetStatus("Build window closed.");
                }
                else
                {
                    _radialTowerMenu.RefreshAffordability(_defenseBudget);
                }
            }

            // In cursor mode only the virtual pointer's own UI hit matters —
            // the idle real mouse might be parked over a HUD panel and must
            // not block gamepad clicks aimed at the board.
            var pointerOverUi = _gamepadCursorMode ? _gamepadVirtualPointerOverUi : IsPointerOverBattleUi();
            var placementPressed = TDInputCompat.GetMouseButtonDown(0) || _gamepadVirtualClick;
            if (!pointerOverUi && placementPressed)
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
            UpdateGamepadCursorVisual();
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
            BuildRadialTowerMenu();
        }

        private void DenyRadialAction(string message)
        {
            SetStatus(message);
            PlaySfxTone("ui_deny", 180f, 0.08f, 0.30f, true);
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

        private void HandlePauseSettings()
        {
            _settingsPanel?.Open();
        }

        public static void SkipTitleScreenForAutomation()
        {
            _skipTitleForAutomation = true;
        }

        /// <summary>Reset the skip flag — title screen will show on next Awake.</summary>
        public static void ResetTitleScreenSkip()
        {
            _skipTitleForAutomation = false;
        }

        private void EnsureWaveRoutineRunning()
        {
            if (_waveRoutine == null && _waveSet != null)
            {
                _waveRoutine = StartCoroutine(WaveLoopFromConfig());
            }
        }

        /// <summary>
        /// Reset every per-run field before starting a fresh run in-place.
        /// EnterLevelInPlace is the only entry point for Retry / Next Mission /
        /// Deploy, and none of them reload the scene — without this reset,
        /// outcome flags (e.g. _campaignResultRecorded) and score/telemetry
        /// accumulators leak from the previous run and corrupt the next one.
        /// </summary>
        private void ResetRunState()
        {
            // Run outcome flags — without these, later runs never record progress.
            _campaignResultRecorded = false;
            _runSummaryLogged = false;
            _currentMissionStars = 0;
            _currentMissionContractCompleted = false;
            _contractFeedbackInitialized = false;
            _contractFeedbackTargetMet = false;
            _nextContractFeedbackTime = 0f;
            _campaignProgressUpdate = null;
            _newlyClaimedChapterReward = null;

            // Core run state.
            _wave = 0;
            _gameOver = false;
            _victory = false;
            _builtTowerCount = 0;
            _isInPrepPhase = false;
            _waveStartRequested = false;
            _waveDispatchedEarly = false;
            _earlyDispatchCount = 0;
            _selectedTowerKind = TDTowerKind.RailLancer;
            _selectedTowerForUi = null;
            _hoveredTower = null;
            _lineIntegrity = 0;
            _defenseBudget = 0;
            _runtimeSpawnIndex = 0;
            _openingGuideShown = false;
            _criticalDefenseCueShown = false;
            _currentWaveDefinition = null;
            _currentWaveStat = null;
            _currentWaveHint = "-";
            _currentWavePhase = "-";
            _currentWaveGoalTag = "-";
            _currentWaveThreatTags = "-";
            _currentWaveBudgetExpected = 0f;
            _currentWaveBudgetActual = 0f;
            _currentWaveBudgetInRange = true;
            _prepCountdown = 0f;
            _prepDuration = 0f;
            _lastWaveStartReadinessScore = 0;
            _lastWaveStartReadinessGrade = "-";

            // Aggregate run statistics (score / contract / telemetry inputs).
            _totalKills = 0;
            _totalEscapes = 0;
            _wavesCleared = 0;
            _totalDamageDealt = 0;
            _totalIntegrityDamageTaken = 0;
            _counterOpportunityDamage = 0;
            _counterMatchedDamage = 0;
            _budgetSpentOnBuilds = 0;
            _budgetSpentOnUpgrades = 0;
            _upgradesPurchased = 0;
            _codexDiscoveriesThisRun = 0;
            _spawnSplitEvents = 0;
            _attritionPenaltyEvents = 0;

            // Resonance / matrix run telemetry (window state + lifetime counters).
            ResetResonanceState();
            _resonanceWindowsTriggered = 0;
            _resonanceCommandsUsed = 0;
            _resonanceMatchedCommands = 0;
            _resonanceBonusDamage = 0f;
            _emberSurgeUses = 0;
            _fractureMarkUses = 0;
            _resonanceChainBonusTriggers = 0;
            _resonanceChainBudgetBonusTotal = 0;
            _resonanceChainIntegrityBonusTotal = 0;
            _doctrineEmpoweredCommands = 0;
            _matrixOpportunities = 0;
            _matrixTraitMatches = 0;
            _matrixResonanceMatches = 0;
            _matrixFullMatches = 0;
            _matrixWindowSync = 0;
            _matrixBestWindowSync = 0;
            _matrixBestWindowSpecializations = 0;
            _matrixConvergenceTriggeredThisWindow = false;
            _matrixConvergenceTriggers = 0;
            _matrixEmberConvergenceTriggers = 0;
            _matrixFractureConvergenceTriggers = 0;
            _matrixFractureConvergenceAffectedTargets = 0;
            _matrixEmberConvergenceWindowSeconds = 0f;

            // Per-run stat dictionaries.
            _waveStats.Clear();
            _failureReasonCounts.Clear();
            _laneStats.Clear();
            _towerStats.Clear();
            _roadSegmentStats.Clear();
            _threatCategoryDamage.Clear();
            _threatCategoryCounterDamage.Clear();

            // Wave-economy telemetry — rebase logged baselines onto the zeroed counters.
            ResetP125EconomyTelemetry();

            // Meta line C ledger resets with the run (ruling B2 carry-over
            // must not leak across levels).
            _subsidyEntitledHundredths = 0;
            _subsidyPaidTotal = 0;
            _salvageDerricks.Clear();
            _derrickWaveCredited = 0;

            // Invalidate per-run UI caches (readiness key could otherwise alias
            // across levels with equal wave/tower counts).
            _readinessCacheReport = null;
            _readinessCacheTowerCount = -1;
            _readinessCacheWave = -1;
            _readinessCacheUpgrades = -1;
            _resonanceSpecCacheTime = -1f;

            // Stop any in-flight scenario reinforcement from the previous run —
            // otherwise it pays its budget reward into the NEW run's economy.
            if (_scenarioReinforcementRoutine != null)
            {
                StopCoroutine(_scenarioReinforcementRoutine);
                _scenarioReinforcementRoutine = null;
            }

            _scenarioReinforcementPending = false;
        }

        /// <summary>
        /// Enter a new level without scene reload. Destroys old board/enemies/towers,
        /// reloads all level data, rebuilds the board, and restarts the wave loop.
        /// </summary>
        private void HandleTitleSettings()
        {
            _settingsPanel?.Open();
        }

        private void HandleWorldMapNodeClick(int levelIndex)
        {
            _missionBoardSelectedLevel = levelIndex;
            // Build intel text for the selected level.
            var level = GetCampaignLevel(levelIndex);
            if (level == null) return;

            var map = GetCampaignMap(level.mapId);
            var progress = TDCampaignProgression.GetLevelProgress(levelIndex);
            var unlocked = TDCampaignProgression.IsLevelUnlocked(levelIndex, _campaign.totalLevels);

            var title = $"{(level.bossLevel ? "BOSS " : "")}L{levelIndex:00}";
            if (map != null) title += $"\n{map.displayName}";

            var body = string.Empty;
            if (map != null && !string.IsNullOrWhiteSpace(map.tacticalHook))
                body += $"{map.tacticalHook}\n\n";
            if (level.newEnemyUnlocks != null && level.newEnemyUnlocks.Length > 0)
                body += $"New enemies: {string.Join(", ", level.newEnemyUnlocks)}\n\n";
            if (progress.cleared)
                body += $"Best: {progress.bestStars}★  Score: {progress.bestTacticalScore}";
            else if (unlocked)
                body += "Ready for deployment.";
            else
                body += $"Locked — clear L{levelIndex - 1:00} first.";

            _worldMap?.ShowIntel(title, body, unlocked);
            PlaySfxTone("ui_level_select", 620f, 0.09f, 0.52f, true);
        }

        private void HandleWorldMapDeploy()
        {
            var selectedLevel = _missionBoardSelectedLevel;
            if (_campaign == null || !TDCampaignProgression.IsLevelUnlocked(selectedLevel, _campaign.totalLevels))
            {
                return;
            }

            TDCampaignRouter.SaveLevelIndex(selectedLevel);

            // Hide world map, then enter the level in-place.
            _worldMap?.Hide();
            EnterLevelInPlace();
        }

        private void HandleWorldMapBack()
        {
            _worldMap?.Hide();
            // Reload the scene — Awake will rebuild the title screen.
            // (RestartCurrentScene would re-deploy the current level in-place.)
            _campaignDeploymentConfirmed = false;
            _skipTitleForAutomation = false;
            LoadingTransition("RETURNING TO TITLE", "EMBERLINE DEFENSE");
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

        private void ToggleSettingsPanel()
        {
            _settingsPanel?.Toggle();
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

        private float GetEffectiveUiScale()
        {
            var lowResolutionAssist = Screen.height <= 600 ? 1.25f : Screen.height <= 768 ? 1.12f : 1f;
            return _uiScale * lowResolutionAssist;
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
            if (TDUiWorldSkin.LoadFormationSprite("threat_strip") == null)
            {
                CreateUiImage("Formation Header Divider", _uiFormationRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -106f), new Vector2(1072f, 1f), new Color(0.56f, 0.72f, 0.80f, 0.28f));
            }

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
                // 12-kind roster = 3 rows of 4; card 58px tall at 66px pitch
                // keeps the grid above the doctrine band (see layout note at
                // the doctrine header below).
                var button = CreateUiButton(
                    $"Formation Tower {towerKind}",
                    _uiFormationRoot,
                    new Vector2(24f + (column * 145f), -160f - (row * 66f)),
                    new Vector2(133f, 58f),
                    string.Empty,
                    11,
                    () => ToggleFormationTower(towerKind));
                var identity = TDUiVisualIdentity.GetTower(towerKind);
                var icon = CreateUiSpriteImage($"Formation {towerKind} Identity Icon", button.transform, new Vector2(8f, -8f), new Vector2(42f, 42f), identity.iconResourcePath, Color.white);
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
                label.rectTransform.anchoredPosition = new Vector2(56f, -7f);
                label.rectTransform.sizeDelta = new Vector2(71f, 44f);
                label.alignment = TextAnchor.MiddleLeft;
                _uiFormationTowerButtons.Add(button);
                _uiFormationTowerButtonTexts.Add(label);
                _uiFormationTowerIcons.Add(icon);
                _uiFormationTowerAccents.Add(accent);
                _uiFormationTowerOutlines.Add(outline);
            }

            CreateUiText("Doctrine Header", _uiFormationRoot, new Vector2(24f, -358f), new Vector2(568f, 18f), "RESONANCE DOCTRINE", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.90f, 0.98f, 1f));
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
                    new Vector2(24f + (i * 194f), -382f),
                    new Vector2(180f, 46f),
                    string.Empty,
                    11,
                    () => SelectFormationDoctrine(doctrine));
                _uiFormationDoctrineButtons.Add(button);
                _uiFormationDoctrineButtonTexts.Add(button.GetComponentInChildren<Text>());
            }

            _uiFormationLockText = CreateUiText("Formation Lock State", _uiFormationRoot, new Vector2(24f, -436f), new Vector2(568f, 40f), string.Empty, 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.96f, 0.76f, 0.56f, 1f));
            CreateUiText("Difficulty Header", _uiFormationRoot, new Vector2(24f, -482f), new Vector2(568f, 16f), "CAMPAIGN DIFFICULTY", 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.90f, 0.98f, 1f));
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
                    new Vector2(24f + (i * 194f), -500f),
                    new Vector2(180f, 42f),
                    GetDifficultyShortLabel(difficulty),
                    11,
                    () => SelectFormationDifficulty(difficulty));
                _uiFormationDifficultyButtons.Add(button);
                _uiFormationDifficultyButtonTexts.Add(button.GetComponentInChildren<Text>());
            }
            if (TDUiWorldSkin.LoadFormationSprite("intel_card") == null)
            {
                CreateUiImage("Formation Intel Divider", _uiFormationRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(620f, -120f), new Vector2(1f, 398f), new Color(0.56f, 0.72f, 0.80f, 0.28f));
            }
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

            if (TDUiWorldSkin.CreateFormationUnderlay(
                    "Formation Threat Banner", _uiFormationRoot, new Vector2(24f, -58f), new Vector2(1072f, 56f), "threat_strip",
                    new Rect(0.134f, 0.052f, 0.732f, 0.896f)) != null)
            {
                _uiFormationThreatText.rectTransform.anchoredPosition = new Vector2(150f, -60f);
                _uiFormationThreatText.rectTransform.sizeDelta = new Vector2(946f, 52f);
            }
            TDUiWorldSkin.CreateFormationUnderlay(
                "Formation Intel Card", _uiFormationRoot, new Vector2(620f, -118f), new Vector2(448f, 454f), "intel_card",
                new Rect(0.145f, 0.440f, 0.711f, 0.540f));
            TDUiWorldSkin.CreateFormationUnderlay(
                "Roster Header Ornament", _uiFormationRoot, new Vector2(24f, -118f), new Vector2(220f, 41f), "header_ornament",
                new Rect(0.027f, 0.042f, 0.945f, 0.906f));
            TDUiWorldSkin.CreateFormationUnderlay(
                "Doctrine Header Ornament", _uiFormationRoot, new Vector2(24f, -356f), new Vector2(220f, 41f), "header_ornament",
                new Rect(0.027f, 0.042f, 0.945f, 0.906f));
            TDUiWorldSkin.CreateFormationUnderlay(
                "Difficulty Header Ornament", _uiFormationRoot, new Vector2(24f, -480f), new Vector2(220f, 41f), "header_ornament",
                new Rect(0.027f, 0.042f, 0.945f, 0.906f));
            _uiFormationRoot.gameObject.SetActive(false);
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

        private void TriggerScenarioBossPhase(TDEnemy boss, int phaseNumber)
        {
            RecordEnemyCodexObservation(boss?.EnemyId, TDEnemyCodexObservation.BossPhase);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
            TrackP135BossPhase(_scenarioBossPhaseSuppressed);
#endif
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
                    var card = TDUiWorldSkin.LoadFormationSprite(
                        !unlocked ? "roster_card_locked"
                        : slot >= 0 ? "roster_card_selected"
                        : "roster_card_base");
                    if (!TDUiWorldSkin.ApplyCardBackground(image, card, Color.white))
                    {
                        image.color = !unlocked
                            ? new Color(0.09f, 0.10f, 0.11f, 0.70f)
                            : slot >= 0
                                ? Color.Lerp(new Color(0.10f, 0.15f, 0.18f, 0.98f), identity.accent, 0.30f)
                                : new Color(0.13f, 0.21f, 0.25f, 0.96f);
                    }
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
                    var plate = TDUiWorldSkin.LoadFormationSprite(
                        selected && doctrineAvailable ? "doctrine_plate_on" : "doctrine_plate_base");
                    if (!TDUiWorldSkin.ApplyCardBackground(
                            image, plate,
                            doctrineAvailable ? Color.white : new Color(0.46f, 0.50f, 0.52f, 0.85f)))
                    {
                        image.color = !doctrineAvailable
                            ? new Color(0.09f, 0.10f, 0.11f, 0.74f)
                            : selected
                            ? GetDoctrineColor(doctrine, 0.98f)
                            : new Color(0.13f, 0.20f, 0.23f, 0.96f);
                    }
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
                    var plate = TDUiWorldSkin.LoadFormationSprite(
                        selectedDifficulty && availableDifficulty ? "difficulty_plate_on" : "difficulty_plate_base");
                    if (!TDUiWorldSkin.ApplyCardBackground(
                            image, plate,
                            availableDifficulty ? Color.white : new Color(0.46f, 0.50f, 0.52f, 0.85f)))
                    {
                        image.color = !availableDifficulty
                            ? new Color(0.09f, 0.10f, 0.11f, 0.74f)
                            : selectedDifficulty
                                ? GetDifficultyColor(difficulty, 0.98f)
                                : new Color(0.13f, 0.20f, 0.23f, 0.96f);
                    }
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
                TDTowerKind.SlagBurner => "Burn/Attrition",
                TDTowerKind.SalvageDerrick => "Economy/Bounty",
                TDTowerKind.RailBarricade => "Block/Intercept",
                TDTowerKind.LongRailCannon => "Snipe/Pierce",
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
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
#endif

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

        private void RebuildTowerBuildButtons()
        {
            // The legacy Tower Build Bar no longer exists (radial menu build
            // flow); kept as a no-op sink for formation/autoplay refresh paths.
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

        private float GetCampaignTowerPowerMultiplier()
        {
            var perLevelPct = Mathf.Max(0f, _campaign?.globalRules?.towerPowerPerLevelPct ?? 0f);
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
            return 1f + Mathf.Max(0, levelIndex - 1) * perLevelPct * 0.01f;
        }

        private void RegisterTowerForAnalytics(TDTower tower)
        {
            GetOrCreateTowerStat(tower);
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

        private void ClearActiveEnemiesAfterRun()
        {
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy != null && enemy.gameObject != null)
                {
                    RecordUnresolvedEnemyAtRunEnd(enemy);
                    enemy.gameObject.SetActive(false);
                    enemy.ReleaseToPool();
                }
            }

            _activeEnemies.Clear();
        }

        private bool IsResonanceWindowActive => _isResonanceSystemEnabled && _resonanceWindowTimer > 0f;

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
            // Teaching copy steps 2 (first window) + 1 (charge bar — shown at
            // the same moment the full gauge becomes readable).
            ShowResonanceTipOnce(
                "window_open",
                "Resonance window open: 7 seconds. All towers gain +10% damage while it lasts. Make one choice — one per window, no take-backs.",
                "共振窗口开启，持续 7 秒——窗口内所有塔的伤害 +10%。做一个选择，一窗只此一次，选定不能反悔。",
                8.0f);
            ShowResonanceTipOnce(
                "charge_bar",
                "The orange track at the top is your Ember Charge. It rises with every hit — the harder the hit, the faster it climbs. Resonance Beacons charge fastest of all.",
                "屏幕顶部的橙色轨道，是余烬电荷。打中敌人它就会上涨——打得越疼，涨得越快。共振信标是这套系统的引擎，它充得比谁都快。");
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

        private static string GetResonanceCommandLabel(TDResonanceCommand command)
        {
            return command switch
            {
                TDResonanceCommand.EmberSurge => "Ember Surge (all towers burst fire)",
                TDResonanceCommand.FractureMark => "Fracture Mark (vulnerability by enemy tags)",
                _ => "None"
            };
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

        private void ShowResonanceTipOnce(string tipKey, string english, string chinese, float duration = 7.0f)
        {
            var key = $"td_p16_resonance_tip_{tipKey}_{TDCampaignProgression.ActiveSaveSlot}";
            if (PlayerPrefs.GetInt(key, 0) > 0)
            {
                return;
            }

            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            var message = TDLocalization.IsChinese ? chinese : english;
            SetStatus(message);
            PushTacticalEvent(message, duration);
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
    }
}
