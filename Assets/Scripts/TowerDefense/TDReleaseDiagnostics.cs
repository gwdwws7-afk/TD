using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace TD
{
    [Serializable]
    public sealed class TDP1253SessionDiagnostic
    {
        public string schemaVersion;
        public string sessionId;
        public string startedUtc;
        public string updatedUtc;
        public string endedUtc;
        public string productName;
        public string version;
        public string unityVersion;
        public string sceneName;
        public string checkpoint;
        public bool cleanShutdown;
        public bool previousSessionRecovered;
        public int errorCount;
        public string lastError;
        public string lastStackTrace;
        public long peakAllocatedMemoryBytes;
        public long peakReservedMemoryBytes;
        public TDP1253RuntimeState runtime;
    }

    public sealed class TDReleaseDiagnostics : MonoBehaviour
    {
        private const float HeartbeatIntervalSeconds = 5f;
        private const int MaxArchivedSessions = 20;
        private static TDReleaseDiagnostics _instance;
        private TDP1253SessionDiagnostic _diagnostic;
        private float _nextHeartbeat;
        private string _currentPath;
        private string _archiveDirectory;

        public static string CurrentSessionPath =>
            Path.Combine(Application.persistentDataPath, "Diagnostics", "session-current.json");

        public static void MarkCheckpoint(string checkpoint)
        {
            if (_instance == null || _instance._diagnostic == null)
            {
                return;
            }

            _instance._diagnostic.checkpoint = string.IsNullOrWhiteSpace(checkpoint)
                ? "runtime"
                : checkpoint.Trim();
            _instance.WriteHeartbeat();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            _currentPath = CurrentSessionPath;
            _archiveDirectory = Path.Combine(Application.persistentDataPath, "Diagnostics", "Archive");
            Directory.CreateDirectory(Path.GetDirectoryName(_currentPath) ?? Application.persistentDataPath);
            Directory.CreateDirectory(_archiveDirectory);

            var previousRecovered = ArchivePreviousUncleanSession();
            _diagnostic = new TDP1253SessionDiagnostic
            {
                schemaVersion = "p1253-session-diagnostic-v1",
                sessionId = Guid.NewGuid().ToString("N"),
                startedUtc = DateTime.UtcNow.ToString("o"),
                productName = Application.productName,
                version = Application.version,
                unityVersion = Application.unityVersion,
                sceneName = SceneManager.GetActiveScene().name,
                checkpoint = "bootstrap",
                previousSessionRecovered = previousRecovered
            };

            Application.logMessageReceived += HandleLog;
            WriteHeartbeat();
            TDReleaseTelemetry.RecordSessionStart(previousRecovered);
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup >= _nextHeartbeat)
            {
                WriteHeartbeat();
            }
        }

        private void OnApplicationQuit()
        {
            if (_diagnostic == null)
            {
                return;
            }

            _diagnostic.cleanShutdown = true;
            _diagnostic.endedUtc = DateTime.UtcNow.ToString("o");
            _diagnostic.checkpoint = "clean_shutdown";
            WriteHeartbeat();
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private bool ArchivePreviousUncleanSession()
        {
            if (!File.Exists(_currentPath))
            {
                return false;
            }

            try
            {
                var previous = JsonUtility.FromJson<TDP1253SessionDiagnostic>(File.ReadAllText(_currentPath));
                if (previous == null || previous.cleanShutdown)
                {
                    return false;
                }

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var sessionId = string.IsNullOrWhiteSpace(previous.sessionId)
                    ? "unknown"
                    : previous.sessionId;
                var archivePath = Path.Combine(
                    _archiveDirectory,
                    $"unclean-{timestamp}-{sessionId}.json");
                File.Copy(_currentPath, archivePath, true);
                TrimArchives();
                Debug.LogWarning($"[TD][P12.5.3] Recovered unclean session diagnostic: {archivePath}");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TD][P12.5.3] Previous session diagnostic could not be archived: {exception.Message}");
                return false;
            }
        }

        private void WriteHeartbeat()
        {
            if (_diagnostic == null)
            {
                return;
            }

            _nextHeartbeat = Time.realtimeSinceStartup + HeartbeatIntervalSeconds;
            _diagnostic.updatedUtc = DateTime.UtcNow.ToString("o");
            _diagnostic.sceneName = SceneManager.GetActiveScene().name;
            _diagnostic.peakAllocatedMemoryBytes = Math.Max(
                _diagnostic.peakAllocatedMemoryBytes,
                Profiler.GetTotalAllocatedMemoryLong());
            _diagnostic.peakReservedMemoryBytes = Math.Max(
                _diagnostic.peakReservedMemoryBytes,
                Profiler.GetTotalReservedMemoryLong());
            var manager = GetComponent<TDGameManager>() ?? FindFirstObjectByType<TDGameManager>();
            _diagnostic.runtime = manager == null ? null : manager.DebugGetP1253RuntimeState();

            try
            {
                WriteTextAtomic(_currentPath, JsonUtility.ToJson(_diagnostic, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TD][P12.5.3] Session diagnostic write failed: {exception.Message}");
            }
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (_diagnostic == null ||
                (type != LogType.Error && type != LogType.Exception && type != LogType.Assert))
            {
                return;
            }

            _diagnostic.errorCount++;
            _diagnostic.lastError = condition ?? string.Empty;
            _diagnostic.lastStackTrace = stackTrace ?? string.Empty;
            _diagnostic.checkpoint = "runtime_error";
            WriteHeartbeat();
            TDReleaseTelemetry.RecordRuntimeError(condition, stackTrace, type);
        }

        private void TrimArchives()
        {
            var files = new List<FileInfo>(new DirectoryInfo(_archiveDirectory).GetFiles("unclean-*.json"));
            files.Sort((left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
            for (var i = MaxArchivedSessions; i < files.Count; i++)
            {
                files[i].Delete();
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
    }
}
