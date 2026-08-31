// Freeze-period S6: the wave state machine moved verbatim from TDGameManager.cs — WaveLoopFromConfig/FallbackWaveLoop coroutines, prep gating, per-group spawning, lane resolution, and dispatch.
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
        private List<string> BuildWavePreviewLaneKeys(TDWaveDefinition wave)
        {
            var lanes = new List<string>();
            if (wave?.groups != null)
            {
                for (var i = 0; i < wave.groups.Length; i++)
                {
                    var keys = ResolvePreviewLaneKeys(wave.groups[i]);
                    for (var k = 0; k < keys.Count; k++)
                    {
                        if (!lanes.Contains(keys[k]))
                        {
                            lanes.Add(keys[k]);
                        }
                    }
                }
            }

            if (lanes.Count == 0 && _activeLanePaths.ContainsKey("default"))
            {
                lanes.Add("default");
            }

            return lanes;
        }

        private string ResolveExistingLaneKey(string laneKey)
        {
            var lane = NormalizeGroupToken(laneKey);
            if (string.IsNullOrEmpty(lane))
            {
                lane = "default";
            }

            if (lane == "split_lane" && !_activeLanePaths.ContainsKey(lane) && _activeLanePaths.ContainsKey("left"))
            {
                lane = "left";
            }
            else if (lane == "cross_lane" && !_activeLanePaths.ContainsKey(lane) && _activeLanePaths.ContainsKey("right"))
            {
                lane = "right";
            }

            if (_activeLanePaths.ContainsKey(lane))
            {
                return lane;
            }

            return "default";
        }

        private IReadOnlyList<Vector3> GetSpawnPathForLane(string laneKey)
        {
            var lane = ResolveExistingLaneKey(laneKey);
            if (_activeLanePaths.TryGetValue(lane, out var path) && path != null && path.Count > 1)
            {
                return path;
            }

            return GetDefaultSpawnPath();
        }

        private bool CanStartCurrentWave()
        {
            return _isInPrepPhase && !_gameOver && !IsOpeningWaveBuildRequired();
        }

        private void TryRequestWaveStart()
        {
            if (_gameOver || !_isInPrepPhase)
            {
                return;
            }

            if (IsOpeningWaveBuildRequired())
            {
                SetStatus("Build one tower before starting the first wave.");
                return;
            }

            if (_waveStartRequested)
            {
                return;
            }

            if (_prepDuration > 0f && _prepCountdown > 0.05f)
            {
                _waveDispatchedEarly = true;
                _earlyDispatchCount++;
                PlaySfxTone("ui_early_dispatch", 660f, 0.12f, 0.58f, true);
            }

            _waveStartRequested = true;
            _prepCountdown = 0f;
            SetStatus($"Wave {_wave} starting.");
            var readiness = CaptureWaveStartReadiness();
            PushTacticalEvent($"Dispatch W{_wave:00}: Ready {readiness.score} {readiness.grade} | {BuildWaveRouteLabel(_currentWaveDefinition)}", 5.6f);
            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.WaveTransition,
                "[W]",
                $"WAVE {_wave:00}  {_currentWavePhase}",
                $"READY {readiness.grade}  /  {BuildWaveRouteLabel(_currentWaveDefinition)}",
                TDBattleFeedbackTier.Tactical,
                1.05f);
            PlaySfxTone("wave_transition", 590f + Mathf.Min(180f, _wave * 12f), 0.13f, 0.58f, true);
            PlaySfxTone("wave_start", 640f, 0.11f, 0.58f, true);
            AdvanceTutorial(TDFirstRunTutorialStep.StartWave);
        }

        private void SpawnEnemy(
            TDEnemyCatalogEntry entry,
            IReadOnlyList<Vector3> path,
            int waveNumber,
            int enemyIndex,
            string laneKey = "default",
            bool registerEncounter = true)
        {
            if (registerEncounter)
            {
                RegisterEnemyEncounter(entry);
            }

            var runtimeEntry = BuildMissionEnemyEntry(entry);

            // Pool first: a released hierarchy of this kind is fully reset by
            // TDEnemy.Initialize below — but its per-kind visuals (shadow
            // offset/scale, visual offset, sprite, material, collider size)
            // were built for the same enemyId, so reuse is exact.
            var enemyObject = TDEnemyPool.Instance != null
                ? TDEnemyPool.Instance.Get(runtimeEntry.enemyId, transform)
                : null;

            if (enemyObject != null)
            {
                var pooledEnemy = enemyObject.GetComponent<TDEnemy>();
                if (pooledEnemy != null)
                {
                    pooledEnemy.Initialize(this, path ?? GetDefaultSpawnPath(), runtimeEntry, laneKey);
                    _activeEnemies.Add(pooledEnemy);
                    RegisterEnemySpawnForAnalytics(pooledEnemy);
                    return;
                }

                // Malformed pooled instance — discard and fall through to build.
                Destroy(enemyObject);
                enemyObject = null;
            }

            enemyObject = new GameObject($"Enemy_{runtimeEntry.enemyId}_{waveNumber}_{enemyIndex}");
            enemyObject.transform.SetParent(transform, true);
            TDEnemyPool.Instance?.Register(enemyObject, runtimeEntry.enemyId);

            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(enemyObject.transform, false);

            var shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
            shadowRenderer.sortingOrder = Mathf.Max(0, GetEnemySortingOrder(runtimeEntry.enemyId) - 3);
            shadowRenderer.sprite = TDArtLibrary.GetSoftShadowSprite();
            shadowRenderer.color = new Color(0.05f, 0.04f, 0.06f, GetEnemyShadowAlpha(runtimeEntry.enemyId));

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(enemyObject.transform, false);
            visualObject.transform.localPosition = GetEnemyVisualOffset(runtimeEntry.enemyId);

            var renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = GetEnemySortingOrder(runtimeEntry.enemyId);
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback(GetEnemySpritePath(runtimeEntry.enemyId), GetEnemyFallbackColor(runtimeEntry.enemyId));
            var enemyMaterial = GetEnemyVisualMaterial(runtimeEntry.enemyId);
            if (enemyMaterial != null)
            {
                renderer.sharedMaterial = enemyMaterial;
            }
            visualObject.transform.localScale = ResolveSpriteScale(renderer.sprite, GetEnemyCellCoverage(runtimeEntry.enemyId));
            AlignEnemyVisualToRouteAnchor(renderer, visualObject.transform, runtimeEntry.enemyId);
            shadowObject.transform.localPosition = ResolveEnemyFootShadowOffset(
                renderer,
                visualObject.transform,
                runtimeEntry.enemyId);
            var shadowScale = ResolveSpriteScale(shadowRenderer.sprite, GetEnemyShadowCoverage(runtimeEntry.enemyId));
            shadowObject.transform.localScale = new Vector3(shadowScale.x, shadowScale.y * 0.42f, shadowScale.z);

            var animator = visualObject.AddComponent<TDSpriteAnimator>();
            animator.Configure(GetEnemyAnimationPrefix(runtimeEntry.enemyId), GetEnemyAnimationFrames(runtimeEntry.enemyId), GetEnemyAnimationFps(runtimeEntry.enemyId), true, true);

            var collider = enemyObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = GetEnemyColliderSize(runtimeEntry.enemyId);
            collider.offset = GetEnemyColliderOffset(runtimeEntry.enemyId);

            var enemy = enemyObject.AddComponent<TDEnemy>();
            enemy.Initialize(this, path ?? GetDefaultSpawnPath(), runtimeEntry, laneKey);
            animator.OnFrameSwapped -= enemy.NotifyVisualFrameSwapped;  // idempotent guard for re-registration
            animator.OnFrameSwapped += enemy.NotifyVisualFrameSwapped;
            enemy.NotifyVisualFrameSwapped();
            _activeEnemies.Add(enemy);
            RegisterEnemySpawnForAnalytics(enemy);

            var isBossThreat = runtimeEntry.tags != null && runtimeEntry.tags.Any(tag =>
                string.Equals(tag, "boss", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "final", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "elite", StringComparison.OrdinalIgnoreCase));
            if (isBossThreat && _bossWarningWave != _wave)
            {
                _bossWarningWave = _wave;
                _battlePresentation?.ShowCinematic(
                    TDBattleCinematicKind.BossPhase,
                    "[B!]",
                    "BOSS THREAT ENTERING",
                    $"{runtimeEntry.displayName}  /  {FormatLaneLabel(laneKey)}",
                    TDBattleFeedbackTier.Critical,
                    1.45f);
                PlayCriticalSfxTone("boss_warning", 175f, 0.36f, 0.96f, true);
            }
        }

        private IEnumerator SpawnSplitChildren(string enemyId, int count, float interval, string laneKey)
        {
            if (_gameOver || count <= 0 || string.IsNullOrWhiteSpace(enemyId))
            {
                yield break;
            }

            if (!_enemyCatalog.TryGetValue(enemyId, out var entry))
            {
                yield break;
            }

            var safeInterval = Mathf.Max(0.05f, interval);
            var resolvedLane = ResolveExistingLaneKey(laneKey);
            for (var i = 0; i < count && !_gameOver; i++)
            {
                _runtimeSpawnIndex++;
                SpawnEnemy(entry, GetSpawnPathForLane(resolvedLane), _wave, 10000 + _runtimeSpawnIndex, resolvedLane);
                if (i < count - 1)
                {
                    yield return new WaitForSeconds(safeInterval);
                }
            }
        }

        private IEnumerator WaveLoopFromConfig()
        {
            // Wait for deployment confirmation with a timeout safeguard.
            // The title screen sets _campaignDeploymentConfirmed=false until the player
            // picks New Game / Continue. Automation (P124) may set it via reflection
            // after a delay. A 5s timeout prevents a permanent deadlock if the flag
            // is never set (e.g. coroutine started before title screen exists).
            var waitStart = Time.realtimeSinceStartup;
            while (!_campaignDeploymentConfirmed && !_gameOver)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
                // Automation escape hatch only: release players wait for the
                // title flow exactly like FallbackWaveLoop does — a timer
                // backdoor here force-deployed every session after 5s and
                // logged a warning into every player's log (review P2).
                if (Time.realtimeSinceStartup - waitStart > 5f)
                {
                    Debug.LogWarning("[TD] WaveLoop waited >5s for deployment confirmation — forcing resume (automation/title path).");
                    _campaignDeploymentConfirmed = true;
                }
#endif

                yield return null;
            }

            // Ensure wave data is ready before entering the loop.
            if (_waveSet == null || _waveSet.waves == null || _waveSet.waves.Length == 0)
            {
                Debug.LogWarning("[TD] WaveLoopFromConfig: _waveSet null/empty — falling back.");
                yield return FallbackWaveLoop();
                yield break;
            }

            // Use real-time delay so timeScale (e.g. 0 during pause) doesn't stall the loop.
            yield return new WaitForSecondsRealtime(1f);

            var waves = _waveSet.waves;
            Array.Sort(waves, (a, b) => a.waveIndex.CompareTo(b.waveIndex));

            for (var w = 0; w < waves.Length && !_gameOver; w++)
            {
                var wave = waves[w];
                _wave = wave.waveIndex;
                _currentWaveDefinition = wave;
                ApplyConfiguredWaveRuntimeContext(wave);
                BeginWaveStat(_wave);
                _currentWaveHint = string.IsNullOrWhiteSpace(wave.hint) ? "(no hint)" : wave.hint;

                _prepCountdown = wave.prepSeconds > 0f ? wave.prepSeconds : _waveSet.globalDefaults.prepSeconds;
                yield return WaitForPrepStart(_prepCountdown);
                if (_waveDispatchedEarly)
                {
                    SetStatus($"Wave {_wave} starting now.");
                }

                var groups = wave.groups ?? Array.Empty<TDWaveGroup>();
                PresentDangerousLaneWarning(wave);
                var remainingGroups = groups.Length;
                for (var g = 0; g < groups.Length; g++)
                {
                    StartCoroutine(SpawnGroup(wave, groups[g], () => remainingGroups--));
                }

                while (remainingGroups > 0 && !_gameOver)
                {
                    yield return null;
                }

                while (_activeEnemies.Count > 0 && !_gameOver)
                {
                    _activeEnemies.RemoveAll(enemy => enemy == null);
                    yield return null;
                }

                if (_gameOver)
                {
                    break;
                }

                var baseReward = wave.rewardGold > 0 ? wave.rewardGold : _waveSet.globalDefaults.baseRewardGold;
                var reward = TDEconomyTuning.ScaleWaveClearReward(
                    ScaleMissionReward(baseReward),
                    _wave,
                    GetConfiguredWaveCount());
                // Meta line C (Wave Subsidy): a percent on top of the decayed
                // tail value — additive to the p12.5.0 curve, never replacing
                // it. Ruling B2: floor WITH remainder carry-over, so the
                // cumulative payout equals floor(Σ income × pct) exactly
                // instead of zeroing out on small tail values. Ruling B3: the
                // ledger base is WAVE-CLEAR INCOME ONLY — combat bounty and
                // reinforcement income never enter it (scenario ROI must not
                // drift with meta ranks).
                var subsidyPercent = TDMetaUpgradeSystem.GetWaveClearIncomeBonusPercent(
                    GetMetaRank(TDMetaUpgradeSystem.UpgradeLine.C));
                var subsidyPayment = TDMetaUpgradeSystem.ResolveSubsidyPayment(
                    ref _subsidyEntitledHundredths,
                    ref _subsidyPaidTotal,
                    reward,
                    subsidyPercent);
                reward += subsidyPayment;

                _defenseBudget += reward;
                TrackP125ClearIncome(reward);
                SetStatus($"Wave {_wave} cleared, reward +{reward} budget");
                PushTacticalEvent($"Clear W{_wave:00}: kills {_currentWaveStat?.kills ?? 0}, leaks {_currentWaveStat?.escapes ?? 0}, +{reward} budget", 6.0f);
                _battlePresentation?.ShowCinematic(
                    TDBattleCinematicKind.WaveTransition,
                    "[W+]",
                    $"WAVE {_wave:00} SECURED",
                    $"+{reward} BUDGET  /  PREPARE NEXT LINE",
                    TDBattleFeedbackTier.Routine,
                    0.78f);
                PlaySfxTone("wave_clear", 500f, 0.16f, 0.66f, true);
                FinalizeCurrentWaveStat(true);
                yield return new WaitForSeconds(0.6f);
            }

            if (_gameOver)
            {
                yield break;
            }

            _victory = true;
            _gameOver = true;
            ResetResonanceState();
            PlaySfxTone("run_victory", 760f, 0.34f, 0.95f, true);
            RecordCampaignResultIfNeeded();
            LogRunSummary();
        }

        private void PresentDangerousLaneWarning(TDWaveDefinition wave)
        {
            if (wave?.groups == null || wave.groups.Length == 0)
            {
                return;
            }

            var lanePressure = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var total = 0;
            for (var i = 0; i < wave.groups.Length; i++)
            {
                var group = wave.groups[i];
                if (group == null || group.count <= 0)
                {
                    continue;
                }

                var lane = string.IsNullOrWhiteSpace(group.lane) ? "center" : group.lane.Trim();
                lanePressure[lane] = lanePressure.TryGetValue(lane, out var current)
                    ? current + group.count
                    : group.count;
                total += group.count;
            }

            if (lanePressure.Count == 0 || total < 4)
            {
                return;
            }

            var dominant = lanePressure
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .First();
            var share = Mathf.RoundToInt((dominant.Value / (float)Mathf.Max(1, total)) * 100f);
            var hasDangerTag = wave.threatTags != null && wave.threatTags.Any(tag =>
                string.Equals(tag, "pressure", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "fast", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "flank", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "boss", StringComparison.OrdinalIgnoreCase));
            if (!hasDangerTag && lanePressure.Count > 1 && share < 45)
            {
                return;
            }

            _battlePresentation?.ShowCinematic(
                TDBattleCinematicKind.DangerousLane,
                "[R!]",
                $"DANGER LANE  {FormatLaneLabel(dominant.Key)}",
                $"{dominant.Value}/{total} CONTACTS  /  {share}% PRESSURE",
                hasDangerTag ? TDBattleFeedbackTier.Critical : TDBattleFeedbackTier.Tactical,
                1.12f);
            PlaySfxTone("danger_lane", 370f, 0.18f, 0.68f, true);
        }

        private IEnumerator WaitForPrepStart(float prepSeconds)
        {
            _prepDuration = Mathf.Max(0f, prepSeconds + _missionPrepSecondsBonus);
            _prepCountdown = _prepDuration;
            _waveStartRequested = false;
            _isInPrepPhase = true;

            if (IsOpeningWaveBuildRequired() && !_openingGuideShown)
            {
                _openingGuideShown = true;
                SetStatus("Build one tower, check range, then start the wave.");
            }

            while (!_gameOver)
            {
                if (_missionBoardOpen)
                {
                    yield return null;
                    continue;
                }

                if (_waveStartRequested && !IsOpeningWaveBuildRequired())
                {
                    break;
                }

                if (!IsOpeningWaveBuildRequired())
                {
                    if (_prepCountdown <= 0f)
                    {
                        break;
                    }

                    _prepCountdown = Mathf.Max(0f, _prepCountdown - Time.deltaTime);
                }

                yield return null;
            }

            CaptureWaveStartReadiness();
            _prepCountdown = 0f;
            _isInPrepPhase = false;
            _waveStartRequested = false;
        }

        private IEnumerator SpawnGroup(TDWaveDefinition wave, TDWaveGroup group, Action onCompleted)
        {
            if (group == null)
            {
                onCompleted?.Invoke();
                yield break;
            }

            var formation = NormalizeGroupToken(group.formation);
            var delayedStart = Mathf.Max(0f, group.startDelay + GetFormationStartDelayOffset(formation) + _scenarioWaveDelayBonus);
            if (delayedStart > 0f)
            {
                yield return new WaitForSeconds(delayedStart);
            }

            var interval = Mathf.Max(group.spawnInterval, _waveSet.globalDefaults.spawnMinSpacing);
            var routeLabel = BuildGroupRouteEventLabel(group, formation);
            if (!string.IsNullOrWhiteSpace(routeLabel))
            {
                PushTacticalEvent(routeLabel, 4.8f);
            }

            for (var i = 0; i < group.count && !_gameOver; i++)
            {
                if (_enemyCatalog.TryGetValue(group.enemyId, out var entry))
                {
                    var laneKey = ResolveSpawnLaneKey(group, formation, i);
                    var path = GetSpawnPathForLane(laneKey);
                    SpawnEnemy(entry, path, wave.waveIndex, i + 1, laneKey);
                }
                else if (i == 0)
                {
                    Debug.LogWarning($"[TD] Missing enemy config for group enemyId={group.enemyId} in wave={wave.waveIndex}");
                }

                if (i < group.count - 1)
                {
                    yield return new WaitForSeconds(ResolveSpawnCadence(interval, formation, i, group.count));
                }
            }

            onCompleted?.Invoke();
        }

        private IEnumerator FallbackWaveLoop()
        {
            _currentWaveDefinition = null;
            _currentWaveHint = "Wave config missing: fallback mode enabled.";
            while (!_campaignDeploymentConfirmed && !_gameOver)
            {
                yield return null;
            }

            yield return new WaitForSeconds(1f);

            while (!_gameOver)
            {
                _wave++;
                var enemyCount = 5 + (_wave * 2);
                ApplyFallbackWaveRuntimeContext(enemyCount);
                BeginWaveStat(_wave);
                yield return WaitForPrepStart(8f);
                var spawnDelay = Mathf.Max(0.35f, 1f - (_wave * 0.04f));

                for (var i = 0; i < enemyCount && !_gameOver; i++)
                {
                    if (!_enemyCatalog.TryGetValue("skitter_runner", out var entry))
                    {
                        entry = new TDEnemyCatalogEntry
                        {
                            enemyId = "skitter_runner",
                            displayName = "Skitter Runner",
                            hp = 30 + (_wave * 2),
                            speed = 1.6f,
                            armorFlat = 0,
                            rewardGold = 8 + (_wave / 2),
                            lineDamage = 1,
                            tags = new[] { "fast", "light" }
                        };
                    }

                    SpawnEnemy(entry, GetDefaultSpawnPath(), _wave, i + 1, "default");
                    yield return new WaitForSeconds(spawnDelay);
                }

                while (_activeEnemies.Count > 0 && !_gameOver)
                {
                    _activeEnemies.RemoveAll(enemy => enemy == null);
                    yield return null;
                }

                if (_gameOver)
                {
                    yield break;
                }

                _defenseBudget += ScaleMissionReward(20 + _wave);
                FinalizeCurrentWaveStat(true);
                yield return new WaitForSeconds(1.2f);
            }
        }

        private void ApplyConfiguredWaveRuntimeContext(TDWaveDefinition wave)
        {
            _waveDispatchedEarly = false;
            _currentWavePhase = NormalizeWaveLabel(wave?.phase, "unknown");
            _currentWaveGoalTag = NormalizeWaveLabel(wave?.goalTag, "none");
            CaptureCurrentWaveThreatTags(wave);
            _currentWaveThreatTags = wave?.threatTags == null || wave.threatTags.Length == 0
                ? "none"
                : string.Join("/", wave.threatTags);

            _currentWaveBudgetExpected = Mathf.Max(0f, wave?.budgetTarget ?? 0f);
            _currentWaveBudgetActual = CalculateWaveBudgetActual(wave);
            _currentWaveBudgetInRange = IsWaveBudgetInRange(_currentWaveBudgetExpected, wave?.budgetTolerance ?? 1f, _currentWaveBudgetActual);
            PrepareScenarioForWave();
            if (!_currentWaveBudgetInRange && wave != null)
            {
                Debug.LogWarning(
                    $"[TD][WaveGrammar] budget out-of-range wave={wave.waveIndex} goal={_currentWaveGoalTag} " +
                    $"target={_currentWaveBudgetExpected:0.##} actual={_currentWaveBudgetActual:0.##} tolerance={wave.budgetTolerance:0.##}");
            }
        }

    }
}
