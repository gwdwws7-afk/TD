using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    /// <summary>
    /// Manager services for the four exam bosses (expansion batch 2):
    /// build-cell blocking (Containermaw), lane swaps and the split twin
    /// (Junction Tyrant), kiln summons (Custodian), mimic reads and signal
    /// jamming (Harbinger). Own file per the split red line — the main
    /// TDGameManager stays out of boss specifics.
    /// </summary>
    public sealed partial class TDGameManager
    {
        private struct TDBlockedCell
        {
            public Vector2Int Cell;
            public float ExpiresAt;
        }

        private readonly List<TDBlockedCell> _bossBlockedCells = new();
        private float _resonanceChargeFrozenUntil;

        public bool IsResonanceChargeFrozen => Time.time < _resonanceChargeFrozenUntil;

        /// <summary>Phase feedback: tactical event + boss-phase cinematic.</summary>
        public void NotifyBossPhaseTransition(TDEnemy boss, string label, string detail)
        {
            PushTacticalEvent($"BOSS {label} — {detail}", TDBossPhases.PhaseEventDurationSeconds);
            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.BossPhase,
                "[B!]",
                label.ToUpperInvariant(),
                detail,
                TDBattleFeedbackTier.Critical,
                1.2f);
            PlayCriticalSfxTone("boss_phase", 245f, 0.28f, 0.84f, true);
        }

        /// <summary>
        /// Containermaw's thrown container: seals a random open build cell for
        /// a window — the spatial-economy half of the L05 exam.
        /// </summary>
        public bool BlockRandomBuildCell(float duration)
        {
            if (_gridMap == null)
            {
                return false;
            }

            PruneExpiredBuildBlocks();
            var candidates = _gridMap.UsesAuthoredBuildCells
                ? _gridMap.AuthoredBuildCells
                : _gridMap.RecommendedBuildCells;
            var open = new List<Vector2Int>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var cell = candidates[i];
                if (_gridMap.GetBuildSiteValidity(cell) == TDBuildSiteValidity.Valid)
                {
                    open.Add(cell);
                }
            }

            if (open.Count == 0)
            {
                return false;
            }

            var picked = open[Random.Range(0, open.Count)];
            _gridMap.SetRuntimeBlocked(picked, true);
            _bossBlockedCells.Add(new TDBlockedCell { Cell = picked, ExpiresAt = Time.time + duration });
            PushTacticalEvent($"Container sealed a build cell ({duration:0}s)", 4.6f);
            PlaySfxTone("boss_container", 210f, 0.24f, 0.7f, false);
            return true;
        }

        private void PruneExpiredBuildBlocks()
        {
            var now = Time.time;
            for (var i = _bossBlockedCells.Count - 1; i >= 0; i--)
            {
                if (_bossBlockedCells[i].ExpiresAt <= now)
                {
                    _gridMap?.SetRuntimeBlocked(_bossBlockedCells[i].Cell, false);
                    _bossBlockedCells.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Junction Tyrant's forced reroute: swap to another lane at the same
        /// route progress. No-op on single-lane maps.
        /// </summary>
        public bool SwapEnemyToAlternateLane(TDEnemy enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            var alternate = ResolveAlternateLaneKey(enemy.LaneKey);
            if (string.IsNullOrEmpty(alternate))
            {
                return false;
            }

            var progress = enemy.GetRouteProgress01();
            enemy.SwapPath(GetSpawnPathForLane(alternate), progress, alternate);
            PushTacticalEvent($"Junction Tyrant rerouted to the {alternate} line", 4.6f);
            return true;
        }

        /// <summary>
        /// Tyrant's split at 35%: a twin spawns on the other line at half the
        /// remaining health; both bodies drop their debuffs.
        /// </summary>
        public void SpawnTyrantTwin(TDEnemy tyrant)
        {
            if (tyrant == null || _gameOver ||
                !_enemyCatalog.TryGetValue("junction_tyrant", out var entry))
            {
                return;
            }

            var alternate = ResolveAlternateLaneKey(tyrant.LaneKey) ?? tyrant.LaneKey;
            var twin = SpawnEnemy(entry, GetSpawnPathForLane(alternate), _wave, 30000 + _runtimeSpawnIndex, alternate);
            if (twin == null)
            {
                return;
            }

            var half = Mathf.Max(1, Mathf.CeilToInt(tyrant.CurrentHealth * 0.5f));
            twin.WarpToProgress(tyrant.GetRouteProgress01());
            twin.SetBaseSpeed(TDBossPhases.TyrantTwinSpeed);
            twin.SetArmorFlat(TDBossPhases.TyrantTwinArmor);
            twin.SetCurrentHealth(half);
            twin.ClearDebuffs();
            tyrant.SetCurrentHealth(half);
            tyrant.SetBaseSpeed(TDBossPhases.TyrantTwinSpeed);
            tyrant.SetArmorFlat(TDBossPhases.TyrantTwinArmor);
            tyrant.ClearDebuffs();
            PushTacticalEvent("Junction Tyrant split — two bodies, two lines", 5.4f);
        }

        /// <summary>Custodian's kiln wave: swarm from the rear entrance.</summary>
        public void SummonKilnWave(string laneKey)
        {
            StartCoroutine(SpawnSplitChildren("ash_swarm", TDBossPhases.CustodianSummonAshSwarm, 0.16f, laneKey));
            StartCoroutine(SpawnSplitChildren("plated_spore", TDBossPhases.CustodianSummonPlatedSpore, 0.30f, laneKey));
            PushTacticalEvent("Kiln Custodian summons the kiln tide", 5.0f);
        }

        /// <summary>
        /// Harbinger's read: the rank-th most-built tower kind this run
        /// (standing towers; ties break by enum order for determinism).
        /// </summary>
        public TDTowerKind? GetNthMostBuiltTowerKind(int rank)
        {
            if (rank < 0)
            {
                return null;
            }

            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            if (towers.Length == 0)
            {
                return null;
            }

            var counts = new Dictionary<TDTowerKind, int>();
            for (var i = 0; i < towers.Length; i++)
            {
                if (towers[i] == null)
                {
                    continue;
                }

                var kind = towers[i].Kind;
                counts[kind] = counts.TryGetValue(kind, out var count) ? count + 1 : 1;
            }

            var ordered = new List<KeyValuePair<TDTowerKind, int>>(counts);
            ordered.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : a.Key.CompareTo(b.Key));
            return rank < ordered.Count ? ordered[rank].Key : null;
        }

        /// <summary>Harbinger's Signal Jam mimic: resonance charge frozen 3s.</summary>
        public void ApplyMimicSignalJamming()
        {
            _resonanceChargeFrozenUntil = Mathf.Max(_resonanceChargeFrozenUntil, Time.time + 3f);
            PushTacticalEvent("Echo Harbinger jams resonance charging", 3.4f);
        }

        private string ResolveAlternateLaneKey(string currentLane)
        {
            if (_activeLanePaths == null || _activeLanePaths.Count == 0)
            {
                return null;
            }

            foreach (var pair in _activeLanePaths)
            {
                if (!string.Equals(pair.Key, currentLane, System.StringComparison.Ordinal) &&
                    pair.Value != null && pair.Value.Count > 1)
                {
                    return pair.Key;
                }
            }

            return null;
        }
    }
}
