using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TD
{
    [Serializable]
    internal sealed class TDP1254TelemetryEvent
    {
        public string schemaVersion;
        public string eventId;
        public string generatedUtc;
        public string sessionHash;
        public string productName;
        public string version;
        public string platform;
        public string scriptingBackend;
        public string eventName;
        public string checkpoint;
        public string category;
        public string fingerprint;
        public int levelIndex;
        public string levelId;
        public string mapId;
        public int wave;
        public float actualSeconds;
        public float averageFps;
        public float p95FrameMilliseconds;
        public float reservedMemoryMegabytes;
        public int cloudMatrixRows;
        public bool passed;
    }

    [Serializable]
    internal sealed class TDP1254QueuedTelemetryEvent
    {
        public int attempts;
        public long nextAttemptUtcTicks;
        public TDP1254TelemetryEvent payload;
    }

    [Serializable]
    public sealed class TDP1254TelemetryStatus
    {
        public bool consentGranted;
        public bool endpointConfigured;
        public bool transportBusy;
        public int queuedEvents;
        public int successfulUploads;
        public int failedUploads;
        public long queuedBytes;
        public long lastResponseCode;
        public string lastFailureCategory;
        public string redactionProbeFingerprint;
    }

    public sealed class TDReleaseTelemetry : MonoBehaviour
    {
        private const string ConsentKey = "td_release_telemetry_consent_v1";
        private const string EndpointArgument = "--td-telemetry-endpoint";
        private const string ConsentArgument = "--td-telemetry-consent";
        private const string AllowLoopbackHttpFlag = "--td-telemetry-allow-loopback-http";
        private const string TransportTestFlag = "--td-p1254-telemetry-test";
        private const int MaxQueueFiles = 96;
        private const long MaxQueueBytes = 2L * 1024L * 1024L;
        private const int RequestTimeoutSeconds = 12;
        private static TDReleaseTelemetry _instance;

        private readonly string _sessionHash = BuildFingerprint(Guid.NewGuid().ToString("N"));
        private string _queueDirectory;
        private Uri _endpoint;
        private bool _consentGranted;
        private bool _transportBusy;
        private float _nextPumpRealtime;
        private int _successfulUploads;
        private int _failedUploads;
        private long _lastResponseCode;
        private string _lastFailureCategory = string.Empty;
        private string _redactionProbeFingerprint = string.Empty;

        public static bool HasConsent => _instance != null && _instance._consentGranted;

        public static void SetPlayerConsent(bool granted)
        {
            PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
            PlayerPrefs.Save();
            if (_instance == null)
            {
                return;
            }

            _instance._consentGranted = granted;
            if (!granted)
            {
                _instance.DeleteQueuedEvents();
            }
            else
            {
                _instance._nextPumpRealtime = 0f;
            }
        }

        public static void RecordSessionStart(bool previousSessionRecovered)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.EnqueueEvent(new TDP1254TelemetryEvent
            {
                eventName = previousSessionRecovered ? "unclean_session_recovered" : "session_start",
                checkpoint = "bootstrap",
                category = previousSessionRecovered ? "recovery" : "lifecycle",
                passed = !previousSessionRecovered
            });
        }

        public static void RecordSessionEnd()
        {
            _instance?.EnqueueEvent(new TDP1254TelemetryEvent
            {
                eventName = "session_end",
                checkpoint = "clean_shutdown",
                category = "lifecycle",
                passed = true
            });
        }

        public static void RecordRuntimeError(string condition, string stackTrace, LogType type)
        {
            if (_instance == null)
            {
                return;
            }

            var raw = (condition ?? string.Empty) + "\n" + (stackTrace ?? string.Empty);
            _instance.EnqueueEvent(new TDP1254TelemetryEvent
            {
                eventName = "runtime_error",
                checkpoint = "runtime_error",
                category = type == LogType.Assert
                    ? "assert"
                    : type == LogType.Exception
                        ? "exception"
                        : "error",
                fingerprint = BuildFingerprint(raw),
                passed = false
            });
        }

        public static void RecordSoakSummary(
            TDP1254SoakRuntimeState runtime,
            float actualSeconds,
            float averageFps,
            float p95FrameMilliseconds,
            long reservedMemoryBytes,
            bool passed)
        {
            if (_instance == null)
            {
                return;
            }

            var managerState = _instance.GetComponent<TDGameManager>()?.DebugGetP1253RuntimeState();
            _instance.EnqueueEvent(new TDP1254TelemetryEvent
            {
                eventName = "performance_soak",
                checkpoint = "p1254_continuous_soak",
                category = "performance",
                levelIndex = managerState?.levelIndex ?? 0,
                levelId = managerState?.levelId ?? string.Empty,
                mapId = managerState?.mapId ?? string.Empty,
                wave = managerState?.currentWave ?? 0,
                actualSeconds = actualSeconds,
                averageFps = averageFps,
                p95FrameMilliseconds = p95FrameMilliseconds,
                reservedMemoryMegabytes = reservedMemoryBytes / (1024f * 1024f),
                passed = passed
            });
        }

        public static void RecordCloudMatrixSummary(TDP1254CloudMatrixAudit audit)
        {
            _instance?.EnqueueEvent(new TDP1254TelemetryEvent
            {
                eventName = "cloud_conflict_matrix",
                checkpoint = "p1254_cloud_matrix",
                category = "save",
                cloudMatrixRows = audit?.rows?.Length ?? 0,
                passed = audit != null && audit.passed
            });
        }

        public static string DebugQueueRedactionProbe(string sensitiveInput)
        {
            if (_instance == null)
            {
                return string.Empty;
            }

            _instance._redactionProbeFingerprint = BuildFingerprint(sensitiveInput ?? string.Empty);
            _instance.EnqueueEvent(new TDP1254TelemetryEvent
            {
                eventName = "transport_redaction_probe",
                checkpoint = "p1254_transport_test",
                category = "automation",
                fingerprint = _instance._redactionProbeFingerprint,
                passed = true
            });
            return _instance._redactionProbeFingerprint;
        }

        public static void DebugFlushNow()
        {
            if (_instance != null)
            {
                _instance._nextPumpRealtime = 0f;
            }
        }

        public static TDP1254TelemetryStatus DebugGetStatus()
        {
            return _instance == null
                ? new TDP1254TelemetryStatus()
                : _instance.BuildStatus();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            _queueDirectory = Path.Combine(Application.persistentDataPath, "TelemetryQueue");
            Directory.CreateDirectory(_queueDirectory);
            _consentGranted = PlayerPrefs.GetInt(ConsentKey, 0) > 0 ||
                              string.Equals(
                                  ReadArgument(ConsentArgument, string.Empty),
                                  "1",
                                  StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(
                                  ReadArgument(ConsentArgument, string.Empty),
                                  "true",
                                  StringComparison.OrdinalIgnoreCase);
            _endpoint = ResolveEndpoint(ReadArgument(EndpointArgument, string.Empty));
            if (!_consentGranted)
            {
                DeleteQueuedEvents();
            }

            TrimQueue();
            if (HasArgument(TransportTestFlag))
            {
                ResetQueuedRetryStateForTest();
            }
        }

        private void Update()
        {
            if (!_consentGranted || _endpoint == null || _transportBusy ||
                Time.realtimeSinceStartup < _nextPumpRealtime)
            {
                return;
            }

            var path = GetNextReadyQueuePath();
            if (string.IsNullOrWhiteSpace(path))
            {
                _nextPumpRealtime = Time.realtimeSinceStartup + 1f;
                return;
            }

            StartCoroutine(UploadQueuedEvent(path));
        }

        private void OnApplicationQuit()
        {
            RecordSessionEnd();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void EnqueueEvent(TDP1254TelemetryEvent payload)
        {
            if (!_consentGranted || payload == null)
            {
                return;
            }

            PopulateAllowlistedContext(payload);
            var queued = new TDP1254QueuedTelemetryEvent
            {
                payload = payload
            };
            try
            {
                Directory.CreateDirectory(_queueDirectory);
                var path = Path.Combine(
                    _queueDirectory,
                    $"{DateTime.UtcNow.Ticks:D19}-{payload.eventId}.json");
                WriteTextAtomic(path, JsonUtility.ToJson(queued));
                TrimQueue();
                _nextPumpRealtime = 0f;
            }
            catch (Exception exception)
            {
                _failedUploads++;
                _lastFailureCategory = "queue_write";
                Debug.LogWarning($"[TD][P12.5.4] Telemetry queue write failed: {exception.Message}");
            }
        }

        private IEnumerator UploadQueuedEvent(string path)
        {
            _transportBusy = true;
            TDP1254QueuedTelemetryEvent queued;
            try
            {
                queued = JsonUtility.FromJson<TDP1254QueuedTelemetryEvent>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                _lastFailureCategory = "queue_decode";
                Debug.LogWarning($"[TD][P12.5.4] Telemetry queue item rejected: {exception.Message}");
                DeleteFileQuietly(path);
                _transportBusy = false;
                yield break;
            }

            if (queued?.payload == null)
            {
                DeleteFileQuietly(path);
                _transportBusy = false;
                yield break;
            }

            using var request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(queued.payload))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = RequestTimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            yield return request.SendWebRequest();

            _lastResponseCode = request.responseCode;
            if (request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 && request.responseCode <= 299)
            {
                _successfulUploads++;
                _lastFailureCategory = string.Empty;
                DeleteFileQuietly(path);
                _nextPumpRealtime = 0f;
            }
            else
            {
                _failedUploads++;
                _lastFailureCategory = request.responseCode >= 500
                    ? "server"
                    : request.responseCode >= 400
                        ? "client"
                        : "network";
                queued.attempts = Mathf.Clamp(queued.attempts + 1, 1, 30);
                var backoffSeconds = Mathf.Min(300f, Mathf.Pow(2f, Mathf.Min(queued.attempts, 8)));
                queued.nextAttemptUtcTicks = DateTime.UtcNow.AddSeconds(backoffSeconds).Ticks;
                try
                {
                    WriteTextAtomic(path, JsonUtility.ToJson(queued));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[TD][P12.5.4] Telemetry retry state write failed: {exception.Message}");
                }

                _nextPumpRealtime = Time.realtimeSinceStartup + Mathf.Min(backoffSeconds, 5f);
            }

            _transportBusy = false;
        }

        private TDP1254TelemetryStatus BuildStatus()
        {
            var files = GetQueueFiles();
            return new TDP1254TelemetryStatus
            {
                consentGranted = _consentGranted,
                endpointConfigured = _endpoint != null,
                transportBusy = _transportBusy,
                queuedEvents = files.Length,
                successfulUploads = _successfulUploads,
                failedUploads = _failedUploads,
                queuedBytes = files.Sum(file => file.Length),
                lastResponseCode = _lastResponseCode,
                lastFailureCategory = _lastFailureCategory,
                redactionProbeFingerprint = _redactionProbeFingerprint
            };
        }

        private void PopulateAllowlistedContext(TDP1254TelemetryEvent payload)
        {
            payload.schemaVersion = "p1254-telemetry-event-v1";
            payload.eventId = Guid.NewGuid().ToString("N");
            payload.generatedUtc = DateTime.UtcNow.ToString("o");
            payload.sessionHash = _sessionHash;
            payload.productName = Application.productName;
            payload.version = Application.version;
            payload.platform = Application.platform.ToString();
#if ENABLE_IL2CPP
            payload.scriptingBackend = "IL2CPP";
#else
            payload.scriptingBackend = "Mono";
#endif
            payload.eventName = NormalizeToken(payload.eventName, "unknown");
            payload.checkpoint = NormalizeToken(payload.checkpoint, "runtime");
            payload.category = NormalizeToken(payload.category, "general");
            payload.levelId = NormalizeToken(payload.levelId, string.Empty);
            payload.mapId = NormalizeToken(payload.mapId, string.Empty);
        }

        private Uri ResolveEndpoint(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) ||
                !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var endpoint))
            {
                return null;
            }

            if (string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return endpoint;
            }

            if (string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                endpoint.IsLoopback &&
                HasArgument(AllowLoopbackHttpFlag))
            {
                return endpoint;
            }

            Debug.LogWarning("[TD][P12.5.4] Telemetry endpoint rejected; HTTPS is required outside loopback automation.");
            return null;
        }

        private string GetNextReadyQueuePath()
        {
            foreach (var file in GetQueueFiles())
            {
                try
                {
                    var queued = JsonUtility.FromJson<TDP1254QueuedTelemetryEvent>(File.ReadAllText(file.FullName));
                    if (queued == null || queued.nextAttemptUtcTicks <= DateTime.UtcNow.Ticks)
                    {
                        return file.FullName;
                    }
                }
                catch
                {
                    return file.FullName;
                }
            }

            return string.Empty;
        }

        private FileInfo[] GetQueueFiles()
        {
            if (string.IsNullOrWhiteSpace(_queueDirectory) || !Directory.Exists(_queueDirectory))
            {
                return Array.Empty<FileInfo>();
            }

            return new DirectoryInfo(_queueDirectory)
                .GetFiles("*.json")
                .OrderBy(file => file.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private void TrimQueue()
        {
            var files = GetQueueFiles();
            var totalBytes = files.Sum(file => file.Length);
            var removeCount = Mathf.Max(0, files.Length - MaxQueueFiles);
            for (var i = 0; i < files.Length && (i < removeCount || totalBytes > MaxQueueBytes); i++)
            {
                totalBytes -= files[i].Length;
                DeleteFileQuietly(files[i].FullName);
            }
        }

        private void DeleteQueuedEvents()
        {
            foreach (var file in GetQueueFiles())
            {
                DeleteFileQuietly(file.FullName);
            }
        }

        private void ResetQueuedRetryStateForTest()
        {
            foreach (var file in GetQueueFiles())
            {
                try
                {
                    var queued = JsonUtility.FromJson<TDP1254QueuedTelemetryEvent>(
                        File.ReadAllText(file.FullName));
                    if (queued == null)
                    {
                        continue;
                    }

                    queued.attempts = 0;
                    queued.nextAttemptUtcTicks = 0L;
                    WriteTextAtomic(file.FullName, JsonUtility.ToJson(queued));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[TD][P12.5.4] Telemetry test retry reset failed: {exception.Message}");
                }
            }
        }

        private static void DeleteFileQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TD][P12.5.4] Telemetry queue cleanup failed: {exception.Message}");
            }
        }

        private static void WriteTextAtomic(string path, string contents)
        {
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, contents);
            if (File.Exists(path))
            {
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        private static string NormalizeToken(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var builder = new StringBuilder(Mathf.Min(value.Length, 64));
            for (var i = 0; i < value.Length && builder.Length < 64; i++)
            {
                var character = value[i];
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == '.')
                {
                    builder.Append(character);
                }
            }

            return builder.Length == 0 ? fallback : builder.ToString();
        }

        private static string BuildFingerprint(string value)
        {
            using var algorithm = SHA256.Create();
            var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return BitConverter.ToString(bytes, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ReadArgument(string name, string fallback)
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
    }
}
