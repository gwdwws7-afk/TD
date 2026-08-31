// Freeze-period move: Paths cluster.
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
        private static string ResolveTowerFirePath(string lowerKey)
        {
            // tower_fire_<kind> -> SFX/Tower/fire_<snake_kind>
            var token = lowerKey.Substring("tower_fire_".Length);
            return token switch
            {
                "raillancer" => "SFX/Tower/fire_rail_lancer",
                "cindermortar" => "SFX/Tower/fire_cinder_mortar",
                "frostcoil" => "SFX/Tower/fire_frost_coil",
                "arcwelder" => "SFX/Tower/fire_arc_welder",
                "siegedrill" => "SFX/Tower/fire_siege_drill",
                "emberflak" => "SFX/Tower/fire_ember_flak",
                "resonancebeacon" => "SFX/Tower/fire_resonance_beacon",
                "gravsnare" => "SFX/Tower/fire_grav_snare",
                _ => null,
            };
        }

        private static IReadOnlyList<Vector2Int> GetPathCellsForMap(string mapId)
        {
            return ConvertLayoutCellsToUnityCells(GetLayoutPathCellsForMap(mapId));
        }

        private static IReadOnlyList<Vector2Int> GetLayoutPathCellsForMap(string mapId)
        {
            return mapId switch
            {
                "ashfall_depot" => AshfallBuildPathCells,
                "split_switch_canyon" => SplitSwitchBuildPathCells,
                "hollow_kiln_basin" => HollowKilnBuildPathCells,
                "last_ember_terminus" => LastEmberBuildPathCells,
                _ => GraylinePathCells
            };
        }

        private void ConfigureActiveLanePaths(string mapId)
        {
            _activeLanePaths.Clear();
            var basePath = BuildWorldPathFromLayoutCells(GetLayoutPathCellsForMap(mapId));
            _activeLanePaths["default"] = basePath;
            _activeLanePaths["center"] = basePath;
            _activeLanePaths["all"] = basePath;

            if (string.Equals(mapId, "grayline_junction", StringComparison.OrdinalIgnoreCase))
            {
                var roadPath = BuildWorldPathFromLayoutPoints(GraylineRoadPathPoints);
                if (roadPath.Count > 1)
                {
                    _activeLanePaths["default"] = roadPath;
                    _activeLanePaths["center"] = roadPath;
                    _activeLanePaths["all"] = roadPath;
                }

                return;
            }

            if (string.Equals(mapId, "split_switch_canyon", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromLayoutPoints(SplitSwitchCenterRoadPathPoints);
                var leftPath = BuildWorldPathFromLayoutPoints(SplitSwitchLeftRoadPathPoints);
                var rightPath = BuildWorldPathFromLayoutPoints(SplitSwitchRightRoadPathPoints);
                var crossPath = BuildWorldPathFromLayoutPoints(SplitSwitchCrossRoadPathPoints);

                _activeLanePaths["default"] = centerPath;
                _activeLanePaths["center"] = centerPath;
                _activeLanePaths["left"] = leftPath;
                _activeLanePaths["right"] = rightPath;
                _activeLanePaths["split_lane"] = leftPath;
                _activeLanePaths["cross_lane"] = crossPath;
                _activeLanePaths["all"] = centerPath;
                return;
            }

            if (string.Equals(mapId, "ashfall_depot", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromLayoutPoints(AshfallCenterRoadPathPoints);
                var leftPath = BuildWorldPathFromLayoutPoints(AshfallLeftRoadPathPoints);
                var rightPath = BuildWorldPathFromLayoutPoints(AshfallRightRoadPathPoints);
                var crossPath = BuildWorldPathFromLayoutPoints(AshfallCrossRoadPathPoints);

                _activeLanePaths["default"] = centerPath;
                _activeLanePaths["center"] = centerPath;
                _activeLanePaths["left"] = leftPath;
                _activeLanePaths["right"] = rightPath;
                _activeLanePaths["split_lane"] = leftPath;
                _activeLanePaths["cross_lane"] = crossPath;
                _activeLanePaths["all"] = centerPath;
                return;
            }

            if (string.Equals(mapId, "hollow_kiln_basin", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromLayoutPoints(HollowKilnCenterRoadPathPoints);
                var leftPath = BuildWorldPathFromLayoutPoints(HollowKilnLeftRoadPathPoints);
                var rightPath = BuildWorldPathFromLayoutPoints(HollowKilnRightRoadPathPoints);
                var crossPath = BuildWorldPathFromLayoutPoints(HollowKilnCrossRoadPathPoints);

                _activeLanePaths["default"] = centerPath;
                _activeLanePaths["center"] = centerPath;
                _activeLanePaths["left"] = leftPath;
                _activeLanePaths["right"] = rightPath;
                _activeLanePaths["split_lane"] = leftPath;
                _activeLanePaths["cross_lane"] = crossPath;
                _activeLanePaths["all"] = centerPath;
                return;
            }

            if (string.Equals(mapId, "last_ember_terminus", StringComparison.OrdinalIgnoreCase))
            {
                var centerPath = BuildWorldPathFromLayoutPoints(LastEmberCenterRoadPathPoints);
                var leftPath = BuildWorldPathFromLayoutPoints(LastEmberLeftRoadPathPoints);
                var rightPath = BuildWorldPathFromLayoutPoints(LastEmberRightRoadPathPoints);
                var crossPath = BuildWorldPathFromLayoutPoints(LastEmberCrossRoadPathPoints);

                _activeLanePaths["default"] = centerPath;
                _activeLanePaths["center"] = centerPath;
                _activeLanePaths["left"] = leftPath;
                _activeLanePaths["right"] = rightPath;
                _activeLanePaths["split_lane"] = leftPath;
                _activeLanePaths["cross_lane"] = crossPath;
                _activeLanePaths["all"] = centerPath;
            }
        }

        private static Vector2Int[] CombinePathCells(params Vector2Int[][] pathSets)
        {
            var combined = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            if (pathSets == null)
            {
                return combined.ToArray();
            }

            for (var p = 0; p < pathSets.Length; p++)
            {
                var set = pathSets[p];
                if (set == null)
                {
                    continue;
                }

                for (var i = 0; i < set.Length; i++)
                {
                    var cell = set[i];
                    if (seen.Add(cell))
                    {
                        combined.Add(cell);
                    }
                }
            }

            return combined.ToArray();
        }

        private static Vector2Int[] ConvertLayoutCellsToUnityCells(IReadOnlyList<Vector2Int> layoutCells)
        {
            var converted = new List<Vector2Int>(layoutCells?.Count ?? 0);
            if (layoutCells == null)
            {
                return converted.ToArray();
            }

            for (var i = 0; i < layoutCells.Count; i++)
            {
                converted.Add(LayoutCellToUnityCell(layoutCells[i]));
            }

            return converted.ToArray();
        }

        private static Vector2Int LayoutCellToUnityCell(Vector2Int cell)
        {
            return new Vector2Int(cell.x, GridHeight - 1 - cell.y);
        }

        private static Vector3 LayoutPointToWorld(Vector2 point)
        {
            var worldX = (-(GridWidth * CellSize) * 0.5f) + (point.x * CellSize);
            var worldY = ((GridHeight * CellSize) * 0.5f) - (point.y * CellSize);
            return new Vector3(worldX, worldY, 0f);
        }

        private static IReadOnlyList<Vector3> BuildWorldPathFromLayoutPoints(Vector2[] pathPoints)
        {
            var anchors = new List<Vector3>(pathPoints?.Length ?? 0);
            if (pathPoints == null)
            {
                return anchors;
            }

            for (var i = 0; i < pathPoints.Length; i++)
            {
                anchors.Add(LayoutPointToWorld(pathPoints[i]));
            }

            return BuildSmoothPath(anchors, 0.05f);
        }

        private static List<Vector3> BuildWorldPathFromLayoutCells(IReadOnlyList<Vector2Int> pathCells)
        {
            var points = new List<Vector3>(pathCells?.Count ?? 0);
            if (pathCells == null)
            {
                return points;
            }

            for (var i = 0; i < pathCells.Count; i++)
            {
                var cell = pathCells[i];
                points.Add(LayoutPointToWorld(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
            }

            return BuildSmoothPath(points, 0.05f).ToList();
        }

        private static IReadOnlyList<Vector3> BuildSmoothPath(IReadOnlyList<Vector3> anchors, float targetSpacing)
        {
            var result = new List<Vector3>();
            if (anchors == null || anchors.Count == 0)
            {
                return result;
            }

            if (anchors.Count == 1)
            {
                result.Add(anchors[0]);
                return result;
            }

            var spacing = Mathf.Clamp(targetSpacing, 0.04f, 0.25f);
            for (var i = 0; i < anchors.Count - 1; i++)
            {
                var p0 = anchors[Mathf.Max(0, i - 1)];
                var p1 = anchors[i];
                var p2 = anchors[i + 1];
                var p3 = anchors[Mathf.Min(anchors.Count - 1, i + 2)];
                var samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / spacing));
                for (var sample = 0; sample < samples; sample++)
                {
                    var t = sample / (float)samples;
                    var t2 = t * t;
                    var t3 = t2 * t;
                    var point = 0.5f *
                                ((2f * p1) +
                                 ((-p0 + p2) * t) +
                                 (((2f * p0) - (5f * p1) + (4f * p2) - p3) * t2) +
                                 ((-p0 + (3f * p1) - (3f * p2) + p3) * t3));
                    AppendPathPointWithMaximumSpacing(result, point, spacing);
                }
            }

            AppendPathPointWithMaximumSpacing(result, anchors[anchors.Count - 1], spacing);
            return result;
        }

        private static void AppendPathPointWithMaximumSpacing(List<Vector3> result, Vector3 point, float maximumSpacing)
        {
            if (result.Count == 0)
            {
                result.Add(point);
                return;
            }

            var previous = result[result.Count - 1];
            var distance = Vector3.Distance(previous, point);
            if (distance <= 0.0001f)
            {
                return;
            }

            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.01f, maximumSpacing)));
            for (var step = 1; step <= steps; step++)
            {
                result.Add(Vector3.Lerp(previous, point, step / (float)steps));
            }
        }

        private IReadOnlyList<Vector3> GetDefaultSpawnPath()
        {
            if (_activeLanePaths.TryGetValue("default", out var defaultPath) && defaultPath != null && defaultPath.Count > 1)
            {
                return defaultPath;
            }

            return _gridMap?.PathWorldPoints ?? Array.Empty<Vector3>();
        }

        private string ResolveSpawnLaneKey(TDWaveGroup group, string formation, int spawnIndex)
        {
            var lane = NormalizeGroupToken(group?.lane);
            if (string.IsNullOrEmpty(lane))
            {
                lane = formation switch
                {
                    "split_lane" => "split_lane",
                    "cross_lane" => "cross_lane",
                    _ => "default"
                };
            }

            if (_activeScenarioMechanic != null &&
                NormalizeGroupToken(_activeScenarioMechanic.mechanicType) == "route_switch" &&
                !string.Equals(_scenarioRouteBias, "center", StringComparison.Ordinal) &&
                (lane == "default" || lane == "center" || lane == "all" || lane == "split_lane" || lane == "cross_lane"))
            {
                lane = _scenarioRouteBias;
            }

            if (lane == "all")
            {
                lane = ResolveAllLaneKey(formation, spawnIndex);
            }

            return ResolveExistingLaneKey(lane);
        }

        private string ResolveAllLaneKey(string formation, int spawnIndex)
        {
            if (!_activeLanePaths.ContainsKey("left") || !_activeLanePaths.ContainsKey("right"))
            {
                return _activeLanePaths.ContainsKey("center") ? "center" : "default";
            }

            if (string.Equals(formation, "pressure_mix", StringComparison.Ordinal))
            {
                if (_activeLanePaths.ContainsKey("cross_lane"))
                {
                    switch (spawnIndex % 3)
                    {
                        case 0:
                            return "left";
                        case 1:
                            return "right";
                        default:
                            return "cross_lane";
                    }
                }

                return spawnIndex % 2 == 0 ? "left" : "right";
            }

            if (string.Equals(formation, "adaptive", StringComparison.Ordinal) && _activeLanePaths.ContainsKey("cross_lane"))
            {
                return spawnIndex % 2 == 0 ? "cross_lane" : "left";
            }

            return spawnIndex % 2 == 0 ? "left" : "right";
        }

        private static float GetFormationStartDelayOffset(string formation)
        {
            return formation switch
            {
                "flank_strike" => -0.28f,
                "flank_stream" => -0.12f,
                "burst" => -0.08f,
                "elite_drop" => 0.24f,
                "boss_entry" => 0.48f,
                _ => 0f
            };
        }

        private float ResolveSpawnCadence(float baseInterval, string formation, int spawnIndex, int count)
        {
            var interval = Mathf.Max(baseInterval, _waveSet.globalDefaults.spawnMinSpacing);
            var safeCount = Mathf.Max(1, count);

            switch (formation)
            {
                case "pack":
                    interval *= (spawnIndex % 3 == 2) ? 1.30f : 0.72f;
                    break;
                case "burst":
                    interval *= (spawnIndex % 4 == 3) ? 1.45f : 0.58f;
                    break;
                case "stagger":
                    interval *= (spawnIndex % 2 == 0) ? 0.70f : 1.42f;
                    break;
                case "flank_stream":
                    interval *= 0.86f;
                    break;
                case "flank_strike":
                    interval *= (spawnIndex % 3 == 0) ? 0.62f : 0.92f;
                    break;
                case "pressure_mix":
                    switch (spawnIndex % 4)
                    {
                        case 0:
                            interval *= 0.78f;
                            break;
                        case 1:
                            interval *= 1.12f;
                            break;
                        case 2:
                            interval *= 0.90f;
                            break;
                        default:
                            interval *= 1.26f;
                            break;
                    }
                    break;
                case "adaptive":
                    var progress = safeCount <= 1 ? 1f : (float)spawnIndex / (safeCount - 1);
                    interval *= Mathf.Lerp(1.14f, 0.78f, progress);
                    break;
                case "spawn_chain":
                    interval *= spawnIndex % 2 == 0 ? 0.68f : 1.08f;
                    break;
                case "escort":
                    interval *= spawnIndex < safeCount / 2 ? 1.18f : 0.86f;
                    break;
                case "elite_drop":
                    interval *= spawnIndex % 2 == 0 ? 1.22f : 0.88f;
                    break;
                case "boss_entry":
                    interval *= 1.32f;
                    break;
                case "split_lane":
                    interval *= 0.94f;
                    break;
                case "cross_lane":
                    interval *= 0.90f;
                    break;
            }

            return Mathf.Max(_waveSet.globalDefaults.spawnMinSpacing, interval);
        }

        private static string NormalizeGroupToken(string token)
        {
            return string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim().ToLowerInvariant();
        }

    }
}
