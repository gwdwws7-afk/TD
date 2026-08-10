"""Verify all ResolveSfxResourcePath outputs map to real .wav files."""
import os

ROOT = os.path.join("Assets", "Resources", "Audio")

# Every resource path produced by ResolveSfxResourcePath (without the Audio/ prefix,
# since AudioBasePath="Audio" is prepended in code).
TEST_PATHS = [
    "SFX/Tower/fire_rail_lancer", "SFX/Tower/fire_cinder_mortar",
    "SFX/Tower/fire_frost_coil", "SFX/Tower/fire_arc_welder",
    "SFX/Tower/fire_siege_drill", "SFX/Tower/fire_ember_flak",
    "SFX/Tower/fire_resonance_beacon", "SFX/Tower/fire_grav_snare",
    "SFX/UI/tower_place", "SFX/UI/tower_upgrade", "SFX/UI/wave_start",
    "SFX/UI/wave_clear", "SFX/UI/hover", "SFX/UI/panel_open",
    "SFX/UI/panel_close", "SFX/UI/level_select", "SFX/UI/deploy_confirm",
    "SFX/UI/early_dispatch", "SFX/UI/tutorial_advance",
    "SFX/UI/tutorial_complete", "SFX/UI/chapter_reward",
    "Music/victory_stinger", "Music/defeat_stinger", "Music/menu_theme",
    "Music/resonance_window", "Music/combat_chapter_a",
    "Music/combat_chapter_b", "Music/combat_chapter_c",
    "Music/combat_chapter_d",
    "Ambience/grayline_junction", "Ambience/ashfall_depot",
    "Ambience/split_switch_canyon", "Ambience/hollow_kiln_basin",
    "Ambience/last_ember_terminus",
    "SFX/Enemy/death_generic", "SFX/Enemy/spore_split",
    "SFX/Enemy/mimic_shift", "SFX/Enemy/burrow_ambush",
    "SFX/Enemy/elite_pressure", "SFX/Enemy/attrition_siphon",
    "SFX/Enemy/support_link", "SFX/Enemy/enemy_leak",
    "SFX/Enemy/boss_phase_shift", "SFX/Enemy/boss_spawn",
    "SFX/Status/armor_break", "SFX/Status/slow_apply",
    "SFX/Status/expose_mark", "SFX/Status/specialization_ult",
    "SFX/Resonance/window_open", "SFX/Resonance/window_close",
    "SFX/Resonance/ember_surge", "SFX/Resonance/fracture_mark",
    "SFX/Resonance/matrix_convergence",
    "SFX/Hit/routine_hit", "SFX/Hit/critical_hit", "SFX/Hit/boss_hit",
    "SFX/Scenario/route_switch", "SFX/Scenario/reinforcement_train",
    "SFX/Scenario/kiln_purge", "SFX/Scenario/boss_breaker",
    "SFX/Scenario/signal_gate",
]

missing = []
for p in TEST_PATHS:
    full = os.path.join(ROOT, p + ".wav")
    if not os.path.isfile(full):
        missing.append(p)

if missing:
    print("MISSING FILES (%d):" % len(missing))
    for m in missing:
        print("  -", m)
    exit(1)
else:
    print("All %d resource paths resolve to existing .wav files." % len(TEST_PATHS))
