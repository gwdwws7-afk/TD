using UnityEngine;

namespace TD
{
    /// <summary>
    /// Slag Burner's burn stacking (expansion tower 9). Pure math only —
    /// application lives on TDEnemy/TDProjectile/TDGameManager.
    ///
    /// Burn is its own DoT slot (independent of slow/expose by design note)
    /// and ticks every <see cref="BurnTickInterval"/> seconds for
    /// layers × damage/layer. Tick damage takes the enemy's FLAT armor only —
    /// never the percentage hybrid — so heavy armor still walls burn out
    /// while armor break restores it (design ruling B1 family: this is the
    /// only new rounding point allowed, and it floors at 1).
    /// </summary>
    public static class TDBurnSystem
    {
        public const int MaxBurnLayers = 6;
        public const float BurnTickInterval = 0.5f;
        public const float DetonateMultiplier = 2.0f;

        /// <summary>
        /// One burn tick against flat armor (break restored first).
        /// Round on entry is the caller's job; this resolves and floors.
        /// </summary>
        public static int ResolveBurnTick(int rawTick, int enemyArmorFlat, int armorBreakFlat)
        {
            var effectiveArmor = Mathf.Max(0, enemyArmorFlat - Mathf.Max(0, armorBreakFlat));
            return Mathf.Max(1, rawTick - effectiveArmor);
        }

        public static int ClampStacks(int layers)
        {
            return Mathf.Clamp(layers, 0, MaxBurnLayers);
        }

        public static int ResolveTickRawDamage(int layers, float damagePerLayerPerSecond)
        {
            return Mathf.RoundToInt(layers * damagePerLayerPerSecond * BurnTickInterval);
        }

        /// <summary>
        /// Slag Sump burst: full stacks resolve at once at 2.0× the per-second
        /// rate. The burst is a direct hit — it goes through the regular
        /// GetModifiedDamageForEnemy + TakeHit pipeline, not this system's
        /// flat-armor channel.
        /// </summary>
        public static int ResolveDetonateDamage(int layers, float damagePerLayerPerSecond)
        {
            return Mathf.RoundToInt(layers * damagePerLayerPerSecond * DetonateMultiplier);
        }
    }
}
