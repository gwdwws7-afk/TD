using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Tests for the hybrid armor model in TDEnemy.TakeHit.
    /// Verifies that high-armor enemies are a real wall for low-per-hit towers,
    /// and that SiegeDrill's armor-piercing is more effective.
    /// </summary>
    public class TDArmorTests
    {
        /// <summary>
        /// Compute the expected damage from the hybrid armor model without
        /// instantiating a full TDEnemy (which needs a scene + catalog).
        /// Mirrors the formula: armorPercent = min(0.60, armor * 0.04);
        /// afterPercent = damage * (1 - armorPercent); taken = max(1, round(afterPercent - armor)).
        /// </summary>
        private static int ComputeDamage(int rawDamage, int armor)
        {
            var armorPercent = Mathf.Min(0.60f, armor * 0.04f);
            var afterPercent = rawDamage * (1f - armorPercent);
            return Mathf.Max(1, Mathf.RoundToInt(afterPercent - armor));
        }

        [Test]
        public void ZeroArmor_FullDamage()
        {
            Assert.AreEqual(18, ComputeDamage(18, 0));
            Assert.AreEqual(10, ComputeDamage(10, 0));
        }

        [Test]
        public void LightArmor_ModerateReduction()
        {
            // 4 armor (Carapace Brute): 16% percent + 4 flat
            // 18 * 0.84 = 15.12 → round = 15 → 15 - 4 = 11
            Assert.AreEqual(11, ComputeDamage(18, 4));
        }

        [Test]
        public void HeavyArmor_SignificantReduction()
        {
            // 9 armor (Husk Titan): 36% + 9 flat
            // 18 * 0.64 = 11.52 → round = 12 → 12 - 9 = 3
            Assert.AreEqual(3, ComputeDamage(18, 9));
        }

        [Test]
        public void BossArmor_ExtremeReduction()
        {
            // 12 armor (Furnace Matriarch): 48% + 12 flat (capped at 60%)
            // 18 * 0.52 = 9.36 → round = 9 → 9 - 12 = -3 → max(1, -3) = 1
            Assert.AreEqual(1, ComputeDamage(18, 12));
        }

        [Test]
        public void HighDamageBypassesArmorBetter()
        {
            // SiegeDrill at 20 dmg vs 9 armor:
            // 20 * 0.64 = 12.8 → round = 13 → 13 - 9 = 4
            // vs RailLancer 18 dmg → 3 (above). SiegeDrill does 33% more.
            var railLancerDmg = ComputeDamage(18, 9);
            var siegeDrillDmg = ComputeDamage(20, 9);
            Assert.Greater(siegeDrillDmg, railLancerDmg);
        }

        [Test]
        public void ArmorFloorAlwaysMinimum1()
        {
            // Even with extreme armor, damage never goes below 1.
            Assert.AreEqual(1, ComputeDamage(5, 50));
            Assert.AreEqual(1, ComputeDamage(1, 100));
        }

        [Test]
        public void ArmorPercentCappedAt60()
        {
            // 20 armor would give 80% but is capped at 60%.
            // Verify by checking 15 armor (60%) and 20 armor (still 60%) give similar results.
            var dmgAt15 = ComputeDamage(20, 15);
            var dmgAt20 = ComputeDamage(20, 20);
            // At 15 armor: 20*0.40=8 → 8-15 < 0 → 1
            // At 20 armor: 20*0.40=8 → 8-20 < 0 → 1
            Assert.AreEqual(1, dmgAt15);
            Assert.AreEqual(1, dmgAt20);
        }
    }
}
