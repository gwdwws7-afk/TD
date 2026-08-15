using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Tests for the fast-enemy evasion system. The miss-chance curve is
    /// tested against the PRODUCTION function (TDCombatMath.
    /// FastEnemyMissChance — the same one TDTower.EvadeableFastEnemyMissChance
    /// uses), and the tower profiles are read through the public
    /// GetBalanceProfile API instead of reflection.
    /// </summary>
    public class TDEvasionTests
    {
        [Test]
        public void MissChance_ZeroForAoeAndHighFireRate()
        {
            Assert.AreEqual(0f, TDCombatMath.FastEnemyMissChance(2.0f, 0f), "fire rate above 1.1/s never misses");
            Assert.AreEqual(0f, TDCombatMath.FastEnemyMissChance(0.5f, 1.5f), "AoE towers never miss");
        }

        [Test]
        public void MissChance_18PercentAtOneShotPerSecond()
        {
            Assert.AreEqual(0.18f, TDCombatMath.FastEnemyMissChance(1.0f, 0f), 0.0001f);
        }

        [Test]
        public void MissChance_ScalesUpTo30PercentAtHalfShotPerSecond()
        {
            Assert.AreEqual(0.30f, TDCombatMath.FastEnemyMissChance(0.5f, 0f), 0.0001f);
            // Midpoint interpolation: 0.75/s sits halfway between 0.18 and 0.30.
            Assert.AreEqual(0.24f, TDCombatMath.FastEnemyMissChance(0.75f, 0f), 0.001f);
        }

        [Test]
        public void RailLancerProfile_HasEvasionVsFast()
        {
            // RailLancer: 1.0 shots/sec, single-target → ~18% miss vs fast.
            var profile = TDTower.GetBalanceProfile(TDTowerKind.RailLancer);
            Assert.GreaterOrEqual(profile.shotsPerSecond, 1.0f);
            Assert.LessOrEqual(profile.shotsPerSecond, 1.1f);
            Assert.AreEqual(0f, profile.aoeRadius);
            Assert.AreEqual(
                0.18f,
                TDCombatMath.FastEnemyMissChance(profile.shotsPerSecond, profile.aoeRadius),
                0.01f,
                "RailLancer (1.0/s, no AoE) should have 18% miss vs fast enemies");
        }

        [Test]
        public void EmberFlakProfile_HighFireRateBypassesEvasion()
        {
            var profile = TDTower.GetBalanceProfile(TDTowerKind.EmberFlak);
            Assert.AreEqual(
                0f,
                TDCombatMath.FastEnemyMissChance(profile.shotsPerSecond, profile.aoeRadius),
                "EmberFlak should have fire rate > 1.1/s to bypass evasion");
        }

        [Test]
        public void CinderMortarProfile_AoEBypassesEvasion()
        {
            var profile = TDTower.GetBalanceProfile(TDTowerKind.CinderMortar);
            Assert.Greater(profile.aoeRadius, 0f, "CinderMortar should have AoE radius > 0 to bypass evasion");
            Assert.AreEqual(
                0f,
                TDCombatMath.FastEnemyMissChance(profile.shotsPerSecond, profile.aoeRadius));
        }
    }
}
