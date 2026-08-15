using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Tests for the hybrid armor model. These call the PRODUCTION formula
    /// (TDCombatMath.ResolveArmoredDamage — the same function TDEnemy.TakeHit
    /// uses), not a mirror of it, so any change to the real armor math fails
    /// here instead of silently drifting.
    /// </summary>
    public class TDArmorTests
    {
        [Test]
        public void ZeroArmor_FullDamage()
        {
            Assert.AreEqual(18, TDCombatMath.ResolveArmoredDamage(18, 0));
            Assert.AreEqual(10, TDCombatMath.ResolveArmoredDamage(10, 0));
        }

        [Test]
        public void LightArmor_ModerateReduction()
        {
            // 4 armor (Carapace Brute): 16% percent + 4 flat
            // 18 * 0.84 = 15.12 → round = 15 → 15 - 4 = 11
            Assert.AreEqual(11, TDCombatMath.ResolveArmoredDamage(18, 4));
        }

        [Test]
        public void HeavyArmor_SignificantReduction()
        {
            // 9 armor (Husk Titan): 36% + 9 flat
            // 18 * 0.64 = 11.52 → round = 12 → 12 - 9 = 3
            Assert.AreEqual(3, TDCombatMath.ResolveArmoredDamage(18, 9));
        }

        [Test]
        public void BossArmor_ExtremeReduction()
        {
            // 12 armor (Furnace Matriarch): 48% + 12 flat
            // 18 * 0.52 = 9.36 → round = 9 → 9 - 12 = -3 → floored at 1
            Assert.AreEqual(1, TDCombatMath.ResolveArmoredDamage(18, 12));
        }

        [Test]
        public void HighDamageBypassesArmorBetter()
        {
            // SiegeDrill at 20 dmg vs 9 armor: 20 * 0.64 = 12.8 → 13 → 13-9 = 4,
            // vs RailLancer 18 dmg → 3. Armor-piercing profile does 33% more.
            var railLancerDmg = TDCombatMath.ResolveArmoredDamage(18, 9);
            var siegeDrillDmg = TDCombatMath.ResolveArmoredDamage(20, 9);
            Assert.Greater(siegeDrillDmg, railLancerDmg);
        }

        [Test]
        public void ArmorFloorAlwaysMinimum1()
        {
            Assert.AreEqual(1, TDCombatMath.ResolveArmoredDamage(5, 50));
            Assert.AreEqual(1, TDCombatMath.ResolveArmoredDamage(1, 100));
        }

        [Test]
        public void ArmorPercentCappedAt60()
        {
            // The percent component caps at 60% (15 armor reaches the cap);
            // past that only the flat subtraction keeps growing.
            var dmgAt15 = TDCombatMath.ResolveArmoredDamage(100, 15);
            Assert.AreEqual(25, dmgAt15, "15 armor = 60% + 15 flat: 100*0.4-15");
        }

        [Test]
        public void Constants_MatchDesignIntents()
        {
            // These constants drive the whole armor curve — pin them so a
            // balance edit can never slip in unnoticed.
            Assert.AreEqual(0.04f, TDCombatMath.ArmorPercentPerPoint);
            Assert.AreEqual(0.60f, TDCombatMath.ArmorPercentCap);
            Assert.AreEqual(1, TDCombatMath.DamageFloor);
        }
    }
}
