using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Pause menu overlay shown when the player presses P / ESC / Start during combat.
    /// Displays RESUME / RESTART / SETTINGS / QUIT TO TITLE buttons.
    ///
    /// Paired with TDGameManager.TogglePauseMenu() which freezes timeScale
    /// while this overlay is visible.
    /// </summary>
    public sealed class TDPauseMenu : MonoBehaviour
    {
        private RectTransform _root;
        private CanvasGroup _fader;
        private bool _isVisible;

        // Callbacks set by TDGameManager
        public System.Action OnResume;
        public System.Action OnRestart;
        public System.Action OnOpenSettings;
        public System.Action OnQuitToTitle;

        /// <summary>True while the pause menu is covering the game.</summary>
        public bool IsVisible => _isVisible;

        private static readonly Color PanelBg = new(0.02f, 0.03f, 0.04f, 0.88f);
        private static readonly Color AccentEmber = new(0.96f, 0.58f, 0.24f, 1f);
        private static readonly Color TextBright = new(0.93f, 0.96f, 0.98f, 1f);
        private static readonly Color TextDim = new(0.62f, 0.70f, 0.78f, 0.80f);

        public void Build(Canvas parent)
        {
            // Root panel — full screen dim overlay
            _root = CreateRect("PauseMenu", parent.transform);
            StretchFullScreen(_root);

            var rootImage = _root.gameObject.AddComponent<Image>();
            rootImage.color = PanelBg;
            rootImage.raycastTarget = true;

            _fader = _root.gameObject.AddComponent<CanvasGroup>();
            _fader.alpha = 0f;
            _fader.blocksRaycasts = false;

            // Centered panel
            var panelRect = CreateRect("PausePanel", _root);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(300f, 320f);
            var panelImage = panelRect.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.06f, 0.08f, 0.95f);

            // Title
            var titleRect = CreateRect("PauseTitle", panelRect);
            titleRect.anchorMin = new Vector2(0.5f, 0.90f);
            titleRect.anchorMax = new Vector2(0.5f, 0.90f);
            titleRect.sizeDelta = new Vector2(260f, 36f);
            var titleText = CreateText(titleRect, "PAUSED", 22, FontStyle.Bold, AccentEmber);
            titleText.alignment = TextAnchor.MiddleCenter;

            // Subtitle hint
            var hintRect = CreateRect("PauseHint", panelRect);
            hintRect.anchorMin = new Vector2(0.5f, 0.82f);
            hintRect.anchorMax = new Vector2(0.5f, 0.82f);
            hintRect.sizeDelta = new Vector2(260f, 18f);
            var hintText = CreateText(hintRect, "Press P or ESC to resume", 9, FontStyle.Italic, TextDim);
            hintText.alignment = TextAnchor.MiddleCenter;

            // Buttons
            var labels = new[] { ("RESUME", "resume"), ("RESTART", "restart"), ("SETTINGS", "settings"), ("QUIT TO TITLE", "quit") };
            var startY = 0.68f;
            var stepY = 0.15f;
            for (var i = 0; i < labels.Length; i++)
            {
                var (label, tag) = labels[i];
                var btnRect = CreateRect($"PauseBtn_{tag}", panelRect);
                btnRect.anchorMin = new Vector2(0.5f, startY - (i * stepY));
                btnRect.anchorMax = new Vector2(0.5f, startY - (i * stepY));
                btnRect.sizeDelta = new Vector2(220f, 34f);
                CreateMenuButton(btnRect, label, tag);
            }

            gameObject.SetActive(true);
            _root.gameObject.SetActive(false);
        }

        private Button CreateMenuButton(RectTransform parent, string label, string tag)
        {
            var img = parent.gameObject.AddComponent<Image>();
            img.color = new Color(0.06f, 0.08f, 0.11f, 0.80f);

            var btn = parent.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.10f);
            colors.highlightedColor = new Color(0.96f, 0.58f, 0.24f, 0.20f);
            colors.pressedColor = new Color(0.96f, 0.58f, 0.24f, 0.40f);
            colors.selectedColor = new Color(0.96f, 0.58f, 0.24f, 0.15f);
            btn.colors = colors;

            var labelRect = CreateRect($"PauseBtnLabel_{tag}", parent);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = CreateText(labelRect, label, 12, FontStyle.Bold, TextBright);
            text.alignment = TextAnchor.MiddleCenter;

            btn.onClick.AddListener(() => HandleClick(tag));
            return btn;
        }

        private void HandleClick(string tag)
        {
            switch (tag)
            {
                case "resume":
                    OnResume?.Invoke();
                    break;
                case "restart":
                    OnRestart?.Invoke();
                    break;
                case "settings":
                    OnOpenSettings?.Invoke();
                    break;
                case "quit":
                    OnQuitToTitle?.Invoke();
                    break;
            }
        }

        public void Show()
        {
            _isVisible = true;
            if (_root != null)
            {
                _root.gameObject.SetActive(true);
                _fader.alpha = 1f;
                _fader.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            _isVisible = false;
            if (_fader != null)
            {
                _fader.alpha = 0f;
                _fader.blocksRaycasts = false;
            }

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
