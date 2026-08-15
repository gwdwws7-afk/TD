#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TD
{
    [Serializable]
    public sealed class TDP135TowerDecisionResult
    {
        public string towerId;
        public string towerKind;
        public int cellX;
        public int cellY;
        public int buildWave;
        public int firstUpgradeWave;
        public int lastUpgradeWave;
        public bool builtAfterMechanic;
        public int damageBranchUpgrades;
        public int utilityBranchUpgrades;
        public string specializationId;
        public int spend;
        public int damage;
        public int kills;
        public int controls;
        public float damageSharePct;
    }

    [Serializable]
    public sealed class TDP135LaneDecisionResult
    {
        public string laneId;
        public int spawned;
        public int spawnedHealth;
        public int damage;
        public int kills;
        public int escapes;
        public int integrityDamage;
        public float spawnedHealthSharePct;
        public float damageSharePct;
        public float escapeSharePct;
    }

    [Serializable]
    public sealed class TDP135WaveDecisionResult
    {
        public int waveIndex;
        public string phase;
        public string goalTag;
        public int budgetStart;
        public int budgetEnd;
        public int integrityStart;
        public int integrityEnd;
        public int kills;
        public int escapes;
        public int integrityDamage;
        public int readinessScore;
        public int purchases;
        public bool cleared;
        public string[] failureReasons;
    }

    [Serializable]
    public sealed class TDP135RealRunReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public TDP124RealRunReport baseline;
        public string mechanicType;
        public string mechanicPolicy;
        public int[] mechanicActivationWaves;
        public int firstMechanicWave;
        public int towersAtFirstMechanic;
        public int upgradesAtFirstMechanic;
        public int buildsAfterFirstMechanic;
        public int upgradesAfterFirstMechanic;
        public bool mechanicChangedDecisionsDuringRun;
        public int bossPhasesTriggered;
        public int bossPhasesSuppressed;
        public int expectedBossPhases;
        public int firstWaveEscapes;
        public int firstWaveIntegrityDamage;
        public int firstWaveReadiness;
        public int firstWavePressureScore;
        public int damageBranchUpgrades;
        public int utilityBranchUpgrades;
        public float routeDamageEntropyPct;
        public string placementSignature;
        public string compositionSignature;
        public string branchSignature;
        public string replaySignature;
        public TDP135LaneDecisionResult[] lanes;
        public TDP135WaveDecisionResult[] waves;
        public TDP135TowerDecisionResult[] towers;
    }

    public sealed partial class TDGameManager
    {
        private string _p135MechanicPolicy = "adaptive";
        private readonly List<int> _p135MechanicActivationWaves = new();
        private readonly Dictionary<string, int> _p135TowerBuildWaves = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _p135TowerFirstUpgradeWaves = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _p135TowerLastUpgradeWaves = new(StringComparer.Ordinal);
        private readonly HashSet<string> _p135TowersBuiltAfterMechanic = new(StringComparer.Ordinal);
        private int _p135FirstMechanicTowerCount;
        private int _p135FirstMechanicUpgradeCount;
        private int _p135BossPhasesTriggered;
        private int _p135BossPhasesSuppressed;

        public string DebugConfigureP135ForTest(string mechanicPolicy = "adaptive")
        {
            var normalized = NormalizeGroupToken(mechanicPolicy);
            if (normalized != "adaptive" && normalized != "engage" && normalized != "hold")
            {
                return $"skip: unknown P13.5 mechanic policy {mechanicPolicy}";
            }

            _p135MechanicPolicy = normalized;
            _p135MechanicActivationWaves.Clear();
            _p135TowerBuildWaves.Clear();
            _p135TowerFirstUpgradeWaves.Clear();
            _p135TowerLastUpgradeWaves.Clear();
            _p135TowersBuiltAfterMechanic.Clear();
            _p135FirstMechanicTowerCount = 0;
            _p135FirstMechanicUpgradeCount = 0;
            _p135BossPhasesTriggered = 0;
            _p135BossPhasesSuppressed = 0;
            return $"p13.5.configured=True mechanicPolicy={_p135MechanicPolicy}";
        }

        private bool ResolveP135ScenarioAutoplayDecision(bool adaptiveDecision)
        {
            return _p135MechanicPolicy == "engage" ||
                   _p135MechanicPolicy != "hold" && adaptiveDecision;
        }

        private TDTowerKind[] ApplyP135FormationPriority(TDTowerKind[] original)
        {
            if (_p135MechanicPolicy != "engage" || _activeScenarioMechanic == null)
            {
                return original;
            }

            var preferred = NormalizeGroupToken(_activeScenarioMechanic.mechanicType) switch
            {
                "signal_gate" => new[]
                {
                    TDTowerKind.CinderMortar, TDTowerKind.ArcWelder,
                    TDTowerKind.FrostCoil, TDTowerKind.EmberFlak
                },
                "timed_reinforcement" => new[]
                {
                    TDTowerKind.SiegeDrill, TDTowerKind.CinderMortar,
                    TDTowerKind.FrostCoil, TDTowerKind.RailLancer
                },
                "route_switch" => new[]
                {
                    TDTowerKind.FrostCoil, TDTowerKind.GravSnare,
                    TDTowerKind.EmberFlak, TDTowerKind.ArcWelder
                },
                "environment_device" => new[]
                {
                    TDTowerKind.RailLancer, TDTowerKind.SiegeDrill,
                    TDTowerKind.ResonanceBeacon, TDTowerKind.FrostCoil
                },
                "boss_phase" => new[]
                {
                    TDTowerKind.RailLancer, TDTowerKind.SiegeDrill,
                    TDTowerKind.ResonanceBeacon, TDTowerKind.FrostCoil
                },
                _ => Array.Empty<TDTowerKind>()
            };
            return preferred.Concat(original ?? Array.Empty<TDTowerKind>()).Distinct().ToArray();
        }

        private TDTowerUpgradeBranch ApplyP135UpgradeBranch(
            TDTowerKind kind,
            int tier,
            TDTowerUpgradeBranch original)
        {
            if (_p135MechanicPolicy != "engage" || _activeScenarioMechanic == null)
            {
                return original;
            }

            return NormalizeGroupToken(_activeScenarioMechanic.mechanicType) switch
            {
                "signal_gate" when kind == TDTowerKind.FrostCoil ||
                                        kind == TDTowerKind.ArcWelder ||
                                        kind == TDTowerKind.EmberFlak => TDTowerUpgradeBranch.Utility,
                "timed_reinforcement" => tier == 2
                    ? TDTowerUpgradeBranch.Utility
                    : TDTowerUpgradeBranch.Damage,
                "route_switch" when kind == TDTowerKind.FrostCoil ||
                                         kind == TDTowerKind.GravSnare ||
                                         kind == TDTowerKind.ArcWelder => TDTowerUpgradeBranch.Utility,
                "environment_device" when kind == TDTowerKind.RailLancer ||
                                              kind == TDTowerKind.SiegeDrill ||
                                              kind == TDTowerKind.ResonanceBeacon => TDTowerUpgradeBranch.Damage,
                "boss_phase" when kind == TDTowerKind.RailLancer ||
                                       kind == TDTowerKind.SiegeDrill => TDTowerUpgradeBranch.Damage,
                "boss_phase" => TDTowerUpgradeBranch.Utility,
                _ => original
            };
        }

        private TDTowerKind ApplyP135BuildKind(
            IReadOnlyList<TDTowerKind> buildableKinds,
            IReadOnlyList<TDTower> towers,
            TDTowerKind original)
        {
            if (_p135MechanicPolicy != "engage" || _activeScenarioMechanic == null ||
                buildableKinds == null || buildableKinds.Count == 0)
            {
                return original;
            }

            var preferred = ApplyP135FormationPriority(Array.Empty<TDTowerKind>())
                .Where(buildableKinds.Contains)
                .ToArray();
            if (preferred.Length == 0)
            {
                return original;
            }

            return preferred
                .OrderBy(kind => towers.Count(tower => tower != null && tower.Kind == kind))
                .ThenBy(kind => Array.IndexOf(preferred, kind))
                .First();
        }

        private float GetP135MechanicSiteBias(Vector3 world, float range, TDTowerKind kind)
        {
            if (_p135MechanicPolicy != "engage" || _activeScenarioMechanic == null)
            {
                return 0f;
            }

            var type = NormalizeGroupToken(_activeScenarioMechanic.mechanicType);
            var targetProgress = type switch
            {
                "signal_gate" => 0.18f,
                "timed_reinforcement" => 0.58f,
                "route_switch" => 0.44f,
                "environment_device" => 0.64f,
                "boss_phase" => 0.74f,
                _ => 0.5f
            };
            var bias = 0f;
            var scoredPaths = new HashSet<IReadOnlyList<Vector3>>();
            foreach (var pair in _activeLanePaths)
            {
                var path = pair.Value;
                if (path == null || path.Count == 0 || !scoredPaths.Add(path))
                {
                    continue;
                }

                var target = GetPathPointAtNormalizedProgress(path, targetProgress);
                var distance = Vector2.Distance(world, target);
                if (distance <= range)
                {
                    var laneWeight = type == "route_switch" &&
                                     pair.Key.IndexOf(_scenarioRouteBias, StringComparison.OrdinalIgnoreCase) >= 0
                        ? 1.45f
                        : 1f;
                    bias += (1f - distance / Mathf.Max(0.01f, range)) * 13f * laneWeight;
                }
            }

            if (type == "environment_device" &&
                (kind == TDTowerKind.RailLancer || kind == TDTowerKind.SiegeDrill))
            {
                bias += 3.5f;
            }
            else if (type == "boss_phase" &&
                     (kind == TDTowerKind.RailLancer || kind == TDTowerKind.ResonanceBeacon))
            {
                bias += 4.5f;
            }

            return bias;
        }

        private void TrackP135TowerBuilt(TDTower tower)
        {
            if (tower == null)
            {
                return;
            }

            _p135TowerBuildWaves[tower.AnalyticsId] = Mathf.Max(0, _wave);
            if (_p135MechanicActivationWaves.Count > 0)
            {
                _p135TowersBuiltAfterMechanic.Add(tower.AnalyticsId);
            }
        }

        private void TrackP135TowerUpgrade(TDTower tower)
        {
            if (tower == null)
            {
                return;
            }

            var id = tower.AnalyticsId;
            if (!_p135TowerFirstUpgradeWaves.ContainsKey(id))
            {
                _p135TowerFirstUpgradeWaves[id] = Mathf.Max(0, _wave);
            }

            _p135TowerLastUpgradeWaves[id] = Mathf.Max(0, _wave);
        }

        private void TrackP135ScenarioActivation(string mechanicType)
        {
            if (_p135MechanicActivationWaves.Count == 0)
            {
                _p135FirstMechanicTowerCount = FindObjectsByType<TDTower>(FindObjectsSortMode.None).Length;
                _p135FirstMechanicUpgradeCount = _upgradesPurchased;
            }

            _p135MechanicActivationWaves.Add(Mathf.Max(0, _wave));
        }

        private void TrackP135BossPhase(bool suppressed)
        {
            _p135BossPhasesTriggered++;
            if (suppressed)
            {
                _p135BossPhasesSuppressed++;
            }
        }

        public TDP135RealRunReport DebugBuildP135RunReport()
        {
            var baseline = DebugBuildP124RunReport();
            var towerObjects = FindObjectsByType<TDTower>(FindObjectsSortMode.None)
                .Where(tower => tower != null)
                .ToDictionary(tower => tower.AnalyticsId, tower => tower, StringComparer.Ordinal);
            var towers = baseline.towers.Select(item =>
            {
                towerObjects.TryGetValue(item.towerId, out var tower);
                var specialization = tower?.ActiveSpecialization;
                return new TDP135TowerDecisionResult
                {
                    towerId = item.towerId,
                    towerKind = item.towerKind,
                    cellX = item.cellX,
                    cellY = item.cellY,
                    buildWave = GetP135DictionaryValue(_p135TowerBuildWaves, item.towerId),
                    firstUpgradeWave = GetP135DictionaryValue(_p135TowerFirstUpgradeWaves, item.towerId),
                    lastUpgradeWave = GetP135DictionaryValue(_p135TowerLastUpgradeWaves, item.towerId),
                    builtAfterMechanic = _p135TowersBuiltAfterMechanic.Contains(item.towerId),
                    damageBranchUpgrades = tower?.DamageBranchCount ?? 0,
                    utilityBranchUpgrades = tower?.UtilityBranchCount ?? 0,
                    specializationId = specialization?.specializationId ?? "none",
                    spend = item.spend,
                    damage = item.damage,
                    kills = item.kills,
                    controls = item.controls,
                    damageSharePct = item.damageSharePct
                };
            }).ToArray();

            var totalSpawnedHealth = Mathf.Max(1, _laneStats.Values.Where(stat => stat != null).Sum(stat => stat.spawnedHealth));
            var totalLaneDamage = Mathf.Max(1, _laneStats.Values.Where(stat => stat != null).Sum(stat => stat.damageDealt));
            var totalLaneEscapes = Mathf.Max(1, _laneStats.Values.Where(stat => stat != null).Sum(stat => stat.escapes));
            var lanes = _laneStats.Values
                .Where(stat => stat != null)
                .OrderBy(stat => stat.laneKey, StringComparer.Ordinal)
                .Select(stat => new TDP135LaneDecisionResult
                {
                    laneId = stat.laneKey,
                    spawned = stat.spawned,
                    spawnedHealth = stat.spawnedHealth,
                    damage = stat.damageDealt,
                    kills = stat.kills,
                    escapes = stat.escapes,
                    integrityDamage = stat.integrityDamageTaken,
                    spawnedHealthSharePct = RoundP124(stat.spawnedHealth * 100f / totalSpawnedHealth),
                    damageSharePct = RoundP124(stat.damageDealt * 100f / totalLaneDamage),
                    escapeSharePct = RoundP124(stat.escapes * 100f / totalLaneEscapes)
                }).ToArray();
            var waves = _waveStats.Values
                .Where(stat => stat != null && stat.logged)
                .OrderBy(stat => stat.waveIndex)
                .Select(stat => new TDP135WaveDecisionResult
                {
                    waveIndex = stat.waveIndex,
                    phase = stat.phase,
                    goalTag = stat.goalTag,
                    budgetStart = stat.budgetStart,
                    budgetEnd = stat.budgetEnd,
                    integrityStart = stat.integrityStart,
                    integrityEnd = stat.integrityEnd,
                    kills = stat.kills,
                    escapes = stat.escapes,
                    integrityDamage = stat.integrityDamageTaken,
                    readinessScore = stat.readinessScore,
                    purchases = stat.buildsPurchased + stat.upgradesPurchased + stat.scenarioUses,
                    cleared = stat.cleared,
                    failureReasons = stat.failureReasons
                        .OrderByDescending(pair => pair.Value)
                        .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => $"{pair.Key}:{pair.Value}")
                        .ToArray()
                }).ToArray();
            var firstWave = waves.FirstOrDefault(item => item.waveIndex == 1);
            var firstWaveEscapes = firstWave?.escapes ?? 0;
            var firstWaveIntegrityDamage = firstWave?.integrityDamage ?? 0;
            var firstWaveReadiness = firstWave?.readinessScore ?? 0;
            var firstWavePressure = Mathf.Clamp(
                firstWaveEscapes * 10 +
                firstWaveIntegrityDamage * 4 +
                Mathf.Max(0, 62 - firstWaveReadiness),
                0,
                100);
            var damageBranches = towers.Sum(item => item.damageBranchUpgrades);
            var utilityBranches = towers.Sum(item => item.utilityBranchUpgrades);
            var firstMechanicWave = _p135MechanicActivationWaves.Count == 0
                ? 0
                : _p135MechanicActivationWaves[0];
            var buildsAfterMechanic = towers.Count(item => item.builtAfterMechanic);
            var upgradesAfterMechanic = firstMechanicWave <= 0
                ? 0
                : towers.Sum(item =>
                    item.lastUpgradeWave >= firstMechanicWave
                        ? Mathf.Max(0, item.damageBranchUpgrades + item.utilityBranchUpgrades -
                                      (item.firstUpgradeWave < firstMechanicWave ? 1 : 0))
                        : 0);
            var expectedBossPhases = _activeScenarioMechanic?.bossPhaseThresholds?.Length ?? 0;
            if (NormalizeGroupToken(_activeScenarioMechanic?.mechanicType) == "boss_phase" && expectedBossPhases == 0)
            {
                expectedBossPhases = 2;
            }

            var placementSignature = string.Join(
                "|",
                towers.OrderBy(item => item.cellX).ThenBy(item => item.cellY)
                    .Select(item => $"{item.cellX},{item.cellY}:{item.towerKind}"));
            var compositionSignature = string.Join(
                "|",
                towers.GroupBy(item => item.towerKind)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => $"{group.Key}:{group.Count()}"));
            var branchSignature = string.Join(
                "|",
                towers.GroupBy(item => item.towerKind)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group =>
                        $"{group.Key}:D{group.Sum(item => item.damageBranchUpgrades)}U{group.Sum(item => item.utilityBranchUpgrades)}"));
            return new TDP135RealRunReport
            {
                schemaVersion = "p135-real-run-v1",
                generatedUtc = DateTime.UtcNow.ToString("o"),
                baseline = baseline,
                mechanicType = NormalizeGroupToken(_activeScenarioMechanic?.mechanicType),
                mechanicPolicy = _p135MechanicPolicy,
                mechanicActivationWaves = _p135MechanicActivationWaves.ToArray(),
                firstMechanicWave = firstMechanicWave,
                towersAtFirstMechanic = _p135FirstMechanicTowerCount,
                upgradesAtFirstMechanic = _p135FirstMechanicUpgradeCount,
                buildsAfterFirstMechanic = buildsAfterMechanic,
                upgradesAfterFirstMechanic = upgradesAfterMechanic,
                mechanicChangedDecisionsDuringRun = buildsAfterMechanic > 0 || upgradesAfterMechanic > 0,
                bossPhasesTriggered = _p135BossPhasesTriggered,
                bossPhasesSuppressed = _p135BossPhasesSuppressed,
                expectedBossPhases = expectedBossPhases,
                firstWaveEscapes = firstWaveEscapes,
                firstWaveIntegrityDamage = firstWaveIntegrityDamage,
                firstWaveReadiness = firstWaveReadiness,
                firstWavePressureScore = firstWavePressure,
                damageBranchUpgrades = damageBranches,
                utilityBranchUpgrades = utilityBranches,
                routeDamageEntropyPct = CalculateP135EntropyPct(lanes.Select(item => item.damage)),
                placementSignature = placementSignature,
                compositionSignature = compositionSignature,
                branchSignature = branchSignature,
                replaySignature = $"{placementSignature}#{compositionSignature}#{branchSignature}",
                lanes = lanes,
                waves = waves,
                towers = towers
            };
        }

        public string DebugWriteP135RunJson(string outputPath)
        {
            var report = DebugBuildP135RunReport();
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
            return DebugAuditP135ForTest() + $"p13.5.report.path={fullPath}\n";
        }

        public string DebugAuditP135ForTest()
        {
            var report = DebugBuildP135RunReport();
            var difficulty = report.baseline.difficultyId;
            var firstWaveLimit = difficulty == "ember_trial" ? 8 : difficulty == "veteran" ? 6 : 4;
            var complete = report.baseline.completed && !report.baseline.stalled;
            var firstWave = report.waves.Any(item => item.waveIndex == 1) &&
                            report.firstWaveEscapes <= firstWaveLimit &&
                            report.firstWavePressureScore <= 85;
            var branches = report.damageBranchUpgrades + report.utilityBranchUpgrades ==
                           report.baseline.upgradesPurchased;
            var telemetry = report.lanes.Length > 0 &&
                            report.waves.Length >= report.baseline.wavesCleared &&
                            report.waves.Length <= report.baseline.wavesCleared + (report.baseline.victory ? 0 : 1) &&
                            report.towers.Length == report.baseline.towersBuilt && branches;
            var mechanic = _p135MechanicPolicy == "hold"
                ? report.mechanicActivationWaves.Length == 0
                : report.baseline.scenarioOpportunities == 0 || report.mechanicActivationWaves.Length > 0;
            var boss = report.mechanicType != "boss_phase" || !report.baseline.victory ||
                       report.bossPhasesTriggered >= Mathf.Min(1, report.expectedBossPhases);
            var pass = complete && firstWave && telemetry && mechanic && boss;
            return
                $"p13.5.audit.complete={complete} [waves={report.baseline.wavesCleared}/{report.baseline.waveCount}]\n" +
                $"p13.5.audit.firstWave={firstWave} [pressure={report.firstWavePressureScore},escapes={report.firstWaveEscapes},limit={firstWaveLimit}]\n" +
                $"p13.5.audit.telemetry={telemetry} [lanes={report.lanes.Length},waves={report.waves.Length},towers={report.towers.Length},branches={report.damageBranchUpgrades}/{report.utilityBranchUpgrades}]\n" +
                $"p13.5.audit.mechanic={mechanic} [type={report.mechanicType},policy={report.mechanicPolicy},uses={report.mechanicActivationWaves.Length}]\n" +
                $"p13.5.audit.boss={boss} [phases={report.bossPhasesTriggered},suppressed={report.bossPhasesSuppressed}]\n" +
                $"p13.5.audit.pass={pass}\n";
        }

        private static int GetP135DictionaryValue(IReadOnlyDictionary<string, int> values, string key)
        {
            return !string.IsNullOrWhiteSpace(key) && values.TryGetValue(key, out var value) ? value : 0;
        }

        private static float CalculateP135EntropyPct(IEnumerable<int> values)
        {
            var positive = values.Where(value => value > 0).Select(value => (float)value).ToArray();
            if (positive.Length <= 1)
            {
                return positive.Length == 0 ? 0f : 100f;
            }

            var total = positive.Sum();
            var entropy = 0f;
            for (var i = 0; i < positive.Length; i++)
            {
                var probability = positive[i] / total;
                entropy -= probability * Mathf.Log(probability);
            }

            return RoundP124(entropy * 100f / Mathf.Log(positive.Length));
        }
    }
}
#endif
