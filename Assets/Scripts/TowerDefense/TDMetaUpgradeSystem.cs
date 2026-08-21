using System;
using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    /// <summary>
    /// Meta upgrade system — the repeat-playability layer, NOT a power layer.
    /// Pure data + settlement math with no MonoBehaviour dependencies so the
    /// balance guardrails are unit-testable (spec:
    /// design/spec/meta-upgrade-system-spec-v1.md).
    ///
    /// Currency: Ember Residue, granted ONLY at run settlement (zero presence
    /// inside combat). Persistence lives in TDCampaignProgression (slot-level
    /// keys + snapshot fields); this type never touches PlayerPrefs.
    /// </summary>
    public static class TDMetaUpgradeSystem
    {
        public const int SingleRunResidueCap = 60;
        public const float RepeatCaptureFactor = 0.2f;
        public const float DefeatConsolationFactor = 0.15f;
        public const int DefeatReferenceStars = 3;

        /// <summary>Upgrade lines A-D. Node counts follow the spec's effect
        /// tables (guardrail 2 caps): two ranks per line, eight nodes total.
        /// The spec's prose mentions "10 nodes / ~1000 residue graduation";
        /// its own effect tables + per-level prices sum to 8 nodes / 630 —
        /// flagged to design for reconciliation. Effect caps are the
        /// acceptance-critical part and are pinned by unit tests.</summary>
        public enum UpgradeLine { A = 0, B = 1, C = 2, D = 3 }

        public static readonly UpgradeLine[] AllLines =
            { UpgradeLine.A, UpgradeLine.B, UpgradeLine.C, UpgradeLine.D };

        public static int MaxRank(UpgradeLine line) => 2;

        /// <summary>Per-level prices (index = current rank being bought).</summary>
        public static int RankPrice(UpgradeLine line, int currentRank)
        {
            return (line, currentRank) switch
            {
                (UpgradeLine.A, 0) => 40,
                (UpgradeLine.A, 1) => 90,
                (UpgradeLine.B, 0) => 40,
                (UpgradeLine.B, 1) => 90,
                (UpgradeLine.C, 0) => 40,
                (UpgradeLine.C, 1) => 90,
                (UpgradeLine.D, 0) => 120,
                (UpgradeLine.D, 1) => 120,
                _ => int.MaxValue
            };
        }

        // ── Guardrail 1 effects (each getter takes the purchased ranks) ──

        /// <summary>Line A — Logistics Reserve: flat starting-budget bonus.
        /// +4 per rank, +8 total (≈6% of L01's start, <3% by L20).</summary>
        public static int GetStartingBudgetBonus(int lineARank)
        {
            return Mathf.Clamp(lineARank, 0, MaxRank(UpgradeLine.A)) * 4;
        }

        /// <summary>Line B — Field Salvage: sell refund ratio. 60% base,
        /// +4pp per rank (64%/68% max).</summary>
        public static float GetSellRefundRatio(int lineBRank)
        {
            return TDTower.SellRefundRatio + Mathf.Clamp(lineBRank, 0, MaxRank(UpgradeLine.B)) * 0.04f;
        }

        /// <summary>Line C — Wave Subsidy: wave-clear income bonus in
        /// percent (applied on top of the p12.5.0 decay curve's tail).</summary>
        public static float GetWaveClearIncomeBonusPercent(int lineCRank)
        {
            return Mathf.Clamp(lineCRank, 0, MaxRank(UpgradeLine.C)) * 2f;
        }

        /// <summary>Line D — Formation Playbook: total formation presets
        /// (base 1, up to 3). Pure save QoL — never a fourth formation slot
        /// in battle (guardrail 4 forbidden zone).</summary>
        public static int GetFormationPresetCount(int lineDRank)
        {
            return 1 + Mathf.Clamp(Mathf.Max(0, lineDRank), 0, 2);
        }

        // ── Guardrail 2: residue settlement ──

        /// <summary>
        /// Residue granted for one run. Three anti-grind gates: repeat
        /// captures pay 0.2x, defeats pay a small progress consolation,
        /// and every run is capped.
        /// </summary>
        /// <param name="stars">Stars earned this run (0 on defeat).</param>
        /// <param name="difficulty">Difficulty the run was played at.</param>
        /// <param name="firstCaptureAtDifficulty">True when this is the
        /// first clear of this level at this difficulty (caller derives from
        /// previous highestDifficultyCleared).</param>
        /// <param name="victory">Run outcome.</param>
        /// <param name="wavesReached">Highest wave entered (defeat path).</param>
        /// <param name="totalWaves">Wave count of the level.</param>
        public static int SettleRunResidue(
            int stars,
            TDCampaignDifficultyTier difficulty,
            bool firstCaptureAtDifficulty,
            bool victory,
            int wavesReached,
            int totalWaves)
        {
            int residue;
            if (victory)
            {
                var safeStars = Mathf.Clamp(stars, 0, 3);
                residue = Mathf.FloorToInt(safeStars * DifficultyCoefficient(difficulty) *
                                           (firstCaptureAtDifficulty ? 1f : RepeatCaptureFactor));
            }
            else
            {
                // Progress consolation, not a reward: fraction of the run's
                // nominal full-stars value at this difficulty.
                var safeTotal = Mathf.Max(1, totalWaves);
                var progress = Mathf.Clamp01(wavesReached / (float)safeTotal);
                residue = Mathf.FloorToInt(progress * DefeatReferenceStars *
                                           DifficultyCoefficient(difficulty) * DefeatConsolationFactor);
            }

            return Mathf.Min(residue, SingleRunResidueCap);
        }

        public static float DifficultyCoefficient(TDCampaignDifficultyTier difficulty)
        {
            return difficulty switch
            {
                TDCampaignDifficultyTier.Veteran => 1.5f,
                TDCampaignDifficultyTier.EmberTrial => 2.2f,
                _ => 1f
            };
        }

        // ── Rank encoding: "a1:2,b1:1" (towerLoadout-style tolerance) ──

        public static string EncodeRanks(IReadOnlyDictionary<UpgradeLine, int> ranks)
        {
            if (ranks == null || ranks.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var line in AllLines)
            {
                if (ranks.TryGetValue(line, out var rank) && rank > 0)
                {
                    // LineToken already carries the "a1"/"b1" form.
                    parts.Add($"{LineToken(line)}:{Mathf.Clamp(rank, 0, MaxRank(line))}");
                }
            }

            return string.Join(",", parts);
        }

        /// <summary>Tolerant parse: unknown tokens and out-of-range ranks are
        /// skipped/clamped, mirroring loadout parsing standards.</summary>
        public static Dictionary<UpgradeLine, int> ParseRanks(string encoded)
        {
            var result = new Dictionary<UpgradeLine, int>();
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return result;
            }

            var tokens = encoded.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();
                var colon = token.IndexOf(':');
                if (colon <= 0 || colon + 1 >= token.Length)
                {
                    continue;
                }

                var line = ParseLineToken(token.Substring(0, colon));
                if (line.HasValue && int.TryParse(token.Substring(colon + 1), out var rank) && rank > 0)
                {
                    result[line.Value] = Mathf.Clamp(rank, 0, MaxRank(line.Value));
                }
            }

            return result;
        }

        /// <summary>Purchase validation + resulting rank, or a refusal
        /// reason. Returns false without mutation when invalid.</summary>
        public static bool TryGetPurchase(
            string encodedRanks,
            UpgradeLine line,
            int currentResidue,
            out int price,
            out int nextRank,
            out string refusal)
        {
            var ranks = ParseRanks(encodedRanks);
            var current = ranks.TryGetValue(line, out var owned) ? owned : 0;
            nextRank = current + 1;
            price = RankPrice(line, current);
            if (current >= MaxRank(line))
            {
                refusal = "line-complete";
                return false;
            }

            if (currentResidue < price)
            {
                refusal = "insufficient-residue";
                return false;
            }

            refusal = null;
            return true;
        }

        /// <summary>Apply a purchase to a ranks dictionary (caller persists
        /// via TDCampaignProgression.SetMetaUpgradeRanks + residue spend).</summary>
        public static string PurchaseRank(string encodedRanks, UpgradeLine line)
        {
            var ranks = ParseRanks(encodedRanks);
            var current = ranks.TryGetValue(line, out var owned) ? owned : 0;
            if (current >= MaxRank(line))
            {
                return encodedRanks ?? string.Empty;
            }

            ranks[line] = current + 1;
            return EncodeRanks(ranks);
        }

        /// <summary>Per-line max of two rank sets (cloud-conflict merge
        /// policy: purchases are permanent, so merge keeps the best of
        /// both sides).</summary>
        public static Dictionary<UpgradeLine, int> MergeRanksByMax(
            IReadOnlyDictionary<UpgradeLine, int> left,
            IReadOnlyDictionary<UpgradeLine, int> right)
        {
            var merged = new Dictionary<UpgradeLine, int>();
            foreach (var line in AllLines)
            {
                var l = left != null && left.TryGetValue(line, out var lv) ? lv : 0;
                var r = right != null && right.TryGetValue(line, out var rv) ? rv : 0;
                var max = Mathf.Max(l, r);
                if (max > 0)
                {
                    merged[line] = Mathf.Min(max, MaxRank(line));
                }
            }

            return merged;
        }

        private static UpgradeLine? ParseLineToken(string token)
        {
            return token switch
            {
                "a1" => UpgradeLine.A,
                "b1" => UpgradeLine.B,
                "c1" => UpgradeLine.C,
                "d1" => UpgradeLine.D,
                _ => null
            };
        }

        private static string LineToken(UpgradeLine line)
        {
            return line switch
            {
                UpgradeLine.A => "a1",
                UpgradeLine.B => "b1",
                UpgradeLine.C => "c1",
                UpgradeLine.D => "d1",
                _ => "x"
            };
        }
    }
}
