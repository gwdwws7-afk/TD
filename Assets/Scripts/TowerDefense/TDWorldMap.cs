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

        private const float MapWidth = 1400f;
        private const float MapHeight = 700f;
        private const float NodeSize = 52f;
        private const float BossNodeSize = 68f;
        private const float PathThickness = 6f;

        // Intel side panel (shown when a node is clicked).
        private RectTransform _intelPanel;
        private Text _intelTitle;
        private Text _intelBody;
        private Button _deployButton;
        private int _selectedNodeLevel;

        public RectTransform Root => _root;
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>Build the world map as a full-screen overlay.</summary>
        public void BuildFullScreen(Canvas parent)
        {
            _root = CreateRect("WorldMap", parent.transform);
            StretchFullScreen(_root);

            // Dark gradient background.
            var bgImage = _root.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.025f, 0.030f, 0.038f, 0.98f);

            // Try loading the startup background as map texture.
            var bgPath = "Art/Branding/emberline_startup_background";
            var bgTex = Resources.Load<Texture2D>(bgPath);
            if (bgTex != null)
            {
                var bgSprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f));
                var bgRect = CreateRect("MapBackground", _root);
                StretchFullScreen(bgRect);
                var bgArt = bgRect.gameObject.AddComponent<Image>();
                bgArt.sprite = bgSprite;
                bgArt.color = new Color(0.40f, 0.42f, 0.48f, 0.35f);
                bgRect.SetAsFirstSibling();
            }

            // Title at top.
            var titleRect = CreateRect("MapTitle", _root);
            titleRect.anchorMin = new Vector2(0.5f, 0.95f);
            titleRect.anchorMax = new Vector2(0.5f, 0.95f);
            titleRect.sizeDelta = new Vector2(600f, 40f);
            var titleText = CreateText(titleRect, "CAMPAIGN MAP", 24, FontStyle.Bold, new Color(0.96f, 0.58f, 0.24f, 0.95f));
            titleText.alignment = TextAnchor.MiddleCenter;

            // Back button (top-left).
            var backRect = CreateRect("MapBackBtn", _root);
            backRect.anchorMin = new Vector2(0.02f, 0.93f);
            backRect.anchorMax = new Vector2(0.02f, 0.93f);
            backRect.sizeDelta = new Vector2(120f, 36f);
            var backBtn = backRect.gameObject.AddComponent<Button>();
            var backImg = backRect.gameObject.AddComponent<Image>();
            backImg.color = new Color(0.06f, 0.08f, 0.11f, 0.80f);
            var backLabelRect = CreateRect("MapBackLabel", backRect);
            backLabelRect.anchorMin = Vector2.zero; backLabelRect.anchorMax = Vector2.one;
            backLabelRect.offsetMin = Vector2.zero; backLabelRect.offsetMax = Vector2.zero;
            var backLabel = CreateText(backLabelRect, "← BACK", 11, FontStyle.Bold, new Color(0.93f, 0.96f, 0.98f, 1f));
            backLabel.alignment = TextAnchor.MiddleCenter;

            // Map content area (centered, slightly larger than before).
            var mapArea = CreateRect("MapContentArea", _root);
            mapArea.anchorMin = new Vector2(0.5f, 0.5f);
            mapArea.anchorMax = new Vector2(0.5f, 0.5f);
            mapArea.pivot = new Vector2(0.5f, 0.5f);
            mapArea.sizeDelta = new Vector2(MapWidth, MapHeight);

            BuildMapContent(mapArea);
            BuildIntelPanel();

            gameObject.SetActive(true);
            _root.gameObject.SetActive(false);
        }

        private void BuildMapContent(Transform parent)
        {
            // Draw chapter zone backgrounds.
            for (var ch = 0; ch < 4; ch++)
            {
                var zoneRect = CreateRect($"ChapterZone_{ch}", parent);
                zoneRect.anchorMin = new Vector2(0.5f, 0.5f);
                zoneRect.anchorMax = new Vector2(0.5f, 0.5f);
                zoneRect.pivot = new Vector2(0.5f, 0.5f);

                var rowY = GetRowY(ch);
                zoneRect.anchoredPosition = new Vector2(0f, rowY);
                zoneRect.sizeDelta = new Vector2(MapWidth * 0.92f, MapHeight * 0.22f);

                var zoneImg = zoneRect.gameObject.AddComponent<Image>();
                zoneImg.color = ChapterColors[ch];
                zoneImg.raycastTarget = false;
                _chapterZones.Add(zoneRect);

                // Chapter label.
                var chLabelRect = CreateRect($"ChapterLabel_{ch}", zoneRect);
                chLabelRect.anchorMin = new Vector2(0.02f, 0.5f);
                chLabelRect.anchorMax = new Vector2(0.02f, 0.5f);
                chLabelRect.sizeDelta = new Vector2(120f, 20f);
                var chLabel = CreateText(chLabelRect, $"CHAPTER {(char)('A' + ch)}", 11, FontStyle.Bold,
                    new Color(0.82f, 0.78f, 0.60f, 0.50f));
                chLabel.alignment = TextAnchor.MiddleLeft;
            }

            // Draw paths between consecutive nodes.
            for (var i = 0; i < 19; i++)
            {
                DrawPathSegment(parent, NodePositions[i], NodePositions[i + 1]);
            }

            // Draw nodes.
            for (var i = 0; i < 20; i++)
            {
                CreateNode(parent, i);
            }
        }

        private void BuildIntelPanel()
        {
            _intelPanel = CreateRect("MapIntelPanel", _root);
            _intelPanel.anchorMin = new Vector2(0.75f, 0.5f);
            _intelPanel.anchorMax = new Vector2(0.98f, 0.85f);
            _intelPanel.offsetMin = Vector2.zero;
            _intelPanel.offsetMax = Vector2.zero;

            var panelImg = _intelPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(0.04f, 0.06f, 0.08f, 0.92f);

            _intelTitle = CreateText(CreateRect("IntelTitle", _intelPanel), "SELECT A LEVEL", 14,
                FontStyle.Bold, new Color(0.96f, 0.58f, 0.24f, 1f));
            _intelTitle.alignment = TextAnchor.UpperCenter;

            _intelBody = CreateText(CreateRect("IntelBody", _intelPanel), string.Empty, 10,
                FontStyle.Normal, new Color(0.78f, 0.88f, 0.96f, 0.90f));
            _intelBody.alignment = TextAnchor.UpperLeft;

            // Deploy button.
            var deployRect = CreateRect("IntelDeploy", _intelPanel);
            deployRect.anchorMin = new Vector2(0.5f, 0.05f);
            deployRect.anchorMax = new Vector2(0.5f, 0.05f);
            deployRect.sizeDelta = new Vector2(180f, 38f);
            var deployImg = deployRect.gameObject.AddComponent<Image>();
            deployImg.color = new Color(0.96f, 0.58f, 0.24f, 0.20f);
            _deployButton = deployRect.gameObject.AddComponent<Button>();
            var deployColors = _deployButton.colors;
            deployColors.highlightedColor = new Color(0.96f, 0.58f, 0.24f, 0.40f);
            _deployButton.colors = deployColors;
            var deployLabelRect = CreateRect("IntelDeployLabel", deployRect);
            deployLabelRect.anchorMin = Vector2.zero; deployLabelRect.anchorMax = Vector2.one;
            deployLabelRect.offsetMin = Vector2.zero; deployLabelRect.offsetMax = Vector2.zero;
            var deployLabel = CreateText(deployLabelRect, "DEPLOY", 13, FontStyle.Bold, new Color(0.96f, 0.58f, 0.24f, 1f));
            deployLabel.alignment = TextAnchor.MiddleCenter;
            _deployButton.interactable = false;

            _intelPanel.gameObject.SetActive(false);
        }

        public void ShowIntel(string title, string body, bool canDeploy)
        {
            if (_intelPanel == null) return;
            _intelPanel.gameObject.SetActive(true);
            _intelTitle.text = title;
            _intelBody.text = body;
            _deployButton.interactable = canDeploy;
        }

        public void HideIntel()
        {
            if (_intelPanel != null) _intelPanel.gameObject.SetActive(false);
        }

        public void Show() { if (_root != null) _root.gameObject.SetActive(true); }
        public new void Hide() { if (_root != null) _root.gameObject.SetActive(false); }

        public Button DeployButton => _deployButton;
        public Button BackButton => _root?.Find("MapBackBtn")?.GetComponent<Button>();
        public int SelectedNodeLevel => _selectedNodeLevel;

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

        private void CreateNode(Transform parent, int index)
        {
            var pos = NodePositions[index];
            var nodeRect = CreateRect($"Node_L{index + 1:00}", parent);
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
            btn.onClick.AddListener(() =>
            {
                _selectedNodeLevel = capturedIndex + 1;
                OnNodeClicked?.Invoke(capturedIndex + 1);
            });

            // Label on a child (can't share GameObject with Image).
            var labelRect = CreateRect($"NodeLabel_L{index + 1:00}", nodeRect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelRect.gameObject.AddComponent<Text>();
            label.font = TDLocalization.ResolveFont(null) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = $"L{index + 1:00}";
            label.fontSize = 10;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = ColorTextBright;
            label.raycastTarget = false;

            _nodes.Add(nodeRect);
            _nodeImages.Add(img);
            _nodeLabels.Add(label);
        }

        private void DrawPathSegment(Transform parent, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var distance = delta.magnitude;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var mid = (from + to) * 0.5f;

            var pathRect = CreateRect("PathSegment", parent);
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

        private static void StretchFullScreen(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text CreateText(RectTransform parent, string content, int fontSize, FontStyle style, Color color)
        {
            var text = parent.gameObject.AddComponent<Text>();
            text.font = TDLocalization.ResolveFont(null) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
