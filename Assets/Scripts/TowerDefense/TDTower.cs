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
        GravSnare = 7,
        // Expansion batch 1 (unlock L04/L09/L14/L18). Values must stay
        // appended: (int)kind-indexed arrays (radial menu) rely on order.
        SlagBurner = 8,
        SalvageDerrick = 9,
        RailBarricade = 10,
        LongRailCannon = 11
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
            public float windupDuration;
            public int damage;
            public float projectileSpeed;
            public float aoeRadius;
            public int aoeMaxTargets;
            public float aoeMinFalloff;
            public float slowPct;
            public float slowDuration;
            public float heavyMultiplier;
            // Slag Burner DoT block (expansion tower 9). Zeros = no burn.
            public int burnLayersPerHit;
            public float burnDamagePerLayer;
            public float burnDuration;
            public float burnSpreadRadius;
            // Salvage Derrick economy block (expansion tower 10). Zeros = no aura.
            public float killBountyAuraRadius;
            public float bountyBonusPercent;
            public int waveSalvageIncome;
            public int killBudgetRebate;
            // Rail Barricade wagon block (expansion tower 11). Zeros = no wagon.
            public int wagonMaxHp;
            public int wagonArmor;
            public int wagonThornsPerSecond;
            public float wagonRepairPerSecond;
            public float wagonSlowFieldRadius;
            public float wagonSlowFieldPercent;
            // Long Rail Cannon pierce block (expansion tower 12). Zero = no pierce.
            public float pierceFalloff;
            public string spritePath;
            public Color fallbackColor;
            public string animationPrefix;
            public int animationFrames;
            public float animationFps;
            public float visualScale;
            public float visualYOffset;
            public int sortingOrder;
            public string baseSpritePath;
            // White since the rebuilt base plates ship their own forged-iron
            // material + amber glow (spec 1e08e46) — the per-kind fake colors
            // were placeholder camouflage. No downstream consumer reads it.
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
            TDTowerKind.GravSnare,
            TDTowerKind.SlagBurner,
            TDTowerKind.SalvageDerrick,
            TDTowerKind.RailBarricade,
            TDTowerKind.LongRailCannon
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
            new("grav_singularity_well", TDTowerKind.GravSnare, TDTowerUpgradeBranch.Utility, "Singularity Well", "Wide gravity pulses pin and expose groups.", new[] { "fast", "flank", "swarm" }, TDResonanceAffinity.FractureMark),
            new("slag_slag_sump", TDTowerKind.SlagBurner, TDTowerUpgradeBranch.Damage, "Slag Sump", "Full burn stacks detonate in one burst.", new[] { "attrition", "heavy", "boss" }, TDResonanceAffinity.EmberSurge),
            new("slag_wildfire_drift", TDTowerKind.SlagBurner, TDTowerUpgradeBranch.Utility, "Wildfire Drift", "Burning kills spread fire to nearby prey.", new[] { "swarm", "spawn", "split" }, TDResonanceAffinity.FractureMark),
            new("derrick_scrap_protocol", TDTowerKind.SalvageDerrick, TDTowerUpgradeBranch.Damage, "Scrap Protocol", "Boss and elite bounties pay 1.5x inside the ring.", new[] { "boss", "elite", "heavy" }, TDResonanceAffinity.Either),
            new("derrick_supply_drop", TDTowerKind.SalvageDerrick, TDTowerUpgradeBranch.Utility, "Supply Drop", "Every wave opens with +3 budget.", new[] { "support", "mixed", "swarm" }, TDResonanceAffinity.Either),
            new("barricade_derailment", TDTowerKind.RailBarricade, TDTowerUpgradeBranch.Damage, "Derailment", "A wrecked wagon detonates: blast, armor break, stall.", new[] { "armored", "heavy" }, TDResonanceAffinity.EmberSurge),
            new("barricade_holding_order", TDTowerKind.RailBarricade, TDTowerUpgradeBranch.Utility, "Holding Order", "Faster rebuilds and a taunt pulse holds the line.", new[] { "fast", "flank", "swarm" }, TDResonanceAffinity.FractureMark),
            new("cannon_full_bore", TDTowerKind.LongRailCannon, TDTowerUpgradeBranch.Damage, "Full Bore", "Zero falloff; the line's last target pays extra.", new[] { "armored", "heavy", "boss" }, TDResonanceAffinity.EmberSurge),
            new("cannon_ballistic_lead", TDTowerKind.LongRailCannon, TDTowerUpgradeBranch.Utility, "Ballistic Lead", "Leads the shot: no fast-enemy misses, opening shots mark.", new[] { "fast", "flank" }, TDResonanceAffinity.FractureMark)
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
        private TDSpriteAnimator _animator;
        private Color _specializationBaseColor;
        private float _specializationPulse;
        private float _cooldown;
        private float _acidDebuffTimer;
        private float _acidDebuffFactor = 1f;
        private int _pierceFirstShotWave = -1;
        private TDEnemy _windupTarget;
        private TDEnemy _cachedTarget;
        private float _targetRescanTimer;
        private float _cadenceCarry;
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
        public int BurnLayersPerHit => _activeState?.burnLayersPerHit ?? 0;
        public float BurnDamagePerLayer => _activeState?.burnDamagePerLayer ?? 0f;
        public float BurnDuration => _activeState?.burnDuration ?? 0f;
        public float BurnSpreadRadius => _activeState?.burnSpreadRadius ?? 0f;
        public float KillBountyAuraRadius => _activeState?.killBountyAuraRadius ?? 0f;
        public float BountyBonusPercent => _activeState?.bountyBonusPercent ?? 0f;
        public int WaveSalvageIncome => _activeState?.waveSalvageIncome ?? 0;
        public int KillBudgetRebate => _activeState?.killBudgetRebate ?? 0;
        public int WagonMaxHp => _activeState?.wagonMaxHp ?? 0;
        public int WagonArmor => _activeState?.wagonArmor ?? 0;
        public int WagonThornsPerSecond => _activeState?.wagonThornsPerSecond ?? 0;
        public float WagonRepairPerSecond => _activeState?.wagonRepairPerSecond ?? 0f;
        public float WagonSlowFieldRadius => _activeState?.wagonSlowFieldRadius ?? 0f;
        public float WagonSlowFieldPercent => _activeState?.wagonSlowFieldPercent ?? 0f;
        public float PierceFalloff => _activeState?.pierceFalloff ?? 0f;
        /// <summary>
        /// Ballistic Lead (utility spec) removes the fast-enemy evasion this
        /// tower's own identity is built on — the one spec allowed to.
        /// </summary>
        public bool IgnoresFastEvade => Kind == TDTowerKind.LongRailCannon && IsUtilitySpecialist;

        /// <summary>
        /// Miss chance vs fast (speed >= 2.2) unslowed enemies for slow-firing
        /// single-target towers. AoE and high fire-rate towers bypass this.
        /// Returns 0 for towers that should never miss (AoE radius > 0, or
        /// fire rate > 1.1/s). For slow single-shot towers (<= 1.0/s), scales
        /// from 0.18 at 1.0/s up to 0.30 at 0.5/s.
        /// </summary>
        public float EvadeableFastEnemyMissChance
        {
            get
            {
                var state = _activeState;
                return state == null ? 0f : TDCombatMath.FastEnemyMissChance(state.shotsPerSecond, state.aoeRadius);
            }
        }

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
                case "slag_burner_tower":
                    kind = TDTowerKind.SlagBurner;
                    return true;
                case "salvage_derrick_tower":
                    kind = TDTowerKind.SalvageDerrick;
                    return true;
                case "rail_barricade_tower":
                    kind = TDTowerKind.RailBarricade;
                    return true;
                case "long_rail_cannon_tower":
                    kind = TDTowerKind.LongRailCannon;
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
                TDTowerKind.SlagBurner => "slag_burner_tower",
                TDTowerKind.SalvageDerrick => "salvage_derrick_tower",
                TDTowerKind.RailBarricade => "rail_barricade_tower",
                TDTowerKind.LongRailCannon => "long_rail_cannon_tower",
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
                TDTowerKind.SlagBurner => "Slag Burner",
                TDTowerKind.SalvageDerrick => "Salvage Derrick",
                TDTowerKind.RailBarricade => "Rail Barricade",
                TDTowerKind.LongRailCannon => "Long Rail Cannon",
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
            _totalInvested = _baseState.buildCost;
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

        public const float SellRefundRatio = 0.6f;

        // Build cost plus every upgrade actually purchased. The ACTUAL refund
        // ratio is meta-line aware — always compute refunds via
        // TDMetaUpgradeSystem.GetSellRefundRatio(manager rank); the old
        // SellRefundValue property here silently ignored it (review P2).
        private int _totalInvested;

        public int TotalInvested => _totalInvested;

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

            _totalInvested += GetUpgradeCost(branch);
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
                if (_cooldown > 0f)
                {
                    return;
                }

                // TD-WINDUP-001: credit the sub-frame overshoot past the
                // cooldown into the next windup — see the windup block below.
                _cadenceCarry = -_cooldown;
                _cooldown = 0f;
                return;
            }

            if (_windupTarget != null)
            {
                _windupTimer -= Time.deltaTime;
                var progress = _windupDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(1f - (_windupTimer / _windupDuration));
                _readability?.SetChargeState(true, progress);
                if (_windupTimer > 0f)
                {
                    return;
                }

                var chargedTarget = _windupTarget;
                _windupTarget = null;
                _readability?.SetChargeState(false, 0f);
                // Corpses are not valid shots: a target that died mid-windup
                // is still a live reference through its death reel + fade,
                // but TakeHit would return zero and the projectile is wasted
                // (review P1). Cadence still advances — no free-fire exploit.
                if (chargedTarget != null && chargedTarget.IsTargetable)
                {
                    FireAt(chargedTarget);
                }

                // Time-exact cadence (TD-WINDUP-001): the frame overshoot past
                // the windup is credited to the cooldown instead of being
                // discarded, so a shot's full cycle lands on the designed
                // interval no matter which frame boundary it crosses. The old
                // frame-quantized timers silently stretched every cycle by up
                // to one frame and let sub-frame timing noise (assembly
                // layout, a Debug.Log line) reorder shots and swing seeded
                // runs by whole waves.
                var windupOvershoot = -_windupTimer;
                var fireRateMultiplier = _gameManager.GetTowerFireRateMultiplier(Kind);
                var shotInterval = 1f / Mathf.Max(0.01f, _activeState.shotsPerSecond * fireRateMultiplier * ResolveAcidFireRateFactor());
                _cooldown = TDCombatMath.ResolvePostWindupCooldown(shotInterval, _windupDuration, windupOvershoot);
                return;
            }

            // Full priority scans are O(all enemies) per tower; with many
            // towers that dominates frame time exactly when waves are
            // densest. Rescan at most every ~0.2s (staggered per tower) and
            // keep firing at the cached target in between.
            _targetRescanTimer -= Time.deltaTime;
            if (_targetRescanTimer <= 0f)
            {
                _cachedTarget = _gameManager.GetPriorityEnemy(transform.position, _activeState.range, Kind);
                _targetRescanTimer = ResolveTargetRescanInterval();
            }

            var target = _cachedTarget;
            if (target != null)
            {
                var rangeSqr = _activeState.range * _activeState.range;
                // Corpse filter (review P1): a dying/escaping enemy stays a
                // non-null reference until destroyed — drop it here so new
                // windups never start on corpses.
                if (!target.IsTargetable ||
                    (target.transform.position - transform.position).sqrMagnitude > rangeSqr)
                {
                    target = _cachedTarget = null;
                }
            }

            if (target == null)
            {
                // No target: the carry is only meaningful for back-to-back
                // shot cycles — drop it so an idle gap can't pre-pay a stale
                // frame into a much later windup (review P2).
                _cadenceCarry = 0f;
                _readability?.SetChargeState(false, 0f);
                return;
            }

            _windupTarget = target;
            _windupDuration = ResolveWindupDuration();
            // Cooldown overshoot pre-pays part of this windup so the cycle
            // stays time-exact across frame boundaries (TD-WINDUP-001).
            _windupTimer = Mathf.Max(0f, _windupDuration - _cadenceCarry);
            _cadenceCarry = 0f;
            _readability?.SetChargeState(true, 0f);
        }

        private float ResolveTargetRescanInterval()
        {
            // Deterministic per-tower jitter staggers rescans across frames.
            var jitter = (GetInstanceID() & 0xF) * 0.0125f;
            return 0.15f + jitter;
        }

        private void FireAt(TDEnemy target)
        {
            _readability?.PlayAttack();
            _animator?.PlayFire();
            _gameManager?.NotifyTowerFired(Kind);

            if (Kind == TDTowerKind.LongRailCannon)
            {
                var cannonDamage = Mathf.RoundToInt(
                    _activeState.damage * GetDamageMultiplier(target) *
                    (_gameManager != null ? _gameManager.GetTowerDamageMultiplier(Kind) : 1f));
                ResolvePierceLine(target, cannonDamage);
                return;
            }
            var resonanceDamageMultiplier = _gameManager != null ? _gameManager.GetTowerDamageMultiplier(Kind) : 1f;
            var resonanceProjectileSpeed = _gameManager != null ? _gameManager.GetProjectileSpeedMultiplier(Kind) : 1f;
            var resonanceAoeRadius = _gameManager != null ? _gameManager.GetAoeRadiusMultiplier(Kind) : 1f;
            var resonanceSlowStrength = _gameManager != null ? _gameManager.GetSlowStrengthMultiplier(Kind) : 1f;
            var resonanceSlowDurationBonus = _gameManager != null ? _gameManager.GetSlowDurationBonus(Kind) : 0f;
            var damage = Mathf.RoundToInt(_activeState.damage * GetDamageMultiplier(target) * resonanceDamageMultiplier);

            var pool = TDObjectPool.Instance;
            TDProjectile projectile;
            SpriteRenderer renderer;
            if (pool != null)
            {
                projectile = pool.GetProjectile();
                renderer = projectile.GetComponent<SpriteRenderer>();
            }
            else
            {
                var shot = new GameObject("Projectile");
                shot.transform.SetParent(_gameManager != null ? _gameManager.transform : null, true);
                renderer = shot.AddComponent<SpriteRenderer>();
                projectile = shot.AddComponent<TDProjectile>();
            }

            projectile.transform.position = transform.position;
            projectile.transform.localScale = Vector3.one * 1.05f;
            renderer.sortingOrder = TDWorldVisualOrder.Projectile;
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback("Art/projectile_bolt", new Color(0.95f, 0.92f, 0.28f));

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

        private const float PierceLineWidth = 0.45f;
        private const int PierceMaxTargets = 12;

        /// <summary>
        /// Segment-cast pierce (expansion tower 12): every enemy within the
        /// line's width resolves at fire time, ordered from the muzzle, each
        /// entry taking the chain's falloff damage through the regular
        /// TakeHit pipeline (fast-enemy evasion rolls per target — the
        /// tower's identity weakness lives there, removed only by Ballistic
        /// Lead via IgnoresFastEvade).
        /// </summary>
        private void ResolvePierceLine(TDEnemy primary, int baseDamage)
        {
            if (_gameManager == null || primary == null || baseDamage <= 0)
            {
                return;
            }

            var origin = transform.position;
            var lineEnd = origin + (primary.transform.position - origin).normalized * _activeState.range;
            var candidates = _gameManager.GetEnemiesInRange(origin, _activeState.range, 24);

            // Shared buffer contract (P1): consume before any other query.
            var lineTargets = new List<TDEnemy>(candidates.Count);
            var projections = new List<float>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var enemy = candidates[i];
                if (enemy == null || !enemy.IsTargetable)
                {
                    continue;
                }

                var toEnemy = enemy.transform.position - origin;
                var line = lineEnd - origin;
                var lengthSqr = line.sqrMagnitude;
                var t = lengthSqr <= 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(toEnemy, line) / lengthSqr);
                var distance = Vector3.Distance(enemy.transform.position, origin + line * t);
                if (distance > PierceLineWidth)
                {
                    continue;
                }

                lineTargets.Add(enemy);
                projections.Add(t);
            }

            if (lineTargets.Count == 0)
            {
                return;
            }

            // Muzzle-first ordering (insertion sort — the line is short).
            for (var i = 1; i < lineTargets.Count; i++)
            {
                var enemy = lineTargets[i];
                var t = projections[i];
                var j = i - 1;
                while (j >= 0 && projections[j] > t)
                {
                    lineTargets[j + 1] = lineTargets[j];
                    projections[j + 1] = projections[j];
                    j--;
                }

                lineTargets[j + 1] = enemy;
                projections[j + 1] = t;
            }

            if (lineTargets.Count > PierceMaxTargets)
            {
                lineTargets.RemoveRange(PierceMaxTargets, lineTargets.Count - PierceMaxTargets);
            }

            var wave = _gameManager.CurrentWaveIndex;
            var isFirstShotOfWave = _pierceFirstShotWave != wave;
            _pierceFirstShotWave = wave;

            var chain = TDCombatMath.ResolvePierceDamageChain(
                baseDamage,
                TDCombatMath.ResolvePierceShotFalloff(IsDamageSpecialist, _activeState.pierceFalloff),
                lineTargets.Count,
                IsDamageSpecialist ? 1.3f : 1f);
            for (var i = 0; i < lineTargets.Count; i++)
            {
                var enemy = lineTargets[i];
                var modified = _gameManager.GetModifiedDamageForEnemy(this, enemy, chain[i]);
                var damageTaken = enemy.TakeHit(modified, 0f, 0f, this);
                if (damageTaken > 0)
                {
                    _gameManager.NotifyEnemyDamaged(this, enemy, damageTaken, 0f, 0f);
                }

                // Ballistic Lead: the wave's opening shot marks its target.
                if (isFirstShotOfWave && IsUtilitySpecialist && i == 0)
                {
                    enemy.SetResonanceMark(1.5f);
                }
            }
        }

        /// <summary>
        /// Acid Blister's death spray (and Echo Harbinger's mimic variant):
        /// towers in the cloud fire slower for the window. Factor-only — the
        /// debuff never touches damage or windup data.
        /// </summary>
        public void ApplyAcidDebuff(float duration, float factor)
        {
            if (duration <= 0f || factor <= 0f || factor >= 1f)
            {
                return;
            }

            _acidDebuffTimer = Mathf.Max(_acidDebuffTimer, duration);
            // Strongest cloud wins; refreshing never dilutes.
            _acidDebuffFactor = Mathf.Min(_acidDebuffFactor == 1f ? factor : _acidDebuffFactor, factor);
        }

        private float ResolveAcidFireRateFactor()
        {
            if (_acidDebuffTimer > 0f)
            {
                _acidDebuffTimer = Mathf.Max(0f, _acidDebuffTimer - Time.deltaTime);
                return _acidDebuffFactor;
            }

            _acidDebuffFactor = 1f;
            return 1f;
        }

        private float ResolveWindupDuration()
        {
            // Combat data — the presentation profile no longer owns pacing, so
            // tuning "feel" there cannot silently shift DPS cadence.
            return _activeState != null ? _activeState.windupDuration : 0f;
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
                case TDTowerKind.SlagBurner:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.18f * factor)));
                    state.burnDamagePerLayer *= 1f + (0.15f * factor);
                    break;
                // Salvage line: flat +6/level (sheet 6/12/18 — no diminishing
                // on the flat stipend).
                case TDTowerKind.SalvageDerrick:
                    state.waveSalvageIncome += 6;
                    break;
                // Armor line: plate +2/level, thorns +4/level (both flat).
                case TDTowerKind.RailBarricade:
                    state.wagonArmor += 2;
                    state.wagonThornsPerSecond += 4;
                    break;
                // Reload line: +22% damage (diminished), falloff -0.1 flat per
                // level (sheet 0.7 -> 0.4).
                case TDTowerKind.LongRailCannon:
                    state.damage = Mathf.RoundToInt(state.damage * (1f + (0.22f * factor)));
                    state.pierceFalloff = Mathf.Max(0.2f, state.pierceFalloff - 0.1f);
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
                case TDTowerKind.SlagBurner:
                    state.burnDuration += 0.4f * factor;
                    state.burnSpreadRadius *= 1f + (0.15f * factor);
                    break;
                // Supply line: ring widens 12%/level (diminished); rebate is a
                // flat +1/level per in-ring kill.
                case TDTowerKind.SalvageDerrick:
                    state.killBountyAuraRadius *= 1f + (0.12f * factor);
                    state.killBudgetRebate += 1;
                    break;
                // Maintenance line: self-repair +4 HP/s per level (flat), slow
                // field +5 percentage points per level (flat).
                case TDTowerKind.RailBarricade:
                    state.wagonRepairPerSecond += 4f;
                    state.wagonSlowFieldPercent = Mathf.Clamp(state.wagonSlowFieldPercent + 0.05f, 0f, 0.5f);
                    break;
                // Fire-control line: range +8%/level, muzzle velocity +15%/level.
                case TDTowerKind.LongRailCannon:
                    state.range *= 1f + (0.08f * factor);
                    state.projectileSpeed *= 1f + (0.15f * factor);
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
                    windupDuration = 0.28f,
                    damage = 18,
                    projectileSpeed = 9f,
                    aoeRadius = 0f,
                    aoeMaxTargets = 1,
                    aoeMinFalloff = 1f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1.0f,
                    spritePath = "Art/anim/tower_rail_lancer_00",
                    fallbackColor = new Color(0.20f, 0.38f, 0.80f),
                    animationPrefix = "Art/anim/tower_rail_lancer",
                    animationFrames = 6,
                    animationFps = 7f,
                    visualScale = 0.94f,
                    visualYOffset = -0.10f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(1f, 1f, 1f, 1f),
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
                    windupDuration = 0.38f,
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
                    baseTint = new Color(1f, 1f, 1f, 1f),
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
                    windupDuration = 0.22f,
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
                    baseTint = new Color(1f, 1f, 1f, 1f),
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
                    windupDuration = 0.20f,
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
                    baseTint = new Color(1f, 1f, 1f, 1f),
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
                    windupDuration = 0.40f,
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
                    baseTint = new Color(1f, 1f, 1f, 1f),
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
                    windupDuration = 0.14f,
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
                    baseTint = new Color(1f, 1f, 1f, 1f),
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
                    windupDuration = 0.25f,
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
                    baseTint = new Color(1f, 1f, 1f, 1f),
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
                    windupDuration = 0.36f,
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
                    baseTint = new Color(1f, 1f, 1f, 1f),
                    baseScale = 0.98f,
                    baseYOffset = -0.08f,
                    baseSortingOrder = 9
                },
                // Expansion batch 1 base states (expansion-tower-sheets-v1).
                // Pierce/burn/aura/wagon behaviors land with their systems;
                // these states carry the sheet baselines so cost/range/cadence
                // tables, the simulator, and tests see the real numbers now.
                TDTowerKind.SlagBurner => new TowerState
                {
                    displayName = "Slag Burner",
                    buildCost = 50,
                    range = 2.2f,
                    shotsPerSecond = 1.1f,
                    windupDuration = 0.18f,
                    damage = 8,
                    projectileSpeed = 8.0f,
                    aoeRadius = 0f,
                    aoeMaxTargets = 1,
                    aoeMinFalloff = 1f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1f,
                    burnLayersPerHit = 3,
                    burnDamagePerLayer = 2.0f,
                    burnDuration = 3.0f,
                    burnSpreadRadius = 1.0f,
                    spritePath = "Art/anim/tower_slag_burner_00",
                    fallbackColor = new Color(0.84f, 0.27f, 0.27f),
                    animationPrefix = "Art/anim/tower_slag_burner",
                    animationFrames = 6,
                    animationFps = 6.5f,
                    visualScale = 0.96f,
                    visualYOffset = -0.09f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(1f, 1f, 1f, 1f),
                    baseScale = 0.96f,
                    baseYOffset = -0.09f,
                    baseSortingOrder = 9
                },
                TDTowerKind.SalvageDerrick => new TowerState
                {
                    displayName = "Salvage Derrick",
                    buildCost = 44,
                    range = 1.8f,
                    shotsPerSecond = 0.9f,
                    windupDuration = 0.24f,
                    damage = 5,
                    projectileSpeed = 7.5f,
                    aoeRadius = 0f,
                    aoeMaxTargets = 1,
                    aoeMinFalloff = 1f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1f,
                    killBountyAuraRadius = 2.5f,
                    bountyBonusPercent = 0.18f,
                    spritePath = "Art/anim/tower_salvage_derrick_00",
                    fallbackColor = new Color(0.50f, 0.78f, 0.43f),
                    animationPrefix = "Art/anim/tower_salvage_derrick",
                    animationFrames = 6,
                    animationFps = 5.5f,
                    visualScale = 1.00f,
                    visualYOffset = -0.09f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(1f, 1f, 1f, 1f),
                    baseScale = 0.98f,
                    baseYOffset = -0.09f,
                    baseSortingOrder = 9
                },
                TDTowerKind.RailBarricade => new TowerState
                {
                    // No ranged attack per the behavior spec — the wagon body
                    // does the intercepting. Placeholder cadence/damage keep
                    // the generic invariants (damage > 0, windup < interval)
                    // valid until TDBlockerWagon replaces targeting.
                    displayName = "Rail Barricade",
                    buildCost = 60,
                    range = 1.2f,
                    shotsPerSecond = 0.5f,
                    windupDuration = 0.30f,
                    damage = 4,
                    projectileSpeed = 6.0f,
                    aoeRadius = 0f,
                    aoeMaxTargets = 1,
                    aoeMinFalloff = 1f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1f,
                    spritePath = "Art/anim/tower_rail_barricade_00",
                    fallbackColor = new Color(0.36f, 0.54f, 0.66f),
                    animationPrefix = "Art/anim/tower_rail_barricade",
                    animationFrames = 6,
                    animationFps = 5.0f,
                    visualScale = 1.02f,
                    visualYOffset = -0.08f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(1f, 1f, 1f, 1f),
                    baseScale = 0.98f,
                    baseYOffset = -0.08f,
                    baseSortingOrder = 9,
                    wagonMaxHp = 240,
                    wagonArmor = 4,
                    wagonThornsPerSecond = 0,
                    wagonRepairPerSecond = 0f,
                    wagonSlowFieldRadius = 1.5f,
                    wagonSlowFieldPercent = 0.10f
                },
                TDTowerKind.LongRailCannon => new TowerState
                {
                    displayName = "Long Rail Cannon",
                    buildCost = 72,
                    range = 4.8f,
                    shotsPerSecond = 0.4f,
                    windupDuration = 0.50f,
                    damage = 34,
                    projectileSpeed = 14f,
                    aoeRadius = 0f,
                    aoeMaxTargets = 1,
                    aoeMinFalloff = 1f,
                    slowPct = 0f,
                    slowDuration = 0f,
                    heavyMultiplier = 1f,
                    spritePath = "Art/anim/tower_long_rail_cannon_00",
                    fallbackColor = new Color(0.42f, 0.36f, 0.91f),
                    animationPrefix = "Art/anim/tower_long_rail_cannon",
                    pierceFalloff = 0.7f,
                    animationFrames = 6,
                    animationFps = 6.0f,
                    visualScale = 1.00f,
                    visualYOffset = -0.10f,
                    sortingOrder = 12,
                    baseSpritePath = "Art/tower_base_plate",
                    baseTint = new Color(1f, 1f, 1f, 1f),
                    baseScale = 0.96f,
                    baseYOffset = -0.10f,
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
                windupDuration = source.windupDuration,
                damage = source.damage,
                projectileSpeed = source.projectileSpeed,
                aoeRadius = source.aoeRadius,
                aoeMaxTargets = source.aoeMaxTargets,
                aoeMinFalloff = source.aoeMinFalloff,
                slowPct = source.slowPct,
                slowDuration = source.slowDuration,
                heavyMultiplier = source.heavyMultiplier,
                burnLayersPerHit = source.burnLayersPerHit,
                burnDamagePerLayer = source.burnDamagePerLayer,
                burnDuration = source.burnDuration,
                burnSpreadRadius = source.burnSpreadRadius,
                killBountyAuraRadius = source.killBountyAuraRadius,
                bountyBonusPercent = source.bountyBonusPercent,
                waveSalvageIncome = source.waveSalvageIncome,
                killBudgetRebate = source.killBudgetRebate,
                wagonMaxHp = source.wagonMaxHp,
                wagonArmor = source.wagonArmor,
                wagonThornsPerSecond = source.wagonThornsPerSecond,
                wagonRepairPerSecond = source.wagonRepairPerSecond,
                wagonSlowFieldRadius = source.wagonSlowFieldRadius,
                wagonSlowFieldPercent = source.wagonSlowFieldPercent,
                pierceFalloff = source.pierceFalloff,
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

            _animator = visualRoot.GetComponent<TDSpriteAnimator>();
            if (!string.IsNullOrWhiteSpace(animationPrefix) && animationFrames > 1)
            {
                if (_animator == null)
                {
                    _animator = visualRoot.gameObject.AddComponent<TDSpriteAnimator>();
                }

                _animator.Configure(animationPrefix, animationFrames, _activeState.animationFps);
                // Fire reel: when a tiered idle prefix is active (T3), prefer
                // its dedicated fire reel — the art batches ship those now. If
                // the tiered reel is ever missing (partial asset import), fall
                // back to the base fire frames so a maxed tower keeps its fire
                // feedback instead of silently losing it.
                var firePrefix = animationPrefix;
                if (firePrefix != _activeState.animationPrefix &&
                    Resources.Load<Sprite>($"{firePrefix}_fire_00") == null)
                {
                    firePrefix = _activeState.animationPrefix;
                }

                _animator.ConfigureFire(firePrefix, 3, 15f);
            }
            else if (_animator != null)
            {
                _animator.enabled = false;
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

            if (Tier < 2)
            {
                return;
            }

            // Tiered skins (spec: tower-t2-visual-spec-v1): T3 wins when its
            // art exists, then T2, then the base form — each tier falls back
            // downward on missing art so partial batches never strip upgrade
            // feedback. Assets land inert until the frames exist.
            if (!TryApplyTierVisual(Tier >= 3 ? "_t3" : "_t2", ref spritePath, ref animationPrefix) &&
                Tier >= 3)
            {
                TryApplyTierVisual("_t2", ref spritePath, ref animationPrefix);
            }
        }

        private bool TryApplyTierVisual(string tierSuffix, ref string spritePath, ref string animationPrefix)
        {
            var applied = false;
            var tierSprite = BuildTierSpritePath(_activeState.spritePath, tierSuffix);
            if (!string.IsNullOrWhiteSpace(tierSprite) && Resources.Load<Sprite>(tierSprite) != null)
            {
                spritePath = tierSprite;
                applied = true;
            }

            if (!string.IsNullOrWhiteSpace(_activeState.animationPrefix))
            {
                var tierPrefix = _activeState.animationPrefix + tierSuffix;
                if (Resources.Load<Sprite>($"{tierPrefix}_00") != null)
                {
                    animationPrefix = tierPrefix;
                    applied = true;
                }
            }

            return applied;
        }

        private static string BuildTierSpritePath(string baseSpritePath, string tierSuffix)
        {
            if (string.IsNullOrWhiteSpace(baseSpritePath) || string.IsNullOrEmpty(tierSuffix))
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

            return baseSpritePath.Insert(split, tierSuffix);
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
