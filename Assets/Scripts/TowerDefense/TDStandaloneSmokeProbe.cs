#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace TD
{
    [Serializable]
    public sealed class TDP1253PerformanceMetrics
    {
        public float actualRealSeconds;
        public int sampledFrames;
        public float averageFps;
        public float p95FrameMilliseconds;
        public float p99FrameMilliseconds;
        public float maximumFrameMilliseconds;
        public int framesOver33Milliseconds;
        public int framesOver50Milliseconds;
        public long peakAllocatedMemoryBytes;
        public long peakReservedMemoryBytes;
        public int peakActiveEnemies;
        public int peakActiveTowers;
        public int peakActiveProjectiles;
        public bool frameRatePassed;
        public bool framePacingPassed;
        public bool memoryPassed;
        public bool passed;
    }

    [Serializable]
    public sealed class TDP1251StandaloneSmokeReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public string productName;
        public string version;
        public string unityVersion;
        public string scriptingBackend;
        public string sceneName;
        public float timeScale;
        public int technicalIntegrity;
        public bool technicalAssistApplied;
        public bool bootstrapLoaded;
        public bool campaignOpened;
        public bool deploymentConfirmed;
        public bool autoplayStarted;
        public bool completed;
        public bool victory;
        public bool economyDecisionValue;
        public bool fullMissionCompleted;
        public bool profileCaptured;
        public bool profileRestored;
        public bool stabilityMode;
        public bool stabilitySampleStarted;
        public bool stabilitySampleCompleted;
        public bool cleanExitRequested;
        public bool passed;
        public int requestedLevel;
        public int stabilityMinimumWave;
        public int stabilitySampleStartWave;
        public float stabilityWarmupScale;
        public float stabilitySampleSeconds;
        public float targetAverageFps;
        public float maximumMemoryMegabytes;
        public string deploymentStatus;
        public string technicalAssistStatus;
        public string autoplayStatus;
        public string profileIsolationError;
        public string[] runtimeErrors;
        public TDCampaignRecoveryAudit saveRecovery;
        public TDP1253PerformanceMetrics performance;
        public TDP124RealRunReport run;
    }

    public sealed class TDStandaloneSmokeProbe : MonoBehaviour
    {
        private const string SmokeFlag = "--td-smoke-test";
        private const string StabilityFlag = "--td-p1253-stability-test";
#if ENABLE_IL2CPP
        private const string RuntimeScriptingBackend = "IL2CPP";
#else
        private const string RuntimeScriptingBackend = "Mono";
#endif
        private readonly List<string> _runtimeErrors = new();
        private readonly List<float> _frameMilliseconds = new();
        private static readonly string[] CapturedSlotSnapshots = new string[TDCampaignProgression.MaxSaveSlots];
        private static readonly bool[] CapturedSlotInitialized = new bool[TDCampaignProgression.MaxSaveSlots];
        private static bool _profileCaptured;
        private static int _capturedActiveSlot;
        private static int _capturedLevelIndex;
        private float _samplingStartedRealtime;
        private long _peakAllocatedMemoryBytes;
        private long _peakReservedMemoryBytes;
        private int _peakActiveEnemies;
        private int _peakActiveTowers;
        private int _peakActiveProjectiles;

        public static bool IsRequested()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], SmokeFlag, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], StabilityFlag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static void PrepareCleanProfile()
        {
            CaptureProfile();
            TDCampaignProgression.ResetProgress(20);
            TDCampaignRouter.SaveLevelIndex(ReadIntArgument(
                "--td-smoke-level",
                1,
                1,
                20));
            PlayerPrefs.Save();
        }

        private static void CaptureProfile()
        {
            if (_profileCaptured)
            {
                return;
            }

            _capturedActiveSlot = TDCampaignProgression.ActiveSaveSlot;
            _capturedLevelIndex = TDCampaignRouter.GetSavedLevelIndex(1);
            var summaries = TDCampaignProgression.GetSaveSlotSummaries(20);
            for (var slot = 1; slot <= TDCampaignProgression.MaxSaveSlots; slot++)
            {
                CapturedSlotInitialized[slot - 1] =
                    summaries.Length >= slot && summaries[slot - 1].initialized;
                if (!CapturedSlotInitialized[slot - 1])
                {
                    CapturedSlotSnapshots[slot - 1] = string.Empty;
                    continue;
                }

                if (!TDCampaignProgression.SetActiveSaveSlot(slot, 20, out var error))
                {
                    throw new InvalidOperationException(error);
                }

                CapturedSlotSnapshots[slot - 1] = TDCampaignProgression.ExportSnapshot(20);
            }

            if (!TDCampaignProgression.SetActiveSaveSlot(_capturedActiveSlot, 20, out var restoreError))
            {
                throw new InvalidOperationException(restoreError);
            }

            _profileCaptured = true;
        }

        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            Application.logMessageReceived += HandleLog;
            TDReleaseDiagnostics.MarkCheckpoint("automation_start");
        }

        private IEnumerator Start()
        {
            yield return null;

            var report = new TDP1251StandaloneSmokeReport
            {
                schemaVersion = "p1253-standalone-smoke-v1",
                generatedUtc = DateTime.UtcNow.ToString("o"),
                productName = Application.productName,
                version = Application.version,
                unityVersion = Application.unityVersion,
                scriptingBackend = RuntimeScriptingBackend,
                sceneName = SceneManager.GetActiveScene().name,
                timeScale = ReadFloatArgument("--td-smoke-time-scale", 16f, 1f, 20f),
                technicalIntegrity = ReadIntArgument("--td-smoke-technical-integrity", 0, 0, 5000),
                requestedLevel = ReadIntArgument("--td-smoke-level", 1, 1, 20),
                targetAverageFps = ReadFloatArgument("--td-stability-target-fps", 55f, 15f, 120f),
                maximumMemoryMegabytes = ReadFloatArgument("--td-stability-max-memory-mb", 1536f, 256f, 8192f),
                stabilityMode = HasArgument(StabilityFlag),
                stabilityMinimumWave = ReadIntArgument("--td-stability-min-wave", 14, 1, 20),
                stabilityWarmupScale = ReadFloatArgument("--td-stability-warmup-scale", 16f, 1f, 20f),
                stabilitySampleSeconds = ReadFloatArgument("--td-stability-sample-seconds", 60f, 15f, 1200f),
                profileCaptured = _profileCaptured
            };

            Time.timeScale = report.stabilityMode ? report.stabilityWarmupScale : report.timeScale;
            if (!report.stabilityMode)
            {
                BeginPerformanceSample();
            }

            var timeoutSeconds = ReadFloatArgument("--td-smoke-timeout", 120f, 30f, 1800f);
            var manager = GetComponent<TDGameManager>() ?? FindFirstObjectByType<TDGameManager>();
            report.bootstrapLoaded = manager != null;
            if (manager == null)
            {
                _runtimeErrors.Add("TDGameManager was not created by the bootstrap scene.");
                Finish(report, 2);
                yield break;
            }

            var campaignBefore = manager.DebugGetP8CampaignReport();
            report.campaignOpened = ContainsTrue(campaignBefore, "p8.ui.open") &&
                                    campaignBefore.Contains($"p8.route.level={report.requestedLevel}");
            report.saveRecovery = TDCampaignProgression.DebugAuditRecoveryForTest(20);
            if (report.requestedLevel > 1)
            {
                manager.DebugPrepareP124RepresentativeProgressionForTest();
            }

            report.deploymentStatus = manager.DebugDeployCurrentMissionForTest();
            var campaignAfter = manager.DebugGetP8CampaignReport();
            report.deploymentConfirmed = ContainsTrue(campaignAfter, "p8.ui.deploymentConfirmed") &&
                                         !report.deploymentStatus.StartsWith("skip:", StringComparison.OrdinalIgnoreCase);
            if (report.technicalIntegrity > 0)
            {
                report.technicalAssistStatus = manager.DebugApplyP1252TechnicalSmokeAssist(
                    report.technicalIntegrity);
                report.technicalAssistApplied = report.technicalAssistStatus.Contains(
                    "p12.5.2.technical_assist.applied=True");
            }

            report.autoplayStatus = manager.DebugStartP124AutoplayForTest(
                "adaptive_network",
                0,
                timeoutSeconds);
            report.autoplayStarted = report.autoplayStatus.Contains("p12.4.autoplay.started=True");

            var deadline = Time.realtimeSinceStartup + timeoutSeconds + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (manager.IsP124AutoplayTerminal)
                {
                    break;
                }

                var runtime = manager.DebugGetP1253RuntimeState();
                if (report.stabilityMode &&
                    !report.stabilitySampleStarted &&
                    runtime.currentWave >= report.stabilityMinimumWave)
                {
                    Time.timeScale = report.timeScale;
                    report.stabilitySampleStarted = true;
                    report.stabilitySampleStartWave = runtime.currentWave;
                    BeginPerformanceSample();
                    TDReleaseDiagnostics.MarkCheckpoint(
                        $"stability_sample_{report.timeScale:0.#}x_wave_{runtime.currentWave}");
                }

                if (!report.stabilityMode || report.stabilitySampleStarted)
                {
                    SampleRuntime(runtime);
                }

                if (report.stabilityMode &&
                    report.stabilitySampleStarted &&
                    Time.realtimeSinceStartup - _samplingStartedRealtime >= report.stabilitySampleSeconds)
                {
                    report.stabilitySampleCompleted = true;
                    break;
                }

                yield return null;
            }

            if (!report.stabilityMode || report.stabilitySampleStarted)
            {
                SampleRuntime(manager.DebugGetP1253RuntimeState());
            }

            report.run = manager.DebugBuildP124RunReport();
            report.completed = report.run != null && report.run.completed && !report.run.stalled;
            report.victory = report.run != null && report.run.victory;
            report.economyDecisionValue = report.run != null && report.run.economyDecisionValue;
            report.fullMissionCompleted = report.run != null &&
                                          report.run.wavesCleared == report.run.waveCount &&
                                          report.run.waveCount > 0;
            report.runtimeErrors = _runtimeErrors.ToArray();
            report.performance = BuildPerformanceMetrics(report);
            var sharedPass = report.bootstrapLoaded && report.campaignOpened &&
                             report.deploymentConfirmed && report.autoplayStarted &&
                             report.runtimeErrors.Length == 0 &&
                             report.saveRecovery != null && report.saveRecovery.passed &&
                             (report.technicalIntegrity <= 0 || report.technicalAssistApplied);
            report.passed = report.stabilityMode
                ? sharedPass &&
                  report.stabilitySampleStarted &&
                  report.stabilitySampleCompleted &&
                  report.performance.passed
                : sharedPass &&
                  report.completed &&
                  report.victory &&
                  report.economyDecisionValue &&
                  report.fullMissionCompleted;
            Finish(report, report.passed ? 0 : 2);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void Finish(TDP1251StandaloneSmokeReport report, int exitCode)
        {
            report.profileRestored = RestoreCapturedProfile(out var isolationError);
            report.profileIsolationError = isolationError;
            if (!report.profileRestored)
            {
                _runtimeErrors.Add($"Automation profile restoration failed: {isolationError}");
                report.passed = false;
                exitCode = 2;
            }

            report.runtimeErrors = _runtimeErrors.ToArray();
            report.cleanExitRequested = true;
            var outputPath = ReadStringArgument(
                "--td-smoke-report",
                Path.Combine(Application.persistentDataPath, "p1251_standalone_smoke.json"));
            try
            {
                var fullPath = Path.GetFullPath(outputPath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
                Debug.Log($"[TD][P12.5.3] Standalone smoke report: {fullPath} passed={report.passed}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[TD][P12.5.3] Could not write standalone smoke report: {exception}");
                exitCode = 3;
            }

            TDReleaseDiagnostics.MarkCheckpoint("automation_complete");
            Application.Quit(exitCode);
        }

        private void BeginPerformanceSample()
        {
            _frameMilliseconds.Clear();
            _samplingStartedRealtime = Time.realtimeSinceStartup;
            _peakAllocatedMemoryBytes = 0L;
            _peakReservedMemoryBytes = 0L;
            _peakActiveEnemies = 0;
            _peakActiveTowers = 0;
            _peakActiveProjectiles = 0;
        }

        private void SampleRuntime(TDP1253RuntimeState runtime)
        {
            var frameMilliseconds = Time.unscaledDeltaTime * 1000f;
            if (frameMilliseconds > 0f && frameMilliseconds < 5000f)
            {
                _frameMilliseconds.Add(frameMilliseconds);
            }

            _peakAllocatedMemoryBytes = Math.Max(
                _peakAllocatedMemoryBytes,
                Profiler.GetTotalAllocatedMemoryLong());
            _peakReservedMemoryBytes = Math.Max(
                _peakReservedMemoryBytes,
                Profiler.GetTotalReservedMemoryLong());
            _peakActiveEnemies = Math.Max(_peakActiveEnemies, runtime.activeEnemies);
            _peakActiveTowers = Math.Max(_peakActiveTowers, runtime.activeTowers);
            _peakActiveProjectiles = Math.Max(_peakActiveProjectiles, runtime.activeProjectiles);
        }

        private TDP1253PerformanceMetrics BuildPerformanceMetrics(TDP1251StandaloneSmokeReport report)
        {
            var ordered = _frameMilliseconds.OrderBy(value => value).ToArray();
            var duration = _samplingStartedRealtime <= 0f
                ? 0f
                : Mathf.Max(0.001f, Time.realtimeSinceStartup - _samplingStartedRealtime);
            var averageFrameMs = ordered.Length == 0 ? float.PositiveInfinity : ordered.Average();
            var result = new TDP1253PerformanceMetrics
            {
                actualRealSeconds = duration,
                sampledFrames = ordered.Length,
                averageFps = float.IsInfinity(averageFrameMs) ? 0f : 1000f / Mathf.Max(0.001f, averageFrameMs),
                p95FrameMilliseconds = Percentile(ordered, 0.95f),
                p99FrameMilliseconds = Percentile(ordered, 0.99f),
                maximumFrameMilliseconds = ordered.Length == 0 ? 0f : ordered[ordered.Length - 1],
                framesOver33Milliseconds = ordered.Count(value => value > 33.34f),
                framesOver50Milliseconds = ordered.Count(value => value > 50f),
                peakAllocatedMemoryBytes = _peakAllocatedMemoryBytes,
                peakReservedMemoryBytes = _peakReservedMemoryBytes,
                peakActiveEnemies = _peakActiveEnemies,
                peakActiveTowers = _peakActiveTowers,
                peakActiveProjectiles = _peakActiveProjectiles
            };
            result.frameRatePassed = result.sampledFrames > 30 &&
                                     result.averageFps >= report.targetAverageFps;
            result.framePacingPassed = result.p95FrameMilliseconds <= 33.34f &&
                                       result.p99FrameMilliseconds <= 50f;
            result.memoryPassed = result.peakReservedMemoryBytes <=
                                  report.maximumMemoryMegabytes * 1024f * 1024f;
            result.passed = result.frameRatePassed && result.framePacingPassed && result.memoryPassed;
            return result;
        }

        private static float Percentile(float[] sortedValues, float percentile)
        {
            if (sortedValues == null || sortedValues.Length == 0)
            {
                return 0f;
            }

            var index = Mathf.Clamp(
                Mathf.CeilToInt(sortedValues.Length * Mathf.Clamp01(percentile)) - 1,
                0,
                sortedValues.Length - 1);
            return sortedValues[index];
        }

        public static bool RestorePreparedProfile(out string error)
        {
            return RestoreCapturedProfile(out error);
        }

        private static bool RestoreCapturedProfile(out string error)
        {
            error = string.Empty;
            if (!_profileCaptured)
            {
                error = "No pre-automation profile was captured.";
                return false;
            }

            try
            {
                for (var slot = 1; slot <= TDCampaignProgression.MaxSaveSlots; slot++)
                {
                    if (!TDCampaignProgression.SetActiveSaveSlot(slot, 20, out var slotError))
                    {
                        error = slotError;
                        return false;
                    }

                    TDCampaignProgression.ImportSnapshot(
                        CapturedSlotInitialized[slot - 1]
                            ? CapturedSlotSnapshots[slot - 1]
                            : string.Empty,
                        20);
                }

                if (!TDCampaignProgression.SetActiveSaveSlot(_capturedActiveSlot, 20, out var restoreError))
                {
                    error = restoreError;
                    return false;
                }

                TDCampaignRouter.SaveLevelIndex(_capturedLevelIndex);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                _profileCaptured = false;
            }
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            var message = string.IsNullOrWhiteSpace(stackTrace)
                ? condition
                : $"{condition}\n{stackTrace}";
            if (!_runtimeErrors.Contains(message))
            {
                _runtimeErrors.Add(message);
            }
        }

        private static bool ContainsTrue(string report, string key)
        {
            return !string.IsNullOrWhiteSpace(report) &&
                   report.IndexOf($"{key}=True", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ReadStringArgument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }

                var prefix = name + "=";
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i].Substring(prefix.Length);
                }
            }

            return fallback;
        }

        private static bool HasArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            return args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        }

        private static float ReadFloatArgument(string name, float fallback, float min, float max)
        {
            var raw = ReadStringArgument(name, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return float.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
                ? Mathf.Clamp(parsed, min, max)
                : fallback;
        }

        private static int ReadIntArgument(string name, int fallback, int min, int max)
        {
            var raw = ReadStringArgument(name, fallback.ToString());
            return int.TryParse(raw, out var parsed)
                ? Mathf.Clamp(parsed, min, max)
                : fallback;
        }
    }
}
#endif
