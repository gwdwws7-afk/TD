using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public sealed class TDProjectile : MonoBehaviour
    {
        private TDGameManager _gameManager;
        private TDEnemy _target;
        private TDTower _sourceTower;
        private TDTowerKind _sourceTowerKind;
        private int _damage;
        private float _speed;
        private float _aoeRadius;
        private int _aoeMaxTargets;
        private float _aoeMinFalloff;
        private float _slowPct;
        private float _slowDuration;
        private bool _damageSpecialist;
        private bool _utilitySpecialist;
        private SpriteRenderer _renderer;
        private float _trailTimer;
        private float _trailInterval;
        private Vector3 _lastTargetPosition;
        private bool _hasLastTargetPosition;
        private float _lifetime;
        private Color _projectileTint;
        private Color _trailStartColor;
        private Color _trailEndColor;
        private Color _impactStartColor;
        private Color _impactEndColor;
        private Color _aoeStartColor;
        private Color _aoeEndColor;
        private string _projectileSpritePath;
        private string _impactSpritePath;
        private float _projectileVisualScale;
        private float _trailScaleMultiplier;
        private float _trailDuration;
        private float _spinDegreesPerSecond;
        private float _rotationOffsetDegrees;
        private bool _orientToVelocity;
        private float _impactScale;
        private float _impactDuration;
        private const float MaxLifetimeSeconds = 5f;
        private const float ArcChainSearchRadiusMin = 1.15f;
        private const float ArcChainSearchRadiusScale = 1.22f;
        private const int ArcChainCandidateBonus = 3;
        private const int ArcChainCandidateMin = 3;
        private const int ArcChainCandidateMax = 9;
        private const int ArcChainCountMin = 2;
        private const int ArcChainCountMax = 7;
        private const float ArcChainDamageBaseScale = 0.70f;
        private const float ArcChainDamageDecayScale = 0.83f;
        private const float ArcChainExposeDuration = 1.0f;
        private const float ArcChainExposeMultiplier = 1.07f;
        private const int SiegeArmorBreakArmored = 5;
        private const int SiegeArmorBreakDefault = 1;
        private const float SiegeArmorBreakHeavyDuration = 3.0f;
        private const float SiegeArmorBreakDefaultDuration = 2.2f;
        private const float EmberPrimaryStaggerDuration = 0.30f;
        private const float EmberPrimaryStaggerMinSpeed = 0.12f;
        private const float EmberSplashRadiusMin = 0.88f;
        private const float EmberSplashRadiusScale = 1.30f;
        private const int EmberSplashMaxTargets = 5;
        private const float EmberSplashDamageScale = 0.30f;
        private const float EmberSplashStaggerDuration = 0.18f;
        private const float EmberSplashStaggerMinSpeed = 0.16f;
        private const float BeaconPrimaryMarkDuration = 1.6f;
        private const float BeaconPrimaryExposeDuration = 1.7f;
        private const float BeaconPrimaryExposeMultiplier = 1.12f;
        private const float BeaconPulseRadiusMin = 1.18f;
        private const float BeaconPulseRadiusScale = 1.50f;
        private const int BeaconPulseMaxTargets = 6;
        private const float BeaconPulseMarkDuration = 1.05f;
        private const float BeaconPulseExposeDuration = 1.05f;
        private const float BeaconPulseExposeMultiplier = 1.05f;
        private const float GravPrimaryStaggerDuration = 0.24f;
        private const float GravPrimaryStaggerMinSpeed = 0.20f;
        private const float GravPrimaryExposeDuration = 1.45f;
        private const float GravPrimaryExposeMultiplier = 1.10f;
        private const float GravPulseRadiusMin = 1.12f;
        private const float GravPulseRadiusScale = 1.25f;
        private const int GravPulseMinTargets = 6;
        private const float GravPulseStaggerDuration = 0.15f;
        private const float GravPulseStaggerMinSpeed = 0.25f;
        private const float GravPulseExposeDuration = 0.90f;
        private const float GravPulseExposeMultiplier = 1.04f;
        private const float DamageSpecExecuteThreshold = 0.40f;
        private const float UtilitySpecFieldRadiusMin = 0.85f;
        private const float UtilitySpecFieldRadiusScale = 0.72f;
        private const int UtilitySpecFieldMaxTargets = 4;
        private const float UtilitySpecExposeDuration = 0.68f;
        private const float UtilitySpecExposeMultiplier = 1.035f;
        private const float UtilitySpecStaggerDuration = 0.11f;
        private const float UtilitySpecStaggerMinSpeed = 0.48f;
        private const float ArcLinkBaseDuration = 0.11f;
        private const float ArcLinkDurationStep = 0.01f;
        private const float ArcLinkStartWidth = 0.045f;
        private const float ArcLinkEndWidth = 0.012f;
        private const float ArcLinkVerticalLift = 0.06f;
        private const float GravityBoundaryDuration = 0.46f;
        private const float GravityBoundaryInnerDuration = 0.33f;
        private static readonly Color ArcLinkStartColor = new(0.70f, 0.94f, 1f, 0.58f);
        private static readonly Color ArcLinkEndColor = new(0.34f, 0.76f, 1f, 0f);
        private static readonly Color GravityBoundaryOuterColor = new(0.54f, 0.62f, 1f, 0.46f);
        private static readonly Color GravityBoundaryInnerColor = new(0.78f, 0.84f, 1f, 0.36f);
        private static readonly Color DamageSpecPulseStartColor = new(1f, 0.88f, 0.36f, 0.86f);
        private static readonly Color DamageSpecPulseEndColor = new(1f, 0.42f, 0.10f, 0f);
        private static readonly Color UtilitySpecFieldStartColor = new(0.36f, 1f, 0.78f, 0.66f);
        private static readonly Color UtilitySpecFieldEndColor = new(0.16f, 0.78f, 0.98f, 0f);

        public static string GetProjectileResourcePath(TDTowerKind kind)
        {
            return $"Art/Combat/P11/projectile_{ResolveTowerVisualSlug(kind)}";
        }

        public static string GetImpactResourcePath(TDTowerKind kind)
        {
            return $"Art/Combat/P11/impact_{ResolveTowerVisualSlug(kind)}";
        }

        public void Initialize(
            TDGameManager gameManager,
            TDEnemy target,
            TDTower sourceTower,
            int damage,
            float speed,
            float aoeRadius,
            int aoeMaxTargets,
            float aoeMinFalloff,
            float slowPct,
            float slowDuration,
            bool damageSpecialist,
            bool utilitySpecialist)
        {
            _gameManager = gameManager;
            _target = target;
            _sourceTower = sourceTower;
            _sourceTowerKind = sourceTower != null ? sourceTower.Kind : TDTowerKind.RailLancer;
            _damage = damage;
            _speed = speed;
            _aoeRadius = aoeRadius;
            _aoeMaxTargets = Mathf.Max(1, aoeMaxTargets);
            _aoeMinFalloff = Mathf.Clamp01(aoeMinFalloff);
            _slowPct = Mathf.Clamp(slowPct, 0f, 0.9f);
            _slowDuration = Mathf.Max(0f, slowDuration);
            _damageSpecialist = damageSpecialist;
            _utilitySpecialist = utilitySpecialist;
            _renderer = GetComponent<SpriteRenderer>();
            _lastTargetPosition = target != null ? target.transform.position : transform.position;
            _hasLastTargetPosition = target != null;
            _lifetime = 0f;

            ConfigureVisualProfile(_sourceTowerKind);
            if (_renderer != null)
            {
                _renderer.sprite = TDArtLibrary.LoadSpriteOrFallback(_projectileSpritePath, _projectileTint);
                _renderer.color = Color.white;
                transform.localScale = Vector3.one * _projectileVisualScale;
            }

            // Detach from pool parent so it moves freely in world space.
            if (transform.parent != null && transform.parent.GetComponent<TDObjectPool>() != null)
            {
                var pos = transform.position;
                var rot = transform.rotation;
                var scale = transform.localScale;
                transform.SetParent(gameManager != null ? gameManager.transform : null, true);
                transform.SetPositionAndRotation(pos, rot);
                transform.localScale = scale;
            }
        }

        /// <summary>
        /// Called by the object pool when this projectile is returned.
        /// Clears all combat state so the next Get() starts clean.
        /// </summary>
        public void ResetForPool()
        {
            _gameManager = null;
            _target = null;
            _sourceTower = null;
            _sourceTowerKind = TDTowerKind.RailLancer;
            _damage = 0;
            _speed = 0f;
            _aoeRadius = 0f;
            _aoeMaxTargets = 1;
            _aoeMinFalloff = 1f;
            _slowPct = 0f;
            _slowDuration = 0f;
            _damageSpecialist = false;
            _utilitySpecialist = false;
            _trailTimer = 0f;
            _lastTargetPosition = Vector3.zero;
            _hasLastTargetPosition = false;
            _lifetime = 0f;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (_renderer != null)
            {
                _renderer.sprite = null;
                _renderer.color = Color.white;
            }
        }

        /// <summary>Return this projectile to the pool instead of destroying it.</summary>
        private void ReturnToPool()
        {
            var pool = TDObjectPool.Instance;
            if (pool != null)
            {
                // Re-parent to pool before release so it's out of the scene hierarchy.
                transform.SetParent(pool.transform, false);
                pool.ReleaseProjectile(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Lifetime cap: a bad speed config must never leave projectiles
            // flying forever.
            _lifetime += Time.deltaTime;
            if (_lifetime >= MaxLifetimeSeconds)
            {
                ResolveHit(transform.position);
                ReturnToPool();
                return;
            }

            // Keep flying to the last known position when the target dies or
            // escapes mid-flight — otherwise AoE/splash projectiles would be
            // consumed without ever resolving their area damage.
            var hasTarget = _target != null;
            if (hasTarget)
            {
                _lastTargetPosition = _target.transform.position;
                _hasLastTargetPosition = true;
            }
            else if (!_hasLastTargetPosition)
            {
                ReturnToPool();
                return;
            }

            var toTarget = _lastTargetPosition - transform.position;
            var step = _speed * Time.deltaTime;
            if (toTarget.sqrMagnitude <= step * step)
            {
                ResolveHit(transform.position);
                ReturnToPool();
                return;
            }

            var direction = toTarget.normalized;
            UpdateProjectileRotation(direction);
            transform.position += direction * step;
            EmitTrailGhost();
        }

        private void ResolveHit(Vector3 impactPoint)
        {
            SpawnImpactSpark(impactPoint, _aoeRadius > 0.01f);

            if (_aoeRadius <= 0.01f)
            {
                var damageTaken = ApplyDamage(_target, _damage, _slowPct, _slowDuration);
                ApplyTowerSpecialOnHit(_target, impactPoint, damageTaken, true);
                return;
            }

            SpawnAoeIndicator(impactPoint, _aoeRadius);

            var targets = _gameManager.GetEnemiesInRange(impactPoint, _aoeRadius, _aoeMaxTargets);
            for (var i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (enemy == null)
                {
                    continue;
                }

                var distanceRatio = Mathf.Clamp01(Vector3.Distance(impactPoint, enemy.transform.position) / _aoeRadius);
                var falloffMultiplier = Mathf.Lerp(1f, _aoeMinFalloff, distanceRatio);
                var adjustedDamage = Mathf.Max(1, Mathf.FloorToInt(_damage * falloffMultiplier));
                var damageTaken = ApplyDamage(enemy, adjustedDamage, _slowPct, _slowDuration);
                ApplyTowerSpecialOnHit(enemy, impactPoint, damageTaken, enemy == _target || i == 0);
            }
        }

        private int ApplyDamage(TDEnemy enemy, int rawDamage, float slowPct, float slowDuration)
        {
            if (enemy == null || rawDamage <= 0)
            {
                return 0;
            }

            rawDamage = ApplyDamageSpecialistBonus(enemy, rawDamage);
            var modifiedDamage = _gameManager != null
                ? _gameManager.GetModifiedDamageForEnemy(_sourceTower, enemy, rawDamage)
                : rawDamage;
            var damageTaken = enemy.TakeHit(modifiedDamage, slowPct, slowDuration, _sourceTower);
            var appliedSlowPct = damageTaken > 0 && enemy.HealthRatio > 0f ? slowPct : 0f;
            var appliedSlowDuration = appliedSlowPct > 0f ? slowDuration : 0f;
            _gameManager?.NotifyEnemyDamaged(_sourceTower, enemy, damageTaken, appliedSlowPct, appliedSlowDuration);
            return damageTaken;
        }

        private void ApplyTowerSpecialOnHit(TDEnemy enemy, Vector3 impactPoint, int damageTaken, bool isPrimaryImpact)
        {
            if (enemy == null || damageTaken <= 0)
            {
                return;
            }

            switch (_sourceTowerKind)
            {
                case TDTowerKind.ArcWelder:
                    if (isPrimaryImpact)
                    {
                        TriggerArcChain(enemy);
                    }
                    break;
                case TDTowerKind.SiegeDrill:
                    {
                        var armorBreak = enemy.HasTag("armored") ? SiegeArmorBreakArmored : SiegeArmorBreakDefault;
                        var breakDuration = enemy.HasTag("heavy") ? SiegeArmorBreakHeavyDuration : SiegeArmorBreakDefaultDuration;
                        enemy.ApplyArmorBreak(armorBreak, breakDuration);
                    }
                    break;
                case TDTowerKind.EmberFlak:
                    if (enemy.HasTag("fast") || enemy.HasTag("flank"))
                    {
                        enemy.ApplyStagger(EmberPrimaryStaggerDuration, EmberPrimaryStaggerMinSpeed);
                    }

                    if (isPrimaryImpact)
                    {
                        ApplyEmberFlakSplash(enemy.transform.position, enemy);
                    }
                    break;
                case TDTowerKind.ResonanceBeacon:
                    {
                        enemy.SetResonanceMark(BeaconPrimaryMarkDuration);
                        enemy.ApplyExposed(BeaconPrimaryExposeDuration, BeaconPrimaryExposeMultiplier);
                        if (isPrimaryImpact)
                        {
                            ApplyBeaconPulse(impactPoint, enemy);
                        }
                    }
                    break;
                case TDTowerKind.GravSnare:
                    {
                        enemy.ApplyStagger(GravPrimaryStaggerDuration, GravPrimaryStaggerMinSpeed);
                        enemy.ApplyExposed(GravPrimaryExposeDuration, GravPrimaryExposeMultiplier);
                        if (isPrimaryImpact)
                        {
                            ApplyGravityWell(impactPoint, enemy);
                        }
                    }
                    break;
                case TDTowerKind.SlagBurner:
                    {
                        enemy.ApplyBurn(
                            _sourceTower != null ? _sourceTower.BurnLayersPerHit : 0,
                            _sourceTower != null ? _sourceTower.BurnDamagePerLayer : 0f,
                            _sourceTower != null ? _sourceTower.BurnDuration : 0f,
                            _sourceTower);
                        if (_damageSpecialist && enemy.BurnLayers >= TDBurnSystem.MaxBurnLayers)
                        {
                            DetonateBurnStacks(enemy);
                        }
                    }
                    break;
            }

            if (_utilitySpecialist && isPrimaryImpact)
            {
                ApplyUtilitySpecialistField(impactPoint, enemy);
            }
        }

        private void DetonateBurnStacks(TDEnemy enemy)
        {
            if (enemy == null || enemy.BurnLayers <= 0)
            {
                return;
            }

            // Slag Sump: full stacks resolve at once as a direct hit (regular
            // armor pipeline), then the fire goes out.
            var burst = TDBurnSystem.ResolveDetonateDamage(enemy.BurnLayers, enemy.BurnDamagePerLayer);
            var modified = _gameManager != null
                ? _gameManager.GetModifiedDamageForEnemy(_sourceTower, enemy, burst)
                : burst;
            var damageTaken = enemy.TakeHit(modified, 0f, 0f, _sourceTower);
            if (damageTaken > 0)
            {
                _gameManager?.NotifyEnemyDamaged(_sourceTower, enemy, damageTaken, 0f, 0f);
            }
            enemy.ClearBurn();
        }

        private int ApplyDamageSpecialistBonus(TDEnemy enemy, int rawDamage)
        {
            if (!_damageSpecialist || enemy == null)
            {
                return rawDamage;
            }

            var multiplier = 1f;
            switch (_sourceTowerKind)
            {
                case TDTowerKind.RailLancer:
                    if (enemy.HasAnyTag("armored", "heavy", "boss"))
                    {
                        enemy.ApplyArmorBreak(6, 2.6f);
                        multiplier *= 1.38f;
                    }
                    break;
                case TDTowerKind.SlagBurner:
                    if (enemy.HasAnyTag("attrition", "heavy", "boss"))
                    {
                        multiplier *= 1.25f;
                    }
                    break;
                case TDTowerKind.CinderMortar:
                    if (enemy.HasAnyTag("swarm", "spawn", "support"))
                    {
                        multiplier *= 1.30f;
                        if (enemy.HealthRatio <= 0.48f)
                        {
                            multiplier *= 1.14f;
                        }
                    }
                    break;
                case TDTowerKind.FrostCoil:
                    if (enemy.IsSlowed || enemy.IsMarked || enemy.IsArmorBroken || enemy.HasTag("armored"))
                    {
                        multiplier *= 1.42f;
                    }
                    break;
                case TDTowerKind.ArcWelder:
                    if (enemy.HasAnyTag("swarm", "mixed", "spawn", "special"))
                    {
                        multiplier *= 1.24f;
                    }
                    break;
                case TDTowerKind.SiegeDrill:
                    if (enemy.HasAnyTag("armored", "heavy", "boss"))
                    {
                        enemy.ApplyArmorBreak(9, 3.4f);
                        multiplier *= 1.48f;
                    }
                    break;
                case TDTowerKind.EmberFlak:
                    if (enemy.HasAnyTag("fast", "flank", "swarm"))
                    {
                        multiplier *= 1.38f;
                        if (enemy.HealthRatio <= DamageSpecExecuteThreshold)
                        {
                            multiplier *= 1.16f;
                        }
                    }
                    break;
                case TDTowerKind.ResonanceBeacon:
                    if (enemy.IsMarked || enemy.HasAnyTag("support", "attrition", "special"))
                    {
                        multiplier *= 1.35f;
                        enemy.SetResonanceMark(1.9f);
                    }
                    break;
                case TDTowerKind.GravSnare:
                    if (enemy.HasAnyTag("heavy", "fast", "boss") || enemy.RouteProgress01 >= 0.55f)
                    {
                        multiplier *= 1.20f + (enemy.RouteProgress01 * 0.24f);
                    }
                    break;
            }

            if (multiplier > 1.001f)
            {
                SpawnSpecialistPulse(
                    enemy.transform.position,
                    0.72f,
                    0.24f,
                    DamageSpecPulseStartColor,
                    DamageSpecPulseEndColor,
                    "Fx_DamageSpecPulse",
                    TDWorldVisualOrder.ProjectileFx);
                _gameManager?.NotifyUltimateEffect(_sourceTower, enemy, false, 1);
            }

            return Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier));
        }

        private void ApplyUtilitySpecialistField(Vector3 impactPoint, TDEnemy primaryTarget)
        {
            if (_gameManager == null)
            {
                return;
            }

            var radiusMultiplier = _sourceTowerKind switch
            {
                TDTowerKind.CinderMortar => 1.30f,
                TDTowerKind.ArcWelder => 1.22f,
                TDTowerKind.EmberFlak => 1.26f,
                TDTowerKind.ResonanceBeacon => 1.34f,
                TDTowerKind.GravSnare => 1.36f,
                _ => 1f
            };
            var targetBonus = _sourceTowerKind switch
            {
                TDTowerKind.CinderMortar => 3,
                TDTowerKind.ArcWelder => 2,
                TDTowerKind.EmberFlak => 3,
                TDTowerKind.ResonanceBeacon => 4,
                TDTowerKind.GravSnare => 4,
                _ => 0
            };
            var synergyMultiplier = _gameManager.GetSpecializationSynergyMultiplier(_sourceTower, primaryTarget);
            var radius = Mathf.Max(UtilitySpecFieldRadiusMin, _aoeRadius * UtilitySpecFieldRadiusScale) * radiusMultiplier * synergyMultiplier;
            targetBonus += synergyMultiplier > 1.001f ? 2 : 0;
            SpawnSpecialistPulse(
                impactPoint,
                Mathf.Max(0.48f, radius * 0.42f),
                Mathf.Max(0.92f, radius * 2.18f),
                UtilitySpecFieldStartColor,
                UtilitySpecFieldEndColor,
                "Fx_UtilitySpecField",
                TDWorldVisualOrder.ProjectileBack);

            var targets = _gameManager.GetEnemiesInRange(impactPoint, radius, UtilitySpecFieldMaxTargets + targetBonus);
            var affected = 0;
            for (var i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (enemy == null)
                {
                    continue;
                }

                switch (_sourceTowerKind)
                {
                    case TDTowerKind.RailLancer:
                        enemy.ApplyStagger(enemy == primaryTarget ? 0.46f : 0.18f, 0.08f);
                        enemy.ApplyExposed(1.45f, 1.10f);
                        break;
                    case TDTowerKind.CinderMortar:
                        enemy.ApplyStagger(0.24f, 0.24f);
                        enemy.ApplyExposed(1.20f, 1.07f);
                        break;
                    case TDTowerKind.FrostCoil:
                        enemy.ApplyStagger(enemy.HasTag("boss") ? 0.16f : 0.42f, enemy.HasTag("boss") ? 0.45f : 0.02f);
                        enemy.ApplyExposed(1.10f, 1.08f);
                        break;
                    case TDTowerKind.ArcWelder:
                        enemy.ApplyExposed(1.55f, 1.10f);
                        if (enemy.HasAnyTag("fast", "special", "swarm"))
                        {
                            enemy.ApplyStagger(0.18f, 0.30f);
                        }
                        break;
                    case TDTowerKind.SiegeDrill:
                        enemy.ApplyArmorBreak(enemy.HasTag("armored") ? 7 : 3, 4.2f);
                        if (enemy.HasAnyTag("support", "heavy"))
                        {
                            enemy.ApplyStagger(0.22f, 0.24f);
                        }
                        break;
                    case TDTowerKind.EmberFlak:
                        if (enemy.HasAnyTag("fast", "flank", "spawn"))
                        {
                            enemy.ApplyStagger(0.36f, 0.05f);
                        }
                        enemy.ApplyExposed(0.95f, 1.05f);
                        break;
                    case TDTowerKind.ResonanceBeacon:
                        enemy.SetResonanceMark(2.1f);
                        enemy.ApplyExposed(1.65f, 1.12f);
                        break;
                    case TDTowerKind.GravSnare:
                        enemy.ApplyStagger(0.34f, 0.06f);
                        enemy.ApplyExposed(1.35f, 1.10f);
                        break;
                    default:
                        enemy.ApplyExposed(UtilitySpecExposeDuration, UtilitySpecExposeMultiplier);
                        if (enemy != primaryTarget && enemy.HasAnyTag("fast", "flank", "special", "swarm"))
                        {
                            enemy.ApplyStagger(UtilitySpecStaggerDuration, UtilitySpecStaggerMinSpeed);
                        }
                        break;
                }

                affected++;
            }

            _gameManager.NotifyUltimateEffect(_sourceTower, primaryTarget, true, Mathf.Max(1, affected));
        }

        private void TriggerArcChain(TDEnemy primaryTarget)
        {
            if (_gameManager == null || primaryTarget == null)
            {
                return;
            }

            var radius = Mathf.Max(ArcChainSearchRadiusMin, _aoeRadius * ArcChainSearchRadiusScale);
            var candidates = _gameManager.GetEnemiesInRange(
                primaryTarget.transform.position,
                radius,
                Mathf.Clamp(_aoeMaxTargets + ArcChainCandidateBonus + (_damageSpecialist ? 2 : 0), ArcChainCandidateMin, ArcChainCandidateMax));
            if (candidates.Count == 0)
            {
                return;
            }

            var chained = 0;
            var maxChains = Mathf.Clamp(_aoeMaxTargets + (_damageSpecialist ? 2 : 0), ArcChainCountMin, ArcChainCountMax);
            var visited = new HashSet<TDEnemy> { primaryTarget };
            var linkOrigin = primaryTarget.transform.position;
            for (var i = 0; i < candidates.Count && chained < maxChains; i++)
            {
                var target = candidates[i];
                if (target == null || visited.Contains(target))
                {
                    continue;
                }

                visited.Add(target);
                chained++;
                var chainDamageScale = ArcChainDamageBaseScale * Mathf.Pow(ArcChainDamageDecayScale, chained - 1) * (_damageSpecialist ? 1.15f : 1f);
                var chainDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * chainDamageScale));
                var damageTaken = ApplyDamage(target, chainDamage, 0f, 0f);
                if (damageTaken > 0)
                {
                    target.ApplyExposed(_utilitySpecialist ? 1.55f : ArcChainExposeDuration, _utilitySpecialist ? 1.10f : ArcChainExposeMultiplier);
                    if (_utilitySpecialist && target.HasAnyTag("fast", "special", "swarm"))
                    {
                        target.ApplyStagger(0.16f, 0.28f);
                    }
                    SpawnImpactSpark(target.transform.position, false);
                    SpawnArcLink(linkOrigin, target.transform.position, chained);
                    linkOrigin = target.transform.position;
                }
            }
        }

        private void ApplyEmberFlakSplash(Vector3 impactPoint, TDEnemy primaryTarget)
        {
            if (_gameManager == null)
            {
                return;
            }

            var splashRadius = Mathf.Max(EmberSplashRadiusMin, _aoeRadius * EmberSplashRadiusScale) * (_utilitySpecialist ? 1.28f : 1f);
            var targets = _gameManager.GetEnemiesInRange(impactPoint, splashRadius, EmberSplashMaxTargets + (_utilitySpecialist ? 3 : 0));
            for (var i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (enemy == null || enemy == primaryTarget)
                {
                    continue;
                }

                if (!enemy.HasTag("fast") && !enemy.HasTag("flank"))
                {
                    continue;
                }

                var splashDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * EmberSplashDamageScale));
                var dealt = ApplyDamage(enemy, splashDamage, 0f, 0f);
                if (dealt > 0)
                {
                    enemy.ApplyStagger(_utilitySpecialist ? 0.32f : EmberSplashStaggerDuration, _utilitySpecialist ? 0.04f : EmberSplashStaggerMinSpeed);
                }
            }
        }

        private void ApplyBeaconPulse(Vector3 impactPoint, TDEnemy primaryTarget)
        {
            if (_gameManager == null)
            {
                return;
            }

            var pulseRadius = Mathf.Max(BeaconPulseRadiusMin, _aoeRadius * BeaconPulseRadiusScale) * (_utilitySpecialist ? 1.30f : 1f);
            var targets = _gameManager.GetEnemiesInRange(impactPoint, pulseRadius, BeaconPulseMaxTargets + (_utilitySpecialist ? 3 : 0));
            for (var i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (enemy == null || enemy == primaryTarget)
                {
                    continue;
                }

                enemy.SetResonanceMark(_utilitySpecialist ? 1.85f : BeaconPulseMarkDuration);
                enemy.ApplyExposed(_utilitySpecialist ? 1.50f : BeaconPulseExposeDuration, _utilitySpecialist ? 1.10f : BeaconPulseExposeMultiplier);
            }
        }

        private void ApplyGravityWell(Vector3 impactPoint, TDEnemy primaryTarget)
        {
            if (_gameManager == null)
            {
                return;
            }

            var pulseRadius = Mathf.Max(GravPulseRadiusMin, _aoeRadius * GravPulseRadiusScale) * (_utilitySpecialist ? 1.32f : 1f);
            SpawnGravityBoundary(impactPoint, pulseRadius);
            var targets = _gameManager.GetEnemiesInRange(impactPoint, pulseRadius, Mathf.Max(_aoeMaxTargets, GravPulseMinTargets) + (_utilitySpecialist ? 4 : 0));
            for (var i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (enemy == null || enemy == primaryTarget)
                {
                    continue;
                }

                enemy.ApplyStagger(_utilitySpecialist ? 0.30f : GravPulseStaggerDuration, _utilitySpecialist ? 0.05f : GravPulseStaggerMinSpeed);
                enemy.ApplyExposed(_utilitySpecialist ? 1.30f : GravPulseExposeDuration, _utilitySpecialist ? 1.09f : GravPulseExposeMultiplier);
            }
        }

        private void SpawnArcLink(Vector3 from, Vector3 to, int chainIndex)
        {
            if (_gameManager == null)
            {
                return;
            }

            var fx = new GameObject("Fx_ArcChainLink");
            fx.transform.SetParent(_gameManager.transform, true);

            var lineFx = fx.AddComponent<TDTransientLineFx>();
            var liftedFrom = from + (Vector3.up * ArcLinkVerticalLift);
            var liftedTo = to + (Vector3.up * ArcLinkVerticalLift);
            var duration = ArcLinkBaseDuration + (Mathf.Clamp(chainIndex, 0, 5) * ArcLinkDurationStep);
            lineFx.Configure(
                liftedFrom,
                liftedTo,
                duration,
                ArcLinkStartWidth,
                ArcLinkEndWidth,
                ArcLinkStartColor,
                ArcLinkEndColor,
                TDWorldVisualOrder.ProjectileFx);
        }

        private void SpawnGravityBoundary(Vector3 impactPoint, float radius)
        {
            if (_gameManager == null || radius <= 0f)
            {
                return;
            }

            var (outer, outerFx, outerRenderer) = TDObjectPool.GetFxObject(
                _gameManager.transform, impactPoint, "Fx_GravityBoundary");

            outerRenderer.sortingOrder = TDWorldVisualOrder.ProjectileBack;
            outerRenderer.sprite = TDArtLibrary.GetSoftRingSprite();
            outerRenderer.color = GravityBoundaryOuterColor;

            var outerStartScale = Vector3.one * Mathf.Max(0.28f, radius * 0.72f);
            var outerEndScale = Vector3.one * Mathf.Max(0.58f, radius * 2f);
            outerFx.Configure(
                GravityBoundaryDuration,
                outerStartScale,
                outerEndScale,
                GravityBoundaryOuterColor,
                new Color(GravityBoundaryOuterColor.r, GravityBoundaryOuterColor.g, GravityBoundaryOuterColor.b, 0f));

            var (inner, innerFx, innerRenderer) = TDObjectPool.GetFxObject(
                _gameManager.transform, impactPoint, "Fx_GravityBoundaryCore");

            innerRenderer.sortingOrder = TDWorldVisualOrder.Projectile;
            innerRenderer.sprite = TDArtLibrary.GetSoftRingSprite();
            innerRenderer.color = GravityBoundaryInnerColor;

            var innerStartScale = Vector3.one * Mathf.Max(0.20f, radius * 0.46f);
            var innerEndScale = Vector3.one * Mathf.Max(0.42f, radius * 1.28f);
            innerFx.Configure(
                GravityBoundaryInnerDuration,
                innerStartScale,
                innerEndScale,
                GravityBoundaryInnerColor,
                new Color(GravityBoundaryInnerColor.r, GravityBoundaryInnerColor.g, GravityBoundaryInnerColor.b, 0f));
        }

        private void ConfigureVisualProfile(TDTowerKind sourceTowerKind)
        {
            _projectileSpritePath = GetProjectileResourcePath(sourceTowerKind);
            _impactSpritePath = GetImpactResourcePath(sourceTowerKind);
            _projectileVisualScale = 0.30f;
            _trailScaleMultiplier = 0.86f;
            _trailDuration = 0.15f;
            _spinDegreesPerSecond = 0f;
            _rotationOffsetDegrees = 0f;
            _orientToVelocity = true;

            switch (sourceTowerKind)
            {
                case TDTowerKind.RailLancer:
                    _projectileVisualScale = 0.30f;
                    _trailScaleMultiplier = 0.90f;
                    _trailDuration = 0.12f;
                    _trailInterval = 0.038f;
                    _projectileTint = new Color(0.92f, 0.97f, 1f, 1f);
                    _trailStartColor = new Color(0.86f, 0.95f, 1f, 0.58f);
                    _trailEndColor = new Color(0.56f, 0.76f, 1f, 0f);
                    _impactStartColor = new Color(0.88f, 0.97f, 1f, 0.95f);
                    _impactEndColor = new Color(0.52f, 0.74f, 1f, 0f);
                    _impactScale = 0.52f;
                    _impactDuration = 0.13f;
                    break;
                case TDTowerKind.CinderMortar:
                    _projectileVisualScale = 0.42f;
                    _trailScaleMultiplier = 0.72f;
                    _trailDuration = 0.21f;
                    _trailInterval = 0.05f;
                    _projectileTint = new Color(1f, 0.90f, 0.64f, 1f);
                    _trailStartColor = new Color(1f, 0.74f, 0.36f, 0.6f);
                    _trailEndColor = new Color(0.93f, 0.42f, 0.18f, 0f);
                    _impactStartColor = new Color(1f, 0.84f, 0.56f, 1f);
                    _impactEndColor = new Color(0.95f, 0.38f, 0.22f, 0f);
                    _impactScale = 0.7f;
                    _impactDuration = 0.18f;
                    break;
                case TDTowerKind.FrostCoil:
                    _projectileVisualScale = 0.30f;
                    _trailScaleMultiplier = 0.88f;
                    _trailDuration = 0.18f;
                    _trailInterval = 0.04f;
                    _projectileTint = new Color(0.86f, 1f, 1f, 1f);
                    _trailStartColor = new Color(0.78f, 0.98f, 1f, 0.62f);
                    _trailEndColor = new Color(0.52f, 0.88f, 1f, 0f);
                    _impactStartColor = new Color(0.86f, 0.99f, 1f, 0.95f);
                    _impactEndColor = new Color(0.46f, 0.82f, 1f, 0f);
                    _impactScale = 0.58f;
                    _impactDuration = 0.15f;
                    break;
                case TDTowerKind.ArcWelder:
                    _projectileVisualScale = 0.28f;
                    _trailScaleMultiplier = 0.82f;
                    _trailDuration = 0.13f;
                    _trailInterval = 0.036f;
                    _projectileTint = new Color(0.72f, 0.97f, 1f, 1f);
                    _trailStartColor = new Color(0.62f, 0.92f, 1f, 0.62f);
                    _trailEndColor = new Color(0.30f, 0.72f, 1f, 0f);
                    _impactStartColor = new Color(0.78f, 0.98f, 1f, 0.96f);
                    _impactEndColor = new Color(0.26f, 0.74f, 1f, 0f);
                    _impactScale = 0.60f;
                    _impactDuration = 0.15f;
                    break;
                case TDTowerKind.SiegeDrill:
                    _projectileVisualScale = 0.30f;
                    _trailScaleMultiplier = 0.88f;
                    _trailDuration = 0.16f;
                    _trailInterval = 0.05f;
                    _projectileTint = new Color(0.98f, 0.86f, 0.54f, 1f);
                    _trailStartColor = new Color(0.98f, 0.80f, 0.44f, 0.60f);
                    _trailEndColor = new Color(0.84f, 0.52f, 0.22f, 0f);
                    _impactStartColor = new Color(1f, 0.88f, 0.62f, 0.96f);
                    _impactEndColor = new Color(0.90f, 0.48f, 0.20f, 0f);
                    _impactScale = 0.66f;
                    _impactDuration = 0.18f;
                    break;
                case TDTowerKind.EmberFlak:
                    _projectileVisualScale = 0.29f;
                    _trailScaleMultiplier = 0.78f;
                    _trailDuration = 0.11f;
                    _trailInterval = 0.03f;
                    _projectileTint = new Color(1f, 0.78f, 0.50f, 1f);
                    _trailStartColor = new Color(1f, 0.70f, 0.40f, 0.58f);
                    _trailEndColor = new Color(0.96f, 0.34f, 0.16f, 0f);
                    _impactStartColor = new Color(1f, 0.86f, 0.56f, 0.96f);
                    _impactEndColor = new Color(1f, 0.34f, 0.12f, 0f);
                    _impactScale = 0.62f;
                    _impactDuration = 0.16f;
                    break;
                case TDTowerKind.ResonanceBeacon:
                    _projectileVisualScale = 0.30f;
                    _trailScaleMultiplier = 0.84f;
                    _trailDuration = 0.18f;
                    _orientToVelocity = false;
                    _spinDegreesPerSecond = 130f;
                    _trailInterval = 0.042f;
                    _projectileTint = new Color(0.78f, 1f, 0.84f, 1f);
                    _trailStartColor = new Color(0.70f, 0.98f, 0.78f, 0.60f);
                    _trailEndColor = new Color(0.36f, 0.82f, 0.56f, 0f);
                    _impactStartColor = new Color(0.86f, 1f, 0.90f, 0.95f);
                    _impactEndColor = new Color(0.32f, 0.78f, 0.52f, 0f);
                    _impactScale = 0.58f;
                    _impactDuration = 0.15f;
                    break;
                case TDTowerKind.GravSnare:
                    _projectileVisualScale = 0.30f;
                    _trailScaleMultiplier = 0.90f;
                    _trailDuration = 0.20f;
                    _orientToVelocity = false;
                    _spinDegreesPerSecond = -105f;
                    _trailInterval = 0.044f;
                    _projectileTint = new Color(0.80f, 0.86f, 1f, 1f);
                    _trailStartColor = new Color(0.72f, 0.82f, 1f, 0.60f);
                    _trailEndColor = new Color(0.34f, 0.46f, 0.96f, 0f);
                    _impactStartColor = new Color(0.86f, 0.90f, 1f, 0.95f);
                    _impactEndColor = new Color(0.34f, 0.42f, 0.92f, 0f);
                    _impactScale = 0.64f;
                    _impactDuration = 0.18f;
                    break;
                default:
                    _projectileVisualScale = 0.30f;
                    _trailInterval = 0.045f;
                    _projectileTint = new Color(0.96f, 0.94f, 0.76f, 1f);
                    _trailStartColor = new Color(0.92f, 0.92f, 0.75f, 0.56f);
                    _trailEndColor = new Color(0.86f, 0.84f, 0.34f, 0f);
                    _impactStartColor = new Color(1f, 0.96f, 0.78f, 0.9f);
                    _impactEndColor = new Color(1f, 0.74f, 0.2f, 0f);
                    _impactScale = 0.56f;
                    _impactDuration = 0.14f;
                    break;
            }

            _aoeStartColor = new Color(_impactStartColor.r, _impactStartColor.g, _impactStartColor.b, 0.68f);
            _aoeEndColor = new Color(_impactEndColor.r, _impactEndColor.g, _impactEndColor.b, 0f);
            _trailTimer = 0f;
            if (_renderer != null)
            {
                _renderer.color = Color.white;
            }
        }

        private void EmitTrailGhost()
        {
            if (_gameManager == null || _renderer == null || _renderer.sprite == null)
            {
                return;
            }

            _trailTimer += Time.deltaTime;
            if (_trailTimer < _trailInterval)
            {
                return;
            }

            _trailTimer -= _trailInterval;

            var (ghost, fx, ghostRenderer) = TDObjectPool.GetFxObject(
                _gameManager.transform, transform.position, "Fx_ProjectileTrail");

            ghostRenderer.sortingOrder = _renderer.sortingOrder - 1;
            ghostRenderer.sprite = _renderer.sprite;
            ghostRenderer.color = _trailStartColor;
            ghost.transform.rotation = transform.rotation;

            var startScale = transform.localScale * _trailScaleMultiplier;
            var endScale = transform.localScale * (_trailScaleMultiplier * 0.42f);
            fx.Configure(_trailDuration, startScale, endScale, _trailStartColor, _trailEndColor);
        }

        private void SpawnImpactSpark(Vector3 impactPoint, bool isAoe)
        {
            if (_gameManager == null)
            {
                return;
            }

            var (spark, fx, sparkRenderer) = TDObjectPool.GetFxObject(
                _gameManager.transform, impactPoint, "Fx_ImpactSpark");

            spark.transform.rotation = transform.rotation;
            sparkRenderer.sortingOrder = TDWorldVisualOrder.ProjectileFx;
            sparkRenderer.sprite = TDArtLibrary.LoadSpriteOrFallback(_impactSpritePath, _impactStartColor);

            var startScaleFactor = isAoe ? _impactScale * 0.7f : _impactScale * 0.55f;
            var endScaleFactor = isAoe ? _impactScale * 1.9f : _impactScale * 1.5f;
            var duration = isAoe ? _impactDuration + 0.05f : _impactDuration;
            fx.Configure(
                duration,
                Vector3.one * startScaleFactor,
                Vector3.one * endScaleFactor,
                _impactStartColor,
                _impactEndColor);
        }

        private void SpawnAoeIndicator(Vector3 impactPoint, float radius)
        {
            if (_gameManager == null || radius <= 0f)
            {
                return;
            }

            var (fx, ringFx, renderer) = TDObjectPool.GetFxObject(
                _gameManager.transform, impactPoint, "Fx_AoeIndicator");

            renderer.sortingOrder = TDWorldVisualOrder.ProjectileBack;
            renderer.sprite = TDArtLibrary.GetSoftRingSprite();

            var startScale = Vector3.one * Mathf.Max(0.20f, radius * 0.34f);
            var endScale = Vector3.one * Mathf.Max(0.55f, radius * 2f);
            ringFx.Configure(
                0.24f,
                startScale,
                endScale,
                _aoeStartColor,
                _aoeEndColor);
        }

        private void UpdateProjectileRotation(Vector3 direction)
        {
            if (_orientToVelocity && direction.sqrMagnitude > 0.0001f)
            {
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle + _rotationOffsetDegrees);
                return;
            }

            if (Mathf.Abs(_spinDegreesPerSecond) > 0.01f)
            {
                transform.Rotate(0f, 0f, _spinDegreesPerSecond * Time.deltaTime);
            }
        }

        private void SpawnSpecialistPulse(
            Vector3 impactPoint,
            float startDiameter,
            float endDiameter,
            Color startColor,
            Color endColor,
            string objectName,
            int sortingOrder)
        {
            if (_gameManager == null || endDiameter <= 0f)
            {
                return;
            }

            var (fx, ringFx, renderer) = TDObjectPool.GetFxObject(
                _gameManager.transform, impactPoint, objectName);

            renderer.sortingOrder = sortingOrder;
            renderer.sprite = TDArtLibrary.GetSoftRingSprite();
            renderer.color = startColor;

            ringFx.Configure(
                0.24f,
                Vector3.one * Mathf.Max(0.08f, startDiameter),
                Vector3.one * Mathf.Max(startDiameter, endDiameter),
                startColor,
                endColor);
        }

        private static string ResolveTowerVisualSlug(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => "rail_lancer",
                TDTowerKind.CinderMortar => "cinder_mortar",
                TDTowerKind.FrostCoil => "frost_coil",
                TDTowerKind.ArcWelder => "arc_welder",
                TDTowerKind.SiegeDrill => "siege_drill",
                TDTowerKind.EmberFlak => "ember_flak",
                TDTowerKind.ResonanceBeacon => "resonance_beacon",
                TDTowerKind.GravSnare => "grav_snare",
                TDTowerKind.SlagBurner => "slag_burner",
                TDTowerKind.SalvageDerrick => "salvage_derrick",
                TDTowerKind.RailBarricade => "rail_barricade",
                TDTowerKind.LongRailCannon => "long_rail_cannon",
                _ => "rail_lancer"
            };
        }
    }
}
