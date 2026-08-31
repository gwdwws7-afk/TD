// Freeze-period S4: the code-built UI factory cluster moved verbatim from TDGameManager.cs (panel/text/button/rect/metric/chrome/icon factories + the SetUiText write-through).
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TD
{
    public sealed partial class TDGameManager : MonoBehaviour
    {
        private RectTransform CreateUiPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var rect = CreateUiRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private Text CreateUiMetric(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, Color background, Color foreground, string iconResourcePath)
        {
            var root = CreateUiPanel(name + " Backdrop", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta, background);
            TDUiWorldSkin.ApplyMetric(root, foreground);
            var accent = CreateUiImage(name + " Accent", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f), foreground);
            accent.raycastTarget = false;
            CreateUiSpriteImage(name + " Icon", root, new Vector2(8f, -6f), new Vector2(26f, 26f), iconResourcePath, Color.white);
            var label = CreateUiText(name, root, new Vector2(38f, 0f), new Vector2(sizeDelta.x - 42f, sizeDelta.y), string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleLeft, foreground);
            return label;
        }

        private void AddUiPanelChrome(RectTransform panel, Color accentColor)
        {
            if (panel == null)
            {
                return;
            }

            if (panel.parent is RectTransform parentRect &&
                Mathf.Abs(parentRect.rect.width - panel.rect.width) <= 2f &&
                Mathf.Abs(parentRect.rect.height - panel.rect.height) <= 2f &&
                parentRect.GetComponent<Image>() != null)
            {
                TDUiWorldSkin.ApplyPanel(panel, accentColor, true);
                return;
            }

            var compact = panel.sizeDelta.y <= 72f;
            TDUiWorldSkin.ApplyPanel(panel, accentColor, compact);
        }

        private static void SetUiBottomRightLayout(RectTransform rect, Vector2 anchoredPosition)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
        }

        private Image CreateUiImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var rect = CreateUiRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (name.Contains("Rule") || name.Contains("Divider") || name.Contains("Chart Back"))
            {
                TDUiWorldSkin.ApplyRule(image, color, sizeDelta.y > sizeDelta.x);
            }
            return image;
        }

        private Image CreateUiSpriteImage(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, string resourcePath, Color color)
        {
            var rect = CreateUiRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadUiSprite(resourcePath);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private Image AddUiButtonIcon(Button button, string name, string resourcePath, Vector2 topLeft, Vector2 sizeDelta, float labelLeftInset)
        {
            if (button == null)
            {
                return null;
            }

            var icon = CreateUiSpriteImage(name, button.transform, topLeft, sizeDelta, resourcePath, Color.white);
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.rectTransform.offsetMin = new Vector2(labelLeftInset, label.rectTransform.offsetMin.y);
            }

            return icon;
        }

        private Text CreateUiText(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var rect = CreateUiRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta);
            var label = rect.gameObject.AddComponent<Text>();
            var role = ResolveUiTextRole(fontSize);
            var resolvedFontSize = GetUiRoleFontSize(role);
            label.font = TDLocalization.ResolveFont(_uiFont);
            label.fontSize = resolvedFontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color;
            TDLocalization.SetLabel(label, text, _uiFont);
            label.lineSpacing = role == TDUiTextRole.Body ? 0.94f : 1f;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = resolvedFontSize <= 17;
            label.resizeTextMinSize = Mathf.Max(9, resolvedFontSize - 3);
            label.resizeTextMaxSize = resolvedFontSize;
            label.raycastTarget = false;
            TDUiWorldSkin.ApplyText(label, fontStyle == FontStyle.Bold);
            _baseUiFontSizes[label] = resolvedFontSize;
            if (_largeTextEnabled)
            {
                label.fontSize = resolvedFontSize + 1;
                label.resizeTextMinSize = Mathf.Max(9, label.fontSize - 3);
                label.resizeTextMaxSize = label.fontSize;
            }
            return label;
        }

        private Button CreateUiButton(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, string text, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateUiRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.28f, 0.31f, 0.94f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var strongAction = name.Contains("Start Wave") || name.Contains("Deploy") ||
                               name.Contains("Command") || name.Contains("Next Mission") ||
                               name.Contains("Restart");
            TDUiWorldSkin.ApplyButton(button, strongAction ? TDUiWorldSkin.Ember : TDUiWorldSkin.Brass, strongAction);
            TDUiFocusVisual.Attach(button);

            var label = CreateUiText("Label", rect, Vector2.zero, sizeDelta, text, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.96f, 0.95f, 0.90f, 1f));
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            return button;
        }

        private RectTransform CreateUiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private void SetUiText(Text label, string text)
        {
            if (label != null)
            {
                TDLocalization.SetLabel(label, text ?? string.Empty, _uiFont);
            }
        }

    }
}
