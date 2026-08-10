using UnityEngine;

namespace TD
{
    public static class TDEconomyTuning
    {
        public const float CombatBountyShare = 0.40f;
        public const int DecisionReserveLimit = 999;

        private const float LateIncomeStartProgress = 0.45f;
        private const float FinalCombatIncomeMultiplier = 0.06f;
        private const float LateClearRewardStartProgress = 0.50f;
        private const float FinalClearRewardMultiplier = 0.50f;
        private const float FinalScenarioPhaseMultiplier = 1.55f;
        private const float ScenarioRepeatStep = 0.22f;
        private const float MaxScenarioRepeatMultiplier = 1.88f;

        public static float GetUpgradeCostMultiplier(int currentTier)
        {
            return currentTier switch
            {
                1 => 1.4f,
                2 => 4.6f,
                _ => 0.8f
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
