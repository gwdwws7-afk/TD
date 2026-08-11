using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Pre-mission briefing overlay shown when entering a level (after loading,
    /// before the first wave prep timer). Displays:
    /// - Level number + map name + chapter
    /// - Scenario mechanic (map device) name + description
    /// - Threat composition (enemy types to expect)
    /// - Contract objective
    /// - "BEGIN" button (or auto-dismiss after a few seconds)
    ///
    /// The briefing pauses the game (timeScale=0) until dismissed, giving the
    /// player time to read the intel before combat starts.
    /// </summary>
    public sealed class TDMissionBriefing : MonoBehaviour
    {
        private RectTransform _root;
        private CanvasGroup _fader;
        private bool _isVisible;

        public System.Action OnBegin;

        private static readonly Color PanelBg = new(0.03f, 0.04f, 0.05f, 0.96f);
        private static readonly Color CardBg = new(0.05f, 0.07f, 0.09f, 0.90f);
        private static readonly Color AccentEmber = new(0.96f, 0.58f, 0.24f, 1f);
        private static readonly Color AccentCyan = new(0.42f, 0.86f, 0.92f, 1f);
        private static readonly Color AccentRed = new(0.92f, 0.38f, 0.30f, 1f);
        private static readonly Color TextBright = new(0.93f, 0.96f, 0.98f, 1f);
        private static readonly Color TextDim = new(0.60f, 0.68f, 0.76f, 0.80f);

        public bool IsVisible => _isVisible;

        public void Build(Canvas parent)
        {
            _root = CreateRect("MissionBriefing", parent.transform);
            StretchFullScreen(_root);

            var rootImage = _root.gameObject.AddComponent<Image>();
            rootImage.color = PanelBg;

            _fader = _root.gameObject.AddComponent<CanvasGroup>();
            _fader.alpha = 0f;
            _fader.blocksRaycasts = false;

            // Centered content card
            var cardRect = CreateRect("BriefingCard", _root);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(640f, 460f);
            var cardImage = cardRect.gameObject.AddComponent<Image>();
            cardImage.color = CardBg;

            // ── Header: level + map ──
            var headerRect = CreateRect("BriefingHeader", cardRect);
            headerRect.anchorMin = new Vector2(0.5f, 0.92f);
            headerRect.anchorMax = new Vector2(0.5f, 0.92f);
            headerRect.sizeDelta = new Vector2(580f, 44f);
            var headerText = CreateText(headerRect, string.Empty, 18, FontStyle.Bold, AccentEmber);
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.name = "BriefingHeaderText"; // lookup tag

            // ── Map tactical hook (subtitle) ──
            var hookRect = CreateRect("BriefingHook", cardRect);
            hookRect.anchorMin = new Vector2(0.5f, 0.85f);
            hookRect.anchorMax = new Vector2(0.5f, 0.85f);
            hookRect.sizeDelta = new Vector2(540f, 18f);
            var hookText = CreateText(hookRect, string.Empty, 10, FontStyle.Italic, TextDim);
            hookText.alignment = TextAnchor.MiddleCenter;
            hookText.name = "BriefingHookText";

            // ── Left column: scenario mechanic ──
            var scenarioRect = CreateRect("BriefingScenario", cardRect);
            scenarioRect.anchorMin = new Vector2(0.04f, 0.40f);
            scenarioRect.anchorMax = new Vector2(0.48f, 0.78f);
            var scenarioText = CreateText(scenarioRect, string.Empty, 10, FontStyle.Normal, AccentCyan);
            scenarioText.alignment = TextAnchor.UpperLeft;
            scenarioText.name = "BriefingScenarioText";

            // ── Right column: threat composition ──
            var threatRect = CreateRect("BriefingThreat", cardRect);
            threatRect.anchorMin = new Vector2(0.52f, 0.40f);
            threatRect.anchorMax = new Vector2(0.96f, 0.78f);
            var threatText = CreateText(threatRect, string.Empty, 10, FontStyle.Normal, AccentRed);
            threatText.alignment = TextAnchor.UpperLeft;
            threatText.name = "BriefingThreatText";

            // ── Bottom: contract objective ──
            var contractRect = CreateRect("BriefingContract", cardRect);
            contractRect.anchorMin = new Vector2(0.04f, 0.20f);
            contractRect.anchorMax = new Vector2(0.96f, 0.36f);
            var contractText = CreateText(contractRect, string.Empty, 10, FontStyle.Normal, TextBright);
            contractText.alignment = TextAnchor.UpperLeft;
            contractText.name = "BriefingContractText";

            // ── Begin button ──
            var btnRect = CreateRect("BriefingBeginBtn", cardRect);
            btnRect.anchorMin = new Vector2(0.5f, 0.06f);
            btnRect.anchorMax = new Vector2(0.5f, 0.06f);
            btnRect.sizeDelta = new Vector2(200f, 38f);
            var btnImage = btnRect.gameObject.AddComponent<Image>();
            btnImage.color = new Color(0.96f, 0.58f, 0.24f, 0.18f);
            var btn = btnRect.gameObject.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.96f, 0.58f, 0.24f, 0.40f);
            btnColors.pressedColor = new Color(0.96f, 0.58f, 0.24f, 0.60f);
            btn.colors = btnColors;
            var btnLabel = CreateText(btnRect, "BEGIN OPERATION", 13, FontStyle.Bold, AccentEmber);
            btnLabel.alignment = TextAnchor.MiddleCenter;
            btn.onClick.AddListener(() => OnBegin?.Invoke());

            gameObject.SetActive(true);
            _root.gameObject.SetActive(false);
        }

        public void Show(
            string levelTitle,
            string mapHook,
            string scenarioIntel,
            string threatIntel,
            string contractIntel)
        {
            SetChildText("BriefingHeaderText", levelTitle);
            SetChildText("BriefingHookText", mapHook);
            SetChildText("BriefingScenarioText", scenarioIntel);
            SetChildText("BriefingThreatText", threatIntel);
            SetChildText("BriefingContractText", contractIntel);

            _isVisible = true;
            _fader.alpha = 1f;
            _fader.blocksRaycasts = true;
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _isVisible = false;
            _fader.alpha = 0f;
            _fader.blocksRaycasts = false;
            _root.gameObject.SetActive(false);
        }

        private void SetChildText(string name, string text)
        {
            var transform = _root.Find($"BriefingCard/{name}");
            if (transform == null)
            {
                var found = _root.GetComponentsInChildren<Text>(true);
                foreach (var t in found)
                {
                    if (t.name == name)
                    {
                        t.text = text;
                        return;
                    }
                }
                return;
            }

            var label = transform.GetComponent<Text>();
            if (label != null)
            {
                label.text = text;
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
            text.lineSpacing = 1.3f;
            text.raycastTarget = false;
            return text;
        }
    }
}
