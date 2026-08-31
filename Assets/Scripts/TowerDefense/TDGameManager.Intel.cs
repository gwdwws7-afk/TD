// Freeze-period move: Intel cluster.
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
                "tactical_score" => CalculateRunScoreCachedForFrame().total,
                "counter_score" => CalculateRunCounterScore(),
                "command_score" => CalculateRunCommandScore(),
                "matrix_full_matches" => _matrixFullMatches,
                "convergence_triggers" => _matrixConvergenceTriggers,
                _ => 0
            };
        }

        /// <summary>
        /// CalculateRunScore walks every lane/tower/threat stat dictionary —
        /// the HUD contract label and the contract feedback both ask for it
        /// every frame, so compute it once per frame at most.
        /// </summary>
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

    }
}
