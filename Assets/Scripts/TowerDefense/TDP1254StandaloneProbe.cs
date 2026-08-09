using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace TD
{
    [Serializable]
    public sealed class TDP1254HostProfile
    {
        public string operatingSystem;
        public string processorType;
        public int processorCount;
        public int systemMemoryMegabytes;
        public string graphicsDeviceName;
        public int graphicsMemoryMegabytes;
        public int screenWidth;
        public int screenHeight;
    }

    [Serializable]
    public sealed class TDP1254SoakCheckpoint
    {
        public float elapsedSeconds;
        public float averageFps;
        public long allocatedMemoryBytes;
        public long reservedMemoryBytes;
        public int activeEnemies;
        public int activeTowers;
        public int activeProjectiles;
        public int spawnedEnemies;
        public int resolvedEnemies;
    }

    [Serializable]
    public sealed class TDP1254SoakReport
    {
        public int requestedLevel;
        public int warmupWave;
        public int sampleStartWave;
        public int targetEnemies;
        public float requestedRealSeconds;
        public float actualRealSeconds;
        public float timeScale;
        public int sampledFrames;
        public float averageFps;
        public float p95FrameMilliseconds;
        public float p99FrameMilliseconds;
        public float maximumFrameMilliseconds;
        public int framesOver33Milliseconds;
        public int framesOver50Milliseconds;
        public long initialReservedMemoryBytes;
        public long finalReservedMemoryBytes;
        public long peakAllocatedMemoryBytes;
        public long peakReservedMemoryBytes;
        public float reservedMemoryDriftMegabytes;
        public float reservedMemorySlopeMegabytesPerMinute;
        public int garbageCollectionGeneration0;
        public int garbageCollectionGeneration1;
        public int garbageCollectionGeneration2;
        public int minimumActiveEnemies;
        public int peakActiveEnemies;
        public int peakActiveTowers;
        public int peakActiveProjectiles;
        public string warmupStatus;
        public string beginStatus;
        public string endStatus;
        public TDP1254SoakRuntimeState finalRuntime;
        public TDP1254SoakCheckpoint[] checkpoints;
        public bool shippingDurationPassed;
        public bool sustainedCombatPassed;
        public bool frameRatePassed;
        public bool framePacingPassed;
        public bool memoryCeilingPassed;
        public bool memoryTrendPassed;
        public bool passed;
    }

    [Serializable]
    public sealed class TDP1254PlayerGateReport
    {
        public string schemaVersion;
        public string generatedUtc;
        public string mode;
        public string productName;
        public string version;
        public string unityVersion;
        public string scriptingBackend;
        public TDP1254HostProfile host;
        public TDP1254SoakReport soak;
        public TDP1254CloudMatrixAudit cloudMatrix;
        public TDP1254TelemetryStatus telemetry;
        public bool profileRestored;
        public string profileIsolationError;
        public string[] runtimeErrors;
        public bool passed;
    }

    public sealed class TDP1254StandaloneProbe : MonoBehaviour
    {
        private const string SoakFlag = "--td-p1254-soak-test";
        private const string CloudFlag = "--td-p1254-cloud-test";
        private const string TelemetryFlag = "--td-p1254-telemetry-test";
#if ENABLE_IL2CPP
        private const string RuntimeScriptingBackend = "IL2CPP";
#else
        private const string RuntimeScriptingBackend = "Mono";
#endif
        private readonly List<string> _runtimeErrors = new();
        private readonly List<float> _frameMilliseconds = new();
        private readonly List<TDP1254SoakCheckpoint> _checkpoints = new();
        private long _peakAllocatedMemoryBytes;
        private long _peakReservedMemoryBytes;
        private int _minimumActiveEnemies = int.MaxValue;
        private int _peakActiveEnemies;
        private int _peakActiveTowers;
        private int _peakActiveProjectiles;

        public static bool IsRequested()
        {
            var args = Environment.GetCommandLineArgs();
            return args.Any(argument =>
                string.Equals(argument, SoakFlag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, CloudFlag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, TelemetryFlag, StringComparison.OrdinalIgnoreCase));
        }

        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            Application.logMessageReceived += HandleLog;
            TDReleaseDiagnostics.MarkCheckpoint("p1254_player_gate_start");
        }

        private IEnumerator Start()
        {
            yield return null;

            var report = new TDP1254PlayerGateReport
            {
                schemaVersion = "p1254-player-gate-v1",
                generatedUtc = DateTime.UtcNow.ToString("o"),
                mode = HasArgument(SoakFlag)
                    ? "continuous_soak"
                    : HasArgument(CloudFlag)
                        ? "cloud_conflict_matrix"
                        : "telemetry_transport",
                productName = Application.productName,
                version = Application.version,
                unityVersion = Application.unityVersion,
                scriptingBackend = RuntimeScriptingBackend,
                host = BuildHostProfile()
            };

            if (HasArgument(SoakFlag))
            {
                yield return RunSoak(report);
            }
            else if (HasArgument(CloudFlag))
            {
                RunCloudMatrix(report);
            }
            else
            {
                yield return RunTelemetryTransport(report);
            }

            Finish(report, report.passed ? 0 : 2);
        }

        private IEnumerator RunSoak(TDP1254PlayerGateReport root)
        {
            var manager = GetComponent<TDGameManager>() ?? FindFirstObjectByType<TDGameManager>();
            var report = new TDP1254SoakReport
            {
                requestedLevel = ReadIntArgument("--td-smoke-level", 20, 1, 20),
                warmupWave = ReadIntArgument("--td-soak-warmup-wave", 10, 1, 20),
                targetEnemies = ReadIntArgument("--td-soak-target-enemies", 36, 12, 96),
                requestedRealSeconds = ReadFloatArgument(
                    "--td-soak-seconds",
                    1200f,
                    30f,
                    3600f),
                timeScale = ReadFloatArgument("--td-soak-time-scale", 1f, 1f, 3f)
            };
            root.soak = report;
            if (manager == null)
            {
                _runtimeErrors.Add("TDGameManager was not created for the P12.5.4 soak.");
                root.passed = false;
                yield break;
            }

            Time.timeScale = ReadFloatArgument("--td-soak-warmup-scale", 16f, 1f, 20f);
            if (report.requestedLevel > 1)
            {
                manager.DebugPrepareP124RepresentativeProgressionForTest();
            }

            var deployment = manager.DebugDeployCurrentMissionForTest();
            var assist = manager.DebugApplyP1252TechnicalSmokeAssist(5000);
            report.warmupStatus = deployment + " | " + assist + " | " +
                                  manager.DebugStartP124AutoplayForTest(
                                      "adaptive_network",
                                      0,
                                      ReadFloatArgument("--td-soak-warmup-timeout", 300f, 60f, 900f));
            var warmupDeadline = Time.realtimeSinceStartup +
                                 ReadFloatArgument("--td-soak-warmup-timeout", 300f, 60f, 900f);
            while (Time.realtimeSinceStartup < warmupDeadline)
            {
                var runtime = manager.DebugGetP1253RuntimeState();
                if (runtime.currentWave >= report.warmupWave &&
                    runtime.activeTowers >= 6 &&
                    !runtime.gameOver)
                {
                    report.sampleStartWave = runtime.currentWave;
                    break;
                }

                if (runtime.gameOver || manager.IsP124AutoplayTerminal)
                {
                    break;
                }

                yield return null;
            }

            if (report.sampleStartWave <= 0)
            {
                _runtimeErrors.Add("P12.5.4 soak could not reach its dense-combat warmup fixture.");
                root.passed = false;
                yield break;
            }

            report.beginStatus = manager.DebugBeginP1254ContinuousSoakForTest(report.targetEnemies);
            if (!report.beginStatus.Contains("p12.5.4.soak.started=True"))
            {
                _runtimeErrors.Add(report.beginStatus);
                root.passed = false;
                yield break;
            }

            Time.timeScale = report.timeScale;
            _frameMilliseconds.Clear();
            _checkpoints.Clear();
            _peakAllocatedMemoryBytes = 0L;
            _peakReservedMemoryBytes = 0L;
            _minimumActiveEnemies = int.MaxValue;
            _peakActiveEnemies = 0;
            _peakActiveTowers = 0;
            _peakActiveProjectiles = 0;
            var startedRealtime = Time.realtimeSinceStartup;
            var initialReservedMemory = Profiler.GetTotalReservedMemoryLong();
            var generation0Start = GC.CollectionCount(0);
            var generation1Start = GC.CollectionCount(1);
            var generation2Start = GC.CollectionCount(2);
            var nextRuntimeSample = 0f;
            var nextCheckpoint = 0f;

            while (Time.realtimeSinceStartup - startedRealtime < report.requestedRealSeconds)
            {
                var frameMilliseconds = Time.unscaledDeltaTime * 1000f;
                if (frameMilliseconds > 0f && frameMilliseconds < 5000f)
                {
                    _frameMilliseconds.Add(frameMilliseconds);
                }

                var elapsed = Time.realtimeSinceStartup - startedRealtime;
                if (elapsed >= nextRuntimeSample)
                {
                    var runtime = manager.DebugGetP1254SoakRuntimeState();
                    SampleRuntime(runtime, elapsed >= 5f);
                    nextRuntimeSample = elapsed + 0.10f;
                    if (!runtime.active)
                    {
                        _runtimeErrors.Add("P12.5.4 continuous soak fixture stopped before its deadline.");
                        break;
                    }
                }

                if (elapsed >= nextCheckpoint)
                {
                    AddCheckpoint(manager, elapsed);
                    nextCheckpoint = elapsed + 60f;
                }

                yield return null;
            }

            report.actualRealSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - startedRealtime);
            AddCheckpoint(manager, report.actualRealSeconds);
            report.endStatus = manager.DebugEndP1254ContinuousSoakForTest();
            report.finalRuntime = manager.DebugGetP1254SoakRuntimeState();
            report.checkpoints = _checkpoints.ToArray();
            report.initialReservedMemoryBytes = initialReservedMemory;
            report.finalReservedMemoryBytes = Profiler.GetTotalReservedMemoryLong();
            report.peakAllocatedMemoryBytes = _peakAllocatedMemoryBytes;
            report.peakReservedMemoryBytes = _peakReservedMemoryBytes;
            report.reservedMemoryDriftMegabytes =
                (report.finalReservedMemoryBytes - report.initialReservedMemoryBytes) / (1024f * 1024f);
            report.reservedMemorySlopeMegabytesPerMinute = report.actualRealSeconds <= 0f
                ? float.PositiveInfinity
                : report.reservedMemoryDriftMegabytes / (report.actualRealSeconds / 60f);
            report.garbageCollectionGeneration0 = GC.CollectionCount(0) - generation0Start;
            report.garbageCollectionGeneration1 = GC.CollectionCount(1) - generation1Start;
            report.garbageCollectionGeneration2 = GC.CollectionCount(2) - generation2Start;
            report.minimumActiveEnemies = _minimumActiveEnemies == int.MaxValue ? 0 : _minimumActiveEnemies;
            report.peakActiveEnemies = _peakActiveEnemies;
            report.peakActiveTowers = _peakActiveTowers;
            report.peakActiveProjectiles = _peakActiveProjectiles;

            var ordered = _frameMilliseconds.OrderBy(value => value).ToArray();
            var averageFrameMilliseconds = ordered.Length == 0
                ? float.PositiveInfinity
                : ordered.Average();
            report.sampledFrames = ordered.Length;
            report.averageFps = float.IsInfinity(averageFrameMilliseconds)
                ? 0f
                : 1000f / Mathf.Max(0.001f, averageFrameMilliseconds);
            report.p95FrameMilliseconds = Percentile(ordered, 0.95f);
            report.p99FrameMilliseconds = Percentile(ordered, 0.99f);
            report.maximumFrameMilliseconds = ordered.Length == 0 ? 0f : ordered[ordered.Length - 1];
            report.framesOver33Milliseconds = ordered.Count(value => value > 33.34f);
            report.framesOver50Milliseconds = ordered.Count(value => value > 50f);

            var maximumMemoryMegabytes = ReadFloatArgument(
                "--td-soak-max-memory-mb",
                1536f,
                256f,
                8192f);
            var maximumMemorySlope = ReadFloatArgument(
                "--td-soak-max-memory-slope-mb-min",
                8f,
                0.5f,
                128f);
            report.shippingDurationPassed = report.requestedRealSeconds >= 1200f &&
                                            report.actualRealSeconds >= 1199f;
            report.sustainedCombatPassed =
                report.finalRuntime != null &&
                report.finalRuntime.spawnedEnemies >= 360 &&
                report.finalRuntime.resolvedEnemies >= 200 &&
                report.minimumActiveEnemies >= Mathf.Max(4, report.targetEnemies / 4) &&
                report.peakActiveTowers >= 6 &&
                report.peakActiveProjectiles > 0;
            report.frameRatePassed = report.sampledFrames > 300 &&
                                     report.averageFps >= ReadFloatArgument(
                                         "--td-soak-target-fps",
                                         55f,
                                         15f,
                                         120f);
            report.framePacingPassed = report.p95FrameMilliseconds <= 33.34f &&
                                       report.p99FrameMilliseconds <= 50f;
            report.memoryCeilingPassed = report.peakReservedMemoryBytes <=
                                         maximumMemoryMegabytes * 1024f * 1024f;
            report.memoryTrendPassed = report.reservedMemoryDriftMegabytes <= 128f &&
                                       report.reservedMemorySlopeMegabytesPerMinute <= maximumMemorySlope;
            report.passed = report.shippingDurationPassed &&
                            report.sustainedCombatPassed &&
                            report.frameRatePassed &&
                            report.framePacingPassed &&
                            report.memoryCeilingPassed &&
                            report.memoryTrendPassed &&
                            _runtimeErrors.Count == 0;
            root.passed = report.passed;
            TDReleaseTelemetry.RecordSoakSummary(
                report.finalRuntime,
                report.actualRealSeconds,
                report.averageFps,
                report.p95FrameMilliseconds,
                report.peakReservedMemoryBytes,
                report.passed);
        }

        private void RunCloudMatrix(TDP1254PlayerGateReport report)
        {
            TDReleaseDiagnostics.MarkCheckpoint("p1254_cloud_matrix");
            report.cloudMatrix = TDCampaignProgression.DebugAuditCloudConflictMatrixForTest(20);
            TDReleaseTelemetry.RecordCloudMatrixSummary(report.cloudMatrix);
            report.passed = report.cloudMatrix != null &&
                            report.cloudMatrix.passed &&
                            _runtimeErrors.Count == 0;
        }

        private IEnumerator RunTelemetryTransport(TDP1254PlayerGateReport report)
        {
            TDReleaseDiagnostics.MarkCheckpoint("p1254_transport_test");
            var fingerprint = TDReleaseTelemetry.DebugQueueRedactionProbe(
                "sk-proj-DO_NOT_TRANSMIT user@example.com C:\\Users\\private\\save.json 192.0.2.1");
            TDReleaseTelemetry.DebugFlushNow();
            var deadline = Time.realtimeSinceStartup +
                           ReadFloatArgument("--td-telemetry-test-timeout", 45f, 10f, 180f);
            while (Time.realtimeSinceStartup < deadline)
            {
                var status = TDReleaseTelemetry.DebugGetStatus();
                if (status.failedUploads >= 1 &&
                    status.successfulUploads >= 2 &&
                    status.queuedEvents == 0 &&
                    !status.transportBusy)
                {
                    break;
                }

                yield return null;
            }

            report.telemetry = TDReleaseTelemetry.DebugGetStatus();
            report.passed = report.telemetry.consentGranted &&
                            report.telemetry.endpointConfigured &&
                            report.telemetry.failedUploads >= 1 &&
                            report.telemetry.successfulUploads >= 2 &&
                            report.telemetry.queuedEvents == 0 &&
                            string.Equals(
                                report.telemetry.redactionProbeFingerprint,
                                fingerprint,
                                StringComparison.Ordinal) &&
                            fingerprint.Length == 16 &&
                            _runtimeErrors.Count == 0;
        }

        private void SampleRuntime(TDP1254SoakRuntimeState runtime, bool includeMinimum)
        {
            if (runtime == null)
            {
                return;
            }

            _peakAllocatedMemoryBytes = Math.Max(
                _peakAllocatedMemoryBytes,
                Profiler.GetTotalAllocatedMemoryLong());
            _peakReservedMemoryBytes = Math.Max(
                _peakReservedMemoryBytes,
                Profiler.GetTotalReservedMemoryLong());
            if (includeMinimum)
            {
                _minimumActiveEnemies = Math.Min(_minimumActiveEnemies, runtime.activeEnemies);
            }

            _peakActiveEnemies = Math.Max(_peakActiveEnemies, runtime.activeEnemies);
            _peakActiveTowers = Math.Max(_peakActiveTowers, runtime.activeTowers);
            _peakActiveProjectiles = Math.Max(_peakActiveProjectiles, runtime.activeProjectiles);
        }

        private void AddCheckpoint(TDGameManager manager, float elapsed)
        {
            var runtime = manager.DebugGetP1254SoakRuntimeState();
            SampleRuntime(runtime, elapsed >= 5f);
            var recentFrames = _frameMilliseconds.Count <= 0
                ? Array.Empty<float>()
                : _frameMilliseconds.Skip(Mathf.Max(0, _frameMilliseconds.Count - 600)).ToArray();
            var averageFrameMilliseconds = recentFrames.Length == 0
                ? 0f
                : recentFrames.Average();
            _checkpoints.Add(new TDP1254SoakCheckpoint
            {
                elapsedSeconds = elapsed,
                averageFps = averageFrameMilliseconds <= 0f ? 0f : 1000f / averageFrameMilliseconds,
                allocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong(),
                reservedMemoryBytes = Profiler.GetTotalReservedMemoryLong(),
                activeEnemies = runtime.activeEnemies,
                activeTowers = runtime.activeTowers,
                activeProjectiles = runtime.activeProjectiles,
                spawnedEnemies = runtime.spawnedEnemies,
                resolvedEnemies = runtime.resolvedEnemies
            });
        }

        private void Finish(TDP1254PlayerGateReport report, int exitCode)
        {
            report.profileRestored = TDStandaloneSmokeProbe.RestorePreparedProfile(out var isolationError);
            report.profileIsolationError = isolationError;
            if (!report.profileRestored)
            {
                _runtimeErrors.Add($"Automation profile restoration failed: {isolationError}");
                report.passed = false;
                exitCode = 2;
            }

            report.runtimeErrors = _runtimeErrors.ToArray();
            var outputPath = ReadStringArgument(
                "--td-p1254-report",
                Path.Combine(Application.persistentDataPath, "p1254_player_gate.json"));
            try
            {
                var fullPath = Path.GetFullPath(outputPath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
                Debug.Log($"[TD][P12.5.4] Player gate report: {fullPath} passed={report.passed}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[TD][P12.5.4] Could not write player gate report: {exception}");
                exitCode = 3;
            }

            TDReleaseDiagnostics.MarkCheckpoint("p1254_player_gate_complete");
            Application.Quit(exitCode);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
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

        private static TDP1254HostProfile BuildHostProfile()
        {
            return new TDP1254HostProfile
            {
                operatingSystem = SystemInfo.operatingSystem,
                processorType = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                systemMemoryMegabytes = SystemInfo.systemMemorySize,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsMemoryMegabytes = SystemInfo.graphicsMemorySize,
                screenWidth = Screen.width,
                screenHeight = Screen.height
            };
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
            return Environment.GetCommandLineArgs()
                .Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        }

        private static float ReadFloatArgument(string name, float fallback, float min, float max)
        {
            var raw = ReadStringArgument(
                name,
                fallback.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
