using NUnit.Framework;
using TD;

namespace TD.Tests
{
    /// <summary>
    /// Tests for tower balance: base stats, specialization multipliers,
    /// and the RailLancer heavyMultiplier removal (R1 fix).
    /// </summary>
    public class TDTowerBalanceTests
    {
        [Test]
        public void RailLancer_HeavyMultiplier_IsRemoved()
        {
            // After the R1 balance fix, RailLancer should have heavyMultiplier = 1.0
            // (was 1.25 before). This was the core cause of single-tower dominance.
            var baseState = typeof(TDTower)
                .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { TDTowerKind.RailLancer });

            Assert.IsNotNull(baseState);
            var heavyField = baseState.GetType().GetField("heavyMultiplier");
            var heavyMult = (float)heavyField.GetValue(baseState);
            Assert.AreEqual(1.0f, heavyMult, 0.001f,
                "RailLancer heavyMultiplier must be 1.0 (was 1.25, caused single-tower clear)");
        }

        [Test]
        public void SiegeDrill_HeavyMultiplier_IsGreaterThanOne()
        {
            // SiegeDrill should still have heavyMultiplier > 1 (it's the anti-armor tower).
            var baseState = typeof(TDTower)
                .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { TDTowerKind.SiegeDrill });

            Assert.IsNotNull(baseState);
            var heavyField = baseState.GetType().GetField("heavyMultiplier");
            var heavyMult = (float)heavyField.GetValue(baseState);
            Assert.Greater(heavyMult, 1.0f, "SiegeDrill should bonus vs heavy/armored");
        }

        [Test]
        public void AllTowerKinds_HaveValidBaseState()
        {
            foreach (TDTowerKind kind in System.Enum.GetValues(typeof(TDTowerKind)))
            {
                var baseState = typeof(TDTower)
                    .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, new object[] { kind });

                Assert.IsNotNull(baseState, $"{kind} should have a base state");
                var dmgField = baseState.GetType().GetField("damage");
                var rangeField = baseState.GetType().GetField("range");
                var costField = baseState.GetType().GetField("buildCost");

                var damage = (int)dmgField.GetValue(baseState);
                var range = (float)rangeField.GetValue(baseState);
                var cost = (int)costField.GetValue(baseState);

                Assert.Greater(damage, 0, $"{kind} damage must be > 0");
                Assert.Greater(range, 1f, $"{kind} range must be > 1");
                Assert.Greater(cost, 0, $"{kind} cost must be > 0");
            }
        }
    }
}
