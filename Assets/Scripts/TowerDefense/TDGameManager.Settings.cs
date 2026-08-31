// Freeze-period move: Settings cluster.
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
        private void HandlePauseResume()
        {
            _pauseMenu?.Hide();
            SetBattlePlaybackSpeed(_lastActivePlaybackSpeed > 0 ? _lastActivePlaybackSpeed : 1f, false);
            PlaySfxTone("ui_panel_close", 420f, 0.08f, 0.48f, false);
        }

        private void HandlePauseRestart()
        {
            _pauseMenu?.Hide();
            RestartCurrentScene();
        }

        private void HandlePauseQuitToTitle()
        {
            _pauseMenu?.Hide();
            // Reload the scene — Awake will rebuild the title screen.
            // Reset deployment so the title appears.
            _campaignDeploymentConfirmed = false;
            _skipTitleForAutomation = false;
            LoadingTransition("RETURNING TO TITLE", "EMBERLINE DEFENSE");
        }

        /// <summary>Skip title screen on next Awake — used by MCP automation.</summary>
        private void LoadP123PresentationPreferences()
        {
            _uiScale = Mathf.Clamp(PlayerPrefs.GetFloat(P123UiScaleKey, 1f), 1f, 1.2f);
            _subtitlesEnabled = PlayerPrefs.GetInt(P123SubtitlesKey, 1) > 0;
            _captionsEnabled = PlayerPrefs.GetInt(P123CaptionsKey, 1) > 0;
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P123MasterVolumeKey, 1f));
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P123MusicVolumeKey, 0.7f));
            _effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P123EffectsVolumeKey, 1f));
        }

        private void HandleSettingsOpenStateChanged(bool open)
        {
            if (!open)
            {
                // Single flush point for the volume sliders (see Set*Volume).
                PlayerPrefs.Save();
            }

            if (open)
            {
                _settingsPauseOwned = !_playbackPaused;
                if (_settingsPauseOwned)
                {
                    SetBattlePlaybackSpeed(0f, false);
                }

                return;
            }

            if (_settingsPauseOwned)
            {
                _settingsPauseOwned = false;
                SetBattlePlaybackSpeed(_lastActivePlaybackSpeed, false);
            }

            if (EventSystem.current != null && _uiSettingsButton != null)
            {
                EventSystem.current.SetSelectedGameObject(_uiSettingsButton.gameObject);
            }
        }

        private void SetUiLanguage(TDUiLanguage language)
        {
            TDLocalization.SetLanguage(language);
            if (_battleCanvas != null)
            {
                TDLocalization.RefreshLabels(_battleCanvas.gameObject, _uiFont);
            }

            _battlePresentation?.RefreshLocalization();
            _settingsPanel?.Refresh();
            _missionBoardNeedsRefresh = true;
            RefreshTutorialUi();
            UpdateBattleUi();
        }

        private void RefreshUiScaleForScreen(bool force = false)
        {
            if (_battleCanvasScaler == null || (!force && _lastUiScaleScreenHeight == Screen.height))
            {
                return;
            }

            var effectiveUiScale = GetEffectiveUiScale();
            _battleCanvasScaler.referenceResolution = new Vector2(1440f / effectiveUiScale, 900f / effectiveUiScale);
            _lastUiScaleScreenHeight = Screen.height;
        }

        private void ToggleSubtitles()
        {
            _subtitlesEnabled = !_subtitlesEnabled;
            PlayerPrefs.SetInt(P123SubtitlesKey, _subtitlesEnabled ? 1 : 0);
            PlayerPrefs.Save();
            _battlePresentation?.SetCaptionState(_subtitlesEnabled, _captionsEnabled);
        }

        private void ToggleCaptions()
        {
            _captionsEnabled = !_captionsEnabled;
            PlayerPrefs.SetInt(P123CaptionsKey, _captionsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            _battlePresentation?.SetCaptionState(_subtitlesEnabled, _captionsEnabled);
        }

        private void ResetP123PresentationDefaults()
        {
            TDLocalization.SetLanguage(TDUiLanguage.English);
            _uiScale = 1f;
            _subtitlesEnabled = true;
            _captionsEnabled = true;
            _masterVolume = 1f;
            _musicVolume = 0.7f;
            _effectsVolume = 1f;
            _colorblindMarkersEnabled = true;
            _largeTextEnabled = Screen.height <= 768;
            PlayerPrefs.SetFloat(P123UiScaleKey, _uiScale);
            PlayerPrefs.SetInt(P123SubtitlesKey, 1);
            PlayerPrefs.SetInt(P123CaptionsKey, 1);
            PlayerPrefs.SetFloat(P123MasterVolumeKey, _masterVolume);
            PlayerPrefs.SetFloat(P123MusicVolumeKey, _musicVolume);
            PlayerPrefs.SetFloat(P123EffectsVolumeKey, _effectsVolume);
            PlayerPrefs.SetInt(P9MarkersEnabledKey, 1);
            PlayerPrefs.SetInt(P9LargeTextEnabledKey, _largeTextEnabled ? 1 : 0);
            PlayerPrefs.Save();
            SetUiScale(_uiScale);
            ApplySfxVolumes();
            ApplyLargeTextMode();
            _battlePresentation?.SetAccessibilityState(_colorblindMarkersEnabled, _largeTextEnabled);
            _battlePresentation?.SetCaptionState(_subtitlesEnabled, _captionsEnabled);
            SetUiLanguage(TDUiLanguage.English);
        }

        private void SetBattlePlaybackSpeed(float requestedSpeed)
        {
            SetBattlePlaybackSpeed(requestedSpeed, true);
        }

        private void SetBattlePlaybackSpeed(float requestedSpeed, bool persist)
        {
            if (requestedSpeed <= 0f)
            {
                _playbackPaused = true;
                Time.timeScale = 0f;
            }
            else
            {
                _playbackSpeed = Mathf.Clamp(Mathf.Round(requestedSpeed), 1f, 3f);
                _lastActivePlaybackSpeed = _playbackSpeed;
                _playbackPaused = false;
                Time.timeScale = _playbackSpeed;
                if (persist)
                {
                    PlayerPrefs.SetFloat(P9PlaybackSpeedKey, _playbackSpeed);
                    PlayerPrefs.Save();
                }
            }

            _battlePresentation?.SetPlaybackState(_lastActivePlaybackSpeed, _playbackPaused);
        }

        private void ToggleColorblindMarkers()
        {
            _colorblindMarkersEnabled = !_colorblindMarkersEnabled;
            PlayerPrefs.SetInt(P9MarkersEnabledKey, _colorblindMarkersEnabled ? 1 : 0);
            PlayerPrefs.Save();
            _battlePresentation?.SetAccessibilityState(_colorblindMarkersEnabled, _largeTextEnabled);
            PushTacticalEvent($"Shape markers {(_colorblindMarkersEnabled ? "enabled" : "disabled")}", 3.2f);
        }

        private void ToggleLargeText()
        {
            _largeTextEnabled = !_largeTextEnabled;
            PlayerPrefs.SetInt(P9LargeTextEnabledKey, _largeTextEnabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyLargeTextMode();
            _battlePresentation?.SetAccessibilityState(_colorblindMarkersEnabled, _largeTextEnabled);
        }

        private void IncrementTutorialTelemetry(string eventName)
        {
            var key = $"{P123TutorialTelemetryPrefix}{TDCampaignProgression.ActiveSaveSlot}_{eventName}";
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);
            PlayerPrefs.Save();
        }

        private void HandleHotkeys()
        {
            for (var i = 0; i < TowerHotkeys.Length; i++)
            {
                if (!TDInputCompat.GetKeyDown(TowerHotkeys[i]))
                {
                    continue;
                }

                if (i >= _unlockedTowerKinds.Count)
                {
                    SetStatus($"Tower slot [{i + 1}] is locked.");
                    break;
                }

                _selectedTowerKind = _unlockedTowerKinds[i];
                break;
            }

            if (TDInputCompat.GetKeyDown(KeyCode.Q))
            {
                _selectedUpgradeBranch = TDTowerUpgradeBranch.Damage;
            }
            else if (TDInputCompat.GetKeyDown(KeyCode.E))
            {
                _selectedUpgradeBranch = TDTowerUpgradeBranch.Utility;
            }

            if (TDInputCompat.GetKeyDown(KeyCode.U))
            {
                TryUpgradeSelectedTowerFromUi(_selectedUpgradeBranch);
            }

            if (TDInputCompat.GetKeyDown(KeyCode.S))
            {
                TrySellSelectedTowerFromUi();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Debug-only level stepping cheats — never compiled into release players.
            if (TDInputCompat.GetKeyDown(KeyCode.F5))
            {
                TryStepCampaignLevel(-1);
            }
            else if (TDInputCompat.GetKeyDown(KeyCode.F6))
            {
                TryStepCampaignLevel(1);
            }
#endif

            if (TDInputBindings.GetKeyDown(TDInputAction.StartWave) ||
                TDInputCompat.GetGamepadButtonDown(TDGamepadButton.North))
            {
                TryRequestWaveStart();
            }

            if (TDInputBindings.GetKeyDown(TDInputAction.ScenarioCommand) ||
                TDInputCompat.GetGamepadButtonDown(TDGamepadButton.West))
            {
                TryActivateScenarioMechanic();
            }

            if (!IsResonanceWindowActive)
            {
                return;
            }

            if (TDInputCompat.GetKeyDown(KeyCode.Z))
            {
                TrySelectResonanceCommand(TDResonanceCommand.EmberSurge);
            }
            else if (TDInputCompat.GetKeyDown(KeyCode.X))
            {
                TrySelectResonanceCommand(TDResonanceCommand.FractureMark);
            }
        }

    }
}
