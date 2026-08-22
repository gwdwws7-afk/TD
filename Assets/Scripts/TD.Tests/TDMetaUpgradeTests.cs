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
            // Consolation ceilings at ceil(0.99) = 1 — small by design, but
            // any wave progress pays SOMETHING (the floored version paid zero
            // on every difficulty; review P0-4).
            var lateDefeat = TDMetaUpgradeSystem.SettleRunResidue(
                0, TDCampaignDifficultyTier.EmberTrial, false, false, 20, 20);
            Assert.AreEqual(1, lateDefeat);
            var midDefeat = TDMetaUpgradeSystem.SettleRunResidue(
                0, TDCampaignDifficultyTier.Standard, false, false, 15, 20);
            Assert.AreEqual(1, midDefeat, "ceil keeps any positive progress >= 1");
            var noProgress = TDMetaUpgradeSystem.SettleRunResidue(
                0, TDCampaignDifficultyTier.EmberTrial, false, false, 0, 20);
            Assert.AreEqual(0, noProgress, "dying on wave 0 pays nothing");
        }

        [Test]
        public void FirstCapture_DerivationCoversRepeatAndFirstClear()
        {
            // Review P0-3 regression: the shipped derivation compared the
            // POST-record value and paid full rate on every repeat capture.
            System.Func<bool, int, int, bool, bool> f = TDMetaUpgradeSystem.IsFirstDifficultyCapture;
            // Never cleared -> any victory is a first capture.
            Assert.IsTrue(f(false, 0, 0, true), "first Standard clear on a fresh level");
            // Repeat at the same tier -> NOT first.
            Assert.IsFalse(f(true, 0, 0, true), "Standard repeat after Standard clear");
            Assert.IsFalse(f(true, 1, 1, true), "Veteran repeat after Veteran clear");
            Assert.IsFalse(f(true, 2, 2, true), "Ember repeat after Ember clear");
            // Higher tier than ever cleared -> first.
            Assert.IsTrue(f(true, 0, 1, true), "Veteran after only Standard clears");
            Assert.IsTrue(f(true, 1, 2, true), "Ember after Veteran");
            // Lower tier than the record -> repeat.
            Assert.IsFalse(f(true, 2, 0, true), "Standard after Ember");
            // Defeats never count.
            Assert.IsFalse(f(false, 0, 2, false));
            Assert.IsFalse(f(true, 1, 2, false));
        }

        [Test]
        public void Merge_ResidueNeverRefundsSpent()
        {
            // Review P0-5 regression: A earned 130 and bought a1:2 (cost
            // 130, bal 0); B kept the pre-purchase cloud copy (bal 130, no
            // ranks). The merged balance derives from the rank UNION's price
            // — the purchase is permanent, the money stays spent.
            TDMetaUpgradeSystem.MergeResidueBalances("a1:2", 130, "", 130, out var balance, out var lifetime);
            Assert.AreEqual(130, lifetime, "lifetime keeps the higher side");
            Assert.AreEqual(0, balance, "union purchase cost comes out of the shared lifetime");

            // Cross-purchases from two sides both consume: A bought b1:1,
            // B bought a1:1 (40 each, both bal 90 of the same 130 lifetime)
            // -> union a1:1+b1:1 = 80 -> 50 remains.
            TDMetaUpgradeSystem.MergeResidueBalances("b1:1", 130, "a1:1", 130, out var bal2, out var life2);
            Assert.AreEqual(130, life2);
            Assert.AreEqual(50, bal2, "cross-purchases consume from the shared lifetime");

            // Degenerate lifetimes clamp; empty ranks cost nothing.
            TDMetaUpgradeSystem.MergeResidueBalances("", -3, "", 4, out var bal3, out var life3);
            Assert.AreEqual(4, life3);
            Assert.AreEqual(4, bal3);
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
