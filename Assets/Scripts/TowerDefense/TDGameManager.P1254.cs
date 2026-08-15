#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
using System;
using System.Linq;
using UnityEngine;

namespace TD
{
    [Serializable]
    public sealed class TDP1254SoakRuntimeState
    {
        public bool active;
        public int targetEnemies;
        public int spawnedEnemies;
        public int refillCount;
        public int resolvedEnemies;
        public int kills;
        public int escapes;
        public int activeEnemies;
        public int activeTowers;
        public int activeProjectiles;
        public int integrity;
        public string lastSpawnStatus;
    }

    public sealed partial class TDGameManager
    {
        private static readonly string[] P1254SoakEnemyIds =
        {
            "skitter_runner",
            "ash_swarm",
            "carapace_brute",
            "plated_spore",
            "burrow_sapper",
            "ember_leech",
            "spore_carrier",
            "rail_warden",
            "cinder_glider",
            "echo_mimic"
        };

        private static readonly string[] P1254SoakLaneKeys =
        {
            "left",
            "center",
            "right",
            "default"
        };

        private bool _p1254SoakActive;
        private int _p1254SoakTargetEnemies;
        private int _p1254SoakSpawnedEnemies;
        private int _p1254SoakRefillCount;
        private int _p1254SoakStartKills;
        private int _p1254SoakStartEscapes;
        private int _p1254SoakEnemyCursor;
        private float _p1254SoakNextRefillRealtime;
        private string _p1254SoakLastSpawnStatus = string.Empty;

        public string DebugBeginP1254ContinuousSoakForTest(int targetEnemies = 36)
        {
            if (!_campaignDeploymentConfirmed || _gridMap == null || _campaignRoute?.level == null)
            {
                return "skip: P12.5.4 soak requires a deployed campaign mission";
            }

            if (_gameOver)
            {
                return "skip: P12.5.4 soak cannot begin after mission end";
            }

            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
                _waveRoutine = null;
            }

            _p124AutoplayEnabled = false;
            _p124AutoplayTerminal = false;
            _p124AutoplayStalled = false;
            _p1254SoakActive = true;
            _p1254SoakTargetEnemies = Mathf.Clamp(targetEnemies, 12, 96);
            _p1254SoakSpawnedEnemies = 0;
            _p1254SoakRefillCount = 0;
            _p1254SoakStartKills = _totalKills;
            _p1254SoakStartEscapes = _totalEscapes;
            _p1254SoakEnemyCursor = 0;
            _p1254SoakNextRefillRealtime = 0f;
            _p1254SoakLastSpawnStatus = string.Empty;
            _lineIntegrity = Mathf.Max(_lineIntegrity, 5000);
            _startingLineIntegrity = Mathf.Max(_startingLineIntegrity, _lineIntegrity);
            _isInPrepPhase = false;
            _waveStartRequested = false;
            _missionBoardOpen = false;
            HideRoutePreview();
            TDReleaseDiagnostics.MarkCheckpoint("p1254_continuous_soak");
            return $"p12.5.4.soak.started=True target={_p1254SoakTargetEnemies} " +
                   $"wave={_wave} towers={FindObjectsByType<TDTower>(FindObjectsSortMode.None).Length}";
        }

        public string DebugEndP1254ContinuousSoakForTest()
        {
            var state = DebugGetP1254SoakRuntimeState();
            _p1254SoakActive = false;
            return $"p12.5.4.soak.ended=True spawned={state.spawnedEnemies} " +
                   $"resolved={state.resolvedEnemies} kills={state.kills} escapes={state.escapes}";
        }

        public TDP1254SoakRuntimeState DebugGetP1254SoakRuntimeState()
        {
            _activeEnemies.RemoveAll(enemy => enemy == null);
            return new TDP1254SoakRuntimeState
            {
                active = _p1254SoakActive,
                targetEnemies = _p1254SoakTargetEnemies,
                spawnedEnemies = _p1254SoakSpawnedEnemies,
                refillCount = _p1254SoakRefillCount,
                resolvedEnemies = Mathf.Max(
                    0,
                    _totalKills - _p1254SoakStartKills + _totalEscapes - _p1254SoakStartEscapes),
                kills = Mathf.Max(0, _totalKills - _p1254SoakStartKills),
                escapes = Mathf.Max(0, _totalEscapes - _p1254SoakStartEscapes),
                activeEnemies = _activeEnemies.Count,
                activeTowers = FindObjectsByType<TDTower>(FindObjectsSortMode.None).Length,
                activeProjectiles = FindObjectsByType<TDProjectile>(FindObjectsSortMode.None).Length,
                integrity = _lineIntegrity,
                lastSpawnStatus = _p1254SoakLastSpawnStatus
            };
        }

        private void UpdateP1254ContinuousSoak()
        {
            if (!_p1254SoakActive)
            {
                return;
            }

            if (_gameOver)
            {
                _p1254SoakActive = false;
                return;
            }

            _lineIntegrity = Mathf.Max(_lineIntegrity, 1000);
            _activeEnemies.RemoveAll(enemy => enemy == null);
            if (_activeEnemies.Count >= _p1254SoakTargetEnemies ||
                Time.realtimeSinceStartup < _p1254SoakNextRefillRealtime)
            {
                return;
            }

            _p1254SoakNextRefillRealtime = Time.realtimeSinceStartup + 0.10f;
            var missing = _p1254SoakTargetEnemies - _activeEnemies.Count;
            var spawnCount = Mathf.Clamp(missing, 1, 8);
            var enemyId = ResolveP1254SoakEnemyId();
            var laneKey = P1254SoakLaneKeys[_p1254SoakRefillCount % P1254SoakLaneKeys.Length];
            var progress = 0.02f + 0.025f * (_p1254SoakRefillCount % 5);
            var healthMultiplier = 2.5f + 0.5f * (_p1254SoakRefillCount % 4);
            _p1254SoakLastSpawnStatus = DebugSpawnEnemyForTest(
                enemyId,
                spawnCount,
                laneKey,
                progress,
                healthMultiplier);
            if (_p1254SoakLastSpawnStatus.StartsWith("spawned", StringComparison.OrdinalIgnoreCase))
            {
                _p1254SoakSpawnedEnemies += spawnCount;
                _p1254SoakRefillCount++;
            }
        }

        private string ResolveP1254SoakEnemyId()
        {
            for (var i = 0; i < P1254SoakEnemyIds.Length; i++)
            {
                var candidate = P1254SoakEnemyIds[_p1254SoakEnemyCursor % P1254SoakEnemyIds.Length];
                _p1254SoakEnemyCursor++;
                if (_enemyCatalog.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return _enemyCatalog.Keys.FirstOrDefault() ?? string.Empty;
        }
    }
}
#endif
