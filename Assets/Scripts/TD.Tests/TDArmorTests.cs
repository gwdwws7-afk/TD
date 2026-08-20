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
            // 9 armor (Husk Titan): 36% percent + flat capped at half
            // 18 * 0.64 = 11.52 → flat cap ceil(5.76)=6 → round(5.52) = 6
            Assert.AreEqual(6, TDCombatMath.ResolveArmoredDamage(18, 9));
        }

        [Test]
        public void BossArmor_ExtremeReduction()
        {
            // 12 armor (Furnace Matriarch): 48% + flat capped at half
            // 18 * 0.52 = 9.36 → flat cap ceil(4.68)=5 → round(4.36) = 4
            // (was the 1-damage floor before the flat-share cap; L13/L20 fix)
            Assert.AreEqual(4, TDCombatMath.ResolveArmoredDamage(18, 12));
        }

        [Test]
        public void FlatShareCap_KeepsMidTierTowersAboveFloor()
        {
            // The 08-19 collapse profile: FrostCoil 8 vs 5 armor floored at 1;
            // with the cap it deals half its mitigated damage.
            Assert.AreEqual(2, TDCombatMath.ResolveArmoredDamage(8, 5));
            // CinderMortar 16 vs 8 armor: 10.88 → flat 6 → 5 (was 3)
            Assert.AreEqual(5, TDCombatMath.ResolveArmoredDamage(16, 8));
        }

        [Test]
        public void HigherDamage_NeverWorseUnderArmorCap()
        {
            // The cap compresses raw-damage differences at high armor — the
            // anti-armor edge now lives in multipliers and armor break, so
            // pin monotonicity instead of a strict ordering.
            var baseDmg = TDCombatMath.ResolveArmoredDamage(18, 9);
            var higherDmg = TDCombatMath.ResolveArmoredDamage(22, 9);
            Assert.GreaterOrEqual(higherDmg, baseDmg);
            Assert.Greater(TDCombatMath.ResolveArmoredDamage(40, 9), baseDmg);
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
            Assert.AreEqual(0.5f, TDCombatMath.ArmorFlatShareCap);
            Assert.AreEqual(1, TDCombatMath.DamageFloor);
        }
    }
}
