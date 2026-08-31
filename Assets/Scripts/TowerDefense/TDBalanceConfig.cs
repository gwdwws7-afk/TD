using UnityEngine;

namespace TD
{
    /// <summary>
    /// Central balance configuration (freeze-period value externalization).
    /// Wave/economy constants that the wave director (S6) and the economy
    /// services consume — tuning happens HERE, not scattered in the manager.
    ///
    /// Tower per-kind sheets stay in TDTower.CreateBaseState for now: the
    /// expansion batch 1 rebuilds them as a 12-kind table anyway (mapping
    /// doc R-C1), so externalizing the 8-kind table twice is wasted motion.
    ///
    /// Every value below is pinned by an existing or adjacent test — changing
    /// one is a balance decision, not a refactor.
    /// </summary>
    [CreateAssetMenu(fileName = "TDBalanceConfig", menuName = "TD/Balance Config")]
    public sealed class TDBalanceConfig : ScriptableObject
    {
        [Header("Run Baselines")]
        [Tooltip("Starting defense budget for a standard mission before mission/chapter rules.")]
        public int defaultDefenseBudget = 120;
        [Tooltip("Starting line integrity before mission/chapter rules.")]
        public int defaultLineIntegrity = 20;

        [Header("Economy — Combat Bounty")]
        [Tooltip("Share of the mission-scaled reward paid per kill (p12.5.0).")]
        public float combatBountyShare = 0.40f;
        [Tooltip("Progress at which late-income decay begins (0-1 of the run).")]
        public float lateIncomeStartProgress = 0.45f;
        [Tooltip("Combat bounty multiplier at 100% progress.")]
        public float finalCombatIncomeMultiplier = 0.06f;

        [Header("Economy — Wave Clear Reward")]
        [Tooltip("Progress at which late-clear decay begins (0-1 of the run).")]
        public float lateClearRewardStartProgress = 0.50f;
        [Tooltip("Clear reward multiplier at 100% progress.")]
        public float finalClearRewardMultiplier = 0.50f;

        [Header("Economy — Scenario Commands")]
        [Tooltip("Scenario command phase multiplier at 100% progress.")]
        public float finalScenarioPhaseMultiplier = 1.55f;
        [Tooltip("Per-repeat cost step for scenario commands.")]
        public float scenarioRepeatStep = 0.22f;
        [Tooltip("Cap on the repeat multiplier.")]
        public float maxScenarioRepeatMultiplier = 1.88f;

        [Header("Economy — Upgrades")]
        [Tooltip("Upgrade cost multiplier per current tier (index 0 fallback for tier 0/3+).")]
        public float tier1UpgradeCostMultiplier = 1.4f;
        public float tier2UpgradeCostMultiplier = 4.6f;
        public float tier0UpgradeCostMultiplier = 0.8f;

        [Header("Economy — Decision Gate")]
        [Tooltip("Ending-budget ceiling for the economy decision-value gate (p12.5.0).")]
        public int decisionReserveLimit = 999;

        // ─── Static access: code reads the instance, tests can construct ───

        private static TDBalanceConfig _instance;

        public static TDBalanceConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<TDBalanceConfig>("Balance/TDBalanceConfig");
                    if (_instance == null)
                    {
                        // Sensible built-in fallback: gameplay never hard-fails
                        // on a missing asset — the defaults ARE the current
                        // in-code values.
                        _instance = CreateInstance<TDBalanceConfig>();
                    }
                }

                return _instance;
            }
        }
    }
}
