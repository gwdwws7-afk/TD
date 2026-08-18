using NUnit.Framework;
using TD;

namespace TD.Tests
{
    /// <summary>
    /// Guards tower windup (charge) durations after they moved from the
    /// presentation profile into combat data (TowerState.windupDuration).
    /// Windup defines the fire cadence — cooldown = max(0.03, 1/sps − windup) —
    /// so any change to this table is a balance decision, not a feel tweak.
    /// </summary>
    public class TDTowerWindupTests
    {
        private static object CreateBaseState(TDTowerKind kind)
        {
            return typeof(TDTower)
                .GetMethod("CreateBaseState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, new object[] { kind });
        }

        private static float ReadField(object state, string fieldName)
        {
            var field = state.GetType().GetField(fieldName);
            Assert.IsNotNull(field, $"TowerState.{fieldName} is missing");
            return (float)field.GetValue(state);
        }

        [Test]
        public void WindupDurations_MatchLockedCadenceTable()
        {
            var expected = new (TDTowerKind kind, float windup)[]
            {
                (TDTowerKind.RailLancer, 0.28f),
                (TDTowerKind.CinderMortar, 0.38f),
                (TDTowerKind.FrostCoil, 0.22f),
                (TDTowerKind.ArcWelder, 0.20f),
                (TDTowerKind.SiegeDrill, 0.40f),
                (TDTowerKind.EmberFlak, 0.14f),
                (TDTowerKind.ResonanceBeacon, 0.25f),
                (TDTowerKind.GravSnare, 0.36f),
            };

            foreach (var (kind, windup) in expected)
            {
                var state = CreateBaseState(kind);
                Assert.IsNotNull(state, $"CreateBaseState returned null for {kind}");
                Assert.AreEqual(windup, ReadField(state, "windupDuration"), 0.0005f,
                    $"{kind} windup must stay {windup:0.00}s — it defines the fire cadence");
            }
        }

        [Test]
        public void Windup_AlwaysShorterThanFireInterval()
        {
            // A windup at or above the full fire interval would re-pace the
            // tower through the 0.03s cooldown floor and silently change DPS.
            foreach (TDTowerKind kind in System.Enum.GetValues(typeof(TDTowerKind)))
            {
                var state = CreateBaseState(kind);
                Assert.IsNotNull(state, $"CreateBaseState returned null for {kind}");
                var sps = ReadField(state, "shotsPerSecond");
                var windup = ReadField(state, "windupDuration");
                Assert.Less(windup, 1f / sps,
                    $"{kind}: windup {windup:0.00}s must stay below the fire interval {1f / sps:0.00}s");
            }
        }

        [Test]
        public void PresentationProfile_NoLongerOwnsChargeDuration()
        {
            Assert.IsNull(typeof(TDTowerPresentationProfile).GetField("chargeDuration"),
                "chargeDuration belongs to combat data (TowerState.windupDuration); the presentation profile must not regain it");
        }

        [Test]
        public void PostWindupCooldown_CreditsFrameOvershoot()
        {
            // Time-exact cadence (TD-WINDUP-001): the sub-frame overshoot past
            // a completed windup reduces the following cooldown so the full
            // cycle lands on the designed interval.
            Assert.AreEqual(0.72f, TDCombatMath.ResolvePostWindupCooldown(1.0f, 0.28f, 0f), 0.0001f,
                "no overshoot: cooldown = interval - windup");
            Assert.AreEqual(0.70f, TDCombatMath.ResolvePostWindupCooldown(1.0f, 0.28f, 0.02f), 0.0001f,
                "2 frames-equivalent overshoot is credited");
            Assert.AreEqual(0.03f, TDCombatMath.ResolvePostWindupCooldown(1.0f, 0.28f, 5f), 0.0001f,
                "heavy overshoot floors at the 0.03s minimum gap");
            Assert.AreEqual(0.72f, TDCombatMath.ResolvePostWindupCooldown(1.0f, 0.28f, -0.5f), 0.0001f,
                "negative overshoot (timer clamped) is ignored");
        }
    }
}
