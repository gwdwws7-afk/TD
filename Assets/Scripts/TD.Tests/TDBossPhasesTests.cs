using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Exam-boss pure logic (expansion batch 2, boss-design-spec-v1):
    /// threshold ladder, mimic families, custodian stack caps, and the
    /// stagger-immunity contract on live enemy instances.
    /// </summary>
    public class TDBossPhasesTests
    {
        [Test]
        public void PhaseLadder_WalksOneStepPerCrossing()
        {
            var thresholds = new[] { 0.70f, 0.35f };
            // Burst from 80% straight to 20%: still advances one rung at a
            // time — every transition event fires, in order.
            var index = TDBossPhases.ResolvePhaseIndex(0.80f, 0, thresholds);
            Assert.AreEqual(0, index);
            index = TDBossPhases.ResolvePhaseIndex(0.20f, index, thresholds);
            Assert.AreEqual(1, index);
            index = TDBossPhases.ResolvePhaseIndex(0.10f, index, thresholds);
            Assert.AreEqual(2, index);
            // Saturated: stays at the top of the ladder.
            index = TDBossPhases.ResolvePhaseIndex(0.01f, index, thresholds);
            Assert.AreEqual(2, index);
        }

        [Test]
        public void PhaseLadder_ContainermawSingleThreshold()
        {
            var thresholds = new[] { TDBossPhases.ContainerPhaseTwoHealthRatio };
            Assert.AreEqual(0, TDBossPhases.ResolvePhaseIndex(0.51f, 0, thresholds));
            Assert.AreEqual(1, TDBossPhases.ResolvePhaseIndex(0.49f, 0, thresholds));
        }

        [Test]
        public void MimicFamilies_CoverAllTwelveKinds()
        {
            var saw = new System.Collections.Generic.HashSet<TDBossPhases.MimicCategory>();
            foreach (TDTowerKind kind in System.Enum.GetValues(typeof(TDTowerKind)))
            {
                var category = TDBossPhases.ResolveMimicCategory(kind);
                Assert.AreNotEqual(TDBossPhases.MimicCategory.None, kind + ": every tower maps to a family");
                saw.Add(category);
            }

            // All six families are represented by the 12-kind table.
            Assert.AreEqual(6, saw.Count);
        }

        [Test]
        public void MimicFamilies_MatchSpecTable()
        {
            Assert.AreEqual(TDBossPhases.MimicCategory.Surge, TDBossPhases.ResolveMimicCategory(TDTowerKind.RailLancer));
            Assert.AreEqual(TDBossPhases.MimicCategory.Surge, TDBossPhases.ResolveMimicCategory(TDTowerKind.LongRailCannon));
            Assert.AreEqual(TDBossPhases.MimicCategory.BurnCloud, TDBossPhases.ResolveMimicCategory(TDTowerKind.SlagBurner));
            Assert.AreEqual(TDBossPhases.MimicCategory.Slipstream, TDBossPhases.ResolveMimicCategory(TDTowerKind.FrostCoil));
            Assert.AreEqual(TDBossPhases.MimicCategory.Reforge, TDBossPhases.ResolveMimicCategory(TDTowerKind.SiegeDrill));
            Assert.AreEqual(TDBossPhases.MimicCategory.Barrage, TDBossPhases.ResolveMimicCategory(TDTowerKind.EmberFlak));
            Assert.AreEqual(TDBossPhases.MimicCategory.SignalJam, TDBossPhases.ResolveMimicCategory(TDTowerKind.ResonanceBeacon));
            Assert.AreEqual(TDBossPhases.MimicCategory.SignalJam, TDBossPhases.ResolveMimicCategory(TDTowerKind.RailBarricade));
        }

        [Test]
        public void CustodianStacks_CapAtEight()
        {
            Assert.AreEqual(8, TDBossPhases.ClampStacks(12));
            Assert.AreEqual(8, TDBossPhases.ClampStacks(8));
            Assert.AreEqual(0, TDBossPhases.ClampStacks(-3));
        }

        [Test]
        public void BossStaggerImmunity_HoldsExceptWhenForced()
        {
            var go = new GameObject("boss_stagger_test");
            var enemy = go.AddComponent<TDEnemy>();
            var path = new[] { Vector3.zero, Vector3.right, Vector3.right * 2 };
            enemy.Initialize(null, path, new TDEnemyCatalogEntry
            {
                enemyId = "junction_tyrant",
                displayName = "Junction Tyrant",
                hp = 380,
                speed = 0.7f,
                armorFlat = 6,
                rewardGold = 46,
                lineDamage = 4,
                threatCost = 15f,
                tags = new[] { "boss" },
            }, "default");

            enemy.ApplyStagger(1f, 0.2f);
            Assert.IsFalse(enemy.IsStaggered, "bosses shrug off ordinary stagger");

            enemy.ApplyStagger(1f, 0.2f, true);
            Assert.IsTrue(enemy.IsStaggered, "the crush exception forces through");
        }
    }
}
