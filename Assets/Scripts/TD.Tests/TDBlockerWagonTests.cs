using NUnit.Framework;
using TD;
using UnityEngine;

namespace TD.Tests
{
    /// <summary>
    /// Rail Barricade blocker contract (expansion tower 11, mapping §2
    /// five-pin draft against rail-barricade-behavior-spec). Logic here is
    /// scene-free by construction — MonoBehaviour shells only.
    /// </summary>
    public class TDBlockerWagonTests
    {
        private static TDBlockerWagon NewWagon()
        {
            var go = new GameObject("wagon_under_test");
            try
            {
                return go.AddComponent<TDBlockerWagon>();
            }
            catch
            {
                Object.DestroyImmediate(go);
                throw;
            }
        }

        private static TDEnemy NewEnemy()
        {
            var go = new GameObject("enemy_under_test");
            return go.AddComponent<TDEnemy>();
        }

        [TearDown]
        public void TearDown()
        {
            TDBlockerWagon.ClearAll();
        }

        [Test]
        public void EngageCapacity_TwoFrontRow_ThirdQueues_DestroyReleasesAll()
        {
            Assert.AreEqual(2, TDBlockContract.EngageCapacity);

            var wagon = NewWagon();
            var e1 = NewEnemy();
            var e2 = NewEnemy();
            var e3 = NewEnemy();

            Assert.IsTrue(wagon.TryEngage(e1), "first enemy takes a front slot");
            Assert.IsTrue(wagon.TryEngage(e2), "second enemy takes a front slot");
            Assert.IsFalse(wagon.TryEngage(e3), "third is refused — queue lives on the enemy side");

            e3.TryEngageWagon(wagon);
            Assert.AreSame(wagon, e3.QueuedWagon, "refused enemy queues on the wagon");
            Assert.IsNull(e3.EngagedWagon);

            // Wagon wrecked: every reference clears, everyone resumes moving.
            // Engaged refs are cleared by DetachAll; a queued ref clears via
            // Unity null semantics on the next Update — assert with the same
            // operator (NUnit's IsNull compares raw references and can't see
            // destroyed-object fakes).
            wagon.DetachAll();
            Object.DestroyImmediate(wagon.gameObject);
            Assert.IsTrue(e1.EngagedWagon == null);
            Assert.IsTrue(e2.EngagedWagon == null);
            Assert.IsTrue(e3.QueuedWagon == null);
            e1.DetachFromWagon();
            e2.DetachFromWagon();
            e3.DetachFromWagon();
        }

        [Test]
        public void BypassList_SappersGlideBossesPass_EveryFourthSwarmLeaks()
        {
            var counter = 0;
            Assert.IsTrue(TDBlockContract.ResolveBypass("burrow_sapper", false, ref counter));
            Assert.IsTrue(TDBlockContract.ResolveBypass("cinder_glider", false, ref counter));
            Assert.IsTrue(TDBlockContract.ResolveBypass("furnace_matriarch", true, ref counter), "bosses crush through");

            counter = 0;
            Assert.IsFalse(TDBlockContract.ResolveBypass("ash_swarm", false, ref counter), "swarm 1 blocked");
            Assert.IsFalse(TDBlockContract.ResolveBypass("ash_swarm", false, ref counter), "swarm 2 blocked");
            Assert.IsFalse(TDBlockContract.ResolveBypass("ash_swarm", false, ref counter), "swarm 3 blocked");
            Assert.IsTrue(TDBlockContract.ResolveBypass("ash_swarm", false, ref counter), "every 4th leaks through");

            counter = 0;
            Assert.IsFalse(TDBlockContract.ResolveBypass("skitter_runner", false, ref counter), "normal enemies fight");
            Assert.IsFalse(TDBlockContract.ResolveBypass("echo_brood", false, ref counter), "batch-2 enemies fight too");
            Assert.IsFalse(TDBlockContract.ResolveBypass("forge_dragon", false, ref counter));
            Assert.IsFalse(TDBlockContract.ResolveBypass("acid_blister", false, ref counter));
        }

        [Test]
        public void BossCrush_WrecksWagonAndStallsBoss()
        {
            var wagon = NewWagon();
            var boss = NewEnemy();

            Assert.IsTrue(wagon.IsAlive);
            wagon.CrushBy(boss);
            Assert.IsFalse(wagon.IsAlive, "one hit wrecks the body");
        }

        [Test]
        public void EngagedEnemies_StayAliveForWaveClearAccounting()
        {
            // Pin (regression guard): engagement never resolves or escapes the
            // enemy — the wave loop's while(activeEnemies.Count > 0) therefore
            // cannot clear a wave while a wagon holds traffic. If a future
            // change makes engagement resolve enemies, this pin fails first.
            var wagon = NewWagon();
            var enemy = NewEnemy();

            Assert.IsTrue(wagon.TryEngage(enemy));
            Assert.IsTrue(enemy.IsTargetable, "engaged enemy is still a live wave participant");
            Assert.AreEqual(1, wagon.EngagedCount);
        }

        [Test]
        public void AutoplaySellGuard_OnlyIdleBarricadesSellable()
        {
            // SpawnFor registers the wagon in the guard's registry — the
            // AddComponent shortcut used elsewhere bypasses it.
            var towerGo = new GameObject("tower_under_test");
            var tower = towerGo.AddComponent<TDTower>();
            var wagon = TDBlockerWagon.SpawnFor(null, tower, Vector3.zero, "seg_guard_test");

            // No traffic: sellable.
            Assert.IsFalse(TDBlockerWagon.HasEngagedTraffic(tower));

            // Engaged traffic under this owner blocks the sale.
            var enemy = NewEnemy();
            Assert.IsTrue(wagon.TryEngage(enemy));
            Assert.IsTrue(TDBlockerWagon.HasEngagedTraffic(tower));

            wagon.DetachAll();
            Assert.IsFalse(TDBlockerWagon.HasEngagedTraffic(tower), "idle wagon is sellable again");

            wagon.DetachAll();
            Object.DestroyImmediate(towerGo);
        }
    }
}
