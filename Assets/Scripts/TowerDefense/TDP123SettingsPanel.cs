using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TD
{
    public sealed class TDP123SettingsBindings
    {
        public Func<bool> GetMarkers;
        public Action ToggleMarkers;
        public Func<bool> GetLargeText;
        public Action ToggleLargeText;
        public Func<bool> GetSubtitles;
        public Action ToggleSubtitles;
        public Func<bool> GetCaptions;
        public Action ToggleCaptions;
        public Func<float> GetUiScale;
        public Action<float> SetUiScale;
        public Func<float> GetMasterVolume;
        public Action<float> SetMasterVolume;
        public Func<float> GetMusicVolume;
        public Action<float> SetMusicVolume;
        public Func<float> GetEffectsVolume;
        public Action<float> SetEffectsVolume;
        public Action<TDUiLanguage> SetLanguage;
        public Action<bool> OpenStateChanged;
        public Action ResetDefaults;
    }

    [DisallowMultipleComponent]
    public sealed class TDP123SettingsPanel : MonoBehaviour
    {
        private readonly List<Button> _languageButtons = new();
        private readonly List<Button> _scaleButtons = new();
        private readonly Dictionary<TDInputAction, Button> _bindingButtons = new();
        private readonly Dictionary<TDInputAction, Text> _bindingLabels = new();
        private RectTransform _scrim;
        private RectTransform _panel;
        private Font _latinFont;
        private TDP123SettingsBindings _bindings;
        private Button _firstButton;
        private Button _markerButton;
        private Text _markerValue;
        private Button _largeTextButton;
        private Text _largeTextValue;
        private Button _subtitleButton;
        private Text _subtitleValue;
        private Button _captionButton;
        private Text _captionValue;
        private Text _gamepadStatus;
        private Slider _masterSlider;
        private Slider _musicSlider;
        private Slider _effectsSlider;
        private Text _masterValue;
        private Text _musicValue;
        private Text _effectsValue;
        private TDInputAction? _pendingRebind;
        private bool _initialized;

        public bool IsOpen => _scrim != null && _scrim.gameObject.activeSelf;
        public bool IsRebinding => _pendingRebind.HasValue;
        public bool IsInitialized => _initialized;
        public int BindingCount => _bindingButtons.Count;

        public void Initialize(Canvas canvas, Font latinFont, TDP123SettingsBindings bindings)
        {
            if (_initialized || canvas == null || latinFont == null || bindings == null)
            {
                return;
            }

            _latinFont = latinFont;
            _bindings = bindings;
            Build(canvas.transform);
            Refresh();
            SetOpen(false, false);
            _initialized = true;
        }

        public void Toggle()
        {
            SetOpen(!IsOpen);
        }

        public void Open()
        {
            SetOpen(true);
        }

        public void Close()
        {
            SetOpen(false);
        }

        public void Tick()
        {
            if (!IsOpen)
            {
                return;
            }

            if (_pendingRebind.HasValue && TDInputCompat.TryGetPressedKeyCode(out var key))
            {
                TDInputBindings.SetKey(_pendingRebind.Value, key);
                _pendingRebind = null;
                RefreshBindings();
            }

            if (!_pendingRebind.HasValue && TDInputCompat.GetGamepadButtonDown(TDGamepadButton.East))
            {
                Close();
            }

            if (_gamepadStatus != null)
            {
                TDLocalization.SetLabel(
                    _gamepadStatus,
                    TDInputCompat.HasGamepad ? "GAMEPAD  /  CONTROLLER READY" : "GAMEPAD  /  CONTROLLER NOT DETECTED",
                    _latinFont);
            }
        }

        public void Refresh()
        {
            if (_panel == null)
            {
                return;
            }

            TDLocalization.RefreshLabels(_panel.gameObject, _latinFont);
            RefreshSegmentedButtons();
            RefreshToggle(_markerButton, _markerValue, _bindings.GetMarkers?.Invoke() ?? true);
            RefreshToggle(_largeTextButton, _largeTextValue, _bindings.GetLargeText?.Invoke() ?? false);
            RefreshToggle(_subtitleButton, _subtitleValue, _bindings.GetSubtitles?.Invoke() ?? true);
            RefreshToggle(_captionButton, _captionValue, _bindings.GetCaptions?.Invoke() ?? true);
            SetSliderWithoutNotify(_masterSlider, _bindings.GetMasterVolume?.Invoke() ?? 1f, _masterValue);
            SetSliderWithoutNotify(_musicSlider, _bindings.GetMusicVolume?.Invoke() ?? 0.7f, _musicValue);
            SetSliderWithoutNotify(_effectsSlider, _bindings.GetEffectsVolume?.Invoke() ?? 1f, _effectsValue);
            RefreshBindings();
        }

        public string BuildAuditReport()
        {
            var selectableCount = _panel != null ? _panel.GetComponentsInChildren<Selectable>(true).Length : 0;
            var focusCount = _panel != null ? _panel.GetComponentsInChildren<TDUiFocusVisual>(true).Length : 0;
            return
                $"p12.3.settings.initialized={_initialized}\n" +
                $"p12.3.settings.language={TDLocalization.CurrentLanguage}\n" +
                $"p12.3.settings.bindings={_bindingButtons.Count}\n" +
                $"p12.3.settings.selectables={selectableCount}\n" +
                $"p12.3.settings.focusVisuals={focusCount}\n" +
                $"p12.3.settings.focusCoverage={selectableCount > 0 && focusCount == selectableCount}\n" +
                $"p12.3.settings.gamepadDetected={TDInputCompat.HasGamepad}";
        }

        private void SetOpen(bool open, bool notify = true)
        {
            if (_scrim == null || IsOpen == open)
            {
                return;
            }

            _pendingRebind = null;
            _scrim.gameObject.SetActive(open);
            if (open)
            {
                _scrim.SetAsLastSibling();
                Refresh();
                if (EventSystem.current != null && _firstButton != null)
                {
                    EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
                }
            }
            else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                     EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_scrim))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            if (notify)
            {
                _bindings.OpenStateChanged?.Invoke(open);
            }
        }

        private void Build(Transform canvasRoot)
        {
            _scrim = CreatePanel(
                "P12.3 Settings Scrim",
                canvasRoot,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.005f, 0.008f, 0.010f, 0.86f));
            _panel = CreatePanel(
                "P12.3 Command Options",
                _scrim,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(820f, 620f),
                new Color(0.025f, 0.034f, 0.038f, 0.99f));
            TDUiWorldSkin.ApplyPanel(_panel, TDUiWorldSkin.Brass);

            CreateText("Options Title", _panel, new Vector2(96f, -18f), new Vector2(292f, 34f), "COMMAND OPTIONS", 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.92f, 0.78f, 1f));
            CreateText("Language Label", _panel, new Vector2(444f, -20f), new Vector2(110f, 30f), "LANGUAGE", 11, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.70f, 0.82f, 0.88f, 1f));
            _languageButtons.Add(CreateButton("English Language", _panel, new Vector2(566f, -18f), new Vector2(104f, 32f), "ENGLISH", 10, () => SetLanguage(TDUiLanguage.English)));
            _languageButtons.Add(CreateButton("Chinese Language", _panel, new Vector2(680f, -18f), new Vector2(112f, 32f), "SIMPLIFIED CHINESE", 10, () => SetLanguage(TDUiLanguage.SimplifiedChinese)));
            _firstButton = _languageButtons[0];
            CreateRule("Header Rule", _panel, new Vector2(28f, -66f), new Vector2(764f, 2f), TDUiWorldSkin.Brass);

            CreateText("Accessibility Header", _panel, new Vector2(28f, -86f), new Vector2(336f, 24f), "ACCESSIBILITY", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.38f, 0.86f, 0.96f, 1f));
            _markerButton = CreateToggleRow("Marker Toggle", _panel, new Vector2(28f, -122f), "COLOR-INDEPENDENT MARKERS", () => _bindings.ToggleMarkers?.Invoke(), out _markerValue);
            _largeTextButton = CreateToggleRow("Large Text Toggle", _panel, new Vector2(28f, -168f), "LARGE TEXT", () => _bindings.ToggleLargeText?.Invoke(), out _largeTextValue);
            _subtitleButton = CreateToggleRow("Subtitle Toggle", _panel, new Vector2(28f, -214f), "SUBTITLES", () => _bindings.ToggleSubtitles?.Invoke(), out _subtitleValue);
            _captionButton = CreateToggleRow("Caption Toggle", _panel, new Vector2(28f, -260f), "SOUND CAPTIONS", () => _bindings.ToggleCaptions?.Invoke(), out _captionValue);

            CreateText("Scale Header", _panel, new Vector2(28f, -318f), new Vector2(120f, 24f), "UI SCALE", 12, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.76f, 0.88f, 0.92f, 1f));
            var scales = new[] { 1f, 1.1f, 1.2f };
            for (var i = 0; i < scales.Length; i++)
            {
                var scale = scales[i];
                _scaleButtons.Add(CreateButton($"UI Scale {scale:0.0}", _panel, new Vector2(158f + (i * 76f), -314f), new Vector2(68f, 32f), $"{Mathf.RoundToInt(scale * 100f)}%", 10, () => SetUiScale(scale)));
            }

            CreateText("Controller Header", _panel, new Vector2(28f, -370f), new Vector2(336f, 24f), "KEYBOARD BINDINGS", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.38f, 0.86f, 0.96f, 1f));
            var actions = TDInputBindings.RebindableActions;
            for (var i = 0; i < actions.Count; i++)
            {
                BuildBindingRow(actions[i], i);
            }

            CreateRule("Column Rule", _panel, new Vector2(398f, -86f), new Vector2(2f, 474f), new Color(0.36f, 0.52f, 0.58f, 0.54f));
            CreateText("Audio Header", _panel, new Vector2(430f, -86f), new Vector2(362f, 24f), "AUDIO", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.68f, 0.28f, 1f));
            _masterSlider = CreateSliderRow("Master Volume", _panel, new Vector2(430f, -126f), "MASTER VOLUME", value => _bindings.SetMasterVolume?.Invoke(value), out _masterValue);
            _musicSlider = CreateSliderRow("Music Volume", _panel, new Vector2(430f, -202f), "MUSIC VOLUME", value => _bindings.SetMusicVolume?.Invoke(value), out _musicValue);
            _effectsSlider = CreateSliderRow("Effects Volume", _panel, new Vector2(430f, -278f), "EFFECTS VOLUME", value => _bindings.SetEffectsVolume?.Invoke(value), out _effectsValue);

            CreateP132Icon("Gamepad Icon", _panel, new Vector2(430f, -366f), new Vector2(30f, 30f), TDUiP132Icon.Gamepad);
            CreateText("Gamepad Header", _panel, new Vector2(466f, -372f), new Vector2(326f, 24f), "GAMEPAD", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.96f, 0.68f, 0.28f, 1f));
            _gamepadStatus = CreateText("Gamepad Status", _panel, new Vector2(430f, -410f), new Vector2(362f, 44f), "GAMEPAD  /  CONTROLLER NOT DETECTED", 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.80f, 0.91f, 0.96f, 1f));
            CreateText("Gamepad Layout", _panel, new Vector2(430f, -462f), new Vector2(362f, 76f), "A  CONFIRM\nB  BACK\nY  START WAVE\nX  SCENARIO\nLB / RB  SPEED", 11, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.78f, 0.88f, 0.92f, 1f));

            var resetButton = CreateButton("Reset Options", _panel, new Vector2(430f, -554f), new Vector2(166f, 42f), "RESET DEFAULTS", 12, ResetDefaults);
            var closeButton = CreateButton("Close Options", _panel, new Vector2(616f, -554f), new Vector2(176f, 42f), "CLOSE OPTIONS", 13, Close);
            _ = resetButton;
            _ = closeButton;
        }

        private void BuildBindingRow(TDInputAction action, int index)
        {
            var row = index % 3;
            var column = index / 3;
            var x = 28f + (column * 184f);
            var y = -404f - (row * 48f);
            CreateText($"Binding {action} Label", _panel, new Vector2(x, y), new Vector2(106f, 32f), TDInputBindings.GetActionLabel(action), 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.88f, 0.92f, 1f));
            var button = CreateButton($"Binding {action}", _panel, new Vector2(x + 108f, y), new Vector2(68f, 32f), TDInputBindings.GetKeyLabel(action), 10, () => BeginRebind(action));
            _bindingButtons[action] = button;
            _bindingLabels[action] = button.GetComponentInChildren<Text>();
        }

        private Button CreateToggleRow(string name, Transform parent, Vector2 topLeft, string labelText, Action onClick, out Text valueText)
        {
            CreateText(name + " Label", parent, topLeft, new Vector2(240f, 36f), labelText, 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.82f, 0.90f, 0.94f, 1f));
            var button = CreateButton(name, parent, new Vector2(topLeft.x + 252f, topLeft.y), new Vector2(104f, 36f), "ON", 11, () =>
            {
                onClick?.Invoke();
                Refresh();
            });
            valueText = button.GetComponentInChildren<Text>();
            return button;
        }

        private Slider CreateSliderRow(string name, Transform parent, Vector2 topLeft, string labelText, Action<float> changed, out Text valueText)
        {
            CreateText(name + " Label", parent, topLeft, new Vector2(240f, 22f), labelText, 11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.82f, 0.90f, 0.94f, 1f));
            valueText = CreateText(name + " Value", parent, new Vector2(topLeft.x + 278f, topLeft.y), new Vector2(84f, 22f), "100%", 11, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.98f, 0.82f, 0.46f, 1f));
            var outputLabel = valueText;
            var sliderRoot = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(topLeft.x, topLeft.y - 32f), new Vector2(362f, 24f));
            var background = CreatePanel(name + " Track", sliderRoot, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, 8f), new Color(0.08f, 0.11f, 0.12f, 1f));
            var fillArea = CreateRect(name + " Fill Area", sliderRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, 0f));
            var fill = CreatePanel(name + " Fill", fillArea, Vector2.zero, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, -18f), new Color(0.96f, 0.58f, 0.18f, 1f));
            var handleArea = CreateRect(name + " Handle Area", sliderRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, 0f));
            var handle = CreatePanel(name + " Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f), new Color(0.90f, 0.82f, 0.60f, 1f));
            TDUiWorldSkin.ApplyRule(background.GetComponent<Image>(), new Color(0.52f, 0.58f, 0.58f, 0.96f));
            TDUiWorldSkin.ApplyRule(fill.GetComponent<Image>(), new Color(1f, 0.58f, 0.18f, 1f));
            TDUiWorldSkin.ApplySliderHandle(handle);
            background.GetComponent<Image>().raycastTarget = true;
            var slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(value =>
            {
                changed?.Invoke(value);
                TDLocalization.SetLabel(outputLabel, $"{Mathf.RoundToInt(value * 100f)}%", _latinFont);
            });
            TDUiFocusVisual.Attach(slider);
            return slider;
        }

        private void SetLanguage(TDUiLanguage language)
        {
            _bindings.SetLanguage?.Invoke(language);
            Refresh();
        }

        private void SetUiScale(float scale)
        {
            _bindings.SetUiScale?.Invoke(scale);
            RefreshSegmentedButtons();
        }

        private void ResetDefaults()
        {
            TDInputBindings.ResetDefaults();
            _bindings.ResetDefaults?.Invoke();
            Refresh();
        }

        private void BeginRebind(TDInputAction action)
        {
            _pendingRebind = action;
            RefreshBindings();
        }

        private void RefreshBindings()
        {
            foreach (var pair in _bindingLabels)
            {
                var source = _pendingRebind.HasValue && _pendingRebind.Value == pair.Key
                    ? "PRESS A KEY"
                    : TDInputBindings.GetKeyLabel(pair.Key);
                TDLocalization.SetLabel(pair.Value, source, _latinFont);
            }
        }

        private void RefreshSegmentedButtons()
        {
            for (var i = 0; i < _languageButtons.Count; i++)
            {
                SetButtonSelected(_languageButtons[i], i == (int)TDLocalization.CurrentLanguage);
            }

            var currentScale = _bindings.GetUiScale?.Invoke() ?? 1f;
            var scales = new[] { 1f, 1.1f, 1.2f };
            for (var i = 0; i < _scaleButtons.Count && i < scales.Length; i++)
            {
                SetButtonSelected(_scaleButtons[i], Mathf.Approximately(currentScale, scales[i]));
            }
        }

        private void RefreshToggle(Button button, Text value, bool enabled)
        {
            TDLocalization.SetLabel(value, enabled ? "ON" : "OFF", _latinFont);
            SetButtonSelected(button, enabled);
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button?.targetGraphic is Image image)
            {
                image.color = selected
                    ? new Color(0.56f, 0.38f, 0.12f, 1f)
                    : new Color(0.12f, 0.16f, 0.17f, 0.98f);
            }
        }

        private static void SetSliderWithoutNotify(Slider slider, float value, Text valueText)
        {
            if (slider == null)
            {
                return;
            }

            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            if (valueText != null)
            {
                TDLocalization.SetLabel(valueText, $"{Mathf.RoundToInt(value * 100f)}%");
            }
        }

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text CreateText(string name, Transform parent, Vector2 topLeft, Vector2 size, string source, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            var rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, size);
            var label = rect.gameObject.AddComponent<Text>();
            var resolvedFontSize = Mathf.Max(11, fontSize);
            label.font = TDLocalization.ResolveFont(_latinFont);
            label.fontSize = resolvedFontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = resolvedFontSize <= 12;
            label.resizeTextMinSize = Mathf.Max(9, resolvedFontSize - 2);
            label.resizeTextMaxSize = resolvedFontSize;
            label.raycastTarget = false;
            TDLocalization.SetLabel(label, source, _latinFont);
            TDUiWorldSkin.ApplyText(label, style == FontStyle.Bold);
            return label;
        }

        private Image CreateP132Icon(string name, Transform parent, Vector2 topLeft, Vector2 size, TDUiP132Icon icon)
        {
            var rect = CreateRect(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                topLeft,
                size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = TDUiP132Art.LoadVirtualSprite(TDUiP132Art.IconPath(icon));
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private Button CreateButton(string name, Transform parent, Vector2 topLeft, Vector2 size, string source, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.17f, 0.98f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            TDUiWorldSkin.ApplyButton(button, TDUiWorldSkin.Brass);
            TDUiFocusVisual.Attach(button);
            var label = CreateText("Label", rect, Vector2.zero, Vector2.zero, source, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.96f, 0.95f, 0.90f, 1f));
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = Vector2.zero;
            return button;
        }

        private static void CreateRule(string name, Transform parent, Vector2 topLeft, Vector2 size, Color color)
        {
            var rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            TDUiWorldSkin.ApplyRule(image, color, size.y > size.x);
        }
    }
}
