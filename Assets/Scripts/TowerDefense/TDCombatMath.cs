using UnityEngine;

namespace TD
{
    /// <summary>
    /// Pure combat-resolution math shared by TDEnemy/TDTower and the EditMode
    /// test suites. Everything here must stay free of scene/instance state so
    /// the balance tests exercise the exact production formula instead of a
    /// drifting mirror of it.
    ///
    /// ROUNDING CANON (director decision B1, 2026-08-24 — exactly three
    /// pinned points; a fourth rounding site is a spec violation):
    ///   1. Final damage: RoundToInt (matches displayed values).
    ///   2. Armor flat-share cap: CeilToInt (the cap is armor's last
    ///      concession — floor would zero it on tiny hits).
    ///   3. Upgrade costs: CeilToInt (buyer pays whole, no free tiers).
    ///   4. [Reserved] Burn ticks (expansion tower 9): RoundToInt — the slot
    ///      is on file here before TDBurnSystem lands.
    /// </summary>
    public static class TDCombatMath
    {
        public const float ArmorPercentPerPoint = 0.04f;
        public const float ArmorPercentCap = 0.60f;
        public const float ArmorFlatShareCap = 0.5f;
        public const int DamageFloor = 1;

        /// <summary>
        /// Hybrid armor model: flat subtraction PLUS percentage mitigation.
        /// Each point of effective armor also reduces damage by 4% (capped at
        /// 60%), so high-armor enemies are a real wall for low-per-hit towers
        /// while armor-piercing becomes mandatory. The flat subtraction may
        /// remove at most half of the post-percent damage: without that cap the
        /// 08-10 model floored nearly the whole roster at 1 vs 9+ armor
        /// (L13/L20 collapse, diagnosis appendix 2) and only SiegeDrill's
        /// break could lift it — with the cap the wall stays real but a
        /// mid per-hit tower deals half its mitigated damage instead of the
        /// floor. The flat floor stays at 1 so chip damage is always possible.
        /// Callers apply exposure and armor-break modifiers to the inputs
        /// before calling.
        /// </summary>
        public static int ResolveArmoredDamage(int damage, int effectiveArmor)
        {
            var armorPercentReduction = Mathf.Min(ArmorPercentCap, effectiveArmor * ArmorPercentPerPoint);
            var afterPercent = damage * (1f - armorPercentReduction);
            var flatCap = Mathf.CeilToInt(afterPercent * ArmorFlatShareCap);
            var effectiveFlat = Mathf.Min(effectiveArmor, flatCap);
            return Mathf.Max(DamageFloor, Mathf.RoundToInt(afterPercent - effectiveFlat));
        }

        /// <summary>
        /// Cooldown that follows a completed windup. The windup's frame
        /// overshoot (how far past zero the timer ran on the firing frame)
        /// is credited against the cooldown so the full cycle stays on the
        /// designed interval regardless of frame boundaries. The 0.03s floor
        /// keeps a minimum gap even when overshot heavily.
        /// </summary>
        public static float ResolvePostWindupCooldown(float shotInterval, float windupDuration, float windupOvershoot)
        {
            var overshoot = Mathf.Max(0f, windupOvershoot);
            return Mathf.Max(0.03f, shotInterval - windupDuration - overshoot);
        }

        /// <summary>
        /// Miss chance of slow-firing single-target towers vs unslowed fast
        /// enemies (speed >= TDEnemy.FastEvadeSpeedThreshold). AoE and fire
        /// rates above 1.1/s never miss; at/below 1.0/s the chance scales from
        /// 0.18 up to 0.30 at 0.5/s. Slowed enemies lose evasion entirely
        /// (checked by the caller).
        /// </summary>
        /// <summary>
        /// Long Rail Cannon pierce chain (expansion tower 12): a straight line
        /// resolves every enemy at fire time, each taking the previous entry's
        /// damage times the falloff — floored, never below 1. The last target
        /// takes a bonus multiplier BEFORE the floor (Full Bore's +30% line
        /// end). Sequential-on-integers is the pinned semantics: the sheet's
        /// 34/23/16/11/7 chain only reproduces this way.
        /// </summary>
        public static int[] ResolvePierceDamageChain(int baseDamage, float falloff, int targetCount, float lastTargetBonus = 1f)
        {
            var count = Mathf.Max(0, targetCount);
            if (count == 0 || baseDamage <= 0)
            {
                return new int[0];
            }

            var chain = new int[count];

            var clampedFalloff = Mathf.Clamp(falloff, 0.05f, 1f);
            var damage = baseDamage;
            for (var i = 0; i < count; i++)
            {
                var multiplier = i == 0 ? 1f : clampedFalloff;
                if (i == count - 1)
                {
                    multiplier *= Mathf.Max(1f, lastTargetBonus);
                }

                damage = Mathf.Max(1, Mathf.FloorToInt(damage * multiplier));
                chain[i] = damage;
            }

            return chain;
        }

        /// <summary>
        /// Full Bore (damage spec) fights the falloff table with 1.0 — zero
        /// decay on the line. Design errata b08df07: the pure chain already
        /// supported it; this pins the CALLER's choice so the wiring cannot
        /// silently regress behind a passing pure-function test again.
        /// </summary>
        public static float ResolvePierceShotFalloff(bool fullBore, float tableFalloff)
        {
            return fullBore ? 1f : tableFalloff;
        }

        public static float FastEnemyMissChance(float shotsPerSecond, float aoeRadius)
        {
            if (aoeRadius > 0f || shotsPerSecond > 1.1f)
            {
                return 0f;
            }

            if (shotsPerSecond >= 1.0f)
            {
                return 0.18f;
            }

            return Mathf.Lerp(0.18f, 0.30f, Mathf.InverseLerp(1.0f, 0.5f, shotsPerSecond));
        }
    }
}
