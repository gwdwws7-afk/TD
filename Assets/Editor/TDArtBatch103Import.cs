// Emberline Defense art importer post-processor.
//
// Forces correct import settings on art assets added by the D103 batch:
//   - Assets/Resources/Art/Exam/P12/*.png          (4 device images)
//   - Assets/Resources/Art/anim/fx_*.png           (26 VFX frames, _00.._N)
//
// Required settings per project spec:
//   - TextureType: Sprite (2D and UI)
//   - SpriteMode:  Single
//   - PPU:         1024  (critical for VFX scaling, not 100 default)
//   - alphaIsTransparency: true
//   - FilterMode:  Bilinear
//   - Compression: library standard — default 512 compressed,
//                  Standalone 512 + DXT5 (matches anim/decal art)
//
// The post-processor forces these every time the asset is imported or
// reimported, so existing .meta files (e.g. with PPU=100 default) get
// corrected on the next project refresh.

using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace TD.Editor
{
    public class TDArtBatch103Import : AssetPostprocessor
    {
        // Path prefixes this script manages
        private const string EXAM_P12_DIR   = "Assets/Resources/Art/Exam/P12/";
        private const string ANIM_DIR       = "Assets/Resources/Art/anim/";
        private const string ART_ROOT       = "Assets/Resources/Art/";
        private const string UI_CAMPAIGN_DIR = "Assets/Resources/Art/UI/Campaign/";
        private const string UI_FORMATION_DIR = "Assets/Resources/Art/UI/Formation/";
        private const string BOSS_DIR = "Assets/Resources/Art/Boss/";
        private const int    PPU_TARGET     = 1024;

        // OnPreprocessTexture fires before texture is imported (raw import).
        // We set importer settings here.
        private void OnPreprocessTexture()
        {
            if (!IsManagedAsset(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            // Campaign board art budget per the memory-optimization pass
            // (commit 3351e4b): 1024 max. The full-screen background is
            // opaque and uses DXT5 elsewhere, so it alone gets DXT1.
            // Formation shares the Campaign tier: threat_strip (1536x192)
            // and intel_card (768x1024) display above 512px inside the
            // 1120x660 panel, so the 512 budget would blur them.
            var maxTex = assetPath.StartsWith(UI_CAMPAIGN_DIR, System.StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith(UI_FORMATION_DIR, System.StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith(BOSS_DIR, System.StringComparison.OrdinalIgnoreCase)
                ? 1024 : 512;
            var format = assetPath.EndsWith("world_map_bg.png", System.StringComparison.OrdinalIgnoreCase)
                ? TextureImporterFormat.DXT1 : TextureImporterFormat.DXT5;
            ApplyImporterSettings(importer, maxTex, format);
            Debug.Log($"[TDArt103] Applied import settings: {assetPath}");
        }

        // Apply the same settings post-import as well, to be safe.
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported.Concat(moved))
            {
                if (!IsManagedAsset(path))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                var currentPPU = importer.spritePixelsPerUnit;
                if (Mathf.Abs(currentPPU - PPU_TARGET) > 0.5f)
                {
                    var maxTex = path.StartsWith(UI_CAMPAIGN_DIR, System.StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith(UI_FORMATION_DIR, System.StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith(BOSS_DIR, System.StringComparison.OrdinalIgnoreCase)
                        ? 1024 : 512;
                    var format = path.EndsWith("world_map_bg.png", System.StringComparison.OrdinalIgnoreCase)
                        ? TextureImporterFormat.DXT1 : TextureImporterFormat.DXT5;
                    ApplyImporterSettings(importer, maxTex, format);
                    importer.SaveAndReimport();
                    Debug.Log($"[TDArt103] Reimported with corrected PPU: {path}");
                }
            }
        }

        private static bool IsManagedAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (path.StartsWith(EXAM_P12_DIR, System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (path.StartsWith(UI_CAMPAIGN_DIR, System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (path.StartsWith(UI_FORMATION_DIR, System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (path.StartsWith(BOSS_DIR, System.StringComparison.OrdinalIgnoreCase))
                return true;
            // Expansion assets under the art root: projectiles/impacts in
            // Combat/P11, badges in UI/P11, boss frames handled above.
            if (path.StartsWith(ART_ROOT, System.StringComparison.OrdinalIgnoreCase))
            {
                var expansionName = Path.GetFileName(path);
                if (expansionName.StartsWith("projectile_", System.StringComparison.Ordinal)
                    || expansionName.StartsWith("impact_", System.StringComparison.Ordinal)
                    || expansionName.StartsWith("boss_", System.StringComparison.Ordinal))
                    return true;
            }
            if (path.StartsWith(ANIM_DIR, System.StringComparison.OrdinalIgnoreCase)
                && (Path.GetFileName(path).StartsWith("fx_", System.StringComparison.Ordinal)
                    || (Path.GetFileName(path).StartsWith("tower_", System.StringComparison.Ordinal)
                        && (Path.GetFileName(path).Contains("_fire_")
                            || Path.GetFileName(path).Contains("_t2_")))))
                return true;
            // Decal/prop decals at Assets/Resources/Art/ root
            if (path.StartsWith(ART_ROOT, System.StringComparison.OrdinalIgnoreCase))
            {
                var fname = Path.GetFileName(path);
                if (fname.StartsWith("decal_", System.StringComparison.Ordinal)
                    || fname.StartsWith("prop_", System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ApplyImporterSettings(TextureImporter importer, int maxTexture = 512,
            TextureImporterFormat standaloneFormat = TextureImporterFormat.DXT5)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PPU_TARGET;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.isReadable = false; // game doesn't need CPU read access
            // Match the existing 1024px art library (anim frames, decals):
            // default platform 512 + compressed, Standalone 512 + DXT5.
            // Uncompressed RGBA32 would cost ~4MB per sprite in-editor.
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = maxTexture;
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = maxTexture;
            standalone.format = standaloneFormat;
            standalone.textureCompression = TextureImporterCompression.Compressed;
            standalone.compressionQuality = 80;
            importer.SetPlatformTextureSettings(standalone);
        }

        // Menu helper: reimport all managed assets in one shot.
        [MenuItem("TD/Art/Reimport Batch 103 (P12 Exam + anim fx + decal/prop)")]
        private static void ReimportAll()
        {
            var paths = Directory.GetFiles(EXAM_P12_DIR, "*.png", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(UI_CAMPAIGN_DIR, "*.png", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(UI_FORMATION_DIR, "*.png", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(ANIM_DIR, "fx_*.png", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(ANIM_DIR, "tower_*_fire_*.png", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(ANIM_DIR, "tower_*_t2_*.png", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(ART_ROOT, "decal_*.png", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(ART_ROOT, "prop_*.png", SearchOption.TopDirectoryOnly))
                .Where(p => p.Replace('\\', '/').StartsWith("Assets/"))
                .Select(p => p.Replace('\\', '/'))
                .Distinct()
                .ToArray();

            if (paths.Length == 0)
            {
                Debug.LogWarning("[TDArt103] No managed assets found to reimport.");
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
            Debug.Log($"[TDArt103] Reimported {paths.Length} assets with PPU={PPU_TARGET}.");
        }
    }
}
