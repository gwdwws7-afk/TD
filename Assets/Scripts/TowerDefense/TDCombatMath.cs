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
        public const int DamageFloor = 1;

        /// <summary>
        /// Hybrid armor model: flat subtraction PLUS percentage mitigation.
        /// Each point of effective armor also reduces damage by 4% (capped at
        /// 60%), so high-armor enemies are a real wall for low-per-hit towers
        /// while armor-piercing becomes mandatory. The flat floor stays at 1
        /// so chip damage is always possible. Callers apply exposure and
        /// armor-break modifiers to the inputs before calling.
        /// </summary>
        public static int ResolveArmoredDamage(int damage, int effectiveArmor)
        {
            var armorPercentReduction = Mathf.Min(ArmorPercentCap, effectiveArmor * ArmorPercentPerPoint);
            var afterPercent = damage * (1f - armorPercentReduction);
            return Mathf.Max(DamageFloor, Mathf.RoundToInt(afterPercent - effectiveArmor));
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
