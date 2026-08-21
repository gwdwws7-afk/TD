using System.Collections.Generic;
using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Meta upgrade guardrails (spec: meta-upgrade-system-spec-v1).
    /// Guardrail 1/2 caps are pinned here — any balance edit that slips past
    /// the effect ceilings or the anti-grind gates fails these before QA.
    /// </summary>
    public class TDMetaUpgradeTests
    {
        // ── Guardrail 1: effect ceilings ──

        [Test]
        public void LineA_BudgetBonusCapsAt8()
        {
            Assert.AreEqual(0, TDMetaUpgradeSystem.GetStartingBudgetBonus(0));
            Assert.AreEqual(4, TDMetaUpgradeSystem.GetStartingBudgetBonus(1));
            Assert.AreEqual(8, TDMetaUpgradeSystem.GetStartingBudgetBonus(2));
            Assert.AreEqual(8, TDMetaUpgradeSystem.GetStartingBudgetBonus(3), "ranks above max must not exceed the cap");
        }

        [Test]
        public void LineB_RefundRatioCapsAt68Percent()
        {
            Assert.AreEqual(0.60f, TDMetaUpgradeSystem.GetSellRefundRatio(0), 0.0001f);
            Assert.AreEqual(0.64f, TDMetaUpgradeSystem.GetSellRefundRatio(1), 0.0001f);
            Assert.AreEqual(0.68f, TDMetaUpgradeSystem.GetSellRefundRatio(2), 0.0001f);
            Assert.AreEqual(0.68f, TDMetaUpgradeSystem.GetSellRefundRatio(9), 0.0001f);
        }

        [Test]
        public void LineC_SubsidyCapsAt4Percent()
        {
            Assert.AreEqual(0f, TDMetaUpgradeSystem.GetWaveClearIncomeBonusPercent(0));
            Assert.AreEqual(2f, TDMetaUpgradeSystem.GetWaveClearIncomeBonusPercent(1));
            Assert.AreEqual(4f, TDMetaUpgradeSystem.GetWaveClearIncomeBonusPercent(2));
        }

        [Test]
        public void LineD_PresetsCapAt3_NeverMoreFormationSlots()
        {
            Assert.AreEqual(1, TDMetaUpgradeSystem.GetFormationPresetCount(0));
            Assert.AreEqual(2, TDMetaUpgradeSystem.GetFormationPresetCount(1));
            Assert.AreEqual(3, TDMetaUpgradeSystem.GetFormationPresetCount(2));
            Assert.AreEqual(3, TDMetaUpgradeSystem.GetFormationPresetCount(7));
        }

        // ── Guardrail 2: residue anti-grind gates ──

        [Test]
        public void Residue_FirstCaptureFull_RepeatPaysOneFifth()
        {
            var first = TDMetaUpgradeSystem.SettleRunResidue(
                3, TDCampaignDifficultyTier.Standard, true, true, 20, 20);
            var repeat = TDMetaUpgradeSystem.SettleRunResidue(
                3, TDCampaignDifficultyTier.Standard, false, true, 20, 20);
            Assert.AreEqual(3, first);
            Assert.AreEqual(0, repeat, "3 * 0.2 floors to zero at standard");
            var veteranFirst = TDMetaUpgradeSystem.SettleRunResidue(
                3, TDCampaignDifficultyTier.Veteran, true, true, 20, 20);
            var veteranRepeat = TDMetaUpgradeSystem.SettleRunResidue(
                3, TDCampaignDifficultyTier.Veteran, false, true, 20, 20);
            Assert.AreEqual(4, veteranFirst, "3 * 1.5");
            Assert.AreEqual(0, veteranRepeat, "4.5 * 0.2 = 0.9 floors to zero");
            var emberRepeat = TDMetaUpgradeSystem.SettleRunResidue(
                3, TDCampaignDifficultyTier.EmberTrial, false, true, 20, 20);
            Assert.AreEqual(1, emberRepeat, "3 * 2.2 * 0.2 = 1.32 -> 1");
        }

        [Test]
        public void Residue_DifficultyCoefficients()
        {
            Assert.AreEqual(1f, TDMetaUpgradeSystem.DifficultyCoefficient(TDCampaignDifficultyTier.Standard));
            Assert.AreEqual(1.5f, TDMetaUpgradeSystem.DifficultyCoefficient(TDCampaignDifficultyTier.Veteran));
            Assert.AreEqual(2.2f, TDMetaUpgradeSystem.DifficultyCoefficient(TDCampaignDifficultyTier.EmberTrial));
        }

        [Test]
        public void Residue_DefeatIsConsolationOnly()
        {
            // Full-progress defeat at ember trial: 3 * 2.2 * 0.15 = 0.99 -> 0.
            var lateDefeat = TDMetaUpgradeSystem.SettleRunResidue(
                0, TDCampaignDifficultyTier.EmberTrial, false, false, 20, 20);
            Assert.AreEqual(0, lateDefeat, "the consolation formula floors to zero for normal play");
        }

        [Test]
        public void Residue_SingleRunCap()
        {
            // Hypothetical runaway stars (defensive): never above 60.
            var capped = TDMetaUpgradeSystem.SettleRunResidue(
                999, TDCampaignDifficultyTier.EmberTrial, true, true, 20, 20);
            Assert.LessOrEqual(capped, TDMetaUpgradeSystem.SingleRunResidueCap);
        }

        // ── Rank encoding: roundtrip + tolerance ──

        [Test]
        public void Ranks_RoundtripAndTolerance()
        {
            var ranks = new Dictionary<TDMetaUpgradeSystem.UpgradeLine, int>
            {
                [TDMetaUpgradeSystem.UpgradeLine.A] = 2,
                [TDMetaUpgradeSystem.UpgradeLine.B] = 1
            };
            var encoded = TDMetaUpgradeSystem.EncodeRanks(ranks);
            var parsed = TDMetaUpgradeSystem.ParseRanks(encoded);
            Assert.AreEqual(2, parsed[TDMetaUpgradeSystem.UpgradeLine.A]);
            Assert.AreEqual(1, parsed[TDMetaUpgradeSystem.UpgradeLine.B]);

            // Unknown tokens skipped, out-of-range clamped, junk ignored.
            var messy = TDMetaUpgradeSystem.ParseRanks("a1:2, z9:5, b1:99, broken, c1:-1");
            Assert.AreEqual(2, messy[TDMetaUpgradeSystem.UpgradeLine.A]);
            Assert.AreEqual(2, messy[TDMetaUpgradeSystem.UpgradeLine.B], "rank clamps to MaxRank");
            Assert.IsFalse(messy.ContainsKey(TDMetaUpgradeSystem.UpgradeLine.C));
        }

        [Test]
        public void Purchase_ValidationAndProgression()
        {
            Assert.IsTrue(TDMetaUpgradeSystem.TryGetPurchase(
                string.Empty, TDMetaUpgradeSystem.UpgradeLine.A, 40,
                out var price, out _, out _));
            Assert.AreEqual(40, price);

            Assert.IsFalse(TDMetaUpgradeSystem.TryGetPurchase(
                string.Empty, TDMetaUpgradeSystem.UpgradeLine.A, 39,
                out _, out _, out var refusal));
            Assert.AreEqual("insufficient-residue", refusal);

            var maxed = TDMetaUpgradeSystem.EncodeRanks(
                new Dictionary<TDMetaUpgradeSystem.UpgradeLine, int>
                    { [TDMetaUpgradeSystem.UpgradeLine.A] = 2 });
            Assert.IsFalse(TDMetaUpgradeSystem.TryGetPurchase(
                maxed, TDMetaUpgradeSystem.UpgradeLine.A, 999,
                out _, out _, out refusal));
            Assert.AreEqual("line-complete", refusal);

            var upgraded = TDMetaUpgradeSystem.PurchaseRank(string.Empty, TDMetaUpgradeSystem.UpgradeLine.D);
            var parsed = TDMetaUpgradeSystem.ParseRanks(upgraded);
            Assert.AreEqual(1, parsed[TDMetaUpgradeSystem.UpgradeLine.D]);
        }

        [Test]
        public void Merge_RanksKeepPerLineMax()
        {
            var left = TDMetaUpgradeSystem.ParseRanks("a1:2");
            var right = TDMetaUpgradeSystem.ParseRanks("a1:1,b1:2");
            var merged = TDMetaUpgradeSystem.MergeRanksByMax(left, right);
            Assert.AreEqual(2, merged[TDMetaUpgradeSystem.UpgradeLine.A]);
            Assert.AreEqual(2, merged[TDMetaUpgradeSystem.UpgradeLine.B]);
        }

        [Test]
        public void Meta0_IsExactlyNoEffect()
        {
            // Guardrail 4.1: the published balance baseline IS meta-0.
            Assert.AreEqual(0, TDMetaUpgradeSystem.GetStartingBudgetBonus(0));
            Assert.AreEqual(TDTower.SellRefundRatio, TDMetaUpgradeSystem.GetSellRefundRatio(0), 0.0001f);
            Assert.AreEqual(0f, TDMetaUpgradeSystem.GetWaveClearIncomeBonusPercent(0));
            Assert.AreEqual(1, TDMetaUpgradeSystem.GetFormationPresetCount(0));
        }
    }
}
