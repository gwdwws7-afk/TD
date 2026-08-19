#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TD
{
    [Serializable]
    public sealed class TDP124TowerResult
    {
        public string towerId;
        public string towerKind;
        public int cellX;
        public int cellY;
        public int spend;
        public int upgrades;
        public int damage;
        public int kills;
        public int controls;
        public float damageSharePct;
        public float valuePerBudget;
    }

    [Serializable]
    public sealed class TDP124RealRunReport
    {
        public string schemaVersion;
        public string mode;
        public string generatedUtc;
        public int levelIndex;
        public string levelId;
        public string mapId;
        public string difficultyId;
        public string strategyId;
        public int siteVariant;
        public bool completed;
        public bool stalled;
        public bool victory;
        public int restartCount;
        public float realDurationSeconds;
        public float simulationDurationSeconds;
        public int currentWave;
        public int waveCount;
        public int wavesCleared;
        public int firstLeakWave;
        public int openingEscapes;
        public int kills;
        public int escapes;
        public int integrityRemaining;
        public int endingBudget;
        public int combatIncome;
        public int clearIncome;
        public int reinforcementIncome;
        public int resonanceIncome;
        public int scenarioSpend;
        public int finalFiveStartWave;
        public int finalFiveStartingBudget;
        public int finalFiveGrossIncome;
        public int finalFiveSpend;
        public int finalFivePurchases;
        public float finalFiveSpendConversionPct;
        public int firstSaturatedWave;
        public bool economyDecisionValue;
        public float towerPowerMultiplier;
        public int towersBuilt;
        public int availableTowerKinds;
        public int towerKindsUsed;
        public int upgradesPurchased;
        public int scenarioOpportunities;
        public int scenarioUses;
        public float scenarioConversionPct;
        public int resonanceWindows;
        public int resonanceCommands;
        public int convergenceTriggers;
        public int coverageScore;
        public int counterScore;
        public int outputScore;
        public int economyScore;
        public int commandScore;
        public int totalScore;
        public string grade;
        public string hottestRoute;
        public string topTowerKind;
        public float topTowerKindDamageSharePct;
        public string topTowerKindByContribution;
        public float topTowerKindContributionSharePct;
        public string topSite;
        public float topSiteDamageSharePct;
        public bool analyticsConsistent;
        public string[] failureReasons;
        public string[] recommendations;
        public TDP125WaveEconomyResult[] finalFiveEconomy;
        public TDP124TowerResult[] towers;
    }

    public sealed partial class TDGameManager
    {
        private const float P124DefaultMaxRealSeconds = 95f;
        private const float P124OpeningEndpointCoverageBonus = 2.5f;
        private const float P124EstablishedEndpointCoverageBonus = 6f;
        private const float P124CenterVariantProgressWeight = 0.25f;
        private const float P124DepthVariantProgressWeight = 0.45f;
        private bool _p124AutoplayEnabled;
        private bool _p124AutoplayTerminal;
        private bool _p124AutoplayStalled;
        private string _p124StrategyId = "adaptive_network";
        private int _p124SiteVariant;
        private int _p124HandledPrepWave = -1;
        private int _p124CommittedPrepWave = -1;
        private readonly Dictionary<string, int> _p124TowerFirstWave = new(StringComparer.OrdinalIgnoreCase);
        private int _p124HandledScenarioWave = -1;
        private float _p124RunStartRealtime;
        private float _p124RunStartSimulationTime;
        private float _p124MaxRealSeconds = P124DefaultMaxRealSeconds;
        private float _p124NextCombatDecisionTime;

        public bool IsP124AutoplayTerminal => _p124AutoplayTerminal || _gameOver;

        public string DebugStartP124AutoplayForTest(
            string strategyId = "adaptive_network",
            int siteVariant = 0,
            float maxRealSeconds = P124DefaultMaxRealSeconds)
        {
            // If the title screen is blocking, auto-dismiss it for automation.
            if (_titleScreen != null && _titleScreen.IsVisible)
            {
                _titleScreen.Hide();
                _campaignDeploymentConfirmed = true;
                _missionBoardOpen = false;
                _formationPanelOpen = false;
                _campaignProfileOpen = false;
                EnsureWaveRoutineRunning();
            }

            if (!_campaignDeploymentConfirmed || _gridMap == null || _campaignRoute?.level == null)
            {
                return "skip: P12.4 requires a deployed campaign mission";
            }

            var normalizedStrategy = NormalizeGroupToken(strategyId);
            if (normalizedStrategy != "focused_fire" && normalizedStrategy != "control_lattice" &&
                normalizedStrategy != "adaptive_network")
            {
                return $"skip: unknown P12.4 strategy {strategyId}";
            }

            ConfigureP124Formation(normalizedStrategy);
            _p124StrategyId = normalizedStrategy;
            _p124SiteVariant = Mathf.Clamp(siteVariant, 0, 2);
            _p124HandledPrepWave = -1;
            _p124CommittedPrepWave = -1;
            _p124HandledScenarioWave = -1;
            _p124RunStartRealtime = Time.realtimeSinceStartup;
            _p124RunStartSimulationTime = Time.time;
            _p124MaxRealSeconds = Mathf.Clamp(maxRealSeconds, 15f, 1800f);
            _p124NextCombatDecisionTime = 0f;
            _p124AutoplayStalled = false;
            _p124AutoplayTerminal = false;
            _p124AutoplayEnabled = true;
            return $"p12.4.autoplay.started=True level={_campaignRoute.level.levelIndex} difficulty={_activeCampaignDifficulty} " +
                   $"strategy={_p124StrategyId} siteVariant={_p124SiteVariant} formation={string.Join(",", _unlockedTowerKinds)}";
        }

        public string DebugPrepareP124RepresentativeProgressionForTest()
        {
            // Auto-dismiss the title screen for automation if it's blocking.
            if (_titleScreen != null && _titleScreen.IsVisible)
            {
                _titleScreen.Hide();
                _campaignDeploymentConfirmed = true;
                _missionBoardOpen = false;
                _formationPanelOpen = false;
                _campaignProfileOpen = false;
                EnsureWaveRoutineRunning();
            }

            if (_campaignRoute?.level == null || _campaign == null)
            {
                return "skip: P12.4 representative progression requires campaign data";
            }

            var targetLevel = _campaignRoute.level.levelIndex;
            for (var level = 1; level < targetLevel; level++)
            {
                TDCampaignProgression.RecordResult(
                    level,
                    true,
                    2,
                    78,
                    15,
                    _campaign.totalLevels,
                    false,
                    TDCampaignDifficultyTier.Standard);
            }

            var claimedRewards = 0;
            foreach (var chapter in _campaign.chapters ?? Array.Empty<TDCampaignChapterDefinition>())
            {
                if (chapter?.reward == null || chapter.endLevel >= targetLevel)
                {
                    continue;
                }

                if (TDCampaignProgression.ClaimChapterReward(chapter.reward.rewardId) ||
                    TDCampaignProgression.IsChapterRewardClaimed(chapter.reward.rewardId))
                {
                    claimedRewards++;
                }
            }

            TDCampaignProgression.SaveTacticalProtocol(targetLevel, "baseline");
            _activeTacticalProtocol = GetTacticalProtocol("baseline");
            ResetMissionRuntimeRules();
            ApplyMissionRuntimeRules(_campaignRoute.level);
            return $"p12.4.progression.prepared=True priorClears={Mathf.Max(0, targetLevel - 1)} " +
                   $"rewards={claimedRewards} budget={_startingDefenseBudget} integrity={_startingLineIntegrity}";
        }

        private void ConfigureP124Formation(string strategyId)
        {
            var priority = ApplyP135FormationPriority(GetP124TowerPriority(strategyId));
            if ((_campaignRoute?.level?.levelIndex ?? 0) == 17 && strategyId == "focused_fire")
            {
                priority = new[]
                {
                    TDTowerKind.RailLancer, TDTowerKind.EmberFlak,
                    TDTowerKind.FrostCoil, TDTowerKind.ResonanceBeacon,
                    TDTowerKind.SiegeDrill, TDTowerKind.CinderMortar,
                    TDTowerKind.ArcWelder, TDTowerKind.GravSnare
                };
            }

            _unlockedTowerKinds.Clear();
            for (var i = 0; i < priority.Length && _unlockedTowerKinds.Count < TDCampaignProgression.MaxFormationTowers; i++)
            {
                if (_availableTowerKinds.Contains(priority[i]) && !_unlockedTowerKinds.Contains(priority[i]))
                {
                    _unlockedTowerKinds.Add(priority[i]);
                }
            }

            for (var i = 0; i < _availableTowerKinds.Count && _unlockedTowerKinds.Count < TDCampaignProgression.MaxFormationTowers; i++)
            {
                if (!_unlockedTowerKinds.Contains(_availableTowerKinds[i]))
                {
                    _unlockedTowerKinds.Add(_availableTowerKinds[i]);
                }
            }

            // Armor-aware formation: buildableKinds draws from this four-slot
            // formation, and SiegeDrill sits 7th in the adaptive priority —
            // without this swap the armor quota in ResolveP124BuildKind can
            // never see it on armor-dominant maps (L13/L20 evidence, 2026-08-19:
            // quota live but formation membership zero).
            if (_availableTowerKinds.Contains(TDTowerKind.SiegeDrill) &&
                !_unlockedTowerKinds.Contains(TDTowerKind.SiegeDrill) &&
                _unlockedTowerKinds.Count > 0 &&
                IsP124ArmorDominantLevel())
            {
                _unlockedTowerKinds[_unlockedTowerKinds.Count - 1] = TDTowerKind.SiegeDrill;
            }

            if (_unlockedTowerKinds.Count == 0)
            {
                _unlockedTowerKinds.Add(TDTowerKind.RailLancer);
            }

            _activeResonanceDoctrine = strategyId == "focused_fire"
                ? TDResonanceDoctrine.EmberSurge
                : strategyId == "control_lattice"
                    ? TDResonanceDoctrine.FractureMark
                    : TDResonanceDoctrine.Adaptive;
            _selectedTowerKind = _unlockedTowerKinds[0];
            RebuildTowerBuildButtons();
        }

        private void UpdateP124Autoplay()
        {
            if (!_p124AutoplayEnabled)
            {
                return;
            }

            if (_gameOver)
            {
                _p124AutoplayEnabled = false;
                _p124AutoplayTerminal = true;
                return;
            }

            if (Time.realtimeSinceStartup - _p124RunStartRealtime >= _p124MaxRealSeconds)
            {
                _p124AutoplayEnabled = false;
                _p124AutoplayStalled = true;
                _p124AutoplayTerminal = true;
                return;
            }

            if (_missionBoardOpen || !_campaignDeploymentConfirmed || _settingsPanel != null && _settingsPanel.IsOpen)
            {
                return;
            }

            if (_isInPrepPhase)
            {
                if (_p124HandledPrepWave != _wave)
                {
                    _p124HandledPrepWave = _wave;
                    TryUseP124ScenarioCommand(true);
                }

                if (_scenarioReinforcementPending || _p124CommittedPrepWave == _wave)
                {
                    return;
                }

                _p124CommittedPrepWave = _wave;
                TrySellP124IdleTowers();
                SpendP124PrepBudget();
                TryRequestWaveStart();
                return;
            }

            if (Time.unscaledTime < _p124NextCombatDecisionTime)
            {
                return;
            }

            _p124NextCombatDecisionTime = Time.unscaledTime + 0.08f;
            TryUseP124ScenarioCommand(false);
            TryUseP124ResonanceCommand();
        }

        private void TrySellP124IdleTowers()
        {
            // Prep-window only. Autoplay sells towers that stood through several
            // waves without a single hit — the p13.5 zero-contribution finding
            // (SiegeDrill 44.6%, RailLancer 30.4%) is a placement failure, and
            // the 60% refund turns that dead investment back into decisions.
            // One sale per prep keeps composition from thrashing.
            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None)
                .Where(tower => tower != null && tower.gameObject != null)
                .ToList();
            if (towers.Count <= 1)
            {
                return;
            }

            TDTower worst = null;
            var worstAge = 0;
            foreach (var tower in towers)
            {
                var stat = GetOrCreateTowerStat(tower);
                if (stat == null || stat.hits > 0 || stat.damageDealt > 0 || stat.controlApplications > 0)
                {
                    continue;
                }

                if (!_p124TowerFirstWave.TryGetValue(stat.towerId, out var firstWave))
                {
                    _p124TowerFirstWave[stat.towerId] = _wave;
                    continue;
                }

                var age = _wave - firstWave;
                if (age >= 3 && age > worstAge)
                {
                    worst = tower;
                    worstAge = age;
                }
            }

            if (worst != null)
            {
                TrySellTower(worst);
            }
        }

        private void SpendP124PrepBudget()
        {
            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None)
                .Where(tower => tower != null)
                .ToList();
            var levelOpening = 3 + Mathf.Max(0, (_campaignRoute?.level?.levelIndex ?? 1) - 1) / 6;
            var activeLaneCount = Mathf.Max(1, BuildWavePreviewLaneKeys(_currentWaveDefinition).Count);
            var strategyBuildLimit = _gridMap.RecommendedBuildSpotCount;
            if (_p124StrategyId == "control_lattice")
            {
                var lateExpansion = _wave >= 16 ? 4 : _wave >= 12 ? 2 : 0;
                strategyBuildLimit = Mathf.Min(
                    strategyBuildLimit,
                    Mathf.Clamp(activeLaneCount * 2 + 2, 6, 8) + lateExpansion);
            }
            var targetTowerCount = Mathf.Clamp(
                Mathf.Max(levelOpening + Mathf.Max(0, _wave), activeLaneCount * 2),
                1,
                strategyBuildLimit);
            var actions = 0;
            while (actions < 8)
            {
                var coverage = CalculateRouteCoverageScore(_currentWaveDefinition, towers.ToArray());
                var shouldBuild = towers.Count < targetTowerCount ||
                                  coverage < 72 && towers.Count < strategyBuildLimit;
                if (shouldBuild && TryBuildP124Tower(towers))
                {
                    actions++;
                    continue;
                }

                if (towers.Count >= 2 && TryUpgradeP124Tower(towers))
                {
                    actions++;
                    continue;
                }

                if (!shouldBuild && towers.Count < strategyBuildLimit &&
                    _wave >= 12 && TryBuildP124Tower(towers))
                {
                    actions++;
                    continue;
                }

                break;
            }
        }

        private bool TryBuildP124Tower(List<TDTower> towers)
        {
            var priority = ApplyP135FormationPriority(GetP124TowerPriority(_p124StrategyId));
            var buildableKinds = priority
                .Where(kind => _unlockedTowerKinds.Contains(kind) && TDTower.GetBuildCost(kind) <= _defenseBudget)
                .ToArray();
            if (buildableKinds.Length == 0)
            {
                return false;
            }

            var kind = ApplyP135BuildKind(buildableKinds, towers, ResolveP124BuildKind(buildableKinds, towers));
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
            var lastEmberTerminus = string.Equals(
                _campaignRoute?.map?.mapId,
                "last_ember_terminus",
                StringComparison.OrdinalIgnoreCase);
            var cinderLimit = levelIndex <= 5 ||
                              lastEmberTerminus && _p124StrategyId == "adaptive_network"
                ? 1
                : 2;
            var cinderCount = towers.Count(tower => tower != null && tower.Kind == TDTowerKind.CinderMortar);
            if (kind == TDTowerKind.CinderMortar && cinderCount >= cinderLimit)
            {
                var alternative = buildableKinds
                    .Where(candidate => candidate != TDTowerKind.CinderMortar)
                    .OrderBy(candidate => towers.Count(tower => tower != null && tower.Kind == candidate))
                    .ThenBy(TDTower.GetBuildCost)
                    .ThenBy(candidate => candidate)
                    .ToArray();
                if (alternative.Length > 0)
                {
                    kind = alternative[0];
                }
            }

            var beaconCount = towers.Count(tower =>
                tower != null && tower.Kind == TDTowerKind.ResonanceBeacon);
            var beaconLimit = _p124StrategyId == "focused_fire" ? 1 : 2;
            var limitBeaconRepetition = string.Equals(
                                            _campaignRoute?.map?.mapId,
                                            "hollow_kiln_basin",
                                            StringComparison.OrdinalIgnoreCase) ||
                                        lastEmberTerminus && _p124StrategyId == "focused_fire";
            if (limitBeaconRepetition &&
                kind == TDTowerKind.ResonanceBeacon && beaconCount >= beaconLimit)
            {
                var alternative = buildableKinds
                    .Where(candidate => candidate != TDTowerKind.ResonanceBeacon)
                    .OrderBy(candidate => towers.Count(tower => tower != null && tower.Kind == candidate))
                    .ThenBy(TDTower.GetBuildCost)
                    .ThenBy(candidate => candidate)
                    .ToArray();
                if (alternative.Length > 0)
                {
                    kind = alternative[0];
                }
            }

            var cell = ResolveP124BuildCell(kind, towers);
            if (!cell.HasValue)
            {
                return false;
            }

            var result = DebugBuildTowerAtCell(cell.Value.x, cell.Value.y, kind);
            if (!result.StartsWith("built ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var towerTransform = transform.Find($"Tower_{cell.Value.x}_{cell.Value.y}");
            var tower = towerTransform != null ? towerTransform.GetComponent<TDTower>() : null;
            if (tower != null)
            {
                towers.Add(tower);
            }

            return tower != null;
        }

        private TDTowerKind ResolveP124BuildKind(
            IReadOnlyList<TDTowerKind> buildableKinds,
            IReadOnlyList<TDTower> towers)
        {
            var towerCount = towers.Count;
            if (towerCount < 3)
            {
                return ResolveP124OpeningKind(buildableKinds, towers);
            }

            if (_p124StrategyId == "control_lattice")
            {
                var frostCount = towers.Count(tower => tower != null && tower.Kind == TDTowerKind.FrostCoil);
                var controlCandidates = buildableKinds
                    .Where(kind => kind != TDTowerKind.FrostCoil || frostCount < 2)
                    .OrderBy(kind => towers.Count(tower => tower != null && tower.Kind == kind))
                    .ThenBy(kind => kind == TDTowerKind.CinderMortar ? 0 : kind == TDTowerKind.ArcWelder ? 1 : 2)
                    .ThenBy(TDTower.GetBuildCost)
                    .ToArray();
                if (controlCandidates.Length > 0)
                {
                    return controlCandidates[0];
                }
            }

            if (_p124StrategyId == "adaptive_network")
            {
                var armorPressure = CalculateP124WaveTagPressure("armored", "heavy", "boss");
                var swarmPressure = CalculateP124WaveTagPressure("swarm", "spawn", "split");
                var fastPressure = CalculateP124WaveTagPressure("fast", "flank");
                var cinderCount = towers.Count(tower => tower != null && tower.Kind == TDTowerKind.CinderMortar);
                var cinderLimit = Mathf.Max(2, Mathf.CeilToInt((towerCount + 1) * 0.22f));
                if (armorPressure > 0f && armorPressure >= Mathf.Max(swarmPressure, fastPressure))
                {
                    var railCount = towers.Count(tower => tower != null && tower.Kind == TDTowerKind.RailLancer);
                    var siegeCount = towers.Count(tower => tower != null && tower.Kind == TDTowerKind.SiegeDrill);
                    var railLimit = Mathf.Max(2, Mathf.CeilToInt((towerCount + 1) * 0.45f));
                    // Armor pressure earns SiegeDrill a small quota of the armor
                    // response. FirstOrDefault alone routes every armor pick to
                    // RailLancer until its cap saturates, and under the hybrid
                    // armor model the roster needs SiegeDrill's armor break to
                    // stay above the damage floor on armor-dominant waves.
                    var siegeQuota = Mathf.Clamp(Mathf.RoundToInt((towerCount + 1) * 0.2f), 1, 2);
                    var armor = siegeCount < siegeQuota && buildableKinds.Contains(TDTowerKind.SiegeDrill)
                        ? TDTowerKind.SiegeDrill
                        : buildableKinds.FirstOrDefault(kind => kind == TDTowerKind.RailLancer || kind == TDTowerKind.SiegeDrill);
                    if ((armor == TDTowerKind.RailLancer && railCount < railLimit) || armor == TDTowerKind.SiegeDrill)
                    {
                        return armor;
                    }

                    var alternative = buildableKinds
                        .Where(kind => kind != TDTowerKind.RailLancer)
                        .OrderBy(kind => towers.Count(tower => tower != null && tower.Kind == kind))
                        .ThenBy(TDTower.GetBuildCost)
                        .ThenBy(kind => kind)
                        .ToArray();
                    if (alternative.Length > 0)
                    {
                        return alternative[0];
                    }
                }

                if (swarmPressure > 0f && swarmPressure >= fastPressure)
                {
                    var arcCount = towers.Count(tower => tower != null && tower.Kind == TDTowerKind.ArcWelder);
                    var areaOrder = arcCount < cinderCount
                        ? new[] { TDTowerKind.ArcWelder, TDTowerKind.CinderMortar, TDTowerKind.EmberFlak }
                        : new[] { TDTowerKind.CinderMortar, TDTowerKind.ArcWelder, TDTowerKind.EmberFlak };
                    var area = FirstP124Buildable(buildableKinds, areaOrder);
                    if (area == TDTowerKind.CinderMortar && cinderCount >= cinderLimit)
                    {
                        var alternative = buildableKinds
                            .Where(kind => kind != TDTowerKind.CinderMortar)
                            .OrderBy(kind => towers.Count(tower => tower != null && tower.Kind == kind))
                            .ThenBy(TDTower.GetBuildCost)
                            .ThenBy(kind => kind)
                            .ToArray();
                        if (alternative.Length > 0)
                        {
                            return alternative[0];
                        }
                    }

                    if (area == TDTowerKind.CinderMortar || area == TDTowerKind.ArcWelder || area == TDTowerKind.EmberFlak)
                    {
                        return area;
                    }
                }

                if (fastPressure > 0f)
                {
                    var frostCount = towers.Count(tower => tower != null && tower.Kind == TDTowerKind.FrostCoil);
                    var control = frostCount < 2
                        ? FirstP124Buildable(
                            buildableKinds,
                            TDTowerKind.FrostCoil,
                            TDTowerKind.EmberFlak,
                            TDTowerKind.GravSnare)
                        : swarmPressure > 0f && cinderCount < cinderLimit
                            ? FirstP124Buildable(
                                buildableKinds,
                                TDTowerKind.CinderMortar,
                                TDTowerKind.ArcWelder,
                                TDTowerKind.EmberFlak,
                                TDTowerKind.RailLancer)
                            : FirstP124Buildable(
                                buildableKinds,
                                TDTowerKind.ArcWelder,
                                TDTowerKind.EmberFlak,
                                TDTowerKind.RailLancer,
                                TDTowerKind.GravSnare,
                                TDTowerKind.FrostCoil,
                                TDTowerKind.CinderMortar);
                    if (control == TDTowerKind.FrostCoil || control == TDTowerKind.EmberFlak || control == TDTowerKind.GravSnare)
                    {
                        return control;
                    }

                    if (control == TDTowerKind.RailLancer || control == TDTowerKind.ArcWelder ||
                        control == TDTowerKind.CinderMortar)
                    {
                        return control;
                    }
                }
            }

            return buildableKinds[towerCount % buildableKinds.Count];
        }

        private bool IsP124ArmorDominantLevel()
        {
            if (_waveSet?.waves == null || _waveSet.waves.Length == 0)
            {
                return false;
            }

            var armor = CalculateP124LevelTagPressure("armored", "heavy", "boss");
            var swarm = CalculateP124LevelTagPressure("swarm", "spawn", "split");
            var fast = CalculateP124LevelTagPressure("fast", "flank");
            return armor > 0f && armor >= Mathf.Max(swarm, fast);
        }

        // Same weighting as CalculateP124WaveTagPressure, aggregated over the
        // whole wave set — used at formation time, before any wave is active.
        private float CalculateP124LevelTagPressure(params string[] tags)
        {
            if (_waveSet?.waves == null || tags == null || tags.Length == 0)
            {
                return 0f;
            }

            var requested = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            var pressure = 0f;
            for (var w = 0; w < _waveSet.waves.Length; w++)
            {
                var groups = _waveSet.waves[w]?.groups;
                if (groups == null)
                {
                    continue;
                }

                for (var i = 0; i < groups.Length; i++)
                {
                    var group = groups[i];
                    if (group == null || group.count <= 0 ||
                        !_enemyCatalog.TryGetValue(group.enemyId, out var enemy) ||
                        enemy.tags == null || !enemy.tags.Any(requested.Contains))
                    {
                        continue;
                    }

                    pressure += group.count * Mathf.Max(0.1f, enemy.threatCost);
                }
            }

            return pressure;
        }

        private float CalculateP124WaveTagPressure(params string[] tags)
        {
            if (_currentWaveDefinition?.groups == null || tags == null || tags.Length == 0)
            {
                return 0f;
            }

            var requested = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            var pressure = 0f;
            for (var i = 0; i < _currentWaveDefinition.groups.Length; i++)
            {
                var group = _currentWaveDefinition.groups[i];
                if (group == null || group.count <= 0 || !_enemyCatalog.TryGetValue(group.enemyId, out var enemy) ||
                    enemy.tags == null || !enemy.tags.Any(requested.Contains))
                {
                    continue;
                }

                pressure += group.count * Mathf.Max(0.1f, enemy.threatCost);
            }

            return pressure;
        }

        private float CalculateP124LaneCounterPressure(string lane, TDTowerKind kind)
        {
            if (_currentWaveDefinition?.groups == null)
            {
                return 0f;
            }

            var pressure = 0f;
            for (var i = 0; i < _currentWaveDefinition.groups.Length; i++)
            {
                var group = _currentWaveDefinition.groups[i];
                if (group == null || group.count <= 0 ||
                    !ResolvePreviewLaneKeys(group).Contains(lane, StringComparer.OrdinalIgnoreCase) ||
                    !_enemyCatalog.TryGetValue(group.enemyId, out var enemy) ||
                    !P124TowerCountersTags(kind, enemy.tags))
                {
                    continue;
                }

                pressure += group.count * Mathf.Max(0.1f, enemy.threatCost);
            }

            return pressure;
        }

        private static bool P124TowerCountersTags(TDTowerKind kind, IEnumerable<string> tags)
        {
            if (tags == null)
            {
                return false;
            }

            var traits = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            return kind switch
            {
                TDTowerKind.FrostCoil or TDTowerKind.GravSnare =>
                    traits.Overlaps(new[] { "fast", "flank", "gap", "pressure" }),
                TDTowerKind.CinderMortar or TDTowerKind.ArcWelder =>
                    traits.Overlaps(new[] { "swarm", "spawn", "split", "light" }),
                TDTowerKind.EmberFlak =>
                    traits.Overlaps(new[] { "fast", "flank", "swarm", "spawn", "split", "light" }),
                TDTowerKind.RailLancer or TDTowerKind.SiegeDrill =>
                    traits.Overlaps(new[] { "armored", "heavy", "boss", "mid" }),
                _ => false
            };
        }

        private TDTowerKind ResolveP124OpeningKind(
            IReadOnlyList<TDTowerKind> buildableKinds,
            IReadOnlyList<TDTower> towers)
        {
            var hasSwarm = _currentWaveThreatTagSet.Overlaps(new[] { "swarm", "spawn", "split" });
            var hasFast = _currentWaveThreatTagSet.Overlaps(new[] { "fast", "flank" });
            var hasArmor = _currentWaveThreatTagSet.Overlaps(new[] { "armored", "heavy", "boss" });
            var activeLaneCount = Mathf.Max(1, BuildWavePreviewLaneKeys(_currentWaveDefinition).Count);
            var areaTowerCount = towers.Count(tower => tower != null &&
                (tower.Kind == TDTowerKind.CinderMortar ||
                 tower.Kind == TDTowerKind.ArcWelder ||
                 tower.Kind == TDTowerKind.EmberFlak));
            var frostTowerCount = towers.Count(tower => tower != null && tower.Kind == TDTowerKind.FrostCoil);
            if (_p124StrategyId == "focused_fire")
            {
                if (!towers.Any(tower => tower != null && tower.Kind == TDTowerKind.RailLancer))
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.RailLancer,
                        TDTowerKind.SiegeDrill);
                }

                if (hasSwarm && !towers.Any(tower => tower != null &&
                                             (tower.Kind == TDTowerKind.CinderMortar || tower.Kind == TDTowerKind.EmberFlak)))
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.EmberFlak,
                        TDTowerKind.CinderMortar);
                }

                if (hasFast && !towers.Any(tower => tower != null && tower.Kind == TDTowerKind.FrostCoil))
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.FrostCoil,
                        TDTowerKind.EmberFlak);
                }

                return FirstP124Buildable(
                    buildableKinds,
                    TDTowerKind.RailLancer,
                    TDTowerKind.EmberFlak,
                    TDTowerKind.SiegeDrill,
                    TDTowerKind.ResonanceBeacon,
                    TDTowerKind.CinderMortar,
                    TDTowerKind.FrostCoil);
            }

            if (_p124StrategyId == "control_lattice")
            {
                var fastPressure = CalculateP124WaveTagPressure("fast", "flank");
                var swarmPressure = CalculateP124WaveTagPressure("swarm", "spawn", "split");
                var fastDominates = fastPressure >= Mathf.Max(0.1f, swarmPressure);
                const int controlAreaTarget = 1;
                if (hasFast && fastDominates && frostTowerCount == 0)
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.FrostCoil,
                        TDTowerKind.GravSnare,
                        TDTowerKind.EmberFlak);
                }

                if (hasSwarm && areaTowerCount < controlAreaTarget)
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.CinderMortar,
                        TDTowerKind.ArcWelder,
                        TDTowerKind.EmberFlak,
                        TDTowerKind.GravSnare);
                }

                if (hasFast && frostTowerCount == 0)
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.FrostCoil,
                        TDTowerKind.GravSnare,
                        TDTowerKind.EmberFlak);
                }

                if (!towers.Any(tower => tower != null && tower.Kind == TDTowerKind.RailLancer))
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.RailLancer,
                        TDTowerKind.SiegeDrill);
                }

                if (hasArmor && !towers.Any(tower => tower != null &&
                                     (tower.Kind == TDTowerKind.RailLancer ||
                                      tower.Kind == TDTowerKind.SiegeDrill)))
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.RailLancer,
                        TDTowerKind.SiegeDrill);
                }

                return FirstP124Buildable(
                    buildableKinds,
                    TDTowerKind.CinderMortar,
                    TDTowerKind.ArcWelder,
                    TDTowerKind.FrostCoil,
                    TDTowerKind.RailLancer);
            }

            if (towers.Count == 0)
            {
                if (hasSwarm)
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.CinderMortar,
                        TDTowerKind.ArcWelder,
                        TDTowerKind.EmberFlak,
                        TDTowerKind.GravSnare);
                }

                if (hasFast)
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.FrostCoil,
                        TDTowerKind.EmberFlak,
                        TDTowerKind.GravSnare,
                        TDTowerKind.RailLancer);
                }

                if (hasArmor)
                {
                    return FirstP124Buildable(
                        buildableKinds,
                        TDTowerKind.RailLancer,
                        TDTowerKind.SiegeDrill);
                }
            }

            var adaptiveAreaTarget = towers.Count < 3
                ? 1
                : Mathf.Min(2, activeLaneCount + (hasSwarm ? 1 : 0));
            if (hasSwarm && areaTowerCount < adaptiveAreaTarget)
            {
                return FirstP124Buildable(
                    buildableKinds,
                    TDTowerKind.CinderMortar,
                    TDTowerKind.ArcWelder,
                    TDTowerKind.EmberFlak,
                    TDTowerKind.GravSnare,
                    TDTowerKind.RailLancer);
            }

            if (hasFast && frostTowerCount == 0)
            {
                return FirstP124Buildable(
                    buildableKinds,
                    TDTowerKind.FrostCoil,
                    TDTowerKind.EmberFlak,
                    TDTowerKind.GravSnare,
                    TDTowerKind.RailLancer);
            }

            if (!towers.Any(tower => tower != null && tower.Kind == TDTowerKind.RailLancer) &&
                buildableKinds.Contains(TDTowerKind.RailLancer))
            {
                return TDTowerKind.RailLancer;
            }

            if (hasFast)
            {
                return FirstP124Buildable(
                    buildableKinds,
                    TDTowerKind.FrostCoil,
                    TDTowerKind.EmberFlak,
                    TDTowerKind.GravSnare,
                    TDTowerKind.RailLancer);
            }

            if (hasSwarm && areaTowerCount >= adaptiveAreaTarget)
            {
                return FirstP124Buildable(
                    buildableKinds,
                    TDTowerKind.RailLancer,
                    TDTowerKind.FrostCoil,
                    TDTowerKind.EmberFlak,
                    TDTowerKind.GravSnare,
                    TDTowerKind.ArcWelder,
                    TDTowerKind.CinderMortar);
            }

            if (hasSwarm)
            {
                return FirstP124Buildable(
                    buildableKinds,
                    TDTowerKind.CinderMortar,
                    TDTowerKind.ArcWelder,
                    TDTowerKind.EmberFlak,
                    TDTowerKind.GravSnare,
                    TDTowerKind.RailLancer);
            }

            return FirstP124Buildable(buildableKinds, GetP124TowerPriority(_p124StrategyId));
        }

        private static TDTowerKind FirstP124Buildable(
            IReadOnlyList<TDTowerKind> buildableKinds,
            params TDTowerKind[] preferredKinds)
        {
            for (var i = 0; i < preferredKinds.Length; i++)
            {
                if (buildableKinds.Contains(preferredKinds[i]))
                {
                    return preferredKinds[i];
                }
            }

            return buildableKinds.OrderBy(TDTower.GetBuildCost).ThenBy(kind => kind).First();
        }

        private Vector2Int? ResolveP124BuildCell(TDTowerKind kind, IReadOnlyList<TDTower> towers)
        {
            Vector2Int? bestCell = null;
            var bestScore = float.MinValue;
            var candidates = _gridMap.RecommendedBuildCells;
            for (var i = 0; i < candidates.Count; i++)
            {
                var cell = candidates[i];
                if (!_gridMap.IsBuildable(cell))
                {
                    continue;
                }

                var outputTower = kind == TDTowerKind.RailLancer ||
                                  kind == TDTowerKind.CinderMortar ||
                                  kind == TDTowerKind.ArcWelder ||
                                  kind == TDTowerKind.SiegeDrill ||
                                  kind == TDTowerKind.EmberFlak;
                var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
                var lastEmberTerminus = string.Equals(
                    _campaignRoute?.map?.mapId,
                    "last_ember_terminus",
                    StringComparison.OrdinalIgnoreCase);
                var hollowKiln = string.Equals(
                    _campaignRoute?.map?.mapId,
                    "hollow_kiln_basin",
                    StringComparison.OrdinalIgnoreCase);
                var entryOutputTower = outputTower ||
                                       hollowKiln && kind == TDTowerKind.ResonanceBeacon ||
                                       lastEmberTerminus && _p124StrategyId == "focused_fire" &&
                                       kind == TDTowerKind.ResonanceBeacon;
                var entryOutputCellLimit = hollowKiln &&
                                           _p124StrategyId == "focused_fire" &&
                                           kind == TDTowerKind.ResonanceBeacon
                    ? 1
                    : hollowKiln || lastEmberTerminus && _p124StrategyId == "focused_fire"
                        ? 4
                        : 3;
                if (levelIndex >= 7 && entryOutputTower &&
                    cell.x <= entryOutputCellLimit)
                {
                    continue;
                }

                if (lastEmberTerminus && _p124StrategyId == "adaptive_network" &&
                    kind == TDTowerKind.CinderMortar && cell.x <= 4)
                {
                    continue;
                }

                if (_p124StrategyId == "control_lattice" &&
                    kind == TDTowerKind.CinderMortar &&
                    (_campaignRoute?.level?.levelIndex ?? 0) == 15 &&
                    string.Equals(
                        _campaignRoute?.map?.mapId,
                        "hollow_kiln_basin",
                        StringComparison.OrdinalIgnoreCase) &&
                    cell.x < 8)
                {
                    continue;
                }

                var score = CalculateP124SiteScore(cell, kind, towers);
                if (_p124StrategyId == "control_lattice" &&
                    kind == TDTowerKind.CinderMortar &&
                    (_campaignRoute?.level?.levelIndex ?? 0) == 15 &&
                    string.Equals(
                        _campaignRoute?.map?.mapId,
                        "hollow_kiln_basin",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var mortarIndex = towers.Count(tower =>
                        tower != null && tower.Kind == TDTowerKind.CinderMortar);
                    var anchor = mortarIndex == 0
                        ? new Vector2Int(12, 2)
                        : new Vector2Int(9, 1);
                    var anchorDistance = Mathf.Abs(cell.x - anchor.x) + Mathf.Abs(cell.y - anchor.y);
                    score += Mathf.Max(0f, 8f - anchorDistance) * 40f;
                }
                if (hollowKiln && _p124StrategyId == "focused_fire" &&
                    kind == TDTowerKind.ResonanceBeacon)
                {
                    var anchor = new Vector2Int(4, 6);
                    var anchorDistance = Mathf.Abs(cell.x - anchor.x) + Mathf.Abs(cell.y - anchor.y);
                    score += Mathf.Max(0f, 8f - anchorDistance) * 40f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return bestCell;
        }

        private float CalculateP124SiteScore(Vector2Int cell, TDTowerKind kind, IReadOnlyList<TDTower> towers)
        {
            var world = _gridMap.CellToBuildWorld(cell);
            var range = Mathf.Max(0.5f, TDTower.GetBaseRange(kind));
            var totalCoverage = 0f;
            var incrementalCoverage = 0f;
            var coveredLanes = 0;
            var progressBias = 0f;
            var uncoveredEndpointBonus = 0f;
            var counterInterceptionBonus = 0f;
            var controlledFocusBonus = 0f;
            var scoredPaths = new HashSet<IReadOnlyList<Vector3>>();
            var waveLanePressure = BuildWaveLanePressureMap(_currentWaveDefinition);
            var activeWaveLanes = new HashSet<string>(waveLanePressure.Keys, StringComparer.OrdinalIgnoreCase);
            var maximumLanePressure = Mathf.Max(1, waveLanePressure.Values.DefaultIfEmpty(1).Max());
            var laneCounterPressure = activeWaveLanes.ToDictionary(
                lane => lane,
                lane => CalculateP124LaneCounterPressure(lane, kind),
                StringComparer.OrdinalIgnoreCase);
            var maximumCounterPressure = Mathf.Max(0.1f, laneCounterPressure.Values.DefaultIfEmpty(0f).Max());
            foreach (var pair in _activeLanePaths)
            {
                var path = pair.Value;
                if (activeWaveLanes.Count > 0 && !activeWaveLanes.Contains(pair.Key) ||
                    path == null || path.Count == 0 || !scoredPaths.Add(path))
                {
                    continue;
                }

                var laneWeight = waveLanePressure.TryGetValue(pair.Key, out var lanePressure)
                    ? Mathf.Lerp(0.72f, 1f, lanePressure / (float)maximumLanePressure)
                    : 1f;
                if (laneCounterPressure.TryGetValue(pair.Key, out var counterPressure) && counterPressure > 0f)
                {
                    laneWeight *= Mathf.Lerp(1f, 1.9f, counterPressure / maximumCounterPressure);
                    if (kind == TDTowerKind.FrostCoil || kind == TDTowerKind.GravSnare ||
                        kind == TDTowerKind.EmberFlak)
                    {
                        var interceptionPoint = GetPathPointAtNormalizedProgress(path, 0.4f);
                        var interceptionDistance = Vector2.Distance(world, interceptionPoint);
                        if (interceptionDistance <= range)
                        {
                            counterInterceptionBonus +=
                                (1f - interceptionDistance / range) *
                                36f *
                                counterPressure / maximumCounterPressure;
                        }
                    }
                }

                var samples = 0;
                var covered = 0;
                var incremental = 0;
                var coveredProgress = 0f;
                for (var pointIndex = 0; pointIndex < path.Count - 1; pointIndex++)
                {
                    for (var sampleIndex = 0; sampleIndex < 3; sampleIndex++)
                    {
                        var sampleProgress = sampleIndex / 2f;
                        var point = Vector3.Lerp(path[pointIndex], path[pointIndex + 1], sampleProgress);
                        samples++;
                        if (Vector2.Distance(world, point) > range)
                        {
                            continue;
                        }

                        covered++;
                        coveredProgress += (pointIndex + sampleProgress) / Mathf.Max(1f, path.Count - 1f);
                        var alreadyCovered = false;
                        for (var towerIndex = 0; towerIndex < towers.Count; towerIndex++)
                        {
                            var tower = towers[towerIndex];
                            if (tower != null && Vector2.Distance(tower.transform.position, point) <= tower.AttackRange)
                            {
                                alreadyCovered = true;
                                break;
                            }
                        }

                        if (!alreadyCovered)
                        {
                            incremental++;
                        }
                    }
                }

                if (covered <= 0 || samples <= 0)
                {
                    continue;
                }

                coveredLanes++;
                totalCoverage += covered / (float)samples * laneWeight;
                incrementalCoverage += incremental / (float)samples * laneWeight;
                var averageProgress = coveredProgress / covered;
                progressBias += _p124SiteVariant == 1
                    ? 1f - averageProgress
                    : _p124SiteVariant == 2
                        ? averageProgress
                        : 1f - Mathf.Abs(0.55f - averageProgress);

                var entrance = GetPathPointAtNormalizedProgress(path, 0f);
                var exit = GetPathPointAtNormalizedProgress(path, 1f);
                var endpointCoverageBonus = towers.Count == 0
                    ? P124OpeningEndpointCoverageBonus
                    : P124EstablishedEndpointCoverageBonus;
                if (Vector2.Distance(world, exit) <= range && !IsP124PointCovered(exit, towers))
                {
                    uncoveredEndpointBonus += endpointCoverageBonus * laneWeight;
                }
                if (Vector2.Distance(world, entrance) <= range && !IsP124PointCovered(entrance, towers))
                {
                    uncoveredEndpointBonus += endpointCoverageBonus * 0.8f * laneWeight;
                }
            }

            var spacing = 0f;
            for (var i = 0; i < towers.Count; i++)
            {
                spacing += Mathf.Clamp(Vector2.Distance(world, towers[i].transform.position), 0f, 4f) * 0.08f;
                if ((kind == TDTowerKind.RailLancer || kind == TDTowerKind.CinderMortar ||
                     kind == TDTowerKind.ArcWelder || kind == TDTowerKind.SiegeDrill) &&
                    (towers[i].Kind == TDTowerKind.FrostCoil ||
                     towers[i].Kind == TDTowerKind.GravSnare))
                {
                    var overlapRadius = Mathf.Max(
                        0.5f,
                        Mathf.Min(range, towers[i].AttackRange) * 1.45f);
                    var controlDistance = Vector2.Distance(world, towers[i].transform.position);
                    if (controlDistance <= overlapRadius)
                    {
                        controlledFocusBonus += (1f - controlDistance / overlapRadius) * 28f;
                    }
                }
            }

            var strategyBias = _p124StrategyId == "focused_fire"
                ? coveredLanes * 0.9f
                : _p124StrategyId == "control_lattice"
                    ? totalCoverage * 2.2f
                    : coveredLanes * 0.45f + totalCoverage;
            var deterministicTieBreak = ((cell.x * 17 + cell.y * 31 + _p124SiteVariant * 13) % 29) * 0.0001f;
            // Site variants should alter route depth without making the late merge objectively dominant.
            var variantProgressWeight = _p124SiteVariant == 0
                ? P124CenterVariantProgressWeight
                : P124DepthVariantProgressWeight;
            var normalizedHorizontalDepth = (cell.x + 0.5f) / GridWidth;
            var outputTower = kind == TDTowerKind.RailLancer ||
                              kind == TDTowerKind.CinderMortar ||
                              kind == TDTowerKind.ArcWelder ||
                              kind == TDTowerKind.SiegeDrill ||
                              kind == TDTowerKind.EmberFlak;
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
            var lastEmberFocusedBeacon = string.Equals(
                                             _campaignRoute?.map?.mapId,
                                             "last_ember_terminus",
                                             StringComparison.OrdinalIgnoreCase) &&
                                         _p124StrategyId == "focused_fire" &&
                                         kind == TDTowerKind.ResonanceBeacon;
            var entryPenaltyDepth = levelIndex >= 7 ? 0.35f : 0.30f;
            var entryPenaltyWeight = levelIndex >= 7 ? 180f : 72f;
            var repeatedEntryPenalty = _p124SiteVariant != 1 && (outputTower || lastEmberFocusedBeacon)
                ? Mathf.Max(0f, entryPenaltyDepth - normalizedHorizontalDepth) * entryPenaltyWeight
                : 0f;
            return totalCoverage * 8f + incrementalCoverage * 18f + coveredLanes * 1.6f +
                   progressBias * variantProgressWeight + uncoveredEndpointBonus + counterInterceptionBonus +
                   controlledFocusBonus + spacing + strategyBias +
                   GetP135MechanicSiteBias(world, range, kind) +
                   deterministicTieBreak - repeatedEntryPenalty;
        }

        private static bool IsP124PointCovered(Vector3 point, IReadOnlyList<TDTower> towers)
        {
            for (var i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                if (tower != null && Vector2.Distance(tower.transform.position, point) <= tower.AttackRange)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryUpgradeP124Tower(IReadOnlyList<TDTower> towers)
        {
            TDTower best = null;
            TDTowerUpgradeBranch bestBranch = TDTowerUpgradeBranch.Damage;
            var bestScore = float.MinValue;
            for (var i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                if (tower == null || !tower.CanUpgrade)
                {
                    continue;
                }

                var branch = ApplyP135UpgradeBranch(
                    tower.Kind,
                    tower.Tier,
                    ResolveP124UpgradeBranch(tower.Kind, tower.Tier));
                var normalizedTowerDepth = (tower.GridCell.x + 0.5f) / GridWidth;
                var outputTower = tower.Kind == TDTowerKind.RailLancer ||
                                  tower.Kind == TDTowerKind.CinderMortar ||
                                  tower.Kind == TDTowerKind.ArcWelder ||
                                  tower.Kind == TDTowerKind.SiegeDrill ||
                                  tower.Kind == TDTowerKind.EmberFlak;
                var hollowKiln = string.Equals(
                    _campaignRoute?.map?.mapId,
                    "hollow_kiln_basin",
                    StringComparison.OrdinalIgnoreCase);
                var hollowKilnEntry = hollowKiln && normalizedTowerDepth < 0.30f;
                var establishedEntryDepth = hollowKilnEntry ? 0.30f : 0.25f;
                var establishedEntryUpgradeLimit = hollowKilnEntry ? 1 : 2;
                if (towers.Count >= 4 && outputTower &&
                    normalizedTowerDepth < establishedEntryDepth && tower.Tier >= establishedEntryUpgradeLimit)
                {
                    continue;
                }

                var cost = tower.GetUpgradeCost(branch);
                if (cost > _defenseBudget)
                {
                    continue;
                }

                var siteScore = CalculateP124SiteScore(tower.GridCell, tower.Kind, towers);
                var specializationBonus = tower.Tier == 1 ? 2.4f : tower.Tier == 2 ? 1.4f : 0f;
                var contributionTotal = _towerStats.Values
                    .Where(stat => stat != null)
                    .Sum(stat => stat.damageDealt + stat.controlApplications * 8f);
                var towerStat = GetOrCreateTowerStat(tower);
                var towerContribution = towerStat == null
                    ? 0f
                    : towerStat.damageDealt + towerStat.controlApplications * 8f;
                var contributionShare = contributionTotal <= 0.01f
                    ? 0f
                    : towerContribution / contributionTotal;
                var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
                var upgradeDominanceLimit = hollowKiln ? 0.22f : levelIndex >= 7 ? 0.26f : 0.30f;
                var upgradeDominanceTierLimit = hollowKiln && tower.Kind == TDTowerKind.CinderMortar
                    ? 0
                    : hollowKiln
                        ? 1
                        : 2;
                var lastEmberTerminus = string.Equals(
                    _campaignRoute?.map?.mapId,
                    "last_ember_terminus",
                    StringComparison.OrdinalIgnoreCase);
                var releaseLastEmberLateUpgrade = lastEmberTerminus && levelIndex >= 17 &&
                                                   (_p124StrategyId == "focused_fire" ||
                                                    _p124StrategyId == "adaptive_network") &&
                                                   _wave >= TDEconomyTuning.GetFinalFiveStartWave(GetConfiguredWaveCount());
                if (contributionTotal >= 400f && towers.Count >= 4 &&
                    contributionShare > upgradeDominanceLimit &&
                    tower.Tier >= upgradeDominanceTierLimit &&
                    !releaseLastEmberLateUpgrade)
                {
                    continue;
                }

                var dominancePenalty = contributionTotal >= 400f && towers.Count >= 3
                    ? Mathf.Max(0f, contributionShare - 0.28f) * 60f
                    : 0f;
                var idlePenalty = _wave >= 3 && contributionTotal >= 400f && towerContribution <= 0.01f
                    ? 14f
                    : 0f;
                var score = siteScore + specializationBonus - tower.Tier * 1.8f -
                            dominancePenalty - idlePenalty;
                if (score > bestScore)
                {
                    best = tower;
                    bestBranch = branch;
                    bestScore = score;
                }
            }

            if (best == null)
            {
                return false;
            }

            var tier = best.Tier;
            TryUpgradeTower(best, bestBranch);
            return best.Tier > tier;
        }

        private TDTowerUpgradeBranch ResolveP124UpgradeBranch(TDTowerKind kind, int tier)
        {
            if (_p124StrategyId == "focused_fire")
            {
                return TDTowerUpgradeBranch.Damage;
            }

            if (_p124StrategyId == "control_lattice")
            {
                return kind == TDTowerKind.FrostCoil || kind == TDTowerKind.ArcWelder ||
                       kind == TDTowerKind.ResonanceBeacon || kind == TDTowerKind.GravSnare
                    ? TDTowerUpgradeBranch.Utility
                    : TDTowerUpgradeBranch.Damage;
            }

            if (kind == TDTowerKind.FrostCoil || kind == TDTowerKind.ArcWelder ||
                kind == TDTowerKind.ResonanceBeacon || kind == TDTowerKind.GravSnare)
            {
                return TDTowerUpgradeBranch.Utility;
            }

            return tier == 2 ? TDTowerUpgradeBranch.Utility : TDTowerUpgradeBranch.Damage;
        }

        private void TryUseP124ScenarioCommand(bool duringPrep)
        {
            if (_activeScenarioMechanic == null || _p124HandledScenarioWave == _wave)
            {
                return;
            }

            var opportunity = string.Equals(_currentWavePhase, "reinforce", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(_currentWavePhase, "exam", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(_currentWavePhase, "boss", StringComparison.OrdinalIgnoreCase);
            if (!opportunity)
            {
                return;
            }

            var mechanicType = NormalizeGroupToken(_activeScenarioMechanic.mechanicType);
            var prepMechanic = mechanicType == "signal_gate" || mechanicType == "route_switch" ||
                               mechanicType == "timed_reinforcement";
            if (prepMechanic != duringPrep)
            {
                return;
            }

            var defenseCritical = _lineIntegrity <= Mathf.CeilToInt(_startingLineIntegrity * 0.70f);
            var decisivePhase = string.Equals(_currentWavePhase, "exam", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(_currentWavePhase, "boss", StringComparison.OrdinalIgnoreCase);
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 0;
            var lastEmberTerminus = string.Equals(
                _campaignRoute?.map?.mapId,
                "last_ember_terminus",
                StringComparison.OrdinalIgnoreCase);
            var finalFiveStartWave = TDEconomyTuning.GetFinalFiveStartWave(GetConfiguredWaveCount());
            var lastEmberFocusedEconomy = lastEmberTerminus && _p124StrategyId == "focused_fire" &&
                                          (levelIndex == 17 && _wave >= 11 ||
                                           levelIndex >= 18 && _wave >= finalFiveStartWave) &&
                                          _defenseBudget >= Mathf.RoundToInt(TDEconomyTuning.DecisionReserveLimit * 0.30f);
            var shouldUse = _p124StrategyId == "control_lattice" ||
                            _p124StrategyId == "adaptive_network" &&
                            (defenseCritical || decisivePhase || _wave % 2 == 1 || mechanicType == "boss_phase") ||
                            _p124StrategyId == "focused_fire" &&
                            (decisivePhase || lastEmberFocusedEconomy);
            if (lastEmberTerminus && levelIndex >= 18 && mechanicType == "boss_phase" &&
                _wave < finalFiveStartWave)
            {
                shouldUse = false;
            }

            shouldUse = ResolveP135ScenarioAutoplayDecision(shouldUse);
            if (!shouldUse || !CanActivateScenarioMechanic(out _))
            {
                return;
            }

            TryActivateScenarioMechanic();
            _p124HandledScenarioWave = _wave;
        }

        private void TryUseP124ResonanceCommand()
        {
            if (!IsResonanceWindowActive || _activeResonanceCommand != TDResonanceCommand.None)
            {
                return;
            }

            var command = _p124StrategyId == "focused_fire"
                ? TDResonanceCommand.EmberSurge
                : _p124StrategyId == "control_lattice"
                    ? TDResonanceCommand.FractureMark
                    : _currentWaveThreatTagSet.Overlaps(new[] { "armored", "heavy", "boss" })
                        ? TDResonanceCommand.EmberSurge
                        : TDResonanceCommand.FractureMark;
            TrySelectResonanceCommand(command);
        }

        private static TDTowerKind[] GetP124TowerPriority(string strategyId)
        {
            return strategyId switch
            {
                "focused_fire" => new[]
                {
                    TDTowerKind.RailLancer, TDTowerKind.SiegeDrill, TDTowerKind.EmberFlak,
                    TDTowerKind.ResonanceBeacon, TDTowerKind.CinderMortar, TDTowerKind.FrostCoil,
                    TDTowerKind.ArcWelder, TDTowerKind.GravSnare
                },
                "control_lattice" => new[]
                {
                    TDTowerKind.FrostCoil, TDTowerKind.GravSnare, TDTowerKind.ArcWelder,
                    TDTowerKind.ResonanceBeacon, TDTowerKind.CinderMortar, TDTowerKind.RailLancer,
                    TDTowerKind.EmberFlak, TDTowerKind.SiegeDrill
                },
                _ => new[]
                {
                    TDTowerKind.RailLancer, TDTowerKind.CinderMortar, TDTowerKind.FrostCoil,
                    TDTowerKind.ArcWelder, TDTowerKind.ResonanceBeacon, TDTowerKind.EmberFlak,
                    TDTowerKind.SiegeDrill, TDTowerKind.GravSnare
                }
            };
        }

        public TDP124RealRunReport DebugBuildP124RunReport()
        {
            var score = CalculateRunScore();
            var towerStats = GetSortedTowerStats();
            var towerResults = towerStats.Select(stat => new TDP124TowerResult
            {
                towerId = stat.towerId,
                towerKind = stat.kind.ToString(),
                cellX = stat.cell.x,
                cellY = stat.cell.y,
                spend = stat.TotalSpend,
                upgrades = stat.upgrades,
                damage = stat.damageDealt,
                kills = stat.kills,
                controls = stat.controlApplications,
                damageSharePct = RoundP124(_totalDamageDealt <= 0 ? 0f : stat.damageDealt * 100f / _totalDamageDealt),
                valuePerBudget = RoundP124((stat.damageDealt + stat.controlApplications * 8f) / Mathf.Max(1f, stat.TotalSpend))
            }).ToArray();
            var kindDamage = towerStats
                .GroupBy(stat => stat.kind)
                .Select(group => new { kind = group.Key, damage = group.Sum(stat => stat.damageDealt) })
                .OrderByDescending(item => item.damage)
                .ToArray();
            var kindContribution = towerStats
                .GroupBy(stat => stat.kind)
                .Select(group => new
                {
                    kind = group.Key,
                    contribution = group.Sum(stat => stat.damageDealt + stat.controlApplications * 8f)
                })
                .OrderByDescending(item => item.contribution)
                .ToArray();
            var firstLeakWave = _waveStats.Values
                .Where(stat => stat != null && stat.escapes > 0)
                .Select(stat => stat.waveIndex)
                .DefaultIfEmpty(0)
                .Min();
            var openingEscapes = _waveStats.Values
                .Where(stat => stat != null && stat.waveIndex <= 2)
                .Sum(stat => Mathf.Max(0, stat.escapes));
            var hottestLane = _laneStats.Values
                .Where(stat => stat != null)
                .OrderByDescending(stat => stat.escapes * 100000 + stat.spawnedHealth - stat.damageDealt)
                .ThenBy(stat => stat.laneKey, StringComparer.Ordinal)
                .FirstOrDefault();
            var laneDamage = _laneStats.Values.Where(stat => stat != null).Sum(stat => stat.damageDealt);
            var towerDamage = towerStats.Sum(stat => stat.damageDealt);
            var laneKills = _laneStats.Values.Where(stat => stat != null).Sum(stat => stat.kills);
            var laneEscapes = _laneStats.Values.Where(stat => stat != null).Sum(stat => stat.escapes);
            var failureReasons = _failureReasonCounts
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}:{pair.Value}")
                .ToArray();
            var recommendationText = BuildRunRecommendationLabel();
            var recommendations = recommendationText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .ToArray();
            var topKindDamage = kindDamage.Length > 0 ? kindDamage[0].damage : 0;
            var totalTowerContribution = kindContribution.Sum(item => item.contribution);
            var topKindContribution = kindContribution.Length > 0 ? kindContribution[0].contribution : 0f;
            var topTower = towerResults.FirstOrDefault();
            var waveCount = GetConfiguredWaveCount();
            var finalFiveStartWave = TDEconomyTuning.GetFinalFiveStartWave(waveCount);
            var finalFiveEconomy = _waveStats.Values
                .Where(stat => stat != null && stat.logged && stat.waveIndex >= finalFiveStartWave)
                .OrderBy(stat => stat.waveIndex)
                .Select(stat => new TDP125WaveEconomyResult
                {
                    waveIndex = stat.waveIndex,
                    budgetStart = stat.budgetStart,
                    budgetEnd = stat.budgetEnd,
                    combatIncome = stat.combatIncome,
                    clearIncome = stat.clearIncome,
                    reinforcementIncome = stat.reinforcementIncome,
                    resonanceIncome = stat.resonanceIncome,
                    grossIncome = stat.combatIncome + stat.clearIncome + stat.reinforcementIncome + stat.resonanceIncome,
                    buildSpend = stat.buildSpend,
                    upgradeSpend = stat.upgradeSpend,
                    scenarioSpend = stat.scenarioSpend,
                    totalSpend = stat.buildSpend + stat.upgradeSpend + stat.scenarioSpend,
                    purchases = stat.buildsPurchased + stat.upgradesPurchased + stat.scenarioUses,
                    towersAtEnd = stat.towersAtEnd,
                    upgradesAtEnd = stat.upgradesAtEnd
                })
                .ToArray();
            var finalFiveGrossIncome = finalFiveEconomy.Sum(item => item.grossIncome);
            var finalFiveSpend = finalFiveEconomy.Sum(item => item.totalSpend);
            var finalFivePurchases = finalFiveEconomy.Sum(item => item.purchases);
            var finalFiveStartingBudget = finalFiveEconomy.Length == 0
                ? _defenseBudget
                : finalFiveEconomy[0].budgetStart + finalFiveEconomy[0].totalSpend;
            var finalFiveAvailableBudget = Mathf.Max(1, finalFiveStartingBudget + finalFiveGrossIncome);
            var firstSaturatedWave = _waveStats.Values
                .Where(stat => stat != null && stat.logged &&
                               stat.towersAtEnd >= Mathf.Max(1, _gridMap?.RecommendedBuildSpotCount ?? 1) &&
                               stat.upgradesAtEnd >= stat.towersAtEnd * 3)
                .Select(stat => stat.waveIndex)
                .DefaultIfEmpty(0)
                .Min();
            var economyDecisionValue = !_victory ||
                                       _defenseBudget <= TDEconomyTuning.DecisionReserveLimit &&
                                       finalFivePurchases >= 2 &&
                                       (firstSaturatedWave == 0 || firstSaturatedWave >= finalFiveStartWave);
            return new TDP124RealRunReport
            {
                schemaVersion = "p125-economy-run-v1",
                mode = "rendered_runtime_autoplay",
                generatedUtc = DateTime.UtcNow.ToString("o"),
                levelIndex = _campaignRoute?.level?.levelIndex ?? 0,
                levelId = _campaignRoute?.level?.levelId ?? "none",
                mapId = _campaignRoute?.level?.mapId ?? "none",
                difficultyId = GetP124DifficultyId(_activeCampaignDifficulty),
                strategyId = _p124StrategyId,
                siteVariant = _p124SiteVariant,
                completed = _gameOver,
                stalled = _p124AutoplayStalled,
                victory = _gameOver && _victory,
                restartCount = 0,
                realDurationSeconds = RoundP124(Mathf.Max(0f, Time.realtimeSinceStartup - _p124RunStartRealtime)),
                simulationDurationSeconds = RoundP124(Mathf.Max(0f, Time.time - _p124RunStartSimulationTime)),
                currentWave = _wave,
                waveCount = waveCount,
                wavesCleared = _wavesCleared,
                firstLeakWave = firstLeakWave,
                openingEscapes = openingEscapes,
                kills = _totalKills,
                escapes = _totalEscapes,
                integrityRemaining = _lineIntegrity,
                endingBudget = _defenseBudget,
                combatIncome = _p125CombatIncome,
                clearIncome = _p125ClearIncome,
                reinforcementIncome = _p125ReinforcementIncome,
                resonanceIncome = _resonanceChainBudgetBonusTotal,
                scenarioSpend = _p125ScenarioSpend,
                finalFiveStartWave = finalFiveStartWave,
                finalFiveStartingBudget = finalFiveStartingBudget,
                finalFiveGrossIncome = finalFiveGrossIncome,
                finalFiveSpend = finalFiveSpend,
                finalFivePurchases = finalFivePurchases,
                finalFiveSpendConversionPct = RoundP124(finalFiveSpend * 100f / finalFiveAvailableBudget),
                firstSaturatedWave = firstSaturatedWave,
                economyDecisionValue = economyDecisionValue,
                towerPowerMultiplier = Mathf.Round(GetCampaignTowerPowerMultiplier() * 100f) / 100f,
                towersBuilt = towerResults.Length,
                availableTowerKinds = _availableTowerKinds.Distinct().Count(),
                towerKindsUsed = towerResults.Select(item => item.towerKind).Distinct(StringComparer.Ordinal).Count(),
                upgradesPurchased = _upgradesPurchased,
                scenarioOpportunities = _scenarioOpportunities,
                scenarioUses = _scenarioUses,
                scenarioConversionPct = RoundP124(_scenarioOpportunities <= 0 ? 0f : _scenarioUses * 100f / _scenarioOpportunities),
                resonanceWindows = _resonanceWindowsTriggered,
                resonanceCommands = _resonanceCommandsUsed,
                convergenceTriggers = _matrixConvergenceTriggers,
                coverageScore = score.coverage,
                counterScore = score.counterMatch,
                outputScore = score.output,
                economyScore = score.economy,
                commandScore = score.command,
                totalScore = score.total,
                grade = score.grade,
                hottestRoute = hottestLane?.laneKey ?? "none",
                topTowerKind = kindDamage.Length > 0 ? kindDamage[0].kind.ToString() : "none",
                topTowerKindDamageSharePct = RoundP124(_totalDamageDealt <= 0 ? 0f : topKindDamage * 100f / _totalDamageDealt),
                topTowerKindByContribution = kindContribution.Length > 0
                    ? kindContribution[0].kind.ToString()
                    : "none",
                topTowerKindContributionSharePct = RoundP124(
                    totalTowerContribution <= 0.01f
                        ? 0f
                        : topKindContribution * 100f / totalTowerContribution),
                topSite = topTower == null ? "none" : $"{topTower.cellX},{topTower.cellY}",
                topSiteDamageSharePct = topTower?.damageSharePct ?? 0f,
                analyticsConsistent = laneDamage == _totalDamageDealt && towerDamage == _totalDamageDealt &&
                                      laneKills == _totalKills && laneEscapes == _totalEscapes,
                failureReasons = failureReasons,
                recommendations = recommendations,
                finalFiveEconomy = finalFiveEconomy,
                towers = towerResults
            };
        }

        public string DebugWriteP124RunJson(string outputPath)
        {
            var report = DebugBuildP124RunReport();
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
            return DebugAuditP124ForTest() + $"p12.4.report.path={fullPath}\n";
        }

        public string DebugAuditP124ForTest()
        {
            var report = DebugBuildP124RunReport();
            var completionPass = report.completed && !report.stalled &&
                                 (!report.victory || report.wavesCleared == report.waveCount);
            var kindChoiceAvailable = _availableTowerKinds.Distinct().Count() > 1;
            var contributionPass = report.towersBuilt < 3 ||
                                   report.topSiteDamageSharePct <= 58f &&
                                   (!kindChoiceAvailable || report.topTowerKindContributionSharePct <= 78f);
            var explanationPass = report.recommendations != null && report.recommendations.Length == 3 &&
                                  (report.victory || report.failureReasons.Length > 0 || report.escapes > 0);
            var durationPass = report.realDurationSeconds <= _p124MaxRealSeconds + 2f;
            var pass = completionPass && report.analyticsConsistent && contributionPass && explanationPass && durationPass;
            return
                $"p12.4.audit.complete={completionPass} [waves={report.wavesCleared}/{report.waveCount},stalled={report.stalled}]\n" +
                $"p12.4.audit.analytics={report.analyticsConsistent}\n" +
                $"p12.4.audit.contribution={contributionPass} [site={report.topSiteDamageSharePct:0.0}%,kindValue={report.topTowerKindContributionSharePct:0.0}%,kindDamage={report.topTowerKindDamageSharePct:0.0}%]\n" +
                $"p12.4.audit.explainable={explanationPass} [failures={report.failureReasons.Length},recommendations={report.recommendations.Length}]\n" +
                $"p12.4.audit.duration={durationPass} [real={report.realDurationSeconds:0.0}s,sim={report.simulationDurationSeconds:0.0}s]\n" +
                $"p12.4.audit.pass={pass}\n";
        }

        public string DebugAuditP131ForTest()
        {
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 0;
            var inScope = levelIndex == 7 || levelIndex == 15;
            var waves = _waveSet?.waves ?? Array.Empty<TDWaveDefinition>();
            var firstWave = waves.FirstOrDefault(wave => wave != null && wave.waveIndex == 1);
            var firstWaveSpan = CalculateP131WaveSpawnSpan(firstWave);
            var secondGroupStart = firstWave?.groups == null || firstWave.groups.Length < 2
                ? 0f
                : firstWave.groups.Skip(1).Min(group => Mathf.Max(0f, group?.startDelay ?? 0f));
            var firstWavePacingPass = !inScope || firstWaveSpan >= 4.5f && secondGroupStart >= 3f;

            var cliffWaveIndices = levelIndex == 7
                ? new[] { 3, 4, 5, 6, 7, 9, 18 }
                : new[] { 3, 6, 7, 8, 9, 11, 12, 15, 16, 17 };
            var minimumCliffSpans = levelIndex == 7
                ? new[] { 7f, 7.5f, 7.5f, 7.5f, 7f, 7f, 8f }
                : new[] { 6.5f, 8.5f, 10.5f, 8f, 10f, 10.5f, 9.5f, 10f, 9f, 10f };
            var cliffWaveSpans = cliffWaveIndices
                .Select(index => waves.FirstOrDefault(wave => wave != null && wave.waveIndex == index))
                .Select(CalculateP131WaveSpawnSpan)
                .ToArray();
            var cliffPacingPass = !inScope || cliffWaveSpans.Length == minimumCliffSpans.Length &&
                                  cliffWaveSpans
                                      .Select((span, index) => span >= minimumCliffSpans[index])
                                      .All(value => value);

            var siteQualities = CalculateP131NeutralSiteQualities();
            var minimumSiteQuality = siteQualities.DefaultIfEmpty(0f).Min();
            var maximumSiteQuality = siteQualities.DefaultIfEmpty(0f).Max();
            var totalSiteQuality = siteQualities.Sum();
            var dominantSiteCoverageShare = totalSiteQuality <= 0.001f
                ? 100f
                : maximumSiteQuality * 100f / totalSiteQuality;
            var sitePolicyPass = P124EstablishedEndpointCoverageBonus <= 6f &&
                                 P124DepthVariantProgressWeight <= 0.5f &&
                                 (!inScope || siteQualities.Length >= 12 && dominantSiteCoverageShare <= 22f);

            var report = DebugBuildP124RunReport();
            var runtimeSiteLimit = _activeCampaignDifficulty switch
            {
                TDCampaignDifficultyTier.Veteran => 35f,
                TDCampaignDifficultyTier.EmberTrial => 38f,
                _ => 32f
            };
            var openingEscapeLimit = _activeCampaignDifficulty switch
            {
                TDCampaignDifficultyTier.Veteran => 6,
                TDCampaignDifficultyTier.EmberTrial => 8,
                _ => 4
            };
            var emberTrialHalfRun = _activeCampaignDifficulty == TDCampaignDifficultyTier.EmberTrial &&
                                    report.completed && report.waveCount > 0 &&
                                    report.wavesCleared >= Mathf.CeilToInt(report.waveCount * 0.5f);
            var runtimeOutcomePass = report.completed && (report.victory || emberTrialHalfRun);
            var runtimePass = !inScope || runtimeOutcomePass &&
                              report.openingEscapes <= openingEscapeLimit &&
                              (report.victory ? report.integrityRemaining > 0 : emberTrialHalfRun) &&
                              report.topSiteDamageSharePct <= runtimeSiteLimit;
            var pass = firstWavePacingPass && cliffPacingPass && sitePolicyPass && runtimePass;
            return
                $"p13.1.audit.firstWave={firstWavePacingPass} [level={levelIndex},span={firstWaveSpan:0.00},secondGroup={secondGroupStart:0.00}]\n" +
                $"p13.1.audit.cliffPacing={cliffPacingPass} [waves={string.Join(",", cliffWaveIndices)},spans={string.Join(",", cliffWaveSpans.Select(value => value.ToString("0.00")))}]\n" +
                $"p13.1.audit.sitePolicy={sitePolicyPass} [sites={siteQualities.Length},quality={minimumSiteQuality:0.00}-{maximumSiteQuality:0.00},dominant={dominantSiteCoverageShare:0.0}%,endpoint={P124EstablishedEndpointCoverageBonus:0.0},variant={P124DepthVariantProgressWeight:0.0}]\n" +
                $"p13.1.audit.runtime={runtimePass} [victory={report.victory},halfTrial={emberTrialHalfRun},waves={report.wavesCleared}/{report.waveCount},firstLeak={report.firstLeakWave},openingEscapes={report.openingEscapes}/{openingEscapeLimit},integrity={report.integrityRemaining},topSite={report.topSiteDamageSharePct:0.0}%/{runtimeSiteLimit:0}%]\n" +
                $"p13.1.audit.pass={pass}\n";
        }

        private static float CalculateP131WaveSpawnSpan(TDWaveDefinition wave)
        {
            return wave?.groups == null
                ? 0f
                : wave.groups
                    .Where(group => group != null && group.count > 0)
                    .Select(group => Mathf.Max(0f, group.startDelay) +
                                     Mathf.Max(0, group.count - 1) * Mathf.Max(0f, group.spawnInterval))
                    .DefaultIfEmpty(0f)
                    .Max();
        }

        private float[] CalculateP131NeutralSiteQualities()
        {
            if (_gridMap == null)
            {
                return Array.Empty<float>();
            }

            var paths = _activeLanePaths.Values
                .Where(path => path != null && path.Count > 1)
                .Distinct()
                .ToArray();
            if (paths.Length == 0)
            {
                return Array.Empty<float>();
            }

            const int sampleCount = 25;
            var range = TDTower.GetBaseRange(TDTowerKind.RailLancer);
            return _gridMap.RecommendedBuildCells
                .Select(cell =>
                {
                    var world = _gridMap.CellToBuildWorld(cell);
                    var quality = 0f;
                    for (var pathIndex = 0; pathIndex < paths.Length; pathIndex++)
                    {
                        var covered = 0;
                        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                        {
                            var progress = sampleIndex / (float)(sampleCount - 1);
                            if (Vector2.Distance(world, GetPathPointAtNormalizedProgress(paths[pathIndex], progress)) <= range)
                            {
                                covered++;
                            }
                        }

                        quality += covered / (float)sampleCount;
                    }

                    return quality;
                })
                .ToArray();
        }

        private static string GetP124DifficultyId(TDCampaignDifficultyTier difficulty)
        {
            return difficulty switch
            {
                TDCampaignDifficultyTier.Veteran => "veteran",
                TDCampaignDifficultyTier.EmberTrial => "ember_trial",
                _ => "standard"
            };
        }

        private static float RoundP124(float value)
        {
            return Mathf.Round(value * 10f) / 10f;
        }
    }
}
#endif
