// Freeze-period move: UiPanels cluster.
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

            // The legacy Tower Build Bar was removed: building goes through the
            // radial tower menu (click an empty build pad). Keyboard 1-8 still
            // drives the build ghost preview via _selectedTowerKind.

            _uiTowerPanelRoot = CreateUiPanel("Tower Upgrade Panel", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(300f, 264f), new Color(0.028f, 0.036f, 0.040f, 0.92f));
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
            _uiSellTowerButton = CreateUiButton("Sell Tower", _uiTowerPanelRoot, new Vector2(12f, -222f), new Vector2(276f, 30f), "SELL", 10, TrySellSelectedTowerFromUi);
            _uiSellTowerButtonText = _uiSellTowerButton.GetComponentInChildren<Text>();
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

        private void BuildRadialTowerMenu()
        {
            if (_battleCanvas == null || _radialTowerMenu != null) return;
            var go = new GameObject("TD Radial Tower Menu");
            go.transform.SetParent(_battleCanvas.transform, false);
            _radialTowerMenu = go.AddComponent<TDRadialTowerMenu>();
            _radialTowerMenu.Build(_battleCanvas);
            _radialTowerMenu.OnTowerSelected = HandleRadialTowerSelected;
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

        private void BuildTitleScreen()
        {
            if (_battleCanvas == null || _titleScreen != null)
            {
                return;
            }

            // Skip the title screen entirely for automated smoke/autoplay probes.
            var skipTitle = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--td-skip-title") >= 0
                || _skipTitleForAutomation
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
                || TDStandaloneSmokeProbe.IsRequested()
                || TDP1254StandaloneProbe.IsRequested()
#endif
                ;
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
                        ? "点击空的可建造塔位，在弹出的环形菜单中选择要部署的防御塔。只有完成部署后，本步骤才会通过。"
                        : "Click an empty build pad, then pick a tower from the radial menu that opens. The action is accepted only after a tower is deployed.";
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

            // World map: full-screen visual map.
            var worldMapGo = new GameObject("TD World Map");
            worldMapGo.transform.SetParent(_battleCanvas.transform, false);
            _worldMap = worldMapGo.AddComponent<TDWorldMap>();
            _worldMap.BuildFullScreen(_battleCanvas);
            _worldMap.OnNodeClicked = HandleWorldMapNodeClick;
            _worldMap.DeployButton?.onClick.AddListener(() => HandleWorldMapDeploy());
            _worldMap.BackButton?.onClick.AddListener(() => HandleWorldMapBack());

            // The map's node buttons ARE the level buttons now — keep the
            // legacy list populated so selection focus and the p8 UI audit
            // (p8.ui.levelButtons) keep working.
            for (var nodeIndex = 0; nodeIndex < _worldMap.NodeButtons.Count; nodeIndex++)
            {
                _uiMissionLevelButtons.Add(_worldMap.NodeButtons[nodeIndex]);
            }

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
            _uiCampaignProfileSummaryText = CreateUiText("Campaign Profile Summary", _uiCampaignProfileRoot, new Vector2(24f, -54f), new Vector2(1072f, 42f), string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.99f, 0.90f, 0.66f, 1f));
            CreateUiImage("Campaign Profile Header Divider", _uiCampaignProfileRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -98f), new Vector2(1072f, 1f), new Color(0.56f, 0.72f, 0.80f, 0.28f));

            CreateUiText("Campaign Profile Chapter Header", _uiCampaignProfileRoot, new Vector2(24f, -118f), new Vector2(520f, 24f), "CHAPTER ARCHIVE", 13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.80f, 0.93f, 1f, 1f));
            _uiCampaignProfileChapterText = CreateUiText("Campaign Profile Chapters", _uiCampaignProfileRoot, new Vector2(24f, -153f), new Vector2(520f, 277f), string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.80f, 0.90f, 0.97f, 1f));
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

            // Text refresh runs at ~10 Hz — the interpolations dominate HUD
            // cost and none of these values need per-frame precision. Layout,
            // visibility and the resonance fill stay per-frame.
            _battleUiTextTimer -= Time.unscaledDeltaTime;
            var refreshTexts = _battleUiTextTimer <= 0f;
            if (refreshTexts)
            {
                _battleUiTextTimer = 0.1f;
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
            }

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
                _resonanceSpecCacheTime = -1f;
                return;
            }

            // Counting owned specializations does two full-scene tower scans —
            // refresh it at 0.25s while the window is open, not every frame.
            if (Time.unscaledTime - _resonanceSpecCacheTime >= 0.25f)
            {
                _resonanceSpecCacheTime = Time.unscaledTime;
                _resonanceSpecEmberAligned = CountOwnedSpecializationsForCommand(TDResonanceCommand.EmberSurge, out _resonanceSpecEmberFit);
                _resonanceSpecFractureAligned = CountOwnedSpecializationsForCommand(TDResonanceCommand.FractureMark, out _resonanceSpecFractureFit);
            }

            var emberAligned = _resonanceSpecEmberAligned;
            var emberThreatFit = _resonanceSpecEmberFit;
            var fractureAligned = _resonanceSpecFractureAligned;
            var fractureThreatFit = _resonanceSpecFractureFit;
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
            // Readiness sampling scans all towers + lane coverage; it only
            // changes when the tower roster, upgrades or wave definition change.
            if (_readinessCacheReport == null ||
                _readinessCacheTowerCount != _builtTowerCount ||
                _readinessCacheWave != _wave ||
                _readinessCacheUpgrades != _upgradesPurchased)
            {
                _readinessCacheTowerCount = _builtTowerCount;
                _readinessCacheWave = _wave;
                _readinessCacheUpgrades = _upgradesPurchased;
                _readinessCacheReport = CalculateDefenseReadiness(_currentWaveDefinition);
            }

            var readiness = _readinessCacheReport;
            SetUiText(
                _uiWaveIntelReadinessText,
                TDLocalization.IsChinese
                    ? $"战备  {readiness.score:00} {readiness.grade}   覆盖 {readiness.coverageScore:00}   克制 {readiness.counterScore:00}"
                    : $"READINESS  {readiness.score:00} {readiness.grade}   COV {readiness.coverageScore:00}   CTR {readiness.counterScore:00}");
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

            if (_uiSellTowerButton != null)
            {
                _uiSellTowerButton.interactable = IsBuildWindowOpen() && !_gameOver;
                // Mirror TrySellTower's meta-aware refund so the label never
                // promises a different number than the payout.
                var refundPreview = Mathf.FloorToInt(
                    tower.TotalInvested * TDMetaUpgradeSystem.GetSellRefundRatio(GetMetaRank(TDMetaUpgradeSystem.UpgradeLine.B)));
                SetUiText(_uiSellTowerButtonText,
                    TDLocalization.IsChinese
                        ? $"拆塔  +{refundPreview}"
                        : $"SELL  +{refundPreview}");
            }
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

            // LoadCampaignContext runs at boot and sets _missionBoardOpen=true
            // before the title screen exists; without this guard the board
            // bleeds through the title's translucent background around the
            // menu buttons (player report 09-03).
            var titleVisible = _titleScreen != null && _titleScreen.IsVisible;
            var boardVisible = _missionBoardOpen && !titleVisible;

            if (_uiMissionBoardScrim != null)
            {
                _uiMissionBoardScrim.gameObject.SetActive(boardVisible);
            }

            _uiMissionBoardRoot.gameObject.SetActive(boardVisible);
            if (_uiFormationRoot != null)
            {
                _uiFormationRoot.gameObject.SetActive(boardVisible && _formationPanelOpen);
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

    }
}
