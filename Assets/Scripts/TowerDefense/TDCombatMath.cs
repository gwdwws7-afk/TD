using UnityEngine;

namespace TD
{
    /// <summary>
    /// Pure combat-resolution math shared by TDEnemy/TDTower and the EditMode
    /// test suites. Everything here must stay free of scene/instance state so
    /// the balance tests exercise the exact production formula instead of a
    /// drifting mirror of it.
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
