using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public sealed class TDGridMap
    {
        private readonly bool[,] _isPath;
        private readonly bool[,] _hasTower;
        private readonly List<Vector3> _pathWorldPoints = new();
        private readonly List<Vector2Int> _recommendedBuildCells = new();
        private readonly Vector2 _bottomLeft;
        private readonly string _mapId;
        private Transform _previewMarkerTransform;
        private SpriteRenderer _previewMarkerRenderer;

        public TDGridMap(int width, int height, float cellSize, IReadOnlyList<Vector2Int> pathCells, Transform root, string mapId = null)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _isPath = new bool[width, height];
            _hasTower = new bool[width, height];
            _mapId = string.IsNullOrWhiteSpace(mapId) ? "grayline_junction" : mapId;

            _bottomLeft = new Vector2(-(width * cellSize) * 0.5f, -(height * cellSize) * 0.5f);

            foreach (var cell in pathCells)
            {
                if (IsInside(cell))
                {
                    _isPath[cell.x, cell.y] = true;
                    _pathWorldPoints.Add(CellToWorld(cell));
                }
            }

            BuildTileVisuals(root);
        }

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public IReadOnlyList<Vector3> PathWorldPoints => _pathWorldPoints;

        public bool TryWorldToCell(Vector3 world, out Vector2Int cell)
        {
            var x = Mathf.FloorToInt((world.x - _bottomLeft.x) / CellSize);
            var y = Mathf.FloorToInt((world.y - _bottomLeft.y) / CellSize);
            cell = new Vector2Int(x, y);
            return IsInside(cell);
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            var worldX = _bottomLeft.x + ((cell.x + 0.5f) * CellSize);
            var worldY = _bottomLeft.y + ((cell.y + 0.5f) * CellSize);
            return new Vector3(worldX, worldY, 0f);
        }

        public bool IsBuildable(Vector2Int cell)
        {
            return IsInside(cell) && !_isPath[cell.x, cell.y] && !_hasTower[cell.x, cell.y];
        }

        public void SetTower(Vector2Int cell, bool hasTower)
        {
            if (!IsInside(cell))
            {
                return;
            }

            _hasTower[cell.x, cell.y] = hasTower;
        }

        public void UpdateBuildPreview(Vector3 worldPosition)
        {
            if (_previewMarkerRenderer == null || _previewMarkerTransform == null)
            {
                return;
            }

            if (!TryWorldToCell(worldPosition, out var cell))
            {
                _previewMarkerRenderer.enabled = false;
                return;
            }

            _previewMarkerRenderer.enabled = true;
            _previewMarkerTransform.position = CellToWorld(cell);

            var buildable = IsBuildable(cell);
            _previewMarkerRenderer.color = buildable
                ? new Color(0.67f, 0.95f, 0.88f, 0.72f)
                : new Color(1f, 0.46f, 0.41f, 0.64f);
        }

        public void HideBuildPreview()
        {
            if (_previewMarkerRenderer != null)
            {
                _previewMarkerRenderer.enabled = false;
            }
        }

        private bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        private void BuildTileVisuals(Transform root)
        {
            var grass = TDArtLibrary.LoadSpriteOrFallback("Art/tile_grass", new Color(0.17f, 0.47f, 0.17f));
            var path = TDArtLibrary.LoadSpriteOrFallback("Art/tile_path", new Color(0.67f, 0.55f, 0.36f));
            var buildMarker = TDArtLibrary.LoadSpriteOrFallback("Art/build_marker", new Color(0.14f, 0.24f, 0.34f));
            var backdrop = TDArtLibrary.LoadSpriteOrFallback("Art/map_backdrop", new Color(0.10f, 0.14f, 0.18f));
            var boardSurface = Resources.Load<Sprite>($"Art/map_surface_{_mapId}_16x9") ??
                               Resources.Load<Sprite>("Art/map_surface_grayline_16x9");
            var shadowOverlay = Resources.Load<Sprite>("Art/map_shadow_overlay");
            var lightOverlay = Resources.Load<Sprite>("Art/map_light_overlay");
            var grassDecals = LoadOptionalSprites(
                "Art/decal_ash_patch_a",
                "Art/decal_ash_patch_b",
                "Art/decal_scrap_cluster_a",
                "Art/decal_scrap_cluster_b");
            var pathDecals = LoadOptionalSprites(
                "Art/decal_path_crack_a",
                "Art/decal_path_crack_b",
                "Art/decal_path_rail_a",
                "Art/decal_path_rail_b");
            var propSprites = LoadOptionalSprites(
                "Art/prop_rail_barricade_a",
                "Art/prop_rail_barricade_b",
                "Art/prop_signal_post_a",
                "Art/prop_signal_post_b",
                "Art/prop_wreck_crate_a",
                "Art/prop_wreck_crate_b");
            var mapSpecificGrassDecals = LoadOptionalSprites(
                $"Art/decal_{_mapId}_ground_a",
                $"Art/decal_{_mapId}_ground_b");
            var mapSpecificPathDecals = LoadOptionalSprites(
                $"Art/decal_{_mapId}_path_a",
                $"Art/decal_{_mapId}_path_b");
            var mapSpecificProps = LoadOptionalSprites(
                $"Art/prop_{_mapId}_a",
                $"Art/prop_{_mapId}_b",
                $"Art/prop_{_mapId}_c");
            var mapAnchorProps = LoadOptionalSprites(
                $"Art/prop_anchor_{_mapId}_a",
                $"Art/prop_anchor_{_mapId}_b",
                $"Art/prop_anchor_{_mapId}_c");
            grassDecals = MergeSpritePools(mapSpecificGrassDecals, grassDecals);
            pathDecals = MergeSpritePools(mapSpecificPathDecals, pathDecals);
            propSprites = MergeSpritePools(mapSpecificProps, propSprites);
            var buildSpotSprite = Resources.Load<Sprite>("Art/tower_base_plate");

            BuildBackdrop(root, backdrop);
            if (boardSurface != null)
            {
                BuildBoardSurface(root, boardSurface);
                DecorateBoardSurface(root, grassDecals, pathDecals, propSprites, mapAnchorProps, buildSpotSprite, buildMarker);
                BuildAtmosphereOverlays(root, shadowOverlay, lightOverlay);
                CreateBuildPreview(root, buildMarker);
                return;
            }

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var tile = new GameObject($"Tile_{x}_{y}");
                    tile.transform.SetParent(root, false);
                    tile.transform.position = CellToWorld(cell);

                    var tileRenderer = tile.AddComponent<SpriteRenderer>();
                    tileRenderer.sortingOrder = 0;
                    tileRenderer.sprite = _isPath[x, y] ? path : grass;
                    ApplyTileVariation(tileRenderer, x, y, _isPath[x, y]);

                    PlaceCellDecal(tile.transform, x, y, _isPath[x, y], grassDecals, pathDecals);
                }
            }

            BuildAtmosphereOverlays(root, shadowOverlay, lightOverlay);
            CreateBuildPreview(root, buildMarker);
        }

        private void BuildBackdrop(Transform root, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var backdrop = new GameObject("MapBackdrop");
            backdrop.transform.SetParent(root, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, 1f);

            var renderer = backdrop.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -5;
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, 0.94f);

            var spriteSize = sprite.bounds.size;
            var targetWidth = Width * CellSize;
            var targetHeight = Height * CellSize;
            var safeWidth = Mathf.Max(0.01f, spriteSize.x);
            var safeHeight = Mathf.Max(0.01f, spriteSize.y);
            backdrop.transform.localScale = new Vector3(targetWidth / safeWidth, targetHeight / safeHeight, 1f);
        }

        private static void ApplyTileVariation(SpriteRenderer renderer, int x, int y, bool isPath)
        {
            var hash = (x * 73856093) ^ (y * 19349663);
            var h1 = (hash & 1023) / 1023f;
            var h2 = ((hash >> 5) & 1023) / 1023f;
            var h3 = ((hash >> 10) & 1023) / 1023f;

            renderer.flipX = h2 > 0.5f;
            renderer.flipY = h3 > 0.7f;

            if (isPath)
            {
                var brightness = Mathf.Lerp(0.92f, 1.08f, h1);
                var warmShift = Mathf.Lerp(-0.04f, 0.05f, h2);
                renderer.color = new Color(
                    Mathf.Clamp01(0.96f * brightness + warmShift),
                    Mathf.Clamp01(0.90f * brightness),
                    Mathf.Clamp01(0.84f * brightness - warmShift * 0.4f),
                    1f);
                return;
            }

            var grassBrightness = Mathf.Lerp(0.90f, 1.07f, h1);
            var hueShift = Mathf.Lerp(-0.05f, 0.04f, h2);
            renderer.color = new Color(
                Mathf.Clamp01(0.92f * grassBrightness + hueShift * 0.25f),
                Mathf.Clamp01(0.98f * grassBrightness),
                Mathf.Clamp01(0.92f * grassBrightness - hueShift * 0.35f),
                1f);
        }

        private void BuildBoardSurface(Transform root, Sprite surface)
        {
            var board = new GameObject("MapSurface");
            board.transform.SetParent(root, false);
            board.transform.localPosition = Vector3.zero;

            var renderer = board.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 0;
            renderer.sprite = surface;
            renderer.color = Color.white;

            var spriteSize = surface.bounds.size;
            var targetWidth = Width * CellSize;
            var targetHeight = Height * CellSize;
            var safeWidth = Mathf.Max(0.01f, spriteSize.x);
            var safeHeight = Mathf.Max(0.01f, spriteSize.y);
            board.transform.localScale = new Vector3(targetWidth / safeWidth, targetHeight / safeHeight, 1f);
        }

        private void BuildAtmosphereOverlays(Transform root, Sprite shadowOverlay, Sprite lightOverlay)
        {
            if (shadowOverlay != null)
            {
                BuildScaledOverlay(root, "MapShadowOverlay", shadowOverlay, 1, new Color(1f, 1f, 1f, 0.26f));
            }

            if (lightOverlay != null)
            {
                BuildScaledOverlay(root, "MapLightOverlay", lightOverlay, 2, new Color(1f, 1f, 1f, 0.20f));
            }
        }

        private void BuildScaledOverlay(Transform root, string name, Sprite sprite, int sortingOrder, Color tint)
        {
            var overlay = new GameObject(name);
            overlay.transform.SetParent(root, false);
            overlay.transform.localPosition = Vector3.zero;

            var renderer = overlay.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.sprite = sprite;
            renderer.color = tint;

            var spriteSize = sprite.bounds.size;
            var targetWidth = Width * CellSize;
            var targetHeight = Height * CellSize;
            var safeWidth = Mathf.Max(0.01f, spriteSize.x);
            var safeHeight = Mathf.Max(0.01f, spriteSize.y);
            overlay.transform.localScale = new Vector3(targetWidth / safeWidth, targetHeight / safeHeight, 1f);
        }

        private void CreateBuildPreview(Transform root, Sprite buildMarker)
        {
            var preview = new GameObject("BuildPreview");
            preview.transform.SetParent(root, false);
            preview.transform.localScale = ResolveSpriteScaleForCell(buildMarker, 0.92f);
            _previewMarkerTransform = preview.transform;

            _previewMarkerRenderer = preview.AddComponent<SpriteRenderer>();
            _previewMarkerRenderer.sprite = buildMarker;
            _previewMarkerRenderer.sortingOrder = 7;
            _previewMarkerRenderer.enabled = false;
        }

        private Vector3 ResolveSpriteScaleForCell(Sprite sprite, float cellCoverage)
        {
            if (sprite == null)
            {
                return Vector3.one;
            }

            var spriteWidth = Mathf.Max(0.0001f, sprite.bounds.size.x);
            var targetWidth = Mathf.Max(0.1f, CellSize * Mathf.Clamp(cellCoverage, 0.1f, 2f));
            return Vector3.one * (targetWidth / spriteWidth);
        }

        private static List<Sprite> LoadOptionalSprites(params string[] paths)
        {
            var sprites = new List<Sprite>();
            for (var i = 0; i < paths.Length; i++)
            {
                var sprite = Resources.Load<Sprite>(paths[i]);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites;
        }

        private static List<Sprite> MergeSpritePools(List<Sprite> primary, List<Sprite> fallback)
        {
            if (primary != null && primary.Count > 0)
            {
                return primary;
            }

            return fallback ?? new List<Sprite>();
        }

        private static void PlaceCellDecal(Transform tileRoot, int x, int y, bool isPath, List<Sprite> grassDecals, List<Sprite> pathDecals)
        {
            var source = isPath ? pathDecals : grassDecals;
            if (source == null || source.Count == 0)
            {
                return;
            }

            var chance = isPath ? 0.38f : 0.26f;
            if (Hash01(x, y, 901) > chance)
            {
                return;
            }

            var pick = Mathf.FloorToInt(Hash01(x, y, 733) * source.Count) % source.Count;
            var sprite = source[pick];
            if (sprite == null)
            {
                return;
            }

            var decal = new GameObject(isPath ? $"PathDecal_{x}_{y}" : $"GrassDecal_{x}_{y}");
            decal.transform.SetParent(tileRoot, false);
            decal.transform.localPosition = new Vector3(0f, 0f, 0f);
            decal.transform.localRotation = Quaternion.Euler(0f, 0f, Hash01(x, y, 512) * 360f);

            var scaleBase = isPath ? 0.92f : 0.98f;
            var scaleJitter = isPath ? 0.25f : 0.30f;
            var scale = scaleBase + ((Hash01(x, y, 358) - 0.5f) * scaleJitter);
            decal.transform.localScale = Vector3.one * Mathf.Max(0.56f, scale);

            var renderer = decal.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1;
            renderer.sprite = sprite;

            if (isPath)
            {
                var alpha = Mathf.Lerp(0.32f, 0.54f, Hash01(x, y, 1203));
                renderer.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                var alpha = Mathf.Lerp(0.24f, 0.46f, Hash01(x, y, 1207));
                renderer.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        private void DecorateBoardSurface(
            Transform root,
            List<Sprite> grassDecals,
            List<Sprite> pathDecals,
            List<Sprite> propSprites,
            List<Sprite> mapAnchorProps,
            Sprite buildSpotSprite,
            Sprite buildMarkerFallback)
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var isPath = _isPath[x, y];

                    var decals = isPath ? pathDecals : grassDecals;
                    var decalChance = isPath ? 0.24f : 0.10f;
                    TryPlaceSurfaceDecal(root, cell, decals, decalChance, isPath ? 0.92f : 0.98f, 3);

                    if (isPath || !IsNearPath(cell))
                    {
                        continue;
                    }

                    if (propSprites == null || propSprites.Count == 0 || Hash01(x, y, 441) > 0.08f)
                    {
                        continue;
                    }

                    var sprite = propSprites[Mathf.FloorToInt(Hash01(x, y, 557) * propSprites.Count) % propSprites.Count];
                    if (sprite == null)
                    {
                        continue;
                    }

                    var prop = new GameObject($"SurfaceProp_{x}_{y}");
                    prop.transform.SetParent(root, false);

                    var cellCenter = CellToWorld(cell);
                    var jitterX = (Hash01(x, y, 605) - 0.5f) * CellSize * 0.34f;
                    var jitterY = (Hash01(x, y, 607) - 0.5f) * CellSize * 0.28f;
                    prop.transform.position = new Vector3(cellCenter.x + jitterX, cellCenter.y + jitterY - 0.08f, 0f);
                    prop.transform.rotation = Quaternion.Euler(0f, 0f, (Hash01(x, y, 611) - 0.5f) * 16f);

                    var renderer = prop.AddComponent<SpriteRenderer>();
                    renderer.sortingOrder = 4;
                    renderer.sprite = sprite;

                    var aspect = sprite.bounds.size.y / Mathf.Max(0.0001f, sprite.bounds.size.x);
                    var baseCoverage = aspect > 1.1f ? 0.60f : 0.86f;
                    var jitterCoverage = (Hash01(x, y, 613) - 0.5f) * 0.14f;
                    var coverage = Mathf.Clamp(baseCoverage + jitterCoverage, 0.52f, 1.04f);
                    prop.transform.localScale = ResolveSpriteScaleForCell(sprite, coverage);

                    var alpha = Mathf.Lerp(0.66f, 0.84f, Hash01(x, y, 617));
                    renderer.color = new Color(1f, 1f, 1f, alpha);
                }
            }

            BuildMapAnchors(root, mapAnchorProps);
            BuildRecommendedSpots(root, buildSpotSprite != null ? buildSpotSprite : buildMarkerFallback);
        }

        private void BuildMapAnchors(Transform root, List<Sprite> mapAnchorProps)
        {
            if (mapAnchorProps == null || mapAnchorProps.Count == 0)
            {
                return;
            }

            var anchorCells = GetMapAnchorCells();
            for (var i = 0; i < anchorCells.Length; i++)
            {
                var cell = anchorCells[i];
                if (!IsInside(cell))
                {
                    continue;
                }

                var sprite = mapAnchorProps[i % mapAnchorProps.Count];
                if (sprite == null)
                {
                    continue;
                }

                var anchor = new GameObject($"MapAnchor_{cell.x}_{cell.y}");
                anchor.transform.SetParent(root, false);

                var center = CellToWorld(cell);
                var jitterX = (Hash01(cell.x, cell.y, 931 + i) - 0.5f) * CellSize * 0.32f;
                var jitterY = (Hash01(cell.x, cell.y, 937 + i) - 0.5f) * CellSize * 0.24f;
                anchor.transform.position = new Vector3(center.x + jitterX, center.y + jitterY - 0.10f, 0f);
                anchor.transform.rotation = Quaternion.Euler(0f, 0f, (Hash01(cell.x, cell.y, 941 + i) - 0.5f) * 12f);

                var renderer = anchor.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 4;
                renderer.sprite = sprite;
                renderer.color = new Color(1f, 1f, 1f, 0.92f);

                var aspect = sprite.bounds.size.y / Mathf.Max(0.0001f, sprite.bounds.size.x);
                var baseCoverage = aspect > 1.15f ? 1.08f : 1.22f;
                var coverage = Mathf.Clamp(baseCoverage + ((Hash01(cell.x, cell.y, 947 + i) - 0.5f) * 0.10f), 0.92f, 1.34f);
                anchor.transform.localScale = ResolveSpriteScaleForCell(sprite, coverage);
            }
        }

        private void TryPlaceSurfaceDecal(
            Transform root,
            Vector2Int cell,
            List<Sprite> spritePool,
            float chance,
            float baseCoverage,
            int sortingOrder)
        {
            if (spritePool == null || spritePool.Count == 0)
            {
                return;
            }

            if (Hash01(cell.x, cell.y, 701) > chance)
            {
                return;
            }

            var sprite = spritePool[Mathf.FloorToInt(Hash01(cell.x, cell.y, 709) * spritePool.Count) % spritePool.Count];
            if (sprite == null)
            {
                return;
            }

            var decal = new GameObject($"SurfaceDecal_{cell.x}_{cell.y}_{sortingOrder}");
            decal.transform.SetParent(root, false);

            var center = CellToWorld(cell);
            var offsetX = (Hash01(cell.x, cell.y, 713) - 0.5f) * CellSize * 0.22f;
            var offsetY = (Hash01(cell.x, cell.y, 719) - 0.5f) * CellSize * 0.18f;
            decal.transform.position = new Vector3(center.x + offsetX, center.y + offsetY - 0.03f, 0f);
            decal.transform.rotation = Quaternion.Euler(0f, 0f, Hash01(cell.x, cell.y, 727) * 360f);

            var renderer = decal.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.sprite = sprite;

            var coverage = Mathf.Clamp(baseCoverage + ((Hash01(cell.x, cell.y, 733) - 0.5f) * 0.20f), 0.58f, 1.28f);
            decal.transform.localScale = ResolveSpriteScaleForCell(sprite, coverage);
            var alpha = Mathf.Lerp(0.14f, 0.34f, Hash01(cell.x, cell.y, 739));
            renderer.color = new Color(1f, 1f, 1f, alpha);
        }

        private void BuildRecommendedSpots(Transform root, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            _recommendedBuildCells.Clear();
            var candidates = new List<Vector2Int>();
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!IsBuildable(cell) || !IsNearPath(cell))
                    {
                        continue;
                    }

                    if (Hash01(x, y, 801) > 0.52f)
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            candidates.Sort((a, b) =>
            {
                var aScore = Hash01(a.x, a.y, 809) + (Mathf.Abs((Width * 0.5f) - a.x) * 0.018f);
                var bScore = Hash01(b.x, b.y, 809) + (Mathf.Abs((Width * 0.5f) - b.x) * 0.018f);
                return aScore.CompareTo(bScore);
            });

            for (var i = 0; i < candidates.Count; i++)
            {
                var cell = candidates[i];
                var blocked = false;
                for (var j = 0; j < _recommendedBuildCells.Count; j++)
                {
                    var other = _recommendedBuildCells[j];
                    var manhattan = Mathf.Abs(cell.x - other.x) + Mathf.Abs(cell.y - other.y);
                    if (manhattan < 3)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                {
                    continue;
                }

                _recommendedBuildCells.Add(cell);
                if (_recommendedBuildCells.Count >= 8)
                {
                    break;
                }
            }

            for (var i = 0; i < _recommendedBuildCells.Count; i++)
            {
                var cell = _recommendedBuildCells[i];
                var marker = new GameObject($"BuildSpot_{cell.x}_{cell.y}");
                marker.transform.SetParent(root, false);
                marker.transform.position = CellToWorld(cell) + new Vector3(0f, -0.06f, 0f);
                marker.transform.rotation = Quaternion.Euler(0f, 0f, Hash01(cell.x, cell.y, 821) * 360f);
                marker.transform.localScale = ResolveSpriteScaleForCell(sprite, 0.80f);

                var renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 5;
                renderer.sprite = sprite;
                renderer.color = new Color(0.88f, 0.95f, 1f, 0.24f);
            }
        }

        private bool IsNearPath(Vector2Int cell)
        {
            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    var nx = cell.x + ox;
                    var ny = cell.y + oy;
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                    {
                        continue;
                    }

                    if (_isPath[nx, ny])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Vector2Int[] GetMapAnchorCells()
        {
            return _mapId switch
            {
                "ashfall_depot" => new[] { new Vector2Int(2, 7), new Vector2Int(12, 1) },
                "split_switch_canyon" => new[] { new Vector2Int(4, 7), new Vector2Int(11, 1) },
                "hollow_kiln_basin" => new[] { new Vector2Int(1, 1), new Vector2Int(14, 6) },
                "last_ember_terminus" => new[] { new Vector2Int(3, 7), new Vector2Int(13, 1) },
                _ => new[] { new Vector2Int(2, 2), new Vector2Int(13, 6) }
            };
        }

        private static float Hash01(int x, int y, int salt)
        {
            var n = (x * 73856093) ^ (y * 19349663) ^ (salt * 83492791);
            n ^= n >> 13;
            n *= 1274126177;
            n ^= n >> 16;
            var positive = n & 0x7fffffff;
            return (positive % 10000) / 10000f;
        }
    }
}
