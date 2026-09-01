// Freeze-period move: Scenario cluster.
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
        private void PresentExamBeat(TDExamPresentationStage stage)
        {
            if (_examPresentationProfile == null || stage <= _examPresentationStage)
            {
                return;
            }

            _examPresentationStage = stage;
            _examScenarioDevice?.SetStage(stage);
            switch (stage)
            {
                case TDExamPresentationStage.Opening:
                    _examOpeningBeatCount++;
                    break;
                case TDExamPresentationStage.Escalation:
                    _examEscalationBeatCount++;
                    break;
                case TDExamPresentationStage.Decision:
                    _examDecisionBeatCount++;
                    break;
                default:
                    return;
            }

            ShowExamBeatVisual(stage);
        }

        private int GetScenarioCommandCost()
        {
            return _activeScenarioMechanic == null
                ? 0
                : TDEconomyTuning.GetScenarioCommandCost(
                    _activeScenarioMechanic.budgetCost,
                    _scenarioCostMultiplier,
                    _wave,
                    GetConfiguredWaveCount(),
                    _scenarioUses);
        }

        private bool CanActivateScenarioMechanic(out string reason)
        {
            reason = string.Empty;
            if (_activeScenarioMechanic == null || _gameOver || !_campaignDeploymentConfirmed)
            {
                reason = "Scenario command unavailable.";
                return false;
            }

            if (string.Equals(_currentWavePhase, "introduce", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Observe the mechanic during Introduce; command unlocks at Reinforce.";
                return false;
            }

            if (_activeScenarioMechanic.maxCharges > 0 && _scenarioCharges <= 0)
            {
                reason = "Scenario device has no charges remaining.";
                return false;
            }

            if (_defenseBudget < GetScenarioCommandCost())
            {
                reason = $"Scenario command needs {GetScenarioCommandCost()} budget.";
                return false;
            }

            var type = NormalizeGroupToken(_activeScenarioMechanic.mechanicType);
            if ((type == "signal_gate" || type == "route_switch" || type == "timed_reinforcement") && !_isInPrepPhase)
            {
                reason = "Scenario route commands are available during prep.";
                return false;
            }

            if (type == "signal_gate" && _scenarioWaveDelayBonus > 0f)
            {
                reason = "Signal gate is already holding this wave.";
                return false;
            }

            if (type == "timed_reinforcement" && _scenarioReinforcementPending)
            {
                reason = "Reserve reinforcement is already inbound.";
                return false;
            }

            if ((type == "environment_device" || type == "boss_phase") && (_isInPrepPhase || _activeEnemies.Count == 0))
            {
                reason = "Scenario device requires active enemies.";
                return false;
            }

            if (type == "boss_phase" && !_activeEnemies.Any(enemy => enemy != null && enemy.HasAnyTag("boss", "final", "elite")))
            {
                reason = "Phase breaker requires an active boss.";
                return false;
            }

            return true;
        }

        private void TryActivateScenarioMechanic()
        {
            if (!CanActivateScenarioMechanic(out var reason))
            {
                SetStatus(reason);
                return;
            }

            var type = NormalizeGroupToken(_activeScenarioMechanic.mechanicType);
            var commandCost = GetScenarioCommandCost();
            _defenseBudget -= commandCost;
            TrackP125ScenarioSpend(commandCost);
            if (_activeScenarioMechanic.maxCharges > 0)
            {
                _scenarioCharges = Mathf.Max(0, _scenarioCharges - 1);
            }

            _scenarioUses++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
            TrackP135ScenarioActivation(type);
#endif
            switch (type)
            {
                case "route_switch":
                    _scenarioRouteBias = _scenarioRouteBias == "center" ? "left" : _scenarioRouteBias == "left" ? "right" : "center";
                    PushTacticalEvent($"Route switch: center traffic -> {_scenarioRouteBias}", 5.2f);
                    PlaySfxTone("scenario_route_switch", 430f, 0.18f, 0.72f, true);
                    break;
                case "timed_reinforcement":
                    if (_scenarioReinforcementRoutine != null)
                    {
                        StopCoroutine(_scenarioReinforcementRoutine);
                    }

                    _scenarioReinforcementRoutine = StartCoroutine(DeliverScenarioReinforcement());
                    PushTacticalEvent("Reserve train dispatched. Hold until arrival or start under-strength.", 6.0f);
                    PlaySfxTone("scenario_reinforcement_train", 520f, 0.22f, 0.72f, true);
                    break;
                case "environment_device":
                    ActivateScenarioEnvironmentDevice();
                    PlaySfxTone("scenario_kiln_purge", 360f, 0.20f, 0.74f, false);
                    break;
                case "boss_phase":
                    ActivateScenarioBossBreaker();
                    PlaySfxTone("scenario_boss_breaker", 240f, 0.24f, 0.80f, true);
                    break;
                default:
                    _scenarioWaveDelayBonus = Mathf.Max(1f, _activeScenarioMechanic.effectDurationSeconds);
                    PushTacticalEvent($"Signal gate armed: enemy deployment held {_scenarioWaveDelayBonus:0.0}s", 5.2f);
                    PlaySfxTone("scenario_signal_gate", 480f, 0.16f, 0.70f, true);
                    break;
            }

            _examScenarioDevice?.TriggerActivation();
            UpdateExamScenarioDevice();
            PlaySfxTone("scenario_command", 430f, 0.18f, 0.72f, true);
            SetStatus($"Scenario command active: {_activeScenarioMechanic.displayName}.");
            AdvanceTutorial(TDFirstRunTutorialStep.UseScenario);
        }

        private IEnumerator DeliverScenarioReinforcement()
        {
            _scenarioReinforcementPending = true;
            yield return new WaitForSeconds(Mathf.Max(1f, _activeScenarioMechanic?.reinforcementDelaySeconds ?? 6f));
            if (!_gameOver)
            {
                var reward = Mathf.Max(10, (_activeScenarioMechanic?.budgetCost ?? 10) * 2);
                _defenseBudget += reward;
                TrackP125ReinforcementIncome(reward);
                PushTacticalEvent($"Reserve train arrived: +{reward} budget", 5.4f);
                PlaySfxTone("scenario_reinforcement", 610f, 0.22f, 0.78f, true);
                _examScenarioDevice?.TriggerActivation();
            }

            _scenarioReinforcementPending = false;
        }

        private void ActivateScenarioEnvironmentDevice()
        {
            var affected = 0;
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                enemy.ApplyArmorBreak(3, Mathf.Max(3f, _activeScenarioMechanic.effectDurationSeconds));
                // Scenario devices are a designed exception to boss stagger
                // immunity (boss-design-spec common rule) — forced through.
                enemy.ApplyStagger(0.8f, enemy.HasTag("boss") ? 0.72f : 0.18f, true);
                enemy.TakeHit(Mathf.Max(6, Mathf.RoundToInt(enemy.MaxHealth * 0.08f)), 0.38f, 3.5f);
                if (string.Equals(enemy.EnemyId, "kiln_custodian", StringComparison.Ordinal))
                {
                    // Kiln Purge vs its boss: cuts the stacked plate and stalls
                    // the forge — the one true answer to the armor wall.
                    enemy.OnKilnPurge();
                }
                affected++;
            }

            PushTacticalEvent($"Kiln purge: {affected} enemies scorched, broken and staggered", 5.6f);
        }

        private void ActivateScenarioBossBreaker()
        {
            var affected = 0;
            for (var i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null || !enemy.HasAnyTag("boss", "final", "elite"))
                {
                    continue;
                }

                enemy.ApplyExposed(Mathf.Max(4f, _activeScenarioMechanic.effectDurationSeconds), 1.22f);
                enemy.ApplyStagger(1.2f, 0.36f);
                affected++;
            }

            _scenarioBossPhaseSuppressed = true;
            PushTacticalEvent($"Phase breaker: {affected} boss target exposed; next surge suppressed", 6.0f);
        }

        private void UpdateScenarioBossPhases()
        {
            if (_activeScenarioMechanic == null ||
                NormalizeGroupToken(_activeScenarioMechanic.mechanicType) != "boss_phase" ||
                _isInPrepPhase || _gameOver)
            {
                return;
            }

            TDEnemy boss = null;
            for (var i = 0; i < _activeEnemies.Count; i++)
            {
                var enemy = _activeEnemies[i];
                if (enemy != null && enemy.HasAnyTag("boss", "final", "elite"))
                {
                    boss = enemy;
                    break;
                }
            }

            if (boss == null)
            {
                return;
            }

            var thresholds = _activeScenarioMechanic.bossPhaseThresholds;
            if (thresholds == null || thresholds.Length == 0)
            {
                thresholds = new[] { 0.70f, 0.35f };
            }

            while (_scenarioBossPhase < thresholds.Length && boss.HealthRatio <= thresholds[_scenarioBossPhase])
            {
                TriggerScenarioBossPhase(boss, _scenarioBossPhase + 2);
                _scenarioBossPhase++;
                _examScenarioDevice?.TriggerActivation();
            }
        }

        private void ConfigureScenarioMechanic(
            TDCampaignScenarioMechanicDefinition mechanic,
            TDCampaignScenarioPlan scenario)
        {
            _activeScenarioMechanic = mechanic;
            var intensity = Mathf.Clamp(scenario?.intensity ?? 1, 1, 3);
            _scenarioCharges = mechanic == null || mechanic.maxCharges <= 0
                ? mechanic?.maxCharges ?? 0
                : mechanic.maxCharges + (scenario?.milestoneExam == true ? 1 : 0) + Mathf.Max(0, intensity - 2);
            _scenarioRouteBias = "center";
            _scenarioUses = 0;
            _scenarioOpportunities = 0;
            _scenarioWaveDelayBonus = 0f;
            _scenarioReinforcementPending = false;
            _scenarioBossPhaseSuppressed = false;
            _scenarioBossPhase = 0;
        }

    }
}
