#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TD.Editor
{
    [Serializable]
    public sealed class TDP1251BuildResult
    {
        public string schemaVersion;
        public string generatedUtc;
        public string outputPath;
        public string bootstrapScene;
        public string productName;
        public string companyName;
        public string applicationIdentifier;
        public string version;
        public int buildNumber;
        public string unityVersion;
        public string backend;
        public bool development;
        public string sourceRevision;
        public string managedStrippingLevel;
        public bool stripEngineCode;
        public string il2cppCompilerConfiguration;
        public int releaseTexturesChanged;
        public int iconSlotCount;
        public bool iconConfigured;
        public bool startupBackgroundConfigured;
        public string buildGuid;
        public ulong totalSizeBytes;
        public int totalErrors;
        public int totalWarnings;
        public string[] warningMessages;
        public float durationSeconds;
        public bool outputExists;
        public bool passed;
        public string error;
    }

    public static class TDReleaseBuilder
    {
        private const string IconPath = "Assets/Art/Branding/emberline_app_icon.png";
        private const string SplashPath = "Assets/Art/Branding/emberline_startup_background.png";

        [MenuItem("TD/Build/Apply Release Player Settings")]
        public static void ApplyReleasePlayerSettingsFromMenu()
        {
            ApplyPlayerSettings(TDReleaseIdentity.SemanticVersion, TDReleaseIdentity.BuildNumber, "Mono");
            Debug.Log("[TD][P12.5.1] Release PlayerSettings applied.");
        }

        [MenuItem("TD/Build/Windows x64 Baseline")]
        public static void BuildWindowsFromMenu()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputPath = Path.Combine(root, "output", "builds", "p1251_windows", "EmberlineDefense.exe");
            var resultPath = Path.Combine(root, "output", "builds", "p1251_windows", "build-result.json");
            var result = BuildWindows(
                outputPath,
                TDReleaseIdentity.SemanticVersion,
                TDReleaseIdentity.BuildNumber,
                false,
                "Mono",
                "working-tree",
                resultPath);
            if (!result.passed)
            {
                throw new InvalidOperationException(result.error);
            }
        }

        public static string BuildWindowsForMcp(
            string outputPath,
            string version,
            int buildNumber,
            bool development,
            string backend,
            string sourceRevision,
            string resultPath,
            bool automation = true)
        {
            var result = BuildWindows(
                outputPath,
                version,
                buildNumber,
                development,
                backend,
                sourceRevision,
                resultPath,
                automation);
            return JsonUtility.ToJson(result);
        }

        public static void BuildWindowsBatch()
        {
            var outputPath = ReadArgument("-tdOutput", string.Empty);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("-tdOutput is required.");
            }

            var version = ReadArgument("-tdVersion", TDReleaseIdentity.SemanticVersion);
            var buildNumber = ReadIntArgument("-tdBuildNumber", TDReleaseIdentity.BuildNumber);
            var development = ReadBoolArgument("-tdDevelopment", false);
            // Release-engineering decision (roadmap stage five): IL2CPP is
            // the shipping default — harder to tamper with, stricter loading.
            // Explicit -tdBackend Mono still builds the parity package.
            var backend = ReadArgument("-tdBackend", "IL2CPP");
            var revision = ReadArgument("-tdSourceRevision", "unknown");
            var automation = ReadBoolArgument("-tdAutomation", true);
            var resultPath = ReadArgument("-tdResult", Path.ChangeExtension(outputPath, ".build.json"));
            var result = BuildWindows(
                outputPath,
                version,
                buildNumber,
                development,
                backend,
                revision,
                resultPath,
                automation);
            if (!result.passed)
            {
                throw new InvalidOperationException(result.error);
            }
        }

        private static TDP1251BuildResult BuildWindows(
            string outputPath,
            string version,
            int buildNumber,
            bool development,
            string backend,
            string sourceRevision,
            string resultPath,
            bool automation = true)
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            var fullResultPath = Path.GetFullPath(resultPath);
            var result = new TDP1251BuildResult
            {
                schemaVersion = "p1252-windows-build-v1",
                generatedUtc = DateTime.UtcNow.ToString("o"),
                outputPath = fullOutputPath,
                bootstrapScene = TDReleaseIdentity.BootstrapScenePath,
                productName = TDReleaseIdentity.ProductName,
                companyName = TDReleaseIdentity.CompanyName,
                applicationIdentifier = TDReleaseIdentity.ApplicationIdentifier,
                version = NormalizeVersion(version),
                buildNumber = Mathf.Max(1, buildNumber),
                unityVersion = Application.unityVersion,
                backend = NormalizeBackend(backend),
                development = development,
                sourceRevision = string.IsNullOrWhiteSpace(sourceRevision) ? "unknown" : sourceRevision
            };

            // Automation builds compile the probe/autoplay/audit code in via the
            // TD_AUTOMATION define (CI smoke gates need it); everything else —
            // editor, development and hand-rolled release builds — compiles it
            // out. The symbol is applied for this build only and restored.
            var namedTarget = UnityEditor.Build.NamedBuildTarget.Standalone;
            var previousDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            var automationDefineApplied = automation &&
                !(";" + previousDefines + ";").Contains(";TD_AUTOMATION;");
            if (automationDefineApplied)
            {
                PlayerSettings.SetScriptingDefineSymbols(
                    namedTarget,
                    string.IsNullOrEmpty(previousDefines) ? "TD_AUTOMATION" : previousDefines + ";TD_AUTOMATION");
            }

            try
            {
                if (!File.Exists(Path.GetFullPath(TDReleaseIdentity.BootstrapScenePath)))
                {
                    throw new FileNotFoundException("Bootstrap scene is missing.", TDReleaseIdentity.BootstrapScenePath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
                result.releaseTexturesChanged = TDReleaseTextureSettings.ApplyReleaseSettings();
                ApplyPlayerSettings(result.version, result.buildNumber, result.backend);
                result.managedStrippingLevel = PlayerSettings
                    .GetManagedStrippingLevel(BuildTargetGroup.Standalone)
                    .ToString();
                result.stripEngineCode = PlayerSettings.stripEngineCode;
                result.il2cppCompilerConfiguration = PlayerSettings
                    .GetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone)
                    .ToString();
                var configuredIcons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
                var requiredIconSlots = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone).Length;
                for (var i = 0; i < configuredIcons.Length; i++)
                {
                    if (configuredIcons[i] != null)
                    {
                        result.iconSlotCount++;
                    }
                }
                result.iconConfigured = requiredIconSlots > 0 && result.iconSlotCount == requiredIconSlots;
                result.startupBackgroundConfigured = PlayerSettings.SplashScreen.background != null;
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(TDReleaseIdentity.BootstrapScenePath, true)
                };
                AssetDatabase.SaveAssets();

                var options = BuildOptions.StrictMode;
                options |= development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.CompressWithLz4
                    : BuildOptions.CompressWithLz4HC;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { TDReleaseIdentity.BootstrapScenePath },
                    locationPathName = fullOutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = options
                });

                result.buildGuid = report.summary.guid.ToString();
                result.totalSizeBytes = report.summary.totalSize;
                result.totalErrors = report.summary.totalErrors;
                result.totalWarnings = report.summary.totalWarnings;
                var warningMessages = new List<string>();
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Warning)
                        {
                            warningMessages.Add(message.content);
                        }
                    }
                }
                result.warningMessages = warningMessages.ToArray();
                result.durationSeconds = (float)report.summary.totalTime.TotalSeconds;
                result.outputExists = File.Exists(fullOutputPath);
                result.passed = report.summary.result == BuildResult.Succeeded &&
                                result.totalErrors == 0 && result.outputExists &&
                                result.iconConfigured && result.startupBackgroundConfigured;
                if (!result.passed)
                {
                    result.error = $"Unity build result={report.summary.result} errors={result.totalErrors} output={result.outputExists}";
                }
            }
            catch (Exception exception)
            {
                result.passed = false;
                result.error = exception.ToString();
                Debug.LogError($"[TD][P12.5.1] Windows build failed: {exception}");
            }
            finally
            {
                if (automationDefineApplied)
                {
                    PlayerSettings.SetScriptingDefineSymbols(namedTarget, previousDefines);
                }
            }

            WriteResult(fullResultPath, result);
            Debug.Log($"[TD][P12.5.2] Windows build passed={result.passed} output={result.outputPath}");
            return result;
        }

        private static void ApplyPlayerSettings(string version, int buildNumber, string backend)
        {
            PlayerSettings.companyName = TDReleaseIdentity.CompanyName;
            PlayerSettings.productName = TDReleaseIdentity.ProductName;
            PlayerSettings.bundleVersion = NormalizeVersion(version);
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Standalone,
                TDReleaseIdentity.ApplicationIdentifier);
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Standalone,
                NormalizeBackend(backend) == "IL2CPP"
                    ? ScriptingImplementation.IL2CPP
                    : ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(
                BuildTargetGroup.Standalone,
                ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCompilerConfiguration(
                BuildTargetGroup.Standalone,
                Il2CppCompilerConfiguration.Release);
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.enableFrameTimingStats = true;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                var iconSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
                var icons = new Texture2D[iconSizes.Length];
                for (var i = 0; i < icons.Length; i++)
                {
                    icons[i] = icon;
                }
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons);
            }
            else
            {
                Debug.LogWarning($"[TD][P12.5.1] App icon is missing: {IconPath}");
            }

            var splash = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
            if (splash != null)
            {
                PlayerSettings.SplashScreen.background = splash;
            }
            else
            {
                Debug.LogWarning($"[TD][P12.5.1] Startup background is missing: {SplashPath}");
            }

            PlayerSettings.SplashScreen.backgroundColor = new Color(0.035f, 0.043f, 0.047f, 1f);
            PlayerSettings.SplashScreen.show = true;
        }

        private static string NormalizeVersion(string version)
        {
            if (Version.TryParse(version, out var parsed))
            {
                return $"{Mathf.Max(0, parsed.Major)}.{Mathf.Max(0, parsed.Minor)}.{Mathf.Max(0, parsed.Build)}";
            }

            return TDReleaseIdentity.SemanticVersion;
        }

        private static string NormalizeBackend(string backend)
        {
            return string.Equals(backend, "IL2CPP", StringComparison.OrdinalIgnoreCase)
                ? "IL2CPP"
                : "Mono";
        }

        private static void WriteResult(string path, TDP1251BuildResult result)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(result, true));
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

        private static int ReadIntArgument(string name, int fallback)
        {
            return int.TryParse(ReadArgument(name, fallback.ToString()), out var parsed)
                ? parsed
                : fallback;
        }

        private static bool ReadBoolArgument(string name, bool fallback)
        {
            return bool.TryParse(ReadArgument(name, fallback.ToString()), out var parsed)
                ? parsed
                : fallback;
        }
    }
}
#endif
