using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Polished title screen with gradient bg, ember particles, title glow,
    /// animated entrance, and styled buttons.
    /// </summary>
    public sealed class TDTitleScreen : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _creditsOverlay;
        private CanvasGroup _fader;
        private Text _titleText;
        private Image _titleGlow;
        private readonly List<RectTransform> _embers = new();
        private Coroutine _emberRoutine;
        private Coroutine _glowRoutine;
        private Coroutine _enterRoutine;

        public System.Action OnNewGame;
        public System.Action OnNewGamePlus;
        public System.Action OnContinue;
        public System.Action OnOpenSettings;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        // Palette.
        private static readonly Color BgTop = new(0.018f, 0.022f, 0.030f, 1f);
        private static readonly Color BgBottom = new(0.045f, 0.028f, 0.018f, 1f);
        private static readonly Color AccentEmber = new(0.96f, 0.52f, 0.18f, 1f);
        private static readonly Color AccentEmberDim = new(0.96f, 0.52f, 0.18f, 0.12f);
        private static readonly Color TextBright = new(0.94f, 0.96f, 0.98f, 1f);
        private static readonly Color TextDim = new(0.56f, 0.60f, 0.66f, 0.75f);
        private static readonly Color BtnNormal = new(0.06f, 0.07f, 0.09f, 0.80f);
        private static readonly Color BtnHover = new(0.14f, 0.10f, 0.06f, 0.90f);
        private static readonly Color DividerColor = new(0.96f, 0.52f, 0.18f, 0.20f);

        private static Sprite _roundedSprite;

        public void Build(Canvas parent, bool hasExistingProgress, bool hasClearedCampaign = false)
        {
            // ── Root ──
            _root = CreateRect("TitleScreen", parent.transform);
            StretchFull(_root);

            // ── Gradient background ──
            var bgRect = CreateRect("TitleBg", _root);
            StretchFull(bgRect);
            var bgImg = bgRect.gameObject.AddComponent<Image>();
            bgImg.sprite = CreateGradientSprite();
            bgImg.color = Color.white;
            bgImg.raycastTarget = true;

            // Try background art.
            var bgPath = "Art/Branding/emberline_startup_background";
            var bgTex = Resources.Load<Texture2D>(bgPath);
            if (bgTex != null)
            {
                var artSprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f));
                var artRect = CreateRect("TitleArt", _root);
                StretchFull(artRect);
                var artImg = artRect.gameObject.AddComponent<Image>();
                artImg.sprite = artSprite;
                artImg.color = new Color(0.38f, 0.36f, 0.40f, 0.35f);
                artImg.raycastTarget = false;
            }

            // ── Ember particles (subtle, behind everything) ──
            CreateEmbers(10);

            // ── Title glow halo ──
            var glowRect = CreateRect("TitleGlow", _root);
            glowRect.anchorMin = new Vector2(0.5f, 0.64f);
            glowRect.anchorMax = new Vector2(0.5f, 0.64f);
            glowRect.sizeDelta = new Vector2(800f, 200f);
            _titleGlow = glowRect.gameObject.AddComponent<Image>();
            _titleGlow.sprite = CreateRadialGradientSprite(400, 100);
            _titleGlow.color = AccentEmberDim;
            _titleGlow.raycastTarget = false;

            // ── Title text ──
            var titleShadowRect = CreateRect("TitleShadow", _root);
            titleShadowRect.anchorMin = new Vector2(0.5f, 0.64f);
            titleShadowRect.anchorMax = new Vector2(0.5f, 0.64f);
            titleShadowRect.sizeDelta = new Vector2(750f, 90f);
            titleShadowRect.anchoredPosition = new Vector2(4f, -4f);
            var shadowTxt = CreateText(titleShadowRect, "EMBERLINE DEFENSE", 46, FontStyle.Bold, new Color(0f, 0f, 0f, 0.50f));
            shadowTxt.alignment = TextAnchor.MiddleCenter;

            var titleRect = CreateRect("TitleLabel", _root);
            titleRect.anchorMin = new Vector2(0.5f, 0.64f);
            titleRect.anchorMax = new Vector2(0.5f, 0.64f);
            titleRect.sizeDelta = new Vector2(750f, 90f);
            _titleText = CreateText(titleRect, "EMBERLINE DEFENSE", 46, FontStyle.Bold, AccentEmber);
            _titleText.alignment = TextAnchor.MiddleCenter;

            // ── Subtitle ──
            var subRect = CreateRect("TitleSubtitle", _root);
            subRect.anchorMin = new Vector2(0.5f, 0.555f);
            subRect.anchorMax = new Vector2(0.5f, 0.555f);
            subRect.sizeDelta = new Vector2(400f, 24f);
            var subTxt = CreateText(subRect, "余 烬 铁 道", 16, FontStyle.Italic, new Color(0.72f, 0.64f, 0.52f, 0.80f));
            subTxt.alignment = TextAnchor.MiddleCenter;

            // ── Tagline ──
            var tagRect = CreateRect("TitleTagline", _root);
            tagRect.anchorMin = new Vector2(0.5f, 0.515f);
            tagRect.anchorMax = new Vector2(0.5f, 0.515f);
            tagRect.sizeDelta = new Vector2(500f, 18f);
            var tagTxt = CreateText(tagRect, "Hold the line. Tend the ember.", 11, FontStyle.Italic, TextDim);
            tagTxt.alignment = TextAnchor.MiddleCenter;

            // ── Divider ──
            var divRect = CreateRect("TitleDivider", _root);
            divRect.anchorMin = new Vector2(0.5f, 0.48f);
            divRect.anchorMax = new Vector2(0.5f, 0.48f);
            divRect.sizeDelta = new Vector2(320f, 2f);
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.color = DividerColor;
            divImg.raycastTarget = false;

            // ── Menu buttons ──
            var buttonList = new List<(string, string)>();
            if (hasExistingProgress) buttonList.Add(("CONTINUE", "continue"));
            buttonList.Add(("NEW GAME", "new"));
            if (hasClearedCampaign) buttonList.Add(("NEW GAME+", "ngplus"));
            buttonList.Add(("SETTINGS", "settings"));
            buttonList.Add(("CREDITS", "credits"));
            buttonList.Add(("QUIT", "quit"));

            var stepY = 0.065f;
            var startY = 0.40f;
            for (var i = 0; i < buttonList.Count; i++)
            {
                var (label, tag) = buttonList[i];
                var btnRect = CreateRect($"TitleBtn_{tag}", _root);
                btnRect.anchorMin = new Vector2(0.5f, startY - i * stepY);
                btnRect.anchorMax = new Vector2(0.5f, startY - i * stepY);
                btnRect.sizeDelta = new Vector2(280f, 40f);
                CreateStyledButton(btnRect, label, tag);
            }

            // ── Version ──
            var verRect = CreateRect("Version", _root);
            verRect.anchorMin = new Vector2(0.98f, 0.02f);
            verRect.anchorMax = new Vector2(0.98f, 0.02f);
            verRect.sizeDelta = new Vector2(240f, 16f);
            var verTxt = CreateText(verRect, "v0.13.0  ·  Emberline Studios  ·  2026", 8, FontStyle.Normal, TextDim);
            verTxt.alignment = TextAnchor.LowerRight;

            _fader = _root.gameObject.AddComponent<CanvasGroup>();
            _fader.alpha = 0f;
            _fader.blocksRaycasts = true;

            BuildCreditsOverlay();
            gameObject.SetActive(true);

            // Start entrance animation.
            _enterRoutine = StartCoroutine(EntranceRoutine());
        }

        // ── Entrance animation ──────────────────────────────────

        private IEnumerator EntranceRoutine()
        {
            var elapsed = 0f;
            var duration = 0.6f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fader.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            _fader.alpha = 1f;

            // Start ember + glow loops.
            _emberRoutine = StartCoroutine(EmberRoutine());
            _glowRoutine = StartCoroutine(TitleGlowRoutine());
        }

        private IEnumerator TitleGlowRoutine()
        {
            while (true)
            {
                // Very subtle pulse: 0.03-0.06 alpha, slow 0.5Hz.
                var pulse = 0.045f + Mathf.Sin(Time.unscaledTime * 0.5f) * 0.015f;
                if (_titleGlow != null) _titleGlow.color = new Color(AccentEmber.r, AccentEmber.g, AccentEmber.b, pulse);
                yield return null;
            }
        }

        // ── Ember particles ─────────────────────────────────────

        private void CreateEmbers(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var ember = CreateRect($"Ember_{i}", _root);
                ember.sizeDelta = new Vector2(3f, 3f);
                // Fixed center anchor — position controlled purely by anchoredPosition.
                ember.anchorMin = new Vector2(0.5f, 0.5f);
                ember.anchorMax = new Vector2(0.5f, 0.5f);
                ember.anchoredPosition = new Vector2(
                    Random.Range(-400f, 400f),
                    Random.Range(-250f, 250f));
                var img = ember.gameObject.AddComponent<Image>();
                img.color = new Color(0.96f, 0.55f, 0.20f, Random.Range(0.15f, 0.35f));
                img.raycastTarget = false;
                _embers.Add(ember);
            }
        }

        private IEnumerator EmberRoutine()
        {
            // Pre-randomize velocity (pixels per second).
            var vx = new float[_embers.Count];
            var vy = new float[_embers.Count];
            var halfH = 300f;
            var halfW = 550f;
            for (var i = 0; i < _embers.Count; i++)
            {
                vy[i] = Random.Range(8f, 25f);
                vx[i] = Random.Range(-6f, 6f);
            }

            while (true)
            {
                var dt = Time.unscaledDeltaTime;
                for (var i = 0; i < _embers.Count; i++)
                {
                    var e = _embers[i];
                    var pos = e.anchoredPosition;
                    pos.y += vy[i] * dt;
                    pos.x += vx[i] * dt;
                    if (pos.y > halfH)
                    {
                        pos.y = -halfH;
                        pos.x = Random.Range(-halfW, halfW);
                        vy[i] = Random.Range(8f, 25f);
                        vx[i] = Random.Range(-6f, 6f);
                    }
                    e.anchoredPosition = pos;
                }
                yield return null;
            }
        }

        // ── Styled buttons ──────────────────────────────────────

        private void CreateStyledButton(RectTransform parent, string label, string tag)
        {
            var img = parent.gameObject.AddComponent<Image>();
            img.sprite = GetRoundedSprite();
            img.color = BtnNormal;
            img.type = Image.Type.Sliced;

            var btn = parent.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.0f, 0.6f);
            colors.pressedColor = new Color(1.5f, 1.2f, 0.8f);
            colors.fadeDuration = 0.12f;
            btn.colors = colors;

            // Label on child.
            var labelRect = CreateRect($"BtnLabel_{tag}", parent);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var txt = CreateText(labelRect, label, 14, FontStyle.Bold, TextBright);
            txt.alignment = TextAnchor.MiddleCenter;

            btn.onClick.AddListener(() => HandleClick(tag));
        }

        // ── Credits overlay ─────────────────────────────────────

        private void BuildCreditsOverlay()
        {
            _creditsOverlay = CreateRect("CreditsOverlay", _root);
            StretchFull(_creditsOverlay);
            var credImg = _creditsOverlay.gameObject.AddComponent<Image>();
            credImg.color = new Color(0.015f, 0.020f, 0.028f, 0.96f);
            _creditsOverlay.gameObject.SetActive(false);

            var titleRect = CreateRect("CreditsTitle", _creditsOverlay);
            titleRect.anchorMin = new Vector2(0.5f, 0.82f);
            titleRect.anchorMax = new Vector2(0.5f, 0.82f);
            titleRect.sizeDelta = new Vector2(400f, 44f);
            var ct = CreateText(titleRect, "CREDITS", 24, FontStyle.Bold, AccentEmber);
            ct.alignment = TextAnchor.MiddleCenter;

            var body = "Design & Engineering\nEmberline Team\n\nAudio\nGenerated via MiniMax\n\nArt\nProcedural + Image Pipeline\n\nSpecial Thanks\nTo all defenders of the Emberline\n\nBuilt with Unity 2022.3";
            var bodyRect = CreateRect("CreditsBody", _creditsOverlay);
            bodyRect.anchorMin = new Vector2(0.5f, 0.38f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.38f);
            bodyRect.sizeDelta = new Vector2(400f, 320f);
            var bt = CreateText(bodyRect, body, 11, FontStyle.Normal, TextBright);
            bt.alignment = TextAnchor.UpperCenter;
            bt.lineSpacing = 1.5f;

            // Back button.
            var backRect = CreateRect("CreditsBack", _creditsOverlay);
            backRect.anchorMin = new Vector2(0.5f, 0.06f);
            backRect.anchorMax = new Vector2(0.5f, 0.06f);
            backRect.sizeDelta = new Vector2(160f, 36f);
            var backBtn = backRect.gameObject.AddComponent<Button>();
            var backImg = backRect.gameObject.AddComponent<Image>();
            backImg.sprite = GetRoundedSprite();
            backImg.color = BtnNormal;
            backImg.type = Image.Type.Sliced;
            var backLabelRect = CreateRect("CreditsBackLabel", backRect);
            backLabelRect.anchorMin = Vector2.zero; backLabelRect.anchorMax = Vector2.one;
            backLabelRect.offsetMin = Vector2.zero; backLabelRect.offsetMax = Vector2.zero;
            var backLabel = CreateText(backLabelRect, "← BACK", 12, FontStyle.Bold, TextBright);
            backLabel.alignment = TextAnchor.MiddleCenter;
            backBtn.onClick.AddListener(() => _creditsOverlay.gameObject.SetActive(false));
        }

        // ── Click handler ───────────────────────────────────────

        private void HandleClick(string tag)
        {
            switch (tag)
            {
                case "new": OnNewGame?.Invoke(); break;
                case "ngplus": OnNewGamePlus?.Invoke(); break;
                case "continue": OnContinue?.Invoke(); break;
                case "settings": OnOpenSettings?.Invoke(); break;
                case "credits": _creditsOverlay?.gameObject.SetActive(true); break;
                case "quit":
#if UNITY_STANDALONE && !UNITY_EDITOR
                    Application.Quit();
#else
                    Debug.Log("[TD] Title: Quit requested (editor mode — ignored).");
#endif
                    break;
            }
        }

        // ── Show / Hide ─────────────────────────────────────────

        public void Hide()
        {
            if (_emberRoutine != null) StopCoroutine(_emberRoutine);
            if (_glowRoutine != null) StopCoroutine(_glowRoutine);
            if (_fader != null) { _fader.alpha = 0f; _fader.blocksRaycasts = false; }
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // ── Sprite generation ───────────────────────────────────

        private static Sprite CreateGradientSprite()
        {
            var tex = new Texture2D(2, 64);
            for (var y = 0; y < 64; y++)
            {
                var t = y / 63f;
                var c = Color.Lerp(BgBottom, BgTop, t);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRadialGradientSprite(int w, int h)
        {
            var tex = new Texture2D(w, h);
            var cx = w * 0.5f;
            var cy = h * 0.5f;
            var maxDist = Mathf.Sqrt(cx * cx + cy * cy);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    var alpha = Mathf.Clamp01(1f - dist / maxDist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        private static Sprite GetRoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            var size = 32;
            var radius = 8;
            var tex = new Texture2D(size, size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = 1f;
                    // Check corners.
                    if (x < radius && y < radius)
                        alpha = Mathf.Clamp01(((x - radius) * (x - radius) + (y - radius) * (y - radius)) / (float)(radius * radius));
                    else if (x >= size - radius && y < radius)
                        alpha = Mathf.Clamp01(((x - (size - radius - 1)) * (x - (size - radius - 1)) + (y - radius) * (y - radius)) / (float)(radius * radius));
                    else if (x < radius && y >= size - radius)
                        alpha = Mathf.Clamp01(((x - radius) * (x - radius) + (y - (size - radius - 1)) * (y - (size - radius - 1))) / (float)(radius * radius));
                    else if (x >= size - radius && y >= size - radius)
                        alpha = Mathf.Clamp01(((x - (size - radius - 1)) * (x - (size - radius - 1)) + (y - (size - radius - 1)) * (y - (size - radius - 1))) / (float)(radius * radius));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            var border = Mathf.Max(1, radius / 4);
            _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100, extrude: 0, meshType: SpriteMeshType.FullRect,
                border: new Vector4(border, border, border, border));
            return _roundedSprite;
        }

        // ── Helpers ─────────────────────────────────────────────

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void StretchFull(RectTransform rt)
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
