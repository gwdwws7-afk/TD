using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    /// <summary>
    /// Bypass contract for the Rail Barricade blocker wagon (expansion tower
    /// 11, rail-barricade-behavior-spec §2). Pure and scene-free so the
    /// contract's five unit pins can call it directly.
    /// </summary>
    public static class TDBlockContract
    {
        public const int EngageCapacity = 2;
        public const float EngageAttackInterval = 1.2f;
        public const float BossCrushStaggerSeconds = 3f;
        public const float BaseRebuildSeconds = 25f;
        public const float HoldingOrderRebuildSeconds = 15f;

        /// <summary>
        /// The explicit bypass list. Boss/final crush THROUGH the wagon (the
        /// crush path lives on the wagon, not here — this only says they never
        /// stop to fight). Ash swarm leaks every 4th specimen through the
        /// seams via the wagon-held pass counter.
        /// </summary>
        public static bool ResolveBypass(string enemyId, bool isBossOrFinal, ref int swarmPassCounter)
        {
            if (isBossOrFinal)
            {
                return true;
            }

            if (string.Equals(enemyId, "burrow_sapper", System.StringComparison.Ordinal) ||
                string.Equals(enemyId, "cinder_glider", System.StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(enemyId, "ash_swarm", System.StringComparison.Ordinal))
            {
                swarmPassCounter++;
                return swarmPassCounter % 4 == 0;
            }

            return false;
        }
    }

    /// <summary>
    /// Rail Barricade's interceptor body (expansion tower 11): a wagon
    /// parked on the track that enemies stop and fight. Interception buys
    /// time, not invincibility — every rule here is counterable by design.
    /// MonoBehaviour is the shell; engagement bookkeeping and the bypass
    /// contract are scene-free testable (see TDBlockerWagonTests).
    ///
    /// No coroutines on purpose: the rebuild is a timer field counted in
    /// Update, which keeps the whole contract EditMode-testable and
    /// pool-discipline friendly.
    /// </summary>
    public class TDBlockerWagon : MonoBehaviour
    {
        public const float EngageRadius = 0.62f;
        public const float ThornsInterval = 1.0f;
        public const float SlowFieldRefreshInterval = 0.2f;
        public const float SlowFieldLingerSeconds = 0.32f;
        public const float SlowFieldMaxTargets = 12;
        public const float DerailmentBlastRadius = 1.6f;
        public const int DerailmentBlastDamage = 30;
        public const int DerailmentArmorBreak = 6;
        public const float DerailmentArmorBreakSeconds = 3f;
        public const float DerailmentStaggerRadius = 2.0f;
        public const float DerailmentStaggerSeconds = 0.8f;
        public const float HoldingOrderTauntRadius = 2.5f;
        public const float HoldingOrderTauntInterval = 3.0f;

        private static readonly List<TDBlockerWagon> ActiveWagons = new();

        private TDGameManager _gameManager;
        private TDTower _ownerTower;
        private string _segmentKey;
        private readonly List<TDEnemy> _engaged = new(TDBlockContract.EngageCapacity);
        private int _swarmPassCounter;
        private bool _alive = true;
        private bool _rebuildArmed;
        private float _rebuildTimer;
        private float _thornsTimer;
        private float _slowFieldTimer;
        private float _tauntTimer;
        private float _hitFlashTimer;
        private SpriteRenderer _renderer;
        private static readonly Color BodyColor = new(0.36f, 0.54f, 0.66f, 1f);

        public bool IsAlive => _alive;
        public string SegmentKey => _segmentKey;
        public TDTower OwnerTower => _ownerTower;
        public int EngagedCount => _engaged.Count;
        public int WagonHpCurrent => Mathf.CeilToInt(_hp);
        public int WagonHpMax => OwnerTower != null ? OwnerTower.WagonMaxHp : 0;

        private float _hp;

        /// <summary>
        /// Spawns (or returns the existing) wagon for a track segment. One
        /// wagon per segment key — later barricades on the same segment are
        /// no-ops.
        /// </summary>
        public static TDBlockerWagon SpawnFor(TDGameManager gameManager, TDTower ownerTower, Vector3 trackPoint, string segmentKey)
        {
            var existing = FindAtSegment(segmentKey);
            if (existing != null)
            {
                return existing;
            }

            var wagonObject = new GameObject($"BlockerWagon_{segmentKey}");
            if (gameManager != null)
            {
                wagonObject.transform.SetParent(gameManager.transform, true);
            }

            wagonObject.transform.position = trackPoint;
            wagonObject.transform.localScale = Vector3.one * 0.92f;
            var renderer = wagonObject.AddComponent<SpriteRenderer>();
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback("Art/anim/tower_rail_barricade_00", BodyColor);
            renderer.sortingOrder = 10;
            renderer.color = BodyColor;
            var wagon = wagonObject.AddComponent<TDBlockerWagon>();
            wagon._gameManager = gameManager;
            wagon._ownerTower = ownerTower;
            wagon._segmentKey = segmentKey;
            wagon._renderer = renderer;
            wagon._hp = ownerTower != null ? ownerTower.WagonMaxHp : 240f;
            ActiveWagons.Add(wagon);
            return wagon;
        }

        public static TDBlockerWagon FindAtSegment(string segmentKey)
        {
            for (var i = 0; i < ActiveWagons.Count; i++)
            {
                var wagon = ActiveWagons[i];
                if (wagon != null && wagon._alive && wagon._segmentKey == segmentKey)
                {
                    return wagon;
                }
            }

            return null;
        }

        /// <summary>
        /// Nearest live wagon in engagement range of a position. Bosses never
        /// get a wagon back — they crush it and eat the 3s stall instead
        /// (their one explicit stagger exception, spec §2).
        /// </summary>
        public static TDBlockerWagon FindBlockingWagon(Vector3 position, TDEnemy enemy)
        {
            if (ActiveWagons.Count == 0)
            {
                return null;
            }

            PruneWagons();
            TDBlockerWagon nearest = null;
            var nearestSqr = EngageRadius * EngageRadius;
            for (var i = 0; i < ActiveWagons.Count; i++)
            {
                var wagon = ActiveWagons[i];
                if (wagon == null || !wagon._alive)
                {
                    continue;
                }

                var sqr = (wagon.transform.position - position).sqrMagnitude;
                if (sqr <= nearestSqr)
                {
                    nearest = wagon;
                    nearestSqr = sqr;
                }
            }

            if (nearest == null || enemy == null)
            {
                return nearest;
            }

            if (enemy.HasAnyTag("boss", "final"))
            {
                nearest.CrushBy(enemy);
                return null;
            }

            if (TDBlockContract.ResolveBypass(enemy.EnemyId, false, ref nearest._swarmPassCounter))
            {
                return null;
            }

            return nearest;
        }

        public static bool HasEngagedTraffic(TDTower ownerTower)
        {
            for (var i = 0; i < ActiveWagons.Count; i++)
            {
                var wagon = ActiveWagons[i];
                if (wagon != null && wagon._ownerTower == ownerTower && wagon._engaged.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Sell path: retract immediately, never rebuild.</summary>
        public static void RetractFor(TDTower ownerTower)
        {
            for (var i = ActiveWagons.Count - 1; i >= 0; i--)
            {
                var wagon = ActiveWagons[i];
                if (wagon != null && wagon._ownerTower == ownerTower)
                {
                    ActiveWagons.RemoveAt(i);
                    wagon.DestroySelf();
                }
            }
        }

        public static void ClearAll()
        {
            for (var i = ActiveWagons.Count - 1; i >= 0; i--)
            {
                ActiveWagons[i]?.DestroySelf();
            }

            ActiveWagons.Clear();
        }

        private void DestroySelf()
        {
            if (gameObject == null)
            {
                return;
            }

            // EditMode (tests) requires DestroyImmediate; play mode defers.
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        /// <summary>
        /// Front-row slot. Returns false when full — the caller queues on the
        /// enemy side and stands still (the path is unique; no detours).
        /// </summary>
        public bool TryEngage(TDEnemy enemy)
        {
            PruneEngaged();
            if (!_alive || enemy == null)
            {
                return false;
            }

            if (_engaged.Contains(enemy))
            {
                return true;
            }

            if (_engaged.Count >= TDBlockContract.EngageCapacity)
            {
                return false;
            }

            _engaged.Add(enemy);
            return true;
        }

        /// <summary>
        /// Engagement damage: raw lineDamage per the spec's own timing math
        /// (§3: light 1-2 → 2-4 waves of delay, heavy 4-5 → 60-90s — both
        /// only hold without an armor floor). WagonArmor is carried data
        /// awaiting its consumer (batch-2 tower-attacking enemies); putting
        /// it here would flatten the heavy/light contrast.
        /// </summary>
        public void TakeEngagementHit(int lineDamage)
        {
            if (!_alive)
            {
                return;
            }

            _hp -= Mathf.Max(1, lineDamage);
            _hitFlashTimer = 0.12f;
            if (_hp <= 0f)
            {
                DestroyWagon();
            }
        }

        /// <summary>Boss crush: one hit wrecks the body; the boss stalls 3s.</summary>
        public void CrushBy(TDEnemy boss)
        {
            if (!_alive || boss == null)
            {
                return;
            }

            boss.ApplyStagger(TDBlockContract.BossCrushStaggerSeconds, 0f);
            DestroyWagon();
        }

        public bool CanBypass(TDEnemy enemy)
        {
            return enemy != null &&
                   TDBlockContract.ResolveBypass(enemy.EnemyId, enemy.HasAnyTag("boss", "final"), ref _swarmPassCounter);
        }

        private void DestroyWagon()
        {
            if (!_alive)
            {
                return;
            }

            _alive = false;
            DetachAll();
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }

            DetonateDerailment();
            _rebuildArmed = _ownerTower != null && _ownerTower.gameObject != null;
            _rebuildTimer = ResolveRebuildSeconds();
        }

        private float ResolveRebuildSeconds()
        {
            return _ownerTower != null && _ownerTower.IsUtilitySpecialist
                ? TDBlockContract.HoldingOrderRebuildSeconds
                : TDBlockContract.BaseRebuildSeconds;
        }

        private void DetonateDerailment()
        {
            if (_gameManager == null || _ownerTower == null || !_ownerTower.IsDamageSpecialist)
            {
                return;
            }

            // Two sequential shared-buffer queries — the first list is fully
            // consumed before the second call reuses the buffer (P1 contract).
            var blasted = _gameManager.GetEnemiesInRange(transform.position, DerailmentBlastRadius, 10);
            for (var i = 0; i < blasted.Count; i++)
            {
                var enemy = blasted[i];
                if (enemy == null)
                {
                    continue;
                }

                var damage = _gameManager.GetModifiedDamageForEnemy(_ownerTower, enemy, DerailmentBlastDamage);
                enemy.TakeHit(damage, 0f, 0f, _ownerTower);
                enemy.ApplyArmorBreak(DerailmentArmorBreak, DerailmentArmorBreakSeconds);
            }

            var staggered = _gameManager.GetEnemiesInRange(transform.position, DerailmentStaggerRadius, 12);
            for (var i = 0; i < staggered.Count; i++)
            {
                staggered[i]?.ApplyStagger(DerailmentStaggerSeconds, 0.2f);
            }
        }

        /// <summary>Test seam + death hook: enemies detach and resume moving.</summary>
        public void DetachAll()
        {
            for (var i = 0; i < _engaged.Count; i++)
            {
                _engaged[i]?.DetachFromWagon();
            }

            _engaged.Clear();
        }

        private void PruneEngaged()
        {
            for (var i = _engaged.Count - 1; i >= 0; i--)
            {
                var enemy = _engaged[i];
                if (enemy == null || !enemy.IsTargetable)
                {
                    _engaged.RemoveAt(i);
                }
            }
        }

        private static void PruneWagons()
        {
            ActiveWagons.RemoveAll(wagon => wagon == null);
        }

        private void Update()
        {
            PruneEngaged();
            if (!_alive)
            {
                if (_rebuildArmed)
                {
                    _rebuildTimer -= Time.deltaTime;
                    if (_rebuildTimer <= 0f && _ownerTower != null && _ownerTower.gameObject != null)
                    {
                        _alive = true;
                        _rebuildArmed = false;
                        _hp = _ownerTower.WagonMaxHp;
                        if (_renderer != null)
                        {
                            _renderer.enabled = true;
                        }
                    }
                }

                return;
            }

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer = Mathf.Max(0f, _hitFlashTimer - Time.deltaTime);
                if (_renderer != null)
                {
                    _renderer.color = _hitFlashTimer > 0f
                        ? new Color(1f, 0.62f, 0.48f, 1f)
                        : BodyColor;
                }
            }

            // Stats pull from the owning tower every frame by design — no
            // event wiring, upgrades apply live.
            if (_ownerTower != null)
            {
                _hp = Mathf.Min(_hp + _ownerTower.WagonRepairPerSecond * Time.deltaTime, _ownerTower.WagonMaxHp);
            }

            UpdateThorns();
            UpdateSlowField();
            UpdateTauntPulse();
        }

        private void UpdateThorns()
        {
            var thorns = _ownerTower != null ? _ownerTower.WagonThornsPerSecond : 0;
            if (thorns <= 0 || _engaged.Count == 0)
            {
                return;
            }

            _thornsTimer -= Time.deltaTime;
            if (_thornsTimer > 0f)
            {
                return;
            }

            _thornsTimer = ThornsInterval;
            for (var i = 0; i < _engaged.Count; i++)
            {
                // Fixed damage — no armor channel (spec: 荆棘反伤不吃护甲减
                // 免), and silent: wagon damage never enters tower DPS stats
                // (zero-contribution exemption, spec §3).
                _engaged[i]?.TakeDirectDamage(thorns);
            }
        }

        private void UpdateSlowField()
        {
            var radius = _ownerTower != null ? _ownerTower.WagonSlowFieldRadius : 0f;
            var pct = _ownerTower != null ? _ownerTower.WagonSlowFieldPercent : 0f;
            if (radius <= 0f || pct <= 0f || _gameManager == null)
            {
                return;
            }

            _slowFieldTimer -= Time.deltaTime;
            if (_slowFieldTimer > 0f)
            {
                return;
            }

            _slowFieldTimer = SlowFieldRefreshInterval;
            var slowed = _gameManager.GetEnemiesInRange(transform.position, radius, Mathf.CeilToInt(SlowFieldMaxTargets));
            for (var i = 0; i < slowed.Count; i++)
            {
                slowed[i]?.ApplyFieldSlow(pct, SlowFieldLingerSeconds);
            }
        }

        private void UpdateTauntPulse()
        {
            if (_ownerTower == null || !_ownerTower.IsUtilitySpecialist || _gameManager == null)
            {
                return;
            }

            _tauntTimer -= Time.deltaTime;
            if (_tauntTimer > 0f)
            {
                return;
            }

            _tauntTimer = HoldingOrderTauntInterval;
            var nearby = _gameManager.GetEnemiesInRange(transform.position, HoldingOrderTauntRadius, 8);
            for (var i = 0; i < nearby.Count; i++)
            {
                var enemy = nearby[i];
                if (enemy == null || !enemy.IsTargetable)
                {
                    continue;
                }

                // Bypassers keep their identity (no counter bump from taunts).
                if (enemy.HasAnyTag("boss", "final") ||
                    string.Equals(enemy.EnemyId, "burrow_sapper", System.StringComparison.Ordinal) ||
                    string.Equals(enemy.EnemyId, "cinder_glider", System.StringComparison.Ordinal))
                {
                    continue;
                }

                enemy.TryEngageWagon(this);
            }
        }

        private void OnDestroy()
        {
            ActiveWagons.Remove(this);
        }
    }
}
