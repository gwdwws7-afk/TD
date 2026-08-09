using UnityEngine;

namespace TD
{
    public readonly struct TDTowerVisualIdentity
    {
        public readonly Color accent;
        public readonly string iconResourcePath;
        public readonly string roleLabel;
        public readonly string markerLabel;

        public TDTowerVisualIdentity(Color accent, string iconResourcePath, string roleLabel, string markerLabel)
        {
            this.accent = accent;
            this.iconResourcePath = iconResourcePath;
            this.roleLabel = roleLabel;
            this.markerLabel = markerLabel;
        }
    }

    public static class TDUiVisualIdentity
    {
        public const string WaveIconPath = "Art/UI/P11/hud_wave";
        public const string IntegrityIconPath = "Art/UI/P11/hud_integrity";
        public const string BudgetIconPath = "Art/UI/P11/hud_budget";
        public const string BuildIconPath = "Art/UI/P11/hud_build";
        public const string DamageIconPath = "Art/UI/P11/hud_damage";
        public const string UtilityIconPath = "Art/UI/P11/hud_utility";
        public const string RouteIconPath = "Art/UI/P11/hud_route";
        public const string EnemyIconPath = "Art/UI/P11/hud_enemy";
        public const string SpeedIconPath = "Art/UI/P11/hud_speed";
        public const string PauseIconPath = "Art/UI/P11/hud_pause";

        public static TDTowerVisualIdentity GetTower(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => new TDTowerVisualIdentity(
                    new Color(0.25f, 0.54f, 0.86f, 1f), "Art/UI/P11/tower_rail_lancer", "PIERCE", "LANCE"),
                TDTowerKind.CinderMortar => new TDTowerVisualIdentity(
                    new Color(0.83f, 0.41f, 0.19f, 1f), "Art/UI/P11/tower_cinder_mortar", "BLAST", "RING"),
                TDTowerKind.FrostCoil => new TDTowerVisualIdentity(
                    new Color(0.31f, 0.76f, 0.91f, 1f), "Art/UI/P11/tower_frost_coil", "SLOW", "FLAKE"),
                TDTowerKind.ArcWelder => new TDTowerVisualIdentity(
                    new Color(0.22f, 0.80f, 0.76f, 1f), "Art/UI/P11/tower_arc_welder", "CHAIN", "LINK"),
                TDTowerKind.SiegeDrill => new TDTowerVisualIdentity(
                    new Color(0.84f, 0.65f, 0.22f, 1f), "Art/UI/P11/tower_siege_drill", "BREAK", "CRACK"),
                TDTowerKind.EmberFlak => new TDTowerVisualIdentity(
                    new Color(0.93f, 0.41f, 0.20f, 1f), "Art/UI/P11/tower_ember_flak", "SWARM", "PELLETS"),
                TDTowerKind.ResonanceBeacon => new TDTowerVisualIdentity(
                    new Color(0.41f, 0.81f, 0.45f, 1f), "Art/UI/P11/tower_resonance_beacon", "SUPPORT", "SIGNAL"),
                TDTowerKind.GravSnare => new TDTowerVisualIdentity(
                    new Color(0.45f, 0.52f, 0.89f, 1f), "Art/UI/P11/tower_grav_snare", "CONTROL", "WELL"),
                _ => new TDTowerVisualIdentity(Color.white, "Art/UI/P11/tower_rail_lancer", "TOWER", "MARK")
            };
        }
    }
}
