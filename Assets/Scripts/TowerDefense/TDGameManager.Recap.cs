// Freeze-period move: Recap cluster.
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
        private string BuildLocalizedChapterRewardLabel()
        {
            return _newlyClaimedChapterReward == null
                ? string.Empty
                : $"   章节奖励：{TDLocalization.LocalizeRuntimeString(_newlyClaimedChapterReward.displayName)}";
        }

        private void UpdateRunResultResponsiveScale()
        {
            var scale = Screen.height <= 600
                ? 1.22f
                : Screen.height <= 760 ? 1.10f : 1f;
            _uiGameOverRoot.localScale = Vector3.one * scale;
        }

        private bool IsFullCampaignCompletionResult(TDCampaignProgressSummary summary)
        {
            return _victory && _campaignRoute?.level != null && summary != null &&
                   _campaignRoute.level.levelIndex == _campaignRoute.totalLevels &&
                   summary.clearedLevels == summary.totalLevels;
        }

        private void UpdateCampaignCompletionUi(TDCampaignProgressSummary summary)
        {
            var masteredChapters = GetMasteredChapterCount();
            var rank = BuildCampaignRank(summary, masteredChapters);
            var campaignPerfected = summary.emberTrialClears == summary.totalLevels;
            var totalAttempts = 0;
            var perfectMissions = 0;
            var totalBestScore = 0;
            for (var level = 1; level <= summary.totalLevels; level++)
            {
                var progress = TDCampaignProgression.GetLevelProgress(level);
                totalAttempts += progress.attempts;
                totalBestScore += progress.bestTacticalScore;
                if (progress.bestStars == 3)
                {
                    perfectMissions++;
                }
            }

            var averageBestScore = summary.totalLevels > 0 ? Mathf.RoundToInt(totalBestScore / (float)summary.totalLevels) : 0;
            if (TDLocalization.IsChinese)
            {
                var victoryEnding = TDLocalization.LocalizeRuntimeString(
                    _examPresentationProfile?.victoryEnding ?? (campaignPerfected ? "CAMPAIGN PERFECTED" : "CAMPAIGN COMPLETE"));
                SetUiText(
                    _uiGameOverTitleText,
                    campaignPerfected
                        ? $"{victoryEnding}   余烬试炼"
                        : $"{victoryEnding}   评级 {rank}");
                SetUiText(
                    _uiGameOverBodyText,
                    $"余烬防线已守住   {summary.clearedLevels}/{summary.totalLevels} 项任务完成\n" +
                    $"最终行动 L{_campaignRoute.level.levelIndex:00}   {TDLocalization.LocalizeRuntimeString(GetDifficultyShortLabel(_activeCampaignDifficulty))}   击杀 {_totalKills}   剩余防线 {_lineIntegrity}");
                SetUiText(
                    _uiGameOverScoreText,
                    $"战役精通   星级 {summary.earnedStars}/{summary.availableStars}   契约 {summary.completedContracts}/{summary.availableContracts}\n" +
                    $"精通章节 {masteredChapters}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   完美任务 {perfectMissions}/{summary.totalLevels}\n" +
                    $"挑战  老兵 {summary.veteranClears}/{summary.totalLevels}   余烬 {summary.emberTrialClears}/{summary.totalLevels}   图鉴  敌人 {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   防御塔 {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}");
            }
            else
            {
                SetUiText(
                    _uiGameOverTitleText,
                    campaignPerfected
                        ? $"{_examPresentationProfile?.victoryEnding ?? "CAMPAIGN PERFECTED"}   EMBER TRIAL"
                        : $"{_examPresentationProfile?.victoryEnding ?? "CAMPAIGN COMPLETE"}   RANK {rank}");
                SetUiText(
                    _uiGameOverBodyText,
                    $"EMBERLINE SECURED   {summary.clearedLevels}/{summary.totalLevels} missions cleared\n" +
                    $"Final operation L{_campaignRoute.level.levelIndex:00}   {GetDifficultyShortLabel(_activeCampaignDifficulty)}   {_totalKills} kills   {_lineIntegrity} integrity remaining");
                SetUiText(
                    _uiGameOverScoreText,
                    $"CAMPAIGN MASTERY   STARS {summary.earnedStars}/{summary.availableStars}   CONTRACTS {summary.completedContracts}/{summary.availableContracts}\n" +
                    $"MASTERED CHAPTERS {masteredChapters}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   PERFECT MISSIONS {perfectMissions}/{summary.totalLevels}\n" +
                    $"CHALLENGE V {summary.veteranClears}/{summary.totalLevels}   EMBER {summary.emberTrialClears}/{summary.totalLevels}   DOSSIERS E {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}  T {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}");
            }

            SetUiText(_uiGameOverLaneText, BuildCampaignCompletionChapterLabel());
            SetUiText(
                _uiGameOverTowerText,
                TDLocalization.IsChinese
                    ? $"战役记录\n部署次数 {totalAttempts}\n平均最佳战术评分 {averageBestScore}\n最远前线 L{summary.highestUnlockedLevel:00}\n存档版本 {TDCampaignProgression.SaveVersion}"
                    : $"CAMPAIGN RECORD\nDeployments {totalAttempts}\nAverage best tactical score {averageBestScore}\nFrontier L{summary.highestUnlockedLevel:00}\nSave version {TDCampaignProgression.SaveVersion}");
            SetUiText(_uiGameOverHeatText, BuildCampaignCompletionRewardLabel());
            SetUiText(
                _uiGameOverFailureText,
                TDLocalization.IsChinese
                    ? _newlyClaimedChapterReward == null
                        ? $"档案状态   {TDCampaignProgression.GetClaimedChapterRewardIds().Length} 项章节奖励生效"
                        : $"最终奖励已取得   {TDLocalization.LocalizeRuntimeString(_newlyClaimedChapterReward.displayName)}"
                    : _newlyClaimedChapterReward == null
                        ? $"ARCHIVE STATUS   {TDCampaignProgression.GetClaimedChapterRewardIds().Length} chapter rewards active"
                        : $"FINAL REWARD SECURED   {_newlyClaimedChapterReward.displayName}");
            SetUiText(
                _uiGameOverRecapText,
                TDLocalization.IsChinese
                    ? campaignPerfected
                        ? "全部任务均已在余烬试炼压力下完成，完整挑战档案已经解锁。"
                        : $"全战役现可使用全部已解锁防御塔、阵容与长期奖励重玩。评级 {rank} 综合星级、契约与章节精通。"
                    : campaignPerfected
                        ? "Every mission has now been cleared under Ember Trial pressure. The complete challenge archive is secured."
                        : $"The full campaign is now replayable with every unlocked tower, formation and claimed legacy bonus. Rank {rank} reflects stars, contracts and complete chapter mastery.");
            SetUiText(_uiGameOverRecommendationText, BuildCampaignCompletionRecommendationLabel());
        }

        private string BuildCampaignCompletionChapterLabel()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            var lines = new List<string> { TDLocalization.IsChinese ? "章节精通" : "CHAPTER MASTERY" };
            for (var i = 0; i < chapters.Length; i++)
            {
                var progress = TDCampaignProgression.BuildChapterSummary(chapters[i]);
                lines.Add(TDLocalization.IsChinese
                    ? $"{(char)('A' + i)} {(progress.mastered ? "已精通" : "已通关")}   星 {progress.earnedStars}/{progress.availableStars}   契 {progress.completedContracts}/{progress.availableContracts}   奖 {(progress.rewardClaimed ? "生效" : "待领取")}"
                    : $"{(char)('A' + i)} {(progress.mastered ? "MASTERED" : "CLEARED")}   S {progress.earnedStars}/{progress.availableStars}   C {progress.completedContracts}/{progress.availableContracts}   R {(progress.rewardClaimed ? "ACTIVE" : "READY")}");
            }

            return string.Join("\n", lines);
        }

        private string BuildCampaignCompletionRecommendationLabel()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            var targets = new List<string>();
            for (var i = 0; i < chapters.Length; i++)
            {
                var progress = TDCampaignProgression.BuildChapterSummary(chapters[i]);
                var missingStars = progress.availableStars - progress.earnedStars;
                var missingContracts = progress.availableContracts - progress.completedContracts;
                if (missingStars > 0 || missingContracts > 0)
                {
                    targets.Add(TDLocalization.IsChinese
                        ? $"章节 {(char)('A' + i)}：星级 +{missingStars} / 契约 +{missingContracts}"
                        : $"Chapter {(char)('A' + i)}: +{missingStars} stars / +{missingContracts} contracts");
                }
            }

            if (targets.Count == 0)
            {
                return TDLocalization.IsChinese
                    ? "下一目标   已达成全部精通。使用不同阵容、学说与 A/B 布防重玩战役。"
                    : "NEXT OBJECTIVE   Full mastery achieved. Compare alternate formations, doctrines and A/B layouts across the campaign.";
            }

            return TDLocalization.IsChinese
                ? $"下一精通目标   {string.Join("   |   ", targets.GetRange(0, Mathf.Min(3, targets.Count)))}"
                : $"NEXT MASTERY TARGETS   {string.Join("   |   ", targets.GetRange(0, Mathf.Min(3, targets.Count)))}";
        }

        private int GetMasteredChapterCount()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            var mastered = 0;
            for (var i = 0; i < chapters.Length; i++)
            {
                if (TDCampaignProgression.BuildChapterSummary(chapters[i]).mastered)
                {
                    mastered++;
                }
            }

            return mastered;
        }

        private string BuildCampaignRank(TDCampaignProgressSummary summary, int masteredChapters)
        {
            if (summary == null || summary.clearedLevels < summary.totalLevels)
            {
                return "IN PROGRESS";
            }

            var chapterCount = Mathf.Max(1, _campaign?.chapters?.Length ?? 0);
            var starRatio = summary.availableStars > 0 ? summary.earnedStars / (float)summary.availableStars : 0f;
            var contractRatio = summary.availableContracts > 0 ? summary.completedContracts / (float)summary.availableContracts : 0f;
            var masteryRatio = masteredChapters / (float)chapterCount;
            var rating = (starRatio * 0.60f) + (contractRatio * 0.30f) + (masteryRatio * 0.10f);
            if (rating >= 0.98f)
            {
                return "S";
            }

            if (rating >= 0.85f)
            {
                return "A";
            }

            if (rating >= 0.70f)
            {
                return "B";
            }

            return "C";
        }

        private string BuildCampaignChapterArchiveLabel()
        {
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            var lines = new List<string>(chapters.Length * 2);
            for (var i = 0; i < chapters.Length; i++)
            {
                var chapter = chapters[i];
                var progress = TDCampaignProgression.BuildChapterSummary(chapter);
                var state = progress.mastered ? "MASTERED" : progress.cleared ? "CLEARED" : "IN PROGRESS";
                var rewardState = progress.rewardClaimed ? "ACTIVE" : progress.cleared ? "READY" : "LOCKED";
                lines.Add($"CHAPTER {(char)('A' + i)}  {state}   REWARD {rewardState}   {chapter?.reward?.displayName ?? "No reward"}");
                lines.Add($"CLEAR {progress.clearedLevels}/{progress.totalLevels}   STAR {progress.earnedStars}/{progress.availableStars}   CONTRACT {progress.completedContracts}/{progress.availableContracts}   V {progress.veteranClears}/{progress.totalLevels}   E {progress.emberTrialClears}/{progress.totalLevels}");
                var exams = (_campaign?.levels ?? Array.Empty<TDCampaignLevelDefinition>())
                    .Where(level => level != null && level.chapterId == chapter?.chapterId && level.scenario?.milestoneExam == true)
                    .Select(level =>
                    {
                        var record = TDCampaignProgression.GetLevelProgress(level.levelIndex);
                        var formation = BuildArchivedFormationSignature(record.towerLoadout);
                        return $"L{level.levelIndex:00} {record.bestTacticalScore:00}P {formation} / {TDCampaignProgression.GetTacticalProtocol(level.levelIndex).ToUpperInvariant()}";
                    })
                    .ToArray();
                if (exams.Length > 0)
                {
                    lines.Add($"EXAM SIGNATURE  {string.Join("   ", exams)}");
                }
            }

            return string.Join("\n", lines);
        }

        private static string BuildArchivedFormationSignature(string rawLoadout)
        {
            if (string.IsNullOrWhiteSpace(rawLoadout))
            {
                return "UNRECORDED";
            }

            var labels = new List<string>();
            foreach (var towerId in rawLoadout.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (TDTower.TryParseTowerId(towerId, out var kind))
                {
                    labels.Add(kind switch
                    {
                        TDTowerKind.RailLancer => "RL",
                        TDTowerKind.CinderMortar => "CM",
                        TDTowerKind.FrostCoil => "FC",
                        TDTowerKind.ArcWelder => "AW",
                        TDTowerKind.SiegeDrill => "SD",
                        TDTowerKind.EmberFlak => "EF",
                        TDTowerKind.ResonanceBeacon => "RB",
                        TDTowerKind.GravSnare => "GS",
                        _ => "?"
                    });
                }
            }

            return labels.Count == 0 ? "UNRECORDED" : string.Join("-", labels);
        }

        private string BuildCampaignRewardBonusLabel()
        {
            CalculateClaimedChapterRewardBonuses(out var budget, out var integrity, out var resonance, out var rewardNames);
            var resonanceBonus = Mathf.RoundToInt((resonance - 1f) * 100f);
            var unlockedProtocols = TDCampaignProgression.GetUnlockedProtocolIds();
            var metaRewards = TDCampaignProgression.GetClaimedMetaRewardIds();
            return
                $"ACTIVE LEGACY BONUSES   Budget +{budget}   Integrity +{integrity}   Resonance +{resonanceBonus}%\n" +
                $"REWARDS {rewardNames.Count}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   {(rewardNames.Count == 0 ? "None claimed" : string.Join(" / ", rewardNames))}\n" +
                $"TACTICAL PROTOCOLS {unlockedProtocols.Length + 1}/{Mathf.Max(1, _campaign?.metaProgression?.tacticalProtocols?.Length ?? 1)}   META REWARDS {metaRewards.Length}   {BuildMetaRewardProgressLabel()}\n" +
                $"CODEX DOSSIERS   ENEMY {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   TOWER {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}";
        }

        private string BuildCampaignCompletionRewardLabel()
        {
            CalculateClaimedChapterRewardBonuses(out var budget, out var integrity, out var resonance, out var rewardNames);
            var resonanceBonus = Mathf.RoundToInt((resonance - 1f) * 100f);
            var unlockedProtocols = TDCampaignProgression.GetUnlockedProtocolIds();
            var metaRewards = TDCampaignProgression.GetClaimedMetaRewardIds();
            if (TDLocalization.IsChinese)
            {
                return
                    $"长期增益   资源 +{budget}   防线 +{integrity}   共鸣 +{resonanceBonus}%\n" +
                    $"奖励 {rewardNames.Count}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   协议 {unlockedProtocols.Length + 1}/{Mathf.Max(1, _campaign?.metaProgression?.tacticalProtocols?.Length ?? 1)}   长期奖励 {metaRewards.Length}\n" +
                    $"图鉴   敌人 {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   防御塔 {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}";
            }

            return
                $"LEGACY   BUDGET +{budget}   INTEGRITY +{integrity}   RESONANCE +{resonanceBonus}%\n" +
                $"REWARDS {rewardNames.Count}/{Mathf.Max(1, _campaign?.chapters?.Length ?? 0)}   PROTOCOLS {unlockedProtocols.Length + 1}/{Mathf.Max(1, _campaign?.metaProgression?.tacticalProtocols?.Length ?? 1)}   META {metaRewards.Length}\n" +
                $"DOSSIERS   ENEMY {GetCompletedEnemyDossierCount()}/{GetCodexTotalCount()}   TOWER {GetCompletedTowerDossierCount()}/{TDTower.GetBuildOrder().Count}   {BuildMetaRewardProgressLabel()}";
        }

        private string BuildMetaRewardProgressLabel()
        {
            var summary = GetCampaignProgressSummary();
            var ratingTarget = (_campaign?.metaProgression?.ratingRewards ?? Array.Empty<TDCampaignMetaRewardDefinition>())
                .Where(reward => !TDCampaignProgression.GetClaimedMetaRewardIds().Contains(reward.rewardId))
                .Select(reward => reward.threshold)
                .DefaultIfEmpty(summary.earnedStars)
                .Min();
            return $"NEXT S {summary.earnedStars}/{ratingTarget} E {GetCompletedEnemyDossierCount()}/4 T {GetCompletedTowerDossierCount()}/4";
        }

        private void CalculateClaimedChapterRewardBonuses(
            out int budget,
            out int integrity,
            out float resonance,
            out List<string> rewardNames)
        {
            budget = 0;
            integrity = 0;
            resonance = 1f;
            rewardNames = new List<string>();
            var chapters = _campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>();
            for (var i = 0; i < chapters.Length; i++)
            {
                var reward = chapters[i]?.reward;
                if (reward == null || !TDCampaignProgression.IsChapterRewardClaimed(reward.rewardId))
                {
                    continue;
                }

                budget += Mathf.Max(0, reward.startingBudgetBonus);
                integrity += Mathf.Max(0, reward.startingIntegrityBonus);
                resonance *= ResolveMutatorMultiplier(reward.resonanceGainMultiplier);
                rewardNames.Add(reward.displayName);
            }
        }

        private TDCampaignChapterDefinition GetCampaignChapterAt(int chapterIndex)
        {
            return _campaign?.chapters != null && chapterIndex >= 0 && chapterIndex < _campaign.chapters.Length
                ? _campaign.chapters[chapterIndex]
                : null;
        }

        private TDCampaignChapterDefinition GetCampaignChapter(string chapterId)
        {
            if (_campaign?.chapters == null)
            {
                return null;
            }

            for (var i = 0; i < _campaign.chapters.Length; i++)
            {
                var chapter = _campaign.chapters[i];
                if (chapter != null && string.Equals(chapter.chapterId, chapterId, StringComparison.OrdinalIgnoreCase))
                {
                    return chapter;
                }
            }

            return null;
        }

        private static string GetDifficultyRecordLabel(TDCampaignLevelProgress progress)
        {
            if (progress == null || !progress.cleared)
            {
                return "UNTESTED";
            }

            return progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.EmberTrial
                ? "EMBER TRIAL"
                : progress.highestDifficultyCleared >= (int)TDCampaignDifficultyTier.Veteran
                    ? "VETERAN"
                    : "STANDARD";
        }

        private static int ApplyRunSurvivalScoreCap(
            int rawScore,
            bool gameOver,
            bool victory,
            int startingIntegrity,
            int remainingIntegrity,
            int integrityDamageTaken)
        {
            var score = Mathf.Clamp(rawScore, 0, 100);
            if (!gameOver)
            {
                return score;
            }

            if (!victory)
            {
                return Mathf.Min(score, 59);
            }

            var safeStartingIntegrity = Mathf.Max(1, startingIntegrity);
            var finalRetention = Mathf.Clamp01(remainingIntegrity / (float)safeStartingIntegrity);
            var pressureRetention = Mathf.Clamp01(1f - (Mathf.Max(0, integrityDamageTaken) / (float)safeStartingIntegrity));
            var survivalQuality = Mathf.Min(finalRetention, pressureRetention);
            if (survivalQuality < 0.25f)
            {
                return Mathf.Min(score, 69);
            }

            if (survivalQuality < 0.50f)
            {
                return Mathf.Min(score, 79);
            }

            if (survivalQuality < 0.80f)
            {
                return Mathf.Min(score, 89);
            }

            return score;
        }

        private int CalculateRunCoverageScore()
        {
            var totalSpawned = 0;
            var totalKills = 0;
            var weakestLaneClear = 100f;
            var laneCount = 0;
            foreach (var pair in _laneStats)
            {
                var stat = pair.Value;
                if (stat == null || stat.spawned <= 0)
                {
                    continue;
                }

                totalSpawned += stat.spawned;
                totalKills += stat.kills;
                weakestLaneClear = Mathf.Min(weakestLaneClear, stat.kills / (float)stat.spawned * 100f);
                laneCount++;
            }

            if (totalSpawned <= 0 || laneCount <= 0)
            {
                return 0;
            }

            var overallClear = totalKills / (float)totalSpawned * 100f;
            var integrityRetention = Mathf.Clamp01(_lineIntegrity / (float)Mathf.Max(1, _startingLineIntegrity)) * 100f;
            return Mathf.Clamp(Mathf.RoundToInt(
                (overallClear * 0.35f) + (weakestLaneClear * 0.20f) + (integrityRetention * 0.45f)), 0, 100);
        }

        private int CalculateRunCounterScore()
        {
            if (GetTotalLaneSpawned() <= 0)
            {
                return 0;
            }

            var actionableDamage = 0;
            var matchedActionableDamage = 0;
            foreach (var pair in _threatCategoryDamage)
            {
                if (!IsCounterCategoryActionable(pair.Key))
                {
                    continue;
                }

                actionableDamage += Mathf.Max(0, pair.Value);
                if (_threatCategoryCounterDamage.TryGetValue(pair.Key, out var matched))
                {
                    matchedActionableDamage += Mathf.Max(0, matched);
                }
            }

            if (actionableDamage <= 0)
            {
                return 100;
            }

            var matchRate = Mathf.Clamp01(matchedActionableDamage / (float)actionableDamage);
            return Mathf.Clamp(Mathf.RoundToInt(20f + (matchRate * 80f)), 0, 100);
        }

        private int CalculateRunOutputScore()
        {
            var totalSpawned = GetTotalLaneSpawned();
            var totalSpawnedHealth = GetTotalLaneSpawnedHealth();
            if (totalSpawned <= 0 || totalSpawnedHealth <= 0)
            {
                return 0;
            }

            var killRate = Mathf.Clamp01(GetTotalLaneKills() / (float)totalSpawned);
            var damageCompletion = Mathf.Clamp01(_totalDamageDealt / (float)totalSpawnedHealth);
            return Mathf.Clamp(Mathf.RoundToInt(((damageCompletion * 0.55f) + (killRate * 0.45f)) * 100f), 0, 100);
        }

        private int CalculateRunEconomyScore()
        {
            var totalSpend = 0;
            var engagedSpend = 0;
            foreach (var pair in _towerStats)
            {
                var stat = pair.Value;
                if (stat == null)
                {
                    continue;
                }

                totalSpend += stat.TotalSpend;
                if (stat.damageDealt > 0 || stat.controlApplications > 0 || stat.utilitySpecProcs > 0)
                {
                    engagedSpend += stat.TotalSpend;
                }
            }

            if (totalSpend <= 0)
            {
                return 0;
            }

            const float targetDamagePerBudget = 6f;
            var efficiency = Mathf.Clamp01(_totalDamageDealt / Mathf.Max(1f, totalSpend * targetDamagePerBudget));
            var utilization = Mathf.Clamp01(engagedSpend / (float)totalSpend);
            var upgradeConversion = _budgetSpentOnUpgrades <= 0
                ? 0f
                : Mathf.Clamp01(_budgetSpentOnUpgrades / Mathf.Max(1f, totalSpend * 0.35f));
            return Mathf.Clamp(Mathf.RoundToInt(
                ((efficiency * 0.55f) + (utilization * 0.30f) + (upgradeConversion * 0.15f)) * 100f), 0, 100);
        }

        private int CalculateRunCommandScore()
        {
            if (GetTotalLaneSpawned() <= 0)
            {
                return 0;
            }

            if (!_isResonanceSystemEnabled)
            {
                return 100;
            }

            if (_resonanceWindowsTriggered <= 0)
            {
                return 60;
            }

            var useRate = Mathf.Clamp01(_resonanceCommandsUsed / (float)_resonanceWindowsTriggered);
            var matchRate = _resonanceCommandsUsed <= 0
                ? 0f
                : Mathf.Clamp01(_resonanceMatchedCommands / (float)_resonanceCommandsUsed);
            var bonusImpact = _totalDamageDealt <= 0
                ? 0f
                : Mathf.Clamp01(_resonanceBonusDamage / Mathf.Max(1f, _totalDamageDealt * 0.12f));
            return Mathf.Clamp(Mathf.RoundToInt(
                ((useRate * 0.45f) + (matchRate * 0.35f) + (bonusImpact * 0.20f)) * 100f), 0, 100);
        }

        private int GetTotalLaneSpawned()
        {
            var total = 0;
            foreach (var pair in _laneStats)
            {
                total += Mathf.Max(0, pair.Value?.spawned ?? 0);
            }

            return total;
        }

        private int GetTotalLaneSpawnedHealth()
        {
            var total = 0;
            foreach (var pair in _laneStats)
            {
                total += Mathf.Max(0, pair.Value?.spawnedHealth ?? 0);
            }

            return total;
        }

        private int GetTotalLaneKills()
        {
            var total = 0;
            foreach (var pair in _laneStats)
            {
                total += Mathf.Max(0, pair.Value?.kills ?? 0);
            }

            return total;
        }

        private int CalculateCurrentMissionStars()
        {
            if (!_victory)
            {
                return 0;
            }

            var stars = 1;
            if (_lineIntegrity >= GetMissionIntegrityStarThreshold())
            {
                stars++;
            }

            if (CalculateRunScore().total >= MissionTacticalStarThreshold)
            {
                stars++;
            }

            return Mathf.Clamp(stars, 1, 3);
        }

        private void RecordCampaignResultIfNeeded()
        {
            if (_campaignResultRecorded)
            {
                return;
            }

            _campaignResultRecorded = true;
            _currentMissionStars = CalculateCurrentMissionStars();
            if (_campaignRoute?.level == null)
            {
                return;
            }

            var score = CalculateRunScore();
            var contract = EvaluateCurrentMissionContract();
            _currentMissionContractCompleted = contract?.completed ?? false;
            _campaignProgressUpdate = TDCampaignProgression.RecordResult(
                _campaignRoute.level.levelIndex,
                _victory,
                _currentMissionStars,
                score.total,
                _lineIntegrity,
                _campaignRoute.totalLevels,
                _currentMissionContractCompleted,
                _activeCampaignDifficulty);
            _newlyClaimedChapterReward = TryAutoClaimCompletedChapterReward();
            RefreshMetaProgressionRewards(true);
            _missionBoardNeedsRefresh = true;
            SettleMetaResidue();
            var summary = GetCampaignProgressSummary();
            Debug.Log(
                $"[TD][CampaignProgress] level={_campaignRoute.level.levelIndex} victory={_victory} stars={_currentMissionStars} " +
                $"bestStars={_campaignProgressUpdate.bestStars} score={score.total} bestScore={_campaignProgressUpdate.bestTacticalScore} " +
                $"firstClear={_campaignProgressUpdate.firstClear} nextUnlocked={_campaignProgressUpdate.nextLevelUnlocked} " +
                $"contract={_currentMissionContractCompleted} firstContract={_campaignProgressUpdate.firstContractCompletion} " +
                $"cleared={summary.clearedLevels}/{summary.totalLevels} totalStars={summary.earnedStars}/{summary.availableStars} " +
                $"contracts={summary.completedContracts}/{summary.availableContracts} frontier={summary.highestUnlockedLevel} " +
                $"difficulty={_activeCampaignDifficulty} bestDifficulty={_campaignProgressUpdate.highestDifficultyCleared} " +
                $"chapterReward={_newlyClaimedChapterReward?.rewardId ?? "none"}");
        }

        /// <summary>
        /// Meta residue settlement (spec: meta-upgrade-system-spec-v1,
        /// guardrail 2). Runs AFTER the campaign result is recorded so the
        /// first-capture flag can be derived from the recorded update, and
        /// never touches the in-run economy.
        /// </summary>
        private void SettleMetaResidue()
        {
            if (_campaignRoute?.level == null || _campaignProgressUpdate == null)
            {
                return;
            }

            // First capture comes from the RECORDED update: its
            // raisedDifficultyCleared flag is derived against the previous
            // progress, and update.victory is false when RecordResult
            // refused a locked level — the run then pays no residue either.
            var firstCapture = _campaignProgressUpdate.victory &&
                               _campaignProgressUpdate.raisedDifficultyCleared;
            var residue = TDMetaUpgradeSystem.SettleRunResidue(
                _currentMissionStars,
                _activeCampaignDifficulty,
                firstCapture,
                _campaignProgressUpdate.victory,
                _wave,
                GetConfiguredWaveCount());
            if (residue <= 0)
            {
                return;
            }

            TDCampaignProgression.AddEmberResidue(residue);
            var label = TDLocalization.IsChinese ? $"余烬残渣 +{residue}" : $"Ember Residue +{residue}";
            SetStatus(label);
            PushTacticalEvent(label, 6.0f);
        }

        /// <summary>Meta line rank for the active slot (tiny string parsed
        /// per query; call sites are level-load / sell / wave-clear).</summary>
        private int GetMetaRank(TDMetaUpgradeSystem.UpgradeLine line)
        {
            var ranks = TDMetaUpgradeSystem.ParseRanks(TDCampaignProgression.GetMetaUpgradeRanks());
            return ranks.TryGetValue(line, out var rank) ? rank : 0;
        }

        private TDCampaignChapterRewardDefinition TryAutoClaimCompletedChapterReward()
        {
            if (!_victory || _campaignRoute?.level == null)
            {
                return null;
            }

            var chapter = GetCampaignChapter(_campaignRoute.level.chapterId);
            var chapterProgress = TDCampaignProgression.BuildChapterSummary(chapter);
            var reward = chapter?.reward;
            if (reward == null || !chapterProgress.cleared || chapterProgress.rewardClaimed ||
                !TDCampaignProgression.ClaimChapterReward(reward.rewardId))
            {
                return null;
            }

            PushTacticalEvent($"Chapter reward secured: {reward.displayName}", 6.4f);
            return reward;
        }

        private void SetRunResultChartsVisible(bool visible)
        {
            _uiGameOverScoreChartRoot?.gameObject.SetActive(visible);
            _uiGameOverLaneChartRoot?.gameObject.SetActive(visible);
            _uiGameOverTowerChartRoot?.gameObject.SetActive(visible);
            if (visible)
            {
                SetRunResultTextSize(_uiGameOverBodyText, 12);
                SetRunResultTextSize(_uiGameOverScoreText, 14);
                SetRunResultTextSize(_uiGameOverLaneText, 12);
                SetRunResultTextSize(_uiGameOverTowerText, 12);
                SetRunResultTextSize(_uiGameOverHeatText, 12);
                SetRunResultTextSize(_uiGameOverFailureText, 12);
                SetRunResultTextSize(_uiGameOverRecapText, 12);
                SetRunResultTextSize(_uiGameOverRecommendationText, 12);
                SetRunResultTextRect(_uiGameOverBodyText, new Vector2(28f, -74f), new Vector2(704f, 20f));
                SetRunResultTextRect(_uiGameOverScoreText, new Vector2(28f, -98f), new Vector2(704f, 22f));
                SetRunResultTextRect(_uiGameOverLaneText, new Vector2(28f, -184f), new Vector2(338f, 18f));
                SetRunResultTextRect(_uiGameOverTowerText, new Vector2(394f, -184f), new Vector2(338f, 18f));
                SetRunResultTextRect(_uiGameOverHeatText, new Vector2(28f, -296f), new Vector2(704f, 44f));
                SetRunResultTextRect(_uiGameOverFailureText, new Vector2(28f, -344f), new Vector2(704f, 26f));
                SetRunResultTextRect(_uiGameOverRecapText, new Vector2(28f, -374f), new Vector2(704f, 50f));
                SetRunResultTextRect(_uiGameOverRecommendationText, new Vector2(28f, -430f), new Vector2(704f, 92f));
                return;
            }

            SetRunResultTextSize(_uiGameOverBodyText, 12);
            SetRunResultTextSize(_uiGameOverScoreText, 14);
            SetRunResultTextSize(_uiGameOverLaneText, 11);
            SetRunResultTextSize(_uiGameOverTowerText, 11);
            SetRunResultTextSize(_uiGameOverHeatText, 11);
            SetRunResultTextSize(_uiGameOverFailureText, 11);
            SetRunResultTextSize(_uiGameOverRecapText, 11);
            SetRunResultTextSize(_uiGameOverRecommendationText, 11);
            SetRunResultTextRect(_uiGameOverBodyText, new Vector2(28f, -52f), new Vector2(704f, 42f));
            SetRunResultTextRect(_uiGameOverScoreText, new Vector2(28f, -100f), new Vector2(704f, 70f));
            SetRunResultTextRect(_uiGameOverLaneText, new Vector2(28f, -178f), new Vector2(338f, 104f));
            SetRunResultTextRect(_uiGameOverTowerText, new Vector2(394f, -178f), new Vector2(338f, 104f));
            SetRunResultTextRect(_uiGameOverHeatText, new Vector2(28f, -290f), new Vector2(704f, 58f));
            SetRunResultTextRect(_uiGameOverFailureText, new Vector2(28f, -352f), new Vector2(704f, 30f));
            SetRunResultTextRect(_uiGameOverRecapText, new Vector2(28f, -388f), new Vector2(704f, 54f));
            SetRunResultTextRect(_uiGameOverRecommendationText, new Vector2(28f, -448f), new Vector2(704f, 78f));
        }

        private static void SetRunResultTextRect(Text text, Vector2 topLeft, Vector2 size)
        {
            if (text == null)
            {
                return;
            }

            text.rectTransform.anchoredPosition = topLeft;
            text.rectTransform.sizeDelta = size;
        }

        private static void SetRunResultTextSize(Text text, int fontSize)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.resizeTextMinSize = Mathf.Max(9, fontSize - 2);
            text.resizeTextMaxSize = fontSize;
        }

        private void UpdateRunResultCharts()
        {
            var score = CalculateRunScore();
            var scoreValues = new[] { score.coverage, score.counterMatch, score.output, score.economy, score.command };
            for (var i = 0; i < _uiGameOverScoreBarFills.Count && i < scoreValues.Length; i++)
            {
                SetRunResultBar(_uiGameOverScoreBarFills[i], scoreValues[i], 100f, 128f);
                SetUiText(_uiGameOverScoreBarValues[i], scoreValues[i].ToString());
            }

            var lanes = _laneStats.Values
                .Where(lane => lane != null && lane.spawned > 0)
                .OrderByDescending(lane => lane.spawned)
                .ThenBy(lane => lane.laneKey)
                .Take(_uiGameOverLaneBarRows.Count)
                .ToArray();
            for (var i = 0; i < _uiGameOverLaneBarRows.Count; i++)
            {
                var visible = i < lanes.Length;
                _uiGameOverLaneBarRows[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var lane = lanes[i];
                var clearPct = Mathf.RoundToInt(lane.kills / Mathf.Max(1f, lane.spawned) * 100f);
                var color = lane.escapes > 0
                    ? new Color(1f, 0.48f, 0.22f, 1f)
                    : new Color(0.28f, 0.82f, 1f, 1f);
                SetRunResultBar(_uiGameOverLaneBarFills[i], clearPct, 100f, 194f, color);
                _uiGameOverLaneBarLabels[i].color = color;
                SetUiText(
                    _uiGameOverLaneBarLabels[i],
                    TDLocalization.IsChinese
                        ? GetLocalizedLaneLabel(lane.laneKey)
                        : FormatLaneLabel(lane.laneKey).ToUpperInvariant());
                _uiGameOverLaneBarValues[i].color = color;
                SetUiText(
                    _uiGameOverLaneBarValues[i],
                    TDLocalization.IsChinese
                        ? $"{lane.kills}/{lane.spawned}  漏{lane.escapes}"
                        : $"{lane.kills}/{lane.spawned}  L{lane.escapes}");
            }

            var towers = GetSortedTowerStats().Take(_uiGameOverTowerBarRows.Count).ToArray();
            for (var i = 0; i < _uiGameOverTowerBarRows.Count; i++)
            {
                var visible = i < towers.Length;
                _uiGameOverTowerBarRows[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var tower = towers[i];
                var share = _totalDamageDealt <= 0
                    ? 0
                    : Mathf.RoundToInt(tower.damageDealt / (float)_totalDamageDealt * 100f);
                var color = i == 0
                    ? new Color(0.98f, 0.78f, 0.28f, 1f)
                    : new Color(0.34f, 0.90f, 0.58f, 1f);
                SetRunResultBar(_uiGameOverTowerBarFills[i], share, 100f, 194f, color);
                _uiGameOverTowerBarLabels[i].color = color;
                SetUiText(
                    _uiGameOverTowerBarLabels[i],
                    TDLocalization.IsChinese
                        ? GetLocalizedCompactTowerLabel(tower.kind)
                        : GetCompactTowerLabel(tower.kind).ToUpperInvariant());
                _uiGameOverTowerBarValues[i].color = color;
                SetUiText(
                    _uiGameOverTowerBarValues[i],
                    $"{share}% / {tower.kills}");
            }
        }

        private static void SetRunResultBar(Image fill, float value, float maximum, float width, Color? color = null)
        {
            if (fill == null)
            {
                return;
            }

            var ratio = Mathf.Clamp01(value / Mathf.Max(1f, maximum));
            fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, width * ratio), fill.rectTransform.sizeDelta.y);
            if (color.HasValue)
            {
                fill.color = color.Value;
            }
        }

        private string BuildLaneBreakdownLabel()
        {
            var lanes = new List<TDLaneRuntimeStat>();
            foreach (var pair in _laneStats)
            {
                if (pair.Value != null && pair.Value.spawned > 0)
                {
                    lanes.Add(pair.Value);
                }
            }

            lanes.Sort((a, b) =>
            {
                var delta = b.spawned.CompareTo(a.spawned);
                return delta != 0 ? delta : string.CompareOrdinal(a.laneKey, b.laneKey);
            });

            if (lanes.Count == 0)
            {
                return "LANE PERFORMANCE\nNo enemies deployed.";
            }

            var labels = new List<string> { "LANE PERFORMANCE  Killed / Spawned" };
            var max = Mathf.Min(4, lanes.Count);
            for (var i = 0; i < max; i++)
            {
                var lane = lanes[i];
                var clearPct = Mathf.RoundToInt(lane.kills / Mathf.Max(1f, lane.spawned) * 100f);
                labels.Add($"{FormatLaneLabel(lane.laneKey),-7} {lane.kills}/{lane.spawned}  Leak {lane.escapes}  Dmg {lane.damageDealt}  {clearPct}%");
            }

            if (lanes.Count > max)
            {
                labels.Add($"+{lanes.Count - max} more lanes in MCP report");
            }

            return string.Join("\n", labels);
        }

        private string BuildTowerContributionLabel()
        {
            var towers = GetSortedTowerStats();
            if (towers.Count == 0)
            {
                return "TOWER CONTRIBUTION\nNo towers built.";
            }

            var labels = new List<string> { "TOWER CONTRIBUTION  Damage Share" };
            var max = Mathf.Min(4, towers.Count);
            for (var i = 0; i < max; i++)
            {
                var tower = towers[i];
                var share = _totalDamageDealt <= 0 ? 0 : Mathf.RoundToInt(tower.damageDealt / (float)_totalDamageDealt * 100f);
                var ultimateProcs = tower.damageSpecProcs + tower.utilitySpecProcs;
                labels.Add($"{i + 1} {GetCompactTowerLabel(tower.kind)} @{tower.cell.x},{tower.cell.y} D{tower.damageDealt} K{tower.kills} C{tower.controlApplications} U{ultimateProcs} M{tower.matrixFullMatches} {share}%");
            }

            if (towers.Count > max)
            {
                labels.Add($"+{towers.Count - max} more towers in MCP report");
            }

            return string.Join("\n", labels);
        }

        private string BuildRoadHeatLabel()
        {
            var reports = BuildRoadHeatReports();
            if (reports.Count == 0)
            {
                return TDLocalization.IsChinese
                    ? "道路热区\n未记录路线压力。"
                    : "ROAD HEAT\nNo route pressure recorded.";
            }

            var labels = new List<string>();
            var firstLine = new List<string> { TDLocalization.IsChinese ? "道路热区" : "ROAD HEAT" };
            var max = Mathf.Min(3, reports.Count);
            for (var i = 0; i < max; i++)
            {
                var report = reports[i];
                var laneLabel = TDLocalization.IsChinese
                    ? GetLocalizedLaneLabel(report.stat.laneKey)
                    : FormatLaneLabel(report.stat.laneKey);
                var segmentLabel = TDLocalization.IsChinese
                    ? GetLocalizedRoadSegmentLabel(report.stat.segmentIndex)
                    : GetRoadSegmentLabel(report.stat.segmentIndex);
                var token = $"{i + 1} {laneLabel}/{segmentLabel} H{report.heatScore} C{report.coverageScore}";
                if (i < 2)
                {
                    firstLine.Add(token);
                }
                else
                {
                    labels.Add(token);
                }
            }

            labels.Insert(0, string.Join("   ", firstLine));
            return string.Join("\n", labels);
        }

        private List<TDRoadHeatReport> BuildRoadHeatReports()
        {
            if (_gameOver && _cachedRoadHeatReports != null)
            {
                return _cachedRoadHeatReports;
            }

            var reports = new List<TDRoadHeatReport>();
            var towers = UnityEngine.Object.FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            foreach (var pair in _laneStats)
            {
                var lane = pair.Value;
                if (lane == null || lane.spawned <= 0)
                {
                    continue;
                }

                for (var segment = 0; segment < RoadSegmentCount; segment++)
                {
                    var stat = GetOrCreateRoadSegmentStat(lane.laneKey, segment);
                    var coverage = CalculateRoadSegmentCoverageScore(lane.laneKey, segment, towers);
                    var nextReached = segment >= RoadSegmentCount - 1
                        ? 0
                        : GetOrCreateRoadSegmentStat(lane.laneKey, segment + 1).reached;
                    var heat = CalculateRoadSegmentHeatScore(lane, stat, nextReached, coverage);
                    var report = new TDRoadHeatReport
                    {
                        stat = stat,
                        coverageScore = coverage,
                        heatScore = heat
                    };
                    report.hasSuggestedCell = TryFindSuggestedBuildCell(lane.laneKey, segment, out report.suggestedCell);
                    reports.Add(report);
                }
            }

            reports.Sort((a, b) =>
            {
                var delta = b.heatScore.CompareTo(a.heatScore);
                if (delta != 0)
                {
                    return delta;
                }

                delta = string.CompareOrdinal(a.stat.laneKey, b.stat.laneKey);
                return delta != 0 ? delta : b.stat.segmentIndex.CompareTo(a.stat.segmentIndex);
            });
            if (_gameOver)
            {
                _cachedRoadHeatReports = reports;
            }

            return reports;
        }

        private bool TryFindSuggestedBuildCell(string laneKey, int segmentIndex, out Vector2Int cell)
        {
            cell = default;
            if (_gridMap == null || !_activeLanePaths.TryGetValue(laneKey, out var path) || path == null || path.Count <= 1)
            {
                return false;
            }

            var targetProgress = (segmentIndex + 0.5f) / RoadSegmentCount;
            var target = GetPathPointAtNormalizedProgress(path, targetProgress);
            var found = false;
            var bestDistance = float.MaxValue;
            for (var x = 0; x < GridWidth; x++)
            {
                for (var y = 0; y < GridHeight; y++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (!_gridMap.IsBuildable(candidate))
                    {
                        continue;
                    }

                    var distance = (_gridMap.CellToBuildWorld(candidate) - target).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        cell = candidate;
                        found = true;
                    }
                }
            }

            return found;
        }

        private static Vector3 GetPathPointAtNormalizedProgress(IReadOnlyList<Vector3> path, float progress)
        {
            if (path == null || path.Count == 0)
            {
                return Vector3.zero;
            }

            if (path.Count == 1)
            {
                return path[0];
            }

            var totalLength = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                totalLength += Vector3.Distance(path[i], path[i + 1]);
            }

            if (totalLength <= 0.0001f)
            {
                return path[0];
            }

            var targetDistance = Mathf.Clamp01(progress) * totalLength;
            var traversed = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var segmentLength = Vector3.Distance(path[i], path[i + 1]);
                if (traversed + segmentLength >= targetDistance)
                {
                    var local = segmentLength <= 0.0001f ? 0f : (targetDistance - traversed) / segmentLength;
                    return Vector3.Lerp(path[i], path[i + 1], Mathf.Clamp01(local));
                }

                traversed += segmentLength;
            }

            return path[path.Count - 1];
        }

        private static IReadOnlyList<Vector3> BuildRemainingPathFromNormalizedProgress(IReadOnlyList<Vector3> path, float progress)
        {
            if (path == null || path.Count <= 1)
            {
                return path ?? Array.Empty<Vector3>();
            }

            var clampedProgress = Mathf.Clamp(progress, 0f, 0.90f);
            if (clampedProgress <= 0.0001f)
            {
                return path;
            }

            var totalLength = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                totalLength += Vector3.Distance(path[i], path[i + 1]);
            }

            if (totalLength <= 0.0001f)
            {
                return path;
            }

            var targetDistance = clampedProgress * totalLength;
            var traversed = 0f;
            var nextPathIndex = 1;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var segmentLength = Vector3.Distance(path[i], path[i + 1]);
                if (traversed + segmentLength >= targetDistance)
                {
                    nextPathIndex = i + 1;
                    break;
                }

                traversed += segmentLength;
            }

            var remaining = new List<Vector3>(path.Count - nextPathIndex + 1)
            {
                GetPathPointAtNormalizedProgress(path, clampedProgress)
            };
            for (var i = nextPathIndex; i < path.Count; i++)
            {
                if ((remaining[remaining.Count - 1] - path[i]).sqrMagnitude > 0.0001f)
                {
                    remaining.Add(path[i]);
                }
            }

            if (remaining.Count == 1)
            {
                remaining.Add(path[path.Count - 1]);
            }

            return remaining;
        }

        private string BuildRunRecapLabel()
        {
            var waves = Mathf.Max(1, GetConfiguredWaveCount());
            var clearPct = Mathf.RoundToInt((_wavesCleared / (float)waves) * 100f);
            var leakPressure = Mathf.Max(0, _totalIntegrityDamageTaken);
            var economySpent = _budgetSpentOnBuilds + _budgetSpentOnUpgrades;
            var damagePerLeak = _totalEscapes <= 0 ? _totalDamageDealt : Mathf.RoundToInt(_totalDamageDealt / Mathf.Max(1f, _totalEscapes));
            var counterPct = _counterOpportunityDamage <= 0
                ? 100
                : Mathf.RoundToInt(_counterMatchedDamage / (float)_counterOpportunityDamage * 100f);

            if (TDLocalization.IsChinese)
            {
                return $"通关 {clearPct}%   伤害 {_totalDamageDealt}   每次漏怪伤害 {damagePerLeak}   克制 {counterPct}%\n" +
                       $"支出 {economySpent}   建造 {_budgetSpentOnBuilds}   升级 {_budgetSpentOnUpgrades} ({_upgradesPurchased})\n" +
                       $"防线 -{leakPressure}   装置 {_scenarioUses}/{_scenarioOpportunities}   指令 {_resonanceMatchedCommands}/{_resonanceCommandsUsed}   矩阵 {_matrixFullMatches}/{_matrixOpportunities}   汇聚 {_matrixConvergenceTriggers}";
            }

            return $"CLEAR {clearPct}%   DAMAGE {_totalDamageDealt}   PER LEAK {damagePerLeak}   COUNTER {counterPct}%\n" +
                   $"SPEND {economySpent}   BUILD {_budgetSpentOnBuilds}   UPGRADE {_budgetSpentOnUpgrades} ({_upgradesPurchased})\n" +
                   $"INTEGRITY -{leakPressure}   DEVICE {_scenarioUses}/{_scenarioOpportunities}   COMMAND {_resonanceMatchedCommands}/{_resonanceCommandsUsed}   MATRIX {_matrixFullMatches}/{_matrixOpportunities}   CONV {_matrixConvergenceTriggers}";
        }

        private string BuildRunRecommendationLabel()
        {
            var heatReports = BuildRoadHeatReports();
            var hotspot = heatReports
                .Where(report => report?.stat != null && report.stat.escapes + report.stat.unresolvedAtEnd > 0)
                .OrderByDescending(report => report.stat.escapes + report.stat.unresolvedAtEnd)
                .ThenByDescending(report => report.heatScore)
                .FirstOrDefault();
            hotspot ??= heatReports
                .Where(report => report?.stat != null && report.stat.reached > 0)
                .OrderBy(report => report.coverageScore)
                .ThenByDescending(report => report.heatScore)
                .FirstOrDefault();
            return $"1. {BuildHotspotRecommendation(hotspot)}\n" +
                   $"2. {BuildCounterCategoryRecommendation(hotspot)}\n" +
                   $"3. {BuildOperationalRecommendation(hotspot)}";
        }

        private string BuildHotspotRecommendation(TDRoadHeatReport hotspot)
        {
            if (hotspot?.stat == null)
            {
                return TDLocalization.IsChinese
                    ? "把第一座塔部署在压力最高的路线旁。"
                    : "Build the first tower beside the highest-pressure route.";
            }

            var failureCount = hotspot.stat.escapes + hotspot.stat.unresolvedAtEnd;
            var cellLabel = hotspot.hasSuggestedCell
                ? $" @{hotspot.suggestedCell.x},{hotspot.suggestedCell.y}"
                : string.Empty;
            if (TDLocalization.IsChinese)
            {
                var segment = $"{GetLocalizedLaneLabel(hotspot.stat.laneKey)}/{GetLocalizedRoadSegmentLabel(hotspot.stat.segmentIndex)}";
                if (failureCount <= 0)
                {
                    return hotspot.coverageScore >= 90
                        ? $"{segment} H{hotspot.heatScore}：到达 {hotspot.stat.reached}，漏怪/存活 0，覆盖 C{hotspot.coverageScore}；覆盖已充足，把低收益火力转向更薄弱的路段。"
                        : $"{segment} H{hotspot.heatScore}：到达 {hotspot.stat.reached}，漏怪/存活 0，覆盖 C{hotspot.coverageScore}；保持当前防线，仅在后续压力出现时增援{cellLabel}。";
                }

                return hotspot.coverageScore >= 90
                    ? $"{segment} H{hotspot.heatScore}：漏怪/存活 {failureCount}，覆盖 C{hotspot.coverageScore}；升级或把火力移至该路段{cellLabel}。"
                    : $"{segment} H{hotspot.heatScore}：漏怪/存活 {failureCount}，覆盖 C{hotspot.coverageScore}；在建议塔位补充覆盖{cellLabel}。";
            }

            if (failureCount <= 0)
            {
                return hotspot.coverageScore >= 90
                    ? $"{FormatLaneLabel(hotspot.stat.laneKey)}/{GetRoadSegmentLabel(hotspot.stat.segmentIndex)} H{hotspot.heatScore}: " +
                      $"{hotspot.stat.reached} reached, 0 leak/live, C{hotspot.coverageScore}; coverage sufficient, shift low-value firepower toward a weaker segment."
                    : $"{FormatLaneLabel(hotspot.stat.laneKey)}/{GetRoadSegmentLabel(hotspot.stat.segmentIndex)} H{hotspot.heatScore}: " +
                      $"{hotspot.stat.reached} reached, 0 leak/live, C{hotspot.coverageScore}; hold this line and reinforce only if later pressure appears{cellLabel}.";
            }

            if (hotspot.coverageScore >= 90)
            {
                return $"{FormatLaneLabel(hotspot.stat.laneKey)}/{GetRoadSegmentLabel(hotspot.stat.segmentIndex)} H{hotspot.heatScore}: " +
                       $"{hotspot.stat.reached} reached, {failureCount} leak/live, C{hotspot.coverageScore}; coverage saturated, upgrade or relocate output toward this segment{cellLabel}.";
            }

            return $"{FormatLaneLabel(hotspot.stat.laneKey)}/{GetRoadSegmentLabel(hotspot.stat.segmentIndex)} H{hotspot.heatScore}: " +
                   $"{hotspot.stat.reached} reached, {failureCount} leak/live, C{hotspot.coverageScore}; add coverage{cellLabel}.";
        }

        private string BuildCounterCategoryRecommendation(TDRoadHeatReport hotspot)
        {
            var category = GetHighestCounterGapCategory(out var matchedDamage, out var totalDamage);
            if (string.IsNullOrWhiteSpace(category) || totalDamage <= 0)
            {
                return TDLocalization.IsChinese
                    ? "没有明显的克制缺口；用一座输出塔搭配一座控制塔。"
                    : "No dominant counter gap; pair one damage tower with one control tower.";
            }

            var matchPct = Mathf.RoundToInt(matchedDamage / Mathf.Max(1f, totalDamage) * 100f);
            if (TDLocalization.IsChinese)
            {
                var localizedLane = hotspot?.stat == null ? "高压路线" : GetLocalizedLaneLabel(hotspot.stat.laneKey);
                return $"{GetLocalizedCounterCategoryLabel(category)}匹配 {matchPct}%（{matchedDamage}/{totalDamage} 伤害）；" +
                       $"在{localizedLane}补充 {GetAvailableCounterCategoryTowerSuggestion(category, true)}。";
            }

            var laneLabel = hotspot?.stat == null ? "the hot route" : FormatLaneLabel(hotspot.stat.laneKey);
            return $"{GetCounterCategoryLabel(category)} match {matchPct}% ({matchedDamage}/{totalDamage} dmg); " +
                   $"add {GetAvailableCounterCategoryTowerSuggestion(category)} on {laneLabel}.";
        }

        private string BuildOperationalRecommendation(TDRoadHeatReport hotspot)
        {
            var score = CalculateRunScore();
            if (_campaignRoute?.level?.scenario?.milestoneExam == true &&
                _activeScenarioMechanic != null &&
                _scenarioOpportunities > 0 &&
                _scenarioUses / (float)_scenarioOpportunities < 0.35f)
            {
                var examDecision = _examPresentationProfile?.decisionBody ??
                                   _campaignRoute.level.scenario.failureFocus.Replace('_', ' ');
                if (TDLocalization.IsChinese)
                {
                    return $"{TDLocalization.LocalizeRuntimeString(_activeScenarioMechanic.displayName)} {_scenarioUses}/{_scenarioOpportunities}：" +
                           $"{TDLocalization.LocalizeRuntimeString(examDecision)}。";
                }

                return $"{_activeScenarioMechanic.displayName} {_scenarioUses}/{_scenarioOpportunities}: {examDecision}.";
            }

            if (_isResonanceSystemEnabled && score.command < 55)
            {
                if (TDLocalization.IsChinese)
                {
                    return $"指令转化：已使用 {_resonanceCommandsUsed}/{_resonanceWindowsTriggered} 个窗口，" +
                           $"其中 {_resonanceMatchedCommands} 次匹配；下一次按威胁标签选择指令。";
                }

                return $"Command conversion: {_resonanceCommandsUsed}/{_resonanceWindowsTriggered} windows used, " +
                       $"{_resonanceMatchedCommands} matched; answer the next threat tag.";
            }

            if (_isResonanceSystemEnabled && _matrixOpportunities > 0 &&
                _matrixFullMatches / (float)_matrixOpportunities < 0.45f)
            {
                var matrixPct = Mathf.RoundToInt(_matrixFullMatches / (float)_matrixOpportunities * 100f);
                if (TDLocalization.IsChinese)
                {
                    return $"矩阵转化 {matrixPct}%（{_matrixFullMatches}/{_matrixOpportunities}）；让敌人特性与专精的指令倾向形成匹配。";
                }

                return $"Matrix conversion {matrixPct}% ({_matrixFullMatches}/{_matrixOpportunities}); pair enemy traits with the specialization's command affinity.";
            }

            if (_isResonanceSystemEnabled && _matrixFullMatches > 0 && _matrixConvergenceTriggers == 0)
            {
                if (_matrixBestWindowSpecializations < MatrixConvergenceRequiredSpecializations)
                {
                    if (TDLocalization.IsChinese)
                    {
                        return $"矩阵同步峰值 {_matrixBestWindowSync}，但只有 {_matrixBestWindowSpecializations} 种不同专精；部署两座倾向一致的终极塔触发汇聚。";
                    }

                    return $"Matrix sync peaked {_matrixBestWindowSync}, but only {_matrixBestWindowSpecializations} unique spec; field two aligned capstones for Convergence.";
                }

                if (TDLocalization.IsChinese)
                {
                    return $"矩阵同步峰值 {_matrixBestWindowSync}/{MatrixConvergenceRequiredMatches}；在两座倾向一致的终极塔同时攻击时释放指令。";
                }

                return $"Matrix sync peaked {_matrixBestWindowSync}/{MatrixConvergenceRequiredMatches}; time the command while both aligned capstones are firing.";
            }

            var towers = GetSortedTowerStats();
            if (towers.Count == 0)
            {
                return TDLocalization.IsChinese
                    ? "出兵前先投入备战资源；本局没有记录到防御塔贡献。"
                    : "Spend prep budget before dispatch; no tower contribution was recorded.";
            }

            var cellLabel = hotspot != null && hotspot.hasSuggestedCell
                ? $" @{hotspot.suggestedCell.x},{hotspot.suggestedCell.y}"
                : string.Empty;
            if (towers.Count == 1)
            {
                var only = towers[0];
                if (TDLocalization.IsChinese)
                {
                    return $"{GetLocalizedCompactTowerLabel(only.kind)}承担了 100% 伤害；在建议塔位增加第二个克制支点{cellLabel}。";
                }

                return $"{GetCompactTowerLabel(only.kind)} carries 100% dmg; add a second counter anchor{cellLabel}.";
            }

            var weakest = GetLeastProductiveTowerStat();
            if (weakest != null)
            {
                var value = weakest.damageDealt / Mathf.Max(1f, weakest.TotalSpend);
                var share = _totalDamageDealt <= 0 ? 0 : Mathf.RoundToInt(weakest.damageDealt / (float)_totalDamageDealt * 100f);
                if (share < 18 || value < 2f)
                {
                    if (TDLocalization.IsChinese)
                    {
                        return $"{GetLocalizedCompactTowerLabel(weakest.kind)} @{weakest.cell.x},{weakest.cell.y}：效率 {value:0.0}，伤害占比 {share}%；移至高压塔位{cellLabel}。";
                    }

                    return $"{GetCompactTowerLabel(weakest.kind)} @{weakest.cell.x},{weakest.cell.y}: {value:0.0} dmg/budget, {share}% share; move toward hot cell{cellLabel}.";
                }
            }

            var top = towers[0];
            var topShare = _totalDamageDealt <= 0 ? 0 : Mathf.RoundToInt(top.damageDealt / (float)_totalDamageDealt * 100f);
            if (TDLocalization.IsChinese)
            {
                return _upgradesPurchased <= 0
                    ? $"为 {GetLocalizedCompactTowerLabel(top.kind)} @{top.cell.x},{top.cell.y} 选择专精；它已经承担 {topShare}% 伤害。"
                    : $"主力支点 {GetLocalizedCompactTowerLabel(top.kind)} 承担 {topShare}% 伤害；继续增援高压塔位{cellLabel}。";
            }

            return _upgradesPurchased <= 0
                ? $"Specialize {GetCompactTowerLabel(top.kind)} @{top.cell.x},{top.cell.y}; it already carries {topShare}% damage."
                : $"Top anchor {GetCompactTowerLabel(top.kind)} carries {topShare}% damage; reinforce the hot cell{cellLabel}.";
        }

        private string GetHighestCounterGapCategory(out int matchedDamage, out int totalDamage)
        {
            var bestCategory = string.Empty;
            var bestGap = 0;
            matchedDamage = 0;
            totalDamage = 0;
            foreach (var pair in _threatCategoryDamage)
            {
                if (!IsCounterCategoryActionable(pair.Key))
                {
                    continue;
                }

                var matched = _threatCategoryCounterDamage.TryGetValue(pair.Key, out var value) ? value : 0;
                var gap = Mathf.Max(0, pair.Value - matched);
                if (gap > bestGap ||
                    (gap == bestGap && pair.Value > totalDamage) ||
                    (gap == bestGap && pair.Value == totalDamage && string.CompareOrdinal(pair.Key, bestCategory) < 0))
                {
                    bestCategory = pair.Key;
                    bestGap = gap;
                    matchedDamage = matched;
                    totalDamage = pair.Value;
                }
            }

            return bestCategory;
        }

        private bool IsCounterCategoryActionable(string category)
        {
            for (var i = 0; i < _availableTowerKinds.Count; i++)
            {
                if (IsTowerCounterForCategory(_availableTowerKinds[i], category))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetAvailableCounterCategoryTowerSuggestion(string category, bool localized = false)
        {
            var labels = new List<string>();
            for (var i = 0; i < _availableTowerKinds.Count; i++)
            {
                var kind = _availableTowerKinds[i];
                if (IsTowerCounterForCategory(kind, category))
                {
                    labels.Add(localized ? GetLocalizedCompactTowerLabel(kind) : GetCompactTowerLabel(kind));
                }
            }

            return labels.Count > 0
                ? string.Join("/", labels)
                : localized ? "已解锁的克制塔" : "an unlocked counter";
        }

        private static string GetCounterCategoryLabel(string category)
        {
            return category switch
            {
                "speed" => "Speed counter",
                "swarm" => "Swarm counter",
                "armor" => "Armor counter",
                "attrition" => "Attrition counter",
                _ => "Threat counter"
            };
        }

        private static string GetLocalizedCounterCategoryLabel(string category)
        {
            return category switch
            {
                "speed" => "高速克制",
                "swarm" => "群体克制",
                "armor" => "护甲克制",
                "attrition" => "消耗克制",
                _ => "威胁克制"
            };
        }

        private void BuildRunResultCharts()
        {
            _uiGameOverScoreChartRoot = CreateUiRect(
                "Run Result Five Axis Chart",
                _uiGameOverRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -124f),
                new Vector2(704f, 52f));

            var scoreLabels = new[] { "COVER", "COUNTER", "OUTPUT", "ECON", "COMMAND" };
            var scoreIcons = new[]
            {
                TDUiP132Icon.Hotspot,
                TDUiP132Icon.ArmorBreak,
                TDUiP132Icon.Damage,
                TDUiP132Icon.Budget,
                TDUiP132Icon.Resonance
            };
            var scoreColors = new[]
            {
                new Color(0.28f, 0.82f, 1f, 1f),
                new Color(1f, 0.54f, 0.20f, 1f),
                new Color(0.98f, 0.78f, 0.28f, 1f),
                new Color(0.36f, 0.88f, 0.54f, 1f),
                new Color(0.88f, 0.54f, 0.96f, 1f)
            };
            for (var i = 0; i < scoreLabels.Length; i++)
            {
                var x = i * 140f;
                CreateUiSpriteImage(
                    $"Score Axis {scoreLabels[i]} Icon",
                    _uiGameOverScoreChartRoot,
                    new Vector2(x, 0f),
                    new Vector2(18f, 18f),
                    TDUiP132Art.IconPath(scoreIcons[i]),
                    Color.white);
                CreateUiText($"Score Axis {scoreLabels[i]}", _uiGameOverScoreChartRoot, new Vector2(x + 22f, 0f), new Vector2(74f, 18f), scoreLabels[i], 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.76f, 0.84f, 0.88f, 1f));
                var value = CreateUiText($"Score Axis {scoreLabels[i]} Value", _uiGameOverScoreChartRoot, new Vector2(x + 98f, 0f), new Vector2(30f, 18f), "0", 12, FontStyle.Bold, TextAnchor.MiddleRight, scoreColors[i]);
                var back = CreateUiImage(
                    $"Score Axis {scoreLabels[i]} Back",
                    _uiGameOverScoreChartRoot,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(x, -26f),
                    new Vector2(128f, 10f),
                    new Color(0.08f, 0.12f, 0.14f, 0.92f));
                var fill = CreateUiImage(
                    $"Score Axis {scoreLabels[i]} Fill",
                    back.transform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    Vector2.zero,
                    new Vector2(1f, 6f),
                    scoreColors[i]);
                _uiGameOverScoreBarFills.Add(fill);
                _uiGameOverScoreBarValues.Add(value);
            }

            _uiGameOverLaneChartRoot = CreateUiRect(
                "Run Result Lane Chart",
                _uiGameOverRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -207f),
                new Vector2(338f, 80f));
            _uiGameOverTowerChartRoot = CreateUiRect(
                "Run Result Tower Chart",
                _uiGameOverRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(394f, -207f),
                new Vector2(338f, 80f));
            BuildRunResultBreakdownRows(
                "Lane",
                _uiGameOverLaneChartRoot,
                _uiGameOverLaneBarRows,
                _uiGameOverLaneBarFills,
                _uiGameOverLaneBarLabels,
                _uiGameOverLaneBarValues,
                new Color(0.28f, 0.80f, 1f, 1f));
            BuildRunResultBreakdownRows(
                "Tower",
                _uiGameOverTowerChartRoot,
                _uiGameOverTowerBarRows,
                _uiGameOverTowerBarFills,
                _uiGameOverTowerBarLabels,
                _uiGameOverTowerBarValues,
                new Color(0.34f, 0.90f, 0.58f, 1f));
        }

        private void BuildRunResultBreakdownRows(
            string prefix,
            Transform parent,
            List<RectTransform> rows,
            List<Image> fills,
            List<Text> labels,
            List<Text> values,
            Color fillColor)
        {
            for (var i = 0; i < 4; i++)
            {
                var row = CreateUiRect(
                    $"{prefix} Chart Row {i + 1}",
                    parent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, -(i * 20f)),
                    new Vector2(338f, 18f));
                var label = CreateUiText($"{prefix} Chart Label {i + 1}", row, Vector2.zero, new Vector2(66f, 18f), "-", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.86f, 0.90f, 1f));
                var back = CreateUiImage(
                    $"{prefix} Chart Back {i + 1}",
                    row,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(68f, -5f),
                    new Vector2(194f, 8f),
                    new Color(0.08f, 0.12f, 0.14f, 0.92f));
                var fill = CreateUiImage(
                    $"{prefix} Chart Fill {i + 1}",
                    back.transform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    Vector2.zero,
                    new Vector2(1f, 5f),
                    fillColor);
                var value = CreateUiText($"{prefix} Chart Value {i + 1}", row, new Vector2(268f, 0f), new Vector2(70f, 18f), "0%", 12, FontStyle.Bold, TextAnchor.MiddleRight, fillColor);
                rows.Add(row);
                fills.Add(fill);
                labels.Add(label);
                values.Add(value);
            }
        }

    }
}
