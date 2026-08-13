using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Visual campaign world map showing all 20 levels as nodes on an S-curve path.
    /// Replaces the flat level-button grid in the mission board.
    ///
    /// Nodes are positioned in a zigzag layout across 4 chapter zones.
    /// Each node shows level number + state (locked/available/cleared/boss).
    /// Clicking a node selects that level.
    /// </summary>
    public sealed class TDWorldMap : MonoBehaviour
    {
        private RectTransform _root;
        private readonly List<RectTransform> _nodes = new();
        private readonly List<Image> _nodeImages = new();
        private readonly List<Text> _nodeLabels = new();
        private readonly List<Image> _pathImages = new();
        private readonly List<RectTransform> _chapterZones = new();

        public System.Action<int> OnNodeClicked;

        // S-curve layout: 4 rows of 5, alternating direction.
        // Coordinates are relative to map root (0,0 = center).
        private static readonly Vector2[] NodePositions = GenerateNodePositions();

        // Chapter zone colors (subtle background tints).
        private static readonly Color[] ChapterColors =
        {
            new(0.20f, 0.30f, 0.45f, 0.15f), // A: blue-gray
            new(0.40f, 0.25f, 0.15f, 0.15f), // B: ember-brown
            new(0.15f, 0.30f, 0.35f, 0.15f), // C: teal-dark
            new(0.35f, 0.15f, 0.20f, 0.15f), // D: deep red
        };

        // Node state colors.
        private static readonly Color ColorLocked = new(0.15f, 0.16f, 0.18f, 0.85f);
        private static readonly Color ColorAvailable = new(0.96f, 0.58f, 0.24f, 0.95f);
        private static readonly Color ColorCleared = new(0.26f, 0.74f, 0.52f, 0.90f);
        private static readonly Color ColorBoss = new(0.92f, 0.28f, 0.22f, 0.95f);
        private static readonly Color ColorSelected = new(0.98f, 0.88f, 0.32f, 1f);
        private static readonly Color ColorTextDark = new(0.04f, 0.05f, 0.07f, 0.95f);
        private static readonly Color ColorTextBright = new(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color ColorPathLocked = new(0.12f, 0.13f, 0.15f, 0.40f);
        private static readonly Color ColorPathCleared = new(0.26f, 0.60f, 0.42f, 0.60f);

        private const float MapWidth = 700f;
        private const float MapHeight = 400f;
        private const float NodeSize = 42f;
        private const float BossNodeSize = 54f;
        private const float PathThickness = 4f;

        public RectTransform Root => _root;

        /// <summary>Build the world map UI inside the given parent.</summary>
        public void Build(Transform parent, float localX, float localY)
        {
            _root = CreateRect("WorldMap", parent);
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = new Vector2(localX, localY);
            _root.sizeDelta = new Vector2(MapWidth, MapHeight);

            // Draw chapter zone backgrounds.
            for (var ch = 0; ch < 4; ch++)
            {
                var zoneRect = CreateRect($"ChapterZone_{ch}", _root);
                zoneRect.anchorMin = new Vector2(0f, 1f);
                zoneRect.anchorMax = new Vector2(0f, 1f);
                zoneRect.pivot = new Vector2(0.5f, 0.5f);

                // Each zone covers ~5 nodes in a horizontal band.
                var rowY = GetRowY(ch);
                zoneRect.anchoredPosition = new Vector2(0f, rowY);
                zoneRect.sizeDelta = new Vector2(MapWidth * 0.92f, MapHeight * 0.22f);

                var zoneImg = zoneRect.gameObject.AddComponent<Image>();
                zoneImg.color = ChapterColors[ch];
                zoneImg.raycastTarget = false;
                _chapterZones.Add(zoneRect);
            }

            // Draw paths between consecutive nodes.
            for (var i = 0; i < 19; i++)
            {
                DrawPathSegment(NodePositions[i], NodePositions[i + 1]);
            }

            // Draw nodes.
            for (var i = 0; i < 20; i++)
            {
                CreateNode(i);
            }
        }

        /// <summary>
        /// Update all node visuals based on the current campaign state.
        /// </summary>
        public void Refresh(
            int selectedLevel,
            int highestUnlocked,
            bool[] clearedLevels,
            int[] starsPerLevel,
            int totalLevels,
            int bossLevel)
        {
            for (var i = 0; i < _nodes.Count && i < totalLevels; i++)
            {
                var levelIndex = i + 1; // 1-based
                var isBoss = levelIndex == bossLevel;
                var isLocked = levelIndex > highestUnlocked;
                var isCleared = i < clearedLevels.Length && clearedLevels[i];
                var isSelected = levelIndex == selectedLevel;

                Color nodeColor;
                Color textColor;
                float size;

                if (isBoss)
                {
                    nodeColor = isCleared ? ColorCleared : ColorBoss;
                    textColor = ColorTextBright;
                    size = BossNodeSize;
                }
                else if (isLocked)
                {
                    nodeColor = ColorLocked;
                    textColor = new Color(0.40f, 0.42f, 0.46f, 0.70f);
                    size = NodeSize;
                }
                else if (isCleared)
                {
                    nodeColor = isSelected ? ColorSelected : ColorCleared;
                    textColor = ColorTextDark;
                    size = NodeSize;
                }
                else
                {
                    // Available (unlocked but not cleared).
                    nodeColor = isSelected ? ColorSelected : ColorAvailable;
                    textColor = ColorTextDark;
                    size = NodeSize;
                }

                _nodeImages[i].color = nodeColor;
                _nodes[i].sizeDelta = Vector2.one * size;

                if (_nodeLabels[i] != null)
                {
                    var label = isLocked ? "🔒" : $"L{levelIndex:00}";
                    if (isCleared && !isBoss && i < starsPerLevel.Length)
                    {
                        var stars = starsPerLevel[i];
                        label = $"L{levelIndex:00}\n{"★".PadRight(stars + 1).Substring(0, Mathf.Max(1, stars))}";
                    }
                    else if (isBoss)
                    {
                        label = isCleared ? "L20\n★" : "L20\nBOSS";
                    }
                    _nodeLabels[i].text = label;
                    _nodeLabels[i].fontSize = isBoss ? 9 : 8;
                    _nodeLabels[i].color = textColor;
                }

                // Node is interactable only if unlocked.
                var button = _nodes[i].GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !isLocked;
                }
            }

            // Update path colors: cleared segments are bright, locked are dim.
            for (var i = 0; i < _pathImages.Count && i < 19; i++)
            {
                var fromCleared = i < clearedLevels.Length && clearedLevels[i];
                _pathImages[i].color = fromCleared ? ColorPathCleared : ColorPathLocked;
            }
        }

        // ─── Layout ──────────────────────────────────────────────────

        private static Vector2[] GenerateNodePositions()
        {
            var positions = new Vector2[20];
            for (var i = 0; i < 20; i++)
            {
                var chapter = i / 5;     // 0-3
                var col = i % 5;         // 0-4
                var rowY = GetRowY(chapter);

                // Alternate direction per chapter for S-curve.
                float x;
                if (chapter % 2 == 0)
                {
                    // Left to right.
                    x = -MapWidth * 0.40f + col * (MapWidth * 0.20f);
                }
                else
                {
                    // Right to left.
                    x = MapWidth * 0.40f - col * (MapWidth * 0.20f);
                }

                positions[i] = new Vector2(x, rowY);
            }

            return positions;
        }

        private static float GetRowY(int chapter)
        {
            // 4 rows evenly spaced in MapHeight.
            return MapHeight * 0.35f - chapter * (MapHeight * 0.24f);
        }

        // ─── Node/Path creation ──────────────────────────────────────

        private void CreateNode(int index)
        {
            var pos = NodePositions[index];
            var nodeRect = CreateRect($"Node_L{index + 1:00}", _root);
            nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
            nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
            nodeRect.pivot = new Vector2(0.5f, 0.5f);
            nodeRect.anchoredPosition = pos;
            nodeRect.sizeDelta = Vector2.one * NodeSize;

            var ringSprite = TDArtLibrary.GetSoftRingSprite();
            var img = nodeRect.gameObject.AddComponent<Image>();
            img.sprite = ringSprite;
            img.color = ColorLocked;
            img.type = Image.Type.Simple;

            var btn = nodeRect.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.3f, 0.30f);
            colors.pressedColor = new Color(1f, 0.85f, 0.3f, 0.50f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            btn.colors = colors;

            var capturedIndex = index;
            btn.onClick.AddListener(() => OnNodeClicked?.Invoke(capturedIndex + 1));

            // Label on a child (can't share GameObject with Image).
            var labelRect = CreateRect($"NodeLabel_L{index + 1:00}", nodeRect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelRect.gameObject.AddComponent<Text>();
            label.font = TDLocalization.ResolveFont(null) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = $"L{index + 1:00}";
            label.fontSize = 8;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = ColorTextBright;
            label.raycastTarget = false;

            _nodes.Add(nodeRect);
            _nodeImages.Add(img);
            _nodeLabels.Add(label);
        }

        private void DrawPathSegment(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var distance = delta.magnitude;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var mid = (from + to) * 0.5f;

            var pathRect = CreateRect("PathSegment", _root);
            pathRect.anchorMin = new Vector2(0.5f, 0.5f);
            pathRect.anchorMax = new Vector2(0.5f, 0.5f);
            pathRect.pivot = new Vector2(0.5f, 0.5f);
            pathRect.anchoredPosition = mid;
            pathRect.sizeDelta = new Vector2(distance, PathThickness);
            pathRect.localRotation = Quaternion.Euler(0, 0, angle);

            var img = pathRect.gameObject.AddComponent<Image>();
            img.color = ColorPathLocked;
            img.raycastTarget = false;

            _pathImages.Add(img);
        }

        // ─── Helpers ─────────────────────────────────────────────────

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
