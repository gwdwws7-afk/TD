using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public enum TDEnemyThreatLevel
    {
        Routine = 1,
        Tactical = 2,
        Elite = 3,
        Boss = 4
    }

    public enum TDEnemyThreatCategory
    {
        Fast,
        Swarm,
        Armored,
        Support,
        Special,
        Boss
    }

    [DisallowMultipleComponent]
    public sealed class TDEnemyReadability : MonoBehaviour
    {
        private const string AssetRoot = "Art/Combat/P11/";
        private const float StatusIconScale = 0.18f;
        private const float StatusIconSpacing = 0.165f;
        private const float ThreatPipScale = 0.045f;
        private const float ThreatPipSpacing = 0.060f;
        private const float TraitIconScale = 0.052f;

        private static Material _silhouetteMaterial;

        private static readonly string[] StatusPaths =
        {
            AssetRoot + "status_armor_break",
            AssetRoot + "status_exposed",
            AssetRoot + "status_resonance",
            AssetRoot + "status_slow",
            AssetRoot + "status_stagger"
        };

        private TDEnemy _enemy;
        private SpriteRenderer _visualRenderer;
        private Transform _outlineRoot;
        private SpriteRenderer _outlineRenderer;
        private Transform _threatRoot;
        private SpriteRenderer _threatRenderer;
        private Transform _statusRoot;
        private readonly List<SpriteRenderer> _statusRenderers = new();
        private readonly List<SpriteRenderer> _threatPipRenderers = new();
        private Transform _traitRoot;
        private SpriteRenderer _resistanceRenderer;
        private SpriteRenderer _weaknessRenderer;
        private Color _threatColor;
        private float _pulse;
        private bool _presentationVisible = true;

        public TDEnemyThreatLevel ThreatLevel { get; private set; } = TDEnemyThreatLevel.Routine;
        public TDEnemyThreatCategory ThreatCategory { get; private set; } = TDEnemyThreatCategory.Fast;
        public int VisibleStatusCount { get; private set; }
        public bool HasOutline => _outlineRenderer != null && _outlineRenderer.enabled;
        public bool ThreatMarkerVisible => _threatRenderer != null && _threatRenderer.enabled;
        public float ThreatMarkerGapWorld => _threatRoot != null ? _threatRoot.localPosition.y - ResolveVisualTop() : 0f;
        public float ThreatMarkerScale => _threatRoot != null ? _threatRoot.localScale.x : 0f;
        public bool ResistanceVisible => _resistanceRenderer != null && _resistanceRenderer.enabled;
        public bool WeaknessVisible => _weaknessRenderer != null && _weaknessRenderer.enabled;
        public int VisibleTraitCount => (ResistanceVisible ? 1 : 0) + (WeaknessVisible ? 1 : 0);
        public bool VisualPriorityValid =>
            _threatRenderer != null &&
            _statusRenderers.TrueForAll(renderer => renderer == null || renderer.sortingOrder == TDWorldVisualOrder.EnemyStatus) &&
            (_resistanceRenderer == null || _resistanceRenderer.sortingOrder == TDWorldVisualOrder.EnemyTrait) &&
            (_weaknessRenderer == null || _weaknessRenderer.sortingOrder == TDWorldVisualOrder.EnemyTrait) &&
            _threatRenderer.sortingOrder >= TDWorldVisualOrder.EnemyThreat;

        public void Initialize(TDEnemy enemy, SpriteRenderer visualRenderer, float threatCost)
        {
            _enemy = enemy;
            _visualRenderer = visualRenderer;
            ThreatCategory = ResolveThreatCategory(enemy);
            ThreatLevel = ResolveThreatLevel(enemy, threatCost);
            _threatColor = ResolveThreatColor(ThreatCategory);
            _presentationVisible = true;
            _pulse = Random.Range(0f, Mathf.PI * 2f);

            EnsureOutline();
            EnsureThreatMarker();
            EnsureStatusStrip();
            EnsureTraitStrip();
            RefreshVisuals();
        }

        public void SetPresentationVisible(bool visible)
        {
            _presentationVisible = visible;
            SetRendererVisible(_outlineRenderer, visible);
            SetRendererVisible(_threatRenderer, visible && ThreatLevel >= TDEnemyThreatLevel.Tactical);
            for (var i = 0; i < _statusRenderers.Count; i++)
            {
                SetRendererVisible(_statusRenderers[i], false);
            }

            for (var i = 0; i < _threatPipRenderers.Count; i++)
            {
                SetRendererVisible(_threatPipRenderers[i], visible && i < (int)ThreatLevel);
            }

            SetRendererVisible(_resistanceRenderer, false);
            SetRendererVisible(_weaknessRenderer, false);
            VisibleStatusCount = 0;
        }

        private void Update()
        {
            if (!_presentationVisible || _enemy == null || _visualRenderer == null)
            {
                return;
            }

            _pulse += Time.deltaTime * ResolvePulseSpeed();
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            UpdateOutline();
            UpdateStatusStrip();
            UpdateTraitStrip();
            UpdateThreatMarker();
        }

        private void EnsureOutline()
        {
            if (_outlineRenderer != null)
            {
                return;
            }

            var outline = new GameObject("Readability_Outline");
            outline.transform.SetParent(transform, false);
            _outlineRoot = outline.transform;
            _outlineRenderer = outline.AddComponent<SpriteRenderer>();
            _outlineRenderer.sharedMaterial = GetSilhouetteMaterial();
        }

        private void UpdateOutline()
        {
            if (_outlineRoot == null || _outlineRenderer == null || _visualRenderer == null)
            {
                return;
            }

            _outlineRenderer.enabled = _presentationVisible && _visualRenderer.enabled && _visualRenderer.sprite != null;
            if (!_outlineRenderer.enabled)
            {
                return;
            }

            _outlineRenderer.sprite = _visualRenderer.sprite;
            _outlineRenderer.sortingLayerID = _visualRenderer.sortingLayerID;
            _outlineRenderer.sortingOrder = _visualRenderer.sortingOrder - 1;
            _outlineRenderer.flipX = _visualRenderer.flipX;
            _outlineRenderer.flipY = _visualRenderer.flipY;
            _outlineRoot.localPosition = _visualRenderer.transform.localPosition;
            _outlineRoot.localRotation = _visualRenderer.transform.localRotation;

            var pulse = 0.5f + (Mathf.Sin(_pulse) * 0.5f);
            var expansion = ThreatLevel switch
            {
                TDEnemyThreatLevel.Boss => Mathf.Lerp(1.052f, 1.068f, pulse),
                TDEnemyThreatLevel.Elite => Mathf.Lerp(1.040f, 1.052f, pulse),
                TDEnemyThreatLevel.Tactical => 1.032f,
                _ => 1.020f
            };
            _outlineRoot.localScale = _visualRenderer.transform.localScale * expansion;

            if (ThreatLevel == TDEnemyThreatLevel.Routine)
            {
                _outlineRenderer.color = new Color(0.03f, 0.08f, 0.10f, 0.50f);
                return;
            }

            var levelAlpha = ThreatLevel switch
            {
                TDEnemyThreatLevel.Boss => Mathf.Lerp(0.76f, 0.90f, pulse),
                TDEnemyThreatLevel.Elite => Mathf.Lerp(0.64f, 0.78f, pulse),
                _ => 0.60f
            };
            var edgeColor = Color.Lerp(new Color(0.04f, 0.07f, 0.08f, 1f), _threatColor, ThreatLevel == TDEnemyThreatLevel.Tactical ? 0.46f : 0.68f);
            _outlineRenderer.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, levelAlpha);
        }

        private void EnsureThreatMarker()
        {
            if (_threatRenderer == null)
            {
                var marker = new GameObject("Readability_ThreatMarker");
                marker.transform.SetParent(transform, false);
                _threatRoot = marker.transform;
                _threatRenderer = marker.AddComponent<SpriteRenderer>();
                _threatRenderer.sprite = Resources.Load<Sprite>(AssetRoot + ResolveThreatAssetName(ThreatCategory));
                _threatRenderer.sortingOrder = ThreatLevel == TDEnemyThreatLevel.Boss
                    ? TDWorldVisualOrder.EnemyCritical
                    : TDWorldVisualOrder.EnemyThreat;
            }

            if (_threatPipRenderers.Count > 0)
            {
                return;
            }

            var pipSprite = Resources.Load<Sprite>(AssetRoot + "threat_pip");
            for (var i = 0; i < (int)TDEnemyThreatLevel.Boss; i++)
            {
                var pipObject = new GameObject($"Readability_ThreatPip_{i + 1}");
                pipObject.transform.SetParent(transform, false);
                pipObject.transform.localScale = Vector3.one * ThreatPipScale;
                var renderer = pipObject.AddComponent<SpriteRenderer>();
                renderer.sprite = pipSprite;
                renderer.sortingOrder = TDWorldVisualOrder.EnemyThreat;
                renderer.color = _threatColor;
                _threatPipRenderers.Add(renderer);
            }
        }

        private void UpdateThreatMarker()
        {
            if (_threatRenderer == null || _threatRoot == null)
            {
                return;
            }

            var markerVisible = _presentationVisible && ThreatLevel >= TDEnemyThreatLevel.Tactical && _threatRenderer.sprite != null;
            _threatRenderer.enabled = markerVisible;
            var top = ResolveVisualTop();
            var pulse = 0.5f + (Mathf.Sin(_pulse) * 0.5f);

            if (markerVisible)
            {
                var baseScale = ThreatLevel switch
                {
                    TDEnemyThreatLevel.Boss => 0.215f,
                    TDEnemyThreatLevel.Elite => 0.245f,
                    _ => 0.22f
                };
                var markerGap = ThreatLevel switch
                {
                    TDEnemyThreatLevel.Boss => VisibleStatusCount > 0 ? 0.30f : 0.18f,
                    TDEnemyThreatLevel.Elite => VisibleStatusCount > 0 ? 0.34f : 0.24f,
                    _ => VisibleStatusCount > 0 ? 0.32f : 0.22f
                };
                var pulseScale = ThreatLevel >= TDEnemyThreatLevel.Elite ? Mathf.Lerp(0.98f, 1.035f, pulse) : 1f;
                _threatRoot.localPosition = new Vector3(0f, top + markerGap, 0f);
                _threatRoot.localRotation = Quaternion.identity;
                _threatRoot.localScale = Vector3.one * (baseScale * pulseScale);
                var alpha = ThreatLevel == TDEnemyThreatLevel.Boss ? Mathf.Lerp(0.84f, 1f, pulse) : 0.94f;
                _threatRenderer.color = new Color(1f, 1f, 1f, alpha);
            }

            var pipCount = markerVisible ? (int)ThreatLevel : 0;
            var pipStart = -((pipCount - 1) * ThreatPipSpacing) * 0.5f;
            var pipGap = VisibleStatusCount > 0
                ? 0.17f
                : ThreatLevel == TDEnemyThreatLevel.Boss ? 0.07f : 0.11f;
            for (var i = 0; i < _threatPipRenderers.Count; i++)
            {
                var renderer = _threatPipRenderers[i];
                var visible = i < pipCount && renderer.sprite != null;
                renderer.enabled = visible;
                if (!visible)
                {
                    continue;
                }

                renderer.transform.localPosition = new Vector3(pipStart + (i * ThreatPipSpacing), top + pipGap, 0f);
                renderer.transform.localScale = Vector3.one * ThreatPipScale;
                renderer.color = new Color(_threatColor.r, _threatColor.g, _threatColor.b, 0.94f);
            }
        }

        private void EnsureStatusStrip()
        {
            if (_statusRoot == null)
            {
                var strip = new GameObject("Readability_StatusStrip");
                strip.transform.SetParent(transform, false);
                _statusRoot = strip.transform;
            }

            if (_statusRenderers.Count > 0)
            {
                return;
            }

            for (var i = 0; i < StatusPaths.Length; i++)
            {
                var statusObject = new GameObject($"Readability_Status_{i + 1}");
                statusObject.transform.SetParent(_statusRoot, false);
                statusObject.transform.localScale = Vector3.one * StatusIconScale;
                var renderer = statusObject.AddComponent<SpriteRenderer>();
                renderer.sprite = Resources.Load<Sprite>(StatusPaths[i]);
                renderer.sortingOrder = TDWorldVisualOrder.EnemyStatus;
                renderer.enabled = false;
                _statusRenderers.Add(renderer);
            }
        }

        private void EnsureTraitStrip()
        {
            if (_traitRoot == null)
            {
                var strip = new GameObject("Readability_TraitStrip");
                strip.transform.SetParent(transform, false);
                _traitRoot = strip.transform;
            }

            if (_resistanceRenderer == null)
            {
                var resistance = new GameObject("Readability_Resistance");
                resistance.transform.SetParent(_traitRoot, false);
                _resistanceRenderer = resistance.AddComponent<SpriteRenderer>();
                _resistanceRenderer.sprite = ResolveResistanceSprite();
                _resistanceRenderer.sortingOrder = TDWorldVisualOrder.EnemyTrait;
            }

            if (_weaknessRenderer == null)
            {
                var weakness = new GameObject("Readability_Weakness");
                weakness.transform.SetParent(_traitRoot, false);
                _weaknessRenderer = weakness.AddComponent<SpriteRenderer>();
                _weaknessRenderer.sprite = ResolveWeaknessSprite(ThreatCategory);
                _weaknessRenderer.sortingOrder = TDWorldVisualOrder.EnemyTrait;
            }
        }

        private void UpdateTraitStrip()
        {
            if (_traitRoot == null || _enemy == null)
            {
                return;
            }

            var showTraits = _presentationVisible && ThreatLevel >= TDEnemyThreatLevel.Tactical;
            var resistanceVisible = showTraits && _resistanceRenderer != null && _resistanceRenderer.sprite != null;
            var weaknessVisible = showTraits && _weaknessRenderer != null && _weaknessRenderer.sprite != null;
            SetRendererVisible(_resistanceRenderer, resistanceVisible);
            SetRendererVisible(_weaknessRenderer, weaknessVisible);
            if (!resistanceVisible && !weaknessVisible)
            {
                return;
            }

            var top = ResolveVisualTop();
            var halfWidth = ResolveVisualHalfWidth();
            _traitRoot.localPosition = new Vector3(0f, top - 0.10f, 0f);
            if (resistanceVisible)
            {
                _resistanceRenderer.transform.localPosition = new Vector3(-halfWidth - 0.09f, 0f, 0f);
                _resistanceRenderer.transform.localScale = Vector3.one * TraitIconScale;
                _resistanceRenderer.color = new Color(0.88f, 0.93f, 0.96f, 0.94f);
            }

            if (weaknessVisible)
            {
                _weaknessRenderer.transform.localPosition = new Vector3(halfWidth + 0.09f, 0f, 0f);
                _weaknessRenderer.transform.localScale = Vector3.one * TraitIconScale;
                _weaknessRenderer.color = new Color(1f, 0.72f, 0.24f, 0.96f);
            }
        }

        private void UpdateStatusStrip()
        {
            if (_enemy == null || _statusRoot == null || _statusRenderers.Count != StatusPaths.Length)
            {
                return;
            }

            var states = new[]
            {
                _enemy.IsArmorBroken,
                _enemy.IsExposed,
                _enemy.IsMarked,
                _enemy.IsSlowed,
                _enemy.IsStaggered
            };
            VisibleStatusCount = 0;
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i] && _statusRenderers[i].sprite != null)
                {
                    VisibleStatusCount++;
                }
            }

            var top = ResolveVisualTop();
            _statusRoot.localPosition = new Vector3(0f, top + 0.055f, 0f);
            var pulse = 0.5f + (Mathf.Sin(_pulse * 1.35f) * 0.5f);
            _statusRoot.localScale = Vector3.one * Mathf.Lerp(0.98f, 1.025f, pulse);
            var start = -((VisibleStatusCount - 1) * StatusIconSpacing) * 0.5f;
            var activeIndex = 0;

            for (var i = 0; i < states.Length; i++)
            {
                var renderer = _statusRenderers[i];
                var visible = _presentationVisible && states[i] && renderer.sprite != null;
                renderer.enabled = visible;
                if (!visible)
                {
                    continue;
                }

                renderer.transform.localPosition = new Vector3(start + (activeIndex * StatusIconSpacing), 0f, 0f);
                renderer.transform.localRotation = Quaternion.identity;
                renderer.transform.localScale = Vector3.one * StatusIconScale;
                renderer.color = Color.white;
                activeIndex++;
            }
        }

        private float ResolveVisualTop()
        {
            if (_visualRenderer == null || _visualRenderer.sprite == null)
            {
                return 0.42f;
            }

            var localHeight = _visualRenderer.sprite.bounds.extents.y * Mathf.Abs(_visualRenderer.transform.localScale.y);
            return _visualRenderer.transform.localPosition.y + Mathf.Clamp(localHeight, 0.24f, 0.72f);
        }

        private float ResolveVisualHalfWidth()
        {
            if (_visualRenderer == null || _visualRenderer.sprite == null)
            {
                return 0.28f;
            }

            var localWidth = _visualRenderer.sprite.bounds.extents.x *
                             Mathf.Abs(_visualRenderer.transform.localScale.x);
            return Mathf.Clamp(localWidth, 0.22f, 0.62f);
        }

        private Sprite ResolveResistanceSprite()
        {
            if (_enemy == null || (_enemy.ArmorFlat <= 0 && !_enemy.HasAnyTag("armored", "heavy", "durability", "boss")))
            {
                return null;
            }

            return TDUiP132Art.LoadVirtualSprite(TDUiP132Art.IconPath(TDUiP132Icon.Armor));
        }

        private static Sprite ResolveWeaknessSprite(TDEnemyThreatCategory category)
        {
            var icon = category switch
            {
                TDEnemyThreatCategory.Fast => TDUiP132Icon.Slow,
                TDEnemyThreatCategory.Swarm => TDUiP132Icon.Damage,
                TDEnemyThreatCategory.Armored => TDUiP132Icon.ArmorBreak,
                TDEnemyThreatCategory.Support => TDUiP132Icon.Exposed,
                TDEnemyThreatCategory.Special => TDUiP132Icon.Resonance,
                _ => TDUiP132Icon.ArmorBreak
            };
            return TDUiP132Art.LoadVirtualSprite(TDUiP132Art.IconPath(icon));
        }

        private float ResolvePulseSpeed()
        {
            return ThreatLevel switch
            {
                TDEnemyThreatLevel.Boss => 4.6f,
                TDEnemyThreatLevel.Elite => 3.8f,
                TDEnemyThreatLevel.Tactical => 2.8f,
                _ => 2.2f
            };
        }

        private static TDEnemyThreatLevel ResolveThreatLevel(TDEnemy enemy, float threatCost)
        {
            if (enemy != null && enemy.HasAnyTag("boss", "final"))
            {
                return TDEnemyThreatLevel.Boss;
            }

            if ((enemy != null && enemy.HasTag("elite")) || threatCost >= 8f)
            {
                return TDEnemyThreatLevel.Elite;
            }

            if (threatCost >= 3f || (enemy != null && enemy.HasAnyTag(
                    "special", "support", "attrition", "mixed", "armored", "heavy", "spawn", "zone_control")))
            {
                return TDEnemyThreatLevel.Tactical;
            }

            return TDEnemyThreatLevel.Routine;
        }

        private static TDEnemyThreatCategory ResolveThreatCategory(TDEnemy enemy)
        {
            if (enemy == null)
            {
                return TDEnemyThreatCategory.Fast;
            }

            if (enemy.HasAnyTag("boss", "final", "elite"))
            {
                return TDEnemyThreatCategory.Boss;
            }

            if (enemy.HasAnyTag("support", "attrition", "zone_control"))
            {
                return TDEnemyThreatCategory.Support;
            }

            if (enemy.HasAnyTag("armored", "heavy", "durability"))
            {
                return TDEnemyThreatCategory.Armored;
            }

            if (enemy.HasAnyTag("special", "mixed"))
            {
                return TDEnemyThreatCategory.Special;
            }

            if (enemy.HasAnyTag("fast", "flank"))
            {
                return TDEnemyThreatCategory.Fast;
            }

            return TDEnemyThreatCategory.Swarm;
        }

        private static Color ResolveThreatColor(TDEnemyThreatCategory category)
        {
            return category switch
            {
                TDEnemyThreatCategory.Fast => new Color(0.28f, 0.77f, 0.89f, 1f),
                TDEnemyThreatCategory.Swarm => new Color(0.93f, 0.44f, 0.25f, 1f),
                TDEnemyThreatCategory.Armored => new Color(0.91f, 0.71f, 0.26f, 1f),
                TDEnemyThreatCategory.Support => new Color(0.38f, 0.81f, 0.48f, 1f),
                TDEnemyThreatCategory.Special => new Color(0.62f, 0.46f, 0.89f, 1f),
                _ => new Color(0.93f, 0.28f, 0.21f, 1f)
            };
        }

        private static string ResolveThreatAssetName(TDEnemyThreatCategory category)
        {
            return category switch
            {
                TDEnemyThreatCategory.Fast => "threat_fast",
                TDEnemyThreatCategory.Swarm => "threat_swarm",
                TDEnemyThreatCategory.Armored => "threat_armored",
                TDEnemyThreatCategory.Support => "threat_support",
                TDEnemyThreatCategory.Special => "threat_special",
                _ => "threat_boss"
            };
        }

        private static void SetRendererVisible(SpriteRenderer renderer, bool visible)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        private static Material GetSilhouetteMaterial()
        {
            if (_silhouetteMaterial != null)
            {
                return _silhouetteMaterial;
            }

            var shader = Shader.Find("TD/EnemySilhouette") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            _silhouetteMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _silhouetteMaterial;
        }
    }
}
