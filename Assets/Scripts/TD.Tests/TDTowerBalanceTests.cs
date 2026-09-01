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
        public void ExpansionTowers_BaseStats_MatchSheets()
        {
            // Expansion batch 1 baselines pinned from expansion-tower-sheets-v1.
            // RailBarricade carries placeholder combat stats (no ranged attack
            // per the behavior spec); only cost is pinned for it here — its
            // cadence/damage are owned by the wagon system, not this table.
            var pins = new (TDTowerKind kind, int cost, float range, float sps, int damage)[]
            {
                (TDTowerKind.SlagBurner, 50, 2.2f, 1.1f, 8),
                (TDTowerKind.SalvageDerrick, 44, 1.8f, 0.9f, 5),
                (TDTowerKind.LongRailCannon, 72, 4.8f, 0.4f, 34),
            };

            foreach (var (kind, cost, range, sps, damage) in pins)
            {
                var baseState = typeof(TDTower)
                    .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, new object[] { kind });

                Assert.IsNotNull(baseState, $"{kind} should have a base state");
                var stateType = baseState.GetType();
                Assert.AreEqual(cost, (int)stateType.GetField("buildCost").GetValue(baseState),
                    $"{kind} build cost must match the sheet");
                Assert.AreEqual(range, (float)stateType.GetField("range").GetValue(baseState), 0.0005f,
                    $"{kind} range must match the sheet");
                Assert.AreEqual(sps, (float)stateType.GetField("shotsPerSecond").GetValue(baseState), 0.0005f,
                    $"{kind} shots/sec must match the sheet");
                Assert.AreEqual(damage, (int)stateType.GetField("damage").GetValue(baseState),
                    $"{kind} damage must match the sheet");
            }

            var barricade = typeof(TDTower)
                .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { TDTowerKind.RailBarricade });
            Assert.IsNotNull(barricade);
            Assert.AreEqual(60, (int)barricade.GetType().GetField("buildCost").GetValue(barricade),
                "RailBarricade build cost must match the sheet");
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
