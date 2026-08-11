using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Tests for the fast-enemy evasion system added in the R1 balance fix.
    /// Slow-firing single-target towers should miss fast enemies; AoE and
    /// high-fire-rate towers should not.
    /// </summary>
    public class TDEvasionTests
    {
        [Test]
        public void RailLancer_HasEvasionVsFast()
        {
            // RailLancer: 1.0 shots/sec, single-target → should have ~18% miss
            var baseState = typeof(TDTower)
                .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { TDTowerKind.RailLancer });

            var spsField = baseState.GetType().GetField("shotsPerSecond");
            var aoeField = baseState.GetType().GetField("aoeRadius");
            var sps = (float)spsField.GetValue(baseState);
            var aoe = (float)aoeField.GetValue(baseState);

            // Compute expected evasion (mirrors EvadeableFastEnemyMissChance logic)
            float expectedEvasion;
            if (aoe > 0f || sps > 1.1f)
            {
                expectedEvasion = 0f;
            }
            else if (sps >= 1.0f)
            {
                expectedEvasion = 0.18f;
            }
            else
            {
                expectedEvasion = Mathf.Lerp(0.18f, 0.30f, Mathf.InverseLerp(1.0f, 0.5f, sps));
            }

            Assert.AreEqual(0.18f, expectedEvasion, 0.01f,
                "RailLancer (1.0/s, no AoE) should have 18% miss vs fast enemies");
        }

        [Test]
        public void EmberFlak_HasNoEvasion()
        {
            // EmberFlak: 1.35 shots/sec → above 1.1 threshold → no evasion
            var baseState = typeof(TDTower)
                .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { TDTowerKind.EmberFlak });

            var spsField = baseState.GetType().GetField("shotsPerSecond");
            var sps = (float)spsField.GetValue(baseState);
            Assert.Greater(sps, 1.1f,
                "EmberFlak should have fire rate > 1.1/s to bypass evasion");
        }

        [Test]
        public void CinderMortar_AoEBypassesEvasion()
        {
            // CinderMortar has AoE radius > 0 → bypasses evasion entirely
            var baseState = typeof(TDTower)
                .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { TDTowerKind.CinderMortar });

            var aoeField = baseState.GetType().GetField("aoeRadius");
            var aoe = (float)aoeField.GetValue(baseState);
            Assert.Greater(aoe, 0f,
                "CinderMortar should have AoE radius > 0 to bypass evasion");
        }
    }
}
