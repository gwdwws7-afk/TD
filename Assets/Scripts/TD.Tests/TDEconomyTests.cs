using NUnit.Framework;
using TD;

namespace TD.Tests
{
    /// <summary>
    /// Tests for TDEconomyTuning — combat bounty scaling, wave-clear rewards,
    /// upgrade cost multipliers, and scenario command costs.
    /// </summary>
    public class TDEconomyTests
    {
        [Test]
        public void CombatBountyShare_IsCompressedTo40Percent()
        {
            Assert.AreEqual(0.40f, TDEconomyTuning.CombatBountyShare);
        }

        [Test]
        public void EarlyWaveBounty_IsFullShare()
        {
            // Wave 1 of 20: progress = 0/19 = 0 → lateProgress = 0 → multiplier = 0.40 * 1.0
            var mult = TDEconomyTuning.GetCombatBountyMultiplier(1, 20);
            Assert.AreEqual(0.40f, mult, 0.001f);
        }

        [Test]
        public void LateWaveBounty_IsSeverelyReduced()
        {
            // Wave 18 of 20: progress = 17/19 = 0.89
            // lateProgress = InverseLerp(0.45, 1.0, 0.89) = 0.80
            // multiplier = 0.40 * Lerp(1, 0.06, 0.80) = 0.40 * 0.248 = 0.099
            var mult = TDEconomyTuning.GetCombatBountyMultiplier(18, 20);
            Assert.Less(mult, 0.15f, "Late wave bounty should be < 15% of share");
            Assert.Greater(mult, 0.01f, "Bounty should never be zero");
        }

        [Test]
        public void WaveClearReward_EarlyWave_IsFull()
        {
            var mult = TDEconomyTuning.GetWaveClearRewardMultiplier(1, 20);
            Assert.AreEqual(1.0f, mult, 0.001f);
        }

        [Test]
        public void WaveClearReward_LateWave_IsHalved()
        {
            // Wave 18 of 20: lateProgress = InverseLerp(0.50, 1.0, 0.89) = 0.78
            // multiplier = Lerp(1, 0.50, 0.78) = 0.61
            var mult = TDEconomyTuning.GetWaveClearRewardMultiplier(18, 20);
            Assert.Less(mult, 0.70f);
            Assert.Greater(mult, 0.50f);
        }

        [Test]
        public void UpgradeCost_Tier1_IsOnePointFourMultiplier()
        {
            Assert.AreEqual(1.4f, TDEconomyTuning.GetUpgradeCostMultiplier(1));
        }

        [Test]
        public void UpgradeCost_Tier2_IsFourPointSixMultiplier()
        {
            Assert.AreEqual(4.6f, TDEconomyTuning.GetUpgradeCostMultiplier(2));
        }

        [Test]
        public void UpgradeCost_Tier0_IsZeroPointEightMultiplier()
        {
            Assert.AreEqual(0.8f, TDEconomyTuning.GetUpgradeCostMultiplier(0));
        }

        [Test]
        public void ScenarioCost_ScalesWithWaveProgress()
        {
            var earlyCost = TDEconomyTuning.GetScenarioCommandCost(10, 1f, 1, 20, 0);
            var lateCost = TDEconomyTuning.GetScenarioCommandCost(10, 1f, 18, 20, 0);
            Assert.GreaterOrEqual(lateCost, earlyCost,
                "Scenario cost should not decrease in late waves");
        }

        [Test]
        public void ScenarioCost_ScalesWithRepeatUse()
        {
            var firstUse = TDEconomyTuning.GetScenarioCommandCost(10, 1f, 10, 20, 0);
            var thirdUse = TDEconomyTuning.GetScenarioCommandCost(10, 1f, 10, 20, 2);
            Assert.Greater(thirdUse, firstUse,
                "Scenario cost should increase with repeat use");
        }

        [Test]
        public void FinalFiveStartWave_IsWave16For20Waves()
        {
            Assert.AreEqual(16, TDEconomyTuning.GetFinalFiveStartWave(20));
        }

        [Test]
        public void ScaleCombatBounty_AlwaysAtLeast1()
        {
            var result = TDEconomyTuning.ScaleCombatBounty(1, 18, 20);
            Assert.GreaterOrEqual(result, 1);
        }

        [Test]
        public void ScaleCombatBounty_ZeroReward_ReturnsZero()
        {
            Assert.AreEqual(0, TDEconomyTuning.ScaleCombatBounty(0, 5, 20));
        }
    }
}
