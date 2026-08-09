namespace TD
{
    public readonly struct TDTowerPresentationProfile
    {
        public readonly float chargeDuration;
        public readonly float chargePulseSpeed;
        public readonly float chargeStartCoverage;
        public readonly float chargeEndCoverage;
        public readonly float attackDuration;
        public readonly float attackKick;
        public readonly float upgradeDuration;
        public readonly float upgradeEndCoverage;
        public readonly string chargeRhythmId;
        public readonly string projectileLanguageId;
        public readonly string impactShapeId;
        public readonly string upgradeMotionId;

        public TDTowerPresentationProfile(
            float chargeDuration,
            float chargePulseSpeed,
            float chargeStartCoverage,
            float chargeEndCoverage,
            float attackDuration,
            float attackKick,
            float upgradeDuration,
            float upgradeEndCoverage,
            string chargeRhythmId,
            string projectileLanguageId,
            string impactShapeId,
            string upgradeMotionId)
        {
            this.chargeDuration = chargeDuration;
            this.chargePulseSpeed = chargePulseSpeed;
            this.chargeStartCoverage = chargeStartCoverage;
            this.chargeEndCoverage = chargeEndCoverage;
            this.attackDuration = attackDuration;
            this.attackKick = attackKick;
            this.upgradeDuration = upgradeDuration;
            this.upgradeEndCoverage = upgradeEndCoverage;
            this.chargeRhythmId = chargeRhythmId;
            this.projectileLanguageId = projectileLanguageId;
            this.impactShapeId = impactShapeId;
            this.upgradeMotionId = upgradeMotionId;
        }
    }

    public static class TDTowerPresentationProfiles
    {
        public static TDTowerPresentationProfile Get(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => new TDTowerPresentationProfile(
                    0.28f, 5.8f, 0.44f, 0.62f, 0.14f, 0.042f, 0.48f, 0.96f,
                    "rail_lock", "needle_bolt", "linear_pierce", "rivet_rise"),
                TDTowerKind.CinderMortar => new TDTowerPresentationProfile(
                    0.38f, 3.2f, 0.48f, 0.60f, 0.24f, 0.026f, 0.74f, 1.08f,
                    "mortar_fuse", "weighted_shell", "ember_bloom", "furnace_lift"),
                TDTowerKind.FrostCoil => new TDTowerPresentationProfile(
                    0.22f, 4.0f, 0.46f, 0.60f, 0.20f, 0.018f, 0.62f, 0.98f,
                    "cryo_condense", "crystal_shard", "crystal_cross", "cryo_lock"),
                TDTowerKind.ArcWelder => new TDTowerPresentationProfile(
                    0.20f, 7.2f, 0.42f, 0.58f, 0.13f, 0.020f, 0.50f, 0.94f,
                    "arc_stutter", "chain_spark", "electric_fork", "spark_snap"),
                TDTowerKind.SiegeDrill => new TDTowerPresentationProfile(
                    0.40f, 2.7f, 0.50f, 0.62f, 0.26f, 0.050f, 0.82f, 1.12f,
                    "siege_spool", "heavy_bore", "hex_break", "ram_lock"),
                TDTowerKind.EmberFlak => new TDTowerPresentationProfile(
                    0.14f, 8.8f, 0.40f, 0.56f, 0.10f, 0.030f, 0.44f, 0.92f,
                    "flak_redline", "burst_pellet", "star_burst", "redline_pop"),
                TDTowerKind.ResonanceBeacon => new TDTowerPresentationProfile(
                    0.25f, 4.8f, 0.48f, 0.61f, 0.22f, 0.012f, 0.68f, 1.02f,
                    "beacon_sync", "signal_orbit", "concentric_pulse", "relay_rise"),
                TDTowerKind.GravSnare => new TDTowerPresentationProfile(
                    0.36f, 2.3f, 0.50f, 0.62f, 0.28f, 0.014f, 0.78f, 1.10f,
                    "grav_compress", "gravity_orbit", "implosion_disc", "singularity_fold"),
                _ => new TDTowerPresentationProfile(
                    0.24f, 4f, 0.46f, 0.60f, 0.18f, 0.024f, 0.60f, 1f,
                    "standard", "standard", "standard", "standard")
            };
        }
    }
}
