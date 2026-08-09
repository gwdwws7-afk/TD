using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TD
{
    public enum TDExamPresentationStage
    {
        Dormant = 0,
        Opening = 1,
        Escalation = 2,
        Decision = 3,
        Ending = 4
    }

    public sealed class TDExamPresentationProfile
    {
        public int levelIndex;
        public string identityId;
        public string marker;
        public string openingTitle;
        public string openingBody;
        public string escalationTitle;
        public string escalationBody;
        public string decisionTitle;
        public string decisionBody;
        public string failureSignature;
        public string victoryEnding;
        public string defeatEnding;
        public string deviceResourcePath;
        public string fallbackResourcePath;
        public Vector2Int deviceCell;
        public Vector2 deviceOffset;
        public float deviceCoverage;
        public Color accent;
    }

    public static class TDExamPresentationCatalog
    {
        private static readonly Dictionary<int, TDExamPresentationProfile> Profiles = new()
        {
            [5] = new TDExamPresentationProfile
            {
                levelIndex = 5,
                identityId = "reserve_window",
                marker = "[R]",
                openingTitle = "DEPOT CLOCK LIVE",
                openingBody = "BANK THE RESERVE  /  HOLD BOTH RAILS",
                escalationTitle = "TRAIN COMMITTED",
                escalationBody = "ARRIVAL WINDOW OPEN  /  DO NOT STRIP A FLANK",
                decisionTitle = "RESERVE EXAM",
                decisionBody = "WAIT FOR DELIVERY  /  OR DISPATCH LIGHT",
                failureSignature = "EARLY DISPATCH  /  EMPTY FLANK",
                victoryEnding = "DEPOT TIMETABLE HELD",
                defeatEnding = "RESERVE WINDOW MISSED",
                deviceResourcePath = "Art/Exam/P12/device_reserve_train",
                fallbackResourcePath = "Art/prop_signal_post_a",
                deviceCell = new Vector2Int(12, 1),
                deviceOffset = new Vector2(0.12f, -0.04f),
                deviceCoverage = 1.24f,
                accent = new Color(0.26f, 0.82f, 0.88f, 1f)
            },
            [9] = new TDExamPresentationProfile
            {
                levelIndex = 9,
                identityId = "switch_commitment",
                marker = "[S]",
                openingTitle = "JUNCTION ARMED",
                openingBody = "READ THE SPLIT  /  PROTECT THE COMMIT",
                escalationTitle = "CROSS TRAFFIC",
                escalationBody = "ONE SWITCH  /  THREE PRESSURE LINES",
                decisionTitle = "ROUTE EXAM",
                decisionBody = "DIVERT BEFORE DISPATCH  /  HOLD THE NEW LANE",
                failureSignature = "LATE SWITCH  /  COVERAGE GAP",
                victoryEnding = "SWITCHBACK SECURED",
                defeatEnding = "ROUTE COMMITMENT BROKE",
                deviceResourcePath = "Art/Exam/P12/device_canyon_switch",
                fallbackResourcePath = "Art/prop_signal_post_b",
                deviceCell = new Vector2Int(7, 4),
                deviceOffset = new Vector2(0.08f, -0.18f),
                deviceCoverage = 1.05f,
                accent = new Color(0.34f, 0.90f, 0.48f, 1f)
            },
            [13] = new TDExamPresentationProfile
            {
                levelIndex = 13,
                identityId = "purge_timing",
                marker = "[K]",
                openingTitle = "KILN PRESSURE RISING",
                openingBody = "STACK THE WAVE  /  SAVE THE PURGE",
                escalationTitle = "BASIN SATURATED",
                escalationBody = "ARMOR CLUSTER FORMING  /  VENT WINDOW NARROW",
                decisionTitle = "PURGE EXAM",
                decisionBody = "BREAK THE DENSEST PACK  /  KEEP EXIT CONTROL",
                failureSignature = "PURGE MISTIMED  /  ARMOR INTACT",
                victoryEnding = "KILN PRESSURE VENTED",
                defeatEnding = "BASIN OVERRAN THE VENT",
                deviceResourcePath = "Art/Exam/P12/device_kiln_purge",
                fallbackResourcePath = "Art/anim/tower_siege_drill_00",
                deviceCell = new Vector2Int(8, 8),
                deviceOffset = new Vector2(-0.12f, 0.06f),
                deviceCoverage = 1.18f,
                accent = new Color(1.00f, 0.46f, 0.10f, 1f)
            },
            [17] = new TDExamPresentationProfile
            {
                levelIndex = 17,
                identityId = "phase_preparation",
                marker = "[P]",
                openingTitle = "TERMINUS ECHO ONLINE",
                openingBody = "ALIGN THE MATRIX  /  BANK ONE BREAK",
                escalationTitle = "ELITE PHASE BUILDING",
                escalationBody = "ECHOES MASK THE SURGE  /  WATCH THE CORE",
                decisionTitle = "PHASE DRILL",
                decisionBody = "EXPOSE THE ELITE  /  CANCEL THE THRESHOLD",
                failureSignature = "MATRIX DESYNC  /  BREAKER UNUSED",
                victoryEnding = "TERMINUS PHASE STABILIZED",
                defeatEnding = "ECHO SURGE BREACHED",
                deviceResourcePath = "Art/Exam/P12/device_phase_breaker",
                fallbackResourcePath = "Art/anim/tower_resonance_beacon_00",
                deviceCell = new Vector2Int(3, 7),
                deviceOffset = new Vector2(0.10f, -0.08f),
                deviceCoverage = 1.16f,
                accent = new Color(0.44f, 0.86f, 0.92f, 1f)
            },
            [20] = new TDExamPresentationProfile
            {
                levelIndex = 20,
                identityId = "final_convergence",
                marker = "[F]",
                openingTitle = "FINAL CONVERGENCE",
                openingBody = "MATRIARCH INBOUND  /  TWO PHASES TO BREAK",
                escalationTitle = "EMBERLINE COLLAPSING",
                escalationBody = "HOLD THREE ROUTES  /  CHARGE THE MATRIX",
                decisionTitle = "TERMINUS EXAM",
                decisionBody = "BREAK 70% AND 35%  /  CONVERGE ON COMMAND",
                failureSignature = "PHASE BREAK MISSED  /  CONVERGENCE LATE",
                victoryEnding = "THE LAST EMBER HELD",
                defeatEnding = "FINAL CONVERGENCE FAILED",
                deviceResourcePath = "Art/Exam/P12/device_phase_breaker",
                fallbackResourcePath = "Art/anim/tower_resonance_beacon_00",
                deviceCell = new Vector2Int(13, 1),
                deviceOffset = new Vector2(-0.10f, 0.08f),
                deviceCoverage = 1.34f,
                accent = new Color(0.98f, 0.24f, 0.14f, 1f)
            }
        };

        public static IReadOnlyCollection<int> ExamLevels => Profiles.Keys;

        public static bool TryGet(int levelIndex, out TDExamPresentationProfile profile)
        {
            return Profiles.TryGetValue(levelIndex, out profile);
        }
    }

    public sealed class TDExamScenarioDeviceView : MonoBehaviour
    {
        private readonly List<SpriteRenderer> _chargePips = new();
        private TDExamPresentationProfile _profile;
        private SpriteRenderer _body;
        private SpriteRenderer _glow;
        private Transform _bodyRoot;
        private Transform _ringRoot;
        private TDExamPresentationStage _stage;
        private float _activationPulse;
        private float _baseCoverage;
        private int _activationCount;

        public bool IsReady => _profile != null && _body != null && _body.sprite != null;
        public int LevelIndex => _profile?.levelIndex ?? 0;
        public TDExamPresentationStage Stage => _stage;
        public int ActivationCount => _activationCount;
        public int VisibleRendererCount => GetComponentsInChildren<SpriteRenderer>(true).Count(renderer => renderer != null && renderer.enabled);

        public void Initialize(TDExamPresentationProfile profile, Vector3 worldPosition, int maximumCharges)
        {
            _profile = profile;
            _baseCoverage = Mathf.Max(0.72f, profile.deviceCoverage);
            transform.position = worldPosition + new Vector3(profile.deviceOffset.x, profile.deviceOffset.y, 0f);
            gameObject.name = $"Exam Device L{profile.levelIndex:00} {profile.identityId}";

            var sprite = Resources.Load<Sprite>(profile.deviceResourcePath) ??
                         Resources.Load<Sprite>(profile.fallbackResourcePath) ??
                         TDArtLibrary.LoadSpriteOrFallback(profile.deviceResourcePath, profile.accent);
            var shadow = new GameObject("Device Shadow");
            shadow.transform.SetParent(transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            shadow.transform.localScale = new Vector3(1.10f, 0.46f, 1f);
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = TDArtLibrary.GetSoftShadowSprite();
            shadowRenderer.color = new Color(0f, 0f, 0f, 0.44f);
            shadowRenderer.sortingOrder = 7;

            _ringRoot = new GameObject("Device State Ring").transform;
            _ringRoot.SetParent(transform, false);
            var ringRenderer = _ringRoot.gameObject.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = TDArtLibrary.GetSoftRingSprite();
            ringRenderer.color = new Color(profile.accent.r, profile.accent.g, profile.accent.b, 0.34f);
            ringRenderer.sortingOrder = 8;

            _bodyRoot = new GameObject("Device Body").transform;
            _bodyRoot.SetParent(transform, false);
            _body = _bodyRoot.gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = sprite;
            _body.sortingOrder = 9;
            _body.color = Color.white;

            var size = sprite.bounds.size;
            var scale = _baseCoverage / Mathf.Max(0.01f, Mathf.Max(size.x, size.y));
            _bodyRoot.localScale = Vector3.one * scale;
            _ringRoot.localScale = Vector3.one * (_baseCoverage * (profile.levelIndex == 20 ? 1.18f : 1.02f));

            var glowRoot = new GameObject("Device Activation Glow").transform;
            glowRoot.SetParent(_bodyRoot, false);
            glowRoot.localScale = Vector3.one * 1.08f;
            _glow = glowRoot.gameObject.AddComponent<SpriteRenderer>();
            _glow.sprite = sprite;
            _glow.sortingOrder = 10;
            _glow.color = new Color(profile.accent.r, profile.accent.g, profile.accent.b, 0.18f);

            var pipCount = Mathf.Clamp(maximumCharges <= 0 ? 3 : maximumCharges, 1, 3);
            for (var i = 0; i < pipCount; i++)
            {
                var pip = new GameObject($"Device Charge {i + 1}");
                pip.transform.SetParent(transform, false);
                pip.transform.localPosition = new Vector3((i - ((pipCount - 1) * 0.5f)) * 0.22f, -(_baseCoverage * 0.54f), 0f);
                pip.transform.localScale = Vector3.one * 0.16f;
                var renderer = pip.AddComponent<SpriteRenderer>();
                renderer.sprite = TDArtLibrary.GetSoftRingSprite();
                renderer.sortingOrder = 10;
                renderer.color = profile.accent;
                _chargePips.Add(renderer);
            }

            SetStage(TDExamPresentationStage.Dormant);
            SetRuntimeState(maximumCharges <= 0 ? pipCount : maximumCharges, false, 0);
        }

        public void SetStage(TDExamPresentationStage stage)
        {
            _stage = stage;
        }

        public void SetRuntimeState(int charges, bool active, int bossPhase)
        {
            for (var i = 0; i < _chargePips.Count; i++)
            {
                var available = _profile != null && _profile.levelIndex == 9 ? i == Mathf.Abs(charges) % _chargePips.Count : i < charges;
                _chargePips[i].color = available
                    ? _profile.accent
                    : new Color(0.18f, 0.20f, 0.20f, 0.36f);
            }

            if (_profile != null && _profile.levelIndex == 20)
            {
                _ringRoot.localScale = Vector3.one * (_baseCoverage * (1.18f + (Mathf.Clamp(bossPhase, 0, 2) * 0.10f)));
            }

            if (active)
            {
                _activationPulse = Mathf.Max(_activationPulse, 0.65f);
            }
        }

        public void TriggerActivation()
        {
            _activationCount++;
            _activationPulse = 1f;
        }

        private void Update()
        {
            if (!IsReady)
            {
                return;
            }

            var stagePower = _stage switch
            {
                TDExamPresentationStage.Opening => 0.46f,
                TDExamPresentationStage.Escalation => 0.66f,
                TDExamPresentationStage.Decision => 0.90f,
                TDExamPresentationStage.Ending => 0.34f,
                _ => 0.24f
            };
            _activationPulse = Mathf.Max(0f, _activationPulse - (Time.unscaledDeltaTime * 1.65f));
            var wave = 0.5f + (Mathf.Sin(Time.unscaledTime * (2.2f + stagePower)) * 0.5f);
            var pulse = Mathf.Max(wave * stagePower, _activationPulse);
            _ringRoot.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * (8f + (stagePower * 16f)));
            _bodyRoot.localPosition = new Vector3(0f, Mathf.Sin(Time.unscaledTime * 1.8f) * 0.018f * stagePower, 0f);
            _glow.color = new Color(_profile.accent.r, _profile.accent.g, _profile.accent.b, 0.08f + (pulse * 0.30f));
            _glow.transform.localScale = Vector3.one * (1.04f + (pulse * 0.10f));
        }
    }
}
