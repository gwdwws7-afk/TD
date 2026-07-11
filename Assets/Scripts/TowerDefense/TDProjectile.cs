using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public sealed class TDProjectile : MonoBehaviour
    {
        private TDGameManager _gameManager;
        private TDEnemy _target;
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
        private Color _projectileTint;
        private Color _trailStartColor;
        private Color _trailEndColor;
        private Color _impactStartColor;
        private Color _impactEndColor;
        private float _impactScale;
        private float _impactDuration;
        private const string AoeRingSpritePath = "Art/build_marker";
        private const string SparkSpritePath = "Art/projectile_bolt";
        private const float ArcChainSearchRadiusMin = 1.15f;
        private const float ArcChainSearchRadiusScale = 1.22f;
        private const int ArcChainCandidateBonus = 3;
        private const int ArcChainCandidateMin = 3;
        private const int ArcChainCandidateMax = 9;
        private const int ArcChainCountMin = 2;
        private const int ArcChainCountMax = 5;
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
        private const float DamageSpecThreatBonus = 1.12f;
        private const float DamageSpecExecuteThreshold = 0.40f;
        private const float DamageSpecExecuteBonus = 1.10f;
        private const float UtilitySpecFieldRadiusMin = 0.85f;
        private const float UtilitySpecFieldRadiusScale = 0.72f;
        private const int UtilitySpecFieldMaxTargets = 4;
        private const float UtilitySpecExposeDuration = 0.68f;
        private const float UtilitySpecExposeMultiplier = 1.035f;
        private const float UtilitySpecStaggerDuration = 0.11f;
        private const float UtilitySpecStaggerMinSpeed = 0.48f;
        private const float ArcLinkBaseDuration = 0.11f;
        private const float ArcLinkDurationStep = 0.01f;
        private const float ArcLinkStartWidth = 0.19f;
        private const float ArcLinkEndWidth = 0.05f;
        private const float ArcLinkVerticalLift = 0.06f;
        private const float GravityBoundaryDuration = 0.46f;
        private const float GravityBoundaryInnerDuration = 0.33f;
        private static readonly Color ArcLinkStartColor = new(0.70f, 0.94f, 1f, 0.90f);
        private static readonly Color ArcLinkEndColor = new(0.34f, 0.76f, 1f, 0f);
        private static readonly Color GravityBoundaryOuterColor = new(0.54f, 0.62f, 1f, 0.75f);
        private static readonly Color GravityBoundaryInnerColor = new(0.78f, 0.84f, 1f, 0.68f);
        private static readonly Color DamageSpecPulseStartColor = new(1f, 0.88f, 0.36f, 0.86f);
        private static readonly Color DamageSpecPulseEndColor = new(1f, 0.42f, 0.10f, 0f);
        private static readonly Color UtilitySpecFieldStartColor = new(0.36f, 1f, 0.78f, 0.66f);
        private static readonly Color UtilitySpecFieldEndColor = new(0.16f, 0.78f, 0.98f, 0f);

        public void Initialize(
            TDGameManager gameManager,
            TDEnemy target,
            TDTowerKind sourceTowerKind,
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
            _sourceTowerKind = sourceTowerKind;
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

            ConfigureVisualProfile(sourceTowerKind);
        }

        private void Update()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            var toTarget = _target.transform.position - transform.position;
            var step = _speed * Time.deltaTime;
            if (toTarget.sqrMagnitude <= step * step)
            {
                ResolveHit(_target.transform.position);
                Destroy(gameObject);
                return;
            }

            transform.position += toTarget.normalized * step;
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
                ? _gameManager.GetModifiedDamageForEnemy(_sourceTowerKind, enemy, rawDamage)
                : rawDamage;
            var damageTaken = enemy.TakeHit(modifiedDamage, slowPct, slowDuration);
            _gameManager?.NotifyEnemyDamaged(_sourceTowerKind, enemy, damageTaken, slowPct > 0f);
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
            }

            if (_utilitySpecialist && isPrimaryImpact)
            {
                ApplyUtilitySpecialistField(impactPoint, enemy);
            }
        }

        private int ApplyDamageSpecialistBonus(TDEnemy enemy, int rawDamage)
        {
            if (!_damageSpecialist || enemy == null)
            {
                return rawDamage;
            }

            var multiplier = 1f;
            if (enemy.HasAnyTag("armored", "heavy", "boss", "final", "elite", "fast", "flank", "support", "attrition", "special"))
            {
                multiplier *= DamageSpecThreatBonus;
            }

            if (enemy.HealthRatio <= DamageSpecExecuteThreshold)
            {
                multiplier *= DamageSpecExecuteBonus;
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
                    22);
                _gameManager?.NotifySpecializationEffect(_sourceTowerKind, false);
            }

            return Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier));
        }

        private void ApplyUtilitySpecialistField(Vector3 impactPoint, TDEnemy primaryTarget)
        {
            if (_gameManager == null)
            {
                return;
            }

            var radius = Mathf.Max(UtilitySpecFieldRadiusMin, _aoeRadius * UtilitySpecFieldRadiusScale);
            SpawnSpecialistPulse(
                impactPoint,
                Mathf.Max(0.48f, radius * 0.42f),
                Mathf.Max(0.92f, radius * 2.18f),
                UtilitySpecFieldStartColor,
                UtilitySpecFieldEndColor,
                "Fx_UtilitySpecField",
                20);
            _gameManager.NotifySpecializationEffect(_sourceTowerKind, true);

            var targets = _gameManager.GetEnemiesInRange(impactPoint, radius, UtilitySpecFieldMaxTargets);
            for (var i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.ApplyExposed(UtilitySpecExposeDuration, UtilitySpecExposeMultiplier);
                if (enemy != primaryTarget && enemy.HasAnyTag("fast", "flank", "special", "swarm"))
                {
                    enemy.ApplyStagger(UtilitySpecStaggerDuration, UtilitySpecStaggerMinSpeed);
                }
            }
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
                Mathf.Clamp(_aoeMaxTargets + ArcChainCandidateBonus, ArcChainCandidateMin, ArcChainCandidateMax));
            if (candidates.Count == 0)
            {
                return;
            }

            var chained = 0;
            var maxChains = Mathf.Clamp(_aoeMaxTargets, ArcChainCountMin, ArcChainCountMax);
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
                var chainDamageScale = ArcChainDamageBaseScale * Mathf.Pow(ArcChainDamageDecayScale, chained - 1);
                var chainDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * chainDamageScale));
                var damageTaken = ApplyDamage(target, chainDamage, 0f, 0f);
                if (damageTaken > 0)
                {
                    target.ApplyExposed(ArcChainExposeDuration, ArcChainExposeMultiplier);
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

            var splashRadius = Mathf.Max(EmberSplashRadiusMin, _aoeRadius * EmberSplashRadiusScale);
            var targets = _gameManager.GetEnemiesInRange(impactPoint, splashRadius, EmberSplashMaxTargets);
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
                    enemy.ApplyStagger(EmberSplashStaggerDuration, EmberSplashStaggerMinSpeed);
                }
            }
        }

        private void ApplyBeaconPulse(Vector3 impactPoint, TDEnemy primaryTarget)
        {
            if (_gameManager == null)
            {
                return;
            }

            var pulseRadius = Mathf.Max(BeaconPulseRadiusMin, _aoeRadius * BeaconPulseRadiusScale);
            var targets = _gameManager.GetEnemiesInRange(impactPoint, pulseRadius, BeaconPulseMaxTargets);
            for (var i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (enemy == null || enemy == primaryTarget)
                {
                    continue;
                }

                enemy.SetResonanceMark(BeaconPulseMarkDuration);
                enemy.ApplyExposed(BeaconPulseExposeDuration, BeaconPulseExposeMultiplier);
            }
        }

        private void ApplyGravityWell(Vector3 impactPoint, TDEnemy primaryTarget)
        {
            if (_gameManager == null)
            {
                return;
            }

            var pulseRadius = Mathf.Max(GravPulseRadiusMin, _aoeRadius * GravPulseRadiusScale);
            SpawnGravityBoundary(impactPoint, pulseRadius);
            var targets = _gameManager.GetEnemiesInRange(impactPoint, pulseRadius, Mathf.Max(_aoeMaxTargets, GravPulseMinTargets));
            for (var i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (enemy == null || enemy == primaryTarget)
                {
                    continue;
                }

                enemy.ApplyStagger(GravPulseStaggerDuration, GravPulseStaggerMinSpeed);
                enemy.ApplyExposed(GravPulseExposeDuration, GravPulseExposeMultiplier);
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
                23);
        }

        private void SpawnGravityBoundary(Vector3 impactPoint, float radius)
        {
            if (_gameManager == null || radius <= 0f)
            {
                return;
            }

            var outer = new GameObject("Fx_GravityBoundary");
            outer.transform.SetParent(_gameManager.transform, true);
            outer.transform.position = impactPoint;

            var outerRenderer = outer.AddComponent<SpriteRenderer>();
            outerRenderer.sortingOrder = 18;
            outerRenderer.sprite = TDArtLibrary.LoadSpriteOrFallback(AoeRingSpritePath, GravityBoundaryOuterColor);
            outerRenderer.color = GravityBoundaryOuterColor;

            var outerFx = outer.AddComponent<TDTransientSpriteFx>();
            var outerStartScale = Vector3.one * Mathf.Max(0.34f, radius * 1.28f);
            var outerEndScale = Vector3.one * Mathf.Max(0.66f, radius * 2.72f);
            outerFx.Configure(
                GravityBoundaryDuration,
                outerStartScale,
                outerEndScale,
                GravityBoundaryOuterColor,
                new Color(GravityBoundaryOuterColor.r, GravityBoundaryOuterColor.g, GravityBoundaryOuterColor.b, 0f));

            var inner = new GameObject("Fx_GravityBoundaryCore");
            inner.transform.SetParent(_gameManager.transform, true);
            inner.transform.position = impactPoint;

            var innerRenderer = inner.AddComponent<SpriteRenderer>();
            innerRenderer.sortingOrder = 19;
            innerRenderer.sprite = TDArtLibrary.LoadSpriteOrFallback(AoeRingSpritePath, GravityBoundaryInnerColor);
            innerRenderer.color = GravityBoundaryInnerColor;

            var innerFx = inner.AddComponent<TDTransientSpriteFx>();
            var innerStartScale = Vector3.one * Mathf.Max(0.24f, radius * 0.90f);
            var innerEndScale = Vector3.one * Mathf.Max(0.54f, radius * 2.10f);
            innerFx.Configure(
                GravityBoundaryInnerDuration,
                innerStartScale,
                innerEndScale,
                GravityBoundaryInnerColor,
                new Color(GravityBoundaryInnerColor.r, GravityBoundaryInnerColor.g, GravityBoundaryInnerColor.b, 0f));
        }

        private void ConfigureVisualProfile(TDTowerKind sourceTowerKind)
        {
            switch (sourceTowerKind)
            {
                case TDTowerKind.RailLancer:
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

            _trailTimer = 0f;
            if (_renderer != null)
            {
                _renderer.color = _projectileTint;
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

            var ghost = new GameObject("Fx_ProjectileTrail");
            ghost.transform.SetParent(_gameManager.transform, true);
            ghost.transform.position = transform.position;

            var ghostRenderer = ghost.AddComponent<SpriteRenderer>();
            ghostRenderer.sortingOrder = _renderer.sortingOrder - 1;
            ghostRenderer.sprite = _renderer.sprite;
            ghostRenderer.color = _trailStartColor;

            var fx = ghost.AddComponent<TDTransientSpriteFx>();
            var startScale = transform.localScale * 0.92f;
            var endScale = transform.localScale * 0.45f;
            fx.Configure(0.17f, startScale, endScale, _trailStartColor, _trailEndColor);
        }

        private void SpawnImpactSpark(Vector3 impactPoint, bool isAoe)
        {
            if (_gameManager == null)
            {
                return;
            }

            var spark = new GameObject("Fx_ImpactSpark");
            spark.transform.SetParent(_gameManager.transform, true);
            spark.transform.position = impactPoint;

            var sparkRenderer = spark.AddComponent<SpriteRenderer>();
            sparkRenderer.sortingOrder = 21;
            sparkRenderer.sprite = TDArtLibrary.LoadSpriteOrFallback(SparkSpritePath, _impactStartColor);

            var fx = spark.AddComponent<TDTransientSpriteFx>();
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

            var fx = new GameObject("Fx_AoeIndicator");
            fx.transform.SetParent(_gameManager.transform, true);
            fx.transform.position = impactPoint;

            var renderer = fx.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 19;
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback(AoeRingSpritePath, new Color(0.49f, 0.78f, 0.94f));

            var ringFx = fx.AddComponent<TDTransientSpriteFx>();
            var startScale = Vector3.one * 0.28f;
            var endScale = Vector3.one * Mathf.Max(0.55f, radius * 2.2f);
            ringFx.Configure(
                0.24f,
                startScale,
                endScale,
                new Color(0.65f, 0.90f, 1f, 0.80f),
                new Color(0.65f, 0.90f, 1f, 0f));
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

            var fx = new GameObject(objectName);
            fx.transform.SetParent(_gameManager.transform, true);
            fx.transform.position = impactPoint;

            var renderer = fx.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.sprite = TDArtLibrary.GetSoftRingSprite();
            renderer.color = startColor;

            var ringFx = fx.AddComponent<TDTransientSpriteFx>();
            ringFx.Configure(
                0.24f,
                Vector3.one * Mathf.Max(0.08f, startDiameter),
                Vector3.one * Mathf.Max(startDiameter, endDiameter),
                startColor,
                endColor);
        }
    }
}
