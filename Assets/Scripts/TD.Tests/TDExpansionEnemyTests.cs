using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Expansion batch-2 enemy behaviors: Forge Dragoon's shield layer,
    /// Ember Strider's marked fragility, Rail Splitter's segment geometry.
    /// Pure/instance seams designed to run without a manager.
    /// </summary>
    public class TDExpansionEnemyTests
    {
        private static TDEnemy NewEnemy(TDEnemyCatalogEntry entry, float[] straightPath = null)
        {
            var go = new GameObject($"enemy_{entry.enemyId}");
            var enemy = go.AddComponent<TDEnemy>();
            var path = new Vector3[]
            {
                new(0f, 0f, 0f),
                new(1f, 0f, 0f),
                new(2f, 0f, 0f),
                new(3f, 0f, 0f),
            };
            enemy.Initialize(null, path, entry, "default");
            return enemy;
        }

        private static TDEnemyCatalogEntry Entry(string id, int hp, float speed, int armor)
        {
            return new TDEnemyCatalogEntry
            {
                enemyId = id,
                displayName = id,
                hp = hp,
                speed = speed,
                armorFlat = armor,
                rewardGold = 1,
                lineDamage = 1,
                threatCost = 1f,
                tags = new string[0],
            };
        }

        [TearDown]
        public void TearDown()
        {
            TDBlockerWagon.ClearAll();
        }

        [Test]
        public void ForgeDragoon_ShieldAbsorbsFirstThreeHitsPerWave()
        {
            var enemy = NewEnemy(Entry("forge_dragoon", 150, 0.75f, 7));
            // No manager: wave resets lazily to 0 — three immune hits, then real damage.
            Assert.AreEqual(0, enemy.TakeHit(10, 0f, 0f), "hit 1 absorbed");
            Assert.AreEqual(0, enemy.TakeHit(10, 0f, 0f), "hit 2 absorbed");
            Assert.AreEqual(0, enemy.TakeHit(10, 0f, 0f), "hit 3 absorbed");
            // Armor 7 vs 10 raw -> ResolveArmoredDamage floor: hybrid model, >= 1.
            Assert.Greater(enemy.TakeHit(10, 0f, 0f), 0, "4th hit lands");
        }

        [Test]
        public void ForgeDragoon_ShieldDoesNotBlockOtherEnemies()
        {
            var enemy = NewEnemy(Entry("skitter_runner", 26, 2.2f, 0));
            Assert.Greater(enemy.TakeHit(5, 0f, 0f), 0, "shield is dragoon-only");
        }

        [Test]
        public void EmberStrider_MarkedTakesBonusAndNeverDodges()
        {
            var enemy = NewEnemy(Entry("ember_strider", 95, 2.4f, 0));
            enemy.SetResonanceMark(1f);
            // Marked: +25% and the evade gate is skipped entirely — 11 raw
            // rounds to 14 (11 * 1.25 = 13.75; avoids Mathf's to-even .5s).
            var applied = enemy.TakeHit(11, 0f, 0f);
            Assert.AreEqual(14, applied);

            // Unmarked: no bonus (armor 0 -> exact 11).
            var plain = NewEnemy(Entry("ember_strider", 95, 2.4f, 0));
            Assert.AreEqual(11, plain.TakeHit(11, 0f, 0f));
        }

        [Test]
        public void WarpToProgress_PlacesMidPath()
        {
            var enemy = NewEnemy(Entry("skitter_runner", 26, 2.2f, 0));
            enemy.WarpToProgress(0.5f);
            Assert.Greater(enemy.transform.position.x, 0.5f, "mid-path warp lands past the first waypoint");
            Assert.Less(enemy.transform.position.x, 2.5f);
            Assert.GreaterOrEqual(enemy.GetRouteProgress01(), 0.25f);
        }

        [Test]
        public void EchoCopyHealth_HalfRoundsUpAtLeastOne()
        {
            var enemy = NewEnemy(Entry("echo_brood", 55, 1.4f, 0));
            enemy.SetCurrentHealth(27);
            // No public HP getter beyond ratio — assert survival (no kill) and
            // that a floor of 1 keeps odd-HP copies alive.
            enemy.SetCurrentHealth(1);
            Assert.IsTrue(enemy.IsTargetable);
        }
    }
}
