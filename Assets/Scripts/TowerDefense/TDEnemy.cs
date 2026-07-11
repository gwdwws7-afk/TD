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
        private static readonly Color ArmorBreakIconColor = new(1f, 0.68f, 0.26f, 1f);
        private const float HitFlashDuration = 0.10f;
        private const float DeathFadeDuration = 0.22f;
        private const string ArmorBreakIconSpritePath = "Art/projectile_bolt";
        private const float HitFxMinInterval = 0.06f;
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
        private float _baseSpeed;
        private float _slowPct;
        private float _slowTimer;
        private float _staggerTimer;
        private float _staggerMinSpeedMultiplier;
        private int _armorBreakFlat;
        private float _armorBreakTimer;
        private float _exposedTimer;
        private float _exposedMultiplier = 1f;
        private int _hp;
        private int _maxHp;
        private int _armorFlat;
        private int _reward;
        private int _lineDamage;
        private string _enemyId;
        private List<string> _tags;
        private bool _resolved;
        private bool _dying;
        private float _hitFlashTimer;
        private float _resonanceMarkTimer;
        private float _deathFadeTimer;
        private float _specialSpeedMultiplier;
        private float _specialBurstTimer;
        private bool _specialBurstUsed;
        private Color _variantTint;
        private Vector3 _deathStartScale;
        private BoxCollider2D _bodyCollider;
        private SpriteRenderer _visualRenderer;
        private Transform _visualRoot;
        private Transform _armorBreakIconRoot;
        private SpriteRenderer _armorBreakIconRenderer;
        private float _armorBreakIconPulse;
        private Transform _threatMarkerRoot;
        private SpriteRenderer _threatMarkerRenderer;
        private Color _threatMarkerColor;
        private float _threatMarkerPulse;
        private bool _threatMarkerEnabled;
        private float _hitFxTimer;
        private bool _bossWarningFxPlayed;
        private bool _burrowAmbushFxPlayed;
        private bool _sporeSplitWarningFxPlayed;
        private bool _mimicShiftFxPlayed;
        private bool _elitePressureFxPlayed;
        private int _mimicVariantIndex = -1;
        private float _attritionSiphonFxTimer;
        private float _supportLinkFxTimer;

        public string EnemyId => _enemyId;
        public bool IsMarked => _resonanceMarkTimer > 0f;
        public float HealthRatio => _maxHp <= 0 ? 1f : Mathf.Clamp01(_hp / (float)_maxHp);

        public void Initialize(TDGameManager gameManager, IReadOnlyList<Vector3> path, TDEnemyCatalogEntry entry)
        {
            _gameManager = gameManager;
            _path = path;
            _enemyId = entry.enemyId;
            _hp = entry.hp;
            _baseSpeed = entry.speed;
            _armorFlat = Mathf.Max(0, entry.armorFlat);
            _reward = Mathf.Max(0, entry.rewardGold);
            _lineDamage = Mathf.Max(1, entry.lineDamage);
            _tags = new List<string>(entry.tags ?? Array.Empty<string>());
            _nextWaypointIndex = 1;
            _bodyCollider = GetComponent<BoxCollider2D>();
            _visualRenderer = ResolveVisualRenderer();
            _visualRoot = _visualRenderer != null ? _visualRenderer.transform : transform;
            _hitFlashTimer = 0f;
            _deathFadeTimer = 0f;
            _dying = false;
            _specialSpeedMultiplier = 1f;
            _specialBurstTimer = 0f;
            _specialBurstUsed = false;
            _variantTint = Color.white;
            _staggerTimer = 0f;
            _staggerMinSpeedMultiplier = 1f;
            _armorBreakFlat = 0;
            _armorBreakTimer = 0f;
            _armorBreakIconPulse = 0f;
            _threatMarkerPulse = 0f;
            _threatMarkerEnabled = false;
            _exposedTimer = 0f;
            _exposedMultiplier = 1f;
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
            EnsureArmorBreakIcon();
            EnsureThreatMarkerIcon();
            if (HasTag("flank"))
            {
                _specialSpeedMultiplier *= 1.08f;
            }

            if (_path.Count > 0)
            {
                transform.position = _path[0];
            }

            TryPlayBossWarningFx();
            TryPlayMimicShiftFx();
        }

        private void Update()
        {
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

            TryUpdateAttritionSiphonFx();
            TryUpdateSupportLinkFx();
            TryPlayElitePressureFx();
            TryPlaySporeSplitWarningFx();
            UpdateThreatMarkerVisual();

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

            UpdateSpecialMovementState();

            if (_nextWaypointIndex >= _path.Count)
            {
                ResolveEscape();
                return;
            }

            var target = _path[_nextWaypointIndex];
            var delta = target - transform.position;
            var effectiveSpeed = _baseSpeed * _specialSpeedMultiplier * Mathf.Clamp01(1f - _slowPct);
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
                UpdateVisualTint();
                return;
            }

            transform.position += delta.normalized * step;
            UpdateVisualTint();
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

        public int TakeHit(int rawDamage, float slowPct, float slowDuration)
        {
            if (_resolved || rawDamage <= 0)
            {
                return 0;
            }

            var damageWithExposure = Mathf.RoundToInt(rawDamage * Mathf.Max(1f, _exposedMultiplier));
            var effectiveArmor = Mathf.Max(0, _armorFlat - _armorBreakFlat);
            var damageTaken = Mathf.Max(1, damageWithExposure - effectiveArmor);
            _hitFlashTimer = HitFlashDuration;
            _hp -= damageTaken;
            if (_hp <= 0)
            {
                ResolveKill();
                return damageTaken;
            }

            TryPlayHitFx();

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
            }

            return damageTaken;
        }

        public void ApplyArmorBreak(int flatAmount, float duration)
        {
            if (_resolved || flatAmount <= 0 || duration <= 0f)
            {
                return;
            }

            _armorBreakFlat = Mathf.Max(_armorBreakFlat, flatAmount);
            _armorBreakTimer = Mathf.Max(_armorBreakTimer, duration);
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
        }

        public void SetResonanceMark(float duration)
        {
            if (_resolved || duration <= 0f)
            {
                return;
            }

            _resonanceMarkTimer = Mathf.Max(_resonanceMarkTimer, duration);
        }

        private void ResolveKill()
        {
            _resolved = true;
            _gameManager.NotifyEnemyKilled(this, _reward);

            _dying = true;
            _deathFadeTimer = 0f;
            _deathStartScale = transform.localScale;
            SetArmorBreakIconVisible(false);

            if (_bodyCollider != null)
            {
                _bodyCollider.enabled = false;
            }

            TryPlayDeathFx();
        }

        private void ResolveEscape()
        {
            _resolved = true;
            SetArmorBreakIconVisible(false);
            _gameManager.NotifyEnemyEscaped(this, _lineDamage, _enemyId);
            Destroy(gameObject);
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
            UpdateArmorBreakIconVisual();
            UpdateThreatMarkerVisual();
        }

        private void UpdateDeathFade()
        {
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
                Destroy(gameObject);
            }
        }

        private void EnsureArmorBreakIcon()
        {
            if (_armorBreakIconRoot != null)
            {
                return;
            }

            var iconRoot = new GameObject("Fx_ArmorBreakIcon");
            iconRoot.transform.SetParent(transform, false);
            iconRoot.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            iconRoot.transform.localScale = Vector3.one * 0.24f;

            var iconRenderer = iconRoot.AddComponent<SpriteRenderer>();
            iconRenderer.sortingOrder = (_visualRenderer != null ? _visualRenderer.sortingOrder : 10) + 4;
            iconRenderer.sprite = TDArtLibrary.LoadSpriteOrFallback(ArmorBreakIconSpritePath, ArmorBreakIconColor);
            iconRenderer.color = new Color(ArmorBreakIconColor.r, ArmorBreakIconColor.g, ArmorBreakIconColor.b, 0f);

            _armorBreakIconRoot = iconRoot.transform;
            _armorBreakIconRenderer = iconRenderer;
            SetArmorBreakIconVisible(false);
        }

        private void UpdateArmorBreakIconVisual()
        {
            if (_armorBreakIconRoot == null || _armorBreakIconRenderer == null)
            {
                return;
            }

            var active = _armorBreakFlat > 0 && _armorBreakTimer > 0f && !_resolved && !_dying;
            SetArmorBreakIconVisible(active);
            if (!active)
            {
                return;
            }

            _armorBreakIconPulse += Time.deltaTime * 7.2f;
            var pulse = 0.5f + (Mathf.Sin(_armorBreakIconPulse) * 0.5f);
            var alpha = Mathf.Lerp(0.45f, 0.92f, pulse);

            _armorBreakIconRoot.localPosition = new Vector3(0f, 0.62f + (pulse * 0.03f), 0f);
            _armorBreakIconRoot.localRotation = Quaternion.Euler(0f, 0f, pulse * 18f);
            _armorBreakIconRoot.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.28f, pulse);
            _armorBreakIconRenderer.color = new Color(
                ArmorBreakIconColor.r,
                ArmorBreakIconColor.g,
                ArmorBreakIconColor.b,
                alpha);
        }

        private void SetArmorBreakIconVisible(bool visible)
        {
            if (_armorBreakIconRoot != null && _armorBreakIconRoot.gameObject.activeSelf != visible)
            {
                _armorBreakIconRoot.gameObject.SetActive(visible);
            }
        }

        private void EnsureThreatMarkerIcon()
        {
            _threatMarkerEnabled = TryResolveThreatMarkerColor(out _threatMarkerColor);
            if (!_threatMarkerEnabled)
            {
                if (_threatMarkerRoot != null)
                {
                    _threatMarkerRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (_threatMarkerRoot == null)
            {
                var markerRoot = new GameObject("Fx_ThreatMarker");
                markerRoot.transform.SetParent(transform, false);
                markerRoot.transform.localPosition = new Vector3(0f, 0.72f, 0f);
                markerRoot.transform.localScale = Vector3.one * 0.22f;
                _threatMarkerRoot = markerRoot.transform;
            }

            if (_threatMarkerRenderer == null)
            {
                _threatMarkerRenderer = _threatMarkerRoot.GetComponent<SpriteRenderer>();
                if (_threatMarkerRenderer == null)
                {
                    _threatMarkerRenderer = _threatMarkerRoot.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            _threatMarkerRenderer.sortingOrder = (_visualRenderer != null ? _visualRenderer.sortingOrder : 10) + 5;
            _threatMarkerRenderer.sprite = TDArtLibrary.GetSoftRingSprite();
            _threatMarkerRoot.gameObject.SetActive(_threatMarkerRenderer.sprite != null);
        }

        private void UpdateThreatMarkerVisual()
        {
            if (!_threatMarkerEnabled || _threatMarkerRoot == null || _threatMarkerRenderer == null || _resolved || _dying)
            {
                if (_threatMarkerRoot != null && _threatMarkerRoot.gameObject.activeSelf)
                {
                    _threatMarkerRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (!_threatMarkerRoot.gameObject.activeSelf)
            {
                _threatMarkerRoot.gameObject.SetActive(true);
            }

            _threatMarkerPulse += Time.deltaTime * 5.0f;
            var pulse = 0.5f + (Mathf.Sin(_threatMarkerPulse) * 0.5f);
            var alpha = Mathf.Lerp(0.36f, 0.84f, pulse);
            var scale = Mathf.Lerp(0.18f, 0.25f, pulse);
            _threatMarkerRoot.localPosition = new Vector3(0f, 0.70f + (pulse * 0.035f), 0f);
            _threatMarkerRoot.localRotation = Quaternion.Euler(0f, 0f, _threatMarkerPulse * 18f);
            _threatMarkerRoot.localScale = Vector3.one * scale;
            _threatMarkerRenderer.color = new Color(_threatMarkerColor.r, _threatMarkerColor.g, _threatMarkerColor.b, alpha);
        }

        private bool TryResolveThreatMarkerColor(out Color color)
        {
            if (HasTag("boss") || HasTag("final") || HasTag("elite"))
            {
                color = new Color(1f, 0.30f, 0.18f, 1f);
                return true;
            }

            if (HasTag("support") || HasTag("attrition") || HasTag("zone_control"))
            {
                color = new Color(0.50f, 1f, 0.54f, 1f);
                return true;
            }

            if (HasTag("armored") || HasTag("heavy") || HasTag("durability"))
            {
                color = new Color(1f, 0.68f, 0.22f, 1f);
                return true;
            }

            if (HasTag("fast") || HasTag("flank") || HasTag("special"))
            {
                color = new Color(0.38f, 0.92f, 1f, 1f);
                return true;
            }

            if (HasTag("swarm") || HasTag("split") || HasTag("spawn") || HasTag("mixed"))
            {
                color = new Color(1f, 0.50f, 0.24f, 1f);
                return true;
            }

            color = Color.clear;
            return false;
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

        private void TryPlayHitFx()
        {
            if (_hitFxTimer > 0f)
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

            var fxObject = new GameObject(fxName);
            if (attachToEnemy)
            {
                fxObject.transform.SetParent(transform, false);
                fxObject.transform.localPosition = ResolveVisualLocalPosition() + offset;
            }
            else
            {
                fxObject.transform.SetParent(_gameManager.transform, true);
                var anchor = _visualRoot != null ? _visualRoot.position : transform.position;
                fxObject.transform.position = anchor + offset;
            }

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = (_visualRenderer != null ? _visualRenderer.sortingOrder : 16) + sortingOffset;
            renderer.sprite = Resources.Load<Sprite>($"{prefix}_00");
            if (renderer.sprite == null)
            {
                Destroy(fxObject);
                return;
            }

            var animator = fxObject.AddComponent<TDSpriteAnimator>();
            animator.Configure(prefix, frameCount, fps, false, false);

            var baseScale = ResolveFxBaseScale();
            var duration = Mathf.Max(0.06f, (frameCount / Mathf.Max(1f, fps)) * 1.05f);
            var transient = fxObject.AddComponent<TDTransientSpriteFx>();
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
