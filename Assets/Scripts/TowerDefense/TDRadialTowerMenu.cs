using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    /// <summary>
    /// Radial tower selection menu that appears when clicking a build site.
    /// Shows available towers in a circle around the click point.
    /// Player clicks a tower icon to build it at the saved location.
    /// </summary>
    public sealed class TDRadialTowerMenu : MonoBehaviour
    {
        private RectTransform _root;
        private CanvasGroup _fader;
        private readonly List<RectTransform> _slots = new();
        private readonly List<Button> _buttons = new();
        private readonly List<Text> _labels = new();
        private readonly List<Image> _icons = new();
        private Vector2Int _targetCell;
        private Vector3 _targetWorld;

        public System.Action<Vector2Int, TDTowerKind> OnTowerSelected;
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        private const float SlotSize = 56f;
        private const float Radius = 70f;

        private static readonly Color PanelBg = new(0.03f, 0.04f, 0.05f, 0.88f);
        private static readonly Color SlotBg = new(0.08f, 0.10f, 0.14f, 0.90f);
        private static readonly Color SlotAffordable = new(0.12f, 0.20f, 0.28f, 0.95f);
        private static readonly Color SlotLocked = new(0.05f, 0.05f, 0.06f, 0.60f);
        private static readonly Color AccentEmber = new(0.96f, 0.58f, 0.24f, 1f);
        private static readonly Color TextBright = new(0.93f, 0.96f, 0.98f, 1f);
        private static readonly Color TextDim = new(0.45f, 0.48f, 0.52f, 0.60f);

        // Tower display colors (compact versions for icons).
        private static readonly Color[] TowerColors =
        {
            new(0.20f, 0.38f, 0.80f, 1f), // RailLancer - blue
            new(0.90f, 0.50f, 0.20f, 1f), // CinderMortar - orange
            new(0.30f, 0.80f, 0.90f, 1f), // FrostCoil - cyan
            new(0.40f, 0.60f, 1.00f, 1f), // ArcWelder - electric blue
            new(0.85f, 0.70f, 0.25f, 1f), // SiegeDrill - gold
            new(0.95f, 0.45f, 0.20f, 1f), // EmberFlak - red-orange
            new(0.30f, 0.85f, 0.45f, 1f), // ResonanceBeacon - green
            new(0.50f, 0.40f, 0.80f, 1f), // GravSnare - purple
        };

        private static readonly string[] TowerShortNames =
        {
            "RAIL", "CIND", "FROST", "ARC", "SIEGE", "FLAK", "BEACON", "GRAV",
        };

        public void Build(Canvas parent)
        {
            _root = CreateRect("RadialMenu", parent.transform);
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = Vector2.zero;

            _fader = _root.gameObject.AddComponent<CanvasGroup>();
            _fader.alpha = 0f;
            _fader.blocksRaycasts = false;

            // Create 8 slots (max towers), positioned in a circle.
            for (var i = 0; i < 8; i++)
            {
                var angle = (i / 8f) * Mathf.PI * 2f - Mathf.PI / 2f; // start from top
                var x = Mathf.Cos(angle) * Radius;
                var y = Mathf.Sin(angle) * Radius;

                var slot = CreateRect($"Slot_{i}", _root);
                slot.anchoredPosition = new Vector2(x, y);
                slot.sizeDelta = Vector2.one * SlotSize;

                var icon = slot.gameObject.AddComponent<Image>();
                icon.color = SlotBg;

                var btn = slot.gameObject.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.96f, 0.58f, 0.24f, 0.30f);
                colors.pressedColor = new Color(0.96f, 0.58f, 0.24f, 0.50f);
                btn.colors = colors;

                // Label (cost + name) on child.
                var labelRect = CreateRect($"SlotLabel_{i}", slot);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                var label = labelRect.gameObject.AddComponent<Text>();
                label.font = TDLocalization.ResolveFont(null);
                label.fontSize = 7;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = TextBright;
                label.raycastTarget = false;

                _slots.Add(slot);
                _icons.Add(icon);
                _buttons.Add(btn);
                _labels.Add(label);

                slot.gameObject.SetActive(false);
            }

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Show the radial menu at a screen position for the given cell.
        /// </summary>
        public void Show(Vector2 screenPos, Vector2Int cell, Vector3 worldPos,
            TDTowerKind[] availableTowers, int[] costs, int budget, bool[] unlocked)
        {
            _targetCell = cell;
            _targetWorld = worldPos;

            // Position root at the screen point.
            _root.anchoredPosition = screenPos -
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Configure slots.
            for (var i = 0; i < _slots.Count; i++)
            {
                if (i >= availableTowers.Length)
                {
                    _slots[i].gameObject.SetActive(false);
                    continue;
                }

                var kind = availableTowers[i];
                var cost = i < costs.Length ? costs[i] : 0;
                var isUnlocked = i < unlocked.Length && unlocked[i];
                var canAfford = budget >= cost;
                var usable = isUnlocked && canAfford;

                _slots[i].gameObject.SetActive(true);
                _icons[i].color = usable
                    ? new Color(TowerColors[(int)kind].r * 0.4f, TowerColors[(int)kind].g * 0.4f, TowerColors[(int)kind].b * 0.4f, 0.95f)
                    : SlotLocked;
                _labels[i].text = $"{TowerShortNames[(int)kind]}\n{cost}";
                _labels[i].color = usable ? TextBright : TextDim;
                _buttons[i].interactable = usable;

                var capturedKind = kind;
                _buttons[i].onClick.RemoveAllListeners();
                _buttons[i].onClick.AddListener(() =>
                {
                    OnTowerSelected?.Invoke(_targetCell, capturedKind);
                    Hide();
                });
            }

            _fader.alpha = 1f;
            _fader.blocksRaycasts = true;
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
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

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
