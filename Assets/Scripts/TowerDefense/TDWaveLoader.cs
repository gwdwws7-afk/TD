using System;
using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public static class TDWaveLoader
    {
        private static readonly HashSet<string> ValidPhases = new()
        {
            "introduce",
            "reinforce",
            "exam",
            "boss"
        };

        public static bool TryLoadFromResources(string resourcePath, out TDWaveSet waveSet, out string error)
        {
            return TryLoadFromResources(resourcePath, null, out waveSet, out error);
        }

        public static bool TryLoadFromResources(
            string resourcePath,
            IReadOnlyDictionary<string, TDEnemyCatalogEntry> externalEnemyCatalog,
            out TDWaveSet waveSet,
            out string error)
        {
            waveSet = null;
            error = string.Empty;

            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                error = $"Wave config not found at Resources/{resourcePath}.json";
                return false;
            }

            try
            {
                waveSet = JsonUtility.FromJson<TDWaveSet>(textAsset.text);
            }
            catch (Exception ex)
            {
                // Malformed/empty JSON throws from FromJson — route it through
                // the error channel so the fallback wave loop can take over.
                error = $"Failed to parse wave config JSON: {ex.Message}";
                waveSet = null;
                return false;
            }

            if (waveSet == null)
            {
                error = "Failed to parse wave config JSON.";
                return false;
            }

            if (!ValidateWaveSet(waveSet, externalEnemyCatalog, out error))
            {
                waveSet = null;
                return false;
            }

            return true;
        }

        private static bool ValidateWaveSet(
            TDWaveSet waveSet,
            IReadOnlyDictionary<string, TDEnemyCatalogEntry> externalEnemyCatalog,
            out string error)
        {
            error = string.Empty;
            if (waveSet == null)
            {
                error = "Wave config is null.";
                return false;
            }

            if (!string.Equals(waveSet.schemaVersion, "wave-schema-v1"))
            {
                error = "Wave schemaVersion must be wave-schema-v1.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(waveSet.waveSetId))
            {
                error = "Wave config requires a non-empty waveSetId.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(waveSet.mapId))
            {
                error = $"WaveSet {waveSet.waveSetId} has empty mapId.";
                return false;
            }

            if (waveSet.globalDefaults == null || waveSet.enemyCatalog == null || waveSet.waves == null)
            {
                error = "Wave config missing globalDefaults/enemyCatalog/waves.";
                return false;
            }

            if (waveSet.enemyCatalog.Length == 0 || waveSet.waves.Length == 0)
            {
                error = "Wave config must contain at least one enemy and one wave.";
                return false;
            }

            if (waveSet.globalDefaults.spawnMinSpacing <= 0f)
            {
                error = $"WaveSet {waveSet.waveSetId} has invalid globalDefaults.spawnMinSpacing.";
                return false;
            }

            if (waveSet.globalDefaults.prepSeconds < 0f)
            {
                error = $"WaveSet {waveSet.waveSetId} has invalid globalDefaults.prepSeconds.";
                return false;
            }

            if (waveSet.globalDefaults.baseRewardGold < 0)
            {
                error = $"WaveSet {waveSet.waveSetId} has invalid globalDefaults.baseRewardGold.";
                return false;
            }

            if (waveSet.globalDefaults.lineDamageDefault <= 0)
            {
                error = $"WaveSet {waveSet.waveSetId} has invalid globalDefaults.lineDamageDefault.";
                return false;
            }

            var enemyCostById = new Dictionary<string, float>();
            if (externalEnemyCatalog != null)
            {
                foreach (var pair in externalEnemyCatalog)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                    {
                        continue;
                    }

                    enemyCostById[pair.Key] = Mathf.Max(0f, pair.Value.threatCost);
                }
            }

            var localEnemyIds = new HashSet<string>();
            for (var i = 0; i < waveSet.enemyCatalog.Length; i++)
            {
                var enemy = waveSet.enemyCatalog[i];
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.enemyId))
                {
                    error = $"WaveSet {waveSet.waveSetId} enemyCatalog[{i}] has empty enemyId.";
                    return false;
                }

                if (!localEnemyIds.Add(enemy.enemyId))
                {
                    error = $"WaveSet {waveSet.waveSetId} duplicate enemyId in enemyCatalog: {enemy.enemyId}.";
                    return false;
                }

                if (enemy.hp <= 0 || enemy.speed <= 0f)
                {
                    error = $"WaveSet {waveSet.waveSetId} enemy {enemy.enemyId} has invalid hp/speed.";
                    return false;
                }

                if (enemy.rewardGold < 0)
                {
                    error = $"WaveSet {waveSet.waveSetId} enemy {enemy.enemyId} has invalid rewardGold.";
                    return false;
                }

                enemyCostById[enemy.enemyId] = Mathf.Max(0f, enemy.threatCost);
            }

            for (var i = 0; i < waveSet.waves.Length; i++)
            {
                var wave = waveSet.waves[i];
                var expectedIndex = i + 1;
                if (wave == null)
                {
                    error = $"WaveSet {waveSet.waveSetId} has null wave at index {expectedIndex}.";
                    return false;
                }

                if (wave.waveIndex != expectedIndex)
                {
                    error = $"WaveSet {waveSet.waveSetId} waveIndex must be contiguous. Expected {expectedIndex}, got {wave.waveIndex}.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(wave.phase) || !ValidPhases.Contains(wave.phase.Trim().ToLowerInvariant()))
                {
                    error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} has invalid phase.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(wave.goalTag))
                {
                    error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} has empty goalTag.";
                    return false;
                }

                if (wave.threatTags == null || wave.threatTags.Length == 0)
                {
                    error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} has empty threatTags.";
                    return false;
                }

                if (wave.prepSeconds < 0f)
                {
                    error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} has invalid prepSeconds.";
                    return false;
                }

                if (wave.rewardGold < 0)
                {
                    error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} has invalid rewardGold.";
                    return false;
                }

                if (wave.budgetTarget <= 0f)
                {
                    error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} has invalid budgetTarget.";
                    return false;
                }

                var safeTolerance = Mathf.Clamp(wave.budgetTolerance <= 0f ? 1f : wave.budgetTolerance, 0.5f, 1.5f);
                if (wave.groups == null || wave.groups.Length == 0)
                {
                    error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} has empty groups.";
                    return false;
                }

                var actualBudget = 0f;
                for (var g = 0; g < wave.groups.Length; g++)
                {
                    var group = wave.groups[g];
                    if (group == null || string.IsNullOrWhiteSpace(group.enemyId))
                    {
                        error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} group {g} has empty enemyId.";
                        return false;
                    }

                    if (group.count <= 0)
                    {
                        error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} group {g} has invalid count.";
                        return false;
                    }

                    if (group.startDelay < 0f)
                    {
                        error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} group {g} has invalid startDelay.";
                        return false;
                    }

                    if (group.spawnInterval < waveSet.globalDefaults.spawnMinSpacing)
                    {
                        error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} group {g} spawnInterval is below spawnMinSpacing.";
                        return false;
                    }

                    if (!enemyCostById.TryGetValue(group.enemyId, out var threatCost))
                    {
                        error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} group {g} references unknown enemyId: {group.enemyId}.";
                        return false;
                    }

                    actualBudget += group.count * Mathf.Max(0f, threatCost);
                }

                var upperBound = wave.budgetTarget * safeTolerance;
                var lowerBound = wave.budgetTarget * (2f - safeTolerance);
                if (lowerBound > upperBound)
                {
                    var temp = lowerBound;
                    lowerBound = upperBound;
                    upperBound = temp;
                }

                if (actualBudget < lowerBound - 0.01f || actualBudget > upperBound + 0.01f)
                {
                    error = $"WaveSet {waveSet.waveSetId} wave {wave.waveIndex} budget out of range (target={wave.budgetTarget:0.##}, tolerance={safeTolerance:0.##}, actual={actualBudget:0.##}).";
                    return false;
                }
            }

            return true;
        }
    }
}
