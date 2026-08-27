// Freeze-period S3: the audio service cluster moved verbatim from
// TDGameManager.cs — SFX tone synthesis, the music state machine with
// resume positions, ambience mapping, mixer setup, and the volume
// setters (settings-panel flush point documented there).
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TD
{
    public sealed partial class TDGameManager : MonoBehaviour
    {
        private void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            // PlayerPrefs.Save() is flushed once on settings close — sliders
            // fire this every drag frame and a per-frame disk flush stalled
            // low-end machines (review P2).
            PlayerPrefs.SetFloat(P123MasterVolumeKey, _masterVolume);
            ApplySfxVolumes();
        }

        private void SetMusicVolume(float value)
        {
            _musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(P123MusicVolumeKey, _musicVolume);
            ApplySfxVolumes();
        }

        private void SetEffectsVolume(float value)
        {
            _effectsVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(P123EffectsVolumeKey, _effectsVolume);
            ApplySfxVolumes();
        }

        public void PlayEnemySfx(string key, float volumeScale = 1f)
        {
            PlaySfxTone(key, 440f, 0.12f, volumeScale, false);
        }

        private void ConfigureSfx()
        {
            LoadAudioMixer();

            _sfxSource = GetComponent<AudioSource>();
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
            }

            _tacticalSfxSource = gameObject.AddComponent<AudioSource>();
            _criticalSfxSource = gameObject.AddComponent<AudioSource>();
            ConfigureSfxSource(_sfxSource, 0f);
            ConfigureSfxSource(_tacticalSfxSource, 0f);
            ConfigureSfxSource(_criticalSfxSource, 0f);

            _musicSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource = gameObject.AddComponent<AudioSource>();
            ConfigureLoopSource(_musicSource);
            ConfigureLoopSource(_ambienceSource);

            RouteAudioSourcesToMixer();
            ApplySfxVolumes();
        }

        private void LoadAudioMixer()
        {
            _emberlineMixer = Resources.Load<AudioMixer>(AudioBasePath + "/EmberlineMixer");
            if (_emberlineMixer == null)
            {
                return;
            }

            var groups = _emberlineMixer.FindMatchingGroups(string.Empty);
            foreach (var group in groups)
            {
                if (group.name == "Music") _mixerMusicGroup = group;
                else if (group.name == "SFX") _mixerSfxGroup = group;
                else if (group.name == "Ambience") _mixerAmbienceGroup = group;
            }
        }

        private void RouteAudioSourcesToMixer()
        {
            if (_emberlineMixer == null)
            {
                return;
            }

            if (_mixerMusicGroup != null && _musicSource != null)
            {
                _musicSource.outputAudioMixerGroup = _mixerMusicGroup;
            }

            if (_mixerAmbienceGroup != null && _ambienceSource != null)
            {
                _ambienceSource.outputAudioMixerGroup = _mixerAmbienceGroup;
            }

            // Route SFX sources to sub-groups if they exist, otherwise the main SFX group.
            if (_mixerSfxGroup != null)
            {
                // GetChildGroups returns the immediate child AudioMixerGroups of this group.
                var subGroups = _emberlineMixer.FindMatchingGroups("SFX");
                RouteSfxSource(_sfxSource, subGroups, "Routine");
                RouteSfxSource(_tacticalSfxSource, subGroups, "Tactical");
                RouteSfxSource(_criticalSfxSource, subGroups, "Critical");
            }
        }

        private void RouteSfxSource(AudioSource source, AudioMixerGroup[] subGroups, string groupName)
        {
            if (source == null)
            {
                return;
            }

            foreach (var sg in subGroups)
            {
                if (sg.name == groupName)
                {
                    source.outputAudioMixerGroup = sg;
                    return;
                }
            }

            source.outputAudioMixerGroup = _mixerSfxGroup;
        }

        private void ApplySfxVolumes()
        {
            var mix = Mathf.Clamp01(_masterVolume) * Mathf.Clamp01(_effectsVolume);
            if (_sfxSource != null)
            {
                _sfxSource.volume = SfxDefaultVolume * 0.78f * mix;
            }

            if (_tacticalSfxSource != null)
            {
                _tacticalSfxSource.volume = SfxDefaultVolume * mix;
            }

            if (_criticalSfxSource != null)
            {
                _criticalSfxSource.volume = SfxDefaultVolume * 1.12f * mix;
            }

            var musicMix = Mathf.Clamp01(_masterVolume) * Mathf.Clamp01(_musicVolume);
            if (_musicSource != null)
            {
                _musicSource.volume = 0.42f * musicMix;
            }

            if (_ambienceSource != null)
            {
                _ambienceSource.volume = 0.55f * musicMix;
            }
        }

        private static void ConfigureSfxSource(AudioSource source, float volume)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = Mathf.Clamp01(volume);
        }

        private static string ResolveMapAmbiencePath(string mapId)
        {
            return mapId switch
            {
                "grayline_junction" => "Ambience/grayline_junction",
                "ashfall_depot" => "Ambience/ashfall_depot",
                "split_switch_canyon" => "Ambience/split_switch_canyon",
                "hollow_kiln_basin" => "Ambience/hollow_kiln_basin",
                "last_ember_terminus" => "Ambience/last_ember_terminus",
                _ => "Ambience/grayline_junction",
            };
        }

        private string ResolveChapterMusicPath()
        {
            var levelIndex = _campaignRoute?.level?.levelIndex ?? 1;
            var chapter = Mathf.Clamp((levelIndex - 1) / 5, 0, 3);
            return chapter switch
            {
                0 => "Music/combat_chapter_a",
                1 => "Music/combat_chapter_b",
                2 => "Music/combat_chapter_c",
                _ => "Music/combat_chapter_d",
            };
        }

        private void StartAmbienceForMap(string mapId)
        {
            if (_ambienceSource == null)
            {
                return;
            }

            var path = ResolveMapAmbiencePath(mapId);
            if (_ambienceClip == null || _ambienceClip.name != System.IO.Path.GetFileNameWithoutExtension(path))
            {
                _ambienceClip = Resources.Load<AudioClip>(AudioBasePath + "/" + path);
            }

            if (_ambienceClip != null && _ambienceSource.clip != _ambienceClip)
            {
                _ambienceSource.clip = _ambienceClip;
                _ambienceSource.Play();
            }
        }

        private void UpdateMusicState()
        {
            if (_musicSource == null)
            {
                return;
            }

            string targetState;
            string targetPath;

            if (_gameOver)
            {
                targetState = _victory ? "victory" : "defeat";
                targetPath = _victory ? "Music/victory_stinger" : "Music/defeat_stinger";
            }
            else if (_missionBoardOpen)
            {
                targetState = "menu";
                targetPath = "Music/menu_theme";
            }
            else if (IsResonanceWindowActive)
            {
                targetState = "resonance";
                targetPath = "Music/resonance_window";
            }
            else
            {
                targetState = "combat";
                targetPath = ResolveChapterMusicPath();
            }

            if (_activeMusicState == targetState && _musicClip != null)
            {
                return;
            }

            // Remember where the looping track was when we switched away, so
            // returning to it (e.g. combat after a resonance window) resumes
            // instead of restarting the whole piece every time.
            if (_musicClip != null && _musicSource.loop && _musicSource.isPlaying)
            {
                _musicResumePositions[_activeMusicPath] = _musicSource.time;
            }

            _activeMusicState = targetState;
            _activeMusicPath = targetPath;
            TransitionMusicSnapshot(targetState);
            var newClip = Resources.Load<AudioClip>(AudioBasePath + "/" + targetPath);
            if (newClip == null)
            {
                return;
            }

            // Stingers (victory/defeat) play once and do not loop; everything else loops.
            var isStinger = targetState == "victory" || targetState == "defeat";
            _musicSource.loop = !isStinger;
            _musicClip = newClip;
            _musicSource.clip = newClip;
            _musicSource.Play();
            if (!isStinger &&
                _musicResumePositions.TryGetValue(targetPath, out var resumeAt) &&
                resumeAt > 0.5f &&
                resumeAt < newClip.length - 0.5f)
            {
                _musicSource.time = resumeAt;
            }
        }

        /// <summary>
        /// Transition the AudioMixer snapshot based on the current music state.
        /// Ducking: boss/resonance states lower music volume so SFX cut through.
        /// Falls back gracefully if no mixer or snapshots are configured.
        /// </summary>
        private void TransitionMusicSnapshot(string state)
        {
            if (_emberlineMixer == null)
            {
                return;
            }

            var snapshotName = state switch
            {
                "resonance" => "Resonance",
                "victory" => "Victory",
                "defeat" => "Defeat",
                "menu" => "Normal",
                _ => "Normal",
            };

            var snapshot = _emberlineMixer.FindSnapshot(snapshotName);
            snapshot?.TransitionTo(0.8f);
        }

        private void PlaySfxTone(string key, float frequency, float duration, float volumeScale = 1f, bool rising = false)
        {
            if (_sfxSource == null || volumeScale <= 0f || duration <= 0f || frequency <= 0f)
            {
                return;
            }

            if (!_sfxClipCache.TryGetValue(key, out var clip) || clip == null)
            {
                var resourcePath = ResolveSfxResourcePath(key);
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    clip = Resources.Load<AudioClip>(AudioBasePath + "/" + resourcePath);
                }

                if (clip == null)
                {
                    clip = CreateSfxClip(key, frequency, duration, rising);
                }

                if (clip == null)
                {
                    return;
                }

                _sfxClipCache[key] = clip;
            }

            var source = IsRoutineSfxKey(key) ? _sfxSource : _tacticalSfxSource;
            source?.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private void PlayCriticalSfxTone(string key, float frequency, float duration, float volumeScale = 1f, bool rising = false)
        {
            if (_criticalSfxSource == null || volumeScale <= 0f || duration <= 0f || frequency <= 0f)
            {
                return;
            }

            if (!_sfxClipCache.TryGetValue(key, out var clip) || clip == null)
            {
                var resourcePath = ResolveSfxResourcePath(key);
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    clip = Resources.Load<AudioClip>(AudioBasePath + "/" + resourcePath);
                }

                if (clip == null)
                {
                    clip = CreateSfxClip(key, frequency, duration, rising);
                }

                if (clip == null)
                {
                    return;
                }

                _sfxClipCache[key] = clip;
            }

            _criticalSfxSource.Stop();
            _criticalSfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private string ResolveSfxResourcePath(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            // Exact-key matches first (most specific).
            switch (key)
            {
                case "tower_build":
                    return "SFX/UI/tower_place";
                case "wave_start":
                case "wave_transition":
                    return "SFX/UI/wave_start";
                case "wave_clear":
                    return "SFX/UI/wave_clear";
                case "run_victory":
                    return "Music/victory_stinger";
                case "run_defeat":
                    return "Music/defeat_stinger";
                case "resonance_ready":
                    return "SFX/Resonance/window_open";
                case "resonance_end":
                    return "SFX/Resonance/window_close";
                case "resonance_locked":
                    return "SFX/Resonance/window_close";
                case "resonance_ember_surge":
                    return "SFX/Resonance/ember_surge";
                case "resonance_fracture_mark":
                    return "SFX/Resonance/fracture_mark";
                case "resonance_chain_bonus":
                    return "SFX/Resonance/matrix_convergence";
                case "boss_phase":
                    return "SFX/Enemy/boss_phase_shift";
                case "boss_warning":
                    return "SFX/Enemy/boss_spawn";
                case "critical_defense":
                    return "SFX/Hit/boss_hit";
                case "enemy_death":
                    return "SFX/Enemy/death_generic";
                case "enemy_spore_split":
                    return "SFX/Enemy/spore_split";
                case "enemy_mimic_shift":
                    return "SFX/Enemy/mimic_shift";
                case "enemy_burrow_ambush":
                    return "SFX/Enemy/burrow_ambush";
                case "enemy_elite_pressure":
                    return "SFX/Enemy/elite_pressure";
                case "enemy_attrition":
                    return "SFX/Enemy/attrition_siphon";
                case "enemy_support_link":
                    return "SFX/Enemy/support_link";
                case "status_expose":
                    return "SFX/Status/expose_mark";
                case "specialization_ult":
                    return "SFX/Status/specialization_ult";
                case "feedback_armor_break":
                case "p121_armor_break":
                case "p134_armor_break":
                    return "SFX/Status/armor_break";
                case "feedback_slow":
                case "p121_slow_control":
                case "p134_slow":
                    return "SFX/Status/slow_apply";
                case "feedback_special_damage":
                case "feedback_special_utility":
                case "p134_specialization":
                case "p121_specialization":
                    return "SFX/Status/specialization_ult";
                case "leak_default":
                case "leak_attrition":
                case "p121_leak":
                case "p134_leak":
                    return "SFX/Enemy/enemy_leak";
                case "p134_boss_phase":
                    return "SFX/Enemy/boss_phase_shift";
                case "p134_boss_damage":
                    return "SFX/Hit/boss_hit";
                case "p134_defense_breach":
                    return "SFX/Hit/boss_hit";
                case "p134_danger_lane":
                case "danger_lane":
                    return "SFX/Scenario/route_switch";
                case "p134_critical_hit":
                case "feedback_critical":
                    return "SFX/Hit/critical_hit";
                case "p134_hit":
                case "p121_feedback_hit":
                case "feedback_hit":
                    return "SFX/Hit/routine_hit";
                case "p134_resonance":
                case "p121_resonance":
                    return "SFX/Resonance/window_open";
                case "p134_wave_transition":
                    return "SFX/UI/wave_start";
                case "scenario_command":
                    return "SFX/Scenario/route_switch";
                case "scenario_reinforcement":
                    return "SFX/Scenario/reinforcement_train";
                case "ui_hover":
                    return "SFX/UI/hover";
                case "ui_click":
                    return "SFX/UI/click_confirm";
                case "ui_panel_open":
                    return "SFX/UI/panel_open";
                case "ui_panel_close":
                    return "SFX/UI/panel_close";
                case "ui_level_select":
                    return "SFX/UI/level_select";
                case "ui_deploy":
                    return "SFX/UI/deploy_confirm";
                case "ui_early_dispatch":
                    return "SFX/UI/early_dispatch";
                case "ui_tutorial_advance":
                    return "SFX/UI/tutorial_advance";
                case "ui_tutorial_complete":
                    return "SFX/UI/tutorial_complete";
                case "ui_chapter_reward":
                    return "SFX/UI/chapter_reward";
                case "scenario_route_switch":
                    return "SFX/Scenario/route_switch";
                case "scenario_reinforcement_train":
                    return "SFX/Scenario/reinforcement_train";
                case "scenario_kiln_purge":
                    return "SFX/Scenario/kiln_purge";
                case "scenario_boss_breaker":
                    return "SFX/Scenario/boss_breaker";
                case "scenario_signal_gate":
                    return "SFX/Scenario/signal_gate";
            }

            // Dynamic-key families: tower fire / upgrade / exam beats / matrix convergence.
            var lower = key.ToLowerInvariant();

            if (lower.StartsWith("tower_fire_", StringComparison.Ordinal))
            {
                return ResolveTowerFirePath(lower);
            }

            if (lower.StartsWith("tower_upgrade_", StringComparison.Ordinal))
            {
                return "SFX/UI/tower_upgrade";
            }

            if (lower.StartsWith("feedback_hit", StringComparison.Ordinal) ||
                lower.StartsWith("p121_feedback_hit", StringComparison.Ordinal))
            {
                return "SFX/Hit/routine_hit";
            }

            if (lower.StartsWith("feedback_critical", StringComparison.Ordinal) ||
                lower.StartsWith("p134_critical", StringComparison.Ordinal))
            {
                return "SFX/Hit/critical_hit";
            }

            if (lower.StartsWith("feedback_boss_damage", StringComparison.Ordinal) ||
                lower.StartsWith("p134_boss_damage", StringComparison.Ordinal))
            {
                return "SFX/Hit/boss_hit";
            }

            if (lower.StartsWith("matrix_convergence", StringComparison.Ordinal))
            {
                return "SFX/Resonance/matrix_convergence";
            }

            // Exam presentation beats map to scenario/level-select for now.
            if (lower.StartsWith("exam_", StringComparison.Ordinal))
            {
                return "SFX/UI/level_select";
            }

            return null;
        }

        private static AudioClip CreateSfxClip(string key, float frequency, float duration, bool rising)
        {
            var sampleCount = Mathf.Max(64, Mathf.CeilToInt(duration * SfxSampleRate));
            var data = new float[sampleCount];
            var phase = 0f;
            var metallic = SfxKeyContains(key, "hit") || SfxKeyContains(key, "armor") || SfxKeyContains(key, "build");
            var controlled = SfxKeyContains(key, "slow") || SfxKeyContains(key, "fracture");
            var percussive = SfxKeyContains(key, "mortar") || SfxKeyContains(key, "flak") ||
                             SfxKeyContains(key, "siege");
            var energized = SfxKeyContains(key, "arcwelder") || SfxKeyContains(key, "frostcoil") ||
                            SfxKeyContains(key, "resonancebeacon") || SfxKeyContains(key, "gravsnare");
            var alarm = SfxKeyContains(key, "boss") || SfxKeyContains(key, "leak") ||
                        SfxKeyContains(key, "defeat") || SfxKeyContains(key, "critical");
            var harmonic = SfxKeyContains(key, "resonance") || SfxKeyContains(key, "upgrade") ||
                           SfxKeyContains(key, "victory") || SfxKeyContains(key, "convergence");
            var noiseState = 2166136261u;
            for (var i = 0; i < key.Length; i++)
            {
                noiseState = (noiseState ^ key[i]) * 16777619u;
            }

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / Mathf.Max(1f, sampleCount - 1f);
                var currentFrequency = rising
                    ? Mathf.Lerp(frequency * 0.75f, frequency * 1.25f, t)
                    : Mathf.Lerp(frequency * 1.12f, frequency * 0.88f, t);
                phase += (2f * Mathf.PI * currentFrequency) / SfxSampleRate;

                noiseState = (noiseState * 1664525u) + 1013904223u;
                var noise = (((noiseState >> 8) & 0x00FFFFFF) / 8388607.5f) - 1f;
                var voice = Mathf.Sin(phase);
                if (harmonic)
                {
                    voice = (voice * 0.66f) + (Mathf.Sin(phase * 2.01f) * 0.23f) +
                            (Mathf.Sin(phase * 0.5f) * 0.18f);
                }

                if (metallic)
                {
                    voice = (voice * 0.50f) + (Mathf.Sin(phase * 2.73f) * 0.20f) +
                            (noise * (1f - t) * 0.38f);
                }

                if (controlled)
                {
                    voice = (voice * 0.62f) + (Mathf.Sin(phase * 0.52f) * 0.28f) + (noise * 0.06f);
                }

                if (percussive)
                {
                    voice = (voice * 0.46f) + (Mathf.Sin(phase * 0.31f) * 0.22f) +
                            (noise * (1f - t) * 0.32f);
                }

                if (energized)
                {
                    voice = (voice * 0.58f) + (Mathf.Sin(phase * 1.73f) * 0.20f) +
                            (Mathf.Sin(phase * 3.11f) * 0.12f);
                }

                if (alarm)
                {
                    var pulse = 0.72f + (Mathf.Sin(t * Mathf.PI * 6f) * 0.18f);
                    voice = ((Mathf.Sin(phase * 0.50f) * 0.58f) + (voice * 0.34f) + (noise * 0.08f)) * pulse;
                }

                var attack = Mathf.Clamp01(t / (metallic ? 0.025f : 0.08f));
                var release = Mathf.Clamp01((1f - t) / (alarm ? 0.34f : 0.22f));
                var envelope = attack * release;
                data[i] = Mathf.Clamp(voice * envelope * 0.42f, -0.92f, 0.92f);
            }

            var clip = AudioClip.Create($"td_sfx_{key}", sampleCount, 1, SfxSampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static bool IsRoutineSfxKey(string key)
        {
            return SfxKeyContains(key, "feedback_hit") || SfxKeyContains(key, "tower_build") ||
                   SfxKeyContains(key, "tower_fire");
        }

        private static bool SfxKeyContains(string key, string token)
        {
            return !string.IsNullOrEmpty(key) && key.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}

// S3 verify
