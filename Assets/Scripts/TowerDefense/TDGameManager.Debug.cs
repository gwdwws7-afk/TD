// Freeze-period S1: the automation/debug region moved verbatim from
// TDGameManager.cs (one #if block, zero behavior change). Keep new debug
// code here — not in the main file.
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
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
            // Automation may deploy locked levels (soak runs on the final
            // mission after a fresh reset); unlock explicitly so RecordResult
            // accepts the run. Normal gameplay never routes through here.
            TDCampaignProgression.DebugUnlockThroughLevelForTest(
                _campaignRoute.level.levelIndex,
                _campaignRoute.totalLevels);
            return $"deployed level={_campaignRoute.level.levelIndex} boardOpen={_missionBoardOpen}";
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
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
#endif

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
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
#endif

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
                                      _unlockedTowerKinds.Count <= TDCampaignProgression.MaxFormationTowers;
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
            var towerIconsReady = _unlockedTowerKinds.Count > 0 &&
                                  _unlockedTowerKinds.All(kind => Resources.Load<Sprite>(TDUiVisualIdentity.GetTower(kind).iconResourcePath) != null);
            var formationIconsReady = _uiFormationTowerIcons.Count == towerKinds.Length &&
                                      _uiFormationTowerIcons.All(image => image != null && image.sprite != null);
            var identityPass = iconPaths.Count == towerKinds.Length &&
                               roleLabels.Count == towerKinds.Length &&
                               identityColors.Count == towerKinds.Length;

            // Canonical role sizes (GetUiRoleFontSize): Caption 11, Body 12,
            // Metric 13, PanelTitle 15, SectionTitle 17, ScreenTitle 20. The
            // old expectation set {10,11,12,14,16,20} predates the role system
            // and failed every role-remapped label (14->15, 12->13).
            var expectedBaseSizes = new HashSet<int> { 11, 12, 13, 15, 17, 20 };
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
            typographyLabels.AddRange(_uiFormationTowerButtonTexts);
            var worldFont = Resources.Load<Font>(TDUiWorldSkin.FontPath);
            // ResolveFont swaps in the CJK face in Chinese sessions — the
            // canonical-font intent is "labels use the canonical font for the
            // active language", not the latin face specifically.
            var expectedFont = TDLocalization.ResolveFont(worldFont);
            var worldFontReady = expectedFont != null && typographyLabels
                .Where(label => label != null)
                .All(label => label.font == expectedFont);
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
                // Best-fit labels self-shrink to their rect — preferredHeight
                // overflow is meaningless for them (e.g. the HUD title at the
                // canonical 15pt CJK line height in its 20px row).
                if (!label.resizeTextForBestFit &&
                    label.preferredHeight > label.rectTransform.rect.height + 1.5f)
                {
                    overflow.Add(label.name);
                }
            }

            var pass = missingResources.Count == 0 && metricIconsReady && towerIconsReady && formationIconsReady &&
                       identityPass && worldFontReady && typographyPass && overflow.Count == 0;
            return
                $"p11.1.audit.resources={(missingResources.Count == 0 ? "ready" : string.Join(",", missingResources))}\n" +
                $"p11.1.audit.metricIcons={metricIconsReady}\n" +
                $"p11.1.audit.towerIcons={towerIconsReady} [{_unlockedTowerKinds.Count}/{_unlockedTowerKinds.Count}]\n" +
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
                                     maxGroundContactDeviation <= 0.02f && visibleRouteLines == 0;
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
                              maxGroundContactDeviation <= 0.02f;
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
                            maxGroundContactDeviation <= 0.02f &&
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
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
#endif

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
        public string DebugAuditP102ForTest()
        {
            return TDBalanceSimulator.BuildAuditText(TDBalanceSimulator.RunMatrix());
        }
#endif

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

#endif
    }
}

// S1 verify
