#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace TD.Editor
{
    public sealed class TDArtImporter : AssetPostprocessor
    {
        private const string ArtFolder = "Assets/Resources/Art/";

        private void OnPreprocessTexture()
        {
            if (!TDReleaseTextureSettings.IsReleaseArt(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            TDReleaseTextureSettings.Configure(importer, assetPath);
        }
    }

    public static class TDReleaseTextureSettings
    {
        private const string ArtFolder = "Assets/Resources/Art/";
        private const string BrandingFolder = "Assets/Art/Branding/";
        private const string P112CombatFolder = "Assets/Resources/Art/Combat/P11/";

        [MenuItem("TD/Build/Apply Release Texture Settings")]
        public static void ApplyReleaseSettingsFromMenu()
        {
            var changed = ApplyReleaseSettings();
            Debug.Log($"[TD][P12.5.2] Release texture settings applied to {changed} assets.");
        }

        public static int ApplyReleaseSettings()
        {
            var guids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { ArtFolder.TrimEnd('/'), BrandingFolder.TrimEnd('/') });
            var changed = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsReleaseArt(path) || AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                if (!Configure(importer, path))
                {
                    continue;
                }

                importer.SaveAndReimport();
                changed++;
            }

            return changed;
        }

        public static bool IsReleaseArt(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   (path.StartsWith(ArtFolder, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(BrandingFolder, StringComparison.OrdinalIgnoreCase));
        }

        public static bool Configure(TextureImporter importer, string path)
        {
            if (importer == null)
            {
                return false;
            }

            var changed = false;
            var maxSize = ResolveMaxTextureSize(path);
            changed |= SetIfDifferent(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
            changed |= SetIfDifferent(importer.spriteImportMode, SpriteImportMode.Single, value => importer.spriteImportMode = value);
            var pixelsPerUnit = path.StartsWith(P112CombatFolder, StringComparison.OrdinalIgnoreCase) ? 128f : 1024f;
            changed |= SetIfDifferent(importer.spritePixelsPerUnit, pixelsPerUnit, value => importer.spritePixelsPerUnit = value);
            changed |= SetIfDifferent(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
            changed |= SetIfDifferent(importer.alphaIsTransparency, true, value => importer.alphaIsTransparency = value);
            changed |= SetIfDifferent(
                importer.textureCompression,
                TextureImporterCompression.CompressedHQ,
                value => importer.textureCompression = value);
            changed |= SetIfDifferent(importer.filterMode, FilterMode.Bilinear, value => importer.filterMode = value);
            changed |= SetIfDifferent(importer.wrapMode, TextureWrapMode.Clamp, value => importer.wrapMode = value);
            changed |= SetIfDifferent(importer.maxTextureSize, maxSize, value => importer.maxTextureSize = value);

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            var format = importer.DoesSourceTextureHaveAlpha()
                ? TextureImporterFormat.DXT5
                : TextureImporterFormat.DXT1;
            if (!standalone.overridden ||
                standalone.maxTextureSize != maxSize ||
                standalone.format != format ||
                standalone.textureCompression != TextureImporterCompression.CompressedHQ ||
                standalone.compressionQuality != 80 ||
                standalone.crunchedCompression)
            {
                standalone.overridden = true;
                standalone.maxTextureSize = maxSize;
                standalone.format = format;
                standalone.textureCompression = TextureImporterCompression.CompressedHQ;
                standalone.compressionQuality = 80;
                standalone.crunchedCompression = false;
                importer.SetPlatformTextureSettings(standalone);
                changed = true;
            }

            return changed;
        }

        private static int ResolveMaxTextureSize(string path)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (path.StartsWith(BrandingFolder, StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("map_surface_", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("map_backdrop", StringComparison.OrdinalIgnoreCase))
            {
                return 2048;
            }

            if (path.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1024;
            }

            return 512;
        }

        private static bool SetIfDifferent<T>(T current, T expected, Action<T> setter)
        {
            if (Equals(current, expected))
            {
                return false;
            }

            setter(expected);
            return true;
        }
    }
}
#endif
