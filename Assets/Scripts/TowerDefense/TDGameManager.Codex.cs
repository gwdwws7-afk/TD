// Freeze-period move: Codex cluster.
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
        private void RecordTowerUpgradeForAnalytics(TDTower tower, int cost)
        {
            var stat = GetOrCreateTowerStat(tower);
            if (stat == null)
            {
                return;
            }

            stat.upgrades++;
            stat.upgradeSpend += Mathf.Max(0, cost);
        }

        private void RegisterEnemySpawnForAnalytics(TDEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            var laneStat = GetOrCreateLaneStat(enemy.LaneKey);
            laneStat.spawned++;
            laneStat.spawnedHealth += Mathf.Max(1, enemy.MaxHealth);
            IncrementCounter(laneStat.enemySpawns, enemy.EnemyId);
        }

        private void RecordUnresolvedEnemyAtRunEnd(TDEnemy enemy)
        {
            if (enemy != null)
            {
                GetEnemyRoadSegmentStat(enemy).unresolvedAtEnd++;
            }
        }

        private void MigrateLegacyCodexDiscoveries()
        {
            foreach (var pair in _globalEnemyCatalog)
            {
                if (PlayerPrefs.GetInt(BuildCodexPlayerPrefsKey(pair.Key), 0) > 0)
                {
                    TDCampaignProgression.RecordEnemyObservation(pair.Key, (int)TDEnemyCodexObservation.Sighted);
                }
            }
        }

        private void RecordEnemyCodexObservation(string enemyId, TDEnemyCodexObservation observation)
        {
            if (TDCampaignProgression.RecordEnemyObservation(enemyId, (int)observation))
            {
                RefreshMetaProgressionRewards(true);
            }
        }

        private void RecordTowerCodexObservation(TDTowerKind kind, TDTowerCodexObservation observation)
        {
            if (TDCampaignProgression.RecordTowerObservation(TDTower.GetTowerId(kind), (int)observation))
            {
                RefreshMetaProgressionRewards(true);
            }
        }

        private int GetCompletedEnemyDossierCount()
        {
            var count = 0;
            foreach (var pair in _globalEnemyCatalog)
            {
                var required = GetRequiredEnemyDossierFlags(pair.Value);
                if ((TDCampaignProgression.GetEnemyObservationFlags(pair.Key) & required) == required)
                {
                    count++;
                }
            }

            return count;
        }

        private int GetCompletedTowerDossierCount()
        {
            var required = (int)(TDTowerCodexObservation.Built | TDTowerCodexObservation.DamageBranch |
                                 TDTowerCodexObservation.UtilityBranch | TDTowerCodexObservation.SpecializationProc);
            return TDTower.GetBuildOrder().Count(kind =>
                (TDCampaignProgression.GetTowerObservationFlags(TDTower.GetTowerId(kind)) & required) == required);
        }

        private void RefreshMetaProgressionRewards(bool showFeedback)
        {
            var meta = _campaign?.metaProgression;
            if (meta == null)
            {
                return;
            }

            var summary = GetCampaignProgressSummary();
            var enemyDossiers = GetCompletedEnemyDossierCount();
            var towerDossiers = GetCompletedTowerDossierCount();
            foreach (var reward in (meta.ratingRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>())
                         .Concat(meta.codexRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>()))
            {
                // Ruling B4 (2026-08-24): stars are the SOLE unlock currency —
                // summary.earnedStars counts each level's bestStars once (never
                // per-difficulty), and difficulty seals are display-only on the
                // world map. Do not add seal counting here: 20×2 seals would
                // vault every threshold instantly.
                var current = reward.sourceType switch
                {
                    "campaign_stars" => summary.earnedStars,
                    "enemy_dossiers" => enemyDossiers,
                    "tower_dossiers" => towerDossiers,
                    _ => 0
                };
                if (current < reward.threshold || !TDCampaignProgression.ClaimMetaReward(reward.rewardId, reward.unlockProtocolId))
                {
                    continue;
                }

                if (showFeedback)
                {
                    PushTacticalEvent($"Meta reward: {reward.displayName} -> {GetTacticalProtocol(reward.unlockProtocolId)?.displayName}", 6.4f);
                }
            }
        }

        private static string BuildCodexPlayerPrefsKey(string enemyId)
        {
            return $"{CodexPlayerPrefsPrefix}{(string.IsNullOrWhiteSpace(enemyId) ? "unknown" : enemyId.Trim().ToLowerInvariant())}";
        }

        private void RegisterEnemyEncounter(TDEnemyCatalogEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId))
            {
                return;
            }

            var firstEncounter = _encounteredEnemyIds.Add(entry.enemyId);
            RecordEnemyCodexObservation(entry.enemyId, TDEnemyCodexObservation.Sighted);
            if (!firstEncounter)
            {
                return;
            }

            PlayerPrefs.SetInt(BuildCodexPlayerPrefsKey(entry.enemyId), 1);
            PlayerPrefs.Save();
            _codexDiscoveriesThisRun++;

            var label = !string.IsNullOrWhiteSpace(entry.displayName) ? entry.displayName : GetEnemyDisplayName(entry.enemyId);
            var tagSummary = BuildEnemyTagSummary(entry, 3);
            var suffix = string.IsNullOrWhiteSpace(tagSummary) ? string.Empty : $" [{tagSummary}]";
            PushTacticalEvent($"Codex +1: First sighting {label}{suffix}", 6.0f);

            // Teaching copy (imbalance diagnosis appendix C.4): fast enemies
            // debut on L03 — surface the evasion counter-tip once, on the
            // same first-sighting moment the codex unlock fires.
            if (entry.tags != null && entry.tags.Any(tag => string.Equals(tag, "fast", StringComparison.OrdinalIgnoreCase)))
            {
                var evasionTip = TDLocalization.IsChinese
                    ? "注意：高速目标会闪避慢速单发弹——先用范围伤害或多次命中压制它们，或让减速先生效。"
                    : "Fast targets evade slow single shots — soften them with splash or repeated hits, or slow them first.";
                SetStatus(evasionTip);
                PushTacticalEvent(evasionTip, 7.0f);
            }

            // Resonance teaching copy step 5 (resonance-teaching-copy-v1):
            // the leech counter-teaching rides its first sighting.
            if (string.Equals(entry.enemyId, "ember_leech", StringComparison.Ordinal))
            {
                var leechTip = TDLocalization.IsChinese
                    ? "余烬水蛭活着的时候，会持续吸走你的电荷。看到它们，优先打死——你的窗口，就是它们的口粮。"
                    : "Ember Leeches drain your charge while they live. Kill them first — your windows are their food.";
                SetStatus(leechTip);
                PushTacticalEvent(leechTip, 7.0f);
            }
        }

        /// <summary>
        /// One-time-per-save-slot teaching tips (resonance-teaching-copy-v1).
        /// Mirrors the tutorial's PlayerPrefs key pattern; copy is verbatim
        /// from the pack, bilingual via the established IsChinese ternary.
        /// </summary>
    }
}
