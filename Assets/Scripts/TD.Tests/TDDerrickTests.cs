using NUnit.Framework;
using TD;

namespace TD.Tests
{
    /// <summary>
    /// Salvage Derrick economy math (expansion tower 10, mapping §1.5).
    /// The per-wave fuse is the roadmap's 防躺赢保险丝 — these pins hold it
    /// at the sheet's red line (single-wave increment ≤ 45) and pin the
    /// aura's bounty arithmetic.
    /// </summary>
    public class TDDerrickTests
    {
        [Test]
        public void WaveIncomeCeiling_T3DualSpec()
        {
            // Max config: D3 salvage 18 + U3 rebate 3 x a dense wave (24
            // in-ring kills) + Supply Drop 3 = 93 raw. The fuse holds the
            // increment at the ceiling.
            Assert.AreEqual(45, TDEconomyTuning.ResolveDerrickWaveIncome(18, 3, 24, 3));
        }

        [Test]
        public void WaveIncome_LowConfig_Unclamped()
        {
            // Early investment must pay exactly what the sheet promises —
            // the fuse only bites at the top.
            Assert.AreEqual(8, TDEconomyTuning.ResolveDerrickWaveIncome(6, 1, 2, 0));
            Assert.AreEqual(9, TDEconomyTuning.ResolveDerrickWaveIncome(6, 0, 0, 3));
        }

        [Test]
        public void WaveIncome_NeverNegative()
        {
            Assert.AreEqual(0, TDEconomyTuning.ResolveDerrickWaveIncome(0, 1, -5, 0));
        }

        [Test]
        public void Aura_BountyPercent()
        {
            Assert.AreEqual(1.18f, TDEconomyTuning.ResolveAuraBountyMultiplier(0.18f, false, false), 0.0001f);
        }

        [Test]
        public void Aura_ScrapProtocol_BossEliteOnly()
        {
            // ×1.5 rides only on boss/elite kills inside a damage-specialist
            // ring; plain kills and non-specialist rings keep the base bonus.
            Assert.AreEqual(1.18f * 1.5f, TDEconomyTuning.ResolveAuraBountyMultiplier(0.18f, true, true), 0.0001f);
            Assert.AreEqual(1.18f, TDEconomyTuning.ResolveAuraBountyMultiplier(0.18f, true, false), 0.0001f);
            Assert.AreEqual(1.18f, TDEconomyTuning.ResolveAuraBountyMultiplier(0.18f, false, true), 0.0001f);
        }

        [Test]
        public void Clamp_StopsAtCeiling()
        {
            // Runtime credit accounting: the last coins of the fuse pay out
            // partially, then nothing more this wave.
            Assert.AreEqual(1, TDEconomyTuning.ClampDerrickWaveCredit(44, 3));
            Assert.AreEqual(0, TDEconomyTuning.ClampDerrickWaveCredit(45, 5));
            Assert.AreEqual(3, TDEconomyTuning.ClampDerrickWaveCredit(0, 3));
        }
    }
}
