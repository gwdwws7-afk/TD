using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TD
{
    public enum TDBattleFeedbackKind
    {
        Hit = 0,
        ArmorBreak = 1,
        Slow = 2,
        Specialization = 3,
        Resonance = 4,
        Leak = 5,
        CriticalHit = 6,
        BossDamage = 7
    }

    public enum TDBattleFeedbackTier
    {
        Routine = 0,
        Tactical = 1,
        Critical = 2
    }

    public enum TDBattleCinematicKind
    {
        WaveTransition = 0,
        DangerousLane = 1,
        BossPhase = 2,
        DefenseBreach = 3
    }

    public sealed class TDBattlePresentation : MonoBehaviour
    {
        private sealed class FloatingSignal
        {
            public RectTransform root;
            public Text label;
            public TDBattleFeedbackKind kind;
            public TDBattleFeedbackTier tier;
            public float timer;
            public float duration;
            public float riseSpeed;
        }

        private const int FeedbackKindCount = 8;
        private const int CinematicKindCount = 4;
        private const int MaxFloatingSignals = 12;
        private const float RoutineHitInterval = 0.16f;
        private const float TacticalSignalInterval = 0.28f;

        private readonly List<FloatingSignal> _floatingSignals = new();
        private readonly float[] _nextSignalTimes = new float[FeedbackKindCount];
        private readonly int[] _emittedCounts = new int[FeedbackKindCount];
        private readonly int[] _cinematicCounts = new int[CinematicKindCount];

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Font _font;
        private Camera _worldCamera;
        private RectTransform _signalRoot;
        private RectTransform _cinematicRoot;
        private CanvasGroup _cinematicGroup;
        private Image _cinematicStripe;
        private Image _cinematicMarker;
        private Text _cinematicTitle;
        private Text _cinematicBody;
        private RectTransform _controlRoot;
        private readonly List<Button> _speedButtons = new();
        private readonly List<Text> _speedButtonLabels = new();
        private Button _markerButton;
        private Text _markerButtonLabel;
        private Button _largeTextButton;
        private Text _largeTextButtonLabel;
        private RectTransform _tutorialRoot;
        private Text _tutorialProgress;
        private Text _tutorialTitle;
        private Text _tutorialBody;
        private Button _tutorialConfirmButton;
        private Text _tutorialConfirmLabel;
        private Button _tutorialSkipButton;
        private float _cinematicTimer;
        private float _cinematicDuration;
        private int _cinematicCount;
        private int _cinematicPriority;
        private int _maxSignalCharacters;
        private int _maximumObservedSignals;
        private int _suppressedSignalCount;
        private float _maximumSignalDuration;
        private float _maximumSignalAlpha;
        private TDBattleCinematicKind _activeCinematicKind;
        private bool _markersEnabled;
        private bool _largeTextEnabled;
        private bool _subtitlesEnabled = true;
        private bool _captionsEnabled = true;
        private bool _modalSuppressed;
        private bool _initialized;
        private Action<float> _playbackCallback;
        private Action _markerCallback;
        private Action _largeTextCallback;
        private Action _tutorialConfirmCallback;
        private Action _tutorialSkipCallback;

        public bool IsInitialized => _initialized;
        public bool MarkersEnabled => _markersEnabled;
        public bool LargeTextEnabled => _largeTextEnabled;
        public int CinematicCount => _cinematicCount;
        public int ActiveSignalCount => _floatingSignals.Count;
        public int MaximumObservedSignals => _maximumObservedSignals;
        public int SuppressedSignalCount => _suppressedSignalCount;
        public float MaximumSignalDuration => _maximumSignalDuration;
        public float MaximumSignalAlpha => _maximumSignalAlpha;
        public int MaxSignalCharacters => _maxSignalCharacters;
        public bool SignalLayerVisible => _signalRoot != null && _signalRoot.gameObject.activeSelf;
        public bool CinematicVisible => _cinematicRoot != null && _cinematicRoot.gameObject.activeSelf;

        public void Initialize(
            Canvas canvas,
            Font font,
            Camera worldCamera,
            Action<float> playbackCallback,
            Action markerCallback,
            Action largeTextCallback,
            Action tutorialConfirmCallback,
            Action tutorialSkipCallback,
            bool markersEnabled,
            bool largeTextEnabled)
        {
            if (_initialized || canvas == null || font == null)
            {
                return;
            }

            _canvas = canvas;
            _canvasRect = canvas.transform as RectTransform;
            _font = TDUiWorldSkin.ResolveFont(font);
            _worldCamera = worldCamera;
            _playbackCallback = playbackCallback;
            _markerCallback = markerCallback;
            _largeTextCallback = largeTextCallback;
            _tutorialConfirmCallback = tutorialConfirmCallback;
            _tutorialSkipCallback = tutorialSkipCallback;
            _markersEnabled = markersEnabled;
            _largeTextEnabled = largeTextEnabled;

            BuildSignalLayer();
            BuildCinematicLayer();
            BuildPlaybackControls();
            BuildTutorialPanel();
            SetAccessibilityState(markersEnabled, largeTextEnabled);
            _initialized = true;
        }

        public void Tick(bool modalOpen)
        {
            if (!_initialized)
            {
                return;
            }

            _modalSuppressed = modalOpen;
            if (_controlRoot != null)
            {
                _controlRoot.gameObject.SetActive(!modalOpen);
            }

            if (modalOpen)
            {
                ClearCombatOverlays();
                return;
            }

            if (_signalRoot != null)
            {
                _signalRoot.gameObject.SetActive(true);
            }

            UpdateFloatingSignals();
            UpdateCinematic();
        }

        public void EmitFeedback(TDBattleFeedbackKind kind, Vector3 worldPosition, string detail, TDBattleFeedbackTier tier)
        {
            if (!_initialized || _canvasRect == null || _modalSuppressed)
            {
                return;
            }

            var index = Mathf.Clamp((int)kind, 0, _nextSignalTimes.Length - 1);
            var now = Time.unscaledTime;
            var interval = ResolveSignalInterval(kind);
            if (tier != TDBattleFeedbackTier.Critical && now < _nextSignalTimes[index])
            {
                _suppressedSignalCount++;
                return;
            }

            _nextSignalTimes[index] = now + interval;
            if (SpawnFloatingSignal(kind, worldPosition, detail, tier))
            {
                _emittedCounts[index]++;
            }
        }

        public void ShowCinematic(string marker, string title, string body, TDBattleFeedbackTier tier, float duration)
        {
            ShowCinematic(InferCinematicKind(title), marker, title, body, tier, duration);
        }

        public void ShowCinematic(
            TDBattleCinematicKind kind,
            string marker,
            string title,
            string body,
            TDBattleFeedbackTier tier,
            float duration)
        {
            if (!_initialized || _cinematicRoot == null || _modalSuppressed)
            {
                return;
            }

            var safeDuration = Mathf.Clamp(duration, 0.65f, 1.35f);
            var incomingPriority = (int)tier;
            var currentPriority = _cinematicRoot.gameObject.activeSelf ? ResolveCinematicPriority() : -1;
            if (_cinematicTimer > 0.25f && incomingPriority < currentPriority)
            {
                return;
            }

            var color = ResolveCinematicColor(kind);
            _cinematicMarker.sprite = TDUiP132Art.LoadVirtualSprite(
                TDUiP132Art.IconPath(ResolveCinematicIcon(kind)));
            _cinematicMarker.color = Color.white;
            TDLocalization.SetLabel(_cinematicTitle, string.IsNullOrWhiteSpace(title) ? "TACTICAL UPDATE" : title.Trim().ToUpperInvariant(), _font);
            TDLocalization.SetLabel(_cinematicBody, _subtitlesEnabled ? body ?? string.Empty : string.Empty, _font);
            _cinematicStripe.color = color;
            _cinematicRoot.gameObject.SetActive(true);
            _cinematicPriority = incomingPriority;
            _activeCinematicKind = kind;
            _cinematicTimer = safeDuration;
            _cinematicDuration = safeDuration;
            _cinematicCount++;
            _cinematicCounts[Mathf.Clamp((int)kind, 0, _cinematicCounts.Length - 1)]++;
        }

        public int GetCinematicCount(TDBattleCinematicKind kind)
        {
            return _cinematicCounts[Mathf.Clamp((int)kind, 0, _cinematicCounts.Length - 1)];
        }

        public int GetFeedbackCount(TDBattleFeedbackKind kind)
        {
            return _emittedCounts[Mathf.Clamp((int)kind, 0, _emittedCounts.Length - 1)];
        }

        public void ResetDiagnostics(bool clearSignals)
        {
            Array.Clear(_emittedCounts, 0, _emittedCounts.Length);
            Array.Clear(_cinematicCounts, 0, _cinematicCounts.Length);
            Array.Clear(_nextSignalTimes, 0, _nextSignalTimes.Length);
            _cinematicCount = 0;
            _maxSignalCharacters = 0;
            _maximumObservedSignals = clearSignals ? 0 : _floatingSignals.Count;
            _suppressedSignalCount = 0;
            _maximumSignalDuration = 0f;
            _maximumSignalAlpha = 0f;
            if (clearSignals)
            {
                ClearCombatOverlays();
            }
        }

        public void SetPlaybackState(float speed, bool paused)
        {
            if (!_initialized)
            {
                return;
            }

            for (var i = 0; i < _speedButtons.Count; i++)
            {
                var selected = paused ? i == 0 : Mathf.Approximately(speed, i);
                var image = _speedButtons[i].targetGraphic as Image;
                if (image != null)
                {
                    image.color = selected
                        ? new Color(0.86f, 0.70f, 0.28f, 0.98f)
                        : new Color(0.18f, 0.25f, 0.28f, 0.94f);
                }

                if (i < _speedButtonLabels.Count && _speedButtonLabels[i] != null)
                {
                    _speedButtonLabels[i].color = selected
                        ? new Color(0.08f, 0.10f, 0.11f, 1f)
                        : new Color(0.92f, 0.96f, 0.98f, 1f);
                }
            }
        }

        public void SetAccessibilityState(bool markersEnabled, bool largeTextEnabled)
        {
            _markersEnabled = markersEnabled;
            _largeTextEnabled = largeTextEnabled;
            if (_markerButtonLabel != null)
            {
                TDLocalization.SetLabel(_markerButtonLabel, markersEnabled ? "MARKS ON" : "MARKS OFF", _font);
            }

            if (_largeTextButtonLabel != null)
            {
                TDLocalization.SetLabel(_largeTextButtonLabel, largeTextEnabled ? "Aa+" : "Aa", _font);
            }

            SetButtonSelected(_markerButton, markersEnabled);
            SetButtonSelected(_largeTextButton, largeTextEnabled);
            ApplyTutorialTextScale();
        }

        private void ApplyTutorialTextScale()
        {
            ApplyAccessibleTextSize(_tutorialProgress, 11, _largeTextEnabled ? 13 : 11);
            ApplyAccessibleTextSize(_tutorialTitle, 14, _largeTextEnabled ? 16 : 14);
            ApplyAccessibleTextSize(_tutorialBody, 11, _largeTextEnabled ? 14 : 11);
            ApplyAccessibleTextSize(_tutorialConfirmLabel, 11, _largeTextEnabled ? 13 : 11);
        }

        private static void ApplyAccessibleTextSize(Text label, int normalSize, int targetSize)
        {
            if (label == null)
            {
                return;
            }

            label.fontSize = targetSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, normalSize - 1);
            label.resizeTextMaxSize = targetSize;
        }

        public void SetCaptionState(bool subtitlesEnabled, bool captionsEnabled)
        {
            _subtitlesEnabled = subtitlesEnabled;
            _captionsEnabled = captionsEnabled;
        }

        public void RefreshLocalization()
        {
            if (_canvas != null)
            {
                TDLocalization.RefreshLabels(_canvas.gameObject, _font);
            }

            SetAccessibilityState(_markersEnabled, _largeTextEnabled);
        }

        public void SetTutorial(string progress, string title, string body, bool visible, bool confirmEnabled, string confirmLabel)
        {
            if (_tutorialRoot == null)
            {
                return;
            }

            _tutorialRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            TDLocalization.SetLabel(_tutorialProgress, progress ?? string.Empty, _font);
            TDLocalization.SetLabel(_tutorialTitle, title ?? string.Empty, _font);
            TDLocalization.SetLabel(_tutorialBody, body ?? string.Empty, _font);
            _tutorialConfirmButton.gameObject.SetActive(confirmEnabled);
            _tutorialConfirmButton.interactable = confirmEnabled;
            TDLocalization.SetLabel(_tutorialConfirmLabel, string.IsNullOrWhiteSpace(confirmLabel) ? "CONFIRM" : confirmLabel, _font);
        }

        public string BuildAuditReport()
        {
            return
                $"p9.presentation.initialized={_initialized}\n" +
                $"p9.presentation.markers={_markersEnabled}\n" +
                $"p9.presentation.largeText={_largeTextEnabled}\n" +
                $"p12.3.presentation.subtitles={_subtitlesEnabled}\n" +
                $"p12.3.presentation.captions={_captionsEnabled}\n" +
                $"p9.presentation.feedback.hit={_emittedCounts[(int)TDBattleFeedbackKind.Hit]}\n" +
                $"p9.presentation.feedback.break={_emittedCounts[(int)TDBattleFeedbackKind.ArmorBreak]}\n" +
                $"p9.presentation.feedback.slow={_emittedCounts[(int)TDBattleFeedbackKind.Slow]}\n" +
                $"p9.presentation.feedback.specialization={_emittedCounts[(int)TDBattleFeedbackKind.Specialization]}\n" +
                $"p9.presentation.feedback.resonance={_emittedCounts[(int)TDBattleFeedbackKind.Resonance]}\n" +
                $"p9.presentation.feedback.leak={_emittedCounts[(int)TDBattleFeedbackKind.Leak]}\n" +
                $"p13.4.presentation.feedback.critical={_emittedCounts[(int)TDBattleFeedbackKind.CriticalHit]}\n" +
                $"p13.4.presentation.feedback.boss={_emittedCounts[(int)TDBattleFeedbackKind.BossDamage]}\n" +
                $"p13.4.presentation.signals={_floatingSignals.Count}/{MaxFloatingSignals} max={_maximumObservedSignals} suppressed={_suppressedSignalCount}\n" +
                $"p13.4.presentation.signalDuration={_maximumSignalDuration:0.00}\n" +
                $"p13.4.presentation.signalAlpha={_maximumSignalAlpha:0.00}\n" +
                $"p13.4.presentation.cinematics.wave={_cinematicCounts[(int)TDBattleCinematicKind.WaveTransition]}\n" +
                $"p13.4.presentation.cinematics.lane={_cinematicCounts[(int)TDBattleCinematicKind.DangerousLane]}\n" +
                $"p13.4.presentation.cinematics.boss={_cinematicCounts[(int)TDBattleCinematicKind.BossPhase]}\n" +
                $"p13.4.presentation.cinematics.breach={_cinematicCounts[(int)TDBattleCinematicKind.DefenseBreach]}\n" +
                $"p12.1.presentation.maxSignalChars={_maxSignalCharacters}\n" +
                $"p9.presentation.cinematics={_cinematicCount}";
        }

        private void BuildSignalLayer()
        {
            _signalRoot = CreateRect(
                "Combat Feedback Signals",
                _canvas.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            _signalRoot.SetAsLastSibling();
        }

        private void BuildCinematicLayer()
        {
            _cinematicRoot = CreatePanel(
                "Combat Cinematic",
                _canvas.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(28f, -20f),
                new Vector2(400f, 66f),
                new Color(0.018f, 0.022f, 0.024f, 0.92f));
            _cinematicGroup = _cinematicRoot.gameObject.AddComponent<CanvasGroup>();
            _cinematicGroup.blocksRaycasts = false;
            _cinematicGroup.interactable = false;
            _cinematicStripe = CreateImage("Signal Stripe", _cinematicRoot, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 0f), Color.white);
            _cinematicMarker = CreateSpriteImage("Signal Marker", _cinematicRoot, new Vector2(18f, -10f), new Vector2(42f, 42f), TDUiP132Icon.Wave);
            _cinematicTitle = CreateText("Signal Title", _cinematicRoot, new Vector2(70f, -7f), new Vector2(316f, 22f), string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            _cinematicBody = CreateText("Signal Body", _cinematicRoot, new Vector2(70f, -32f), new Vector2(316f, 24f), string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.82f, 0.90f, 0.94f, 1f));
            _cinematicRoot.gameObject.SetActive(false);
        }

        private void BuildPlaybackControls()
        {
            _controlRoot = CreatePanel(
                "Playback And Accessibility",
                _canvas.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(300f, 44f),
                new Color(0.025f, 0.032f, 0.036f, 0.90f));

            var labels = new[] { "II", "1x", "2x", "3x" };
            for (var i = 0; i < labels.Length; i++)
            {
                var captured = i;
                var button = CreateButton($"Playback {labels[i]}", _controlRoot, new Vector2(7f + (i * 42f), -7f), new Vector2(36f, 30f), labels[i], 11, () => _playbackCallback?.Invoke(captured));
                _speedButtons.Add(button);
                _speedButtonLabels.Add(button.GetComponentInChildren<Text>());
            }

            _markerButton = CreateButton("Colorblind Markers", _controlRoot, new Vector2(177f, -7f), new Vector2(76f, 30f), "MARKS", 9, () => _markerCallback?.Invoke());
            _markerButtonLabel = _markerButton.GetComponentInChildren<Text>();
            _largeTextButton = CreateButton("Large Text", _controlRoot, new Vector2(260f, -7f), new Vector2(33f, 30f), "Aa", 11, () => _largeTextCallback?.Invoke());
            _largeTextButtonLabel = _largeTextButton.GetComponentInChildren<Text>();
        }

        private void BuildTutorialPanel()
        {
            _tutorialRoot = CreatePanel(
                "Interactive Tutorial",
                _canvas.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 70f),
                new Vector2(440f, 118f),
                new Color(0.020f, 0.030f, 0.034f, 0.96f));
            _tutorialProgress = CreateText("Tutorial Progress", _tutorialRoot, new Vector2(14f, -8f), new Vector2(72f, 24f), "STEP", 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.72f, 0.28f, 1f));
            CreateSpriteImage("Tutorial Manual Icon", _tutorialRoot, new Vector2(88f, -5f), new Vector2(34f, 34f), TDUiP132Icon.Exposed);
            _tutorialTitle = CreateText("Tutorial Title", _tutorialRoot, new Vector2(128f, -5f), new Vector2(196f, 28f), string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.98f, 1f, 1f));
            _tutorialBody = CreateText("Tutorial Body", _tutorialRoot, new Vector2(14f, -38f), new Vector2(310f, 68f), string.Empty, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.82f, 0.91f, 0.96f, 1f));
            _tutorialConfirmButton = CreateButton("Tutorial Confirm", _tutorialRoot, new Vector2(336f, -40f), new Vector2(90f, 30f), "CONFIRM", 10, () => _tutorialConfirmCallback?.Invoke());
            _tutorialConfirmLabel = _tutorialConfirmButton.GetComponentInChildren<Text>();
            _tutorialSkipButton = CreateButton("Tutorial Skip", _tutorialRoot, new Vector2(336f, -78f), new Vector2(90f, 30f), "SKIP", 10, () => _tutorialSkipCallback?.Invoke());

            // TD-GP-004: the tutorial panel is informational — its background
            // and text must not swallow board clicks (build pad (8,1) sits
            // under it on the grayline maps, blocking every pointer build for
            // the whole tutorial). Confirm/Skip keep their own raycast targets.
            foreach (var graphic in _tutorialRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.GetComponent<Button>() == null)
                {
                    graphic.raycastTarget = false;
                }
            }

            _tutorialRoot.gameObject.SetActive(false);
        }

        private bool SpawnFloatingSignal(TDBattleFeedbackKind kind, Vector3 worldPosition, string detail, TDBattleFeedbackTier tier)
        {
            var kindLimit = ResolveSignalLimit(kind);
            var sameKindCount = 0;
            for (var i = 0; i < _floatingSignals.Count; i++)
            {
                if (_floatingSignals[i] != null && _floatingSignals[i].kind == kind)
                {
                    sameKindCount++;
                }
            }

            if (sameKindCount >= kindLimit)
            {
                var sameKindIndex = _floatingSignals.FindIndex(signal => signal != null && signal.kind == kind);
                if (tier == TDBattleFeedbackTier.Critical && sameKindIndex >= 0)
                {
                    DestroyFloatingSignal(sameKindIndex);
                }
                else
                {
                    _suppressedSignalCount++;
                    return false;
                }
            }

            if (_floatingSignals.Count >= MaxFloatingSignals)
            {
                var replaceIndex = tier == TDBattleFeedbackTier.Routine
                    ? -1
                    : _floatingSignals.FindIndex(signal => signal != null && signal.tier == TDBattleFeedbackTier.Routine);
                if (replaceIndex < 0)
                {
                    _suppressedSignalCount++;
                    return false;
                }

                DestroyFloatingSignal(replaceIndex);
            }

            var screenPoint = _worldCamera != null ? _worldCamera.WorldToScreenPoint(worldPosition) : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out var localPoint))
            {
                localPoint = Vector2.zero;
            }

            var signalLabel = BuildSignalLabel(kind, detail);
            _maxSignalCharacters = Mathf.Max(_maxSignalCharacters, signalLabel.Length);
            var root = CreatePanel(
                $"Feedback {kind}",
                _signalRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                localPoint + new Vector2(0f, 24f),
                tier == TDBattleFeedbackTier.Critical
                    ? new Vector2(116f, 30f)
                    : tier == TDBattleFeedbackTier.Tactical ? new Vector2(88f, 25f) : new Vector2(54f, 21f),
                ResolveBackdropColor(kind, tier));
            var labelColor = ResolveColor(kind);
            labelColor.a = Mathf.Min(labelColor.a, 0.96f);
            var label = CreateText("Label", root, Vector2.zero, Vector2.zero, signalLabel, tier == TDBattleFeedbackTier.Critical ? 14 : tier == TDBattleFeedbackTier.Tactical ? 12 : 11, FontStyle.Bold, TextAnchor.MiddleCenter, labelColor);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = Vector2.zero;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            var duration = ResolveSignalDuration(kind, tier);
            _maximumSignalDuration = Mathf.Max(_maximumSignalDuration, duration);
            _maximumSignalAlpha = Mathf.Max(_maximumSignalAlpha, labelColor.a);
            _floatingSignals.Add(new FloatingSignal
            {
                root = root,
                label = label,
                kind = kind,
                tier = tier,
                timer = duration,
                duration = duration,
                riseSpeed = tier == TDBattleFeedbackTier.Critical ? 34f : 24f
            });
            _maximumObservedSignals = Mathf.Max(_maximumObservedSignals, _floatingSignals.Count);
            return true;
        }

        private void UpdateFloatingSignals()
        {
            var delta = Time.unscaledDeltaTime;
            for (var i = _floatingSignals.Count - 1; i >= 0; i--)
            {
                var signal = _floatingSignals[i];
                if (signal == null || signal.root == null)
                {
                    _floatingSignals.RemoveAt(i);
                    continue;
                }

                signal.timer -= delta;
                signal.root.anchoredPosition += Vector2.up * (signal.riseSpeed * delta);
                var alpha = Mathf.Clamp01(signal.timer / Mathf.Max(0.01f, signal.duration * 0.44f));
                var image = signal.root.GetComponent<Image>();
                if (image != null)
                {
                    var color = image.color;
                    color.a = Mathf.Min(color.a, alpha * 0.86f);
                    image.color = color;
                }

                if (signal.label != null)
                {
                    var color = signal.label.color;
                    color.a = alpha;
                    signal.label.color = color;
                }

                if (signal.timer <= 0f)
                {
                    DestroyFloatingSignal(i);
                }
            }
        }

        private void ClearCombatOverlays()
        {
            for (var i = _floatingSignals.Count - 1; i >= 0; i--)
            {
                DestroyFloatingSignal(i);
            }

            if (_signalRoot != null)
            {
                _signalRoot.gameObject.SetActive(false);
            }

            if (_cinematicRoot != null)
            {
                _cinematicRoot.gameObject.SetActive(false);
            }

            _cinematicTimer = 0f;
            _cinematicDuration = 0f;
        }

        private void UpdateCinematic()
        {
            if (_cinematicRoot == null || !_cinematicRoot.gameObject.activeSelf)
            {
                return;
            }

            _cinematicTimer -= Time.unscaledDeltaTime;
            var elapsed = _cinematicDuration - _cinematicTimer;
            var fadeIn = Mathf.Clamp01(elapsed / 0.12f);
            var fadeOut = Mathf.Clamp01(_cinematicTimer / 0.20f);
            _cinematicGroup.alpha = Mathf.Min(fadeIn, fadeOut);
            _cinematicRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(-14f, -20f, fadeIn));
            var normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _cinematicDuration));
            if (_cinematicMarker != null)
            {
                var punch = 1f + (Mathf.Sin(Mathf.Clamp01(normalized * 4f) * Mathf.PI) * 0.14f);
                _cinematicMarker.rectTransform.localScale = Vector3.one * punch;
                var direction = _activeCinematicKind == TDBattleCinematicKind.DangerousLane ? -1f : 1f;
                _cinematicMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, direction * Mathf.Lerp(-7f, 0f, fadeIn));
            }

            if (_cinematicStripe != null)
            {
                _cinematicStripe.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(3f, 7f, fadeIn), 0f);
            }
            if (_cinematicTimer <= 0f)
            {
                _cinematicRoot.gameObject.SetActive(false);
            }
        }

        private int ResolveCinematicPriority()
        {
            return _cinematicPriority;
        }

        private void DestroyFloatingSignal(int index)
        {
            if (index < 0 || index >= _floatingSignals.Count)
            {
                return;
            }

            var item = _floatingSignals[index];
            if (item?.root != null)
            {
                Destroy(item.root.gameObject);
            }

            _floatingSignals.RemoveAt(index);
        }

        private string BuildSignalLabel(TDBattleFeedbackKind kind, string detail)
        {
            var safeDetail = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail.Trim();
            if (kind == TDBattleFeedbackKind.Hit)
            {
                return string.IsNullOrWhiteSpace(safeDetail) ? "+" : safeDetail;
            }

            if (kind == TDBattleFeedbackKind.CriticalHit)
            {
                return string.IsNullOrWhiteSpace(safeDetail) ? "CRIT" : $"CRIT {safeDetail}";
            }

            if (kind == TDBattleFeedbackKind.BossDamage)
            {
                return string.IsNullOrWhiteSpace(safeDetail) ? "BOSS" : $"BOSS {safeDetail}";
            }

            if (safeDetail.Length > 10)
            {
                var firstTokenEnd = safeDetail.IndexOf(' ');
                safeDetail = firstTokenEnd > 0 ? safeDetail.Substring(0, firstTokenEnd) : safeDetail;
                if (safeDetail.Length > 10)
                {
                    safeDetail = safeDetail.Substring(0, 10);
                }
            }

            var marker = _markersEnabled ? ResolveMarker(kind) : kind switch
            {
                TDBattleFeedbackKind.ArmorBreak => "BRK",
                TDBattleFeedbackKind.Slow => "SLW",
                TDBattleFeedbackKind.Specialization => "SPEC",
                TDBattleFeedbackKind.Resonance => "RSN",
                TDBattleFeedbackKind.Leak => "BREACH",
                TDBattleFeedbackKind.CriticalHit => "CRIT",
                TDBattleFeedbackKind.BossDamage => "BOSS",
                _ => string.Empty
            };
            return string.IsNullOrWhiteSpace(safeDetail) ? marker : $"{marker} {safeDetail}";
        }

        private static string ResolveMarker(TDBattleFeedbackKind kind)
        {
            return kind switch
            {
                TDBattleFeedbackKind.ArmorBreak => "[#]",
                TDBattleFeedbackKind.Slow => "[v]",
                TDBattleFeedbackKind.Specialization => "[*]",
                TDBattleFeedbackKind.Resonance => "[R]",
                TDBattleFeedbackKind.Leak => "[!]",
                TDBattleFeedbackKind.CriticalHit => "[X]",
                TDBattleFeedbackKind.BossDamage => "[B]",
                _ => "[+]"
            };
        }

        private Color ResolveColor(TDBattleFeedbackKind kind)
        {
            if (_markersEnabled)
            {
                return kind switch
                {
                    TDBattleFeedbackKind.ArmorBreak => new Color(1f, 0.73f, 0.25f, 1f),
                    TDBattleFeedbackKind.Slow => new Color(0.30f, 0.82f, 1f, 1f),
                    TDBattleFeedbackKind.Specialization => new Color(0.96f, 0.46f, 0.92f, 1f),
                    TDBattleFeedbackKind.Resonance => new Color(0.34f, 1f, 0.63f, 1f),
                    TDBattleFeedbackKind.Leak => new Color(1f, 0.32f, 0.24f, 1f),
                    TDBattleFeedbackKind.CriticalHit => new Color(1f, 0.92f, 0.38f, 1f),
                    TDBattleFeedbackKind.BossDamage => new Color(1f, 0.54f, 0.24f, 1f),
                    _ => new Color(0.92f, 0.98f, 1f, 1f)
                };
            }

            return kind switch
            {
                TDBattleFeedbackKind.ArmorBreak => new Color(1f, 0.62f, 0.22f, 1f),
                TDBattleFeedbackKind.Slow => new Color(0.52f, 0.86f, 1f, 1f),
                TDBattleFeedbackKind.Specialization => new Color(0.96f, 0.68f, 0.34f, 1f),
                TDBattleFeedbackKind.Resonance => new Color(1f, 0.84f, 0.30f, 1f),
                TDBattleFeedbackKind.Leak => new Color(1f, 0.38f, 0.28f, 1f),
                TDBattleFeedbackKind.CriticalHit => new Color(1f, 0.82f, 0.22f, 1f),
                TDBattleFeedbackKind.BossDamage => new Color(1f, 0.48f, 0.18f, 1f),
                _ => Color.white
            };
        }

        private Color ResolveBackdropColor(TDBattleFeedbackKind kind, TDBattleFeedbackTier tier)
        {
            var color = ResolveColor(kind);
            var alpha = tier == TDBattleFeedbackTier.Critical ? 0.68f : tier == TDBattleFeedbackTier.Tactical ? 0.20f : 0f;
            return new Color(color.r * 0.16f, color.g * 0.16f, color.b * 0.16f, alpha);
        }

        private static float ResolveSignalInterval(TDBattleFeedbackKind kind)
        {
            return kind switch
            {
                TDBattleFeedbackKind.Hit => RoutineHitInterval,
                TDBattleFeedbackKind.CriticalHit => 0.22f,
                TDBattleFeedbackKind.BossDamage => 0.30f,
                TDBattleFeedbackKind.Leak => 0.12f,
                _ => TacticalSignalInterval
            };
        }

        private static int ResolveSignalLimit(TDBattleFeedbackKind kind)
        {
            return kind switch
            {
                TDBattleFeedbackKind.Hit => 4,
                TDBattleFeedbackKind.CriticalHit => 2,
                TDBattleFeedbackKind.BossDamage => 2,
                TDBattleFeedbackKind.Leak => 2,
                _ => 2
            };
        }

        private static float ResolveSignalDuration(TDBattleFeedbackKind kind, TDBattleFeedbackTier tier)
        {
            var baseDuration = kind switch
            {
                TDBattleFeedbackKind.Hit => 0.42f,
                TDBattleFeedbackKind.CriticalHit => 0.62f,
                TDBattleFeedbackKind.BossDamage => 0.70f,
                TDBattleFeedbackKind.Leak => 1.02f,
                TDBattleFeedbackKind.Specialization => 0.82f,
                TDBattleFeedbackKind.Resonance => 0.88f,
                _ => 0.68f
            };
            return Mathf.Min(1.05f, baseDuration + (tier == TDBattleFeedbackTier.Critical ? 0.10f : 0f));
        }

        private static TDBattleCinematicKind InferCinematicKind(string title)
        {
            var safeTitle = title ?? string.Empty;
            if (safeTitle.IndexOf("BOSS", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TDBattleCinematicKind.BossPhase;
            }

            if (safeTitle.IndexOf("BREACH", StringComparison.OrdinalIgnoreCase) >= 0 ||
                safeTitle.IndexOf("DEFENSE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TDBattleCinematicKind.DefenseBreach;
            }

            if (safeTitle.IndexOf("LANE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                safeTitle.IndexOf("ROUTE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TDBattleCinematicKind.DangerousLane;
            }

            return TDBattleCinematicKind.WaveTransition;
        }

        private static TDUiP132Icon ResolveCinematicIcon(TDBattleCinematicKind kind)
        {
            return kind switch
            {
                TDBattleCinematicKind.DangerousLane => TDUiP132Icon.RouteSwitch,
                TDBattleCinematicKind.BossPhase => TDUiP132Icon.BossBreak,
                TDBattleCinematicKind.DefenseBreach => TDUiP132Icon.Integrity,
                _ => TDUiP132Icon.Wave
            };
        }

        private Color ResolveCinematicColor(TDBattleCinematicKind kind)
        {
            return kind switch
            {
                TDBattleCinematicKind.DangerousLane => ResolveColor(TDBattleFeedbackKind.Slow),
                TDBattleCinematicKind.BossPhase => ResolveColor(TDBattleFeedbackKind.BossDamage),
                TDBattleCinematicKind.DefenseBreach => ResolveColor(TDBattleFeedbackKind.Leak),
                _ => ResolveColor(TDBattleFeedbackKind.Resonance)
            };
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button?.targetGraphic is Image image)
            {
                image.color = selected
                    ? new Color(0.28f, 0.48f, 0.42f, 0.98f)
                    : new Color(0.18f, 0.25f, 0.28f, 0.94f);
            }
        }

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (name == "Combat Cinematic")
            {
                TDUiWorldSkin.ApplyPanel(rect, TDUiWorldSkin.Ember, true, true);
            }
            else if (name == "Playback And Accessibility")
            {
                TDUiWorldSkin.ApplyPanel(rect, TDUiWorldSkin.Brass, true);
            }
            else if (name == "Interactive Tutorial")
            {
                TDUiWorldSkin.ApplyPanel(rect, TDUiWorldSkin.Instrument);
            }
            return rect;
        }

        private Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Image CreateSpriteImage(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, TDUiP132Icon icon)
        {
            var rect = CreateRect(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                topLeft,
                sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = TDUiP132Art.LoadVirtualSprite(TDUiP132Art.IconPath(icon));
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, string value, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta);
            var label = rect.gameObject.AddComponent<Text>();
            var resolvedFontSize = Mathf.Max(11, fontSize);
            label.font = TDLocalization.ResolveFont(_font);
            label.fontSize = resolvedFontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color;
            TDLocalization.SetLabel(label, value ?? string.Empty, _font);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, resolvedFontSize - 2);
            label.resizeTextMaxSize = resolvedFontSize;
            label.raycastTarget = false;
            TDUiWorldSkin.ApplyText(label, fontStyle == FontStyle.Bold);
            return label;
        }

        private Button CreateButton(string name, Transform parent, Vector2 topLeft, Vector2 sizeDelta, string value, int fontSize, UnityAction onClick)
        {
            var rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.25f, 0.28f, 0.94f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var strongAction = name == "Tutorial Confirm";
            TDUiWorldSkin.ApplyButton(button, strongAction ? TDUiWorldSkin.Ember : TDUiWorldSkin.Brass, strongAction);
            TDUiFocusVisual.Attach(button);

            var label = CreateText("Label", rect, Vector2.zero, sizeDelta, value, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.94f, 0.97f, 0.98f, 1f));
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = Vector2.zero;
            return button;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
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
    }
}
