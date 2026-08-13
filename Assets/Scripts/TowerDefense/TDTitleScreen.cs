using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Title screen / main menu overlay shown on game launch.
    /// Renders on top of the battle canvas, fades to reveal the mission board
    /// when the player chooses New Game or Continue.
    ///
    /// Flow:
    ///   Splash → Title Screen → [New/Continue] → Mission Board → Deploy → Battle
    ///                     ↘ [Settings] → Settings Panel
    ///                     ↘ [Credits] → Credits overlay
    ///                     ↘ [Quit] → Exit (standalone only)
    /// </summary>
    public sealed class TDTitleScreen : MonoBehaviour
    {
        private CanvasGroup _fader;
        private RectTransform _root;
        private RectTransform _creditsOverlay;

        // Callbacks set by TDGameManager
        public System.Action OnNewGame;
        public System.Action OnNewGamePlus;
        public System.Action OnContinue;
        public System.Action OnOpenSettings;

        /// <summary>True while the title screen is covering the game.</summary>
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        private static readonly Color PanelBg = new(0.025f, 0.030f, 0.038f, 0.96f);
        private static readonly Color AccentEmber = new(0.96f, 0.58f, 0.24f, 1f);
        private static readonly Color TextBright = new(0.93f, 0.96f, 0.98f, 1f);
        private static readonly Color TextDim = new(0.62f, 0.70f, 0.78f, 0.80f);
        private static readonly Color ButtonHover = new(0.12f, 0.14f, 0.18f, 0.92f);

        public void Build(Canvas parent, bool hasExistingProgress, bool hasClearedCampaign = false)
        {
            // Root panel — full screen
            _root = CreateRect("TitleScreen", parent.transform);
            StretchFullScreen(_root);

            var rootImage = _root.gameObject.AddComponent<Image>();
            rootImage.color = PanelBg;
            rootImage.raycastTarget = true;

            _fader = _root.gameObject.AddComponent<CanvasGroup>();
            _fader.alpha = 1f;
            _fader.blocksRaycasts = true;

            // Background image (startup background branding art)
            var bgPath = "Art/Branding/emberline_startup_background";
            var bgSprite = Resources.Load<Sprite>(bgPath) ?? Resources.Load<Texture2D>(bgPath) as object as Sprite;
            if (bgSprite == null)
            {
                // Try loading as texture and converting
                var tex = Resources.Load<Texture2D>(bgPath);
                if (tex != null)
                {
                    bgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            if (bgSprite != null)
            {
                var bgRect = CreateRect("TitleBackground", _root);
                StretchFullScreen(bgRect);
                var bgImage = bgRect.gameObject.AddComponent<Image>();
                bgImage.sprite = bgSprite;
                bgImage.color = new Color(0.52f, 0.50f, 0.54f, 0.50f); // semi-visible background
                bgRect.SetAsFirstSibling();
            }
            else
            {
                // No background art — use a dark gradient feel with vignette overlay.
                var vbgRect = CreateRect("TitleVignette", _root);
                StretchFullScreen(vbgRect);
                var vbgImage = vbgRect.gameObject.AddComponent<Image>();
                vbgImage.color = new Color(0.02f, 0.025f, 0.035f, 0.80f);
                vbgRect.SetAsFirstSibling();
            }

            // Title shadow (dark offset behind main title for depth).
            var shadowRect = CreateRect("TitleShadow", _root);
            shadowRect.anchorMin = new Vector2(0.5f, 0.62f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.62f);
            shadowRect.sizeDelta = new Vector2(720f, 80f);
            shadowRect.anchoredPosition = new Vector2(3f, -3f);
            var shadowText = CreateText(shadowRect, "EMBERLINE DEFENSE", 42, FontStyle.Bold, new Color(0f, 0f, 0f, 0.55f));
            shadowText.alignment = TextAnchor.MiddleCenter;

            // Title text — centered, upper third, larger with outline feel.
            var titleRect = CreateRect("TitleLabel", _root);
            titleRect.anchorMin = new Vector2(0.5f, 0.62f);
            titleRect.anchorMax = new Vector2(0.5f, 0.62f);
            titleRect.sizeDelta = new Vector2(720f, 80f);
            var titleText = CreateText(titleRect, "EMBERLINE DEFENSE", 42, FontStyle.Bold, AccentEmber);
            titleText.alignment = TextAnchor.MiddleCenter;

            // Subtitle
            var subRect = CreateRect("TitleSubtitle", _root);
            subRect.anchorMin = new Vector2(0.5f, 0.56f);
            subRect.anchorMax = new Vector2(0.5f, 0.56f);
            subRect.sizeDelta = new Vector2(500f, 22f);
            var subText = CreateText(subRect, "余烬铁道", 15, FontStyle.Italic, TextDim);
            subText.alignment = TextAnchor.MiddleCenter;

            // Menu buttons — centered
            var hasContinue = hasExistingProgress;
            var ngPlusAvailable = hasClearedCampaign;

            // Build button list based on what's available
            var buttonList = new System.Collections.Generic.List<(string, string)>();
            if (hasContinue)
            {
                buttonList.Add(("CONTINUE", "continue"));
            }

            buttonList.Add(("NEW GAME", "new"));
            if (ngPlusAvailable)
            {
                buttonList.Add(("NEW GAME+", "ngplus"));
            }

            buttonList.Add(("SETTINGS", "settings"));
            buttonList.Add(("CREDITS", "credits"));
            buttonList.Add(("QUIT", "quit"));
            var buttonLabels = buttonList.ToArray();

            // Pack buttons tighter if there are more of them
            var stepY = buttonLabels.Length > 5 ? 0.054f : 0.062f;
            var startY = 0.42f;

            for (var i = 0; i < buttonLabels.Length; i++)
            {
                var (label, tag) = buttonLabels[i];
                var btnRect = CreateRect($"TitleBtn_{tag}", _root);
                btnRect.anchorMin = new Vector2(0.5f, startY - (i * stepY));
                btnRect.anchorMax = new Vector2(0.5f, startY - (i * stepY));
                btnRect.sizeDelta = new Vector2(240f, 38f);
                CreateMenuButton(btnRect, label, tag);
            }

            // Decorative divider above buttons.
            var divRect = CreateRect("TitleDivider", _root);
            divRect.anchorMin = new Vector2(0.5f, 0.50f);
            divRect.anchorMax = new Vector2(0.5f, 0.50f);
            divRect.sizeDelta = new Vector2(280f, 2f);
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.color = new Color(0.96f, 0.58f, 0.24f, 0.30f);
            divImg.raycastTarget = false;

            // Tagline below subtitle.
            var tagRect = CreateRect("TitleTagline", _root);
            tagRect.anchorMin = new Vector2(0.5f, 0.52f);
            tagRect.anchorMax = new Vector2(0.5f, 0.52f);
            tagRect.sizeDelta = new Vector2(500f, 18f);
            var tagText = CreateText(tagRect, "Hold the line. Tend the ember.", 11, FontStyle.Italic, new Color(0.72f, 0.66f, 0.54f, 0.70f));
            tagText.alignment = TextAnchor.MiddleCenter;

            // Version text — bottom corner
            var verRect = CreateRect("VersionLabel", _root);
            verRect.anchorMin = new Vector2(0.98f, 0.02f);
            verRect.anchorMax = new Vector2(0.98f, 0.02f);
            verRect.sizeDelta = new Vector2(200f, 16f);
            var verText = CreateText(verRect, "v0.13.0  ·  2026 Emberline Studios", 8, FontStyle.Normal, TextDim);
            verText.alignment = TextAnchor.LowerRight;

            BuildCreditsOverlay();
            gameObject.SetActive(true);
        }

        private void BuildCreditsOverlay()
        {
            _creditsOverlay = CreateRect("CreditsOverlay", _root);
            StretchFullScreen(_creditsOverlay);
            var credImage = _creditsOverlay.gameObject.AddComponent<Image>();
            credImage.color = new Color(0.02f, 0.03f, 0.04f, 0.94f);
            _creditsOverlay.gameObject.SetActive(false);

            var credTitle = CreateRect("CreditsTitle", _creditsOverlay);
            credTitle.anchorMin = new Vector2(0.5f, 0.80f);
            credTitle.anchorMax = new Vector2(0.5f, 0.80f);
            credTitle.sizeDelta = new Vector2(400f, 40f);
            var ct = CreateText(credTitle, "CREDITS", 22, FontStyle.Bold, AccentEmber);
            ct.alignment = TextAnchor.MiddleCenter;

            var creditsText = "Design & Engineering\nEmberline Team\n\nAudio\nGenerated via MiniMax\n\nArt\nProcedural + Image Pipeline\n\nSpecial Thanks\nTo all defenders of the Emberline\n\nBuilt with Unity 2022.3";
            var credBody = CreateRect("CreditsBody", _creditsOverlay);
            credBody.anchorMin = new Vector2(0.5f, 0.40f);
            credBody.anchorMax = new Vector2(0.5f, 0.40f);
            credBody.sizeDelta = new Vector2(400f, 300f);
            var cb = CreateText(credBody, creditsText, 11, FontStyle.Normal, TextBright);
            cb.alignment = TextAnchor.UpperCenter;
            cb.lineSpacing = 1.4f;

            // Back button
            var backRect = CreateRect("CreditsBack", _creditsOverlay);
            backRect.anchorMin = new Vector2(0.5f, 0.08f);
            backRect.anchorMax = new Vector2(0.5f, 0.08f);
            backRect.sizeDelta = new Vector2(160f, 36f);
            var backBtn = backRect.gameObject.AddComponent<Button>();
            var backImg = backRect.gameObject.AddComponent<Image>();
            backImg.color = new Color(0.08f, 0.10f, 0.14f, 0.80f);
            // Text must be on a child — can't share a GameObject with Image.
            var backLabelRect = CreateRect("CreditsBackLabel", backRect);
            backLabelRect.anchorMin = Vector2.zero;
            backLabelRect.anchorMax = Vector2.one;
            backLabelRect.offsetMin = Vector2.zero;
            backLabelRect.offsetMax = Vector2.zero;
            var backLabel = CreateText(backLabelRect, "BACK", 12, FontStyle.Bold, TextBright);
            backLabel.alignment = TextAnchor.MiddleCenter;
            backBtn.onClick.AddListener(() =>
            {
                _creditsOverlay.gameObject.SetActive(false);
            });
        }

        private Button CreateMenuButton(RectTransform parent, string label, string tag)
        {
            var img = parent.gameObject.AddComponent<Image>();
            img.color = new Color(0.06f, 0.08f, 0.11f, 0.75f);

            var btn = parent.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.10f);
            colors.highlightedColor = new Color(0.96f, 0.58f, 0.24f, 0.20f);
            colors.pressedColor = new Color(0.96f, 0.58f, 0.24f, 0.40f);
            colors.selectedColor = new Color(0.96f, 0.58f, 0.24f, 0.15f);
            btn.colors = colors;

            var labelRect = CreateRect($"TitleBtnLabel_{tag}", parent);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = CreateText(labelRect, label, 13, FontStyle.Bold, TextBright);
            text.alignment = TextAnchor.MiddleCenter;

            btn.onClick.AddListener(() => HandleMenuClick(tag));
            return btn;
        }

        private void HandleMenuClick(string tag)
        {
            switch (tag)
            {
                case "new":
                    OnNewGame?.Invoke();
                    break;
                case "ngplus":
                    OnNewGamePlus?.Invoke();
                    break;
                case "continue":
                    OnContinue?.Invoke();
                    break;
                case "settings":
                    OnOpenSettings?.Invoke();
                    break;
                case "credits":
                    if (_creditsOverlay != null)
                    {
                        _creditsOverlay.gameObject.SetActive(true);
                    }
                    break;
                case "quit":
#if UNITY_STANDALONE && !UNITY_EDITOR
                    Application.Quit();
#else
                    Debug.Log("[TD] Title: Quit requested (editor mode — ignored).");
#endif
                    break;
            }
        }

        /// <summary>Hide the title screen immediately (synchronous).</summary>
        public void Hide()
        {
            if (_fader != null)
            {
                _fader.alpha = 0f;
                _fader.blocksRaycasts = false;
            }

            // Deactivate the root panel (the child RectTransform that holds all UI).
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        // ─── UI helpers ──────────────────────────────────────────

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
