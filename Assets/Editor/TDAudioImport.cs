// Emberline Defense audio importer post-processor.
//
// Regularizes import settings for audio delivered per
// design/spec/audio-design-spec-v1.md:
//
//   Assets/Resources/Audio/SFX/**       wav 44.1k/16-bit mono clips,
//                                        short and frequent -> PCM,
//                                        DecompressOnLoad, mono-guarded
//   Assets/Resources/Audio/Music/**     long seamless loops ->
//                                        Vorbis, Streaming, stereo kept
//   Assets/Resources/Audio/Ambience/**  ambient beds -> Vorbis,
//                                        Streaming, stereo kept
//
// Rationale: SFX fire dozens of PlayOneShot calls per wave at <=32
// concurrency - decompressed PCM avoids decoder spikes on the hot path.
// Music/ambience are minutes long - streaming Vorbis keeps memory flat.
//
// Menu TD/Audio/Reimport Audio applies the profile to existing assets.

using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace TD.Editor
{
    public class TDAudioImport : AssetPostprocessor
    {
        private const string AUDIO_ROOT = "Assets/Resources/Audio/";
        private const string SFX_DIR = "Assets/Resources/Audio/SFX/";
        private const string MUSIC_DIR = "Assets/Resources/Audio/Music/";
        private const string AMBIENCE_DIR = "Assets/Resources/Audio/Ambience/";

        private void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!IsManagedAsset(path))
                return;
            ApplySettings((AudioImporter)assetImporter, ResolveKind(path));
            Debug.Log($"[TDAudio] Applied import settings: {path}");
        }

        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported.Concat(moved))
            {
                var p = path.Replace('\\', '/');
                if (!IsManagedAsset(p))
                    continue;
                var importer = AssetImporter.GetAtPath(p) as AudioImporter;
                if (importer == null)
                    continue;
                var kind = ResolveKind(p);
                if (!MatchesProfile(importer, kind))
                {
                    ApplySettings(importer, kind);
                    importer.SaveAndReimport();
                    Debug.Log($"[TDAudio] Reimported with corrected profile: {p}");
                }
            }
        }

        private enum AudioKind { Sfx, Streaming }

        private static bool IsManagedAsset(string path)
        {
            return path.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase);
        }

        private static AudioKind ResolveKind(string path)
        {
            if (path.StartsWith(MUSIC_DIR, System.StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(AMBIENCE_DIR, System.StringComparison.OrdinalIgnoreCase))
                return AudioKind.Streaming;
            return AudioKind.Sfx;
        }

        private static bool MatchesProfile(AudioImporter importer, AudioKind kind)
        {
            var settings = importer.defaultSampleSettings;
            if (kind == AudioKind.Sfx)
            {
                return settings.loadType == AudioClipLoadType.DecompressOnLoad
                    && settings.compressionFormat == AudioCompressionFormat.PCM;
            }
            return settings.loadType == AudioClipLoadType.Streaming
                && settings.compressionFormat == AudioCompressionFormat.Vorbis;
        }

        private static void ApplySettings(AudioImporter importer, AudioKind kind)
        {
            var settings = importer.defaultSampleSettings;
            if (kind == AudioKind.Sfx)
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.forceToMono = true;   // spec: mono; guard stray stereo sources
                settings.preloadAudioData = true;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.quality = 1f;
            }
            else
            {
                settings.loadType = AudioClipLoadType.Streaming;
                importer.forceToMono = false;  // keep stereo beds/themes
                settings.preloadAudioData = false;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;       // ~q5 per spec
            }
            importer.defaultSampleSettings = settings;
        }

        [MenuItem("TD/Audio/Reimport Audio (Sfx PCM / Music+Ambience Vorbis)")]
        private static void ReimportAll()
        {
            var roots = new[] { SFX_DIR, MUSIC_DIR, AMBIENCE_DIR };
            var paths = roots
                .Where(Directory.Exists)
                .SelectMany(r => Directory.GetFiles(r, "*.wav", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(r, "*.ogg", SearchOption.AllDirectories)))
                .Select(p => p.Replace('\\', '/'))
                .ToArray();

            if (paths.Length == 0)
            {
                Debug.LogWarning("[TDAudio] No managed audio assets found.");
                return;
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in paths)
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log($"[TDAudio] Reimported {paths.Length} audio assets.");
        }
    }
}
