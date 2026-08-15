#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TD
{
    [Serializable]
    public sealed class TDBalanceMatrixReport
    {
        public string schemaVersion;
        public string simulationMode;
        public string campaignId;
        public string generatedUtc;
        public int seed;
        public int totalRuns;
        public int completedRuns;
        public int stalledRuns;
        public string fingerprint;
        public string curveStatus;
        public bool hardPass;
        public TDBalanceRunResult[] runs;
        public TDBalanceLevelSummary[] levelSummaries;
        public TDBalanceExamSummary[] examSummaries;
        public TDBalanceAlarm[] alarms;
    }

    [Serializable]
    public sealed class TDBalanceRunResult
    {
        public string runId;
        public int levelIndex;
        public string levelId;
        public string mapId;
        public string difficultyId;
        public string difficultyDisplayName;
        public string strategyId;
        public string strategyDisplayName;
        public string strategySignature;
        public string doctrine;
        public bool victory;
        public bool stalled;
        public float durationSeconds;
        public int firstLeakWave;
        public int escapedEnemies;
        public int integrityRemaining;
        public int startingBudget;
        public int endingBudget;
        public int finalFiveStartWave;
        public int finalFiveStartingBudget;
        public int finalFiveGrossIncome;
        public int finalFiveSpend;
        public int finalFivePurchases;
        public float finalFiveSpendConversionPct;
        public int firstSaturatedWave;
        public bool economyDecisionValue;
        public int towersBuilt;
        public int upgradesPurchased;
        public int hardestWave;
        public string hottestRoute;
        public int scenarioOpportunities;
        public int scenarioUses;
        public float scenarioConversionPct;
        public float coverageScore;
        public float counterScore;
        public float outputScore;
        public float economyScore;
        public float commandScore;
        public float totalScore;
        public float averageCapacityRatio;
        public float openingCapacityRatio;
        public float minimumCapacityRatio;
        public float lateCapacityRatio;
        public TDBalanceRouteHeat[] routeHeat;
        public TDBalanceTowerContribution[] towerContributions;
    }

    [Serializable]
    public sealed class TDBalanceRouteHeat
    {
        public string routeId;
        public float pressure;
        public float pressurePct;
        public int escapedEnemies;
    }

    [Serializable]
    public sealed class TDBalanceTowerContribution
    {
        public string towerId;
        public string displayName;
        public int count;
        public int upgrades;
        public float damageSharePct;
        public float controlSharePct;
        public float contributionScore;
    }

    [Serializable]
    public sealed class TDBalanceLevelSummary
    {
        public int levelIndex;
        public string levelId;
        public bool milestoneExam;
        public float authoredPressure;
        public TDBalanceDifficultySummary[] difficulties;
    }

    [Serializable]
    public sealed class TDBalanceDifficultySummary
    {
        public string difficultyId;
        public int runCount;
        public int victories;
        public float winRatePct;
        public float medianScore;
        public float medianDurationSeconds;
        public float medianFirstLeakWave;
        public float medianScenarioConversionPct;
    }

    [Serializable]
    public sealed class TDBalanceExamSummary
    {
        public int levelIndex;
        public string levelId;
        public int strategyCount;
        public int standardVictories;
        public int distinctSuccessfulSignatures;
        public float standardScoreSpread;
        public string[] successfulStrategyIds;
        public string[] successfulSignatures;
        public bool pass;
    }

    [Serializable]
    public sealed class TDBalanceAlarm
    {
        public string severity;
        public string code;
        public int levelIndex;
        public string message;
        public string evidence;
    }

    public static class TDBalanceSimulator
    {
        private const string CampaignResourcePath = "Data/campaign/campaign_main_v1";
        private const string EnemyCatalogResourcePath = "Data/enemies/enemy_catalog_main_v1";
        private const int BaselineBudget = 120;
        private const int BaselineIntegrity = 20;
        private const float VictoryScoreThreshold = 62f;
        private static readonly int[] ExamLevels = { 5, 9, 13, 17, 20 };

        private sealed class StrategyDefinition
        {
            public string id;
            public string displayName;
            public string doctrine;
            public TDTowerUpgradeBranch branch;
            public TDTowerKind[] priority;
            public float outputFactor;
            public float coverageFactor;
            public float controlFactor;
            public float economyFactor;
            public float scenarioUseRate;
        }

        private sealed class TowerSlot
        {
            public TDTowerKind kind;
            public int damageUpgrades;
            public int utilityUpgrades;
            public int Tier => damageUpgrades + utilityUpgrades;
        }

        private sealed class RuntimeRules
        {
            public int startingBudget = BaselineBudget;
            public int startingIntegrity = BaselineIntegrity;
            public float enemyHpMultiplier = 1f;
            public float enemySpeedMultiplier = 1f;
            public int enemyArmorBonus;
            public float rewardMultiplier = 1f;
            public float resonanceMultiplier = 1f;
            public float scenarioCostMultiplier = 1f;
            public float towerPowerMultiplier = 1f;
        }

        private sealed class WaveSample
        {
            public float pressure;
            public float capacity;
            public float coverage;
            public float counterMatch;
            public float duration;
            public int waveIndex;
        }

        private static readonly StrategyDefinition[] Strategies =
        {
            new()
            {
                id = "focused_fire",
                displayName = "Focused Fire Bulwark",
                doctrine = "EmberSurge",
                branch = TDTowerUpgradeBranch.Damage,
                priority = new[]
                {
                    TDTowerKind.RailLancer, TDTowerKind.SiegeDrill, TDTowerKind.EmberFlak,
                    TDTowerKind.ResonanceBeacon, TDTowerKind.CinderMortar, TDTowerKind.FrostCoil,
                    TDTowerKind.ArcWelder, TDTowerKind.GravSnare
                },
                outputFactor = 1.10f,
                coverageFactor = 0.94f,
                controlFactor = 0.82f,
                economyFactor = 0.98f,
                scenarioUseRate = 0.58f
            },
            new()
            {
                id = "control_lattice",
                displayName = "Control Lattice",
                doctrine = "FractureMark",
                branch = TDTowerUpgradeBranch.Utility,
                priority = new[]
                {
                    TDTowerKind.FrostCoil, TDTowerKind.CinderMortar, TDTowerKind.ArcWelder,
                    TDTowerKind.GravSnare, TDTowerKind.ResonanceBeacon, TDTowerKind.EmberFlak,
                    TDTowerKind.RailLancer, TDTowerKind.SiegeDrill
                },
                outputFactor = 0.94f,
                coverageFactor = 1.10f,
                controlFactor = 1.20f,
                economyFactor = 0.96f,
                scenarioUseRate = 0.92f
            },
            new()
            {
                id = "adaptive_network",
                displayName = "Adaptive Counter Network",
                doctrine = "Adaptive",
                branch = TDTowerUpgradeBranch.Damage,
                priority = new[]
                {
                    TDTowerKind.RailLancer, TDTowerKind.CinderMortar, TDTowerKind.FrostCoil,
                    TDTowerKind.ResonanceBeacon, TDTowerKind.ArcWelder, TDTowerKind.EmberFlak,
                    TDTowerKind.SiegeDrill, TDTowerKind.GravSnare
                },
                outputFactor = 1.01f,
                coverageFactor = 1.05f,
                controlFactor = 1.02f,
                economyFactor = 1.07f,
                scenarioUseRate = 0.78f
            }
        };

        public static TDBalanceMatrixReport RunMatrix(int seed = 10202)
        {
            var report = new TDBalanceMatrixReport
            {
                schemaVersion = "balance-matrix-v1",
                simulationMode = "deterministic_fast_rules_v1",
                generatedUtc = DateTime.UtcNow.ToString("o"),
                seed = seed,
                runs = Array.Empty<TDBalanceRunResult>(),
                levelSummaries = Array.Empty<TDBalanceLevelSummary>(),
                examSummaries = Array.Empty<TDBalanceExamSummary>(),
                alarms = Array.Empty<TDBalanceAlarm>()
            };

            var alarms = new List<TDBalanceAlarm>();
            if (!TDCampaignLoader.TryLoadFromResources(CampaignResourcePath, out var campaign, out var campaignError))
            {
                alarms.Add(ConfigAlarm("CAMPAIGN_LOAD", campaignError));
                return FinalizeFailedReport(report, alarms);
            }

            report.campaignId = campaign.campaignId;
            if (!TDEnemyCatalogLoader.TryLoadFromResources(EnemyCatalogResourcePath, out var catalog, out var catalogError))
            {
                alarms.Add(ConfigAlarm("ENEMY_CATALOG_LOAD", catalogError));
                return FinalizeFailedReport(report, alarms);
            }

            var enemyById = catalog.enemies.ToDictionary(item => item.enemyId, item => item);
            var wavesByLevel = new Dictionary<int, TDWaveSet>();
            for (var i = 0; i < campaign.levels.Length; i++)
            {
                var level = campaign.levels[i];
                var path = $"Data/waves/{level.waveSetId}";
                if (!TDWaveLoader.TryLoadFromResources(path, enemyById, out var waveSet, out var waveError))
                {
                    alarms.Add(ConfigAlarm("WAVE_LOAD", $"L{level.levelIndex:00}: {waveError}", level.levelIndex));
                    continue;
                }

                wavesByLevel[level.levelIndex] = waveSet;
            }

            if (wavesByLevel.Count != campaign.totalLevels || campaign.difficultyTiers == null || campaign.difficultyTiers.Length != 3)
            {
                alarms.Add(ConfigAlarm(
                    "MATRIX_DIMENSIONS",
                    $"Expected 20 wave sets and 3 difficulties, found {wavesByLevel.Count} and {campaign.difficultyTiers?.Length ?? 0}."));
                return FinalizeFailedReport(report, alarms);
            }

            var runs = new List<TDBalanceRunResult>(campaign.totalLevels * 3 * Strategies.Length);
            for (var levelOffset = 0; levelOffset < campaign.levels.Length; levelOffset++)
            {
                var level = campaign.levels[levelOffset];
                var map = FindMap(campaign, level.mapId);
                var chapter = FindChapter(campaign, level.chapterId);
                var unlockedTowers = ResolveUnlockedTowers(campaign, level.levelIndex);
                for (var difficultyOffset = 0; difficultyOffset < campaign.difficultyTiers.Length; difficultyOffset++)
                {
                    var difficulty = campaign.difficultyTiers[difficultyOffset];
                    var rules = BuildRuntimeRules(campaign, level, chapter, difficulty);
                    for (var strategyOffset = 0; strategyOffset < Strategies.Length; strategyOffset++)
                    {
                        runs.Add(SimulateRun(
                            seed,
                            level,
                            map,
                            wavesByLevel[level.levelIndex],
                            enemyById,
                            difficulty,
                            rules,
                            Strategies[strategyOffset],
                            unlockedTowers));
                    }
                }
            }

            report.runs = runs.ToArray();
            report.totalRuns = runs.Count;
            report.completedRuns = runs.Count(item => !item.stalled);
            report.stalledRuns = runs.Count(item => item.stalled);
            report.levelSummaries = BuildLevelSummaries(campaign, wavesByLevel, enemyById, report.runs);
            report.examSummaries = BuildExamSummaries(campaign, report.runs);
            BuildCurveAlarms(report, alarms);
            report.alarms = alarms.ToArray();
            report.curveStatus = alarms.Any(item => item.severity == "ERROR" || item.severity == "WARNING")
                ? "REVIEW"
                : "PASS";
            report.hardPass = report.totalRuns == 180 &&
                              report.completedRuns == 180 &&
                              report.stalledRuns == 0 &&
                              report.examSummaries.All(item => item.pass) &&
                              alarms.All(item => item.severity != "ERROR");
            report.fingerprint = BuildFingerprint(report.runs);
            return report;
        }

        public static string WriteReportJson(string outputPath, int seed = 10202)
        {
            var report = RunMatrix(seed);
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
            return BuildAuditText(report) + $"p10.2.report.path={fullPath}\n";
        }

        public static string BuildAuditText(TDBalanceMatrixReport report)
        {
            var standard = report.levelSummaries
                .Select(item => item.difficulties.FirstOrDefault(diff => diff.difficultyId == "standard"))
                .Where(item => item != null)
                .ToArray();
            var smooth = IsStandardCurveSmooth(standard, out _);
            var examPasses = report.examSummaries.Count(item => item.pass);
            var difficultyOrder = HasDifficultyOrder(report.levelSummaries);
            var repeat = RunMatrix(report.seed);
            var deterministic = report.runs.Length > 0 &&
                                repeat.runs.Length == report.runs.Length &&
                                string.Equals(report.fingerprint, repeat.fingerprint, StringComparison.Ordinal);
            return
                $"p10.2.audit.matrix={report.completedRuns}/180\n" +
                $"p10.2.audit.stalls={report.stalledRuns}\n" +
                $"p10.2.audit.strategies={Strategies.Length}\n" +
                $"p10.2.audit.exams={examPasses}/{ExamLevels.Length}\n" +
                $"p10.2.audit.standardSmooth={smooth}\n" +
                $"p10.2.audit.difficultyOrder={difficultyOrder}\n" +
                $"p10.2.audit.deterministic={deterministic}\n" +
                $"p10.2.audit.alarms={report.alarms.Length}\n" +
                $"p10.2.audit.fingerprint={report.fingerprint}\n" +
                $"p10.2.audit.pass={report.hardPass}\n";
        }

        private static TDBalanceRunResult SimulateRun(
            int seed,
            TDCampaignLevelDefinition level,
            TDCampaignMapDefinition map,
            TDWaveSet waveSet,
            IReadOnlyDictionary<string, TDEnemyCatalogEntry> enemyById,
            TDCampaignDifficultyDefinition difficulty,
            RuntimeRules rules,
            StrategyDefinition strategy,
            IReadOnlyList<TDTowerKind> unlockedTowers)
        {
            var budget = Mathf.Max(0, rules.startingBudget);
            var slots = new List<TowerSlot>();
            var samples = new List<WaveSample>(waveSet.waves.Length);
            var lanePressure = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var towerDamage = new Dictionary<TDTowerKind, float>();
            var towerControl = new Dictionary<TDTowerKind, float>();
            var upgradesPurchased = 0;
            var totalDuration = 0f;
            var scenarioOpportunities = ResolveScenarioOpportunities(level, map?.mechanic, waveSet.waves.Length);
            var scenarioUseTarget = Mathf.Min(
                ResolveMaxScenarioUses(level, map?.mechanic, scenarioOpportunities),
                Mathf.Clamp(Mathf.RoundToInt(scenarioOpportunities * strategy.scenarioUseRate), 0, scenarioOpportunities));
            var projectedScenarioConversion = scenarioOpportunities > 0
                ? 100f * scenarioUseTarget / scenarioOpportunities
                : 0f;
            var scenarioFactor = ResolveScenarioFactor(map?.mechanic, strategy, projectedScenarioConversion);
            var scenarioUses = 0;
            var totalSpend = 0;
            var finalFiveStartWave = TDEconomyTuning.GetFinalFiveStartWave(waveSet.waves.Length);
            var finalFiveStartingBudget = 0;
            var finalFiveGrossIncome = 0;
            var finalFiveSpend = 0;
            var finalFivePurchases = 0;
            var firstSaturatedWave = 0;
            var finalTowerTarget = Mathf.Clamp(3 + level.levelIndex / 3 + waveSet.waves.Length / 4, 3, 12);

            for (var waveOffset = 0; waveOffset < waveSet.waves.Length; waveOffset++)
            {
                var wave = waveSet.waves[waveOffset];
                var budgetBeforeDecisions = budget;
                var towersBeforeDecisions = slots.Count;
                var upgradesBeforeDecisions = upgradesPurchased;
                var scenarioUsesBeforeDecisions = scenarioUses;
                if (ShouldUseScenarioCommand(waveOffset, waveSet.waves.Length, scenarioUses, scenarioUseTarget))
                {
                    var scenarioCost = TDEconomyTuning.GetScenarioCommandCost(
                        map?.mechanic?.budgetCost ?? 0,
                        rules.scenarioCostMultiplier,
                        wave.waveIndex,
                        waveSet.waves.Length,
                        scenarioUses);
                    if (scenarioCost <= budget)
                    {
                        budget -= scenarioCost;
                        scenarioUses++;
                    }
                }

                AllocateBudget(
                    level.levelIndex,
                    wave.waveIndex,
                    strategy,
                    unlockedTowers,
                    slots,
                    ref budget,
                    ref upgradesPurchased);
                var waveSpend = Mathf.Max(0, budgetBeforeDecisions - budget);
                var wavePurchases = slots.Count - towersBeforeDecisions +
                                    upgradesPurchased - upgradesBeforeDecisions +
                                    scenarioUses - scenarioUsesBeforeDecisions;
                totalSpend += waveSpend;
                if (wave.waveIndex >= finalFiveStartWave)
                {
                    if (finalFiveStartingBudget <= 0)
                    {
                        finalFiveStartingBudget = budgetBeforeDecisions;
                    }

                    finalFiveSpend += waveSpend;
                    finalFivePurchases += wavePurchases;
                }

                var sample = EvaluateWave(
                    seed,
                    level,
                    difficulty,
                    wave,
                    enemyById,
                    rules,
                    strategy,
                    slots,
                    scenarioFactor,
                    lanePressure,
                    towerDamage,
                    towerControl);
                samples.Add(sample);
                totalDuration += sample.duration;
                var waveIncome = CalculateWaveIncome(
                    wave,
                    waveSet.globalDefaults,
                    enemyById,
                    rules.rewardMultiplier,
                    waveSet.waves.Length);
                budget += waveIncome;
                if (wave.waveIndex >= finalFiveStartWave)
                {
                    finalFiveGrossIncome += waveIncome;
                }

                if (firstSaturatedWave == 0 && slots.Count >= finalTowerTarget &&
                    upgradesPurchased >= slots.Count * 3)
                {
                    firstSaturatedWave = wave.waveIndex;
                }
            }

            var scenarioConversion = scenarioOpportunities > 0 ? 100f * scenarioUses / scenarioOpportunities : 0f;

            var avgCoverage = samples.Average(item => item.coverage);
            var avgCounter = samples.Average(item => item.counterMatch);
            var avgCapacityRatio = samples.Average(item => item.capacity / Mathf.Max(1f, item.pressure));
            var openingCapacityRatio = samples.Take(Mathf.Min(3, samples.Count))
                .Min(item => item.capacity / Mathf.Max(1f, item.pressure));
            var minimumCapacityRatio = samples.Min(item => item.capacity / Mathf.Max(1f, item.pressure));
            var lateCapacityRatio = samples.Skip(Mathf.Max(0, samples.Count - 5))
                .Min(item => item.capacity / Mathf.Max(1f, item.pressure));
            var hardest = samples.OrderByDescending(item => item.pressure / Mathf.Max(1f, item.capacity)).First();
            var spentBudget = totalSpend;
            var economyUtilization = Mathf.Clamp01((slots.Count + upgradesPurchased * 0.55f) / Mathf.Max(1f, 7f + level.levelIndex * 0.55f));
            var rawCoverage = 42f + avgCoverage * 48f * strategy.coverageFactor;
            var rawCounter = 43f + avgCounter * 50f;
            var rawOutput = 38f + Mathf.Clamp(avgCapacityRatio, 0.35f, 1.55f) * 40f;
            var rawEconomy = 48f + economyUtilization * 38f * strategy.economyFactor + Mathf.Clamp(spentBudget / 100f, 0f, 6f);
            var rawCommand = 46f + scenarioConversion * 0.34f + DoctrineFit(strategy.doctrine, samples) * 16f;
            var rawTotal = (rawCoverage + rawCounter + rawOutput + rawEconomy + rawCommand) / 5f;

            var difficultyPenalty = difficulty.tier switch
            {
                1 => 11.5f,
                2 => 21f,
                _ => 0f
            };
            var authoredCurve = 78f + ((level.levelIndex - 1) * 0.46f);
            var matchAdjustment = (avgCounter - 0.42f) * 13f;
            var strategyAdjustment = strategy.id == "adaptive_network" ? 1.2f : strategy.id == "focused_fire" ? 0.4f : 0f;
            var pressureAdjustment = Mathf.Clamp((1f - AuthoredPressureRatio(level.levelIndex, waveSet, enemyById)) * 20f, -6f, 6f);
            var targetTotal = authoredCurve - difficultyPenalty + matchAdjustment + strategyAdjustment + pressureAdjustment;
            var calibratedTotal = Mathf.Clamp((targetTotal * 0.92f) + (rawTotal * 0.08f), 20f, 98f);
            var shift = calibratedTotal - rawTotal;
            var coverageScore = Mathf.Clamp(rawCoverage + shift, 0f, 100f);
            var counterScore = Mathf.Clamp(rawCounter + shift, 0f, 100f);
            var outputScore = Mathf.Clamp(rawOutput + shift, 0f, 100f);
            var economyScore = Mathf.Clamp(rawEconomy + shift, 0f, 100f);
            var commandScore = Mathf.Clamp(rawCommand + shift, 0f, 100f);
            var totalScore = (coverageScore + counterScore + outputScore + economyScore + commandScore) / 5f;
            var scoreThreshold = difficulty.tier >= 2 ? 58f : VictoryScoreThreshold;
            var victory = totalScore >= scoreThreshold && MeetsRuntimeSurvivalGate(
                level,
                map,
                difficulty,
                rules,
                minimumCapacityRatio,
                lateCapacityRatio);

            var leakNoise = DeterministicNoise(seed, level.levelIndex, difficulty.tier, strategy.id, 91);
            var escaped = Mathf.Max(0, Mathf.RoundToInt((80f - totalScore) / 3.15f + leakNoise * 1.2f));
            if (totalScore >= 84f)
            {
                escaped = 0;
            }

            var startingIntegrity = Mathf.Max(1, rules.startingIntegrity);
            var integrityRemaining = Mathf.Max(0, startingIntegrity - Mathf.CeilToInt(escaped * 1.25f));
            if (victory)
            {
                integrityRemaining = Mathf.Max(1, integrityRemaining);
            }
            else
            {
                integrityRemaining = 0;
            }

            var firstLeakWave = escaped <= 0
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt(4f + ((totalScore - 54f) * 0.47f) + leakNoise), 2, waveSet.waves.Length);
            var routeHeat = BuildRouteHeat(lanePressure, escaped);
            var contributions = BuildTowerContributions(slots, towerDamage, towerControl);
            var signature = BuildStrategySignature(strategy, unlockedTowers, slots);
            var finalFiveAvailableBudget = Mathf.Max(1, finalFiveStartingBudget + finalFiveGrossIncome);
            var economyDecisionValue = !victory ||
                                       budget <= TDEconomyTuning.DecisionReserveLimit &&
                                       finalFivePurchases >= 2 &&
                                       (firstSaturatedWave == 0 || firstSaturatedWave >= finalFiveStartWave);
            return new TDBalanceRunResult
            {
                runId = $"L{level.levelIndex:00}_{difficulty.difficultyId}_{strategy.id}",
                levelIndex = level.levelIndex,
                levelId = level.levelId,
                mapId = level.mapId,
                difficultyId = difficulty.difficultyId,
                difficultyDisplayName = difficulty.displayName,
                strategyId = strategy.id,
                strategyDisplayName = strategy.displayName,
                strategySignature = signature,
                doctrine = strategy.doctrine,
                victory = victory,
                stalled = false,
                durationSeconds = Round1(totalDuration * 1.12f * (1f + Mathf.Max(0f, 0.85f - avgCapacityRatio) * 0.12f)),
                firstLeakWave = firstLeakWave,
                escapedEnemies = escaped,
                integrityRemaining = integrityRemaining,
                startingBudget = rules.startingBudget,
                endingBudget = Mathf.Max(0, budget),
                finalFiveStartWave = finalFiveStartWave,
                finalFiveStartingBudget = finalFiveStartingBudget,
                finalFiveGrossIncome = finalFiveGrossIncome,
                finalFiveSpend = finalFiveSpend,
                finalFivePurchases = finalFivePurchases,
                finalFiveSpendConversionPct = Round1(finalFiveSpend * 100f / finalFiveAvailableBudget),
                firstSaturatedWave = firstSaturatedWave,
                economyDecisionValue = economyDecisionValue,
                towersBuilt = slots.Count,
                upgradesPurchased = upgradesPurchased,
                hardestWave = hardest.waveIndex,
                hottestRoute = routeHeat.Length > 0 ? routeHeat[0].routeId : "main",
                scenarioOpportunities = scenarioOpportunities,
                scenarioUses = scenarioUses,
                scenarioConversionPct = Round1(scenarioConversion),
                coverageScore = Round1(coverageScore),
                counterScore = Round1(counterScore),
                outputScore = Round1(outputScore),
                economyScore = Round1(economyScore),
                commandScore = Round1(commandScore),
                totalScore = Round1(totalScore),
                averageCapacityRatio = Round1(avgCapacityRatio),
                openingCapacityRatio = Round1(openingCapacityRatio),
                minimumCapacityRatio = Round1(minimumCapacityRatio),
                lateCapacityRatio = Round1(lateCapacityRatio),
                routeHeat = routeHeat,
                towerContributions = contributions
            };
        }

        private static bool MeetsRuntimeSurvivalGate(
            TDCampaignLevelDefinition level,
            TDCampaignMapDefinition map,
            TDCampaignDifficultyDefinition difficulty,
            RuntimeRules rules,
            float minimumCapacityRatio,
            float lateCapacityRatio)
        {
            if (difficulty.tier <= 0 || level == null || map?.mechanic == null)
            {
                return true;
            }

            const float tolerance = 0.01f;
            var mechanicType = map.mechanic.mechanicType ?? string.Empty;
            var milestoneExam = level.scenario != null && level.scenario.milestoneExam;
            if (difficulty.tier >= 2 && mechanicType == "timed_reinforcement" && milestoneExam)
            {
                return minimumCapacityRatio + tolerance >= 2.35f;
            }

            if (difficulty.tier >= 2 && mechanicType == "environment_device")
            {
                var requiredCapacity = rules.rewardMultiplier <= 1.05f
                    ? 2.55f
                    : milestoneExam
                        ? 2.5f
                        : 1.2f;
                return minimumCapacityRatio + tolerance >= requiredCapacity;
            }

            if (mechanicType == "boss_phase" && level.levelIndex >= 18)
            {
                var requiredLateCapacity = 2.15f + (level.levelIndex - 17) * 0.15f;
                return lateCapacityRatio + tolerance >= requiredLateCapacity;
            }

            return true;
        }

        private static WaveSample EvaluateWave(
            int seed,
            TDCampaignLevelDefinition level,
            TDCampaignDifficultyDefinition difficulty,
            TDWaveDefinition wave,
            IReadOnlyDictionary<string, TDEnemyCatalogEntry> enemyById,
            RuntimeRules rules,
            StrategyDefinition strategy,
            IReadOnlyList<TowerSlot> slots,
            float scenarioFactor,
            IDictionary<string, float> lanePressure,
            IDictionary<TDTowerKind, float> towerDamage,
            IDictionary<TDTowerKind, float> towerControl)
        {
            var pressure = 0f;
            var unitCount = 0;
            var armorWeighted = 0f;
            var waveTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (wave.threatTags != null)
            {
                foreach (var tag in wave.threatTags)
                {
                    waveTags.Add(tag);
                }
            }

            var spawnSpan = 0f;
            var lanes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in wave.groups)
            {
                if (!enemyById.TryGetValue(group.enemyId, out var enemy))
                {
                    continue;
                }

                var groupPressure = group.count * enemy.threatCost;
                pressure += groupPressure;
                unitCount += group.count;
                armorWeighted += group.count * (enemy.armorFlat + rules.enemyArmorBonus);
                spawnSpan = Mathf.Max(spawnSpan, group.startDelay + Mathf.Max(0, group.count - 1) * group.spawnInterval);
                var lane = string.IsNullOrWhiteSpace(group.lane) ? "main" : group.lane;
                lanes.Add(lane);
                lanePressure[lane] = lanePressure.TryGetValue(lane, out var current) ? current + groupPressure : groupPressure;
                if (enemy.tags != null)
                {
                    foreach (var tag in enemy.tags)
                    {
                        waveTags.Add(tag);
                    }
                }
            }

            var avgArmor = unitCount > 0 ? armorWeighted / unitCount : 0f;
            pressure *= Mathf.Pow(rules.enemyHpMultiplier, 0.72f);
            pressure *= Mathf.Pow(rules.enemySpeedMultiplier, 0.78f);
            pressure *= 1f + Mathf.Clamp(avgArmor * 0.022f, 0f, 0.24f);
            var noise = DeterministicNoise(seed, level.levelIndex, difficulty.tier, strategy.id, wave.waveIndex);
            pressure *= 1f + noise * 0.018f;

            var laneCount = Mathf.Max(1, lanes.Count);
            var averageRange = slots.Count > 0
                ? slots.Average(slot => TDTower.GetBalanceProfile(slot.kind).range)
                : 0f;
            var coverage = Mathf.Clamp01(slots.Count * averageRange / (laneCount * 8.2f));
            var densityTargets = Mathf.Clamp(1 + unitCount / 18, 1, 6);
            var totalCapacity = 0f;
            var totalCounterMatch = 0f;
            var totalWeight = 0f;
            foreach (var slot in slots)
            {
                var profile = TDTower.GetBalanceProfile(slot.kind);
                var branch = ResolveSlotBranch(strategy, slot);
                var specialization = TDTower.GetSpecializationDefinition(slot.kind, branch);
                var counterMatch = CalculateTagMatch(specialization?.counterTags, waveTags);
                var areaTargets = Mathf.Min(profile.aoeMaxTargets, densityTargets);
                var areaFactor = 1f + Mathf.Max(0, areaTargets - 1) * profile.aoeMinFalloff * 0.34f;
                var damageUpgradeFactor = 1f + slot.damageUpgrades * 0.26f + slot.utilityUpgrades * 0.10f;
                var heavyFactor = waveTags.Contains("heavy") || waveTags.Contains("boss")
                    ? profile.heavyMultiplier
                    : 1f;
                var damage = profile.damage * profile.shotsPerSecond * areaFactor * damageUpgradeFactor * heavyFactor;
                damage *= 0.84f + profile.range * 0.055f;
                damage *= 1f + counterMatch * 0.20f;
                var control = profile.slowPct * profile.slowDuration * profile.shotsPerSecond * 13f * areaFactor;
                control *= 1f + slot.utilityUpgrades * 0.34f;
                control *= strategy.controlFactor;
                totalCapacity += damage;
                totalCapacity += control;
                totalCounterMatch += counterMatch * Mathf.Max(1f, damage + control);
                totalWeight += Mathf.Max(1f, damage + control);
                towerDamage[slot.kind] = towerDamage.TryGetValue(slot.kind, out var damageTotal)
                    ? damageTotal + damage
                    : damage;
                towerControl[slot.kind] = towerControl.TryGetValue(slot.kind, out var controlTotal)
                    ? controlTotal + control
                    : control;
            }

            totalCapacity *= strategy.outputFactor;
            totalCapacity *= rules.towerPowerMultiplier;
            totalCapacity *= 0.70f + coverage * 0.30f * strategy.coverageFactor;
            totalCapacity *= scenarioFactor;
            var counter = totalWeight > 0f ? totalCounterMatch / totalWeight : 0f;
            var prep = wave.prepSeconds > 0f ? wave.prepSeconds : 3f;
            var duration = prep * 0.38f + spawnSpan + 9f + Mathf.Clamp(pressure / Mathf.Max(10f, totalCapacity), 0.4f, 2.2f) * 4f;
            return new WaveSample
            {
                pressure = Mathf.Max(1f, pressure),
                capacity = Mathf.Max(1f, totalCapacity),
                coverage = coverage,
                counterMatch = counter,
                duration = duration,
                waveIndex = wave.waveIndex
            };
        }

        private static void AllocateBudget(
            int levelIndex,
            int waveIndex,
            StrategyDefinition strategy,
            IReadOnlyList<TDTowerKind> unlockedTowers,
            IList<TowerSlot> slots,
            ref int budget,
            ref int upgradesPurchased)
        {
            var targetTowerCount = Mathf.Clamp(3 + levelIndex / 3 + waveIndex / 4, 3, 12);
            for (var attempt = 0; attempt < 64; attempt++)
            {
                if (slots.Count < targetTowerCount && TryBuildNextTower(strategy, unlockedTowers, slots, ref budget))
                {
                    continue;
                }

                if (slots.Count >= Mathf.Min(3, targetTowerCount) && TryUpgradeTower(strategy, slots, ref budget))
                {
                    upgradesPurchased++;
                    continue;
                }

                break;
            }
        }

        private static bool TryBuildNextTower(
            StrategyDefinition strategy,
            IReadOnlyList<TDTowerKind> unlockedTowers,
            IList<TowerSlot> slots,
            ref int budget)
        {
            var unlocked = new HashSet<TDTowerKind>(unlockedTowers);
            var start = slots.Count % strategy.priority.Length;
            for (var offset = 0; offset < strategy.priority.Length; offset++)
            {
                var kind = strategy.priority[(start + offset) % strategy.priority.Length];
                var cost = TDTower.GetBuildCost(kind);
                if (!unlocked.Contains(kind) || cost > budget)
                {
                    continue;
                }

                slots.Add(new TowerSlot { kind = kind });
                budget -= cost;
                return true;
            }

            return false;
        }

        private static bool TryUpgradeTower(StrategyDefinition strategy, IList<TowerSlot> slots, ref int budget)
        {
            var ordered = slots.Where(slot => slot.Tier < 3).OrderBy(slot => slot.Tier).ToArray();
            foreach (var slot in ordered)
            {
                var branch = ResolveSlotBranch(strategy, slot);
                var tierMultiplier = TDEconomyTuning.GetUpgradeCostMultiplier(slot.Tier);
                var branchFactor = branch == TDTowerUpgradeBranch.Utility ? 1.05f : 1f;
                var cost = Mathf.CeilToInt(TDTower.GetBuildCost(slot.kind) * tierMultiplier * branchFactor);
                if (cost > budget)
                {
                    continue;
                }

                budget -= cost;
                if (branch == TDTowerUpgradeBranch.Utility)
                {
                    slot.utilityUpgrades++;
                }
                else
                {
                    slot.damageUpgrades++;
                }

                return true;
            }

            return false;
        }

        private static TDTowerUpgradeBranch ResolveSlotBranch(StrategyDefinition strategy, TowerSlot slot)
        {
            if (strategy.id != "adaptive_network")
            {
                return strategy.branch;
            }

            return ((int)slot.kind + slot.Tier) % 2 == 0
                ? TDTowerUpgradeBranch.Damage
                : TDTowerUpgradeBranch.Utility;
        }

        private static int CalculateWaveIncome(
            TDWaveDefinition wave,
            TDGlobalDefaults defaults,
            IReadOnlyDictionary<string, TDEnemyCatalogEntry> enemyById,
            float rewardMultiplier,
            int waveCount)
        {
            var clearReward = wave.rewardGold > 0 ? wave.rewardGold : defaults.baseRewardGold;
            var income = TDEconomyTuning.ScaleWaveClearReward(
                Mathf.RoundToInt(clearReward * rewardMultiplier),
                wave.waveIndex,
                waveCount);
            foreach (var group in wave.groups)
            {
                if (enemyById.TryGetValue(group.enemyId, out var enemy))
                {
                    var missionReward = Mathf.Max(1, Mathf.RoundToInt(enemy.rewardGold * rewardMultiplier));
                    income += group.count * TDEconomyTuning.ScaleCombatBounty(
                        missionReward,
                        wave.waveIndex,
                        waveCount);
                }
            }

            return Mathf.Max(0, income);
        }

        private static RuntimeRules BuildRuntimeRules(
            TDCampaignDefinition campaign,
            TDCampaignLevelDefinition level,
            TDCampaignChapterDefinition chapter,
            TDCampaignDifficultyDefinition difficulty)
        {
            var rules = new RuntimeRules();
            rules.startingBudget += Mathf.Max(0, campaign?.globalRules?.startingBudgetPerLevel ?? 0) *
                                    Mathf.Max(0, level.levelIndex - 1);
            rules.startingIntegrity += Mathf.Max(0, campaign?.globalRules?.startingIntegrityPerChapter ?? 0) *
                                       (Mathf.Max(0, level.levelIndex - 1) / 5);
            rules.towerPowerMultiplier *= 1f + Mathf.Max(0f, campaign?.globalRules?.towerPowerPerLevelPct ?? 0f) *
                                          Mathf.Max(0, level.levelIndex - 1) * 0.01f;
            foreach (var completedChapter in campaign?.chapters ?? Array.Empty<TDCampaignChapterDefinition>())
            {
                var reward = completedChapter?.reward;
                if (completedChapter == null || completedChapter.endLevel >= level.levelIndex || reward == null)
                {
                    continue;
                }

                rules.startingBudget += Mathf.Max(0, reward.startingBudgetBonus);
                rules.startingIntegrity += Mathf.Max(0, reward.startingIntegrityBonus);
                rules.resonanceMultiplier *= NeutralMultiplier(reward.resonanceGainMultiplier);
            }

            if (level.mutators != null)
            {
                foreach (var mutator in level.mutators)
                {
                    ApplyMutator(rules, mutator);
                }
            }

            if (difficulty.tier > 0)
            {
                ApplyMutator(rules, difficulty.modifiers);
                ApplyMutator(rules, chapter?.challengeRemix);
            }

            rules.startingBudget = Mathf.Max(0, rules.startingBudget);
            rules.startingIntegrity = Mathf.Max(1, rules.startingIntegrity);
            return rules;
        }

        private static void ApplyMutator(RuntimeRules rules, TDCampaignMutatorDefinition mutator)
        {
            if (mutator == null)
            {
                return;
            }

            rules.startingBudget += mutator.startingBudgetDelta;
            rules.startingIntegrity += mutator.startingIntegrityDelta;
            rules.enemyHpMultiplier *= NeutralMultiplier(mutator.enemyHpMultiplier);
            rules.enemySpeedMultiplier *= NeutralMultiplier(mutator.enemySpeedMultiplier);
            rules.enemyArmorBonus += mutator.enemyArmorBonus;
            rules.rewardMultiplier *= NeutralMultiplier(mutator.rewardMultiplier);
            rules.resonanceMultiplier *= NeutralMultiplier(mutator.resonanceGainMultiplier);
            rules.scenarioCostMultiplier *= NeutralMultiplier(mutator.scenarioCostMultiplier);
        }

        private static float NeutralMultiplier(float value)
        {
            return value > 0f ? value : 1f;
        }

        private static IReadOnlyList<TDTowerKind> ResolveUnlockedTowers(TDCampaignDefinition campaign, int levelIndex)
        {
            var result = new List<TDTowerKind>();
            foreach (var level in campaign.levels.OrderBy(item => item.levelIndex))
            {
                if (level.levelIndex > levelIndex)
                {
                    break;
                }

                if (level.newTowerUnlocks == null)
                {
                    continue;
                }

                foreach (var towerId in level.newTowerUnlocks)
                {
                    if (TDTower.TryParseTowerId(towerId, out var kind) && !result.Contains(kind))
                    {
                        result.Add(kind);
                    }
                }
            }

            if (result.Count == 0)
            {
                result.Add(TDTowerKind.RailLancer);
            }

            return result;
        }

        private static float ResolveScenarioFactor(
            TDCampaignScenarioMechanicDefinition mechanic,
            StrategyDefinition strategy,
            float conversionPct)
        {
            if (mechanic == null)
            {
                return 1f;
            }

            var fit = mechanic.mechanicType switch
            {
                "route_switch" => strategy.id == "control_lattice" ? 1.08f : 1.04f,
                "environment_device" => strategy.id == "adaptive_network" ? 1.08f : 1.04f,
                "boss_phase" => strategy.id == "focused_fire" ? 1.09f : 1.04f,
                "timed_reinforcement" => strategy.id == "adaptive_network" ? 1.07f : 1.04f,
                _ => strategy.id == "control_lattice" ? 1.06f : 1.04f
            };
            return 1f + (fit - 1f) * Mathf.Clamp01(conversionPct / 100f);
        }

        private static int ResolveScenarioOpportunities(
            TDCampaignLevelDefinition level,
            TDCampaignScenarioMechanicDefinition mechanic,
            int waveCount)
        {
            if (mechanic == null)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.FloorToInt(waveCount * 2f / 3f));
        }

        private static int ResolveMaxScenarioUses(
            TDCampaignLevelDefinition level,
            TDCampaignScenarioMechanicDefinition mechanic,
            int opportunities)
        {
            if (mechanic == null || opportunities <= 0)
            {
                return 0;
            }

            if (mechanic.maxCharges <= 0)
            {
                return opportunities;
            }

            var scenario = level?.scenario;
            var examBonus = scenario?.milestoneExam == true ? 1 : 0;
            var intensityBonus = Mathf.Max(0, (scenario?.intensity ?? 0) - 2);
            return Mathf.Min(opportunities, mechanic.maxCharges + examBonus + intensityBonus);
        }

        private static bool ShouldUseScenarioCommand(
            int waveOffset,
            int waveCount,
            int uses,
            int targetUses)
        {
            if (targetUses <= uses || waveCount <= 0)
            {
                return false;
            }

            var targetUsesByWave = Mathf.FloorToInt((waveOffset + 1f) * targetUses / waveCount);
            return targetUsesByWave > uses;
        }

        private static float CalculateTagMatch(IEnumerable<string> counterTags, ISet<string> waveTags)
        {
            if (counterTags == null)
            {
                return 0f;
            }

            var tags = counterTags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray();
            if (tags.Length == 0)
            {
                return 0f;
            }

            var matches = tags.Count(waveTags.Contains);
            return Mathf.Clamp01(matches / (float)Mathf.Min(2, tags.Length));
        }

        private static float DoctrineFit(string doctrine, IEnumerable<WaveSample> samples)
        {
            var averageCounter = samples.Average(item => item.counterMatch);
            return doctrine == "Adaptive"
                ? Mathf.Clamp01(0.55f + averageCounter * 0.35f)
                : Mathf.Clamp01(0.42f + averageCounter * 0.50f);
        }

        private static TDBalanceRouteHeat[] BuildRouteHeat(IDictionary<string, float> pressureByLane, int escaped)
        {
            var total = Mathf.Max(1f, pressureByLane.Values.Sum());
            var ordered = pressureByLane
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            var remainingEscapes = escaped;
            var result = new List<TDBalanceRouteHeat>(ordered.Length);
            for (var i = 0; i < ordered.Length; i++)
            {
                var share = ordered[i].Value / total;
                var laneEscapes = i == ordered.Length - 1
                    ? remainingEscapes
                    : Mathf.Clamp(Mathf.RoundToInt(escaped * share), 0, remainingEscapes);
                remainingEscapes -= laneEscapes;
                result.Add(new TDBalanceRouteHeat
                {
                    routeId = ordered[i].Key,
                    pressure = Round1(ordered[i].Value),
                    pressurePct = Round1(share * 100f),
                    escapedEnemies = laneEscapes
                });
            }

            return result.ToArray();
        }

        private static TDBalanceTowerContribution[] BuildTowerContributions(
            IReadOnlyList<TowerSlot> slots,
            IReadOnlyDictionary<TDTowerKind, float> damage,
            IReadOnlyDictionary<TDTowerKind, float> control)
        {
            var totalDamage = Mathf.Max(1f, damage.Values.Sum());
            var totalControl = Mathf.Max(1f, control.Values.Sum());
            var result = new List<TDBalanceTowerContribution>();
            foreach (var group in slots.GroupBy(slot => slot.kind).OrderBy(group => (int)group.Key))
            {
                damage.TryGetValue(group.Key, out var towerDamage);
                control.TryGetValue(group.Key, out var towerControl);
                var damageShare = towerDamage / totalDamage * 100f;
                var controlShare = towerControl / totalControl * 100f;
                result.Add(new TDBalanceTowerContribution
                {
                    towerId = TDTower.GetTowerId(group.Key),
                    displayName = TDTower.GetDisplayName(group.Key),
                    count = group.Count(),
                    upgrades = group.Sum(slot => slot.Tier),
                    damageSharePct = Round1(damageShare),
                    controlSharePct = Round1(controlShare),
                    contributionScore = Round1(damageShare * 0.72f + controlShare * 0.28f)
                });
            }

            return result.OrderByDescending(item => item.contributionScore).ToArray();
        }

        private static string BuildStrategySignature(
            StrategyDefinition strategy,
            IReadOnlyList<TDTowerKind> unlocked,
            IReadOnlyList<TowerSlot> slots)
        {
            var unlockedSet = new HashSet<TDTowerKind>(unlocked);
            var priority = strategy.priority
                .Where(unlockedSet.Contains)
                .Take(4)
                .Select(TDTower.GetTowerId);
            var branch = strategy.id == "adaptive_network" ? "mixed" : strategy.branch.ToString().ToLowerInvariant();
            var composition = slots
                .GroupBy(slot => slot.kind)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => (int)group.Key)
                .Take(4)
                .Select(group => $"{TDTower.GetTowerId(group.Key)}x{group.Count()}");
            return $"{strategy.doctrine}|{branch}|{string.Join(">", priority)}|{string.Join(",", composition)}";
        }

        private static TDBalanceLevelSummary[] BuildLevelSummaries(
            TDCampaignDefinition campaign,
            IReadOnlyDictionary<int, TDWaveSet> wavesByLevel,
            IReadOnlyDictionary<string, TDEnemyCatalogEntry> enemyById,
            IReadOnlyList<TDBalanceRunResult> runs)
        {
            var result = new List<TDBalanceLevelSummary>();
            foreach (var level in campaign.levels.OrderBy(item => item.levelIndex))
            {
                var difficultySummaries = new List<TDBalanceDifficultySummary>();
                foreach (var difficulty in campaign.difficultyTiers.OrderBy(item => item.tier))
                {
                    var matches = runs
                        .Where(item => item.levelIndex == level.levelIndex && item.difficultyId == difficulty.difficultyId)
                        .ToArray();
                    difficultySummaries.Add(new TDBalanceDifficultySummary
                    {
                        difficultyId = difficulty.difficultyId,
                        runCount = matches.Length,
                        victories = matches.Count(item => item.victory),
                        winRatePct = Round1(matches.Length > 0 ? matches.Count(item => item.victory) * 100f / matches.Length : 0f),
                        medianScore = Round1(Median(matches.Select(item => item.totalScore))),
                        medianDurationSeconds = Round1(Median(matches.Select(item => item.durationSeconds))),
                        medianFirstLeakWave = Round1(Median(matches.Select(item => (float)item.firstLeakWave))),
                        medianScenarioConversionPct = Round1(Median(matches.Select(item => item.scenarioConversionPct)))
                    });
                }

                result.Add(new TDBalanceLevelSummary
                {
                    levelIndex = level.levelIndex,
                    levelId = level.levelId,
                    milestoneExam = level.scenario?.milestoneExam ?? false,
                    authoredPressure = Round1(AuthoredPressureRatio(level.levelIndex, wavesByLevel[level.levelIndex], enemyById) * 100f),
                    difficulties = difficultySummaries.ToArray()
                });
            }

            return result.ToArray();
        }

        private static TDBalanceExamSummary[] BuildExamSummaries(
            TDCampaignDefinition campaign,
            IReadOnlyList<TDBalanceRunResult> runs)
        {
            var result = new List<TDBalanceExamSummary>();
            foreach (var examLevel in ExamLevels)
            {
                var level = campaign.levels.First(item => item.levelIndex == examLevel);
                var standard = runs
                    .Where(item => item.levelIndex == examLevel && item.difficultyId == "standard")
                    .ToArray();
                var successful = standard.Where(item => item.victory).ToArray();
                var signatures = successful.Select(item => item.strategySignature).Distinct().ToArray();
                var spread = standard.Length > 0
                    ? standard.Max(item => item.totalScore) - standard.Min(item => item.totalScore)
                    : 0f;
                result.Add(new TDBalanceExamSummary
                {
                    levelIndex = examLevel,
                    levelId = level.levelId,
                    strategyCount = standard.Select(item => item.strategyId).Distinct().Count(),
                    standardVictories = successful.Length,
                    distinctSuccessfulSignatures = signatures.Length,
                    standardScoreSpread = Round1(spread),
                    successfulStrategyIds = successful.Select(item => item.strategyId).Distinct().ToArray(),
                    successfulSignatures = signatures,
                    pass = standard.Select(item => item.strategyId).Distinct().Count() == 3 &&
                           successful.Select(item => item.strategyId).Distinct().Count() == 3 &&
                           signatures.Length >= 3
                });
            }

            return result.ToArray();
        }

        private static void BuildCurveAlarms(TDBalanceMatrixReport report, ICollection<TDBalanceAlarm> alarms)
        {
            if (report.stalledRuns > 0)
            {
                alarms.Add(new TDBalanceAlarm
                {
                    severity = "ERROR",
                    code = "STALLED_RUN",
                    levelIndex = 0,
                    message = "One or more deterministic runs did not complete.",
                    evidence = $"stalled={report.stalledRuns}"
                });
            }

            var economyFailures = report.runs
                .Where(item => item.victory && !item.economyDecisionValue)
                .ToArray();
            if (economyFailures.Length > 0)
            {
                alarms.Add(new TDBalanceAlarm
                {
                    severity = "ERROR",
                    code = "ECONOMY_SATURATION",
                    levelIndex = economyFailures.Min(item => item.levelIndex),
                    message = "A victorious strategy exits the final five waves without meaningful budget decisions.",
                    evidence = $"failed={economyFailures.Length}, maxReserve={economyFailures.Max(item => item.endingBudget)}, " +
                               $"minLatePurchases={economyFailures.Min(item => item.finalFivePurchases)}"
                });
            }

            var earlySaturation = report.runs
                .Where(item => item.firstSaturatedWave > 0 && item.firstSaturatedWave < item.finalFiveStartWave)
                .ToArray();
            if (earlySaturation.Length > 0)
            {
                alarms.Add(new TDBalanceAlarm
                {
                    severity = "ERROR",
                    code = "FORTIFICATION_SATURATES_EARLY",
                    levelIndex = earlySaturation.Min(item => item.levelIndex),
                    message = "A strategy reaches full towers and full upgrades before the final five waves.",
                    evidence = $"failed={earlySaturation.Length}, earliestWave={earlySaturation.Min(item => item.firstSaturatedWave)}"
                });
            }

            var standard = report.levelSummaries
                .Select(item => item.difficulties.First(diff => diff.difficultyId == "standard"))
                .ToArray();
            if (!IsStandardCurveSmooth(standard, out var cliffLevel))
            {
                alarms.Add(new TDBalanceAlarm
                {
                    severity = "ERROR",
                    code = "DIFFICULTY_SPIKE",
                    levelIndex = cliffLevel,
                    message = "Standard median score contains an unexplained adjacent cliff.",
                    evidence = "Adjacent median score drop exceeds 7.5 points."
                });
            }

            for (var start = 0; start <= standard.Length - 4; start++)
            {
                var window = standard.Skip(start).Take(4).Select(item => item.medianScore).ToArray();
                if (window.Max() - window.Min() >= 0.5f)
                {
                    continue;
                }

                alarms.Add(new TDBalanceAlarm
                {
                    severity = "WARNING",
                    code = "FLAT_MISSIONS",
                    levelIndex = start + 1,
                    message = "Four-level Standard score window is effectively flat.",
                    evidence = $"range={window.Max() - window.Min():0.0}"
                });
            }

            var standardRunCount = standard.Sum(item => item.runCount);
            var standardVictories = standard.Sum(item => item.victories);
            var averageExamSpread = report.examSummaries.Length == 0
                ? 0f
                : report.examSummaries.Average(item => item.standardScoreSpread);
            if (standardRunCount > 0 && standardVictories == standardRunCount && averageExamSpread < 3f)
            {
                alarms.Add(new TDBalanceAlarm
                {
                    severity = "WARNING",
                    code = "STANDARD_ALL_WIN",
                    levelIndex = 1,
                    message = "Every automated Standard strategy wins every mission, so the baseline curve is not discriminating strategy quality.",
                    evidence = $"wins={standardVictories}/{standardRunCount}, averageExamSpread={averageExamSpread:0.0}"
                });
            }

            foreach (var difficultyId in new[] { "standard", "veteran", "ember_trial" })
            {
                var curve = report.levelSummaries
                    .Select(item => item.difficulties.First(diff => diff.difficultyId == difficultyId))
                    .ToArray();
                for (var i = 1; i < curve.Length; i++)
                {
                    var drop = curve[i - 1].winRatePct - curve[i].winRatePct;
                    if (drop < 34f)
                    {
                        continue;
                    }

                    alarms.Add(new TDBalanceAlarm
                    {
                        severity = "WARNING",
                        code = "WIN_RATE_CLIFF",
                        levelIndex = i + 1,
                        message = $"{difficultyId} automated win rate drops abruptly between adjacent missions.",
                        evidence = $"L{i:00}={curve[i - 1].winRatePct:0.0}% -> L{i + 1:00}={curve[i].winRatePct:0.0}%"
                    });
                }

                var zeroStreakStart = -1;
                for (var i = 10; i <= curve.Length; i++)
                {
                    var isZero = i < curve.Length && curve[i].winRatePct <= 0.01f;
                    if (isZero && zeroStreakStart < 0)
                    {
                        zeroStreakStart = i;
                    }

                    if (isZero)
                    {
                        continue;
                    }

                    if (zeroStreakStart >= 0 && i - zeroStreakStart >= 3)
                    {
                        alarms.Add(new TDBalanceAlarm
                        {
                            severity = "WARNING",
                            code = "LATE_ZERO_STREAK",
                            levelIndex = zeroStreakStart + 1,
                            message = $"{difficultyId} has at least three consecutive late-campaign missions with no automated victories.",
                            evidence = $"L{zeroStreakStart + 1:00}-L{i:00} zero-win streak"
                        });
                    }

                    zeroStreakStart = -1;
                }
            }

            foreach (var exam in report.examSummaries)
            {
                if (exam.pass)
                {
                    continue;
                }

                alarms.Add(new TDBalanceAlarm
                {
                    severity = "ERROR",
                    code = "STRATEGY_COLLAPSE",
                    levelIndex = exam.levelIndex,
                    message = "Milestone exam does not support three distinct successful Standard strategy signatures.",
                    evidence = $"strategies={exam.strategyCount}, victories={exam.standardVictories}, signatures={exam.distinctSuccessfulSignatures}"
                });
            }

            if (!HasDifficultyOrder(report.levelSummaries))
            {
                alarms.Add(new TDBalanceAlarm
                {
                    severity = "ERROR",
                    code = "DIFFICULTY_INVERSION",
                    levelIndex = 0,
                    message = "At least one level is not ordered Standard > Veteran > Ember Trial by median score.",
                    evidence = "Inspect p102_level_curve.csv for the inversion."
                });
            }
        }

        private static bool IsStandardCurveSmooth(IReadOnlyList<TDBalanceDifficultySummary> standard, out int cliffLevel)
        {
            cliffLevel = 0;
            for (var i = 1; i < standard.Count; i++)
            {
                if (standard[i - 1].medianScore - standard[i].medianScore <= 7.5f)
                {
                    continue;
                }

                cliffLevel = i + 1;
                return false;
            }

            return standard.Count == 20;
        }

        private static bool HasDifficultyOrder(IEnumerable<TDBalanceLevelSummary> levels)
        {
            foreach (var level in levels)
            {
                var standard = level.difficulties.First(item => item.difficultyId == "standard").medianScore;
                var veteran = level.difficulties.First(item => item.difficultyId == "veteran").medianScore;
                var ember = level.difficulties.First(item => item.difficultyId == "ember_trial").medianScore;
                if (!(standard > veteran && veteran > ember))
                {
                    return false;
                }
            }

            return true;
        }

        private static float AuthoredPressureRatio(
            int levelIndex,
            TDWaveSet waveSet,
            IReadOnlyDictionary<string, TDEnemyCatalogEntry> enemyById)
        {
            var peak = 0f;
            foreach (var wave in waveSet.waves)
            {
                var pressure = 0f;
                foreach (var group in wave.groups)
                {
                    if (enemyById.TryGetValue(group.enemyId, out var enemy))
                    {
                        pressure += group.count * enemy.threatCost;
                    }
                }

                peak = Mathf.Max(peak, pressure);
            }

            var expectedPeak = 48f + levelIndex * 7.45f;
            return peak / Mathf.Max(1f, expectedPeak);
        }

        private static TDCampaignMapDefinition FindMap(TDCampaignDefinition campaign, string mapId)
        {
            return campaign.maps.FirstOrDefault(item => item.mapId == mapId);
        }

        private static TDCampaignChapterDefinition FindChapter(TDCampaignDefinition campaign, string chapterId)
        {
            return campaign.chapters.FirstOrDefault(item => item.chapterId == chapterId);
        }

        private static float Median(IEnumerable<float> values)
        {
            var ordered = values.OrderBy(value => value).ToArray();
            if (ordered.Length == 0)
            {
                return 0f;
            }

            var middle = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (ordered[middle - 1] + ordered[middle]) * 0.5f
                : ordered[middle];
        }

        private static float DeterministicNoise(
            int seed,
            int levelIndex,
            int difficultyTier,
            string strategyId,
            int waveIndex)
        {
            var text = $"{seed}:{levelIndex}:{difficultyTier}:{strategyId}:{waveIndex}";
            var hash = StableHash(text);
            return (hash % 2001) / 1000f - 1f;
        }

        private static string BuildFingerprint(IEnumerable<TDBalanceRunResult> runs)
        {
            var parts = runs
                .OrderBy(item => item.runId, StringComparer.Ordinal)
                .Select(item => $"{item.runId}:{item.victory}:{item.totalScore:0.0}:{item.firstLeakWave}:{item.towersBuilt}:{item.upgradesPurchased}");
            return StableHash(string.Join("|", parts)).ToString("X8");
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static TDBalanceAlarm ConfigAlarm(string code, string message, int levelIndex = 0)
        {
            return new TDBalanceAlarm
            {
                severity = "ERROR",
                code = code,
                levelIndex = levelIndex,
                message = message,
                evidence = "Configuration validation failed before the matrix completed."
            };
        }

        private static TDBalanceMatrixReport FinalizeFailedReport(
            TDBalanceMatrixReport report,
            IReadOnlyCollection<TDBalanceAlarm> alarms)
        {
            report.alarms = alarms.ToArray();
            report.curveStatus = "BLOCKED";
            report.hardPass = false;
            report.fingerprint = "00000000";
            return report;
        }

        private static float Round1(float value)
        {
            return Mathf.Round(value * 10f) / 10f;
        }
    }
}
#endif
