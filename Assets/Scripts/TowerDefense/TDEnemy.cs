using System;
using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public sealed class TDEnemy : MonoBehaviour
    {
        private static readonly Dictionary<string, bool> FxPrefixAvailability = new();
        private static readonly Color SlowTint = new(0.64f, 0.89f, 1f, 1f);
        private static readonly Color ResonanceMarkTint = new(1f, 0.66f, 0.24f, 1f);
        private const float HitFlashDuration = 0.10f;
        private const float DeathFadeDuration = 0.22f;
        // Per-enemy death reel (spec: enemy-death-frames-spec-v1): 4 frames
        // standard, bosses may ship 6; the animator collects whatever exists.
        private const int DeathReelMaxFrames = 6;
        private const float DeathReelFps = 12f;
        private const float DeathReelHoldMaxSeconds = 0.75f;
        private const float HitFxMinInterval = 0.06f;
        private const float FastEvadeSpeedThreshold = 2.2f;
        private const string HitFxPrefix = "Art/anim/fx_enemy_hit";
        private const int HitFxFrameCount = 6;
        private const float HitFxFps = 24f;
        private const string DeathFxPrefix = "Art/anim/fx_enemy_death";
        private const int DeathFxFrameCount = 8;
        private const float DeathFxFps = 16f;
        private const string BossWarningFxPrefix = "Art/anim/fx_boss_warning";
        private const int BossWarningFxFrameCount = 10;
        private const float BossWarningFxFps = 12f;
        private const string BurrowAmbushFxPrefix = "Art/anim/fx_burrow_ambush";
        private const int BurrowAmbushFxFrameCount = 8;
        private const float BurrowAmbushFxFps = 12f;
        private const string SporeSplitWarningFxPrefix = "Art/anim/fx_spore_split_warning";
        private const int SporeSplitWarningFxFrameCount = 8;
        private const float SporeSplitWarningFxFps = 11f;
        private const string MimicShiftFxPrefix = "Art/anim/fx_mimic_shift";
        private const int MimicShiftFxFrameCount = 8;
        private const float MimicShiftFxFps = 12f;
        private const float SporeSplitWarningHealthThreshold = 0.45f;
        private const string AttritionSiphonFxPrefix = "Art/anim/fx_attrition_siphon";
        private const int AttritionSiphonFxFrameCount = 8;
        private const float AttritionSiphonFxFps = 10f;
        private const string SupportLinkFxPrefix = "Art/anim/fx_support_link";
        private const int SupportLinkFxFrameCount = 8;
        private const float SupportLinkFxFps = 11f;
        private const string ElitePressureFxPrefix = "Art/anim/fx_elite_pressure";
        private const int ElitePressureFxFrameCount = 10;
        private const float ElitePressureFxFps = 12f;
        private const float ElitePressureHealthThreshold = 0.55f;
        private const float AttritionSiphonPulseInterval = 2.3f;
        private const float SupportLinkPulseInterval = 1.6f;
        private const float SupportLinkCheckRadius = 1.9f;

        private TDGameManager _gameManager;
        private IReadOnlyList<Vector3> _path;
        private int _nextWaypointIndex;
        private int _lastReportedRoadSegment = -1;
        private float _baseSpeed;
        private float _slowPct;
        private float _slowTimer;
        private float _staggerTimer;
        private float _staggerMinSpeedMultiplier;
        private int _armorBreakFlat;
        private float _armorBreakTimer;
        private float _exposedTimer;
        private float _exposedMultiplier = 1f;
        // Slag Burner DoT slot — independent of slow/expose by design note
        // (expansion tower 9). Zero for every fight without a Slag Burner.
        private int _burnLayers;
        private float _burnDamagePerLayer;
        private float _burnTimer;
        private float _burnTickAccumulator;
        private TDTower _burnSourceTower;
        // Rail Barricade engagement state (expansion tower 11). The enemy
        // either fights the wagon (_engagedWagon), stands in line
        // (_queuedWagon), or moves freely — both null.
        private TDBlockerWagon _engagedWagon;
        private TDBlockerWagon _queuedWagon;
        private float _wagonAttackTimer;
        // Forge Dragoon shield layer (expansion batch 2): the first 3 hits
        // each wave are immune — multi-hit tools burn the charges fast, single
        // snipers waste shots on it.
        private int _shieldHitsRemaining;
        private int _shieldWave = -1;
        private int _hp;
        private int _maxHp;
        private int _armorFlat;
        private int _reward;
        private int _lineDamage;
        private string _enemyId;
        private string _laneKey;
        private List<string> _tags;
        private bool _resolved;
        private bool _dying;
        private float _hitFlashTimer;
        private float _resonanceMarkTimer;
        private float _deathFadeTimer;
        private float _specialSpeedMultiplier;
        private float _specialBurstTimer;
        private float _scenarioSpeedMultiplier = 1f;
        private float _scenarioSpeedTimer;
        private bool _specialBurstUsed;
        private Color _variantTint;
        private Vector3 _deathStartScale;
        private BoxCollider2D _bodyCollider;
        private SpriteRenderer _visualRenderer;
        private SpriteRenderer _shadowRenderer;
        private Transform _visualRoot;
        private TDSpriteAnimator _bodyAnimator;
        private bool _bodyDeathReelPlaying;
        private float _deathReelHoldTimer;
        private TDEnemyReadability _readability;
        private float _hitFxTimer;
        private bool _bossWarningFxPlayed;
        private bool _burrowAmbushFxPlayed;
        private bool _sporeSplitWarningFxPlayed;
        private bool _mimicShiftFxPlayed;
        private bool _elitePressureFxPlayed;
        private int _mimicVariantIndex = -1;
        private float _attritionSiphonFxTimer;
        private float _supportLinkFxTimer;
        private Vector3 _visualBaseLocalPosition;
        private Vector3 _visualBaseLocalScale = Vector3.one;
        private float _motionPhase;
        private float _hitReactionTimer;
        private int _hitReactionCount;
        private Vector2 _smoothedMovementDirection = Vector2.right;
        private float _turnPose;
        private int _facingSign = 1;
        private float _maximumRouteDeviationObserved;

        public string EnemyId => _enemyId;
        /// <summary>Alive and attackable — false once the enemy is killed
        /// (death reel + fade window) or escaping, before the GameObject is
        /// actually destroyed. Towers filter cached/windup targets on this
        /// so they stop firing at corpses (review P1).</summary>
        public bool IsTargetable => !_resolved && !_dying;
        public string LaneKey => string.IsNullOrWhiteSpace(_laneKey) ? "default" : _laneKey;
        public int MaxHealth => _maxHp;
        public int ArmorFlat => _armorFlat;
        public bool IsMarked => _resonanceMarkTimer > 0f;
        public bool IsSlowed => _slowTimer > 0f && _slowPct > 0f;
        public bool IsBurning => _burnLayers > 0 && _burnTimer > 0f;
        public TDBlockerWagon EngagedWagon => _engagedWagon;
        public TDBlockerWagon QueuedWagon => _queuedWagon;
        public int LineDamage => _lineDamage;
        public int BurnLayers => _burnLayers;
        public float BurnDamagePerLayer => _burnDamagePerLayer;
        public bool IsStaggered => _staggerTimer > 0f;
        public bool IsArmorBroken => _armorBreakTimer > 0f && _armorBreakFlat > 0;
        public bool IsExposed => _exposedTimer > 0f && _exposedMultiplier > 1f;
        public float HealthRatio => _maxHp <= 0 ? 1f : Mathf.Clamp01(_hp / (float)_maxHp);
        public float RouteProgress01 => CalculateRouteProgress01();
        public float RouteDeviationWorld => CalculateRouteDeviationWorld();
        public float GroundContactRouteDeviationWorld => CalculateRouteDeviationWorld(ResolveGroundContactWorldPosition());
        public TDEnemyReadability Readability => _readability;
        public bool MotionReady => _visualRoot != null;
        public int HitReactionCount => _hitReactionCount;
        public float MaximumRouteDeviationObserved => _maximumRouteDeviationObserved;
        public float TurnPoseDegrees => _visualRoot != null ? Mathf.DeltaAngle(0f, _visualRoot.localEulerAngles.z) : 0f;
        public int FacingSign => _facingSign;
        public float ShadowAspectRatio => _shadowRenderer != null
            ? Mathf.Abs(_shadowRenderer.transform.localScale.y) /
              Mathf.Max(0.0001f, Mathf.Abs(_shadowRenderer.transform.localScale.x))
            : 1f;
        public float FootShadowGapWorld
        {
            get
            {
                if (_visualRenderer == null || _visualRenderer.sprite == null || _shadowRenderer == null)
                {
                    return float.MaxValue;
                }

                // Compare the OPAQUE pixel bottom (not the FullRect bounds,
                // which include transparent padding) against the shadow.
                var visualTransform = _visualRenderer.transform;
                var bottomPadding = TDArtLibrary.GetFootAnchorPadding01(_visualRenderer.sprite.name).x;
                var opaqueBottom = visualTransform.localPosition.y +
                                   ((_visualRenderer.sprite.bounds.min.y +
                                     (bottomPadding * _visualRenderer.sprite.bounds.size.y)) *
                                    Mathf.Abs(visualTransform.localScale.y));
                return Mathf.Abs(_shadowRenderer.transform.localPosition.y - opaqueBottom);
            }
        }
        public bool FootShadowAligned => FootShadowGapWorld >= 0.018f &&
                                         FootShadowGapWorld <= 0.11f &&
                                         ShadowAspectRatio >= 0.30f &&
                                         ShadowAspectRatio <= 0.52f;

        /// <summary>
        /// World position of the visual's opaque-pixel bottom (the feet).
        /// Used for ground-contact route deviation audits; the shadow blob
        /// intentionally sits slightly below this point.
        /// </summary>
        private Vector3 ResolveGroundContactWorldPosition()
        {
            if (_visualRenderer == null || _visualRenderer.sprite == null)
            {
                return _shadowRenderer != null ? _shadowRenderer.transform.position : transform.position;
            }

            var visualTransform = _visualRenderer.transform;
            var bottomPadding = TDArtLibrary.GetFootAnchorPadding01(_visualRenderer.sprite.name).x;
            var opaqueBottomLocal = _visualRenderer.sprite.bounds.min.y +
                                    (bottomPadding * _visualRenderer.sprite.bounds.size.y);
            return new Vector3(
                visualTransform.position.x,
                visualTransform.position.y + (opaqueBottomLocal * Mathf.Abs(visualTransform.localScale.y)),
                0f);
        }

        /// <summary>
        /// Called by the sprite animator after every frame swap so the feet
        /// stay planted on the route line even when frames carry different
        /// transparent padding.
        /// </summary>
        public void NotifyVisualFrameSwapped()
        {
            if (_visualRenderer == null || _visualRenderer.sprite == null || _visualRoot == null)
            {
                return;
            }

            var anchoredLocalY = TDArtLibrary.ResolveFootAnchorLocalY(
                _visualRenderer.sprite,
                _visualRoot.localScale.y);
            _visualBaseLocalPosition = new Vector3(
                _visualBaseLocalPosition.x,
                anchoredLocalY,
                _visualBaseLocalPosition.z);
            _visualRoot.localPosition = _visualBaseLocalPosition;
        }

        public void Initialize(TDGameManager gameManager, IReadOnlyList<Vector3> path, TDEnemyCatalogEntry entry, string laneKey = "default")
        {
            _gameManager = gameManager;
            _path = path ?? Array.Empty<Vector3>();
            _enemyId = entry.enemyId;
            _laneKey = string.IsNullOrWhiteSpace(laneKey) ? "default" : laneKey.Trim().ToLowerInvariant();
            _hp = entry.hp;
            _baseSpeed = entry.speed;
            _armorFlat = Mathf.Max(0, entry.armorFlat);
            _reward = Mathf.Max(0, entry.rewardGold);
            _lineDamage = Mathf.Max(1, entry.lineDamage);
            _tags = new List<string>(entry.tags ?? Array.Empty<string>());
            _nextWaypointIndex = 1;
            _lastReportedRoadSegment = -1;
            _bodyCollider = GetComponent<BoxCollider2D>();
            _visualRenderer = ResolveVisualRenderer();
            var shadow = transform.Find("Shadow");
            _shadowRenderer = shadow != null ? shadow.GetComponent<SpriteRenderer>() : null;
            _visualRoot = _visualRenderer != null ? _visualRenderer.transform : transform;
            _bodyAnimator = _visualRoot.GetComponent<TDSpriteAnimator>();
            _visualBaseLocalPosition = _visualRoot.localPosition;
            _visualBaseLocalScale = _visualRoot.localScale;
            _motionPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _hitReactionTimer = 0f;
            _hitReactionCount = 0;
            _smoothedMovementDirection = ResolveInitialMovementDirection(_path);
            _turnPose = 0f;
            _facingSign = _smoothedMovementDirection.x < -0.05f ? -1 : 1;
            _maximumRouteDeviationObserved = 0f;
            _hitFlashTimer = 0f;
            _deathFadeTimer = 0f;
            _dying = false;
            // Lifecycle flag MUST reset for pool reuse — a reused hierarchy
            // carried _resolved=true from its previous life and froze on the
            // track forever (untargetable, wave never cleared).
            _resolved = false;
            _bodyDeathReelPlaying = false;
            _deathReelHoldTimer = 0f;
            _specialSpeedMultiplier = 1f;
            _specialBurstTimer = 0f;
            _scenarioSpeedMultiplier = 1f;
            _scenarioSpeedTimer = 0f;
            _specialBurstUsed = false;
            _variantTint = Color.white;
            _staggerTimer = 0f;
            _staggerMinSpeedMultiplier = 1f;
            _armorBreakFlat = 0;
            _armorBreakTimer = 0f;
            _exposedTimer = 0f;
            _exposedMultiplier = 1f;
            _burnLayers = 0;
            _burnDamagePerLayer = 0f;
            _burnTimer = 0f;
            _burnTickAccumulator = 0f;
            _burnSourceTower = null;
            _engagedWagon = null;
            _queuedWagon = null;
            _wagonAttackTimer = 0f;
            _hitFxTimer = 0f;
            _bossWarningFxPlayed = false;
            _burrowAmbushFxPlayed = false;
            _sporeSplitWarningFxPlayed = false;
            _mimicShiftFxPlayed = false;
            _elitePressureFxPlayed = false;
            _mimicVariantIndex = -1;
            _attritionSiphonFxTimer = UnityEngine.Random.Range(0.45f, 0.95f);
            _supportLinkFxTimer = UnityEngine.Random.Range(0.35f, 0.75f);

            ApplyVariantProfileIfNeeded();
            _maxHp = Mathf.Max(1, _hp);
            _readability = GetComponent<TDEnemyReadability>();
            if (_readability == null)
            {
                _readability = gameObject.AddComponent<TDEnemyReadability>();
            }
            _readability.Initialize(this, _visualRenderer, entry.threatCost);
            if (HasTag("flank"))
            {
                _specialSpeedMultiplier *= 1.08f;
            }

            if (_path.Count > 0)
            {
                transform.position = _path[0];
            }

            RefreshDepthSorting();

            TryPlayBossWarningFx();
            TryPlayMimicShiftFx();
            ReportRoadSegmentProgress();
        }

        private void Update()
        {
            RefreshDepthSorting();
            if (_dying)
            {
                UpdateDeathFade();
                return;
            }

            if (_resolved || _path == null || _path.Count == 0)
            {
                return;
            }

            if (_hitFxTimer > 0f)
            {
                _hitFxTimer = Mathf.Max(0f, _hitFxTimer - Time.deltaTime);
            }

            if (_hitReactionTimer > 0f)
            {
                _hitReactionTimer = Mathf.Max(0f, _hitReactionTimer - Time.deltaTime);
            }

            TryUpdateAttritionSiphonFx();
            TryUpdateSupportLinkFx();
            TryPlayElitePressureFx();
            TryPlaySporeSplitWarningFx();

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer = Mathf.Max(0f, _hitFlashTimer - Time.deltaTime);
            }

            if (_resonanceMarkTimer > 0f)
            {
                _resonanceMarkTimer = Mathf.Max(0f, _resonanceMarkTimer - Time.deltaTime);
            }

            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f)
                {
                    _slowPct = 0f;
                    _slowTimer = 0f;
                }
            }

            if (_staggerTimer > 0f)
            {
                _staggerTimer = Mathf.Max(0f, _staggerTimer - Time.deltaTime);
                if (_staggerTimer <= 0f)
                {
                    _staggerMinSpeedMultiplier = 1f;
                }
            }

            if (_armorBreakTimer > 0f)
            {
                _armorBreakTimer = Mathf.Max(0f, _armorBreakTimer - Time.deltaTime);
                if (_armorBreakTimer <= 0f)
                {
                    _armorBreakFlat = 0;
                }
            }

            if (_exposedTimer > 0f)
            {
                _exposedTimer = Mathf.Max(0f, _exposedTimer - Time.deltaTime);
                if (_exposedTimer <= 0f)
                {
                    _exposedMultiplier = 1f;
                }
            }

            if (_burnLayers > 0)
            {
                _burnTimer -= Time.deltaTime;
                if (_burnTimer <= 0f)
                {
                    _burnLayers = 0;
                    _burnTickAccumulator = 0f;
                }
                else
                {
                    _burnTickAccumulator += Time.deltaTime;
                    if (_burnTickAccumulator >= TDBurnSystem.BurnTickInterval)
                    {
                        _burnTickAccumulator -= TDBurnSystem.BurnTickInterval;
                        TakeBurnTick();
                    }
                }
            }

            if (_scenarioSpeedTimer > 0f)
            {
                _scenarioSpeedTimer = Mathf.Max(0f, _scenarioSpeedTimer - Time.deltaTime);
                if (_scenarioSpeedTimer <= 0f)
                {
                    _scenarioSpeedMultiplier = 1f;
                }
            }

            UpdateSpecialMovementState();

            if (!UpdateWagonEngagement())
            {
                return;
            }

            if (_nextWaypointIndex >= _path.Count)
            {
                RefreshEnemyMotion(Vector3.zero, 0f);
                ReportRoadSegmentProgress();
                ResolveEscape();
                return;
            }

            var target = _path[_nextWaypointIndex];
            var delta = target - transform.position;
            var effectiveSpeed = _baseSpeed * _specialSpeedMultiplier * _scenarioSpeedMultiplier *
                                 ResolveCinderPileSpeedBonus() * ResolveSplitterSpeedMultiplier() *
                                 Mathf.Clamp01(1f - _slowPct);
            var minMoveFloor = 0.35f;
            if (_staggerTimer > 0f)
            {
                effectiveSpeed *= Mathf.Clamp(_staggerMinSpeedMultiplier, 0f, 1f);
                minMoveFloor = Mathf.Min(minMoveFloor, Mathf.Clamp(_staggerMinSpeedMultiplier, 0f, 1f));
            }

            effectiveSpeed = Mathf.Max(effectiveSpeed, _baseSpeed * minMoveFloor);
            var step = effectiveSpeed * Time.deltaTime;

            if (delta.sqrMagnitude <= step * step)
            {
                transform.position = target;
                _nextWaypointIndex++;
                RefreshEnemyMotion(delta.normalized, effectiveSpeed / Mathf.Max(0.01f, _baseSpeed));
                _maximumRouteDeviationObserved = Mathf.Max(_maximumRouteDeviationObserved, CalculateRouteDeviationWorld());
                ReportRoadSegmentProgress();
                UpdateVisualTint();
                return;
            }

            transform.position += delta.normalized * step;
            RefreshEnemyMotion(delta.normalized, effectiveSpeed / Mathf.Max(0.01f, _baseSpeed));
            _maximumRouteDeviationObserved = Mathf.Max(_maximumRouteDeviationObserved, CalculateRouteDeviationWorld());
            ReportRoadSegmentProgress();
            UpdateVisualTint();
        }

        private void RefreshEnemyMotion(Vector3 direction, float speedRatio)
        {
            if (_visualRoot == null || _visualRoot == transform)
            {
                return;
            }

            var speed = Mathf.Clamp(speedRatio, 0f, 1.8f);
            var stridePhase = (Time.time * Mathf.Lerp(5.5f, 9.5f, Mathf.Clamp01(speed))) + _motionPhase;
            var stride = Mathf.Sin(stridePhase);
            var lift = Mathf.Abs(stride) * Mathf.Clamp01(speed) * 0.012f;
            var hit = _hitReactionTimer <= 0f
                ? 0f
                : Mathf.Sin((_hitReactionTimer / HitFlashDuration) * Mathf.PI);
            var requestedDirection = new Vector2(direction.x, direction.y);
            if (requestedDirection.sqrMagnitude > 0.0001f)
            {
                requestedDirection.Normalize();
                var previousDirection = _smoothedMovementDirection;
                var blend = 1f - Mathf.Exp(-Time.deltaTime * 11f);
                _smoothedMovementDirection = Vector2.Lerp(previousDirection, requestedDirection, blend).normalized;
                var turnCross = (previousDirection.x * _smoothedMovementDirection.y) -
                                (previousDirection.y * _smoothedMovementDirection.x);
                _turnPose = Mathf.Lerp(_turnPose, Mathf.Clamp(turnCross * 18f, -1f, 1f), blend);
                if (Mathf.Abs(_smoothedMovementDirection.x) >= 0.08f)
                {
                    _facingSign = _smoothedMovementDirection.x < 0f ? -1 : 1;
                }
            }
            else
            {
                _turnPose = Mathf.MoveTowards(_turnPose, 0f, Time.deltaTime * 5f);
            }

            _visualRoot.localPosition = _visualBaseLocalPosition + new Vector3(0f, lift, 0f);
            _visualRoot.localScale = new Vector3(
                _visualBaseLocalScale.x * (1f + (hit * 0.07f) - (stride * 0.010f)),
                _visualBaseLocalScale.y * (1f - (hit * 0.09f) + (stride * 0.012f)),
                _visualBaseLocalScale.z);
            var travelLean = -_smoothedMovementDirection.x * 1.35f;
            var cornerLean = -_turnPose * 3.1f;
            _visualRoot.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Clamp(travelLean + cornerLean, -4.2f, 4.2f));
            if (_visualRenderer != null)
            {
                _visualRenderer.flipX = _facingSign < 0;
            }
        }

        private void ResetEnemyMotion()
        {
            if (_visualRoot == null || _visualRoot == transform)
            {
                return;
            }

            _visualRoot.localPosition = _visualBaseLocalPosition;
            _visualRoot.localScale = _visualBaseLocalScale;
            _visualRoot.localRotation = Quaternion.identity;
        }

        private static Vector2 ResolveInitialMovementDirection(IReadOnlyList<Vector3> path)
        {
            if (path == null)
            {
                return Vector2.right;
            }

            for (var i = 0; i < path.Count - 1; i++)
            {
                var direction = (Vector2)(path[i + 1] - path[i]);
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized;
                }
            }

            return Vector2.right;
        }

        private void RefreshDepthSorting()
        {
            var bodyOrder = TDWorldVisualOrder.ResolveBodyOrder(transform.position.y);
            if (_visualRenderer != null)
            {
                _visualRenderer.sortingOrder = bodyOrder;
            }

            if (_shadowRenderer != null)
            {
                _shadowRenderer.sortingOrder = bodyOrder - 3;
            }
        }

        public int GetRoadSegmentIndex(int segmentCount)
        {
            var safeCount = Mathf.Max(1, segmentCount);
            return Mathf.Clamp(Mathf.FloorToInt(RouteProgress01 * safeCount), 0, safeCount - 1);
        }

        private float CalculateRouteProgress01()
        {
            if (_path == null || _path.Count <= 1)
            {
                return 0f;
            }

            if (_nextWaypointIndex >= _path.Count)
            {
                return 1f;
            }

            var previousIndex = Mathf.Clamp(_nextWaypointIndex - 1, 0, _path.Count - 2);
            var start = _path[previousIndex];
            var end = _path[previousIndex + 1];
            var segment = end - start;
            var localProgress = segment.sqrMagnitude <= 0.0001f
                ? 1f
                : Mathf.Clamp01(Vector3.Dot(transform.position - start, segment) / segment.sqrMagnitude);
            return Mathf.Clamp01((previousIndex + localProgress) / Mathf.Max(1f, _path.Count - 1f));
        }

        private float CalculateRouteDeviationWorld()
        {
            return CalculateRouteDeviationWorld(transform.position);
        }

        private float CalculateRouteDeviationWorld(Vector3 worldPoint)
        {
            if (_path == null || _path.Count == 0)
            {
                return 0f;
            }

            var point = (Vector2)worldPoint;
            var closest = float.MaxValue;
            for (var i = 0; i < _path.Count - 1; i++)
            {
                var start = (Vector2)_path[i];
                var end = (Vector2)_path[i + 1];
                var segment = end - start;
                var progress = segment.sqrMagnitude <= 0.000001f
                    ? 0f
                    : Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
                closest = Mathf.Min(closest, Vector2.Distance(point, start + (segment * progress)));
            }

            return closest == float.MaxValue ? Vector2.Distance(point, _path[0]) : closest;
        }

        private void ReportRoadSegmentProgress()
        {
            if (_gameManager == null || _resolved)
            {
                return;
            }

            var currentSegment = GetRoadSegmentIndex(TDGameManager.RoadSegmentCount);
            if (currentSegment <= _lastReportedRoadSegment)
            {
                return;
            }

            for (var segment = _lastReportedRoadSegment + 1; segment <= currentSegment; segment++)
            {
                _gameManager.NotifyEnemyReachedRoadSegment(this, segment);
            }

            _lastReportedRoadSegment = currentSegment;
        }

        public bool HasTag(string tag)
        {
            if (_tags == null)
            {
                return false;
            }

            for (var i = 0; i < _tags.Count; i++)
            {
                if (_tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAnyTag(params string[] tags)
        {
            if (tags == null)
            {
                return false;
            }

            for (var i = 0; i < tags.Length; i++)
            {
                if (HasTag(tags[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public int TakeHit(int rawDamage, float slowPct, float slowDuration, TDTower sourceTower = null)
        {
            if (_resolved || rawDamage <= 0)
            {
                return 0;
            }

            // Fast enemies (speed >= 2.2) have a chance to evade fire from slow-firing
            // towers (shotsPerSecond <= 1.0). Slowed enemies lose this evasion entirely,
            // making Frost Coil / Grav Snare a hard requirement for Burrow Sapper and
            // Cinder Glider. High fire-rate towers (Ember Flak 1.35/s, Arc Welder 0.85/s
            // but chain) also bypass this via their own flag on the source tower.
            // Ember Strider: marked prey cannot dodge and pays for it.
            var striderMarked = string.Equals(_enemyId, "ember_strider", StringComparison.Ordinal) && IsMarked;

            if (sourceTower != null && !sourceTower.IgnoresFastEvade && !striderMarked &&
                !IsSlowed && _baseSpeed >= FastEvadeSpeedThreshold)
            {
                var evadeChance = sourceTower.EvadeableFastEnemyMissChance;
                if (evadeChance > 0f && UnityEngine.Random.value < evadeChance)
                {
                    return 0;
                }
            }

            // Forge Dragoon: shield layer absorbs the wave's first 3 hits.
            if (AbsorbHitWithShield())
            {
                _hitFxTimer = 0.10f;
                return 0;
            }

            var wasSlowed = IsSlowed;
            var damageWithExposure = Mathf.RoundToInt(rawDamage * Mathf.Max(1f, _exposedMultiplier));
            if (striderMarked)
            {
                damageWithExposure = Mathf.RoundToInt(damageWithExposure * 1.25f);
            }
            var effectiveArmor = Mathf.Max(0, _armorFlat - _armorBreakFlat);
            // Hybrid armor model — see TDCombatMath.ResolveArmoredDamage
            // (flat + percentage mitigation, floor of 1).
            var damageTaken = TDCombatMath.ResolveArmoredDamage(damageWithExposure, effectiveArmor);
            var appliedDamage = Mathf.Min(Mathf.Max(0, _hp), damageTaken);
            _hitFlashTimer = HitFlashDuration;
            _hitReactionTimer = HitFlashDuration;
            _hitReactionCount++;
            _hp = Mathf.Max(0, _hp - damageTaken);
            if (_hp <= 0)
            {
                ResolveKill(sourceTower);
                return appliedDamage;
            }

            TryPlayHitFx(sourceTower);

            if (slowPct > 0f && slowDuration > 0f)
            {
                var appliedSlow = slowPct;
                if (HasTag("flank"))
                {
                    appliedSlow *= 0.65f;
                }

                if (HasTag("boss"))
                {
                    appliedSlow *= 0.55f;
                }

                _slowPct = Mathf.Clamp(Mathf.Max(_slowPct, appliedSlow), 0f, 0.9f);
                _slowTimer = Mathf.Max(_slowTimer, slowDuration);
                if (!wasSlowed && IsSlowed)
                {
                    _gameManager?.NotifyEnemySlowed(this, _slowPct);
                }
            }

            return appliedDamage;
        }

        /// <summary>
        /// Rail Barricade engagement tick (expansion tower 11). Returns false
        /// when the enemy spent the frame fighting or queuing at a wagon —
        /// callers must not move it. Bypassers never enter; bosses crush the
        /// wagon inside FindBlockingWagon and walk on.
        /// </summary>
        private bool UpdateWagonEngagement()
        {
            if (_engagedWagon != null)
            {
                if (!_engagedWagon.IsAlive)
                {
                    _engagedWagon = null;
                    return true;
                }

                RefreshEnemyMotion(Vector3.zero, 0f);
                _wagonAttackTimer -= Time.deltaTime;
                if (_wagonAttackTimer <= 0f)
                {
                    _wagonAttackTimer = TDBlockContract.EngageAttackInterval;
                    _engagedWagon.TakeEngagementHit(_lineDamage);
                }

                return false;
            }

            if (_queuedWagon != null)
            {
                if (!_queuedWagon.IsAlive)
                {
                    _queuedWagon = null;
                    return true;
                }

                RefreshEnemyMotion(Vector3.zero, 0f);
                if (_queuedWagon.TryEngage(this))
                {
                    _engagedWagon = _queuedWagon;
                    _queuedWagon = null;
                    _wagonAttackTimer = TDBlockContract.EngageAttackInterval;
                }

                return false;
            }

            var wagon = TDBlockerWagon.FindBlockingWagon(transform.position, this);
            if (wagon == null)
            {
                return true;
            }

            RefreshEnemyMotion(Vector3.zero, 0f);
            if (wagon.TryEngage(this))
            {
                _engagedWagon = wagon;
                _wagonAttackTimer = TDBlockContract.EngageAttackInterval;
            }
            else
            {
                _queuedWagon = wagon;
            }

            return false;
        }

        /// <summary>Taunt pulse / wagon-death hook: pull this enemy into the fight.</summary>
        public void TryEngageWagon(TDBlockerWagon wagon)
        {
            if (_resolved || _dying || wagon == null || !wagon.IsAlive ||
                _engagedWagon != null || _queuedWagon != null)
            {
                return;
            }

            RefreshEnemyMotion(Vector3.zero, 0f);
            if (wagon.TryEngage(this))
            {
                _engagedWagon = wagon;
                _wagonAttackTimer = TDBlockContract.EngageAttackInterval;
            }
            else
            {
                _queuedWagon = wagon;
            }
        }

        public void DetachFromWagon()
        {
            _engagedWagon = null;
            _queuedWagon = null;
            _wagonAttackTimer = 0f;
        }

        private float ResolveCinderPileSpeedBonus()
        {
            // Cinder Husk's remains (expansion batch 2): a fresh pile speeds
            // whoever crosses it — killing in the wrong place feeds the wave.
            return _gameManager != null && _gameManager.IsOnCinderPile(transform.position) ? 1.25f : 1f;
        }

        /// <summary>
        /// Rail Splitter: sprints on straight track (x1.8), drags through
        /// bends (x0.7) — pure segment geometry, straight when the path
        /// keeps its direction.
        /// </summary>
        private float ResolveSplitterSpeedMultiplier()
        {
            if (!string.Equals(_enemyId, "rail_splitter", StringComparison.Ordinal) ||
                _path == null || _path.Count <= 1)
            {
                return 1f;
            }

            var index = Mathf.Clamp(_nextWaypointIndex, 1, _path.Count - 1);
            var outDir = index + 1 < _path.Count ? _path[index + 1] - _path[index] : _path[index] - _path[index - 1];
            var inDir = _path[index] - _path[index - 1];
            if (inDir.sqrMagnitude < 1e-6f || outDir.sqrMagnitude < 1e-6f)
            {
                return 1f;
            }

            return Vector3.Dot(inDir.normalized, outDir.normalized) >= 0.9f ? 1.8f : 0.7f;
        }

        /// <summary>Route progress for lane swaps and echo copies.</summary>
        public float GetRouteProgress01()
        {
            if (_path == null || _path.Count <= 1)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)_nextWaypointIndex / (_path.Count - 1));
        }

        /// <summary>Places the enemy at a progress point (echo copies, lane swaps).</summary>
        public void WarpToProgress(float progress01)
        {
            if (_path == null || _path.Count == 0)
            {
                return;
            }

            var progress = Mathf.Clamp01(progress01);
            _nextWaypointIndex = Mathf.Clamp(Mathf.RoundToInt(progress * (_path.Count - 1)), 1, _path.Count - 1);
            transform.position = _path[_nextWaypointIndex - 1] + ((_path[_nextWaypointIndex] - _path[_nextWaypointIndex - 1]) * 0.5f);
        }

        public void SetCurrentHealth(int hp)
        {
            _hp = Mathf.Clamp(hp, 0, _maxHp);
            if (_hp <= 0)
            {
                ResolveKill(null);
            }
        }

        private bool AbsorbHitWithShield()
        {
            if (!string.Equals(_enemyId, "forge_dragoon", StringComparison.Ordinal))
            {
                return false;
            }

            var currentWave = _gameManager != null ? _gameManager.CurrentWaveIndex : 0;
            if (_shieldWave != currentWave)
            {
                _shieldWave = currentWave;
                _shieldHitsRemaining = 3;
            }

            if (_shieldHitsRemaining <= 0)
            {
                return false;
            }

            _shieldHitsRemaining--;
            return true;
        }

        /// <summary>
        /// Fixed damage channel (wagon thorns): no armor, no evasion, no
        /// tower DPS attribution — the zero-contribution exemption. Kills
        /// resolve without a source tower (bounty intact, no tower credit).
        /// </summary>
        public void TakeDirectDamage(int amount)
        {
            if (_resolved || _dying || amount <= 0)
            {
                return;
            }

            _hp = Mathf.Max(0, _hp - amount);
            _hitFxTimer = 0.10f;
            if (_hp <= 0)
            {
                ResolveKill(null);
            }
        }

        /// <summary>Wagon slow field: uses the standard slow slot semantics.</summary>
        public void ApplyFieldSlow(float slowPct, float duration)
        {
            if (_resolved || slowPct <= 0f || duration <= 0f)
            {
                return;
            }

            var appliedSlow = slowPct;
            if (HasTag("flank"))
            {
                appliedSlow *= 0.65f;
            }

            if (HasTag("boss"))
            {
                appliedSlow *= 0.55f;
            }

            var wasSlowed = IsSlowed;
            _slowPct = Mathf.Clamp(Mathf.Max(_slowPct, appliedSlow), 0f, 0.9f);
            _slowTimer = Mathf.Max(_slowTimer, duration);
            if (!wasSlowed && IsSlowed)
            {
                _gameManager?.NotifyEnemySlowed(this, _slowPct);
            }
        }

        public void ApplyBurn(int layers, float damagePerLayerPerSecond, float duration, TDTower sourceTower)
        {
            if (_resolved || layers <= 0 || duration <= 0f || damagePerLayerPerSecond <= 0f)
            {
                return;
            }

            _burnLayers = TDBurnSystem.ClampStacks(_burnLayers + layers);
            // Strongest fire wins per layer; duration refreshes to the longest
            // remaining so re-ignition never weakens an existing burn.
            _burnDamagePerLayer = Mathf.Max(_burnDamagePerLayer, damagePerLayerPerSecond);
            _burnTimer = Mathf.Max(_burnTimer, duration);
            _burnSourceTower = sourceTower;
        }

        public void ClearBurn()
        {
            _burnLayers = 0;
            _burnDamagePerLayer = 0f;
            _burnTimer = 0f;
            _burnTickAccumulator = 0f;
        }

        private void TakeBurnTick()
        {
            if (_resolved)
            {
                return;
            }

            var rawTick = TDBurnSystem.ResolveTickRawDamage(_burnLayers, _burnDamagePerLayer);
            var damageTaken = TDBurnSystem.ResolveBurnTick(rawTick, _armorFlat, _armorBreakFlat);
            _hp = Mathf.Max(0, _hp - damageTaken);
            _gameManager?.NotifyEnemyDamaged(_burnSourceTower, this, damageTaken, 0f, 0f);
            if (_hp <= 0)
            {
                ResolveKill(_burnSourceTower);
            }
        }

        public void ApplyArmorBreak(int flatAmount, float duration)
        {
            if (_resolved || flatAmount <= 0 || duration <= 0f)
            {
                return;
            }

            var wasArmorBroken = IsArmorBroken;
            _armorBreakFlat = Mathf.Max(_armorBreakFlat, flatAmount);
            _armorBreakTimer = Mathf.Max(_armorBreakTimer, duration);
            if (!wasArmorBroken && IsArmorBroken)
            {
                _gameManager?.NotifyEnemyArmorBroken(this, _armorBreakFlat);
            }
        }

        public void ApplyStagger(float duration, float minSpeedMultiplier)
        {
            if (_resolved || duration <= 0f)
            {
                return;
            }

            _staggerTimer = Mathf.Max(_staggerTimer, duration);
            _staggerMinSpeedMultiplier = Mathf.Clamp(minSpeedMultiplier, 0f, 1f);
        }

        public void ApplyExposed(float duration, float damageMultiplier)
        {
            if (_resolved || duration <= 0f || damageMultiplier <= 1f)
            {
                return;
            }

            _exposedTimer = Mathf.Max(_exposedTimer, duration);
            _exposedMultiplier = Mathf.Max(_exposedMultiplier, damageMultiplier);
            _gameManager?.PlayEnemySfx("status_expose", 0.5f);
        }

        public void ApplyScenarioSpeed(float duration, float speedMultiplier)
        {
            if (_resolved || duration <= 0f || speedMultiplier <= 0f)
            {
                return;
            }

            _scenarioSpeedTimer = Mathf.Max(_scenarioSpeedTimer, duration);
            _scenarioSpeedMultiplier = Mathf.Max(_scenarioSpeedMultiplier, speedMultiplier);
        }

        public void SetResonanceMark(float duration)
        {
            if (_resolved || duration <= 0f)
            {
                return;
            }

            _resonanceMarkTimer = Mathf.Max(_resonanceMarkTimer, duration);
        }

        private void ResolveKill(TDTower sourceTower)
        {
            _resolved = true;
            _gameManager.NotifyEnemyKilled(this, _reward, sourceTower);

            _dying = true;
            _deathFadeTimer = 0f;
            _deathStartScale = transform.localScale;
            // Death freezes UpdateVisualTint — clear the combat tints so the
            // corpse doesn't spend its whole death reel stuck white/marked
            // from the killing blow (review P2).
            _hitFlashTimer = 0f;
            _hitReactionTimer = 0f;
            _slowTimer = 0f;
            _resonanceMarkTimer = 0f;
            ResetEnemyMotion();
            _readability?.SetPresentationVisible(false);

            if (_bodyCollider != null)
            {
                _bodyCollider.enabled = false;
            }

            PlayBodyDeathReel();
            TryPlayDeathFx();
        }

        /// <summary>
        /// Per-enemy death reel (spec: enemy-death-frames-spec-v1). Probed,
        /// not assumed: without frames for this enemy the body keeps today's
        /// fade-only death, so art can land in batches with zero code
        /// follow-up. The shared fx_enemy_death burst stays on top either way.
        /// </summary>
        private void PlayBodyDeathReel()
        {
            if (_bodyAnimator == null || string.IsNullOrWhiteSpace(_enemyId))
            {
                return;
            }

            var idlePrefix = $"Art/anim/enemy_{_enemyId}";
            if (!IsFxSequenceAvailable($"{idlePrefix}_death"))
            {
                return;
            }

            // ConfigureDeath appends the _death segment itself; extra frame
            // slots simply stay empty for 4-frame enemies.
            _bodyAnimator.ConfigureDeath(idlePrefix, DeathReelMaxFrames, DeathReelFps);
            _bodyAnimator.PlayDeath();
            // A single-frame reel can't advance (the animator returns early
            // on frames<=1 and never disables itself) — the hold would ride
            // the full 0.75s safety timer. Only hold when the reel actually
            // plays multiple frames (review P2).
            if (_bodyAnimator.CurrentState == TDAnimationState.Death &&
                _bodyAnimator.DeathFrameCount > 1)
            {
                _bodyDeathReelPlaying = true;
            }
        }

        /// <summary>Funnel every enemy-destruction path through the pool
        /// (kill fade, escape, level switch, defeat sweep) — never Destroy
        /// directly or the hierarchy leaks out of the reuse cycle.</summary>
        public void ReleaseToPool()
        {
            if (TDEnemyPool.Instance != null)
            {
                TDEnemyPool.Instance.Release(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ResolveEscape()
        {
            _resolved = true;
            _readability?.SetPresentationVisible(false);
            _gameManager.NotifyEnemyEscaped(this, _lineDamage, _enemyId);
            ReleaseToPool();
        }

        private void UpdateVisualTint()
        {
            if (_visualRenderer == null)
            {
                return;
            }

            var slowBlend = _slowTimer > 0f ? Mathf.Clamp01(_slowPct * 0.75f) : 0f;
            var tinted = Color.Lerp(Color.white, SlowTint, slowBlend);
            tinted = new Color(
                tinted.r * _variantTint.r,
                tinted.g * _variantTint.g,
                tinted.b * _variantTint.b,
                1f);

            if (_armorBreakFlat > 0)
            {
                var breakBlend = Mathf.Clamp01(_armorBreakFlat / 8f);
                tinted = Color.Lerp(tinted, new Color(1f, 0.78f, 0.58f, 1f), breakBlend * 0.55f);
            }

            if (_exposedTimer > 0f)
            {
                var exposedBlend = Mathf.Clamp01((_exposedMultiplier - 1f) / 0.35f);
                tinted = Color.Lerp(tinted, new Color(1f, 0.74f, 0.74f, 1f), exposedBlend * 0.55f);
            }

            if (_resonanceMarkTimer > 0f)
            {
                var markBlend = Mathf.Clamp01(_resonanceMarkTimer * 1.8f);
                tinted = Color.Lerp(tinted, ResonanceMarkTint, markBlend * 0.70f);
            }

            if (_hitFlashTimer > 0f)
            {
                var flash = Mathf.Clamp01(_hitFlashTimer / HitFlashDuration);
                tinted = Color.Lerp(tinted, Color.white, flash * 0.95f);
            }

            tinted.a = 1f;
            _visualRenderer.color = tinted;
        }

        private void UpdateDeathFade()
        {
            // While the per-enemy death reel is playing the body stays fully
            // visible; the animator holds its last frame and disables itself
            // at the end, which releases the fade. The timer bounds the wait
            // against animator edge cases.
            if (_bodyDeathReelPlaying)
            {
                _deathReelHoldTimer += Time.deltaTime;
                var reelDone = _bodyAnimator == null ||
                               !_bodyAnimator.enabled ||
                               _bodyAnimator.CurrentState != TDAnimationState.Death ||
                               _deathReelHoldTimer >= DeathReelHoldMaxSeconds;
                if (!reelDone)
                {
                    return;
                }

                _bodyDeathReelPlaying = false;
            }

            _deathFadeTimer += Time.deltaTime;
            var t = Mathf.Clamp01(_deathFadeTimer / DeathFadeDuration);

            if (_visualRenderer != null)
            {
                var color = _visualRenderer.color;
                color.a = 1f - t;
                _visualRenderer.color = color;
            }

            transform.localScale = Vector3.Lerp(_deathStartScale, _deathStartScale * 0.9f, t);
            if (t >= 1f)
            {
                ReleaseToPool();
            }
        }

        private void UpdateSpecialMovementState()
        {
            if (_specialBurstTimer > 0f)
            {
                _specialBurstTimer = Mathf.Max(0f, _specialBurstTimer - Time.deltaTime);
                if (_specialBurstTimer <= 0f)
                {
                    _specialSpeedMultiplier = HasTag("flank") ? 1.08f : 1f;
                }
            }

            if (_specialBurstUsed || !HasTag("special") || _path == null || _path.Count <= 1)
            {
                return;
            }

            var progress = Mathf.Clamp01((float)_nextWaypointIndex / (_path.Count - 1));
            if (progress < 0.55f)
            {
                return;
            }

            _specialBurstUsed = true;
            var burstFactor = HasTag("flank") ? 1.55f : 1.35f;
            _specialSpeedMultiplier *= burstFactor;
            _specialBurstTimer = HasTag("flank") ? 2.0f : 1.6f;
            TryPlayBurrowAmbushFx();
        }

        private void ApplyVariantProfileIfNeeded()
        {
            if (!HasTag("mixed"))
            {
                return;
            }

            var roll = UnityEngine.Random.Range(0, 3);
            switch (roll)
            {
                case 0:
                    AddTag("fast");
                    _baseSpeed *= 1.30f;
                    _hp = Mathf.RoundToInt(_hp * 0.85f);
                    _variantTint = new Color(1f, 0.88f, 0.70f, 1f);
                    _mimicVariantIndex = 0;
                    break;
                case 1:
                    AddTag("armored");
                    AddTag("heavy");
                    _armorFlat += 4;
                    _hp = Mathf.RoundToInt(_hp * 1.15f);
                    _baseSpeed *= 0.88f;
                    _variantTint = new Color(0.74f, 0.80f, 0.96f, 1f);
                    _mimicVariantIndex = 1;
                    break;
                default:
                    AddTag("swarm");
                    _hp = Mathf.RoundToInt(_hp * 0.75f);
                    _baseSpeed *= 1.18f;
                    _lineDamage = Mathf.Max(1, _lineDamage - 1);
                    _variantTint = new Color(0.82f, 1f, 0.78f, 1f);
                    _mimicVariantIndex = 2;
                    break;
            }
        }

        private void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || HasTag(tag))
            {
                return;
            }

            _tags.Add(tag);
        }

        private SpriteRenderer ResolveVisualRenderer()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }

            SpriteRenderer best = null;
            var bestScore = int.MinValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                var candidate = renderers[i];
                if (candidate == null)
                {
                    continue;
                }

                var score = candidate.sortingOrder;
                var childName = candidate.transform.name;
                if (string.Equals(childName, "Visual", StringComparison.OrdinalIgnoreCase))
                {
                    score += 2000;
                }

                if (string.Equals(childName, "Shadow", StringComparison.OrdinalIgnoreCase))
                {
                    score -= 2000;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = candidate;
            }

            return best ?? renderers[0];
        }

        private void TryPlayHitFx(TDTower sourceTower)
        {
            // Tower hits are rate-limited by HitFxMinInterval (they arrive many
            // times per second); sourceless damage (scenario devices) shows its
            // own device FX and has no projectile impact spark to lean on.
            if (sourceTower == null || _hitFxTimer > 0f)
            {
                return;
            }

            _hitFxTimer = HitFxMinInterval;
            SpawnFxSequence(
                "Fx_EnemyHit",
                HitFxPrefix,
                HitFxFrameCount,
                HitFxFps,
                true,
                Vector3.zero,
                0.62f,
                1.28f,
                3,
                new Color(1f, 0.88f, 0.62f, 0.92f),
                new Color(1f, 0.54f, 0.24f, 0f));
        }

        private void TryPlayDeathFx()
        {
            SpawnFxSequence(
                "Fx_EnemyDeath",
                DeathFxPrefix,
                DeathFxFrameCount,
                DeathFxFps,
                false,
                new Vector3(0f, 0.03f, 0f),
                0.85f,
                1.85f,
                5,
                new Color(1f, 0.82f, 0.70f, 0.94f),
                new Color(0.80f, 0.20f, 0.14f, 0f));
        }

        private void TryPlayBossWarningFx()
        {
            if (_bossWarningFxPlayed || (!HasTag("boss") && !HasTag("final")))
            {
                return;
            }

            _bossWarningFxPlayed = true;
            SpawnFxSequence(
                "Fx_BossWarning",
                BossWarningFxPrefix,
                BossWarningFxFrameCount,
                BossWarningFxFps,
                true,
                new Vector3(0f, 0.12f, 0f),
                0.95f,
                2.05f,
                6,
                new Color(1f, 0.84f, 0.50f, 0.90f),
                new Color(1f, 0.30f, 0.18f, 0f));
        }

        private void TryPlayBurrowAmbushFx()
        {
            if (_burrowAmbushFxPlayed || !string.Equals(_enemyId, "burrow_sapper", StringComparison.Ordinal))
            {
                return;
            }

            _burrowAmbushFxPlayed = true;
            _gameManager?.PlayEnemySfx("enemy_burrow_ambush", 0.66f);
            SpawnFxSequence(
                "Fx_BurrowAmbush",
                BurrowAmbushFxPrefix,
                BurrowAmbushFxFrameCount,
                BurrowAmbushFxFps,
                true,
                new Vector3(0f, -0.03f, 0f),
                0.80f,
                1.70f,
                5,
                new Color(1f, 0.82f, 0.52f, 0.92f),
                new Color(0.92f, 0.34f, 0.18f, 0f));
        }

        private void TryPlaySporeSplitWarningFx()
        {
            if (
                _sporeSplitWarningFxPlayed ||
                _resolved ||
                _dying ||
                _maxHp <= 0 ||
                !string.Equals(_enemyId, "spore_carrier", StringComparison.Ordinal))
            {
                return;
            }

            var threshold = Mathf.Max(1, Mathf.RoundToInt(_maxHp * SporeSplitWarningHealthThreshold));
            if (_hp > threshold)
            {
                return;
            }

            _sporeSplitWarningFxPlayed = true;
            SpawnFxSequence(
                "Fx_SporeSplitWarning",
                SporeSplitWarningFxPrefix,
                SporeSplitWarningFxFrameCount,
                SporeSplitWarningFxFps,
                true,
                new Vector3(0f, 0.04f, 0f),
                0.78f,
                1.66f,
                4,
                new Color(0.86f, 1f, 0.68f, 0.90f),
                new Color(0.34f, 0.84f, 0.46f, 0f));
        }

        private void TryPlayMimicShiftFx()
        {
            if (
                _mimicShiftFxPlayed ||
                !string.Equals(_enemyId, "echo_mimic", StringComparison.Ordinal) ||
                _mimicVariantIndex < 0)
            {
                return;
            }

            _mimicShiftFxPlayed = true;
            _gameManager?.PlayEnemySfx("enemy_mimic_shift", 0.64f);
            var color = _mimicVariantIndex switch
            {
                0 => new Color(1f, 0.78f, 0.46f, 0.92f),
                1 => new Color(0.74f, 0.88f, 1f, 0.92f),
                _ => new Color(0.78f, 1f, 0.74f, 0.92f)
            };
            var fadeColor = _mimicVariantIndex switch
            {
                0 => new Color(0.96f, 0.44f, 0.18f, 0f),
                1 => new Color(0.38f, 0.58f, 1f, 0f),
                _ => new Color(0.34f, 0.88f, 0.56f, 0f)
            };

            SpawnFxSequence(
                "Fx_MimicShift",
                MimicShiftFxPrefix,
                MimicShiftFxFrameCount,
                MimicShiftFxFps,
                true,
                new Vector3(0f, 0.06f, 0f),
                0.84f,
                1.86f,
                5,
                color,
                fadeColor);
        }

        private void TryUpdateAttritionSiphonFx()
        {
            if (!HasTag("attrition") || _resolved || _dying)
            {
                return;
            }

            _attritionSiphonFxTimer = Mathf.Max(0f, _attritionSiphonFxTimer - Time.deltaTime);
            if (_attritionSiphonFxTimer > 0f)
            {
                return;
            }

            _attritionSiphonFxTimer = AttritionSiphonPulseInterval + UnityEngine.Random.Range(-0.25f, 0.25f);
            _gameManager?.PlayEnemySfx("enemy_attrition", 0.50f);
            SpawnFxSequence(
                "Fx_AttritionSiphon",
                AttritionSiphonFxPrefix,
                AttritionSiphonFxFrameCount,
                AttritionSiphonFxFps,
                true,
                new Vector3(0f, 0.03f, 0f),
                0.78f,
                1.72f,
                4,
                new Color(1f, 0.70f, 0.54f, 0.90f),
                new Color(0.92f, 0.22f, 0.16f, 0f));
        }

        private void TryUpdateSupportLinkFx()
        {
            if (!HasTag("support") || _resolved || _dying)
            {
                return;
            }

            _supportLinkFxTimer = Mathf.Max(0f, _supportLinkFxTimer - Time.deltaTime);
            if (_supportLinkFxTimer > 0f)
            {
                return;
            }

            _supportLinkFxTimer = SupportLinkPulseInterval + UnityEngine.Random.Range(-0.22f, 0.22f);
            if (!HasSupportTargetsNearby())
            {
                return;
            }

            _gameManager?.PlayEnemySfx("enemy_support_link", 0.54f);
            SpawnFxSequence(
                "Fx_SupportLink",
                SupportLinkFxPrefix,
                SupportLinkFxFrameCount,
                SupportLinkFxFps,
                true,
                new Vector3(0f, 0.05f, 0f),
                0.82f,
                1.84f,
                4,
                new Color(0.72f, 0.90f, 1f, 0.90f),
                new Color(0.28f, 0.54f, 0.98f, 0f));
        }

        private bool HasSupportTargetsNearby()
        {
            if (_gameManager == null)
            {
                return false;
            }

            var neighbors = _gameManager.GetEnemiesInRange(transform.position, SupportLinkCheckRadius, 8);
            for (var i = 0; i < neighbors.Count; i++)
            {
                var enemy = neighbors[i];
                if (enemy == null || ReferenceEquals(enemy, this))
                {
                    continue;
                }

                if (enemy.HasTag("armored") || enemy.HasTag("heavy") || enemy.HasTag("boss") || enemy.HasTag("final"))
                {
                    return true;
                }
            }

            return false;
        }

        private void TryPlayElitePressureFx()
        {
            if (
                _elitePressureFxPlayed ||
                _resolved ||
                _dying ||
                _maxHp <= 0 ||
                (!HasTag("elite") && !string.Equals(_enemyId, "husk_titan", StringComparison.Ordinal)))
            {
                return;
            }

            var threshold = Mathf.Max(1, Mathf.RoundToInt(_maxHp * ElitePressureHealthThreshold));
            if (_hp > threshold)
            {
                return;
            }

            _elitePressureFxPlayed = true;
            _gameManager?.PlayEnemySfx("enemy_elite_pressure", 0.68f);
            SpawnFxSequence(
                "Fx_ElitePressure",
                ElitePressureFxPrefix,
                ElitePressureFxFrameCount,
                ElitePressureFxFps,
                true,
                new Vector3(0f, 0.05f, 0f),
                0.90f,
                2.02f,
                5,
                new Color(1f, 0.82f, 0.54f, 0.90f),
                new Color(0.96f, 0.32f, 0.12f, 0f));
        }

        private void SpawnFxSequence(
            string fxName,
            string prefix,
            int frameCount,
            float fps,
            bool attachToEnemy,
            Vector3 offset,
            float startScaleMultiplier,
            float endScaleMultiplier,
            int sortingOffset,
            Color startColor,
            Color endColor)
        {
            if (_gameManager == null || frameCount <= 0 || fps <= 0f || !IsFxSequenceAvailable(prefix))
            {
                return;
            }

            // Pooled (SpriteRenderer + animator + transient FX are pre-wired on
            // pool objects) instead of instantiate/destroy per event — these
            // fire on every rate-limited hit, every death and every pulse.
            // If the host enemy is destroyed before the FX finishes, the
            // checked-out pooled object simply isn't returned — the pool
            // creates a fresh one on demand, so no stale references survive.
            var parent = attachToEnemy ? transform : _gameManager.transform;
            var (fxObject, transient, renderer) = TDObjectPool.GetFxObject(
                parent,
                transform.position,
                fxName);
            if (attachToEnemy)
            {
                fxObject.transform.localPosition = ResolveVisualLocalPosition() + offset;
            }
            else
            {
                var anchor = _visualRoot != null ? _visualRoot.position : transform.position;
                fxObject.transform.position = anchor + offset;
            }

            renderer.sortingOrder = (_visualRenderer != null ? _visualRenderer.sortingOrder : 16) + sortingOffset;
            renderer.sprite = Resources.Load<Sprite>($"{prefix}_00");
            if (renderer.sprite == null)
            {
                transient.ReturnToPool();
                return;
            }

            var animator = fxObject.GetComponent<TDSpriteAnimator>();
            if (animator == null)
            {
                // Defensive: the no-pool fallback path may hand out an object
                // assembled before the animator was part of the FX kit.
                animator = fxObject.AddComponent<TDSpriteAnimator>();
            }

            animator.Configure(prefix, frameCount, fps, false, false);

            var baseScale = ResolveFxBaseScale();
            var duration = Mathf.Max(0.06f, (frameCount / Mathf.Max(1f, fps)) * 1.05f);
            transient.Configure(
                duration,
                Vector3.one * (baseScale * Mathf.Max(0.01f, startScaleMultiplier)),
                Vector3.one * (baseScale * Mathf.Max(0.01f, endScaleMultiplier)),
                startColor,
                endColor);
        }

        private static bool IsFxSequenceAvailable(string prefix)
        {
            if (FxPrefixAvailability.TryGetValue(prefix, out var available))
            {
                return available;
            }

            available = Resources.Load<Sprite>($"{prefix}_00") != null;
            FxPrefixAvailability[prefix] = available;
            return available;
        }

        private Vector3 ResolveVisualLocalPosition()
        {
            return _visualRoot != null ? _visualRoot.localPosition : Vector3.zero;
        }

        private float ResolveFxBaseScale()
        {
            var sourceScale = _visualRoot != null ? _visualRoot.localScale.x : 0.72f;
            return Mathf.Max(0.42f, sourceScale);
        }
    }
}
