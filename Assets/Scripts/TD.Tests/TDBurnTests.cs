using NUnit.Framework;
using TD;

namespace TD.Tests
{
    /// <summary>
    /// Slag Burner burn math (expansion tower 9, mapping §1.5 assertion
    /// drafts). Pure-function pins — the application layer (TDEnemy ticks,
    /// projectile ignition, detonation, spread) is exercised in play
    /// regression via the anchor protocol.
    /// </summary>
    public class TDBurnTests
    {
        [Test]
        public void BurnTick_FlatArmorOnly()
        {
            // 2 raw tick vs 9 flat armor: the percentage hybrid never applies
            // to burn — heavy armor stays a wall (design note, ruling B1
            // family). Floor keeps the fire alive at 1.
            Assert.AreEqual(1, TDBurnSystem.ResolveBurnTick(2, 9, 0));
        }

        [Test]
        public void BurnTick_ArmorBreakApplies()
        {
            // Armor break restores burn throughput through the flat channel
            // only. 2 raw vs 9 armor with 6 broken (effective 3): still the
            // floor — break's value for burn is at higher raw ticks, and this
            // pins that the channel never goes negative.
            Assert.AreEqual(1, TDBurnSystem.ResolveBurnTick(2, 9, 6));
        }

        [Test]
        public void BurnTick_Floor()
        {
            Assert.AreEqual(1, TDBurnSystem.ResolveBurnTick(1, 0, 0));
        }

        [Test]
        public void BurnTick_HighRawScalesWithBreak()
        {
            // 6-layer cap tick (6 raw at base per-layer 2.0/s, 0.5s tick):
            // 9-armor walls it to 1, breaking 6 armor opens it to 3. This is
            // the armor-break synergy the sheet promises.
            Assert.AreEqual(1, TDBurnSystem.ResolveBurnTick(6, 9, 0));
            Assert.AreEqual(3, TDBurnSystem.ResolveBurnTick(6, 9, 6));
        }

        [Test]
        public void StackCap_Six()
        {
            Assert.AreEqual(6, TDBurnSystem.ClampStacks(7));
            Assert.AreEqual(6, TDBurnSystem.ClampStacks(6));
            Assert.AreEqual(0, TDBurnSystem.ClampStacks(-1));
        }

        [Test]
        public void Detonate_ClearsStacks_ResolvedAtDoubleRate()
        {
            // Slag Sump: 6 layers x 2.0/s resolved at 2.0x = 24 burst.
            Assert.AreEqual(24, TDBurnSystem.ResolveDetonateDamage(6, 2.0f));
            // Full stacks only — the burst is meaningless below cap.
            Assert.AreEqual(0, TDBurnSystem.ResolveDetonateDamage(0, 2.0f));
        }

        [Test]
        public void TickRawDamage_MatchesSheet()
        {
            // Base per-layer 2.0/s: a 2-layer tick at the 0.5s cadence is
            // exactly the 2-raw used throughout these pins.
            Assert.AreEqual(2, TDBurnSystem.ResolveTickRawDamage(2, 2.0f));
            Assert.AreEqual(6, TDBurnSystem.ResolveTickRawDamage(6, 2.0f));
        }
    }
}
