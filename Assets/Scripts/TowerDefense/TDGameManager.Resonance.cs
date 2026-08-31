// Freeze-period move: Resonance cluster.
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
        public float GetTowerDamageMultiplier(TDTowerKind towerKind)
        {
            var multiplier = GetCampaignTowerPowerMultiplier();
            if (!IsResonanceWindowActive)
            {
                return multiplier;
            }

            multiplier *= 1.10f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.16f;
                multiplier *= GetDoctrineCommandPowerMultiplier(TDResonanceCommand.EmberSurge);
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark &&
                (towerKind == TDTowerKind.FrostCoil || towerKind == TDTowerKind.SiegeDrill))
            {
                multiplier *= 1.08f;
            }

            return multiplier;
        }

        public float GetTowerFireRateMultiplier(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var multiplier = 1.12f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.20f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark &&
                (towerKind == TDTowerKind.RailLancer || towerKind == TDTowerKind.ArcWelder))
            {
                multiplier *= 1.08f;
            }

            if (_matrixConvergenceTriggeredThisWindow && _activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= MatrixConvergenceEmberFireRateMultiplier;
            }

            return multiplier;
        }

        public float GetProjectileSpeedMultiplier(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var multiplier = 1.06f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.12f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark &&
                (towerKind == TDTowerKind.RailLancer || towerKind == TDTowerKind.EmberFlak))
            {
                multiplier *= 1.10f;
            }

            return multiplier;
        }

        public float GetAoeRadiusMultiplier(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var supportsAoe =
                towerKind == TDTowerKind.CinderMortar ||
                towerKind == TDTowerKind.ArcWelder ||
                towerKind == TDTowerKind.EmberFlak ||
                towerKind == TDTowerKind.ResonanceBeacon ||
                towerKind == TDTowerKind.GravSnare;
            if (!supportsAoe)
            {
                return 1f;
            }

            var multiplier = 1.08f;
            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.18f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
            {
                multiplier *= 1.08f;
            }

            if (towerKind == TDTowerKind.CinderMortar || towerKind == TDTowerKind.GravSnare)
            {
                multiplier *= 1.04f;
            }

            return multiplier;
        }

        public float GetSlowStrengthMultiplier(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 1f;
            }

            var supportsSlow =
                towerKind == TDTowerKind.FrostCoil ||
                towerKind == TDTowerKind.GravSnare ||
                towerKind == TDTowerKind.ResonanceBeacon;
            if (!supportsSlow)
            {
                return 1f;
            }

            var multiplier = 1.10f;
            if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
            {
                multiplier *= 1.25f;
            }
            else if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= 1.08f;
            }

            return multiplier;
        }

        public float GetSlowDurationBonus(TDTowerKind towerKind)
        {
            if (!IsResonanceWindowActive)
            {
                return 0f;
            }

            var supportsSlow =
                towerKind == TDTowerKind.FrostCoil ||
                towerKind == TDTowerKind.GravSnare ||
                towerKind == TDTowerKind.ResonanceBeacon;
            if (!supportsSlow)
            {
                return 0f;
            }

            if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
            {
                return 0.55f;
            }

            if (_activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                return 0.20f;
            }

            return 0f;
        }

        private void UpdateResonanceState()
        {
            if (!_isResonanceSystemEnabled)
            {
                ResetResonanceState();
                return;
            }

            if (!IsResonanceWindowActive)
            {
                return;
            }

            _resonanceWindowTimer = Mathf.Max(0f, _resonanceWindowTimer - Time.deltaTime);
            _resonanceCharge = Mathf.Clamp(
                ResonanceChargeMax * (_resonanceWindowTimer / ResonanceWindowDuration),
                0f,
                ResonanceChargeMax);

            if (_resonanceWindowTimer <= 0f)
            {
                EndResonanceWindow();
                return;
            }

            if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
            {
                _resonanceMarkPulseTimer -= Time.deltaTime;
                if (_resonanceMarkPulseTimer <= 0f)
                {
                    PulseResonanceMarks();
                    _resonanceMarkPulseTimer = ResonanceMarkPulseInterval;
                }
            }
        }

        private void TrySelectResonanceCommand(TDResonanceCommand command)
        {
            if (!_isResonanceSystemEnabled)
            {
                return;
            }

            if (!IsResonanceWindowActive || command == TDResonanceCommand.None)
            {
                return;
            }

            if (_activeResonanceCommand != TDResonanceCommand.None)
            {
                SetStatus($"Resonance command locked: {GetResonanceCommandLabel(_activeResonanceCommand)}");
                PlaySfxTone("resonance_locked", 220f, 0.10f, 0.42f, false);
                return;
            }

            _activeResonanceCommand = command;
            _resonanceCommandsUsed++;
            if (command == TDResonanceCommand.EmberSurge)
            {
                _emberSurgeUses++;
                PlaySfxTone("resonance_ember_surge", 550f, 0.18f, 0.78f, true);
            }
            else if (command == TDResonanceCommand.FractureMark)
            {
                _fractureMarkUses++;
                PulseResonanceMarks();
                _resonanceMarkPulseTimer = ResonanceMarkPulseInterval;
                PlaySfxTone("resonance_fracture_mark", 420f, 0.19f, 0.76f, false);
            }

            var threatMatched = IsResonanceCommandMatchForCurrentThreat(command);
            if (threatMatched)
            {
                _resonanceMatchedCommands++;
            }

            var resonanceTarget = _activeEnemies.FirstOrDefault(enemy => enemy != null);
            _battlePresentation?.EmitFeedback(
                TDBattleFeedbackKind.Resonance,
                resonanceTarget != null ? resonanceTarget.transform.position : Vector3.zero,
                threatMatched ? "MATCH" : GetResonanceCommandShortLabel(command),
                threatMatched ? TDBattleFeedbackTier.Critical : TDBattleFeedbackTier.Tactical);

            var doctrinePower = GetDoctrineCommandPowerMultiplier(command);
            if (doctrinePower > 1f)
            {
                _doctrineEmpoweredCommands++;
                PushTacticalEvent(
                    $"{GetDoctrineShortLabel(_activeResonanceDoctrine)} doctrine amplified {GetResonanceCommandShortLabel(command)} +{Mathf.RoundToInt((doctrinePower - 1f) * 100f)}%",
                    5.0f);
            }

            var chainBonusTriggered = TryApplyResonanceChainBonus(command);
            if (!chainBonusTriggered)
            {
                var chainLabel = threatMatched
                    ? $"Match {_resonanceChainMatchStreak}/{ResonanceChainRequiredMatches}"
                    : "NoMatch (streak reset)";
                SetStatus($"Resonance command engaged: {GetResonanceCommandLabel(command)} [{chainLabel}]");
                if (!threatMatched)
                {
                    // Teaching copy step 3: read the wave before pressing.
                    ShowResonanceTipOnce(
                        "first_nomatch",
                        "Read the wave before you press. Armored, heavy or boss-heavy waves want Ember Surge. Fast, swarm or flanking waves want Fracture Mark.",
                        "先看这一波是什么敌人，再选：装甲、重装、BOSS 当道 → 余烬涌动（Z）是对的；快速、虫群、侧翼突袭 → 裂痕标记（X）是对的。",
                        8.0f);
                }
            }
        }

        private void ResetResonanceState()
        {
            CaptureMatrixWindowPeak();
            _resonanceWindowTimer = 0f;
            _resonanceCharge = 0f;
            _activeResonanceCommand = TDResonanceCommand.None;
            _resonanceMarkPulseTimer = 0f;
            _resonanceChainMatchStreak = 0;
            ResetMatrixWindowState();
        }

    }
}
