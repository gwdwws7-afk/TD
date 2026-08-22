using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Campaign world map: 20 levels anchored to the five painted terrain
    /// regions of Art/UI/Campaign/world_map_bg. Each node is a composition
    /// of a per-level landmark, a state badge, three difficulty seals and a
    /// selected highlight ring (campaign-worldmap-art-spec-v2.md).
    ///
    /// Falls back to the legacy procedural layout (solid zones + ring
    /// nodes) whenever the campaign art pack is missing.
    /// Node buttons stay in level order (index 0 = L01) for the mission
    /// board focus and the p8 UI audit probe.
    /// </summary>
    public sealed class TDWorldMap : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _mapArea;
        private readonly List<RectTransform> _nodes = new();
        private readonly List<Image> _nodeImages = new();   // state badge (art mode) or node body (legacy)
        private readonly List<Text> _nodeLabels = new();
        private readonly List<Button> _nodeButtons = new();
        private readonly List<Image> _pathImages = new();
        private readonly List<RectTransform> _chapterZones = new();
        private readonly List<Image> _nodeLandmarks = new();
        private readonly List<Image> _nodeRings = new();
        private readonly List<Image[]> _nodeSeals = new();

        public System.Action<int> OnNodeClicked;

        // Art-mode anchor table: one row per painted region, following the
        // verified world_map_bg layout (top: junction/depot/canyon left to
        // right, bottom: kiln basin left, terminus right). Organic zigzag
        // inside each region box; map coords, 0,0 = center of map area.
        private static readonly Vector2[] NodePositions = GenerateNodePositions();
        private static readonly Vector2[] ArtNodePositions = GenerateArtNodePositions();
        private static readonly Vector2[] RegionPlatePositions =
        {
            new(-430f, 285f), new(55f, 290f), new(475f, 290f),
            new(-315f, -278f), new(475f, -278f),
        };
        private static readonly string[] RegionNames =
        {
            "GRAYLINE JUNCTION", "ASHFALL DEPOT", "SPLIT SWITCH CANYON",
            "HOLLOW KILN BASIN", "LAST EMBER TERMINUS",
        };

        // Chapter zone colors (legacy fallback tints).
        private static readonly Color[] ChapterColors =
        {
            new(0.20f, 0.30f, 0.45f, 0.15f), // A: blue-gray
            new(0.40f, 0.25f, 0.15f, 0.15f), // B: ember-brown
            new(0.15f, 0.30f, 0.35f, 0.15f), // C: teal-dark
            new(0.35f, 0.15f, 0.20f, 0.15f), // D: deep red
        };

        // Node state colors (legacy mode + path/seal tints).
        private static readonly Color ColorLocked = new(0.15f, 0.16f, 0.18f, 0.85f);
        private static readonly Color ColorAvailable = new(0.96f, 0.58f, 0.24f, 0.95f);
        private static readonly Color ColorCleared = new(0.26f, 0.74f, 0.52f, 0.90f);
        private static readonly Color ColorBoss = new(0.92f, 0.28f, 0.22f, 0.95f);
        private static readonly Color ColorSelected = new(0.98f, 0.88f, 0.32f, 1f);
        private static readonly Color ColorTextDark = new(0.04f, 0.05f, 0.07f, 0.95f);
        private static readonly Color ColorTextBright = new(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color ColorPathLocked = new(0.12f, 0.13f, 0.15f, 0.55f);
        private static readonly Color ColorPathCleared = new(1f, 0.72f, 0.30f, 0.85f);
        private static readonly Color ColorSealLit = new(1f, 0.78f, 0.35f, 1f);

        private const float MapWidth = 1400f;
        private const float MapHeight = 700f;
        private const float NodeSize = 52f;
        private const float BossNodeSize = 68f;
        private const float PathThickness = 6f;

        // Art-mode sizes.
        private const float LandmarkSize = 86f;
        private const float BossLandmarkSize = 104f;
        private const float BadgeSize = 36f;
        private const float RingSize = 108f;
        private const float SealSize = 13f;
        private const float NodeHitSize = 92f;

        // Art pack sprites (null-safe: art mode activates only when the
        // world map background resolves).
        private Sprite _worldMapBg;
        private Sprite _landmarkFallback;
        private Sprite _badgeAvailable;
        private Sprite _badgeCleared;
        private Sprite _badgeLocked;
        private Sprite _badgeBoss;
        private Sprite _ringSelected;
        private Sprite _sealPip;
        private Sprite _sealEmpty;
        private Sprite _regionPlate;
        private Sprite _railStrip;
        private Sprite _metaEntry;
        private Sprite _metaPanel;
        private Sprite _titlePlate;
        private bool _artMode;
        private float _ringPulse;

        // Intel side panel (shown when a node is clicked).
        private RectTransform _intelPanel;
        private Text _intelTitle;
        private Text _intelBody;
        private Button _deployButton;
        private int _selectedNodeLevel;

        // Meta upgrade entry + stub panel (system pending - design spec
        // meta-upgrade-system-spec-v1.md; TDMetaUpgradePanel will replace
        // the stub body once the system lands).
        private RectTransform _metaPanelRoot;
        private Button _metaEntryButton;

        public RectTransform Root => _root;
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>
        /// Node buttons in level order (index 0 = L01). Consumed by the game
        /// manager's mission-board selection focus and the p8 UI audit probe.
        /// </summary>
        public IReadOnlyList<Button> NodeButtons => _nodeButtons;

        public Button MetaEntryButton => _metaEntryButton;

        /// <summary>Build the world map as a full-screen overlay.</summary>
        public void BuildFullScreen(Canvas parent)
        {
            LoadCampaignArt();

            _root = CreateRect("WorldMap", parent.transform);
            StretchFullScreen(_root);

            // Base dark layer (keeps letterboxing consistent behind the art).
            var bgImage = _root.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.025f, 0.030f, 0.038f, 0.98f);

            if (_artMode && _worldMapBg != null)
            {
                var bgRect = CreateRect("MapBackground", _root);
                StretchFullScreen(bgRect);
                var bgArt = bgRect.gameObject.AddComponent<Image>();
                bgArt.sprite = _worldMapBg;
                bgArt.color = new Color(1f, 1f, 1f, 0.96f);
                bgArt.raycastTarget = false;
                bgRect.SetAsFirstSibling();
            }
            else
            {
                // Legacy: dimmed startup background.
                var bgTex = Resources.Load<Texture2D>("Art/Branding/emberline_startup_background");
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
            }

            BuildTitle();
            BuildBackButton();

            // Map content area (centered).
            var mapArea = CreateRect("MapContentArea", _root);
            mapArea.anchorMin = new Vector2(0.5f, 0.5f);
            mapArea.anchorMax = new Vector2(0.5f, 0.5f);
            mapArea.pivot = new Vector2(0.5f, 0.5f);
            mapArea.sizeDelta = new Vector2(MapWidth, MapHeight);
            _mapArea = mapArea;

            BuildMapContent(mapArea);
            BuildIntelPanel();
            BuildMetaEntry();
            ApplyMapScale();

            gameObject.SetActive(true);
            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The 1400x700 logical layout is authored against the reference
        /// resolution; the UI-scale setting shrinks the logical canvas (up to
        /// 1200x750 at 1.2x) and the edge nodes clipped ~70px on both sides
        /// (review P1). Uniform down-scale keeps the whole journey readable
        /// instead; re-applied on Refresh so opening after a scale change
        /// re-fits.
        /// </summary>
        private void ApplyMapScale()
        {
            if (_mapArea == null || _root == null)
            {
                return;
            }

            var available = _root.rect;
            var margin = 40f;
            var scale = Mathf.Min(
                1f,
                Mathf.Max(0.5f, (available.width - margin) / MapWidth),
                Mathf.Max(0.5f, (available.height - margin) / MapHeight));
            _mapArea.localScale = Vector3.one * scale;
        }

        private void LoadCampaignArt()
        {
            const string dir = "Art/UI/Campaign/";
            _worldMapBg = Resources.Load<Sprite>(dir + "world_map_bg");
            _artMode = _worldMapBg != null;
            if (!_artMode) return;

            _badgeAvailable = Resources.Load<Sprite>(dir + "node_available");
            _badgeCleared = Resources.Load<Sprite>(dir + "node_cleared");
            _badgeLocked = Resources.Load<Sprite>(dir + "node_locked");
            _badgeBoss = Resources.Load<Sprite>(dir + "node_boss");
            _ringSelected = Resources.Load<Sprite>(dir + "node_selected");
            _sealPip = Resources.Load<Sprite>(dir + "seal_pip");
            _sealEmpty = Resources.Load<Sprite>(dir + "seal_pip_empty");
            _regionPlate = Resources.Load<Sprite>(dir + "region_plate");
            _railStrip = Resources.Load<Sprite>(dir + "path_rail_strip");
            _metaEntry = Resources.Load<Sprite>(dir + "meta_entry_button");
            _metaPanel = Resources.Load<Sprite>(dir + "meta_panel_frame");
            _titlePlate = Resources.Load<Sprite>(dir + "campaign_title_plate");
        }

        private void BuildTitle()
        {
            var titleRect = CreateRect("MapTitle", _root);
            titleRect.anchorMin = new Vector2(0.5f, 0.95f);
            titleRect.anchorMax = new Vector2(0.5f, 0.95f);
            titleRect.sizeDelta = new Vector2(600f, 40f);

            if (_artMode && _titlePlate != null)
            {
                var plateRect = CreateRect("MapTitlePlate", titleRect);
                plateRect.anchorMin = Vector2.zero;
                plateRect.anchorMax = Vector2.one;
                plateRect.offsetMin = new Vector2(-60f, -14f);
                plateRect.offsetMax = new Vector2(60f, 14f);
                var plate = plateRect.gameObject.AddComponent<Image>();
                plate.sprite = _titlePlate;
                plate.raycastTarget = false;
            }

            var titleText = CreateText(titleRect, "CAMPAIGN MAP", _artMode ? 22 : 24, FontStyle.Bold,
                new Color(0.98f, 0.86f, 0.55f, 0.98f));
            titleText.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildBackButton()
        {
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
        }

        private void BuildMapContent(Transform parent)
        {
            var positions = _artMode ? ArtNodePositions : NodePositions;

            if (_artMode)
            {
                // Region nameplates replace the solid chapter zones; the
                // painted terrain is the zoning.
                for (var r = 0; r < 5; r++)
                {
                    var plateRect = CreateRect($"RegionPlate_{r}", parent);
                    plateRect.anchorMin = new Vector2(0.5f, 0.5f);
                    plateRect.anchorMax = new Vector2(0.5f, 0.5f);
                    plateRect.anchoredPosition = RegionPlatePositions[r];
                    plateRect.sizeDelta = new Vector2(330f, 38f);
                    if (_regionPlate != null)
                    {
                        var plateImg = plateRect.gameObject.AddComponent<Image>();
                        plateImg.sprite = _regionPlate;
                        plateImg.color = new Color(1f, 1f, 1f, 0.92f);
                        plateImg.raycastTarget = false;
                    }
                    else
                    {
                        var zoneImg = plateRect.gameObject.AddComponent<Image>();
                        zoneImg.color = ChapterColors[Mathf.Min(r, 3)];
                        zoneImg.raycastTarget = false;
                    }
                    var plateLabelRect = CreateRect($"RegionLabel_{r}", plateRect);
                    plateLabelRect.anchorMin = Vector2.zero; plateLabelRect.anchorMax = Vector2.one;
                    plateLabelRect.offsetMin = Vector2.zero; plateLabelRect.offsetMax = Vector2.zero;
                    var plateLabel = CreateText(plateLabelRect, RegionNames[r], 10, FontStyle.Bold,
                        new Color(0.95f, 0.88f, 0.70f, 0.95f));
                    plateLabel.alignment = TextAnchor.MiddleCenter;
                }
            }
            else
            {
                for (var ch = 0; ch < 4; ch++)
                {
                    var zoneRect = CreateRect($"ChapterZone_{ch}", parent);
                    zoneRect.anchorMin = new Vector2(0.5f, 0.5f);
                    zoneRect.anchorMax = new Vector2(0.5f, 0.5f);
                    zoneRect.pivot = new Vector2(0.5f, 0.5f);
                    zoneRect.anchoredPosition = new Vector2(0f, GetRowY(ch));
                    zoneRect.sizeDelta = new Vector2(MapWidth * 0.92f, MapHeight * 0.22f);
                    var zoneImg = zoneRect.gameObject.AddComponent<Image>();
                    zoneImg.color = ChapterColors[ch];
                    zoneImg.raycastTarget = false;
                    _chapterZones.Add(zoneRect);

                    var chLabelRect = CreateRect($"ChapterLabel_{ch}", zoneRect);
                    chLabelRect.anchorMin = new Vector2(0.02f, 0.5f);
                    chLabelRect.anchorMax = new Vector2(0.02f, 0.5f);
                    chLabelRect.sizeDelta = new Vector2(120f, 20f);
                    var chLabel = CreateText(chLabelRect, $"CHAPTER {(char)('A' + ch)}", 11, FontStyle.Bold,
                        new Color(0.82f, 0.78f, 0.60f, 0.50f));
                    chLabel.alignment = TextAnchor.MiddleLeft;
                }
            }

            // Journey rails between consecutive levels.
            for (var i = 0; i < 19; i++)
            {
                DrawPathSegment(parent, positions[i], positions[i + 1]);
            }

            for (var i = 0; i < 20; i++)
            {
                CreateNode(parent, i, positions);
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

        private void BuildMetaEntry()
        {
            // Persistent entry bottom-right (DEPLOY side). Opens a stub
            // panel framed by meta_panel_frame; the upgrade lines and
            // residue currency are wired by the meta upgrade system.
            if (!_artMode || _metaEntry == null) return;

            var entryRect = CreateRect("MetaEntryBtn", _root);
            entryRect.anchorMin = new Vector2(0.965f, 0.06f);
            entryRect.anchorMax = new Vector2(0.965f, 0.06f);
            entryRect.sizeDelta = new Vector2(64f, 64f);
            var entryImg = entryRect.gameObject.AddComponent<Image>();
            entryImg.sprite = _metaEntry;
            _metaEntryButton = entryRect.gameObject.AddComponent<Button>();
            var entryColors = _metaEntryButton.colors;
            entryColors.highlightedColor = new Color(1f, 0.85f, 0.4f, 0.9f);
            entryColors.pressedColor = new Color(0.8f, 0.65f, 0.3f, 1f);
            _metaEntryButton.colors = entryColors;
            _metaEntryButton.onClick.AddListener(ShowMetaPanelStub);
        }

        private void ShowMetaPanelStub()
        {
            if (_metaPanelRoot != null) { _metaPanelRoot.gameObject.SetActive(true); return; }

            _metaPanelRoot = CreateRect("MetaPanel", _root);
            _metaPanelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _metaPanelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _metaPanelRoot.sizeDelta = new Vector2(880f, 560f);

            var frame = _metaPanelRoot.gameObject.AddComponent<Image>();
            frame.sprite = _metaPanel;
            frame.color = new Color(1f, 1f, 1f, 0.98f);

            var titleRect = CreateRect("MetaTitle", _metaPanelRoot);
            titleRect.anchorMin = new Vector2(0.5f, 0.93f);
            titleRect.anchorMax = new Vector2(0.5f, 0.93f);
            titleRect.sizeDelta = new Vector2(420f, 34f);
            var title = CreateText(titleRect, "EMBER RESONANCE", 16, FontStyle.Bold,
                new Color(0.98f, 0.86f, 0.55f, 1f));
            title.alignment = TextAnchor.MiddleCenter;

            var bodyRect = CreateRect("MetaBody", _metaPanelRoot);
            bodyRect.anchorMin = new Vector2(0.12f, 0.30f);
            bodyRect.anchorMax = new Vector2(0.88f, 0.80f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
            var body = CreateText(bodyRect, "UPGRADE LATTICE OFFLINE\n\nAwaiting the resonance engineers.",
                12, FontStyle.Normal, new Color(0.85f, 0.88f, 0.92f, 0.95f));
            body.alignment = TextAnchor.MiddleCenter;

            var closeRect = CreateRect("MetaClose", _metaPanelRoot);
            closeRect.anchorMin = new Vector2(0.5f, 0.08f);
            closeRect.anchorMax = new Vector2(0.5f, 0.08f);
            closeRect.sizeDelta = new Vector2(150f, 34f);
            var closeBtn = closeRect.gameObject.AddComponent<Button>();
            var closeImg = closeRect.gameObject.AddComponent<Image>();
            closeImg.color = new Color(0.10f, 0.12f, 0.15f, 0.85f);
            var closeLabelRect = CreateRect("MetaCloseLabel", closeRect);
            closeLabelRect.anchorMin = Vector2.zero; closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero; closeLabelRect.offsetMax = Vector2.zero;
            var closeLabel = CreateText(closeLabelRect, "CLOSE", 12, FontStyle.Bold, ColorTextBright);
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeBtn.onClick.AddListener(() => _metaPanelRoot.gameObject.SetActive(false));
        }

        public void ShowIntel(string title, string body, bool canDeploy)
        {
            if (_intelPanel == null) return;
            _intelPanel.gameObject.SetActive(true);
            TDLocalization.SetLabel(_intelTitle, title);
            TDLocalization.SetLabel(_intelBody, body);
            _deployButton.interactable = canDeploy;
        }

        public void HideIntel()
        {
            if (_intelPanel != null) _intelPanel.gameObject.SetActive(false);
        }

        public void Show() { if (_root != null) _root.gameObject.SetActive(true); }
        public void Hide() { if (_root != null) _root.gameObject.SetActive(false); }

        public Button DeployButton => _deployButton;
        public Button BackButton => _root?.Find("MapBackBtn")?.GetComponent<Button>();
        public int SelectedNodeLevel => _selectedNodeLevel;

        /// <summary>
        /// Update all node visuals based on the current campaign state.
        /// </summary>
        /// <param name="starsPerLevel">Best performance stars — legacy
        /// layout's star display only.</param>
        /// <param name="difficultyPerLevel">highestDifficultyCleared tier
        /// (0=Standard/1=Veteran/2=EmberTrial) — drives the art-mode
        /// difficulty seals (spec: campaign-worldmap-art-spec-v2).</param>
        public void Refresh(
            int selectedLevel,
            int highestUnlocked,
            bool[] clearedLevels,
            int[] starsPerLevel,
            int totalLevels,
            int bossLevel,
            int[] difficultyPerLevel = null)
        {
            for (var i = 0; i < _nodes.Count && i < totalLevels; i++)
            {
                var levelIndex = i + 1; // 1-based
                var isBoss = levelIndex == bossLevel;
                var isLocked = levelIndex > highestUnlocked;
                var isCleared = i < clearedLevels.Length && clearedLevels[i];
                var isSelected = levelIndex == selectedLevel;

                if (_artMode)
                {
                    // Seal count = tiers cleared THROUGH, so Standard clear
                    // lights one seal, EmberTrial lights all three.
                    var tierCleared = isCleared && difficultyPerLevel != null && i < difficultyPerLevel.Length
                        ? difficultyPerLevel[i]
                        : -1;
                    RefreshNodeArt(i, levelIndex, isBoss, isLocked, isCleared, isSelected, tierCleared);
                }
                else
                {
                    RefreshNodeLegacy(i, levelIndex, isBoss, isLocked, isCleared, isSelected,
                        starsPerLevel, i);
                }

                var button = _nodes[i].GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !isLocked;
                }
            }

            // Journey rails: cleared segments glow warm, locked stay dim.
            for (var i = 0; i < _pathImages.Count && i < 19; i++)
            {
                var fromCleared = i < clearedLevels.Length && clearedLevels[i];
                _pathImages[i].color = fromCleared ? ColorPathCleared : ColorPathLocked;
            }

            // Re-fit after possible UI-scale changes since the last open.
            ApplyMapScale();
        }

        private void RefreshNodeArt(int i, int levelIndex, bool isBoss, bool isLocked, bool isCleared,
            bool isSelected, int tierCleared)
        {
            var landmark = _nodeLandmarks[i];
            var badge = _nodeImages[i];

            landmark.sprite = ResolveLandmark(levelIndex);
            if (landmark.sprite == null)
            {
                // Missing landmark art: fall back to the badge as the node
                // body instead of rendering an untinted white quad.
                landmark.gameObject.SetActive(false);
            }
            else
            {
                landmark.gameObject.SetActive(true);
                if (landmark.transform is RectTransform landmarkRect)
                {
                    landmarkRect.sizeDelta = Vector2.one * (isBoss ? BossLandmarkSize : LandmarkSize);
                }
            }
            landmark.color = isLocked
                ? new Color(0.42f, 0.44f, 0.48f, 0.66f)
                : (isCleared ? new Color(0.82f, 0.87f, 0.84f, 0.96f) : Color.white);

            badge.sprite = isBoss && !isCleared ? _badgeBoss
                : isCleared ? _badgeCleared
                : isLocked ? _badgeLocked
                : _badgeAvailable;
            badge.color = Color.white;

            if (_nodeRings[i] != null)
            {
                _nodeRings[i].gameObject.SetActive(isSelected && _ringSelected != null);
            }

            var sealSet = _nodeSeals[i];
            if (sealSet != null)
            {
                for (var s = 0; s < sealSet.Length; s++)
                {
                    if (sealSet[s] == null) continue;
                    // tierCleared is the highest DIFFICULTY tier cleared
                    // (0/1/2); seal s lights when that tier covers it.
                    var lit = !isLocked && s <= tierCleared;
                    sealSet[s].sprite = lit ? _sealPip : _sealEmpty;
                    sealSet[s].color = lit ? ColorSealLit : Color.white;
                }
            }

            if (_nodeLabels[i] != null)
            {
                TDLocalization.SetLabel(_nodeLabels[i], isLocked ? "?" : $"L{levelIndex:00}");
                _nodeLabels[i].fontSize = isBoss ? 10 : 9;
                _nodeLabels[i].color = isLocked
                    ? new Color(0.55f, 0.57f, 0.60f, 0.9f)
                    : ColorTextBright;
            }
        }

        private void RefreshNodeLegacy(int i, int levelIndex, bool isBoss, bool isLocked, bool isCleared,
            bool isSelected, int[] starsPerLevel, int idx)
        {
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
                nodeColor = isSelected ? ColorSelected : ColorAvailable;
                textColor = ColorTextDark;
                size = NodeSize;
            }

            _nodeImages[i].color = nodeColor;
            _nodes[i].sizeDelta = Vector2.one * size;

            if (_nodeLabels[i] != null)
            {
                var label = isLocked ? "🔒" : $"L{levelIndex:00}";
                if (isCleared && !isBoss && idx < starsPerLevel.Length)
                {
                    var stars = starsPerLevel[idx];
                    label = $"L{levelIndex:00}\n{"★".PadRight(stars + 1).Substring(0, Mathf.Max(1, stars))}";
                }
                else if (isBoss)
                {
                    // Boss level number follows the parameter, not a literal.
                    label = isCleared ? $"L{levelIndex:00}\n★" : $"L{levelIndex:00}\nBOSS";
                }
                TDLocalization.SetLabel(_nodeLabels[i], label);
                _nodeLabels[i].fontSize = isBoss ? 9 : 8;
                _nodeLabels[i].color = textColor;
            }
        }

        private Sprite ResolveLandmark(int levelIndex)
        {
            var landmark = Resources.Load<Sprite>($"Art/UI/Campaign/landmark_L{levelIndex:00}");
            return landmark != null ? landmark : _landmarkFallback;
        }

        private void Update()
        {
            // Selected-ring breathing.
            if (!_artMode || _ringSelected == null) return;
            _ringPulse += Time.unscaledDeltaTime * 2.2f;
            var scale = 1f + 0.045f * (0.5f + 0.5f * Mathf.Sin(_ringPulse));
            for (var i = 0; i < _nodeRings.Count; i++)
            {
                var ring = _nodeRings[i];
                if (ring != null && ring.gameObject.activeSelf)
                {
                    ring.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        // ─── Layout ──────────────────────────────────────────────────

        private static Vector2[] GenerateNodePositions()
        {
            var positions = new Vector2[20];
            for (var i = 0; i < 20; i++)
            {
                var chapter = i / 5;
                var col = i % 5;
                var rowY = GetRowY(chapter);
                float x;
                if (chapter % 2 == 0)
                {
                    x = -MapWidth * 0.40f + col * (MapWidth * 0.20f);
                }
                else
                {
                    x = MapWidth * 0.40f - col * (MapWidth * 0.20f);
                }
                positions[i] = new Vector2(x, rowY);
            }
            return positions;
        }

        private static Vector2[] GenerateArtNodePositions()
        {
            // Anchored to the painted regions of world_map_bg: top band
            // left-to-right (junction -> depot -> canyon), bottom band
            // kiln basin left, terminus right. Zigzag inside each region.
            return new Vector2[]
            {
                // Region 1: grayline junction (top-left).
                new(-580f, 220f), new(-470f, 140f), new(-360f, 225f), new(-265f, 135f),
                // Region 2: ashfall depot (top-center).
                new(-90f, 230f), new(20f, 150f), new(130f, 225f), new(225f, 140f),
                // Region 3: split switch canyon (top-right).
                new(330f, 235f), new(445f, 150f), new(555f, 225f), new(625f, 140f),
                // Region 4: hollow kiln basin (bottom-left).
                new(-480f, -125f), new(-370f, -200f), new(-260f, -120f), new(-155f, -195f),
                // Region 5: last ember terminus (bottom-right).
                new(330f, -130f), new(440f, -205f), new(550f, -120f), new(625f, -195f),
            };
        }

        private static float GetRowY(int chapter)
        {
            return MapHeight * 0.35f - chapter * (MapHeight * 0.24f);
        }

        // ─── Node/Path creation ──────────────────────────────────────

        private void CreateNode(Transform parent, int index, Vector2[] positions)
        {
            if (_artMode)
            {
                CreateNodeArt(parent, index, positions);
                return;
            }

            var pos = positions[index];
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
            StyleNodeButton(btn);

            var capturedIndex = index;
            btn.onClick.AddListener(() =>
            {
                _selectedNodeLevel = capturedIndex + 1;
                OnNodeClicked?.Invoke(capturedIndex + 1);
            });

            var label = CreateNodeLabel(nodeRect, index, $"L{index + 1:00}", 10, ColorTextBright);

            _nodes.Add(nodeRect);
            _nodeImages.Add(img);
            _nodeLabels.Add(label);
            _nodeButtons.Add(btn);
            _nodeLandmarks.Add(null);
            _nodeRings.Add(null);
            _nodeSeals.Add(null);
        }

        private void CreateNodeArt(Transform parent, int index, Vector2[] positions)
        {
            var pos = positions[index];
            var isBossSlot = index == 19; // sized pre-Refresh; Refresh corrects via sprites

            var nodeRect = CreateRect($"Node_L{index + 1:00}", parent);
            nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
            nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
            nodeRect.pivot = new Vector2(0.5f, 0.5f);
            nodeRect.anchoredPosition = pos;
            nodeRect.sizeDelta = Vector2.one * NodeHitSize;

            // Hit target + button on the root (transparent image so the
            // button still receives clicks over the landmark).
            var hit = nodeRect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var btn = nodeRect.gameObject.AddComponent<Button>();
            StyleNodeButton(btn);
            var capturedIndex = index;
            btn.onClick.AddListener(() =>
            {
                _selectedNodeLevel = capturedIndex + 1;
                OnNodeClicked?.Invoke(capturedIndex + 1);
            });

            var landmarkSize = isBossSlot ? BossLandmarkSize : LandmarkSize;
            var landmarkRect = CreateRect($"NodeLandmark_L{index + 1:00}", nodeRect);
            landmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            landmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            landmarkRect.sizeDelta = Vector2.one * landmarkSize;
            var landmark = landmarkRect.gameObject.AddComponent<Image>();
            landmark.sprite = ResolveLandmark(index + 1);
            landmark.raycastTarget = false;
            _nodeLandmarks.Add(landmark);

            // Selected highlight ring (drawn above the landmark).
            Image ring = null;
            if (_ringSelected != null)
            {
                var ringRect = CreateRect($"NodeRing_L{index + 1:00}", nodeRect);
                ringRect.anchorMin = new Vector2(0.5f, 0.5f);
                ringRect.anchorMax = new Vector2(0.5f, 0.5f);
                ringRect.sizeDelta = Vector2.one * RingSize;
                ring = ringRect.gameObject.AddComponent<Image>();
                ring.sprite = _ringSelected;
                ring.raycastTarget = false;
                ringRect.gameObject.SetActive(false);
            }
            _nodeRings.Add(ring);

            // State badge pinned to the landmark's lower-right.
            var badgeRect = CreateRect($"NodeBadge_L{index + 1:00}", nodeRect);
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(26f, -24f);
            badgeRect.sizeDelta = Vector2.one * BadgeSize;
            var badge = badgeRect.gameObject.AddComponent<Image>();
            badge.sprite = _badgeLocked;
            badge.raycastTarget = false;
            _nodeImages.Add(badge);

            // Level number rides the badge center.
            var label = CreateNodeLabel(badgeRect, index, $"L{index + 1:00}", 9, ColorTextBright);
            _nodeLabels.Add(label);

            // Three difficulty seals under the landmark.
            var seals = new Image[3];
            for (var s = 0; s < 3; s++)
            {
                var sealRect = CreateRect($"NodeSeal{s}_L{index + 1:00}", nodeRect);
                sealRect.anchorMin = new Vector2(0.5f, 0.5f);
                sealRect.anchorMax = new Vector2(0.5f, 0.5f);
                sealRect.anchoredPosition = new Vector2((s - 1) * 20f, -(landmarkSize * 0.5f + 12f));
                sealRect.sizeDelta = Vector2.one * SealSize;
                var seal = sealRect.gameObject.AddComponent<Image>();
                seal.sprite = _sealEmpty;
                seal.raycastTarget = false;
                seals[s] = seal;
            }
            _nodeSeals.Add(seals);

            _nodes.Add(nodeRect);
            _nodeButtons.Add(btn);
        }

        private static void StyleNodeButton(Button btn)
        {
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.3f, 0.30f);
            colors.pressedColor = new Color(1f, 0.85f, 0.3f, 0.50f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            btn.colors = colors;
        }

        private Text CreateNodeLabel(RectTransform parent, int index, string text, int size, Color color)
        {
            var labelRect = CreateRect($"NodeLabel_L{index + 1:00}", parent);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelRect.gameObject.AddComponent<Text>();
            label.font = TDLocalization.ResolveFont(null) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Route through the localization pipeline so the label registers
            // a source string and translates / re-translates on language
            // switches (review P1: world-map texts bypassed it entirely).
            TDLocalization.SetLabel(label, text);
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            return label;
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
            pathRect.sizeDelta = new Vector2(distance, _artMode && _railStrip != null ? 18f : PathThickness);
            pathRect.localRotation = Quaternion.Euler(0, 0, angle);

            var img = pathRect.gameObject.AddComponent<Image>();
            if (_artMode && _railStrip != null)
            {
                img.sprite = _railStrip;
            }
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
            // Localization pipeline (review P1): registers the English source
            // and translates/re-translates on language switches.
            TDLocalization.SetLabel(text, content);
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
