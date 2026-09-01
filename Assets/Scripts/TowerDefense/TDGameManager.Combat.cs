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
                _ => 0.52f
            };
        }

    }
}
