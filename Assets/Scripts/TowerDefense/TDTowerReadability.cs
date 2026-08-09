using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    [DisallowMultipleComponent]
    public sealed class TDTowerReadability : MonoBehaviour
    {
        private const string TierPipPath = "Art/Combat/P11/threat_pip";
        private const int ChargeSegmentCount = 4;

        private readonly List<SpriteRenderer> _chargeSegments = new();
        private readonly List<SpriteRenderer> _tierPips = new();
        private TDTower _tower;
        private Transform _chargeRoot;
        private SpriteRenderer _chargeRingRenderer;
        private float _cellSize;
        private float _pulse;
        private bool _chargeVisible;
        private bool _debugChargeHold;
        private int _bodySortingOrder;
        private Transform _visualRoot;
        private Vector3 _visualBasePosition;
        private Vector3 _visualBaseScale = Vector3.one;
        private float _attackMotionTimer;
        private float _upgradeMotionTimer;
        private int _attackDirection = 1;
        private Transform _interactionRoot;
        private SpriteRenderer _interactionRenderer;
        private bool _hovered;
        private bool _selected;

        public bool ChargeVisible => _chargeVisible && _chargeRingRenderer != null && _chargeRingRenderer.enabled;
        public float ChargeProgress { get; private set; }
        public int VisibleTierPips { get; private set; }
        public int UpgradePresentationCount { get; private set; }
        public int AttackPresentationCount { get; private set; }
        public int BodySortingOrder => _bodySortingOrder;
        public float ChargeDiameterWorld => _cellSize * PresentationProfile.chargeEndCoverage;
        public bool MotionReady => _visualRoot != null;
        public bool InteractionVisible => _interactionRenderer != null && _interactionRenderer.enabled;
        public bool IsHovered => _hovered;
        public bool IsSelected => _selected;
        public float InteractionDiameterWorld => _cellSize * (_selected ? 0.86f : 0.76f);
        public string ChargeRhythmId => PresentationProfile.chargeRhythmId;
        public string ProjectileLanguageId => PresentationProfile.projectileLanguageId;
        public string ImpactShapeId => PresentationProfile.impactShapeId;
        public string UpgradeMotionId => PresentationProfile.upgradeMotionId;
        public float PresentationAttackDuration => PresentationProfile.attackDuration;
        public float PresentationUpgradeDuration => PresentationProfile.upgradeDuration;

        private TDTowerPresentationProfile PresentationProfile =>
            TDTowerPresentationProfiles.Get(_tower != null ? _tower.Kind : TDTowerKind.RailLancer);

        public void Initialize(TDTower tower, float cellSize)
        {
            _tower = tower;
            _cellSize = Mathf.Max(0.1f, cellSize);
            _pulse = Random.Range(0f, Mathf.PI * 2f);
            EnsureChargeVisual();
            EnsureTierPips();
            EnsureInteractionVisual();
            SetChargeState(false, 0f);
            SetInteractionState(false, false);
        }

        public void RefreshMotionBaseline()
        {
            _visualRoot = transform.Find("Visual");
            if (_visualRoot == null)
            {
                return;
            }

            _visualBasePosition = _visualRoot.localPosition;
            _visualBaseScale = _visualRoot.localScale;
            _visualRoot.localRotation = Quaternion.identity;
        }

        public void SetChargeState(bool visible, float progress)
        {
            EnsureChargeVisual();
            if (_debugChargeHold)
            {
                return;
            }
            _chargeVisible = visible;
            ChargeProgress = visible ? Mathf.Clamp01(progress) : 0f;

            if (_chargeRingRenderer != null)
            {
                _chargeRingRenderer.enabled = visible && _chargeRingRenderer.sprite != null;
            }

            RefreshChargeVisual();
        }

        public void DebugHoldCharge(float progress)
        {
            EnsureChargeVisual();
            _debugChargeHold = true;
            _chargeVisible = true;
            ChargeProgress = Mathf.Clamp01(progress);
            _chargeRingRenderer.enabled = _chargeRingRenderer.sprite != null;
            RefreshChargeVisual();
        }

        public void RefreshTier(IReadOnlyList<TDTowerUpgradeBranch> history)
        {
            EnsureTierPips();
            VisibleTierPips = Mathf.Min(3, history?.Count ?? 0);
            var start = -((VisibleTierPips - 1) * 0.105f) * 0.5f;
            for (var i = 0; i < _tierPips.Count; i++)
            {
                var renderer = _tierPips[i];
                var visible = i < VisibleTierPips && renderer.sprite != null;
                renderer.enabled = visible;
                if (!visible)
                {
                    continue;
                }

                renderer.transform.localPosition = new Vector3(start + (i * 0.105f), -0.34f, 0f);
                renderer.transform.localScale = Vector3.one * 0.055f;
                renderer.color = ResolveBranchColor(history[i], 0.94f);
                renderer.sortingOrder = _bodySortingOrder + 3;
            }
        }

        public void ApplySorting(int bodySortingOrder)
        {
            _bodySortingOrder = bodySortingOrder;
            if (_interactionRenderer != null)
            {
                _interactionRenderer.sortingOrder = TDWorldVisualOrder.GroundInteraction;
            }

            if (_chargeRingRenderer != null)
            {
                _chargeRingRenderer.sortingOrder = bodySortingOrder + 1;
            }

            for (var i = 0; i < _chargeSegments.Count; i++)
            {
                _chargeSegments[i].sortingOrder = bodySortingOrder + 2;
            }

            for (var i = 0; i < _tierPips.Count; i++)
            {
                _tierPips[i].sortingOrder = bodySortingOrder + 3;
            }
        }

        public void SetInteractionState(bool hovered, bool selected)
        {
            EnsureInteractionVisual();
            _hovered = hovered;
            _selected = selected;
            RefreshInteractionVisual();
        }

        public void PlayUpgrade(TDTowerUpgradeBranch branch, int tier)
        {
            UpgradePresentationCount++;
            _upgradeMotionTimer = PresentationProfile.upgradeDuration;
            var color = ResolveBranchColor(branch, 1f);
            SpawnUpgradeRing(color);
            SpawnUpgradeIcon(branch, color);
            SpawnTowerIdentityBurst(color, tier);
        }

        public void PlayAttack()
        {
            AttackPresentationCount++;
            _attackMotionTimer = PresentationProfile.attackDuration;
            _attackDirection = -_attackDirection;
            var animator = _visualRoot != null ? _visualRoot.GetComponent<TDSpriteAnimator>() : null;
            animator?.Restart();
        }

        public void DebugPlayAttack()
        {
            PlayAttack();
        }

        public void DebugPlayUpgrade(TDTowerUpgradeBranch branch, int tier)
        {
            PlayUpgrade(branch, tier);
        }

        private void Update()
        {
            _pulse += Time.deltaTime * PresentationProfile.chargePulseSpeed;
            _attackMotionTimer = Mathf.Max(0f, _attackMotionTimer - Time.deltaTime);
            _upgradeMotionTimer = Mathf.Max(0f, _upgradeMotionTimer - Time.deltaTime);
            if (_chargeVisible)
            {
                RefreshChargeVisual();
            }

            if (_hovered || _selected)
            {
                RefreshInteractionVisual();
            }

            RefreshTowerMotion();
        }

        private void RefreshTowerMotion()
        {
            if (_visualRoot == null)
            {
                RefreshMotionBaseline();
            }

            if (_visualRoot == null)
            {
                return;
            }

            var idle = Mathf.Sin(_pulse * 0.54f) * _cellSize * 0.006f;
            var charge = _chargeVisible ? Mathf.Clamp01(ChargeProgress) : 0f;
            var attackDuration = Mathf.Max(0.01f, PresentationProfile.attackDuration);
            var upgradeDuration = Mathf.Max(0.01f, PresentationProfile.upgradeDuration);
            var attack = _attackMotionTimer <= 0f
                ? 0f
                : Mathf.Sin((1f - (_attackMotionTimer / attackDuration)) * Mathf.PI);
            var upgrade = _upgradeMotionTimer <= 0f
                ? 0f
                : Mathf.Sin((1f - (_upgradeMotionTimer / upgradeDuration)) * Mathf.PI);
            var attackKick = PresentationProfile.attackKick;

            _visualRoot.localPosition = _visualBasePosition + new Vector3(
                attack * _attackDirection * _cellSize * attackKick,
                idle - (charge * _cellSize * 0.018f) + (upgrade * _cellSize * 0.025f),
                0f);
            _visualRoot.localScale = new Vector3(
                _visualBaseScale.x * (1f + (charge * 0.025f) + (attack * 0.035f)),
                _visualBaseScale.y * (1f - (charge * 0.020f) - (attack * 0.028f) + (upgrade * 0.045f)),
                _visualBaseScale.z);
            _visualRoot.localRotation = Quaternion.Euler(0f, 0f, attack * _attackDirection * 2.2f);
        }

        private void EnsureChargeVisual()
        {
            if (_chargeRingRenderer != null)
            {
                return;
            }

            var root = new GameObject("Charge_Readability");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            _chargeRoot = root.transform;

            var ring = new GameObject("Charge_Ring");
            ring.transform.SetParent(_chargeRoot, false);
            _chargeRingRenderer = ring.AddComponent<SpriteRenderer>();
            _chargeRingRenderer.sprite = TDArtLibrary.GetSoftRingSprite();

            var pipSprite = Resources.Load<Sprite>(TierPipPath);
            for (var i = 0; i < ChargeSegmentCount; i++)
            {
                var segment = new GameObject($"Charge_Segment_{i + 1}");
                segment.transform.SetParent(_chargeRoot, false);
                var angle = (Mathf.PI * 2f * i / ChargeSegmentCount) + (Mathf.PI * 0.25f);
                segment.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.20f, Mathf.Sin(angle) * 0.13f, 0f);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, (-angle * Mathf.Rad2Deg) + 45f);
                segment.transform.localScale = Vector3.one * 0.045f;
                var renderer = segment.AddComponent<SpriteRenderer>();
                renderer.sprite = pipSprite;
                renderer.enabled = false;
                _chargeSegments.Add(renderer);
            }
        }

        private void EnsureTierPips()
        {
            if (_tierPips.Count > 0)
            {
                return;
            }

            var sprite = Resources.Load<Sprite>(TierPipPath);
            for (var i = 0; i < 3; i++)
            {
                var pip = new GameObject($"Upgrade_Tier_{i + 1}");
                pip.transform.SetParent(transform, false);
                var renderer = pip.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.enabled = false;
                _tierPips.Add(renderer);
            }
        }

        private void EnsureInteractionVisual()
        {
            if (_interactionRenderer != null)
            {
                return;
            }

            var root = new GameObject("Tower_Interaction");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            _interactionRoot = root.transform;

            _interactionRenderer = root.AddComponent<SpriteRenderer>();
            _interactionRenderer.sprite = TDArtLibrary.GetSoftRingSprite();
            _interactionRenderer.sortingOrder = TDWorldVisualOrder.GroundInteraction;
            _interactionRenderer.enabled = false;
        }

        private void RefreshInteractionVisual()
        {
            if (_interactionRenderer == null || _interactionRoot == null)
            {
                return;
            }

            var visible = (_hovered || _selected) && _interactionRenderer.sprite != null;
            _interactionRenderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            var pulse = 0.5f + (Mathf.Sin(_pulse * 1.25f) * 0.5f);
            var color = _selected
                ? new Color(1f, 0.63f, 0.20f, Mathf.Lerp(0.44f, 0.58f, pulse))
                : new Color(0.42f, 0.82f, 0.88f, Mathf.Lerp(0.26f, 0.36f, pulse));
            var coverage = _selected ? 0.86f : 0.76f;
            _interactionRenderer.color = color;
            _interactionRoot.localScale = ScaleSpriteToCell(_interactionRenderer.sprite, coverage);
        }

        private void RefreshChargeVisual()
        {
            var identity = TDUiVisualIdentity.GetTower(_tower != null ? _tower.Kind : TDTowerKind.RailLancer);
            var pulse = 0.5f + (Mathf.Sin(_pulse) * 0.5f);
            var profile = PresentationProfile;
            if (_chargeRingRenderer != null)
            {
                var alpha = _chargeVisible ? Mathf.Lerp(0.10f, 0.38f, ChargeProgress) + (pulse * 0.04f) : 0f;
                _chargeRingRenderer.color = new Color(identity.accent.r, identity.accent.g, identity.accent.b, alpha);
                _chargeRingRenderer.transform.localScale = ScaleSpriteToCell(
                    _chargeRingRenderer.sprite,
                    Mathf.Lerp(profile.chargeStartCoverage, profile.chargeEndCoverage, ChargeProgress));
                _chargeRingRenderer.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Sin(_pulse * 0.42f) * (4f + ((int)(_tower != null ? _tower.Kind : TDTowerKind.RailLancer) * 1.5f)));
            }

            var segmentBias = (_tower != null ? (int)_tower.Kind : 0) % ChargeSegmentCount;
            var litSegments = _chargeVisible ? Mathf.CeilToInt(ChargeProgress * ChargeSegmentCount) : 0;
            for (var i = 0; i < _chargeSegments.Count; i++)
            {
                var renderer = _chargeSegments[i];
                renderer.enabled = _chargeVisible && renderer.sprite != null;
                var orderedIndex = (i + segmentBias) % ChargeSegmentCount;
                renderer.color = orderedIndex < litSegments
                    ? new Color(identity.accent.r, identity.accent.g, identity.accent.b, 0.92f)
                    : new Color(0.10f, 0.14f, 0.16f, 0.34f);
            }
        }

        private void SpawnUpgradeRing(Color color)
        {
            var fx = new GameObject("Upgrade_Ring_Fx");
            fx.transform.SetParent(transform, false);
            fx.transform.localPosition = Vector3.zero;
            var renderer = fx.AddComponent<SpriteRenderer>();
            renderer.sprite = TDArtLibrary.GetSoftRingSprite();
            renderer.sortingOrder = TDWorldVisualOrder.PresentationFx;
            var profile = PresentationProfile;
            var start = ScaleSpriteToCell(renderer.sprite, 0.58f);
            var end = ScaleSpriteToCell(renderer.sprite, profile.upgradeEndCoverage);
            fx.AddComponent<TDTransientSpriteFx>().Configure(
                profile.upgradeDuration,
                start,
                end,
                new Color(color.r, color.g, color.b, 0.82f),
                new Color(color.r, color.g, color.b, 0f));
        }

        private void SpawnUpgradeIcon(TDTowerUpgradeBranch branch, Color color)
        {
            var path = branch == TDTowerUpgradeBranch.Damage
                ? TDUiVisualIdentity.DamageIconPath
                : TDUiVisualIdentity.UtilityIconPath;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                return;
            }

            var fx = new GameObject("Upgrade_Branch_Fx");
            fx.transform.SetParent(transform, false);
            fx.transform.localPosition = new Vector3(0f, 0.48f, 0f);
            var renderer = fx.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = TDWorldVisualOrder.PresentationFx + 1;
            var start = ScaleSpriteToCell(sprite, 0.16f);
            var end = ScaleSpriteToCell(sprite, 0.34f);
            fx.AddComponent<TDTransientSpriteFx>().Configure(
                PresentationProfile.upgradeDuration,
                start,
                end,
                new Color(1f, 1f, 1f, 0.98f),
                new Color(color.r, color.g, color.b, 0f));
        }

        private void SpawnTowerIdentityBurst(Color branchColor, int tier)
        {
            var identity = TDUiVisualIdentity.GetTower(_tower != null ? _tower.Kind : TDTowerKind.RailLancer);
            var sprite = Resources.Load<Sprite>(TDProjectile.GetImpactResourcePath(_tower != null ? _tower.Kind : TDTowerKind.RailLancer));
            if (sprite == null)
            {
                return;
            }

            var fx = new GameObject("Upgrade_Identity_Fx");
            fx.transform.SetParent(transform, false);
            fx.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            fx.transform.localRotation = Quaternion.Euler(0f, 0f, tier * 18f);
            var renderer = fx.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = TDWorldVisualOrder.PresentationFx;
            var start = ScaleSpriteToCell(sprite, 0.30f);
            var end = ScaleSpriteToCell(sprite, 0.72f);
            var mixed = Color.Lerp(identity.accent, branchColor, 0.45f);
            fx.AddComponent<TDTransientSpriteFx>().Configure(
                PresentationProfile.upgradeDuration * 0.78f,
                start,
                end,
                new Color(mixed.r, mixed.g, mixed.b, 0.72f),
                new Color(mixed.r, mixed.g, mixed.b, 0f));
        }

        private Vector3 ScaleSpriteToCell(Sprite sprite, float coverage)
        {
            if (sprite == null)
            {
                return Vector3.one * coverage;
            }

            var width = Mathf.Max(0.0001f, sprite.bounds.size.x);
            return Vector3.one * ((_cellSize * coverage) / width);
        }

        private static Color ResolveBranchColor(TDTowerUpgradeBranch branch, float alpha)
        {
            return branch == TDTowerUpgradeBranch.Damage
                ? new Color(1f, 0.50f, 0.18f, alpha)
                : new Color(0.30f, 0.88f, 1f, alpha);
        }
    }
}
