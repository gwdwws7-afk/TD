// Freeze-period S2: the run-stats / score / telemetry cluster moved
// verbatim from TDGameManager.cs (scattered members, zero behavior
// change). Inputs: the Notify* event stream + Begin/Finalize wave stats;
// outputs: CalculateRunScore and the stat dictionaries.
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

        private TDRunScoreReport CalculateRunScoreCachedForFrame()
        {
            if (_runScoreFrameCacheFrame == Time.frameCount && _runScoreFrameCache != null)
            {
                return _runScoreFrameCache;
            }

            _runScoreFrameCacheFrame = Time.frameCount;
            _runScoreFrameCache = CalculateRunScore();
            return _runScoreFrameCache;
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
            // Teaching copy step 4 (L17+): convergence is the payoff for
            // building the right towers — explain it the first time it fires.
            ShowResonanceTipOnce(
                "matrix_convergence",
                "Every specialization has a resonance affinity — damage specs favor Surge, utility specs favor Mark. When the right spec hits the right enemies in the right window, Matrix Convergence triggers: Surge convergence extends the window and amplifies the whole line; Mark convergence pins every enemy in place. That is the highest reward for building the right towers.",
                "每座塔的专精都有共鸣倾向：伤害系专精亲和涌动，功能系专精亲和标记。当专精在正确的窗口里反复命中正确的敌人，会触发矩阵收敛——涌动收敛延长窗口、全队增伤；标记收敛把全场敌人钉在原地。这是\"塔建对了\"的最高奖赏。",
                9.0f);
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                _matrixEmberConvergenceTriggers++;
                var before = _resonanceWindowTimer;
                if (!IsResonanceChargeFrozen)
                {
                    _resonanceWindowTimer = Mathf.Min(
                        ResonanceWindowDuration + MatrixConvergenceEmberWindowExtension,
                        _resonanceWindowTimer + MatrixConvergenceEmberWindowExtension);
                }
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

        private static bool IsCounterOpportunity(TDEnemy enemy)
        {
            return enemy != null && enemy.HasAnyTag(
                "fast", "flank", "swarm", "split", "armored", "heavy", "boss", "support", "attrition");
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

        public void NotifyEnemyKilled(TDEnemy enemy, int reward, TDTower sourceTower)
        {
            _activeEnemies.Remove(enemy);
            TrySpreadBurnOnKill(enemy, sourceTower);
            RegisterExpansionDeathEffects(enemy);
            if (_gameOver)
            {
                return;
            }

            if (enemy != null && sourceTower?.ActiveSpecialization != null &&
                (DoesEnemyMatchSpecialization(enemy, sourceTower.ActiveSpecialization) || enemy.IsMarked))
            {
                RecordEnemyCodexObservation(enemy.EnemyId, TDEnemyCodexObservation.CounterKilled);
            }

            var auraReward = ApplySalvageBountyAura(enemy, reward);
            var combatReward = TDEconomyTuning.ScaleCombatBounty(auraReward, _wave, GetConfiguredWaveCount());
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

            if (IsResonanceWindowActive && _activeResonanceCommand == TDResonanceCommand.EmberSurge &&
                !IsResonanceChargeFrozen)
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
                _waveRoutine = null;
            }

            PlayCriticalSfxTone("run_defeat", 150f, 0.28f, 0.90f, false);
            RecordCampaignResultIfNeeded();
            LogRunSummary();
        }

        private void ResetMatrixWindowState()
        {
            _matrixWindowSync = 0;
            _matrixWindowSpecializationIds.Clear();
            _matrixConvergenceTriggeredThisWindow = false;
        }

        private void BeginWaveStat(int waveIndex)
        {
            // Derrick fuse resets per wave; Supply Drop pays here so both wave
            // loops (config + fallback) get it from one site.
            _derrickWaveCredited = 0;
            _salvageDerricks.RemoveAll(tower => tower == null);
            for (var i = 0; i < _salvageDerricks.Count; i++)
            {
                if (_salvageDerricks[i].IsUtilitySpecialist)
                {
                    CreditDerrickWaveIncome(3);
                }
            }

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

    }
}
