using UnityEngine;

namespace TD
{
    /// <summary>
    /// Economy scaling curves (p12.5.0) — pure functions over the values in
    /// TDBalanceConfig. Tuning lives in the config asset; the shapes of the
    /// curves (Lerp over run progress) are code.
    /// </summary>
    public static class TDEconomyTuning
    {
        public static float CombatBountyShare => TDBalanceConfig.Instance.combatBountyShare;
        public static int DecisionReserveLimit => TDBalanceConfig.Instance.decisionReserveLimit;

        private static float LateIncomeStartProgress => TDBalanceConfig.Instance.lateIncomeStartProgress;
        private static float FinalCombatIncomeMultiplier => TDBalanceConfig.Instance.finalCombatIncomeMultiplier;
        private static float LateClearRewardStartProgress => TDBalanceConfig.Instance.lateClearRewardStartProgress;
        private static float FinalClearRewardMultiplier => TDBalanceConfig.Instance.finalClearRewardMultiplier;
        private static float FinalScenarioPhaseMultiplier => TDBalanceConfig.Instance.finalScenarioPhaseMultiplier;
        private static float ScenarioRepeatStep => TDBalanceConfig.Instance.scenarioRepeatStep;
        private static float MaxScenarioRepeatMultiplier => TDBalanceConfig.Instance.maxScenarioRepeatMultiplier;

        public static float GetUpgradeCostMultiplier(int currentTier)
        {
            var config = TDBalanceConfig.Instance;
            return currentTier switch
            {
                1 => config.tier1UpgradeCostMultiplier,
                2 => config.tier2UpgradeCostMultiplier,
                _ => config.tier0UpgradeCostMultiplier
            };
        }

        public static float GetCombatBountyMultiplier(int waveIndex, int waveCount)
        {
            var progress = GetWaveProgress(waveIndex, waveCount);
            var lateProgress = Mathf.InverseLerp(LateIncomeStartProgress, 1f, progress);
            return CombatBountyShare * Mathf.Lerp(1f, FinalCombatIncomeMultiplier, lateProgress);
        }

        public static float GetWaveClearRewardMultiplier(int waveIndex, int waveCount)
        {
            var progress = GetWaveProgress(waveIndex, waveCount);
            var lateProgress = Mathf.InverseLerp(LateClearRewardStartProgress, 1f, progress);
            return Mathf.Lerp(1f, FinalClearRewardMultiplier, lateProgress);
        }

        public static int GetScenarioCommandCost(
            int baseCost,
            float missionCostMultiplier,
            int waveIndex,
            int waveCount,
            int priorUses)
        {
            if (baseCost <= 0)
            {
                return 0;
            }

            var progress = GetWaveProgress(waveIndex, waveCount);
            var phaseMultiplier = Mathf.Lerp(1f, FinalScenarioPhaseMultiplier, progress);
            var repeatMultiplier = Mathf.Min(
                MaxScenarioRepeatMultiplier,
                1f + Mathf.Max(0, priorUses) * ScenarioRepeatStep);
            return Mathf.Max(
                1,
                Mathf.CeilToInt(baseCost * Mathf.Max(0.01f, missionCostMultiplier) * phaseMultiplier * repeatMultiplier));
        }

        public static int ScaleCombatBounty(int missionScaledReward, int waveIndex, int waveCount)
        {
            return missionScaledReward <= 0
                ? 0
                : Mathf.Max(1, Mathf.RoundToInt(missionScaledReward * GetCombatBountyMultiplier(waveIndex, waveCount)));
        }

        // ── Salvage Derrick (expansion tower 10) ──
        // Roadmap fuse ("单波收入上限保险丝，防止玩家躺赢"): the crane's whole
        // per-wave increment — salvage + aura bounty bonus + kill rebates +
        // supply drops — never exceeds this ceiling. Combat bounty decay
        // applies after the aura, so pre-decay accounting is conservative
        // (the fuse trips no later than the spec's post-decay intent).
        public const int DerrickWaveIncomeCeiling = 45;

        public static int ResolveDerrickWaveIncome(int salvage, int rebatePerKill, int auraKills, int supplyDrop)
        {
            var raw = salvage + (rebatePerKill * Mathf.Max(0, auraKills)) + Mathf.Max(0, supplyDrop);
            return Mathf.Min(raw, DerrickWaveIncomeCeiling);
        }

        public static int ClampDerrickWaveCredit(int alreadyCreditedThisWave, int amount)
        {
            return Mathf.Max(0, Mathf.Min(Mathf.Max(0, amount), DerrickWaveIncomeCeiling - alreadyCreditedThisWave));
        }

        public static float ResolveAuraBountyMultiplier(float bountyBonusPercent, bool scrapProtocol, bool bossOrElite)
        {
            var multiplier = 1f + Mathf.Max(0f, bountyBonusPercent);
            if (scrapProtocol && bossOrElite)
            {
                multiplier *= 1.5f;
            }

            return multiplier;
        }

        public static int ScaleWaveClearReward(int missionScaledReward, int waveIndex, int waveCount)
        {
            return missionScaledReward <= 0
                ? 0
                : Mathf.Max(1, Mathf.RoundToInt(missionScaledReward * GetWaveClearRewardMultiplier(waveIndex, waveCount)));
        }

        public static int GetFinalFiveStartWave(int waveCount)
        {
            return Mathf.Max(1, waveCount - 4);
        }

        private static float GetWaveProgress(int waveIndex, int waveCount)
        {
            if (waveCount <= 1)
            {
                return 0f;
            }

            return Mathf.Clamp01((Mathf.Max(1, waveIndex) - 1f) / (waveCount - 1f));
        }
    }
}
