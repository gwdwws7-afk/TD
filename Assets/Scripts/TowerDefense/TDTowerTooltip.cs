using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Floating tooltip that shows tower stats when hovering.
    /// Attached to a UI RectTransform, positioned near the cursor.
    /// Shows: name, cost, damage/range/rate, counter tags, upgrade status.
    /// </summary>
    public sealed class TDTowerTooltip : MonoBehaviour
    {
        private RectTransform _rect;
        private RectTransform _canvasRect;
        private Text _nameText;
        private Text _statsText;
        private Text _counterText;
        private TDTower _currentTower;
        private float _hoverTimer;
        private int _lastContentTier = -1;
        private const float ShowDelay = 0.4f;
        private const float HideDistance = 80f;

        public static TDTowerTooltip Create(Transform parent)
        {
            var go = new GameObject("TowerTooltip", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tooltip = go.AddComponent<TDTowerTooltip>();
            tooltip.Initialize();
            return tooltip;
        }

        private void Initialize()
        {
            _rect = GetComponent<RectTransform>();
            var parentCanvas = GetComponentInParent<Canvas>();
            _canvasRect = parentCanvas != null ? (RectTransform)parentCanvas.transform : null;
            _rect.anchorMin = new Vector2(0f, 0f);
            _rect.anchorMax = new Vector2(0f, 0f);
            _rect.sizeDelta = new Vector2(220f, 90f);
            _rect.pivot = new Vector2(0f, 0f);

            // Background
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.04f, 0.05f, 0.94f);

            // Name text
            _nameText = CreateText("TooltipName", new Vector2(8f, -4f), new Vector2(204f, 18f), 11, FontStyle.Bold, new Color(0.95f, 0.97f, 1f, 1f));
            _statsText = CreateText("TooltipStats", new Vector2(8f, -24f), new Vector2(204f, 32f), 9, FontStyle.Normal, new Color(0.78f, 0.88f, 0.95f, 0.9f));
            _counterText = CreateText("TooltipCounter", new Vector2(8f, -58f), new Vector2(204f, 28f), 8, FontStyle.Italic, new Color(0.85f, 0.75f, 0.45f, 0.85f));

            gameObject.SetActive(false);
        }

        private Text CreateText(string name, Vector2 pos, Vector2 size, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = TDLocalization.ResolveFont(null);
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public void HoverTower(TDTower tower)
        {
            // Called every frame while hovering — only restart the delay when
            // the target actually changes, otherwise the tooltip would never
            // survive its own ShowDelay.
            if (_currentTower != tower)
            {
                _hoverTimer = 0f;
                _currentTower = tower;
            }

            // TD-GP-003: an inactive component's Update can never re-enable
            // itself, so the show-delay must elapse HERE while hidden —
            // activation is driven externally once the delay passes (Update
            // hides us again after hover ends).
            if (tower != null && !gameObject.activeSelf)
            {
                _hoverTimer += Time.unscaledDeltaTime;
                if (_hoverTimer >= ShowDelay)
                {
                    gameObject.SetActive(true);
                    RefreshContent();
                    _lastContentTier = tower.Tier;
                }
            }
        }

        public void ClearHover()
        {
            _currentTower = null;
            _hoverTimer = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_currentTower == null)
            {
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            _hoverTimer += Time.unscaledDeltaTime;
            if (_hoverTimer < ShowDelay)
            {
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                RefreshContent();
                _lastContentTier = _currentTower.Tier;
            }
            else if (_currentTower.Tier != _lastContentTier)
            {
                // Upgrades can land while the pointer keeps resting on the tower.
                RefreshContent();
                _lastContentTier = _currentTower.Tier;
            }

            // Position near cursor (offset right/up), converted into canvas
            // units — the battle canvas is ScaleWithScreenSize, so raw screen
            // pixels would drift away from the cursor at any scale ≠ 1.
            var mouse = TDInputCompat.MousePosition;
            if (_canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, mouse, null, out var localPoint))
            {
                var halfWidth = _canvasRect.rect.width * 0.5f;
                var halfHeight = _canvasRect.rect.height * 0.5f;
                var offset = new Vector2(16f, 16f);

                // Flip if the tooltip would cross the right/top edges.
                if (localPoint.x + offset.x + _rect.rect.width > halfWidth)
                {
                    offset.x = -offset.x - _rect.rect.width;
                }

                if (localPoint.y + offset.y + _rect.rect.height > halfHeight)
                {
                    offset.y = -offset.y - _rect.rect.height;
                }

                _rect.localPosition = localPoint + offset;
            }
        }

        private void RefreshContent()
        {
            if (_currentTower == null)
            {
                return;
            }

            _nameText.text = _currentTower.DisplayName;
            _statsText.text = $"DMG {_currentTower.Damage}  RNG {_currentTower.AttackRange:0.0}  ROF {_currentTower.ShotsPerSecond:0.0}/s\nTIER {_currentTower.Tier}/3";

            var spec = _currentTower.ActiveSpecialization;
            _counterText.text = spec != null
                ? $"Spec: {spec.displayName}\nCounters: {spec.counterTags}"
                : "No specialization\nUpgrade 2x in one branch";

            // Codex note (imbalance diagnosis appendix C.4): teach the
            // first-shot miss rule where players read the tower's numbers.
            if (_currentTower.Kind == TDTowerKind.FrostCoil)
            {
                _counterText.text += "\n" + (TDLocalization.IsChinese
                    ? "首发射击对未减速的快速目标有失手可能；命中后减速生效，全队即恢复必中。"
                    : "First shots may miss unslowed fast targets; once a hit lands the slow applies and the whole roster is guaranteed to hit.");
            }
        }
    }
}
