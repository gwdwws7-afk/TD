using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TD
{
    public static class TDCampaignLoader
    {
        private static readonly HashSet<string> ContractMetrics = new()
        {
            "integrity",
            "budget",
            "escapes",
            "tower_count",
            "upgrades",
            "tactical_score",
            "counter_score",
            "command_score",
            "matrix_full_matches",
            "convergence_triggers"
        };

        private static readonly HashSet<string> ContractComparisons = new()
        {
            "at_least",
            "at_most"
        };

        private static readonly HashSet<string> ScenarioMechanicTypes = new()
        {
            "signal_gate",
            "timed_reinforcement",
            "route_switch",
            "environment_device",
            "boss_phase"
        };

        private static readonly HashSet<int> MilestoneExamLevels = new() { 5, 9, 13, 17, 20 };

        public static bool TryLoadFromResources(string resourcePath, out TDCampaignDefinition campaign, out string error)
        {
            campaign = null;
            error = string.Empty;

            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                error = $"Campaign config not found at Resources/{resourcePath}.json";
                return false;
            }

            try
            {
                campaign = JsonUtility.FromJson<TDCampaignDefinition>(textAsset.text);
            }
            catch (Exception ex)
            {
                // Malformed/empty JSON throws from FromJson — route it through
                // the error channel instead of crashing the boot path.
                error = $"Failed to parse campaign config JSON: {ex.Message}";
                campaign = null;
                return false;
            }

            if (campaign == null)
            {
                error = "Failed to parse campaign config JSON.";
                return false;
            }

            if (!ValidateCampaign(campaign, out error))
            {
                campaign = null;
                return false;
            }

            return true;
        }

        private static bool ValidateCampaign(TDCampaignDefinition campaign, out string error)
        {
            error = string.Empty;
            if (campaign.chapters == null || campaign.maps == null || campaign.levels == null)
            {
                error = "Campaign config missing chapters/maps/levels.";
                return false;
            }

            if (campaign.totalLevels <= 0 || campaign.levels.Length != campaign.totalLevels)
            {
                error = "Campaign totalLevels must match levels length.";
                return false;
            }

            if (!string.Equals(campaign.schemaVersion, "campaign-schema-v1"))
            {
                error = "Campaign schemaVersion must be campaign-schema-v1.";
                return false;
            }

            if (campaign.globalRules != null &&
                (campaign.globalRules.startingBudgetPerLevel < 0 || campaign.globalRules.startingBudgetPerLevel > 20 ||
                 campaign.globalRules.startingIntegrityPerChapter < 0 || campaign.globalRules.startingIntegrityPerChapter > 10 ||
                 campaign.globalRules.towerPowerPerLevelPct < 0f || campaign.globalRules.towerPowerPerLevelPct > 3f))
            {
                error = "Campaign global starting budget/integrity growth is outside the supported range.";
                return false;
            }

            var chapterIds = new HashSet<string>();
            var chapterRewardIds = new HashSet<string>();
            var mutatorIds = new HashSet<string>();
            var chapterById = new Dictionary<string, TDCampaignChapterDefinition>();
            for (var i = 0; i < campaign.chapters.Length; i++)
            {
                var chapter = campaign.chapters[i];
                if (chapter == null || string.IsNullOrWhiteSpace(chapter.chapterId))
                {
                    error = "Campaign chapter contains null/empty chapterId.";
                    return false;
                }

                if (!chapterIds.Add(chapter.chapterId))
                {
                    error = $"Campaign chapterId duplicated: {chapter.chapterId}";
                    return false;
                }

                if (chapter.startLevel <= 0 || chapter.endLevel < chapter.startLevel)
                {
                    error = $"Campaign chapter level range invalid: {chapter.chapterId}";
                    return false;
                }

                var reward = chapter.reward;
                if (reward == null || string.IsNullOrWhiteSpace(reward.rewardId) ||
                    string.IsNullOrWhiteSpace(reward.displayName) || string.IsNullOrWhiteSpace(reward.description))
                {
                    error = $"Campaign chapter reward is incomplete: {chapter.chapterId}";
                    return false;
                }

                if (!chapterRewardIds.Add(reward.rewardId))
                {
                    error = $"Campaign chapter rewardId duplicated: {reward.rewardId}";
                    return false;
                }

                if (reward.startingBudgetBonus < 0 || reward.startingIntegrityBonus < 0 ||
                    reward.resonanceGainMultiplier < 0f || reward.resonanceGainMultiplier > 2f)
                {
                    error = $"Campaign chapter reward effect is invalid: {reward.rewardId}";
                    return false;
                }

                if (reward.startingBudgetBonus == 0 && reward.startingIntegrityBonus == 0 &&
                    reward.resonanceGainMultiplier <= 1f)
                {
                    error = $"Campaign chapter reward has no gameplay effect: {reward.rewardId}";
                    return false;
                }

                if (chapter.challengeRemix == null)
                {
                    if (campaign.totalLevels >= 20)
                    {
                        error = $"Campaign chapter is missing its P8.5 challenge remix: {chapter.chapterId}";
                        return false;
                    }
                }
                else if (!ValidateMutatorDefinition(
                             chapter.challengeRemix,
                             mutatorIds,
                             $"Campaign chapter {chapter.chapterId} challenge remix",
                             out error))
                {
                    return false;
                }

                chapterById[chapter.chapterId] = chapter;
            }

            if (campaign.difficultyTiers == null || campaign.difficultyTiers.Length != 3)
            {
                error = "Campaign P8.5 difficultyTiers must define exactly three tiers.";
                return false;
            }

            var difficultyIds = new HashSet<string>();
            var difficultyIndexes = new HashSet<int>();
            for (var i = 0; i < campaign.difficultyTiers.Length; i++)
            {
                var difficulty = campaign.difficultyTiers[i];
                if (difficulty == null || difficulty.tier < 0 || difficulty.tier > 2 ||
                    string.IsNullOrWhiteSpace(difficulty.difficultyId) ||
                    string.IsNullOrWhiteSpace(difficulty.displayName) ||
                    string.IsNullOrWhiteSpace(difficulty.description) ||
                    !difficultyIds.Add(difficulty.difficultyId) || !difficultyIndexes.Add(difficulty.tier))
                {
                    error = "Campaign contains an invalid or duplicate P8.5 difficulty tier.";
                    return false;
                }

                if (difficulty.tier == 0)
                {
                    if (difficulty.modifiers != null && HasMutatorEffect(difficulty.modifiers))
                    {
                        error = "Campaign Standard difficulty must not add runtime modifiers.";
                        return false;
                    }
                }
                else if (!ValidateMutatorDefinition(
                             difficulty.modifiers,
                             mutatorIds,
                             $"Campaign difficulty {difficulty.difficultyId}",
                             out error))
                {
                    return false;
                }
            }

            if (!difficultyIndexes.Contains(0) || !difficultyIndexes.Contains(1) || !difficultyIndexes.Contains(2))
            {
                error = "Campaign P8.5 difficulty tiers must cover indexes 0, 1 and 2.";
                return false;
            }

            if (!ValidateMetaProgression(campaign.metaProgression, out error))
            {
                return false;
            }

            var mapIds = new HashSet<string>();
            var mapUsage = new Dictionary<string, int>();
            var mechanicIds = new HashSet<string>();
            var mechanicTypes = new HashSet<string>();
            for (var i = 0; i < campaign.maps.Length; i++)
            {
                var map = campaign.maps[i];
                if (map == null || string.IsNullOrWhiteSpace(map.mapId))
                {
                    error = "Campaign map contains null/empty mapId.";
                    return false;
                }

                if (!mapIds.Add(map.mapId))
                {
                    error = $"Campaign mapId duplicated: {map.mapId}";
                    return false;
                }

                var mechanic = map.mechanic;
                if (mechanic == null || string.IsNullOrWhiteSpace(mechanic.mechanicId) ||
                    string.IsNullOrWhiteSpace(mechanic.displayName) || string.IsNullOrWhiteSpace(mechanic.description) ||
                    string.IsNullOrWhiteSpace(mechanic.commandLabel) ||
                    !ScenarioMechanicTypes.Contains(mechanic.mechanicType ?? string.Empty) ||
                    !mechanicIds.Add(mechanic.mechanicId) || !mechanicTypes.Add(mechanic.mechanicType) ||
                    mechanic.maxCharges < 0 || mechanic.maxCharges > 12 || mechanic.budgetCost < 0 || mechanic.budgetCost > 100 ||
                    mechanic.reinforcementDelaySeconds < 0f || mechanic.effectDurationSeconds < 0f)
                {
                    error = $"Campaign map has an invalid or duplicate P8.6 scenario mechanic: {map.mapId}";
                    return false;
                }

                mapUsage[map.mapId] = 0;
            }

            var levelIndexes = new HashSet<int>();
            var contractIds = new HashSet<string>();
            var resonanceEnabledFromLevel = campaign.globalRules != null && campaign.globalRules.resonanceEnabledFromLevel > 0
                ? campaign.globalRules.resonanceEnabledFromLevel
                : 1;
            for (var i = 0; i < campaign.levels.Length; i++)
            {
                var level = campaign.levels[i];
                if (level == null || level.levelIndex <= 0)
                {
                    error = "Campaign level contains null/invalid levelIndex.";
                    return false;
                }

                if (!levelIndexes.Add(level.levelIndex))
                {
                    error = $"Campaign levelIndex duplicated: {level.levelIndex}";
                    return false;
                }

                if (!chapterIds.Contains(level.chapterId))
                {
                    error = $"Campaign level {level.levelIndex} references missing chapterId: {level.chapterId}";
                    return false;
                }

                var chapter = chapterById[level.chapterId];
                if (level.levelIndex < chapter.startLevel || level.levelIndex > chapter.endLevel)
                {
                    error = $"Campaign level {level.levelIndex} is outside chapter range: {level.chapterId}";
                    return false;
                }

                if (!mapIds.Contains(level.mapId))
                {
                    error = $"Campaign level {level.levelIndex} references missing mapId: {level.mapId}";
                    return false;
                }

                mapUsage[level.mapId] = mapUsage[level.mapId] + 1;

                if (string.IsNullOrWhiteSpace(level.waveSetId))
                {
                    error = $"Campaign level {level.levelIndex} has empty waveSetId.";
                    return false;
                }

                var waveConfig = Resources.Load<TextAsset>($"Data/waves/{level.waveSetId}");
                if (waveConfig == null)
                {
                    error = $"Campaign level {level.levelIndex} missing wave config: Data/waves/{level.waveSetId}.json";
                    return false;
                }

                var waveSet = JsonUtility.FromJson<TDWaveSet>(waveConfig.text);
                if (!HasCompleteScenarioGrammar(waveSet))
                {
                    error = $"Campaign level {level.levelIndex} must contain Introduce, Reinforce and Exam wave phases.";
                    return false;
                }

                var isMilestoneExam = MilestoneExamLevels.Contains(level.levelIndex);
                if (isMilestoneExam != (level.scenario?.milestoneExam == true) ||
                    (isMilestoneExam && (level.scenario.intensity < 3 || string.IsNullOrWhiteSpace(level.scenario.failureFocus))))
                {
                    error = $"Campaign level {level.levelIndex} has invalid P8.6 milestone exam metadata.";
                    return false;
                }

                if (!ValidateMissionRules(
                        level,
                        contractIds,
                        mutatorIds,
                        campaign.totalLevels >= 20,
                        resonanceEnabledFromLevel,
                        out error))
                {
                    return false;
                }
            }

            for (var idx = 1; idx <= campaign.totalLevels; idx++)
            {
                if (!levelIndexes.Contains(idx))
                {
                    error = $"Campaign levelIndex is not contiguous. Missing level {idx}.";
                    return false;
                }
            }

            if (campaign.totalLevels >= 20)
            {
                foreach (var kv in mapUsage)
                {
                    if (kv.Value < 4)
                    {
                        error = $"Campaign mapId must appear at least 4 times in 20-level scope: {kv.Key}";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ValidateMetaProgression(TDCampaignMetaProgressionDefinition meta, out string error)
        {
            error = string.Empty;
            if (meta?.tacticalProtocols == null || meta.tacticalProtocols.Length < 3 ||
                meta.ratingRewards == null || meta.ratingRewards.Length == 0 ||
                meta.codexRewards == null || meta.codexRewards.Length < 2)
            {
                error = "Campaign P10.1 meta progression is incomplete.";
                return false;
            }

            var protocolIds = new HashSet<string>();
            for (var i = 0; i < meta.tacticalProtocols.Length; i++)
            {
                var protocol = meta.tacticalProtocols[i];
                if (protocol == null || string.IsNullOrWhiteSpace(protocol.protocolId) ||
                    string.IsNullOrWhiteSpace(protocol.displayName) || string.IsNullOrWhiteSpace(protocol.description) ||
                    !protocolIds.Add(protocol.protocolId))
                {
                    error = "Campaign contains an invalid or duplicate P10.1 tactical protocol.";
                    return false;
                }

                var baseline = string.Equals(protocol.protocolId, "baseline", System.StringComparison.OrdinalIgnoreCase);
                var hasBenefit = protocol.startingBudgetDelta > 0 || protocol.prepSecondsDelta > 0 ||
                                 protocol.scenarioChargeDelta > 0 || protocol.rewardMultiplier > 1f;
                var hasCost = protocol.startingBudgetDelta < 0 || protocol.enemyHpMultiplier > 1f ||
                              protocol.scenarioCostMultiplier > 1f;
                if ((!baseline && (!hasBenefit || !hasCost)) ||
                    protocol.startingBudgetDelta < -100 || protocol.startingBudgetDelta > 100 ||
                    protocol.prepSecondsDelta < 0 || protocol.prepSecondsDelta > 15 ||
                    protocol.scenarioChargeDelta < 0 || protocol.scenarioChargeDelta > 3 ||
                    !IsOptionalMultiplierValid(protocol.enemyHpMultiplier, 1f, 1.5f) ||
                    !IsOptionalMultiplierValid(protocol.rewardMultiplier, 0.5f, 1.5f) ||
                    !IsOptionalMultiplierValid(protocol.scenarioCostMultiplier, 1f, 2f))
                {
                    error = $"Campaign tactical protocol {protocol.protocolId} must contain a bounded benefit and cost.";
                    return false;
                }
            }

            if (!protocolIds.Contains("baseline"))
            {
                error = "Campaign P10.1 tactical protocols must include baseline.";
                return false;
            }

            var rewardIds = new HashSet<string>();
            foreach (var reward in meta.ratingRewards.Concat(meta.codexRewards))
            {
                if (reward == null || string.IsNullOrWhiteSpace(reward.rewardId) ||
                    string.IsNullOrWhiteSpace(reward.displayName) || string.IsNullOrWhiteSpace(reward.description) ||
                    string.IsNullOrWhiteSpace(reward.sourceType) || reward.threshold <= 0 ||
                    !rewardIds.Add(reward.rewardId) || !protocolIds.Contains(reward.unlockProtocolId))
                {
                    error = "Campaign contains an invalid, duplicate or unresolved P10.1 meta reward.";
                    return false;
                }
            }

            return true;
        }

        private static bool HasCompleteScenarioGrammar(TDWaveSet waveSet)
        {
            if (waveSet?.waves == null || waveSet.waves.Length == 0)
            {
                return false;
            }

            var introduce = false;
            var reinforce = false;
            var exam = false;
            for (var i = 0; i < waveSet.waves.Length; i++)
            {
                var phase = waveSet.waves[i]?.phase?.Trim().ToLowerInvariant();
                introduce |= phase == "introduce";
                reinforce |= phase == "reinforce";
                exam |= phase == "exam" || phase == "boss";
            }

            return introduce && reinforce && exam;
        }

        private static bool ValidateMissionRules(
            TDCampaignLevelDefinition level,
            HashSet<string> contractIds,
            HashSet<string> mutatorIds,
            bool rulesRequired,
            int resonanceEnabledFromLevel,
            out string error)
        {
            error = string.Empty;
            var contract = level.contract;
            if (contract == null)
            {
                if (rulesRequired)
                {
                    error = $"Campaign level {level.levelIndex} is missing its P8.2 contract.";
                    return false;
                }

                return true;
            }

            if (string.IsNullOrWhiteSpace(contract.contractId) ||
                string.IsNullOrWhiteSpace(contract.displayName) ||
                !ContractMetrics.Contains(contract.metric ?? string.Empty) ||
                !ContractComparisons.Contains(contract.comparison ?? string.Empty) ||
                contract.target < 0)
            {
                error = $"Campaign level {level.levelIndex} has an invalid P8.2 contract.";
                return false;
            }

            if (!contractIds.Add(contract.contractId))
            {
                error = $"Campaign contractId duplicated: {contract.contractId}";
                return false;
            }

            if (IsResonanceContractMetric(contract.metric) &&
                level.levelIndex < resonanceEnabledFromLevel)
            {
                error = $"Campaign level {level.levelIndex} uses resonance contract metric before resonance unlock.";
                return false;
            }

            if (level.mutators == null || level.mutators.Length == 0)
            {
                if (rulesRequired)
                {
                    error = $"Campaign level {level.levelIndex} is missing its P8.2 mutator.";
                    return false;
                }

                return true;
            }

            for (var i = 0; i < level.mutators.Length; i++)
            {
                var mutator = level.mutators[i];
                if (!ValidateMutatorDefinition(
                        mutator,
                        mutatorIds,
                        $"Campaign level {level.levelIndex} P8.2 mutator",
                        out error))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateMutatorDefinition(
            TDCampaignMutatorDefinition mutator,
            HashSet<string> mutatorIds,
            string context,
            out string error)
        {
            error = string.Empty;
            if (mutator == null || string.IsNullOrWhiteSpace(mutator.mutatorId) ||
                string.IsNullOrWhiteSpace(mutator.displayName))
            {
                error = $"{context} is incomplete.";
                return false;
            }

            if (!mutatorIds.Add(mutator.mutatorId))
            {
                error = $"Campaign mutatorId duplicated: {mutator.mutatorId}";
                return false;
            }

            if (!IsOptionalMultiplierValid(mutator.enemyHpMultiplier, 0.5f, 3f) ||
                !IsOptionalMultiplierValid(mutator.enemySpeedMultiplier, 0.5f, 2f) ||
                !IsOptionalMultiplierValid(mutator.rewardMultiplier, 0.25f, 2f) ||
                !IsOptionalMultiplierValid(mutator.resonanceGainMultiplier, 0.25f, 2f) ||
                mutator.enemyArmorBonus < 0 || mutator.enemyArmorBonus > 10 ||
                mutator.startingBudgetDelta < -100 || mutator.startingBudgetDelta > 500 ||
                mutator.startingIntegrityDelta < -19 || mutator.startingIntegrityDelta > 50 ||
                !HasMutatorEffect(mutator))
            {
                error = $"{context} {mutator.mutatorId} has invalid or empty effects.";
                return false;
            }

            return true;
        }

        private static bool IsResonanceContractMetric(string metric)
        {
            return string.Equals(metric, "command_score") ||
                   string.Equals(metric, "matrix_full_matches") ||
                   string.Equals(metric, "convergence_triggers");
        }

        private static bool IsOptionalMultiplierValid(float value, float min, float max)
        {
            return Mathf.Approximately(value, 0f) || value >= min && value <= max;
        }

        private static bool HasMutatorEffect(TDCampaignMutatorDefinition mutator)
        {
            return mutator.startingBudgetDelta != 0 ||
                   mutator.startingIntegrityDelta != 0 ||
                   mutator.enemyArmorBonus != 0 ||
                   IsNonNeutralMultiplier(mutator.enemyHpMultiplier) ||
                   IsNonNeutralMultiplier(mutator.enemySpeedMultiplier) ||
                   IsNonNeutralMultiplier(mutator.rewardMultiplier) ||
                   IsNonNeutralMultiplier(mutator.resonanceGainMultiplier);
        }

        private static bool IsNonNeutralMultiplier(float value)
        {
            return value > 0f && !Mathf.Approximately(value, 1f);
        }
    }
}
