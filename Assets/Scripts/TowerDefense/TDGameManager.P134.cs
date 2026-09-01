#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TD
{
    public sealed partial class TDGameManager
    {
        public string DebugPrepareP134ForTest()
        {
            var baseFixture = DebugPrepareP133ForTest();
            _battlePresentation?.ResetDiagnostics(true);
            _battlePresentation?.Tick(false);
            TDCombatFxBudget.ResetDiagnostics();

            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None)
                .OrderBy(tower => (int)tower.Kind)
                .ThenBy(tower => tower.GridCell.y)
                .ThenBy(tower => tower.GridCell.x)
                .ToArray();
            for (var i = 0; i < towers.Length; i++)
            {
                var readability = towers[i].Readability;
                if (readability == null)
                {
                    continue;
                }

                readability.DebugHoldCharge(Mathf.Lerp(0.24f, 1f, (i + 1f) / Mathf.Max(1f, towers.Length)));
                readability.DebugPlayAttack();
                readability.DebugPlayUpgrade(
                    i % 2 == 0 ? TDTowerUpgradeBranch.Damage : TDTowerUpgradeBranch.Utility,
                    1 + (i % 3));
            }

            var feedbackKinds = (TDBattleFeedbackKind[])Enum.GetValues(typeof(TDBattleFeedbackKind));
            var feedbackDetails = new[] { "18", "-8", "42%", "MATRIX", "SYNC", "-3", "96", "128" };
            for (var i = 0; i < feedbackKinds.Length; i++)
            {
                _battlePresentation?.EmitFeedback(
                    feedbackKinds[i],
                    new Vector3(-4.2f + (i * 1.18f), 0.55f + ((i % 2) * 0.36f), 0f),
                    feedbackDetails[Mathf.Min(i, feedbackDetails.Length - 1)],
                    i >= 5 ? TDBattleFeedbackTier.Critical :
                    i == 0 ? TDBattleFeedbackTier.Routine : TDBattleFeedbackTier.Tactical);
            }

            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.WaveTransition,
                "[W]",
                "WAVE TRANSITION",
                "FORMATION LOCKED",
                TDBattleFeedbackTier.Critical,
                0.82f);
            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.DangerousLane,
                "[R!]",
                "DANGER LANE  LEFT",
                "62% PRESSURE",
                TDBattleFeedbackTier.Critical,
                0.92f);
            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.BossPhase,
                "[B!]",
                "BOSS PHASE 2",
                "OVERDRIVE",
                TDBattleFeedbackTier.Critical,
                1.08f);
            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.DefenseBreach,
                "[!]",
                "DEFENSE BREACH",
                "EXIT COMPROMISED",
                TDBattleFeedbackTier.Critical,
                1.16f);

            PlaySfxTone("p134_hit", 640f, 0.06f, 0.34f, false);
            PlaySfxTone("p134_critical_hit", 980f, 0.09f, 0.58f, true);
            PlaySfxTone("p134_armor_break", 320f, 0.14f, 0.62f, false);
            PlaySfxTone("p134_slow", 450f, 0.12f, 0.44f, false);
            PlaySfxTone("p134_specialization", 840f, 0.13f, 0.54f, true);
            PlaySfxTone("p134_resonance", 690f, 0.20f, 0.68f, true);
            PlaySfxTone("p134_boss_damage", 250f, 0.12f, 0.54f, false);
            PlayCriticalSfxTone("p134_leak", 205f, 0.18f, 0.72f, false);
            PlaySfxTone("p134_wave_transition", 590f, 0.13f, 0.58f, true);
            PlaySfxTone("p134_danger_lane", 370f, 0.18f, 0.68f, true);
            PlayCriticalSfxTone("p134_boss_phase", 245f, 0.28f, 0.84f, true);
            PlayCriticalSfxTone("p134_defense_breach", 180f, 0.22f, 0.82f, false);
            var towerKinds = (TDTowerKind[])Enum.GetValues(typeof(TDTowerKind));
            for (var i = 0; i < towerKinds.Length; i++)
            {
                _nextTowerFireAudioTime = 0f;
                NotifyTowerFired(towerKinds[i]);
            }

            var focusTarget = GetP134FocusTarget();
            if (EventSystem.current != null && focusTarget != null)
            {
                EventSystem.current.SetSelectedGameObject(focusTarget.gameObject);
            }

            Canvas.ForceUpdateCanvases();
            return
                $"p13.4.fixture.ready=True towers={towers.Length} feedback={feedbackKinds.Length} " +
                $"cinematics={_battlePresentation?.CinematicCount ?? 0} fx={TDCombatFxBudget.ActiveTotal} " +
                $"focus={focusTarget?.name ?? "none"}\n{baseFixture}";
        }

        public string DebugAuditP134ForTest()
        {
            var feedbackKinds = (TDBattleFeedbackKind[])Enum.GetValues(typeof(TDBattleFeedbackKind));
            var feedbackPass = _battlePresentation != null &&
                               feedbackKinds.All(kind => _battlePresentation.GetFeedbackCount(kind) > 0);
            var signalBudgetPass = _battlePresentation != null &&
                                   _battlePresentation.MaximumObservedSignals <= 12 &&
                                   _battlePresentation.MaximumSignalDuration <= 1.05f + 0.001f &&
                                   _battlePresentation.MaximumSignalAlpha <= 0.96f + 0.001f;

            var cinematicKinds = (TDBattleCinematicKind[])Enum.GetValues(typeof(TDBattleCinematicKind));
            var cinematicPass = _battlePresentation != null &&
                                cinematicKinds.All(kind => _battlePresentation.GetCinematicCount(kind) > 0);

            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            var towerKinds = towers.Select(tower => tower.Kind).Distinct().OrderBy(kind => (int)kind).ToArray();
            var expectedTowerKinds = (TDTowerKind[])Enum.GetValues(typeof(TDTowerKind));
            var readability = towers
                .Select(tower => tower.Readability)
                .Where(item => item != null)
                .ToArray();
            var chargeIds = readability.Select(item => item.ChargeRhythmId).Distinct(StringComparer.Ordinal).ToArray();
            var projectileIds = readability.Select(item => item.ProjectileLanguageId).Distinct(StringComparer.Ordinal).ToArray();
            var impactIds = readability.Select(item => item.ImpactShapeId).Distinct(StringComparer.Ordinal).ToArray();
            var upgradeIds = readability.Select(item => item.UpgradeMotionId).Distinct(StringComparer.Ordinal).ToArray();
            var towerIdentityPass = towerKinds.Length == expectedTowerKinds.Length &&
                                    readability.Length >= expectedTowerKinds.Length &&
                                    chargeIds.Length == expectedTowerKinds.Length &&
                                    projectileIds.Length == expectedTowerKinds.Length &&
                                    impactIds.Length == expectedTowerKinds.Length &&
                                    upgradeIds.Length == expectedTowerKinds.Length &&
                                    readability.All(item =>
                                        item.ChargeVisible &&
                                        item.AttackPresentationCount > 0 &&
                                        item.UpgradePresentationCount > 0);

            var missingProjectileResources = new List<string>();
            for (var i = 0; i < towerKinds.Length; i++)
            {
                var kind = towerKinds[i];
                if (Resources.Load<Sprite>(TDProjectile.GetProjectileResourcePath(kind)) == null)
                {
                    missingProjectileResources.Add($"{kind}:projectile");
                }
                if (Resources.Load<Sprite>(TDProjectile.GetImpactResourcePath(kind)) == null)
                {
                    missingProjectileResources.Add($"{kind}:impact");
                }
            }
            var projectilePass = towerKinds.Length == expectedTowerKinds.Length && missingProjectileResources.Count == 0;

            var expectedAudioKeys = new[]
            {
                "p134_hit",
                "p134_critical_hit",
                "p134_armor_break",
                "p134_slow",
                "p134_specialization",
                "p134_resonance",
                "p134_boss_damage",
                "p134_leak",
                "p134_wave_transition",
                "p134_danger_lane",
                "p134_boss_phase",
                "p134_defense_breach",
                "tower_fire_raillancer",
                "tower_fire_cindermortar",
                "tower_fire_frostcoil",
                "tower_fire_arcwelder",
                "tower_fire_siegedrill",
                "tower_fire_emberflak",
                "tower_fire_resonancebeacon",
                "tower_fire_gravsnare",
                "tower_fire_slagburner",
                "tower_fire_salvagederrick",
                "tower_fire_railbarricade",
                "tower_fire_longrailcannon"
            };
            var audioPass = _sfxSource != null && _tacticalSfxSource != null && _criticalSfxSource != null &&
                            expectedAudioKeys.All(key => _sfxClipCache.ContainsKey(key));

            var selectables = _battleCanvas != null
                ? _battleCanvas.GetComponentsInChildren<Selectable>(true)
                : Array.Empty<Selectable>();
            var focusVisuals = _battleCanvas != null
                ? _battleCanvas.GetComponentsInChildren<TDUiFocusVisual>(true)
                : Array.Empty<TDUiFocusVisual>();
            var selectedObject = EventSystem.current?.currentSelectedGameObject;
            var selectedFocus = selectedObject != null ? selectedObject.GetComponent<TDUiFocusVisual>() : null;
            var keyboardPass = TowerHotkeys.Length == expectedTowerKinds.Length &&
                               TDInputBindings.GetKey(TDInputAction.StartWave) != KeyCode.None &&
                               TDInputBindings.GetKey(TDInputAction.Pause) != KeyCode.None;
            var mousePass = _mainCamera != null && towers.All(tower => tower.GetComponent<Collider2D>() != null);
            var gamepadPass = Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.Start) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.Select) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.North) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.West) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.LeftShoulder) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.RightShoulder) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.DpadUp) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.DpadDown) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.DpadLeft) &&
                              Enum.IsDefined(typeof(TDGamepadButton), TDGamepadButton.DpadRight);
            var focusPass = EventSystem.current != null &&
                            selectables.Length > 0 &&
                            focusVisuals.Length == selectables.Length &&
                            selectedObject != null &&
                            selectedFocus != null &&
                            selectedFocus.IsFocused;
            var inputPass = keyboardPass && mousePass && gamepadPass && focusPass;

            var fxPass = TDCombatFxBudget.ActiveTotal <= TDCombatFxBudget.MaxTotal &&
                         TDCombatFxBudget.MaximumObserved <= TDCombatFxBudget.MaxTotal &&
                         TDCombatFxBudget.MaximumAcceptedDuration <= 0.90f + 0.001f &&
                         TDCombatFxBudget.MaximumAcceptedAlpha <= 0.96f + 0.001f;
            var pass = feedbackPass && signalBudgetPass && cinematicPass && towerIdentityPass &&
                       projectilePass && audioPass && inputPass && fxPass;
            return
                $"p13.4.audit.feedback8={feedbackPass} [counts={string.Join(",", feedbackKinds.Select(kind => $"{kind}:{_battlePresentation?.GetFeedbackCount(kind) ?? 0}"))}]\n" +
                $"p13.4.audit.signalBudget={signalBudgetPass} [active={_battlePresentation?.ActiveSignalCount ?? 0},max={_battlePresentation?.MaximumObservedSignals ?? 0},suppressed={_battlePresentation?.SuppressedSignalCount ?? 0},duration={(_battlePresentation?.MaximumSignalDuration ?? 0f):0.00},alpha={(_battlePresentation?.MaximumSignalAlpha ?? 0f):0.00}]\n" +
                $"p13.4.audit.cinematics={cinematicPass} [counts={string.Join(",", cinematicKinds.Select(kind => $"{kind}:{_battlePresentation?.GetCinematicCount(kind) ?? 0}"))}]\n" +
                $"p13.4.audit.towerIdentity={towerIdentityPass} [kinds={towerKinds.Length},charge={chargeIds.Length},projectile={projectileIds.Length},impact={impactIds.Length},upgrade={upgradeIds.Length}]\n" +
                $"p13.4.audit.projectileResources={projectilePass} [missing={(missingProjectileResources.Count == 0 ? "none" : string.Join(",", missingProjectileResources))}]\n" +
                $"p13.4.audit.audio={audioPass} [clips={expectedAudioKeys.Count(key => _sfxClipCache.ContainsKey(key))}/{expectedAudioKeys.Length},sources={(_sfxSource != null ? 1 : 0) + (_tacticalSfxSource != null ? 1 : 0) + (_criticalSfxSource != null ? 1 : 0)}/3]\n" +
                $"p13.4.audit.input={inputPass} [keyboard={keyboardPass},mouse={mousePass},gamepad={gamepadPass},focus={focusPass},selectables={selectables.Length},visuals={focusVisuals.Length},selected={selectedObject?.name ?? "none"}]\n" +
                $"p13.4.audit.fxBudget={fxPass} [active={TDCombatFxBudget.ActiveTotal}/{TDCombatFxBudget.MaxTotal},max={TDCombatFxBudget.MaximumObserved},suppressed={TDCombatFxBudget.SuppressedCount},duration={TDCombatFxBudget.MaximumAcceptedDuration:0.00},alpha={TDCombatFxBudget.MaximumAcceptedAlpha:0.00}]\n" +
                TDCombatFxBudget.BuildAuditReport() + "\n" +
                $"p13.4.audit.pass={pass}\n";
        }

        private Selectable GetP134FocusTarget()
        {
            if (_uiStartWaveButton != null && _uiStartWaveButton.gameObject.activeInHierarchy &&
                _uiStartWaveButton.interactable)
            {
                return _uiStartWaveButton;
            }

            return _battleCanvas != null
                ? _battleCanvas.GetComponentsInChildren<Selectable>(true)
                    .FirstOrDefault(item => item != null && item.gameObject.activeInHierarchy && item.interactable)
                : null;
        }
    }
}
#endif
