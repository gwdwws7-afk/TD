// Freeze-period move: Campaign cluster.
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

            // Teaching copy step 0 (resonance-teaching-copy-v1): the L16
            // briefing carries the worldbuilding line for the resonance
            // system's debut level (once per save slot).
            if (level.levelIndex >= 16)
            {
                ShowResonanceTipOnce(
                    "briefing",
                    "The fire in your line never died. From this level on, every hit banks an ember — and when the gauge fills, you get to light it yourself.",
                    "防线的火没有熄。从这一关起，每一次命中都会积攒余烬——攒满的那一刻，你可以亲手点燃它。",
                    10.0f);
            }

            // Pause the game while briefing is up
            SetBattlePlaybackSpeed(0f, false);
            _missionBriefing.Show(levelTitle, mapHook, scenarioIntel, threatIntel: threatLines, contractIntel);
            PlaySfxTone("ui_panel_open", 540f, 0.10f, 0.52f, true);
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

            // Show the full-screen world map for level selection.
            if (_worldMap != null)
            {
                _missionBoardSelectedLevel = _campaignRoute?.level?.levelIndex ?? 1;
                RefreshWorldMap();
                _worldMap.Show();
                PlaySfxTone("ui_panel_open", 540f, 0.10f, 0.52f, true);
            }
        }

        private void RefreshWorldMap()
        {
            if (_worldMap == null || _campaign == null) return;
            var totalLevels = _campaign.totalLevels;
            var highestUnlocked = TDCampaignProgression.GetHighestUnlockedLevel(totalLevels);
            var clearedArr = new bool[totalLevels];
            var starsArr = new int[totalLevels];
            var difficultyArr = new int[totalLevels];
            for (var lvl = 1; lvl <= totalLevels; lvl++)
            {
                var prog = TDCampaignProgression.GetLevelProgress(lvl);
                clearedArr[lvl - 1] = prog.cleared;
                starsArr[lvl - 1] = prog.bestStars;
                difficultyArr[lvl - 1] = prog.highestDifficultyCleared;
            }
            _worldMap.Refresh(_missionBoardSelectedLevel, highestUnlocked, clearedArr, starsArr, totalLevels, 20, difficultyArr);
        }

        /// <summary>
        /// Ensure the wave loop coroutine is running. If it died (e.g. due to a
        /// transient null during title-screen wait), restart it.
        /// </summary>
        private void EnterLevelInPlace()
        {
            // Destroy old board (paths, build sites, grid).
            var oldBoard = transform.Find("Board");
            if (oldBoard != null) Destroy(oldBoard.gameObject);

            // Clear all active enemies (released to the pool — mid-death
            // corpses included; Initialize resets them on next Get).
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                if (_activeEnemies[i] != null) _activeEnemies[i].ReleaseToPool();
            }
            _activeEnemies.Clear();

            // Destroy all towers (they're children of the Board or the transform).
            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            foreach (var tower in towers)
            {
                if (tower != null) Destroy(tower.gameObject);
            }

            // Destroy stray projectiles and FX.
            var projectiles = FindObjectsByType<TDProjectile>(FindObjectsSortMode.None);
            foreach (var proj in projectiles)
            {
                if (proj != null) Destroy(proj.gameObject);
            }

            // Reset ALL per-run state (outcome flags, stats, telemetry).
            ResetRunState();

            // Reload all level data.
            LoadCampaignContext();
            LoadEnemyCatalog();
            LoadWaveConfig();
            RefreshUnlockedTowerKinds();

            // Rebuild the board for the new level (paths + build sites + grid).
            _gridMap = null;
            BuildBoard();

            // Deploy and start wave loop. The old routine may already be dead
            // (victory ends it naturally; defeat stops it) — its Coroutine
            // reference stays non-null and would block the restart below, and
            // a live one would keep using the previous level's wave array.
            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
                _waveRoutine = null;
            }

            _campaignDeploymentConfirmed = true;
            EnsureWaveRoutineRunning();

            // Hide game over UI if it was visible.
            if (_uiGameOverRoot != null) _uiGameOverRoot.gameObject.SetActive(false);

            // Show the mission briefing.
            ShowMissionBriefing();
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
                var difficultyArr = new int[totalLevels];
                for (var lvl = 1; lvl <= totalLevels; lvl++)
                {
                    var prog = TDCampaignProgression.GetLevelProgress(lvl);
                    clearedArr[lvl - 1] = prog.cleared;
                    starsArr[lvl - 1] = prog.bestStars;
                    difficultyArr[lvl - 1] = prog.highestDifficultyCleared;
                }

                _worldMap.Refresh(
                    _missionBoardSelectedLevel,
                    highestUnlocked,
                    clearedArr,
                    starsArr,
                    totalLevels,
                    20,
                    difficultyArr);
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

            if (selectedLevel == _campaignRoute.level.levelIndex && !_gameOver)
            {
                CloseMissionBoard();
                return;
            }

            // Deploy WITHOUT scene reload — switch level data in-place.
            TDCampaignRouter.SaveLevelIndex(selectedLevel);
            PlaySfxTone("ui_deploy", 700f, 0.16f, 0.66f, true);

            // Close all overlay panels.
            _missionBoardOpen = false;
            _formationPanelOpen = false;
            _campaignProfileOpen = false;
            if (_uiFormationRoot != null) _uiFormationRoot.gameObject.SetActive(false);
            if (_uiMissionBoardRoot != null) _uiMissionBoardRoot.gameObject.SetActive(false);
            _worldMap?.Hide();

            // Full level reset + rebuild in-place.
            EnterLevelInPlace();
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
            EnterLevelInPlace();
        }

        private TDCampaignProgressSummary GetCampaignProgressSummary()
        {
            return TDCampaignProgression.BuildSummary(_campaign?.totalLevels ?? 1);
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
            // Meta line A (Logistics Reserve) rides the default baseline so
            // the flat bonus dilutes naturally against level costs; mission
            // and chapter rules keep applying on top unchanged.
            _startingDefenseBudget = ConfigDefaultDefenseBudget +
                                     TDMetaUpgradeSystem.GetStartingBudgetBonus(GetMetaRank(TDMetaUpgradeSystem.UpgradeLine.A));
            _startingLineIntegrity = ConfigDefaultLineIntegrity;
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

    }
}
