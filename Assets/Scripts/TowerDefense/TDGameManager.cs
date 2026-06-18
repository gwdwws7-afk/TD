using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TD
{
    public sealed class TDGameManager : MonoBehaviour
    {
        private enum TDResonanceCommand
        {
            None = 0,
            EmberSurge = 1,
            FractureMark = 2
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
        private const int GridWidth = 16;
        private const int GridHeight = 9;
        private const float CellSize = 1f;
        private const bool AllowBuildAndUpgradeDuringCombat = false;
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
        private const int ResonanceChainBudgetBonusOnEmberSurge = 24;
        private const int ResonanceChainBudgetBonusOnFractureMark = 12;
        private const int ResonanceChainIntegrityBonusOnFractureMark = 1;
        private const float SfxDefaultVolume = 0.24f;
        private const int SfxSampleRate = 22050;
        private const string FailureTagOutputInsufficient = "output_insufficient";
        private const string FailureTagCoverageGap = "coverage_gap";
        private const string FailureTagCounterMismatch = "counter_mismatch";

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

        private static readonly Vector2Int[] GraylinePathCells =
        {
            new(0, 5), new(1, 5), new(2, 5), new(3, 4), new(4, 4),
            new(5, 4), new(6, 3), new(7, 3), new(8, 3), new(9, 3),
            new(10, 4), new(11, 4), new(12, 4), new(13, 3), new(14, 3),
            new(15, 4)
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

        private static readonly Vector2Int[] SplitSwitchPathCells =
        {
            new(0, 4), new(1, 4), new(2, 5), new(3, 5), new(4, 4),
            new(5, 3), new(6, 3), new(7, 4), new(8, 5), new(9, 5),
            new(10, 4), new(11, 3), new(12, 3), new(13, 4), new(14, 4),
            new(15, 3)
        };

        private static readonly Vector2Int[] SplitSwitchLeftPathCells =
        {
            new(0, 6), new(1, 6), new(2, 6), new(3, 6), new(4, 6),
            new(5, 6), new(6, 5), new(7, 5), new(8, 6), new(9, 6),
            new(10, 6), new(11, 5), new(12, 5), new(13, 5), new(14, 5),
            new(15, 5)
        };

        private static readonly Vector2Int[] SplitSwitchRightPathCells =
        {
            new(0, 2), new(1, 2), new(2, 2), new(3, 2), new(4, 2),
            new(5, 2), new(6, 3), new(7, 3), new(8, 2), new(9, 2),
            new(10, 2), new(11, 2), new(12, 3), new(13, 3), new(14, 2),
            new(15, 2)
        };

        private static readonly Vector2Int[] SplitSwitchCrossLanePathCells =
        {
            new(0, 5), new(1, 5), new(2, 4), new(3, 4), new(4, 5),
            new(5, 5), new(6, 4), new(7, 4), new(8, 4), new(9, 4),
            new(10, 5), new(11, 5), new(12, 4), new(13, 4), new(14, 4),
            new(15, 4)
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
            public bool cleared;
            public bool logged;
            public readonly Dictionary<string, int> failureReasons = new();
        }

        private readonly List<TDEnemy> _activeEnemies = new();
        private readonly Dictionary<string, TDEnemyCatalogEntry> _enemyCatalog = new();
        private readonly Dictionary<string, TDEnemyCatalogEntry> _globalEnemyCatalog = new();
        private readonly Dictionary<int, TDWaveRuntimeStat> _waveStats = new();
        private readonly Dictionary<string, int> _failureReasonCounts = new();
        private readonly List<TDTowerKind> _unlockedTowerKinds = new();
        private readonly Dictionary<string, IReadOnlyList<Vector3>> _activeLanePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _currentWaveThreatTagSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> _sfxClipCache = new();

        private TDGridMap _gridMap;
        private Camera _mainCamera;
        private AudioSource _sfxSource;
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

        private bool _gameOver;
        private bool _victory;
        private bool _runSummaryLogged;
        private int _defenseBudget = DefaultDefenseBudget;
        private int _lineIntegrity = DefaultLineIntegrity;
        private int _wave;
        private int _totalKills;
        private int _totalEscapes;
        private int _wavesCleared;
        private float _prepCountdown;
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
        private float _resonanceBonusDamage;
        private int _emberSurgeUses;
        private int _fractureMarkUses;
        private int _resonanceChainMatchStreak;
        private int _resonanceChainBonusTriggers;
        private int _resonanceChainBudgetBonusTotal;
        private int _resonanceChainIntegrityBonusTotal;
        private int _spawnSplitEvents;
        private int _attritionPenaltyEvents;
        private int _runtimeSpawnIndex;
        private float _resonanceMarkPulseTimer;
        private GUIStyle _hudPanelStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudTextStyle;
        private GUIStyle _hudStatusStyle;
        private GUIStyle _hudMetricLabelStyle;
        private GUIStyle _hudMetricValueStyle;
        private GUIStyle _hudButtonStyle;
        private Texture2D _hudPanelTexture;
        private Texture2D _hudPanelBgTexture;
        private Texture2D _hudPanelTitleTexture;
        private Texture2D _hudStatusStripTexture;
        private Texture2D _hudButtonTexture;
        private Texture2D _hudIconWaveTexture;
        private Texture2D _hudIconIntegrityTexture;
        private Texture2D _hudIconBudgetTexture;

        public bool IsGameOver => _gameOver;
        public float CellWorldSize => CellSize;

        private void Awake()
        {
            ConfigureCamera();
            ConfigureSfx();
            LoadCampaignContext();
            BuildBoard();
            LoadEnemyCatalog();
            LoadWaveConfig();
            RefreshUnlockedTowerKinds();
        }

        private void Start()
        {
            _waveRoutine = StartCoroutine(_waveSet != null ? WaveLoopFromConfig() : FallbackWaveLoop());
        }

        private void Update()
        {
            if (_gameOver)
            {
                _gridMap?.HideBuildPreview();
                if (TDInputCompat.GetKeyDown(KeyCode.R))
                {
                    RestartCurrentScene();
                }

                return;
            }

            HandleHotkeys();
            UpdateResonanceState();

            if (TDInputCompat.GetMouseButtonDown(0))
            {
                TryPlaceTowerAtCursor();
            }

            if (TDInputCompat.GetMouseButtonDown(1))
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
        }

        private void OnGUI()
        {
            EnsureHudStyles();

            var hudScale = Mathf.Clamp(Screen.width / 1920f, 0.82f, 1.20f);
            ApplyHudScale(hudScale);

            var panelRect = new Rect(18f, 18f, 560f * hudScale, 278f * hudScale);
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

            var metricHeight = 42f * hudScale;
            var metricGap = 10f * hudScale;
            var metricWidth = (contentWidth - (metricGap * 2f)) / 3f;
            DrawHudMetric(new Rect(left, y, metricWidth, metricHeight), _hudIconWaveTexture, "WAVE", _wave.ToString());
            DrawHudMetric(new Rect(left + metricWidth + metricGap, y, metricWidth, metricHeight), _hudIconIntegrityTexture, "INTEGRITY", _lineIntegrity.ToString());
            DrawHudMetric(new Rect(left + ((metricWidth + metricGap) * 2f), y, metricWidth, metricHeight), _hudIconBudgetTexture, "BUDGET", _defenseBudget.ToString());
            y += 50f * hudScale;

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
                $"LMB Place   RMB Upgrade   [1-8] Tower   {(_isResonanceSystemEnabled ? "Z/X Resonance" : $"Z/X Resonance(L{_resonanceEnabledFromLevel:00}+)")}   {(_allowEarlyWaveDispatch ? "Space Dispatch" : "Space Dispatch(Off)")}   F5/F6 Level   R Restart",
                _hudTextStyle,
                new Color(0.84f, 0.92f, 0.97f, 1f),
                new Color(0f, 0f, 0f, 0.44f));
            y += 19f * hudScale;

            DrawShadowedLabel(
                new Rect(left, y, contentWidth, 18f * hudScale),
                $"Prep {Mathf.Max(0f, _prepCountdown):0.0}s   Phase {_currentWavePhase}   Goal {_currentWaveGoalTag}   Budget {_currentWaveBudgetActual:0.##}/{_currentWaveBudgetExpected:0.##} {(_currentWaveBudgetInRange ? "OK" : "OUT")}   Hint {_currentWaveHint}",
                _hudTextStyle,
                new Color(0.84f, 0.92f, 0.97f, 1f),
                new Color(0f, 0f, 0f, 0.44f));
            y += 20f * hudScale;

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
            _hudIconWaveTexture = Resources.Load<Texture2D>("Art/hud_icon_wave");
            _hudIconIntegrityTexture = Resources.Load<Texture2D>("Art/hud_icon_integrity");
            _hudIconBudgetTexture = Resources.Load<Texture2D>("Art/hud_icon_budget");

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
                normal = { textColor = new Color(0.84f, 0.92f, 0.96f, 0.96f) }
            };

            _hudStatusStyle = new GUIStyle(_hudTextStyle)
            {
                normal = { textColor = new Color(0.96f, 0.90f, 0.72f, 0.98f) }
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
            _hudMetricLabelStyle.fontSize = Mathf.RoundToInt(10f * hudScale);
            _hudMetricValueStyle.fontSize = Mathf.RoundToInt(18f * hudScale);
            _hudButtonStyle.fontSize = Mathf.RoundToInt(20f * hudScale);
        }

        private void DrawHudPanel(Rect panelRect, float hudScale)
        {
            if (_hudPanelBgTexture != null)
            {
                DrawTexture(panelRect, _hudPanelBgTexture, 1f);
            }
            else
            {
                GUI.Box(panelRect, string.Empty, _hudPanelStyle);
            }

            if (_hudPanelTitleTexture == null)
            {
                return;
            }

            var titleHeight = Mathf.Min(panelRect.height * 0.38f, 74f * hudScale);
            var titleRect = new Rect(panelRect.x, panelRect.y, panelRect.width, titleHeight);
            DrawTexture(titleRect, _hudPanelTitleTexture, 0.60f);
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
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var multiplier = 1.10f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.16f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark &&
                (towerKind == TDTowerKind.FrostCoil || towerKind == TDTowerKind.SiegeDrill))
            {
                multiplier *= 1.08f;
            }

            return multiplier;
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

        public int GetModifiedDamageForEnemy(TDTowerKind sourceTowerKind, TDEnemy enemy, int rawDamage)
        {
            if (enemy == null || rawDamage <= 0)
            {
                return Mathf.Max(1, rawDamage);
            }

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
                        multiplier *= 1.45f;
                    }
                    else if (enemy.HasTag("heavy"))
                    {
                        multiplier *= 1.35f;
                    }
                    else if (enemy.HasTag("fast"))
                    {
                        multiplier *= 1.25f;
                    }
                    else
                    {
                        multiplier *= 1.18f;
                    }

                    if (sourceTowerKind == TDTowerKind.FrostCoil)
                    {
                        multiplier *= 1.08f;
                    }

                    if (sourceTowerKind == TDTowerKind.SiegeDrill && enemy.HasTag("armored"))
                    {
                        multiplier *= 1.12f;
                    }

                    if (sourceTowerKind == TDTowerKind.ResonanceBeacon)
                    {
                        multiplier *= 1.07f;
                    }
                }
            }

            var adjusted = Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier));
            if (adjusted > rawDamage)
            {
                _resonanceBonusDamage += adjusted - rawDamage;
            }

            return adjusted;
        }

        public void NotifyEnemyDamaged(TDTowerKind sourceTowerKind, TDEnemy enemy, int damageTaken, bool appliedSlow)
        {
            if (damageTaken <= 0)
            {
                return;
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
            if (appliedSlow)
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

        public void NotifyEnemyKilled(TDEnemy enemy, int reward)
        {
            _activeEnemies.Remove(enemy);
            _defenseBudget += reward;
            _totalKills++;

            if (enemy != null && enemy.EnemyId == "spore_carrier")
            {
                _spawnSplitEvents++;
                StartCoroutine(SpawnSplitChildren("ash_swarm", 2, 0.22f));
            }
            else if (enemy != null && enemy.EnemyId == "furnace_matriarch")
            {
                _spawnSplitEvents++;
                StartCoroutine(SpawnSplitChildren("ash_swarm", 6, 0.16f));
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

            _totalEscapes++;
            if (_currentWaveStat != null)
            {
                _currentWaveStat.escapes++;
            }

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

            _lineIntegrity = Mathf.Max(0, _lineIntegrity - Mathf.Max(1, lineDamage));
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
            SetStatus($"Leak: {enemyId} dealt {lineDamage} integrity damage [{failureReason}]{attritionLabel}");
            PlaySfxTone(extraBudgetLoss > 0 ? "leak_attrition" : "leak_default", extraBudgetLoss > 0 ? 180f : 240f, 0.18f, 0.74f, false);

            if (_lineIntegrity > 0)
            {
                return;
            }

            FinalizeCurrentWaveStat(false);
            _gameOver = true;
            _victory = false;
            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
            }

            PlaySfxTone("run_defeat", 150f, 0.28f, 0.90f, false);
            LogRunSummary();
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
            _resonanceCharge = ResonanceChargeMax * (_resonanceWindowTimer / ResonanceWindowDuration);

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

            _resonanceCharge = Mathf.Clamp(_resonanceCharge + amount, 0f, ResonanceChargeMax);
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
            _resonanceWindowTimer = 0f;
            _resonanceCharge = 0f;
            _activeResonanceCommand = TDResonanceCommand.None;
            _resonanceMarkPulseTimer = 0f;
            if (missedCommand)
            {
                _resonanceChainMatchStreak = 0;
            }

            SetStatus("Resonance window ended");
            PlaySfxTone("resonance_end", 290f, 0.17f, 0.60f, false);
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
                _lineIntegrity = Mathf.Min(DefaultLineIntegrity, _lineIntegrity + integrityBonus);
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
                    return $"Resonance ACTIVE {_resonanceWindowTimer:0.0}s  Choose [Z] Ember Surge / [X] Fracture Mark  Chain {_resonanceChainMatchStreak}/{ResonanceChainRequiredMatches}";
                }

                return $"Resonance ACTIVE {_resonanceWindowTimer:0.0}s  Command {GetResonanceCommandLabel(_activeResonanceCommand)}  BonusDmg +{Mathf.RoundToInt(_resonanceBonusDamage)}  Chain {_resonanceChainMatchStreak}/{ResonanceChainRequiredMatches}";
            }

            return $"Resonance Charge {_resonanceCharge:0}/{ResonanceChargeMax:0}  ChainBonus {_resonanceChainBonusTriggers}";
        }

        private void ResetResonanceState()
        {
            _resonanceWindowTimer = 0f;
            _resonanceCharge = 0f;
            _activeResonanceCommand = TDResonanceCommand.None;
            _resonanceMarkPulseTimer = 0f;
            _resonanceChainMatchStreak = 0;
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
            _mainCamera.orthographicSize = 5.8f;
            _mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
        }

        private void ConfigureSfx()
        {
            _sfxSource = GetComponent<AudioSource>();
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
            }

            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
            _sfxSource.spatialBlend = 0f;
            _sfxSource.volume = SfxDefaultVolume;
        }

        private void PlaySfxTone(string key, float frequency, float duration, float volumeScale = 1f, bool rising = false)
        {
            if (_sfxSource == null || volumeScale <= 0f || duration <= 0f || frequency <= 0f)
            {
                return;
            }

            if (!_sfxClipCache.TryGetValue(key, out var clip) || clip == null)
            {
                clip = CreateSfxClip(key, frequency, duration, rising);
                if (clip == null)
                {
                    return;
                }

                _sfxClipCache[key] = clip;
            }

            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private static AudioClip CreateSfxClip(string key, float frequency, float duration, bool rising)
        {
            var sampleCount = Mathf.Max(64, Mathf.CeilToInt(duration * SfxSampleRate));
            var data = new float[sampleCount];
            var phase = 0f;
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / Mathf.Max(1f, sampleCount - 1f);
                var currentFrequency = rising
                    ? Mathf.Lerp(frequency * 0.75f, frequency * 1.25f, t)
                    : Mathf.Lerp(frequency * 1.12f, frequency * 0.88f, t);
                phase += (2f * Mathf.PI * currentFrequency) / SfxSampleRate;

                var attack = Mathf.Clamp01(t / 0.10f);
                var release = Mathf.Clamp01((1f - t) / 0.24f);
                var envelope = attack * release;
                data[i] = Mathf.Sin(phase) * envelope * 0.30f;
            }

            var clip = AudioClip.Create($"td_sfx_{key}", sampleCount, 1, SfxSampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void BuildBoard()
        {
            var boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(transform, false);

            var mapId = _campaignRoute?.level?.mapId ?? "grayline_junction";
            _gridMap = new TDGridMap(GridWidth, GridHeight, CellSize, GetPathCellsForMap(mapId), boardRoot, mapId);
            ConfigureActiveLanePaths(mapId);
        }

        private static IReadOnlyList<Vector2Int> GetPathCellsForMap(string mapId)
        {
            var layoutCells = mapId switch
            {
                "ashfall_depot" => AshfallBuildPathCells,
                "split_switch_canyon" => SplitSwitchBuildPathCells,
                "hollow_kiln_basin" => HollowKilnBuildPathCells,
                "last_ember_terminus" => LastEmberBuildPathCells,
                _ => GraylinePathCells
            };

            return ConvertLayoutCellsToUnityCells(layoutCells);
        }

        private void ConfigureActiveLanePaths(string mapId)
        {
            _activeLanePaths.Clear();
            if (_gridMap == null)
            {
                return;
            }

            var basePath = new List<Vector3>(_gridMap.PathWorldPoints);
            _activeLanePaths["default"] = basePath;
            _activeLanePaths["center"] = basePath;
            _activeLanePaths["all"] = basePath;

            if (string.Equals(mapId, "split_switch_canyon", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromCells(SplitSwitchPathCells);
                var leftPath = BuildWorldPathFromCells(SplitSwitchLeftPathCells);
                var rightPath = BuildWorldPathFromCells(SplitSwitchRightPathCells);
                var crossPath = BuildWorldPathFromCells(SplitSwitchCrossLanePathCells);

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
                var centerPath = BuildWorldPathFromCells(AshfallPathCells);
                var leftPath = BuildWorldPathFromCells(AshfallLeftPathCells);
                var rightPath = BuildWorldPathFromCells(AshfallRightPathCells);
                var crossPath = BuildWorldPathFromCells(AshfallCrossLanePathCells);

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
                var centerPath = BuildWorldPathFromCells(HollowKilnPathCells);
                var leftPath = BuildWorldPathFromCells(HollowKilnLeftPathCells);
                var rightPath = BuildWorldPathFromCells(HollowKilnRightPathCells);
                var crossPath = BuildWorldPathFromCells(HollowKilnCrossLanePathCells);

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
                var centerPath = BuildWorldPathFromCells(LastEmberPathCells);
                var leftPath = BuildWorldPathFromCells(LastEmberLeftPathCells);
                var rightPath = BuildWorldPathFromCells(LastEmberRightPathCells);
                var crossPath = BuildWorldPathFromCells(LastEmberCrossLanePathCells);

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

        private IReadOnlyList<Vector3> BuildWorldPathFromCells(Vector2Int[] pathCells)
        {
            var points = new List<Vector3>(pathCells?.Length ?? 0);
            if (_gridMap == null || pathCells == null)
            {
                return points;
            }

            for (var i = 0; i < pathCells.Length; i++)
            {
                points.Add(_gridMap.CellToWorld(LayoutCellToUnityCell(pathCells[i])));
            }

            return points;
        }

        private IReadOnlyList<Vector3> GetDefaultSpawnPath()
        {
            if (_activeLanePaths.TryGetValue("default", out var defaultPath) && defaultPath != null && defaultPath.Count > 1)
            {
                return defaultPath;
            }

            return _gridMap?.PathWorldPoints ?? Array.Empty<Vector3>();
        }

        private IReadOnlyList<Vector3> ResolveSpawnPath(TDWaveGroup group, string formation, int spawnIndex)
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

            if (lane == "all")
            {
                lane = ResolveAllLaneKey(formation, spawnIndex);
            }

            if (lane == "split_lane" && !_activeLanePaths.ContainsKey(lane) && _activeLanePaths.ContainsKey("left"))
            {
                lane = "left";
            }
            else if (lane == "cross_lane" && !_activeLanePaths.ContainsKey(lane) && _activeLanePaths.ContainsKey("right"))
            {
                lane = "right";
            }

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
            _campaign = null;
            _campaignRoute = null;
            _campaignError = string.Empty;
            _waveResourcePath = DefaultWaveResourcePath;
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
            ApplyCampaignGlobalRules(campaign, route.level.levelIndex);
            TDCampaignRouter.SaveLevelIndex(route.level.levelIndex);
            _campaignError = string.Empty;
            RefreshUnlockedTowerKinds();
            Debug.Log($"[TD][Campaign] level={route.level.levelIndex} levelId={route.level.levelId} map={route.level.mapId} waveSet={route.level.waveSetId}");
            RefreshLoadError();
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

        private void RefreshUnlockedTowerKinds()
        {
            _unlockedTowerKinds.Clear();
            var seen = new HashSet<TDTowerKind>();

            if (_campaignRoute?.level != null && _campaign?.levels != null)
            {
                var currentLevel = _campaignRoute.level.levelIndex;
                for (var i = 0; i < _campaign.levels.Length; i++)
                {
                    var level = _campaign.levels[i];
                    if (level == null || level.levelIndex > currentLevel || level.newTowerUnlocks == null)
                    {
                        continue;
                    }

                    for (var t = 0; t < level.newTowerUnlocks.Length; t++)
                    {
                        if (!TDTower.TryParseTowerId(level.newTowerUnlocks[t], out var kind))
                        {
                            continue;
                        }

                        seen.Add(kind);
                    }
                }
            }

            var buildOrder = TDTower.GetBuildOrder();
            for (var i = 0; i < buildOrder.Count; i++)
            {
                var kind = buildOrder[i];
                if (seen.Contains(kind))
                {
                    _unlockedTowerKinds.Add(kind);
                }
            }

            if (_unlockedTowerKinds.Count == 0)
            {
                _unlockedTowerKinds.Add(TDTowerKind.RailLancer);
                _unlockedTowerKinds.Add(TDTowerKind.CinderMortar);
                _unlockedTowerKinds.Add(TDTowerKind.FrostCoil);
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

            if (TDInputCompat.GetKeyDown(KeyCode.F5))
            {
                TryStepCampaignLevel(-1);
            }
            else if (TDInputCompat.GetKeyDown(KeyCode.F6))
            {
                TryStepCampaignLevel(1);
            }

            if (TDInputCompat.GetKeyDown(KeyCode.Space))
            {
                TryDispatchCurrentWaveEarly();
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

            TDCampaignRouter.SaveLevelIndex(nextLevel);
            SetStatus($"Switching to level {nextLevel:00}...");
            RestartCurrentScene();
        }

        private void TryDispatchCurrentWaveEarly()
        {
            if (_gameOver || _waveSet == null || _prepCountdown <= 0f)
            {
                return;
            }

            if (!_allowEarlyWaveDispatch)
            {
                SetStatus("Early dispatch is disabled by campaign global rules.");
                return;
            }

            if (_waveDispatchedEarly)
            {
                return;
            }

            _waveDispatchedEarly = true;
            _earlyDispatchCount++;
            _prepCountdown = 0f;
            SetStatus($"Wave {_wave} dispatched early.");
            PlaySfxTone("wave_dispatch_early", 640f, 0.11f, 0.58f, true);
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

        private void TryPlaceTowerAtCursor()
        {
            if (!IsBuildWindowOpen())
            {
                SetStatus("Build is disabled during combat. Wait for prep phase.");
                return;
            }

            if (!IsTowerUnlocked(_selectedTowerKind))
            {
                SetStatus($"{GetTowerKindLabel(_selectedTowerKind)} is not unlocked on this level.");
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

            if (TryGetTowerUnderCursor(world, out _))
            {
                return;
            }

            if (!_gridMap.IsBuildable(cell))
            {
                SetStatus("This cell is not buildable.");
                return;
            }

            _defenseBudget -= towerCost;
            _gridMap.SetTower(cell, true);
            SpawnTower(cell, _selectedTowerKind);
            SetStatus($"Built {GetTowerKindLabel(_selectedTowerKind)} (-{towerCost} budget)");
            PlaySfxTone("tower_build", 420f, 0.10f, 0.55f, true);
        }

        private void TryUpgradeTowerAtCursor()
        {
            if (!IsBuildWindowOpen())
            {
                SetStatus("Upgrade is disabled during combat. Wait for prep phase.");
                return;
            }

            var mouse = TDInputCompat.MousePosition;
            mouse.z = -_mainCamera.transform.position.z;
            var world = _mainCamera.ScreenToWorldPoint(mouse);
            world.z = 0f;

            if (!TryGetTowerUnderCursor(world, out var tower))
            {
                return;
            }

            if (!tower.CanUpgrade)
            {
                SetStatus("Tower is already at max tier.");
                return;
            }

            var upgradeCost = tower.GetUpgradeCost(_selectedUpgradeBranch);
            if (_defenseBudget < upgradeCost)
            {
                SetStatus($"Insufficient defense budget. Upgrade needs {upgradeCost}.");
                return;
            }

            if (!tower.ApplyUpgrade(_selectedUpgradeBranch))
            {
                SetStatus("Upgrade failed.");
                return;
            }

            _defenseBudget -= upgradeCost;
            SetStatus($"Upgraded {tower.DisplayName} [{GetUpgradeBranchLabel(_selectedUpgradeBranch)}] (-{upgradeCost} budget)");
            PlaySfxTone("tower_upgrade", 520f, 0.12f, 0.60f, true);
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

        private void SpawnTower(Vector2Int cell, TDTowerKind kind)
        {
            var towerObject = new GameObject($"Tower_{cell.x}_{cell.y}");
            towerObject.transform.position = _gridMap.CellToWorld(cell);
            towerObject.transform.localScale = Vector3.one;
            towerObject.transform.SetParent(transform, true);

            var collider = towerObject.AddComponent<BoxCollider2D>();
            collider.size = GetTowerColliderSize(kind);
            collider.offset = GetTowerColliderOffset(kind);

            var tower = towerObject.AddComponent<TDTower>();
            tower.Initialize(this, kind);
        }

        private void SpawnEnemy(TDEnemyCatalogEntry entry, IReadOnlyList<Vector3> path, int waveNumber, int enemyIndex)
        {
            var enemyObject = new GameObject($"Enemy_{entry.enemyId}_{waveNumber}_{enemyIndex}");
            enemyObject.transform.SetParent(transform, true);

            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(enemyObject.transform, false);
            shadowObject.transform.localPosition = GetEnemyShadowOffset(entry.enemyId);

            var shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
            shadowRenderer.sortingOrder = Mathf.Max(0, GetEnemySortingOrder(entry.enemyId) - 3);
            shadowRenderer.sprite = TDArtLibrary.GetSoftShadowSprite();
            shadowRenderer.color = new Color(0f, 0f, 0f, GetEnemyShadowAlpha(entry.enemyId));
            shadowObject.transform.localScale = ResolveSpriteScale(shadowRenderer.sprite, GetEnemyShadowCoverage(entry.enemyId));

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(enemyObject.transform, false);
            visualObject.transform.localPosition = GetEnemyVisualOffset(entry.enemyId);

            var renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = GetEnemySortingOrder(entry.enemyId);
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback(GetEnemySpritePath(entry.enemyId), GetEnemyFallbackColor(entry.enemyId));
            visualObject.transform.localScale = ResolveSpriteScale(renderer.sprite, GetEnemyCellCoverage(entry.enemyId));

            var animator = visualObject.AddComponent<TDSpriteAnimator>();
            animator.Configure(GetEnemyAnimationPrefix(entry.enemyId), GetEnemyAnimationFrames(entry.enemyId), GetEnemyAnimationFps(entry.enemyId), true, true);

            var collider = enemyObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = GetEnemyColliderSize(entry.enemyId);
            collider.offset = GetEnemyColliderOffset(entry.enemyId);

            var enemy = enemyObject.AddComponent<TDEnemy>();
            enemy.Initialize(this, path ?? GetDefaultSpawnPath(), entry);
            _activeEnemies.Add(enemy);
        }

        private IEnumerator SpawnSplitChildren(string enemyId, int count, float interval)
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
            for (var i = 0; i < count && !_gameOver; i++)
            {
                _runtimeSpawnIndex++;
                SpawnEnemy(entry, GetDefaultSpawnPath(), _wave, 10000 + _runtimeSpawnIndex);
                if (i < count - 1)
                {
                    yield return new WaitForSeconds(safeInterval);
                }
            }
        }

        private IEnumerator WaveLoopFromConfig()
        {
            yield return new WaitForSeconds(1f);

            var waves = _waveSet.waves;
            Array.Sort(waves, (a, b) => a.waveIndex.CompareTo(b.waveIndex));

            for (var w = 0; w < waves.Length && !_gameOver; w++)
            {
                var wave = waves[w];
                _wave = wave.waveIndex;
                ApplyConfiguredWaveRuntimeContext(wave);
                BeginWaveStat(_wave);
                _currentWaveHint = string.IsNullOrWhiteSpace(wave.hint) ? "(no hint)" : wave.hint;

                _prepCountdown = wave.prepSeconds > 0f ? wave.prepSeconds : _waveSet.globalDefaults.prepSeconds;
                while (_prepCountdown > 0f && !_gameOver)
                {
                    _prepCountdown -= Time.deltaTime;
                    yield return null;
                }
                _prepCountdown = 0f;
                if (_waveDispatchedEarly)
                {
                    SetStatus($"Wave {_wave} dispatched early, spawning now.");
                }

                var groups = wave.groups ?? Array.Empty<TDWaveGroup>();
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

                var reward = wave.rewardGold > 0 ? wave.rewardGold : _waveSet.globalDefaults.baseRewardGold;
                _defenseBudget += reward;
                SetStatus($"Wave {_wave} cleared, reward +{reward} budget");
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
            PlaySfxTone("run_victory", 760f, 0.34f, 0.95f, true);
            LogRunSummary();
        }

        private IEnumerator SpawnGroup(TDWaveDefinition wave, TDWaveGroup group, Action onCompleted)
        {
            if (group == null)
            {
                onCompleted?.Invoke();
                yield break;
            }

            var formation = NormalizeGroupToken(group.formation);
            var delayedStart = Mathf.Max(0f, group.startDelay + GetFormationStartDelayOffset(formation));
            if (delayedStart > 0f)
            {
                yield return new WaitForSeconds(delayedStart);
            }

            var interval = Mathf.Max(group.spawnInterval, _waveSet.globalDefaults.spawnMinSpacing);
            for (var i = 0; i < group.count && !_gameOver; i++)
            {
                if (_enemyCatalog.TryGetValue(group.enemyId, out var entry))
                {
                    var path = ResolveSpawnPath(group, formation, i);
                    SpawnEnemy(entry, path, wave.waveIndex, i + 1);
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
            _currentWaveHint = "Wave config missing: fallback mode enabled.";
            yield return new WaitForSeconds(1f);

            while (!_gameOver)
            {
                _wave++;
                var enemyCount = 5 + (_wave * 2);
                ApplyFallbackWaveRuntimeContext(enemyCount);
                BeginWaveStat(_wave);
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

                    SpawnEnemy(entry, GetDefaultSpawnPath(), _wave, i + 1);
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

                _defenseBudget += 20 + _wave;
                FinalizeCurrentWaveStat(true);
                yield return new WaitForSeconds(1.2f);
            }
        }

        private void RestartCurrentScene()
        {
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
            return enemyId switch
            {
                "skitter_runner" => 0.30f,
                "carapace_brute" => 0.34f,
                "ash_swarm" => 0.28f,
                "plated_spore" => 0.32f,
                "burrow_sapper" => 0.30f,
                "ember_leech" => 0.30f,
                "spore_carrier" => 0.31f,
                "rail_warden" => 0.33f,
                "cinder_glider" => 0.30f,
                "husk_titan" => 0.35f,
                "echo_mimic" => 0.32f,
                "furnace_matriarch" => 0.36f,
                _ => 0.30f
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
            CaptureCurrentWaveThreatTags(wave?.threatTags);
            _currentWaveThreatTags = wave?.threatTags == null || wave.threatTags.Length == 0
                ? "none"
                : string.Join("/", wave.threatTags);

            _currentWaveBudgetExpected = Mathf.Max(0f, wave?.budgetTarget ?? 0f);
            _currentWaveBudgetActual = CalculateWaveBudgetActual(wave);
            _currentWaveBudgetInRange = IsWaveBudgetInRange(_currentWaveBudgetExpected, wave?.budgetTolerance ?? 1f, _currentWaveBudgetActual);
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

        private void CaptureCurrentWaveThreatTags(string[] sourceTags)
        {
            _currentWaveThreatTagSet.Clear();
            if (sourceTags == null || sourceTags.Length == 0)
            {
                return;
            }

            for (var i = 0; i < sourceTags.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(sourceTags[i]))
                {
                    continue;
                }

                _currentWaveThreatTagSet.Add(sourceTags[i].Trim().ToLowerInvariant());
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

        private static void IncrementCounter(Dictionary<string, int> counter, string key)
        {
            if (counter.TryGetValue(key, out var value))
            {
                counter[key] = value + 1;
                return;
            }

            counter[key] = 1;
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
                $"cleared={stat.cleared} kills={stat.kills} escapes={stat.escapes} " +
                $"budget={stat.budgetStart}->{stat.budgetEnd} integrity={stat.integrityStart}->{stat.integrityEnd} " +
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
            Debug.Log(
                $"[TD][RunSummary] result={result} level={levelId} map={mapId} waveSet={waveSetId} reachedWave={_wave} cleared={_wavesCleared}/{GetConfiguredWaveCount()} " +
                $"kills={_totalKills} escapes={_totalEscapes} failures={GetTopFailureReasonSummary()} " +
                $"earlyDispatches={_earlyDispatchCount} earlyDispatchEnabled={_allowEarlyWaveDispatch} " +
                $"resonanceEnabledFrom={_resonanceEnabledFromLevel} resonanceEnabled={_isResonanceSystemEnabled} " +
                $"resonanceWindows={_resonanceWindowsTriggered} resonanceCommands={_resonanceCommandsUsed} " +
                $"emberSurgeUses={_emberSurgeUses} fractureMarkUses={_fractureMarkUses} resonanceBonusDmg={Mathf.RoundToInt(_resonanceBonusDamage)} " +
                $"chainBonusTriggers={_resonanceChainBonusTriggers} chainBudgetBonus={_resonanceChainBudgetBonusTotal} chainIntegrityBonus={_resonanceChainIntegrityBonusTotal} " +
                $"splitSpawnEvents={_spawnSplitEvents} attritionPenaltyEvents={_attritionPenaltyEvents}");
        }

        private void SetStatus(string message)
        {
            _lastStatus = message;
            _statusTimer = 2.5f;
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

            return _wave == 0 || _prepCountdown > 0f;
        }

        private void UpdateBuildPreviewUnderCursor()
        {
            if (_mainCamera == null || _gridMap == null)
            {
                return;
            }

            var mouse = TDInputCompat.MousePosition;
            mouse.z = -_mainCamera.transform.position.z;
            var world = _mainCamera.ScreenToWorldPoint(mouse);
            world.z = 0f;
            _gridMap.UpdateBuildPreview(world);
        }
    }
}

