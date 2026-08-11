using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Loading transition screen shown during scene reloads (deploy/restart/quit-to-title).
    /// Displays a full-screen overlay with the level name and an animated loading bar.
    ///
    /// Usage: TDGameManager.LoadingTransition() shows this screen for 1 frame,
    /// then calls SceneManager.LoadScene. Because LoadScene is synchronous,
    /// the loading screen must render before the call blocks.
    /// </summary>
    public sealed class TDLoadingScreen : MonoBehaviour
    {
        private RectTransform _root;
        private Image _barFill;
        private Text _levelLabel;
        private Text _loadingLabel;
        private CanvasGroup _fader;
        private Coroutine _pulseRoutine;

        private static readonly Color PanelBg = new(0.02f, 0.03f, 0.04f, 0.98f);
        private static readonly Color AccentEmber = new(0.96f, 0.58f, 0.24f, 1f);
        private static readonly Color TextBright = new(0.93f, 0.96f, 0.98f, 1f);
        private static readonly Color TextDim = new(0.50f, 0.58f, 0.66f, 0.70f);
        private static readonly Color BarBg = new(0.08f, 0.10f, 0.13f, 0.90f);

        public void Build(Canvas parent)
        {
            _root = CreateRect("LoadingScreen", parent.transform);
            StretchFullScreen(_root);

            var rootImage = _root.gameObject.AddComponent<Image>();
            rootImage.color = PanelBg;

            _fader = _root.gameObject.AddComponent<CanvasGroup>();
            _fader.alpha = 0f;
            _fader.blocksRaycasts = false;

            // Centered content
            var contentRect = CreateRect("LoadingContent", _root);
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(500f, 140f);

            // Level name
            var levelRect = CreateRect("LoadingLevel", contentRect);
            levelRect.anchorMin = new Vector2(0.5f, 0.80f);
            levelRect.anchorMax = new Vector2(0.5f, 0.80f);
            levelRect.sizeDelta = new Vector2(480f, 36f);
            _levelLabel = CreateText(levelRect, string.Empty, 20, FontStyle.Bold, AccentEmber);
            _levelLabel.alignment = TextAnchor.MiddleCenter;

            // Loading text
            var loadingRect = CreateRect("LoadingText", contentRect);
            loadingRect.anchorMin = new Vector2(0.5f, 0.58f);
            loadingRect.anchorMax = new Vector2(0.5f, 0.58f);
            loadingRect.sizeDelta = new Vector2(300f, 20f);
            _loadingLabel = CreateText(loadingRect, "DEPLOYING", 11, FontStyle.Normal, TextDim);
            _loadingLabel.alignment = TextAnchor.MiddleCenter;

            // Progress bar background
            var barBgRect = CreateRect("LoadingBarBg", contentRect);
            barBgRect.anchorMin = new Vector2(0.5f, 0.30f);
            barBgRect.anchorMax = new Vector2(0.5f, 0.30f);
            barBgRect.sizeDelta = new Vector2(360f, 8f);
            var barBgImage = barBgRect.gameObject.AddComponent<Image>();
            barBgImage.color = BarBg;

            // Progress bar fill (child of bg, anchored left)
            var barFillRect = CreateRect("LoadingBarFill", barBgRect);
            barFillRect.anchorMin = new Vector2(0f, 0f);
            barFillRect.anchorMax = new Vector2(0f, 1f);
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;
            barFillRect.pivot = new Vector2(0f, 0.5f);
            _barFill = barFillRect.gameObject.AddComponent<Image>();
            _barFill.color = AccentEmber;
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillAmount = 0f;

            gameObject.SetActive(true);
            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Show the loading screen with a level label, then the caller should
        /// yield one frame and call SceneManager.LoadScene.
        /// </summary>
        public void Show(string levelLabel, string loadingVerb)
        {
            if (_levelLabel != null)
            {
                _levelLabel.text = levelLabel;
            }

            if (_loadingLabel != null && !string.IsNullOrEmpty(loadingVerb))
            {
                _loadingLabel.text = loadingVerb;
            }

            _barFill.fillAmount = 0f;
            _fader.alpha = 1f;
            _fader.blocksRaycasts = true;
            _root.gameObject.SetActive(true);

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }

            _pulseRoutine = StartCoroutine(AnimateBarRoutine());
        }

        private IEnumerator AnimateBarRoutine()
        {
            // Animate the bar fill in a loop — it's cosmetic since LoadScene is
            // synchronous and will block after the next yield.
            var t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime * 0.6f;
                _barFill.fillAmount = Mathf.Repeat(t, 1f);
                yield return null;
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
