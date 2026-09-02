using NUnit.Framework;
using TD;

namespace TD.Tests
{
    /// <summary>
    /// Long Rail Cannon pierce math (expansion tower 12, mapping §1.5).
    /// The chain pins the sheet's 34/23/16/11/7 line, Full Bore's zero-
    /// falloff +30% line end, and the tower's identity weakness — the 30%
    /// fast-enemy miss at its 0.4 shots/sec cadence — against future curve
    /// edits.
    /// </summary>
    public class TDPierceTests
    {
        [Test]
        public void Chain_Falloff07_MatchesSheetSequence()
        {
            var chain = TDCombatMath.ResolvePierceDamageChain(34, 0.7f, 5);
            Assert.AreEqual(new[] { 34, 23, 16, 11, 7 }, chain);
        }

        [Test]
        public void Chain_FullBore_ZeroFalloff_LastTargetBonus()
        {
            // Full Bore: falloff 1.0 — every entry stays at base — and the
            // line's end target pays +30% before the floor.
            var chain = TDCombatMath.ResolvePierceDamageChain(34, 1.0f, 5, 1.3f);
            Assert.AreEqual(new[] { 34, 34, 34, 34, 44 }, chain);
        }

        [Test]
        public void Chain_FloorsAtOne_NeverZero()
        {
            var chain = TDCombatMath.ResolvePierceDamageChain(2, 0.2f, 6);
            for (var i = 0; i < chain.Length; i++)
            {
                Assert.GreaterOrEqual(chain[i], 1, $"entry {i} must never drop below 1");
            }
        }

        [Test]
        public void Chain_EmptyAndDegenerate()
        {
            Assert.AreEqual(0, TDCombatMath.ResolvePierceDamageChain(34, 0.7f, 0).Length);
            Assert.AreEqual(0, TDCombatMath.ResolvePierceDamageChain(0, 0.7f, 5).Length);
        }

        [Test]
        public void FullBore_ShotFalloff_WiringPinned()
        {
            // Design errata b08df07: the spec bypasses the falloff table
            // entirely (not "falloff improved to 1.0 by upgrades") — the shot
            // resolver must hand the chain 1.0 whenever Full Bore is live.
            Assert.AreEqual(1.0f, TDCombatMath.ResolvePierceShotFalloff(true, 0.7f));
            Assert.AreEqual(1.0f, TDCombatMath.ResolvePierceShotFalloff(true, 0.4f));
            Assert.AreEqual(0.7f, TDCombatMath.ResolvePierceShotFalloff(false, 0.7f));
            Assert.AreEqual(0.4f, TDCombatMath.ResolvePierceShotFalloff(false, 0.4f));
        }

        [Test]
        public void Cannon_EvadeIdentity_ThirtyPercentPinned()
        {
            // The cannon's identity weakness is NOT a new mechanic — it is the
            // existing slow-firing curve evaluated at its 0.4/s cadence. Pin
            // the coincidence so a future curve tweak cannot silently delete
            // the weakness (only Ballistic Lead may remove it).
            Assert.AreEqual(0.30f, TDCombatMath.FastEnemyMissChance(0.4f, 0f), 0.0001f);
        }
    }
}
