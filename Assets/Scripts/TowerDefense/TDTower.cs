using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public enum TDTowerKind
    {
        RailLancer = 0,
        CinderMortar = 1,
        FrostCoil = 2,
        ArcWelder = 3,
        SiegeDrill = 4,
        EmberFlak = 5,
        ResonanceBeacon = 6,
        GravSnare = 7
    }

    public enum TDTowerUpgradeBranch
    {
        Damage = 0,
        Utility = 1
    }

    public enum TDResonanceAffinity
    {
        EmberSurge = 0,
        FractureMark = 1,
        Either = 2
    }

    public readonly struct TDTowerBalanceProfile
    {
        public readonly TDTowerKind kind;
        public readonly int buildCost;
        public readonly float range;
        public readonly float shotsPerSecond;
        public readonly int damage;
        public readonly float aoeRadius;
        public readonly int aoeMaxTargets;
        public readonly float aoeMinFalloff;
        public readonly float slowPct;
        public readonly float slowDuration;
        public readonly float heavyMultiplier;

        public TDTowerBalanceProfile(
            TDTowerKind kind,
            int buildCost,
            float range,
            float shotsPerSecond,
            int damage,
            float aoeRadius,
            int aoeMaxTargets,
            float aoeMinFalloff,
            float slowPct,
            float slowDuration,
            float heavyMultiplier)
        {
            this.kind = kind;
            this.buildCost = buildCost;
            this.range = range;
            this.shotsPerSecond = shotsPerSecond;
            this.damage = damage;
            this.aoeRadius = aoeRadius;
            this.aoeMaxTargets = aoeMaxTargets;
            this.aoeMinFalloff = aoeMinFalloff;
            this.slowPct = slowPct;
            this.slowDuration = slowDuration;
            this.heavyMultiplier = heavyMultiplier;
        }
    }

    public sealed class TDTowerSpecializationDefinition
    {
        public readonly string specializationId;
        public readonly TDTowerKind towerKind;
        public readonly TDTowerUpgradeBranch branch;
        public readonly string displayName;
        public readonly string effectSummary;
        public readonly string[] counterTags;
        public readonly TDResonanceAffinity resonanceAffinity;

        public TDTowerSpecializationDefinition(
            string specializationId,
            TDTowerKind towerKind,
            TDTowerUpgradeBranch branch,
            string displayName,
            string effectSummary,
            string[] counterTags,
            TDResonanceAffinity resonanceAffinity)
        {
            this.specializationId = specializationId;
            this.towerKind = towerKind;
            this.branch = branch;
            this.displayName = displayName;
            this.effectSummary = effectSummary;
            this.counterTags = counterTags ?? System.Array.Empty<string>();
            this.resonanceAffinity = resonanceAffinity;
        }
    }

    public sealed class TDTower : MonoBehaviour
    {
        private sealed class TowerState
        {
            public string displayName;
            public int buildCost;
            public float range;
            public float shotsPerSecond;
            public int damage;
            public float projectileSpeed;
            public float aoeRadius;
            public int aoeMaxTargets;
            public float aoeMinFalloff;
            public float slowPct;
            public float slowDuration;
            public float heavyMultiplier;
            public string spritePath;
            public Color fallbackColor;
            public string animationPrefix;
            public int animationFrames;
            public float animationFps;
            public float visualScale;
            public float visualYOffset;
            public int sortingOrder;
            public string baseSpritePath;
            public Color baseTint;
            public float baseScale;
            public float baseYOffset;
            public int baseSortingOrder;
        }

        private static readonly float[] UpgradeDiminishing = { 1f, 0.9f, 0.8f };
        private static readonly TDTowerKind[] BuildOrder =
        {
            TDTowerKind.RailLancer,
            TDTowerKind.CinderMortar,
            TDTowerKind.FrostCoil,
            TDTowerKind.ArcWelder,
            TDTowerKind.SiegeDrill,
            TDTowerKind.EmberFlak,
            TDTowerKind.ResonanceBeacon,
            TDTowerKind.GravSnare
        };

        private static readonly TDTowerSpecializationDefinition[] SpecializationDefinitions =
        {
            new("rail_armor_lance", TDTowerKind.RailLancer, TDTowerUpgradeBranch.Damage, "Armor Lance", "Pre-breaks armor and punishes heavy targets.", new[] { "armored", "heavy", "boss" }, TDResonanceAffinity.EmberSurge),
            new("rail_pinning_rail", TDTowerKind.RailLancer, TDTowerUpgradeBranch.Utility, "Pinning Rail", "Pins and exposes priority runners.", new[] { "fast", "flank", "heavy" }, TDResonanceAffinity.FractureMark),
            new("mortar_cinder_saturation", TDTowerKind.CinderMortar, TDTowerUpgradeBranch.Damage, "Cinder Saturation", "Amplifies swarm splash and low-health burn.", new[] { "swarm", "spawn", "support" }, TDResonanceAffinity.FractureMark),
            new("mortar_ash_denial", TDTowerKind.CinderMortar, TDTowerUpgradeBranch.Utility, "Ash Denial", "Impact zones stagger and expose groups.", new[] { "swarm", "fast", "flank" }, TDResonanceAffinity.FractureMark),
            new("frost_cryo_shatter", TDTowerKind.FrostCoil, TDTowerUpgradeBranch.Damage, "Cryo Shatter", "Shatters slowed, marked, or armored targets.", new[] { "fast", "flank", "armored" }, TDResonanceAffinity.FractureMark),
            new("frost_absolute_zero", TDTowerKind.FrostCoil, TDTowerUpgradeBranch.Utility, "Absolute Zero", "Deep-freeze pulses pin advancing threats.", new[] { "fast", "flank", "boss" }, TDResonanceAffinity.FractureMark),
            new("arc_chain_overload", TDTowerKind.ArcWelder, TDTowerUpgradeBranch.Damage, "Chain Overload", "Adds two stronger chain jumps.", new[] { "swarm", "mixed", "spawn" }, TDResonanceAffinity.FractureMark),
            new("arc_conductive_net", TDTowerKind.ArcWelder, TDTowerUpgradeBranch.Utility, "Conductive Net", "Chain links expose and pin special targets.", new[] { "swarm", "fast", "special" }, TDResonanceAffinity.FractureMark),
            new("siege_core_bore", TDTowerKind.SiegeDrill, TDTowerUpgradeBranch.Damage, "Core Bore", "Cracks armor before a massive bore hit.", new[] { "armored", "heavy", "boss" }, TDResonanceAffinity.EmberSurge),
            new("siege_breach_lock", TDTowerKind.SiegeDrill, TDTowerUpgradeBranch.Utility, "Breach Lock", "Locks breached armor and staggers support lines.", new[] { "armored", "support", "heavy" }, TDResonanceAffinity.EmberSurge),
            new("flak_redline_burst", TDTowerKind.EmberFlak, TDTowerUpgradeBranch.Damage, "Redline Burst", "Executes fast and flanking targets.", new[] { "fast", "flank", "swarm" }, TDResonanceAffinity.FractureMark),
            new("flak_intercept_screen", TDTowerKind.EmberFlak, TDTowerUpgradeBranch.Utility, "Intercept Screen", "Wide stagger bursts intercept runner packs.", new[] { "fast", "flank", "spawn" }, TDResonanceAffinity.FractureMark),
            new("beacon_signal_burn", TDTowerKind.ResonanceBeacon, TDTowerUpgradeBranch.Damage, "Signal Burn", "Burns marked, support, and attrition targets.", new[] { "support", "attrition", "special" }, TDResonanceAffinity.EmberSurge),
            new("beacon_resonance_relay", TDTowerKind.ResonanceBeacon, TDTowerUpgradeBranch.Utility, "Resonance Relay", "Relays marks, exposure, and extra command charge.", new[] { "support", "attrition", "mixed" }, TDResonanceAffinity.Either),
            new("grav_event_horizon", TDTowerKind.GravSnare, TDTowerUpgradeBranch.Damage, "Event Horizon", "Damage scales with mass and route progress.", new[] { "heavy", "fast", "boss" }, TDResonanceAffinity.EmberSurge),
            new("grav_singularity_well", TDTowerKind.GravSnare, TDTowerUpgradeBranch.Utility, "Singularity Well", "Wide gravity pulses pin and expose groups.", new[] { "fast", "flank", "swarm" }, TDResonanceAffinity.FractureMark)
        };

        private readonly List<TDTowerUpgradeBranch> _upgradeHistory = new();
        private TDGameManager _gameManager;
        private TowerState _baseState;
        private TowerState _activeState;
        private Transform _visualRoot;
        private Transform _baseRoot;
        private Transform _shadowRoot;
        private Transform _specializationRoot;
        private SpriteRenderer _specializationRenderer;
        private TDTowerReadability _readability;
        private Color _specializationBaseColor;
        private float _specializationPulse;
        private float _cooldown;
        private TDEnemy _windupTarget;
        private float _windupTimer;
        private float _windupDuration;

        public TDTowerKind Kind { get; private set; }
        public int Tier => _upgradeHistory.Count;
        public bool CanUpgrade => Tier < 3;
        public string DisplayName => _activeState?.displayName ?? Kind.ToString();
        public int Damage => _activeState?.damage ?? 0;
        public float AttackRange => _activeState?.range ?? 0f;
        public float ShotsPerSecond => _activeState?.shotsPerSecond ?? 0f;
        public float AoeRadius => _activeState?.aoeRadius ?? 0f;
        public int AoeMaxTargets => _activeState?.aoeMaxTargets ?? 1;
        public float SlowPct => _activeState?.slowPct ?? 0f;
        public float SlowDuration => _activeState?.slowDuration ?? 0f;
        public float HeavyMultiplier => _activeState?.heavyMultiplier ?? 1f;
        public int DamageBranchCount => CountUpgradeBranch(TDTowerUpgradeBranch.Damage);
        public int UtilityBranchCount => CountUpgradeBranch(TDTowerUpgradeBranch.Utility);
        public bool IsDamageSpecialist => DamageBranchCount >= 2;
        public bool IsUtilitySpecialist => UtilityBranchCount >= 2;
        public TDTowerSpecializationDefinition ActiveSpecialization => IsDamageSpecialist
            ? GetSpecializationDefinition(Kind, TDTowerUpgradeBranch.Damage)
            : IsUtilitySpecialist
                ? GetSpecializationDefinition(Kind, TDTowerUpgradeBranch.Utility)
                : null;
        public string SpecializationLabel => BuildSpecializationLabel(Kind, _upgradeHistory);
        public string SpecializationEffectLabel => BuildSpecializationEffectLabel(Kind, _upgradeHistory);
        public Vector2Int GridCell { get; private set; }
        public string AnalyticsId => gameObject != null ? gameObject.name : $"Tower_{GridCell.x}_{GridCell.y}";
        public TDTowerReadability Readability => _readability;
        public bool HasFoundation
        {
            get
            {
                var renderer = _baseRoot != null ? _baseRoot.GetComponent<SpriteRenderer>() : null;
                return renderer != null && renderer.enabled && renderer.sprite != null;
            }
        }

        public static IReadOnlyList<TDTowerKind> GetBuildOrder()
        {
            return BuildOrder;
        }

        public static IReadOnlyList<TDTowerSpecializationDefinition> GetSpecializationDefinitions()
        {
            return SpecializationDefinitions;
        }

        public static TDTowerSpecializationDefinition GetSpecializationDefinition(TDTowerKind kind, TDTowerUpgradeBranch branch)
        {
            for (var i = 0; i < SpecializationDefinitions.Length; i++)
            {
                var definition = SpecializationDefinitions[i];
                if (definition.towerKind == kind && definition.branch == branch)
                {
                    return definition;
                }
            }

            return null;
        }

        public static bool TryParseTowerId(string towerId, out TDTowerKind kind)
        {
            switch (towerId)
            {
                case "rail_lancer_tower":
                    kind = TDTowerKind.RailLancer;
                    return true;
                case "cinder_mortar_tower":
                    kind = TDTowerKind.CinderMortar;
                    return true;
                case "frost_coil_tower":
                    kind = TDTowerKind.FrostCoil;
                    return true;
                case "arc_welder_tower":
                    kind = TDTowerKind.ArcWelder;
                    return true;
                case "siege_drill_tower":
                    kind = TDTowerKind.SiegeDrill;
                    return true;
                case "ember_flak_tower":
                    kind = TDTowerKind.EmberFlak;
                    return true;
                case "resonance_beacon_tower":
                    kind = TDTowerKind.ResonanceBeacon;
                    return true;
                case "grav_snare_tower":
                    kind = TDTowerKind.GravSnare;
                    return true;
                default:
                    kind = TDTowerKind.RailLancer;
                    return false;
            }
        }

        public static string GetTowerId(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => "rail_lancer_tower",
                TDTowerKind.CinderMortar => "cinder_mortar_tower",
                TDTowerKind.FrostCoil => "frost_coil_tower",
                TDTowerKind.ArcWelder => "arc_welder_tower",
                TDTowerKind.SiegeDrill => "siege_drill_tower",
                TDTowerKind.EmberFlak => "ember_flak_tower",
                TDTowerKind.ResonanceBeacon => "resonance_beacon_tower",
                TDTowerKind.GravSnare => "grav_snare_tower",
                _ => "rail_lancer_tower"
            };
        }

        public static string GetDisplayName(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => "Rail Lancer",
                TDTowerKind.CinderMortar => "Cinder Mortar",
                TDTowerKind.FrostCoil => "Frost Coil",
                TDTowerKind.ArcWelder => "Arc Welder",
                TDTowerKind.SiegeDrill => "Siege Drill",
                TDTowerKind.EmberFlak => "Ember Flak",
                TDTowerKind.ResonanceBeacon => "Resonance Beacon",
                TDTowerKind.GravSnare => "Grav Snare",
                _ => kind.ToString()
            };
        }

        public static int GetBuildCost(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => 40,
                TDTowerKind.CinderMortar => 55,
                TDTowerKind.FrostCoil => 45,
                TDTowerKind.ArcWelder => 62,
                TDTowerKind.SiegeDrill => 68,
                TDTowerKind.EmberFlak => 58,
                TDTowerKind.ResonanceBeacon => 70,
                TDTowerKind.GravSnare => 76,
                _ => 40
            };
        }

        public static float GetBaseRange(TDTowerKind kind)
        {
            return Mathf.Max(0f, CreateBaseState(kind).range);
        }

        public static TDTowerBalanceProfile GetBalanceProfile(TDTowerKind kind)
        {
            var state = CreateBaseState(kind);
            return new TDTowerBalanceProfile(
                kind,
                state.buildCost,
                state.range,
                state.shotsPerSecond,
                state.damage,
                state.aoeRadius,
                state.aoeMaxTargets,
                state.aoeMinFalloff,
                state.slowPct,
                state.slowDuration,
                state.heavyMultiplier);
        }

        public void Initialize(TDGameManager gameManager, TDTowerKind kind, Vector2Int gridCell = default)
        {
            _gameManager = gameManager;
            Kind = kind;
            GridCell = gridCell;
            _baseState = CreateBaseState(kind);
            RebuildActiveState();
            _readability = GetComponent<TDTowerReadability>();
            if (_readability == null)
            {
                _readability = gameObject.AddComponent<TDTowerReadability>();
            }
            _readability.Initialize(this, _gameManager != null ? _gameManager.CellWorldSize : 1f);
            RefreshVisual();
            _readability.RefreshTier(_upgradeHistory);
            RefreshDepthSorting();
        }

        public int GetUpgradeCost(TDTowerUpgradeBranch branch)
        {
            if (!CanUpgrade)
            {
                return int.MaxValue;
            }

            var tierMultiplier = TDEconomyTuning.GetUpgradeCostMultiplier(Tier);
            var branchFactor = branch == TDTowerUpgradeBranch.Utility ? 1.05f : 1f;
            return Mathf.CeilToInt(_baseState.buildCost * tierMultiplier * branchFactor);
        }

        public bool ApplyUpgrade(TDTowerUpgradeBranch branch)
        {
            if (!CanUpgrade)
            {
                return false;
            }

            _upgradeHistory.Add(branch);
            RebuildActiveState();
            RefreshVisual();
            _readability?.RefreshTier(_upgradeHistory);
            _readability?.PlayUpgrade(branch, Tier);
            return true;
        }

        public string GetUpgradePreviewSummary(TDTowerUpgradeBranch branch)
        {
            var summary = GetUpgradeStatDeltaSummary(branch);
            if (summary == "MAX")
            {
                return summary;
            }

            var specToken = GetSpecializationPreviewToken(branch);
            return string.IsNullOrWhiteSpace(specToken) ? summary : $"{summary} {specToken}";
        }

        public string GetUpgradeStatDeltaSummary(TDTowerUpgradeBranch branch)
        {
            if (!CanUpgrade || _activeState == null)
            {
                return "MAX";
            }

            var before = CloneState(_activeState);
            var previewHistory = new List<TDTowerUpgradeBranch>(_upgradeHistory)
            {
                branch
            };
            var after = BuildStateWithHistory(previewHistory);

            return BuildUpgradeDeltaSummary(before, after);
        }

        private void Update()
        {
            RefreshDepthSorting();
            if (_gameManager == null || _gameManager.IsGameOver)
            {
                _readability?.SetChargeState(false, 0f);
                return;
            }

            UpdateSpecializationVisualPulse();

            if (_cooldown > 0f)
            {
                _cooldown -= Time.deltaTime;
                _readability?.SetChargeState(false, 0f);
                return;
            }

            if (_windupTarget != null)
            {
                _windupTimer = Mathf.Max(0f, _windupTimer - Time.deltaTime);
                var progress = _windupDuration <= 0f ? 1f : 1f - (_windupTimer / _windupDuration);
                _readability?.SetChargeState(true, progress);
                if (_windupTimer > 0f)
                {
                    return;
                }

                var chargedTarget = _windupTarget;
                _windupTarget = null;
                _readability?.SetChargeState(false, 0f);
                if (chargedTarget != null)
                {
                    FireAt(chargedTarget);
                    var fireRateMultiplier = _gameManager.GetTowerFireRateMultiplier(Kind);
                    var shotInterval = 1f / Mathf.Max(0.01f, _activeState.shotsPerSecond * fireRateMultiplier);
                    _cooldown = Mathf.Max(0.03f, shotInterval - _windupDuration);
                }
                return;
            }

            var target = _gameManager.GetPriorityEnemy(transform.position, _activeState.range, Kind);
            if (target == null)
            {
                _readability?.SetChargeState(false, 0f);
                return;
            }

            _windupTarget = target;
            _windupDuration = ResolveWindupDuration();
            _windupTimer = _windupDuration;
            _readability?.SetChargeState(true, 0f);
        }

        private void FireAt(TDEnemy target)
        {
            _readability?.PlayAttack();
            _gameManager?.NotifyTowerFired(Kind);
            var resonanceDamageMultiplier = _gameManager != null ? _gameManager.GetTowerDamageMultiplier(Kind) : 1f;
            var resonanceProjectileSpeed = _gameManager != null ? _gameManager.GetProjectileSpeedMultiplier(Kind) : 1f;
            var resonanceAoeRadius = _gameManager != null ? _gameManager.GetAoeRadiusMultiplier(Kind) : 1f;
            var resonanceSlowStrength = _gameManager != null ? _gameManager.GetSlowStrengthMultiplier(Kind) : 1f;
            var resonanceSlowDurationBonus = _gameManager != null ? _gameManager.GetSlowDurationBonus(Kind) : 0f;
            var damage = Mathf.RoundToInt(_activeState.damage * GetDamageMultiplier(target) * resonanceDamageMultiplier);

            var shot = new GameObject("Projectile");
            shot.transform.position = transform.position;
            shot.transform.SetParent(_gameManager.transform, true);
            shot.transform.localScale = Vector3.one * 1.05f;

            var renderer = shot.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = TDWorldVisualOrder.Projectile;
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback("Art/projectile_bolt", new Color(0.95f, 0.92f, 0.28f));

            var projectile = shot.AddComponent<TDProjectile>();
            projectile.Initialize(
                _gameManager,
                target,
                this,
                damage,
                _activeState.projectileSpeed * resonanceProjectileSpeed,
                _activeState.aoeRadius * resonanceAoeRadius,
                _activeState.aoeMaxTargets,
                _activeState.aoeMinFalloff,
                _activeState.slowPct * resonanceSlowStrength,
                _activeState.slowDuration + resonanceSlowDurationBonus,
                IsDamageSpecialist,
                IsUtilitySpecialist);
        }

        private float ResolveWindupDuration()
        {
            return TDTowerPresentationProfiles.Get(Kind).chargeDuration;
        }

        private float GetDamageMultiplier(TDEnemy target)
        {
            var multiplier = 1f;

            switch (Kind)
            {
                case TDTowerKind.RailLancer:
                    if (target.HasTag("heavy"))
                    {
                        multiplier *= _activeState.heavyMultiplier;
                    }
                    break;
                case TDTowerKind.SiegeDrill:
                    if (target.HasTag("armored"))
                    {
                        multiplier *= _activeState.heavyMultiplier * 1.08f;
                    }
                    else if (target.HasTag("heavy"))
                    {
                        multiplier *= _activeState.heavyMultiplier;
                    }
                    break;
                case TDTowerKind.EmberFlak:
                    if (target.HasTag("fast") || target.HasTag("flank"))
                    {
                        multiplier *= 1.15f;
                    }
                    break;
                case TDTowerKind.ArcWelder:
                    if (target.HasTag("swarm"))
                    {
                        multiplier *= 1.12f;
                    }
                    break;
            }

            return multiplier;
        }

        private void RebuildActiveState()
        {
            _activeState = BuildStateWithHistory(_upgradeHistory);
        }

        private TowerState BuildStateWithHistory(IReadOnlyList<TDTowerUpgradeBranch> history)
        {
            var state = CloneState(_baseState);
            var damageBranches = 0;
            var utilityBranches = 0;

            if (history == null)
            {
                return state;
            }

            for (var i = 0; i < history.Count; i++)
            {
                var branch = history[i];
                var factor = UpgradeDiminishing[Mathf.Min(i, UpgradeDiminishing.Length - 1)];
                if (branch == TDTowerUpgradeBranch.Damage)
                {
                    damageBranches++;
                    ApplyDamageBranch(state, factor);
                }
                else
                {
                    utilityBranches++;
                    ApplyUtilityBranch(state, factor);
                }
            }

            ApplySpecializationBonus(state, damageBranches, utilityBranches);
            return state;
        }

        private static void ApplySpecializationBonus(TowerState state, int damageBranches, int utilityBranches)
        {
            if (state == null)
            {
                return;
            }

            if (damageBranches >= 2)
            {
                state.damage = Mathf.RoundToInt(state.damage * 1.08f);
                state.heavyMultiplier += 0.05f;
                if (state.aoeRadius > 0f)
                {
                    state.aoeMinFalloff = Mathf.Clamp01(state.aoeMinFalloff + 0.05f);
                }
            }

            if (utilityBranches >= 2)
            {
                state.range *= 1.06f;
                state.projectileSpeed *= 1.05f;
                if (state.aoeRadius > 0f)
                {
                    state.aoeRadius *= 1.04f;
                }

                if (state.slowPct > 0f)
                {
                    state.slowDuration += 0.18f;
                }
            }
        }

        private int CountUpgradeBranch(TDTowerUpgradeBranch branch)
        {
            var count = 0;
            for (var i = 0; i < _upgradeHistory.Count; i++)
            {
                if (_upgradeHistory[i] == branch)
                {
                    count++;
                }
            }

            return count;
        }

        private string GetSpecializationPreviewToken(TDTowerUpgradeBranch branch)
        {
            var currentCount = CountUpgradeBranch(branch);
            if (currentCount != 1)
            {
                return string.Empty;
            }

            var definition = GetSpecializationDefinition(Kind, branch);
            return definition == null
                ? (branch == TDTowerUpgradeBranch.Damage ? "spec:D" : "spec:U")
                : $"-> {definition.displayName}";
        }

        private static string BuildSpecializationLabel(TDTowerKind kind, IReadOnlyList<TDTowerUpgradeBranch> history)
        {
            if (history == null || history.Count == 0)
            {
                return "Base";
            }

            var damageBranches = 0;
            var utilityBranches = 0;
            for (var i = 0; i < history.Count; i++)
            {
                if (history[i] == TDTowerUpgradeBranch.Damage)
                {
                    damageBranches++;
                }
                else
                {
                    utilityBranches++;
                }
            }

            if (damageBranches >= 2)
            {
                return GetSpecializationDefinition(kind, TDTowerUpgradeBranch.Damage)?.displayName ?? "Damage specialist";
            }

            if (utilityBranches >= 2)
            {
                return GetSpecializationDefinition(kind, TDTowerUpgradeBranch.Utility)?.displayName ?? "Utility specialist";
            }

            if (damageBranches == utilityBranches)
            {
                return "Balanced";
            }

            return damageBranches > utilityBranches ? "Damage leaning" : "Utility leaning";
        }

        private static string BuildSpecializationEffectLabel(TDTowerKind kind, IReadOnlyList<TDTowerUpgradeBranch> history)
        {
            if (history == null || history.Count == 0)
            {
                return "Spec effect: none";
            }

            var damageBranches = 0;
            var utilityBranches = 0;
            for (var i = 0; i < history.Count; i++)
            {
                if (history[i] == TDTowerUpgradeBranch.Damage)
                {
                    damageBranches++;
                }
                else
                {
                    utilityBranches++;
                }
            }

            if (damageBranches >= 2)
            {
                var definition = GetSpecializationDefinition(kind, TDTowerUpgradeBranch.Damage);
                return definition == null
                    ? "Spec effect: threat execute"
                    : $"{definition.effectSummary} [{GetResonanceAffinityLabel(definition.resonanceAffinity)}]";
            }

            if (utilityBranches >= 2)
            {
                var definition = GetSpecializationDefinition(kind, TDTowerUpgradeBranch.Utility);
                return definition == null
                    ? "Spec effect: control field"
                    : $"{definition.effectSummary} [{GetResonanceAffinityLabel(definition.resonanceAffinity)}]";
            }

            return "Spec effect: unlock at D2 or U2";
        }

        public static string GetResonanceAffinityLabel(TDResonanceAffinity affinity)
        {
            return affinity switch
            {
                TDResonanceAffinity.EmberSurge => "Ember",
                TDResonanceAffinity.FractureMark => "Fracture",
                _ => "Either"
            };
        }

        private void ApplyDamageBranch(TowerState state, float factor)
        {
            switch (Kind)
            {
                case TDTowerKind.RailLancer:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.25f * factor)));
                    state.heavyMultiplier += 0.10f * factor;
                    break;
                case TDTowerKind.CinderMortar:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.20f * factor)));
                    state.aoeMinFalloff = Mathf.Clamp01(state.aoeMinFalloff + (0.08f * factor));
                    break;
                case TDTowerKind.FrostCoil:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.16f * factor)));
                    state.shotsPerSecond *= 1f + (0.12f * factor);
                    break;
                case TDTowerKind.ArcWelder:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.22f * factor)));
                    state.aoeMaxTargets += Mathf.Max(1, Mathf.RoundToInt(1f * factor));
                    break;
                case TDTowerKind.SiegeDrill:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.24f * factor)));
                    state.heavyMultiplier += 0.12f * factor;
                    break;
                case TDTowerKind.EmberFlak:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.18f * factor)));
                    state.shotsPerSecond *= 1f + (0.16f * factor);
                    break;
                case TDTowerKind.ResonanceBeacon:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.14f * factor)));
                    state.shotsPerSecond *= 1f + (0.10f * factor);
                    break;
                case TDTowerKind.GravSnare:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.15f * factor)));
                    state.aoeMinFalloff = Mathf.Clamp01(state.aoeMinFalloff + (0.10f * factor));
                    break;
            }
        }

        private void ApplyUtilityBranch(TowerState state, float factor)
        {
            switch (Kind)
            {
                case TDTowerKind.RailLancer:
                    state.range *= 1f + (0.12f * factor);
                    state.projectileSpeed *= 1f + (0.08f * factor);
                    break;
                case TDTowerKind.CinderMortar:
                    state.aoeRadius *= 1f + (0.15f * factor);
                    state.aoeMaxTargets += Mathf.Max(1, Mathf.RoundToInt(1f * factor));
                    break;
                case TDTowerKind.FrostCoil:
                    state.slowPct = Mathf.Clamp(state.slowPct + (0.08f * factor), 0f, 0.70f);
                    state.slowDuration += 0.30f * factor;
                    break;
                case TDTowerKind.ArcWelder:
                    state.range *= 1f + (0.10f * factor);
                    state.aoeRadius *= 1f + (0.10f * factor);
                    break;
                case TDTowerKind.SiegeDrill:
                    state.range *= 1f + (0.08f * factor);
                    state.shotsPerSecond *= 1f + (0.10f * factor);
                    break;
                case TDTowerKind.EmberFlak:
                    state.aoeRadius *= 1f + (0.12f * factor);
                    state.projectileSpeed *= 1f + (0.12f * factor);
                    break;
                case TDTowerKind.ResonanceBeacon:
                    state.range *= 1f + (0.14f * factor);
                    state.slowPct = Mathf.Clamp(state.slowPct + (0.05f * factor), 0f, 0.45f);
                    state.slowDuration += 0.22f * factor;
                    break;
                case TDTowerKind.GravSnare:
                    state.aoeRadius *= 1f + (0.10f * factor);
                    state.slowPct = Mathf.Clamp(state.slowPct + (0.10f * factor), 0f, 0.80f);
                    state.slowDuration += 0.35f * factor;
                    break;
            }
        }

        private static TowerState CreateBaseState(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => new TowerState
                {
                    displayName = "Rail Lancer",
                    buildCost = 40,
                    range = 3.0f,
                    shotsPerSecond = 1.0f,
                    damage = 18,
                    projectileSpeed = 9f,
                    aoeRadius = 0f,
                    aoeMaxTargets = 1,
                    aoeMinFalloff = 1f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1.25f,
                    spritePath = "Art/anim/tower_rail_lancer_00",
                    fallbackColor = new Color(0.20f, 0.38f, 0.80f),
                    animationPrefix = "Art/anim/tower_rail_lancer",
                    animationFrames = 6,
                    animationFps = 7f,
                    visualScale = 0.94f,
                    visualYOffset = -0.10f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(0.60f, 0.74f, 0.84f, 0.92f),
                    baseScale = 0.96f,
                    baseYOffset = -0.10f,
                    baseSortingOrder = 9
                },
                TDTowerKind.CinderMortar => new TowerState
                {
                    displayName = "Cinder Mortar",
                    buildCost = 55,
                    range = 2.7f,
                    shotsPerSecond = 0.50f,
                    damage = 16,
                    projectileSpeed = 7.2f,
                    aoeRadius = 1.2f,
                    aoeMaxTargets = 5,
                    aoeMinFalloff = 0.38f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1f,
                    spritePath = "Art/anim/tower_cinder_mortar_00",
                    fallbackColor = new Color(0.78f, 0.43f, 0.18f),
                    animationPrefix = "Art/anim/tower_cinder_mortar",
                    animationFrames = 6,
                    animationFps = 6f,
                    visualScale = 1.00f,
                    visualYOffset = -0.09f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(0.82f, 0.64f, 0.50f, 0.92f),
                    baseScale = 0.98f,
                    baseYOffset = -0.09f,
                    baseSortingOrder = 9
                },
                TDTowerKind.FrostCoil => new TowerState
                {
                    displayName = "Frost Coil",
                    buildCost = 45,
                    range = 2.6f,
                    shotsPerSecond = 0.8f,
                    damage = 8,
                    projectileSpeed = 8.4f,
                    aoeRadius = 0f,
                    aoeMaxTargets = 1,
                    aoeMinFalloff = 1f,
                    slowPct = 0.30f,
                    slowDuration = 1.5f,
                    heavyMultiplier = 1f,
                    spritePath = "Art/anim/tower_frost_coil_00",
                    fallbackColor = new Color(0.38f, 0.78f, 0.94f),
                    animationPrefix = "Art/anim/tower_frost_coil",
                    animationFrames = 6,
                    animationFps = 7.5f,
                    visualScale = 0.90f,
                    visualYOffset = -0.07f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(0.54f, 0.82f, 0.92f, 0.92f),
                    baseScale = 0.92f,
                    baseYOffset = -0.08f,
                    baseSortingOrder = 9
                },
                TDTowerKind.ArcWelder => new TowerState
                {
                    displayName = "Arc Welder",
                    buildCost = 62,
                    range = 2.7f,
                    shotsPerSecond = 0.85f,
                    damage = 10,
                    projectileSpeed = 8.7f,
                    aoeRadius = 1.0f,
                    aoeMaxTargets = 3,
                    aoeMinFalloff = 0.55f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1f,
                    spritePath = "Art/anim/tower_arc_welder_00",
                    fallbackColor = new Color(0.26f, 0.86f, 0.86f),
                    animationPrefix = "Art/anim/tower_arc_welder",
                    animationFrames = 6,
                    animationFps = 8.5f,
                    visualScale = 0.94f,
                    visualYOffset = -0.09f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(0.49f, 0.84f, 0.88f, 0.92f),
                    baseScale = 0.94f,
                    baseYOffset = -0.08f,
                    baseSortingOrder = 9
                },
                TDTowerKind.SiegeDrill => new TowerState
                {
                    displayName = "Siege Drill",
                    buildCost = 68,
                    range = 2.9f,
                    shotsPerSecond = 0.72f,
                    damage = 20,
                    projectileSpeed = 7.8f,
                    aoeRadius = 0f,
                    aoeMaxTargets = 1,
                    aoeMinFalloff = 1f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1.30f,
                    spritePath = "Art/anim/tower_siege_drill_00",
                    fallbackColor = new Color(0.80f, 0.58f, 0.22f),
                    animationPrefix = "Art/anim/tower_siege_drill",
                    animationFrames = 6,
                    animationFps = 6.6f,
                    visualScale = 0.98f,
                    visualYOffset = -0.08f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(0.84f, 0.72f, 0.48f, 0.92f),
                    baseScale = 0.98f,
                    baseYOffset = -0.08f,
                    baseSortingOrder = 9
                },
                TDTowerKind.EmberFlak => new TowerState
                {
                    displayName = "Ember Flak",
                    buildCost = 58,
                    range = 2.55f,
                    shotsPerSecond = 1.35f,
                    damage = 10,
                    projectileSpeed = 9.2f,
                    aoeRadius = 0.7f,
                    aoeMaxTargets = 3,
                    aoeMinFalloff = 0.62f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1f,
                    spritePath = "Art/anim/tower_ember_flak_00",
                    fallbackColor = new Color(0.95f, 0.51f, 0.26f),
                    animationPrefix = "Art/anim/tower_ember_flak",
                    animationFrames = 6,
                    animationFps = 9f,
                    visualScale = 0.92f,
                    visualYOffset = -0.09f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(0.96f, 0.70f, 0.44f, 0.92f),
                    baseScale = 0.94f,
                    baseYOffset = -0.08f,
                    baseSortingOrder = 9
                },
                TDTowerKind.ResonanceBeacon => new TowerState
                {
                    displayName = "Resonance Beacon",
                    buildCost = 70,
                    range = 3.1f,
                    shotsPerSecond = 0.95f,
                    damage = 9,
                    projectileSpeed = 8.2f,
                    aoeRadius = 0.65f,
                    aoeMaxTargets = 2,
                    aoeMinFalloff = 0.70f,
                    slowPct = 0.16f,
                    slowDuration = 1.1f,
                    heavyMultiplier = 1f,
                    spritePath = "Art/anim/tower_resonance_beacon_00",
                    fallbackColor = new Color(0.52f, 0.86f, 0.56f),
                    animationPrefix = "Art/anim/tower_resonance_beacon",
                    animationFrames = 6,
                    animationFps = 7.6f,
                    visualScale = 0.96f,
                    visualYOffset = -0.08f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(0.66f, 0.88f, 0.68f, 0.92f),
                    baseScale = 0.96f,
                    baseYOffset = -0.08f,
                    baseSortingOrder = 9
                },
                TDTowerKind.GravSnare => new TowerState
                {
                    displayName = "Grav Snare",
                    buildCost = 76,
                    range = 2.85f,
                    shotsPerSecond = 0.70f,
                    damage = 9,
                    projectileSpeed = 7.0f,
                    aoeRadius = 1.1f,
                    aoeMaxTargets = 5,
                    aoeMinFalloff = 0.58f,
                    slowPct = 0.24f,
                    slowDuration = 2.2f,
                    heavyMultiplier = 1f,
                    spritePath = "Art/anim/tower_grav_snare_00",
                    fallbackColor = new Color(0.46f, 0.58f, 0.96f),
                    animationPrefix = "Art/anim/tower_grav_snare",
                    animationFrames = 6,
                    animationFps = 6.8f,
                    visualScale = 0.98f,
                    visualYOffset = -0.08f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(0.62f, 0.70f, 0.94f, 0.92f),
                    baseScale = 0.98f,
                    baseYOffset = -0.08f,
                    baseSortingOrder = 9
                },
                _ => new TowerState()
            };
        }

        private static TowerState CloneState(TowerState source)
        {
            return new TowerState
            {
                displayName = source.displayName,
                buildCost = source.buildCost,
                range = source.range,
                shotsPerSecond = source.shotsPerSecond,
                damage = source.damage,
                projectileSpeed = source.projectileSpeed,
                aoeRadius = source.aoeRadius,
                aoeMaxTargets = source.aoeMaxTargets,
                aoeMinFalloff = source.aoeMinFalloff,
                slowPct = source.slowPct,
                slowDuration = source.slowDuration,
                heavyMultiplier = source.heavyMultiplier,
                spritePath = source.spritePath,
                fallbackColor = source.fallbackColor,
                animationPrefix = source.animationPrefix,
                animationFrames = source.animationFrames,
                animationFps = source.animationFps,
                visualScale = source.visualScale,
                visualYOffset = source.visualYOffset,
                sortingOrder = source.sortingOrder,
                baseSpritePath = source.baseSpritePath,
                baseTint = source.baseTint,
                baseScale = source.baseScale,
                baseYOffset = source.baseYOffset,
                baseSortingOrder = source.baseSortingOrder
            };
        }

        private static string BuildUpgradeDeltaSummary(TowerState before, TowerState after)
        {
            if (before == null || after == null)
            {
                return "-";
            }

            var deltas = new List<string>(4);
            AddIntDelta(deltas, "dmg", before.damage, after.damage);
            AddFloatDelta(deltas, "rng", before.range, after.range, "0.0");
            AddFloatDelta(deltas, "rate", before.shotsPerSecond, after.shotsPerSecond, "0.00");
            AddFloatDelta(deltas, "aoe", before.aoeRadius, after.aoeRadius, "0.0");
            AddIntDelta(deltas, "targets", before.aoeMaxTargets, after.aoeMaxTargets);
            AddPercentDelta(deltas, "slow", before.slowPct, after.slowPct);
            AddFloatDelta(deltas, "slowT", before.slowDuration, after.slowDuration, "0.0");
            AddFloatDelta(deltas, "heavy", before.heavyMultiplier, after.heavyMultiplier, "0.00");

            if (deltas.Count == 0)
            {
                return "role tune";
            }

            var max = Mathf.Min(2, deltas.Count);
            var shown = new List<string>(max);
            for (var i = 0; i < max; i++)
            {
                shown.Add(deltas[i]);
            }

            return string.Join(" ", shown);
        }

        private static void AddIntDelta(List<string> deltas, string label, int before, int after)
        {
            var delta = after - before;
            if (delta != 0)
            {
                deltas.Add($"{label} +{delta}");
            }
        }

        private static void AddFloatDelta(List<string> deltas, string label, float before, float after, string format)
        {
            var delta = after - before;
            if (Mathf.Abs(delta) > 0.005f)
            {
                deltas.Add($"{label} +{delta.ToString(format)}");
            }
        }

        private static void AddPercentDelta(List<string> deltas, string label, float before, float after)
        {
            var delta = after - before;
            if (Mathf.Abs(delta) > 0.005f)
            {
                deltas.Add($"{label} +{Mathf.RoundToInt(delta * 100f)}%");
            }
        }

        private void RefreshVisual()
        {
            ApplyBaseVisual();
            ApplyGroundShadow();

            var visualRoot = GetOrCreateVisualRoot();
            visualRoot.localPosition = new Vector3(0f, _activeState.visualYOffset, 0f);

            var renderer = visualRoot.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            ResolveVisualResourcePaths(out var spritePath, out var animationPrefix, out var animationFrames);
            renderer.sortingOrder = _activeState.sortingOrder;
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback(spritePath, _activeState.fallbackColor);
            renderer.color = ResolveSpecializationTowerTint();
            visualRoot.localScale = ResolveScaleToCellWidth(renderer.sprite, _activeState.visualScale, 1f);
            visualRoot.localPosition = ResolveFoundationAnchoredVisualPosition(renderer);
            ApplySpecializationVisual(renderer.sortingOrder);

            var animator = visualRoot.GetComponent<TDSpriteAnimator>();
            if (!string.IsNullOrWhiteSpace(animationPrefix) && animationFrames > 1)
            {
                if (animator == null)
                {
                    animator = visualRoot.gameObject.AddComponent<TDSpriteAnimator>();
                }

                animator.Configure(animationPrefix, animationFrames, _activeState.animationFps);
            }
            else if (animator != null)
            {
                animator.enabled = false;
            }

            _readability?.RefreshMotionBaseline();

            RefreshDepthSorting();
        }

        private Color ResolveSpecializationTowerTint()
        {
            if (DamageBranchCount >= 2)
            {
                return new Color(1f, 0.91f, 0.76f, 1f);
            }

            if (UtilityBranchCount >= 2)
            {
                return new Color(0.78f, 0.96f, 1f, 1f);
            }

            return Color.white;
        }

        private void ApplySpecializationVisual(int towerSortingOrder)
        {
            if (!TryResolveSpecializationColor(out var color))
            {
                if (_specializationRoot != null)
                {
                    _specializationRoot.gameObject.SetActive(false);
                }

                return;
            }

            var renderer = GetOrCreateSpecializationRenderer();
            var sprite = TDArtLibrary.GetSoftRingSprite();
            renderer.sprite = sprite;
            renderer.sortingOrder = Mathf.Max(1, towerSortingOrder - 2);
            _specializationBaseColor = color;
            _specializationRoot.gameObject.SetActive(sprite != null);
            _specializationRoot.localPosition = Vector3.zero;
            _specializationRoot.localScale = ResolveScaleToCellWidth(sprite, 1.18f, 1.18f);
            UpdateSpecializationVisualPulse();
        }

        private void UpdateSpecializationVisualPulse()
        {
            if (_specializationRoot == null || _specializationRenderer == null || !_specializationRoot.gameObject.activeSelf)
            {
                return;
            }

            _specializationPulse += Time.deltaTime * 3.2f;
            var pulse = 0.5f + (Mathf.Sin(_specializationPulse) * 0.5f);
            var alpha = Mathf.Lerp(0.28f, 0.54f, pulse);
            var scale = Mathf.Lerp(0.98f, 1.05f, pulse);
            _specializationRenderer.color = new Color(
                _specializationBaseColor.r,
                _specializationBaseColor.g,
                _specializationBaseColor.b,
                alpha);
            _specializationRoot.localScale = ResolveScaleToCellWidth(_specializationRenderer.sprite, 1.18f * scale, 1.18f * scale);
        }

        private bool TryResolveSpecializationColor(out Color color)
        {
            if (DamageBranchCount >= 2)
            {
                color = new Color(1f, 0.54f, 0.18f, 1f);
                return true;
            }

            if (UtilityBranchCount >= 2)
            {
                color = new Color(0.34f, 0.92f, 1f, 1f);
                return true;
            }

            color = Color.clear;
            return false;
        }

        private void ResolveVisualResourcePaths(out string spritePath, out string animationPrefix, out int animationFrames)
        {
            spritePath = _activeState.spritePath;
            animationPrefix = _activeState.animationPrefix;
            animationFrames = _activeState.animationFrames;

            if (Tier < 3)
            {
                return;
            }

            var tier3Sprite = BuildTier3SpritePath(_activeState.spritePath);
            if (!string.IsNullOrWhiteSpace(tier3Sprite) && Resources.Load<Sprite>(tier3Sprite) != null)
            {
                spritePath = tier3Sprite;
            }

            if (!string.IsNullOrWhiteSpace(_activeState.animationPrefix))
            {
                var tier3Prefix = _activeState.animationPrefix + "_t3";
                if (Resources.Load<Sprite>($"{tier3Prefix}_00") != null)
                {
                    animationPrefix = tier3Prefix;
                }
            }
        }

        private static string BuildTier3SpritePath(string baseSpritePath)
        {
            if (string.IsNullOrWhiteSpace(baseSpritePath))
            {
                return baseSpritePath;
            }

            var split = baseSpritePath.LastIndexOf('_');
            if (split <= 0 || split + 1 >= baseSpritePath.Length)
            {
                return baseSpritePath;
            }

            var frameSuffix = baseSpritePath.Substring(split + 1);
            if (!int.TryParse(frameSuffix, out _))
            {
                return baseSpritePath;
            }

            return baseSpritePath.Insert(split, "_t3");
        }

        private void ApplyGroundShadow()
        {
            var shadowRenderer = GetOrCreateShadowRenderer();
            if (shadowRenderer == null)
            {
                return;
            }

            var shadowSprite = TDArtLibrary.GetSoftShadowSprite();
            shadowRenderer.enabled = shadowSprite != null;
            if (!shadowRenderer.enabled)
            {
                return;
            }

            shadowRenderer.sprite = shadowSprite;
            shadowRenderer.sortingOrder = Mathf.Max(0, _activeState.sortingOrder - 3);
            // Stronger, warmer contact shadow so the body reads as grounded rather than floating.
            shadowRenderer.color = new Color(0.05f, 0.04f, 0.06f, 0.58f);

            var shadowCoverage = Mathf.Clamp(_activeState.visualScale * 1.0f, 0.62f, 1.15f);
            // Raise the shadow so it kisses the body's bottom anchor (+0.02), giving a tight 0.04 gap
            // that reads as contact rather than the previous 0.10 floating gap.
            _shadowRoot.localPosition = new Vector3(0f, -0.02f, 0f);
            _shadowRoot.localScale = ResolveScaleToCellWidth(shadowSprite, shadowCoverage, shadowCoverage);
        }

        private void ApplyBaseVisual()
        {
            var baseRenderer = GetOrCreateBaseRenderer();
            if (baseRenderer == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_activeState.baseSpritePath))
            {
                baseRenderer.enabled = false;
                return;
            }

            var sprite = Resources.Load<Sprite>(_activeState.baseSpritePath);
            if (sprite == null)
            {
                baseRenderer.enabled = false;
                return;
            }

            baseRenderer.enabled = true;
            baseRenderer.sprite = sprite;
            baseRenderer.color = new Color(0.92f, 0.95f, 0.96f, 0.84f);
            baseRenderer.sortingOrder = _activeState.baseSortingOrder;

            _baseRoot.localPosition = Vector3.zero;
            _baseRoot.localScale = ResolveScaleToCellWidth(sprite, 0.80f, 0.80f);
        }

        private Vector3 ResolveFoundationAnchoredVisualPosition(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return new Vector3(0f, _activeState.visualYOffset, 0f);
            }

            if (Kind == TDTowerKind.FrostCoil ||
                Kind == TDTowerKind.ResonanceBeacon ||
                Kind == TDTowerKind.GravSnare)
            {
                return new Vector3(0f, 0.02f, 0f);
            }

            var scaledBottom = renderer.sprite.bounds.min.y * Mathf.Abs(renderer.transform.localScale.y);
            return new Vector3(0f, -scaledBottom + 0.02f, 0f);
        }

        private void RefreshDepthSorting()
        {
            var bodyOrder = TDWorldVisualOrder.ResolveBodyOrder(transform.position.y);
            if (_visualRoot != null)
            {
                var renderer = _visualRoot.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = bodyOrder;
                }
            }

            if (_baseRoot != null)
            {
                var renderer = _baseRoot.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = bodyOrder - 2;
                }
            }

            if (_shadowRoot != null)
            {
                var renderer = _shadowRoot.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = bodyOrder - 3;
                }
            }

            if (_specializationRenderer != null)
            {
                _specializationRenderer.sortingOrder = bodyOrder - 1;
            }

            _readability?.ApplySorting(bodyOrder);
        }

        private Transform GetOrCreateVisualRoot()
        {
            if (_visualRoot != null)
            {
                return _visualRoot;
            }

            var child = transform.Find("Visual");
            if (child == null)
            {
                var visualObject = new GameObject("Visual");
                child = visualObject.transform;
                child.SetParent(transform, false);
            }

            _visualRoot = child;
            return _visualRoot;
        }

        private SpriteRenderer GetOrCreateBaseRenderer()
        {
            if (_baseRoot == null)
            {
                var baseChild = transform.Find("Base");
                if (baseChild == null)
                {
                    var baseObject = new GameObject("Base");
                    baseChild = baseObject.transform;
                    baseChild.SetParent(transform, false);
                }

                _baseRoot = baseChild;
            }

            var renderer = _baseRoot.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = _baseRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            return renderer;
        }

        private SpriteRenderer GetOrCreateShadowRenderer()
        {
            if (_shadowRoot == null)
            {
                var shadowChild = transform.Find("Shadow");
                if (shadowChild == null)
                {
                    var shadowObject = new GameObject("Shadow");
                    shadowChild = shadowObject.transform;
                    shadowChild.SetParent(transform, false);
                }

                _shadowRoot = shadowChild;
            }

            var renderer = _shadowRoot.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = _shadowRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            return renderer;
        }

        private SpriteRenderer GetOrCreateSpecializationRenderer()
        {
            if (_specializationRoot == null)
            {
                var specChild = transform.Find("SpecAura");
                if (specChild == null)
                {
                    var specObject = new GameObject("SpecAura");
                    specChild = specObject.transform;
                    specChild.SetParent(transform, false);
                }

                _specializationRoot = specChild;
            }

            if (_specializationRenderer == null)
            {
                _specializationRenderer = _specializationRoot.GetComponent<SpriteRenderer>();
                if (_specializationRenderer == null)
                {
                    _specializationRenderer = _specializationRoot.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            return _specializationRenderer;
        }

        private Vector3 ResolveScaleToCellWidth(Sprite sprite, float targetCellCoverage, float fallbackScale)
        {
            if (sprite == null || _gameManager == null)
            {
                return Vector3.one * Mathf.Max(0.1f, fallbackScale);
            }

            var spriteWidth = Mathf.Max(0.0001f, sprite.bounds.size.x);
            var cellSize = Mathf.Max(0.01f, _gameManager.CellWorldSize);
            var targetWidth = Mathf.Max(0.1f, cellSize * Mathf.Clamp(targetCellCoverage, 0.1f, 2f));
            return Vector3.one * (targetWidth / spriteWidth);
        }
    }
}
