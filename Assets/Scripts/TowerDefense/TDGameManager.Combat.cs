// Freeze-period S5: combat services + per-kind enemy config tables moved verbatim from TDGameManager.cs — registry queries (priority/bounded-range shared buffers), support-aura cache, damage modification, and the enemy visual/collider/animation lookup tables.
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
        public TDEnemy GetPriorityEnemy(Vector3 origin, float maxRange, TDTowerKind towerKind)
        {
            TDEnemy best = null;
            var rangeSqr = maxRange * maxRange;
            var bestScore = float.MinValue;
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                var sqrDistance = (enemy.transform.position - origin).sqrMagnitude;
                if (sqrDistance > rangeSqr)
                {
                    continue;
                }

                var score = enemy.RouteProgress01 * 100f;
                score += ResolveTowerTargetCounterBonus(towerKind, enemy);
                score += (1f - enemy.HealthRatio) * 5f;
                score -= sqrDistance / Mathf.Max(0.01f, rangeSqr) * 2f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = enemy;
            }

            return best;
        }

        private static float ResolveTowerTargetCounterBonus(TDTowerKind towerKind, TDEnemy enemy)
        {
            if (enemy == null)
            {
                return 0f;
            }

            return towerKind switch
            {
                TDTowerKind.FrostCoil or TDTowerKind.GravSnare =>
                    enemy.HasTag("fast") || enemy.HasTag("flank") ? (enemy.IsSlowed ? 8f : 24f) : 0f,
                TDTowerKind.EmberFlak =>
                    enemy.HasTag("fast") || enemy.HasTag("flank") ? 22f :
                    enemy.HasTag("swarm") ? 12f : 0f,
                TDTowerKind.RailLancer or TDTowerKind.SiegeDrill =>
                    enemy.HasTag("armored") || enemy.HasTag("heavy") || enemy.HasTag("boss") ? 20f :
                    enemy.HasTag("special") ? 12f : 0f,
                TDTowerKind.CinderMortar or TDTowerKind.ArcWelder =>
                    enemy.HasTag("swarm") || enemy.HasTag("spawn") || enemy.HasTag("split") ? 12f : 0f,
                _ => 0f
            };
        }

        // ── Expansion batch-2 enemy services ──
        private struct TDCinderPile
        {
            public Vector3 Position;
            public float ExpiresAt;
        }

        private readonly List<TDCinderPile> _cinderPiles = new();
        private string _lastDiedEnemyId;
        public const float CinderPileRadius = 0.7f;
        public const float CinderPileDurationSeconds = 8f;
        public const float CinderPileSpeedBonus = 1.25f;
        public const float AcidBurstRadius = 1.6f;
        public const float AcidBurstDurationSeconds = 4f;

        public bool IsOnCinderPile(Vector3 position)
        {
            var now = Time.time;
            for (var i = _cinderPiles.Count - 1; i >= 0; i--)
            {
                if (_cinderPiles[i].ExpiresAt <= now)
                {
                    _cinderPiles.RemoveAt(i);
                    continue;
                }

                if ((position - _cinderPiles[i].Position).sqrMagnitude <= CinderPileRadius * CinderPileRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private void RegisterCinderPile(Vector3 position)
        {
            _cinderPiles.Add(new TDCinderPile { Position = position, ExpiresAt = Time.time + CinderPileDurationSeconds });
            // Ground prop, not a target: the husk's final death frame lies
            // where it fell and burns out with the pile.
            var pileObject = new GameObject("CinderPile");
            pileObject.transform.SetParent(transform, true);
            pileObject.transform.position = position;
            var renderer = pileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = TDArtLibrary.LoadSpriteOrFallback(
                "Art/anim/enemy_cinder_husk_death_03", new Color(0.95f, 0.55f, 0.20f));
            renderer.sortingOrder = 7;
            renderer.color = new Color(1f, 0.72f, 0.42f, 0.85f);
            Destroy(pileObject, CinderPileDurationSeconds);
        }

        /// <summary>
        /// Acid Blister's death spray: the first enemy that punishes tower
        /// placement — towers in the burst lose attack speed for a window.
        /// Echo Harbinger's mimic reuses the same debuff at -12%.
        /// </summary>
        public void ApplyTowerAcidBurst(Vector3 position, float radius, float duration, float factor)
        {
            var towers = FindObjectsByType<TDTower>(FindObjectsSortMode.None);
            for (var i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                if (tower == null)
                {
                    continue;
                }

                if ((tower.transform.position - position).sqrMagnitude <= radius * radius)
                {
                    tower.ApplyAcidDebuff(duration, factor);
                }
            }
        }

        private void RegisterExpansionDeathEffects(TDEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (string.Equals(enemy.EnemyId, "cinder_husk", StringComparison.Ordinal))
            {
                RegisterCinderPile(enemy.transform.position);
            }
            else if (string.Equals(enemy.EnemyId, "acid_blister", StringComparison.Ordinal))
            {
                ApplyTowerAcidBurst(enemy.transform.position, AcidBurstRadius, AcidBurstDurationSeconds, 0.90f);
            }
            else if (string.Equals(enemy.EnemyId, "echo_brood", StringComparison.Ordinal) &&
                     !string.IsNullOrEmpty(_lastDiedEnemyId))
            {
                SpawnEchoCopy(_lastDiedEnemyId, enemy);
            }

            if (!enemy.HasAnyTag("boss", "final"))
            {
                _lastDiedEnemyId = enemy.EnemyId;
            }
        }

        /// <summary>
        /// Echo Brood's copy: the most recent non-boss death returns at half
        // health from where the brood fell — a chaos engine for late waves.
        /// </summary>
        private void SpawnEchoCopy(string enemyId, TDEnemy brood)
        {
            if (_gameOver || brood == null || !_enemyCatalog.TryGetValue(enemyId, out var entry))
            {
                return;
            }

            var copy = SpawnEnemy(entry, GetSpawnPathForLane(brood.LaneKey), _wave, 20000 + _runtimeSpawnIndex, brood.LaneKey);
            if (copy == null)
            {
                return;
            }

            copy.WarpToProgress(brood.GetRouteProgress01());
            copy.SetCurrentHealth(Mathf.Max(1, Mathf.RoundToInt(copy.MaxHealth * 0.5f)));
            PushTacticalEvent($"Echo Brood echoed: {entry.displayName} returns at half strength", 4.6f);
        }

        // ── Salvage Derrick economy services (expansion tower 10) ──
        // Registry maintained at build/sell; pruned of destroyed entries on
        // query (Unity fake-null), so level resets need no extra sweep.
        private readonly List<TDTower> _salvageDerricks = new();
        private int _derrickWaveCredited;

        private void RegisterSalvageDerrick(TDTower tower)
        {
            if (tower != null && tower.Kind == TDTowerKind.SalvageDerrick && !_salvageDerricks.Contains(tower))
            {
                _salvageDerricks.Add(tower);
            }
        }

        private void UnregisterSalvageDerrick(TDTower tower)
        {
            _salvageDerricks.Remove(tower);
        }

        /// <summary>
        /// Credits a derrick-sourced income increment against the per-wave
        /// fuse (TDEconomyTuning.DerrickWaveIncomeCeiling) and pays whatever
        /// survived the clamp straight into the budget.
        /// </summary>
        private void CreditDerrickWaveIncome(int amount)
        {
            var credited = TDEconomyTuning.ClampDerrickWaveCredit(_derrickWaveCredited, amount);
            if (credited <= 0)
            {
                return;
            }

            _derrickWaveCredited += credited;
            _defenseBudget += credited;
        }

        /// <summary>
        /// Kill bounties inside a crane's ring pay a premium (strongest ring
        /// wins — no stacking) and refund budget per the supply line. Called
        /// from the kill funnel before combat-bounty decay.
        /// </summary>
        private int ApplySalvageBountyAura(TDEnemy enemy, int reward)
        {
            if (enemy == null || reward <= 0 || _salvageDerricks.Count == 0)
            {
                return reward;
            }

            _salvageDerricks.RemoveAll(tower => tower == null);
            TDTower bestRing = null;
            var bestBonus = 0f;
            var rebate = 0;
            var position = enemy.transform.position;
            for (var i = 0; i < _salvageDerricks.Count; i++)
            {
                var derrick = _salvageDerricks[i];
                var radius = derrick.KillBountyAuraRadius;
                if (radius <= 0f)
                {
                    continue;
                }

                var delta = derrick.transform.position - position;
                if (delta.sqrMagnitude > radius * radius)
                {
                    continue;
                }

                if (derrick.BountyBonusPercent > bestBonus)
                {
                    bestBonus = derrick.BountyBonusPercent;
                    bestRing = derrick;
                }

                rebate = Mathf.Max(rebate, derrick.KillBudgetRebate);
            }

            if (bestRing == null && rebate <= 0)
            {
                return reward;
            }

            var multiplier = TDEconomyTuning.ResolveAuraBountyMultiplier(
                bestBonus,
                bestRing != null && bestRing.IsDamageSpecialist,
                enemy.HasAnyTag("boss", "elite"));
            var adjusted = Mathf.RoundToInt(reward * multiplier);
            if (adjusted > reward)
            {
                CreditDerrickWaveIncome(adjusted - reward);
            }

            if (rebate > 0)
            {
                CreditDerrickWaveIncome(rebate);
            }

            return adjusted;
        }

        private void TrySpreadBurnOnKill(TDEnemy enemy, TDTower sourceTower)
        {
            // Wildfire line (utility levels) spreads fire from burning kills.
            // Base: 1 layer to 2 targets at the tower's spread radius; the
            // Wildfire Drift specialization widens to 2 cells / 3 targets / 2
            // layers. Targets come from the P1 shared buffer — consume before
            // any other range query runs.
            if (enemy == null || enemy.BurnLayers <= 0 || sourceTower == null ||
                sourceTower.Kind != TDTowerKind.SlagBurner || sourceTower.UtilityBranchCount <= 0)
            {
                return;
            }

            var wildfire = sourceTower.IsUtilitySpecialist;
            var radius = wildfire ? Mathf.Max(2f, sourceTower.BurnSpreadRadius) : sourceTower.BurnSpreadRadius;
            var maxTargets = wildfire ? 3 : 2;
            var layers = wildfire ? 2 : 1;
            var targets = GetEnemiesInRange(enemy.transform.position, radius, maxTargets);
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target != null && target.IsTargetable)
                {
                    target.ApplyBurn(layers, sourceTower.BurnDamagePerLayer, sourceTower.BurnDuration, sourceTower);
                }
            }
        }

        public List<TDEnemy> GetEnemiesInRange(Vector3 origin, float radius, int maxTargets)
        {
            // Shared buffers: this query sits on the damage hot path (AoE,
            // chains, pulses fire it per hit) and used to allocate a List plus
            // a sorting closure per call. All callers consume the returned
            // list synchronously — nothing stores it across frames.
            var buffer = _enemiesInRangeBuffer;
            var distances = _enemiesInRangeDistances;
            buffer.Clear();
            distances.Clear();

            var radiusSqr = radius * radius;
            var cap = Mathf.Max(1, maxTargets);

            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                var sqrDistance = (enemy.transform.position - origin).sqrMagnitude;
                if (sqrDistance > radiusSqr)
                {
                    continue;
                }

                // Bounded insertion keeps the nearest `cap` candidates in
                // ascending distance order — same result as the old full
                // List.Sort + RemoveRange, with zero allocations.
                if (buffer.Count == cap && sqrDistance >= distances[cap - 1])
                {
                    continue;
                }

                var insert = buffer.Count;
                while (insert > 0 && distances[insert - 1] > sqrDistance)
                {
                    insert--;
                }

                buffer.Insert(insert, enemy);
                distances.Insert(insert, sqrDistance);
                if (buffer.Count > cap)
                {
                    buffer.RemoveAt(cap);
                    distances.RemoveAt(cap);
                }
            }

            return buffer;
        }

        private bool HasSupportAuraNearby(TDEnemy target, float radius)
        {
            if (target == null || radius <= 0f)
            {
                return false;
            }

            // This runs per damage event on armored/boss targets; scanning all
            // active enemies there is O(hits × enemies). Instead keep a cached
            // list of support-tagged enemies (refreshed every 0.2s — one full
            // scan per interval) and distance-check only those.
            RefreshSupportEnemiesCache();

            var radiusSqr = radius * radius;
            var origin = target.transform.position;
            for (var i = 0; i < _supportEnemiesCache.Count; i++)
            {
                var other = _supportEnemiesCache[i];
                if (other == null || ReferenceEquals(other, target))
                {
                    continue;
                }

                if ((other.transform.position - origin).sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshSupportEnemiesCache()
        {
            if (Time.time - _supportEnemiesCacheRefreshTime < 0.2f)
            {
                return;
            }

            _supportEnemiesCacheRefreshTime = Time.time;
            _supportEnemiesCache.Clear();
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy != null && enemy.HasTag("support"))
                {
                    _supportEnemiesCache.Add(enemy);
                }
            }
        }

        public int GetModifiedDamageForEnemy(TDTower sourceTower, TDEnemy enemy, int rawDamage)
        {
            if (enemy == null || rawDamage <= 0)
            {
                return Mathf.Max(1, rawDamage);
            }

            var sourceTowerKind = sourceTower != null ? sourceTower.Kind : TDTowerKind.RailLancer;
            var multiplier = 1f;
            if (enemy.HasTag("armored") && HasSupportAuraNearby(enemy, SupportAuraRadius))
            {
                multiplier *= 0.84f;
            }

            if (enemy.HasTag("boss") && HasSupportAuraNearby(enemy, SupportAuraRadius + 0.4f))
            {
                multiplier *= 0.90f;
            }

            if (enemy.IsMarked && sourceTowerKind != TDTowerKind.ResonanceBeacon)
            {
                multiplier *= 1.10f;
                if (sourceTowerKind == TDTowerKind.ArcWelder)
                {
                    multiplier *= 1.06f;
                }
            }

            if (sourceTowerKind == TDTowerKind.GravSnare && (enemy.HasTag("fast") || enemy.HasTag("flank")))
            {
                multiplier *= 1.12f;
            }

            if (IsResonanceWindowActive)
            {
                multiplier *= 1.08f;
                if (_activeResonanceCommand == TDResonanceCommand.FractureMark)
                {
                    if (enemy.HasTag("armored"))
                    {
                        multiplier *= 1.32f;
                    }
                    else if (enemy.HasTag("heavy"))
                    {
                        multiplier *= 1.28f;
                    }
                    else if (enemy.HasTag("fast"))
                    {
                        multiplier *= 1.24f;
                    }
                    else
                    {
                        multiplier *= 1.16f;
                    }

                    if (sourceTowerKind == TDTowerKind.FrostCoil)
                    {
                        multiplier *= 1.08f;
                    }

                    if (sourceTowerKind == TDTowerKind.SiegeDrill && enemy.HasTag("armored"))
                    {
                        multiplier *= 1.08f;
                    }

                    if (sourceTowerKind == TDTowerKind.ResonanceBeacon)
                    {
                        multiplier *= 1.07f;
                    }

                    multiplier *= GetDoctrineCommandPowerMultiplier(TDResonanceCommand.FractureMark);
                }
            }

            multiplier *= GetSpecializationSynergyMultiplier(sourceTower, enemy);
            if (_matrixConvergenceTriggeredThisWindow && _activeResonanceCommand == TDResonanceCommand.EmberSurge)
            {
                multiplier *= MatrixConvergenceEmberDamageMultiplier;
            }

            var adjusted = Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier));
            if (adjusted > rawDamage)
            {
                _resonanceBonusDamage += adjusted - rawDamage;
            }

            return adjusted;
        }

        private TDEnemyCatalogEntry BuildMissionEnemyEntry(TDEnemyCatalogEntry entry)
        {
            var runtimeEntry = CloneEnemyEntry(entry);
            runtimeEntry.hp = Mathf.Max(1, Mathf.RoundToInt(runtimeEntry.hp * _missionEnemyHpMultiplier));
            runtimeEntry.speed = Mathf.Max(0.05f, runtimeEntry.speed * _missionEnemySpeedMultiplier);
            runtimeEntry.armorFlat = Mathf.Max(0, runtimeEntry.armorFlat + _missionEnemyArmorBonus);
            runtimeEntry.rewardGold = ScaleMissionReward(runtimeEntry.rewardGold);
            return runtimeEntry;
        }

        private static string GetEnemySpritePath(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => "Art/anim/enemy_skitter_runner_00",
                "carapace_brute" => "Art/anim/enemy_carapace_brute_00",
                "ash_swarm" => "Art/anim/enemy_ash_swarm_00",
                "plated_spore" => "Art/anim/enemy_plated_spore_00",
                "burrow_sapper" => "Art/anim/enemy_burrow_sapper_00",
                "ember_leech" => "Art/anim/enemy_ember_leech_00",
                "spore_carrier" => "Art/anim/enemy_spore_carrier_00",
                "rail_warden" => "Art/anim/enemy_rail_warden_00",
                "cinder_glider" => "Art/anim/enemy_cinder_glider_00",
                "husk_titan" => "Art/anim/enemy_husk_titan_00",
                "echo_mimic" => "Art/anim/enemy_echo_mimic_00",
                "furnace_matriarch" => "Art/anim/enemy_furnace_matriarch_00",
                "cinder_husk" => "Art/anim/enemy_cinder_husk_00",
                "rail_splitter" => "Art/anim/enemy_rail_splitter_00",
                "acid_blister" => "Art/anim/enemy_acid_blister_00",
                "forge_dragoon" => "Art/anim/enemy_forge_dragoon_00",
                "ember_strider" => "Art/anim/enemy_ember_strider_00",
                "echo_brood" => "Art/anim/enemy_echo_brood_00",
                // C-3 boss reels landed (468b040): 10-frame idle each.
                "containermaw" => "Art/anim/boss_containermaw_00",
                "junction_tyrant" => "Art/anim/boss_junction_tyrant_00",
                "kiln_custodian" => "Art/anim/boss_kiln_custodian_00",
                "echo_harbinger" => "Art/anim/boss_echo_harbinger_00",
                _ => "Art/enemy_slime"
            };
        }

        private static Material GetEnemyVisualMaterial(string enemyId)
        {
            var repairMode = enemyId?.ToLowerInvariant() switch
            {
                "ember_leech" => 0f,
                "furnace_matriarch" => 1f,
                "cinder_glider" => 2f,
                _ => -1f
            };
            if (repairMode < 0f)
            {
                return null;
            }

            if (EnemyBodyRepairMaterials.TryGetValue(enemyId, out var cachedMaterial) && cachedMaterial != null)
            {
                return cachedMaterial;
            }

            var shader = Shader.Find("TD/EnemyBodyRepair");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetFloat("_RepairMode", repairMode);
            EnemyBodyRepairMaterials[enemyId] = material;
            return material;
        }

        private static string GetEnemyAnimationPrefix(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => "Art/anim/enemy_skitter_runner",
                "carapace_brute" => "Art/anim/enemy_carapace_brute",
                "ash_swarm" => "Art/anim/enemy_ash_swarm",
                "plated_spore" => "Art/anim/enemy_plated_spore",
                "burrow_sapper" => "Art/anim/enemy_burrow_sapper",
                "ember_leech" => "Art/anim/enemy_ember_leech",
                "spore_carrier" => "Art/anim/enemy_spore_carrier",
                "rail_warden" => "Art/anim/enemy_rail_warden",
                "cinder_glider" => "Art/anim/enemy_cinder_glider",
                "husk_titan" => "Art/anim/enemy_husk_titan",
                "echo_mimic" => "Art/anim/enemy_echo_mimic",
                "furnace_matriarch" => "Art/anim/enemy_furnace_matriarch",
                "cinder_husk" => "Art/anim/enemy_cinder_husk",
                "rail_splitter" => "Art/anim/enemy_rail_splitter",
                "acid_blister" => "Art/anim/enemy_acid_blister",
                "forge_dragoon" => "Art/anim/enemy_forge_dragoon",
                "ember_strider" => "Art/anim/enemy_ember_strider",
                "echo_brood" => "Art/anim/enemy_echo_brood",
                "containermaw" => "Art/anim/boss_containermaw",
                "junction_tyrant" => "Art/anim/boss_junction_tyrant",
                "kiln_custodian" => "Art/anim/boss_kiln_custodian",
                "echo_harbinger" => "Art/anim/boss_echo_harbinger",
                _ => string.Empty
            };
        }

        private static int GetEnemyAnimationFrames(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 8,
                "carapace_brute" => 6,
                "ash_swarm" => 8,
                "plated_spore" => 6,
                "burrow_sapper" => 8,
                "ember_leech" => 6,
                "spore_carrier" => 6,
                "rail_warden" => 6,
                "cinder_glider" => 8,
                "husk_titan" => 6,
                "echo_mimic" => 8,
                "furnace_matriarch" => 6,
                "cinder_husk" => 8,
                "rail_splitter" => 8,
                "acid_blister" => 8,
                "forge_dragoon" => 8,
                "ember_strider" => 8,
                "echo_brood" => 8,
                "containermaw" => 10,
                "junction_tyrant" => 10,
                "kiln_custodian" => 10,
                "echo_harbinger" => 10,
                _ => 1
            };
        }

        private static float GetEnemyAnimationFps(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 10f,
                "carapace_brute" => 7f,
                "ash_swarm" => 12f,
                "plated_spore" => 7f,
                "burrow_sapper" => 11f,
                "ember_leech" => 7f,
                "spore_carrier" => 6.5f,
                "rail_warden" => 6.2f,
                "cinder_glider" => 13f,
                "husk_titan" => 5.8f,
                "echo_mimic" => 9f,
                "furnace_matriarch" => 5.4f,
                "cinder_husk" => 7.5f,
                "rail_splitter" => 10f,
                "acid_blister" => 5.5f,
                "forge_dragoon" => 6f,
                "ember_strider" => 11f,
                "echo_brood" => 8f,
                "containermaw" => 5f,
                "junction_tyrant" => 5.5f,
                "kiln_custodian" => 4.5f,
                "echo_harbinger" => 6f,
                _ => 6f
            };
        }

        private static Color GetEnemyFallbackColor(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Color(0.93f, 0.54f, 0.27f),
                "carapace_brute" => new Color(0.59f, 0.33f, 0.23f),
                "ash_swarm" => new Color(0.77f, 0.76f, 0.67f),
                "plated_spore" => new Color(0.46f, 0.68f, 0.42f),
                "burrow_sapper" => new Color(0.88f, 0.47f, 0.22f),
                "ember_leech" => new Color(0.92f, 0.30f, 0.26f),
                "spore_carrier" => new Color(0.74f, 0.85f, 0.50f),
                "rail_warden" => new Color(0.58f, 0.63f, 0.70f),
                "cinder_glider" => new Color(0.97f, 0.58f, 0.18f),
                "husk_titan" => new Color(0.42f, 0.38f, 0.34f),
                "echo_mimic" => new Color(0.56f, 0.44f, 0.82f),
                "furnace_matriarch" => new Color(0.66f, 0.22f, 0.18f),
                "cinder_husk" => new Color(0.32f, 0.26f, 0.24f),
                "rail_splitter" => new Color(0.62f, 0.35f, 0.25f),
                "acid_blister" => new Color(0.78f, 0.88f, 0.42f),
                "forge_dragoon" => new Color(0.55f, 0.30f, 0.28f),
                "ember_strider" => new Color(0.90f, 0.45f, 0.20f),
                "echo_brood" => new Color(0.56f, 0.44f, 0.82f),
                "containermaw" => new Color(0.45f, 0.42f, 0.36f),
                "junction_tyrant" => new Color(0.58f, 0.30f, 0.48f),
                "kiln_custodian" => new Color(0.72f, 0.40f, 0.18f),
                "echo_harbinger" => new Color(0.44f, 0.50f, 0.86f),
                _ => new Color(0.82f, 0.29f, 0.26f)
            };
        }

        private static float GetEnemyCellCoverage(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 0.70f,
                "carapace_brute" => 0.88f,
                "ash_swarm" => 0.62f,
                "plated_spore" => 0.76f,
                "burrow_sapper" => 0.68f,
                "ember_leech" => 0.74f,
                "spore_carrier" => 0.76f,
                "rail_warden" => 0.82f,
                "cinder_glider" => 0.66f,
                "husk_titan" => 1.05f,
                "echo_mimic" => 0.80f,
                "furnace_matriarch" => 1.22f,
                _ => 0.68f
            };
        }

        private static Vector3 GetEnemyVisualOffset(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Vector3(0f, -0.06f, 0f),
                "carapace_brute" => new Vector3(0f, -0.05f, 0f),
                "ash_swarm" => new Vector3(0f, -0.03f, 0f),
                "plated_spore" => new Vector3(0f, -0.04f, 0f),
                "burrow_sapper" => new Vector3(0f, -0.05f, 0f),
                "ember_leech" => new Vector3(0f, -0.04f, 0f),
                "spore_carrier" => new Vector3(0f, -0.04f, 0f),
                "rail_warden" => new Vector3(0f, -0.04f, 0f),
                "cinder_glider" => new Vector3(0f, -0.05f, 0f),
                "husk_titan" => new Vector3(0f, -0.04f, 0f),
                "echo_mimic" => new Vector3(0f, -0.04f, 0f),
                "furnace_matriarch" => new Vector3(0f, -0.03f, 0f),
                _ => new Vector3(0f, -0.04f, 0f)
            };
        }

        private static Vector2 GetEnemyColliderSize(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Vector2(0.34f, 0.34f),
                "carapace_brute" => new Vector2(0.46f, 0.46f),
                "ash_swarm" => new Vector2(0.32f, 0.32f),
                "plated_spore" => new Vector2(0.40f, 0.40f),
                "burrow_sapper" => new Vector2(0.36f, 0.36f),
                "ember_leech" => new Vector2(0.40f, 0.40f),
                "spore_carrier" => new Vector2(0.42f, 0.42f),
                "rail_warden" => new Vector2(0.44f, 0.44f),
                "cinder_glider" => new Vector2(0.34f, 0.34f),
                "husk_titan" => new Vector2(0.52f, 0.52f),
                "echo_mimic" => new Vector2(0.44f, 0.44f),
                "furnace_matriarch" => new Vector2(0.64f, 0.64f),
                "cinder_husk" => new Vector2(0.42f, 0.42f),
                "rail_splitter" => new Vector2(0.44f, 0.30f),
                "acid_blister" => new Vector2(0.44f, 0.44f),
                "forge_dragoon" => new Vector2(0.46f, 0.46f),
                "ember_strider" => new Vector2(0.38f, 0.38f),
                "echo_brood" => new Vector2(0.34f, 0.34f),
                "containermaw" => new Vector2(0.62f, 0.62f),
                "junction_tyrant" => new Vector2(0.60f, 0.60f),
                "kiln_custodian" => new Vector2(0.60f, 0.60f),
                "echo_harbinger" => new Vector2(0.60f, 0.60f),
                _ => new Vector2(0.38f, 0.38f)
            };
        }

        private static Vector2 GetEnemyColliderOffset(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Vector2(0f, -0.05f),
                "carapace_brute" => new Vector2(0f, -0.03f),
                "ash_swarm" => new Vector2(0f, -0.03f),
                "plated_spore" => new Vector2(0f, -0.04f),
                "burrow_sapper" => new Vector2(0f, -0.04f),
                "ember_leech" => new Vector2(0f, -0.04f),
                "spore_carrier" => new Vector2(0f, -0.04f),
                "rail_warden" => new Vector2(0f, -0.03f),
                "cinder_glider" => new Vector2(0f, -0.05f),
                "husk_titan" => new Vector2(0f, -0.03f),
                "echo_mimic" => new Vector2(0f, -0.03f),
                "furnace_matriarch" => new Vector2(0f, -0.02f),
                _ => new Vector2(0f, -0.03f)
            };
        }

        private static int GetEnemySortingOrder(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 16,
                "carapace_brute" => 16,
                "ash_swarm" => 16,
                "plated_spore" => 16,
                "burrow_sapper" => 16,
                "ember_leech" => 16,
                "spore_carrier" => 16,
                "rail_warden" => 16,
                "cinder_glider" => 16,
                "husk_titan" => 17,
                "echo_mimic" => 16,
                "furnace_matriarch" => 18,
                _ => 16
            };
        }

        private static float GetEnemyShadowCoverage(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => 0.62f,
                "carapace_brute" => 0.80f,
                "ash_swarm" => 0.56f,
                "plated_spore" => 0.68f,
                "burrow_sapper" => 0.60f,
                "ember_leech" => 0.66f,
                "spore_carrier" => 0.68f,
                "rail_warden" => 0.72f,
                "cinder_glider" => 0.58f,
                "husk_titan" => 0.92f,
                "echo_mimic" => 0.70f,
                "furnace_matriarch" => 1.04f,
                _ => 0.62f
            };
        }

        private static float GetEnemyShadowAlpha(string enemyId)
        {
            // Contact shadows lifted across the board (+0.22) so bodies read as grounded
            // rather than floating above a faint tint. Relative ordering between enemies preserved.
            return enemyId switch
            {
                "skitter_runner" => 0.52f,
                "carapace_brute" => 0.56f,
                "ash_swarm" => 0.50f,
                "plated_spore" => 0.54f,
                "burrow_sapper" => 0.52f,
                "ember_leech" => 0.52f,
                "spore_carrier" => 0.53f,
                "rail_warden" => 0.55f,
                "cinder_glider" => 0.52f,
                "husk_titan" => 0.57f,
                "echo_mimic" => 0.54f,
                "furnace_matriarch" => 0.58f,
                "cinder_husk" => 0.55f,
                "rail_splitter" => 0.53f,
                "acid_blister" => 0.54f,
                "forge_dragoon" => 0.57f,
                "ember_strider" => 0.52f,
                "echo_brood" => 0.51f,
                "containermaw" => 0.58f,
                "junction_tyrant" => 0.58f,
                "kiln_custodian" => 0.58f,
                "echo_harbinger" => 0.58f,
                _ => 0.52f
            };
        }

    }
}
