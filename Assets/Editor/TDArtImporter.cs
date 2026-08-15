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

        private void OnPostprocessTexture(Texture2D texture)
        {
            var normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/Resources/Art/anim/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = System.IO.Path.GetFileNameWithoutExtension(normalized);
            if (!fileName.StartsWith("enemy_", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TDFootAnchorBaking.Bake(texture, fileName);
        }
    }

    /// <summary>
    /// Keeps Resources/Art/anim/foot_anchors.json in sync with enemy art.
    /// Runtime sprites use FullRect meshes and non-readable textures, so the
    /// opaque-pixel bottom padding must be baked at import time for the
    /// feet-on-route anchoring (see TDArtLibrary.ResolveFootAnchorLocalY).
    /// </summary>
    public static class TDFootAnchorBaking
    {
        private const string JsonPath = "Assets/Resources/Art/anim/foot_anchors.json";
        private const byte AlphaThreshold = 12;

        public static void Bake(Texture2D texture, string spriteName)
        {
            try
            {
                var width = texture.width;
                var height = texture.height;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                var pixels = texture.GetPixels32();
                var bottomPadding = 1f;
                for (var y = 0; y < height; y++)
                {
                    if (!RowHasOpaquePixel(pixels, width, y))
                    {
                        continue;
                    }

                    bottomPadding = y / (float)height;
                    break;
                }

                var topPadding = 1f;
                for (var y = height - 1; y >= 0; y--)
                {
                    if (!RowHasOpaquePixel(pixels, width, y))
                    {
                        continue;
                    }

                    topPadding = (height - 1 - y) / (float)height;
                    break;
                }

                WriteEntry(spriteName, bottomPadding, topPadding);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TD][TDArtImporter] foot anchor baking failed for {spriteName}: {ex.Message}");
            }
        }

        private static bool RowHasOpaquePixel(Color32[] pixels, int width, int y)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                if (pixels[rowOffset + x].a >= AlphaThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteEntry(string spriteName, float bottom, float top)
        {
            var entries = new System.Collections.Generic.SortedDictionary<string, (float b, float t)>();
            if (System.IO.File.Exists(JsonPath))
            {
                var existing = System.IO.File.ReadAllText(JsonPath);
                var pattern = new System.Text.RegularExpressions.Regex(
                    "\"([^\"]+)\"\\s*:\\s*\\{\\s*\"b\"\\s*:\\s*([-0-9.eE+]+)\\s*,\\s*\"t\"\\s*:\\s*([-0-9.eE+]+)\\s*\\}");
                foreach (System.Text.RegularExpressions.Match match in pattern.Matches(existing))
                {
                    entries[match.Groups[1].Value] = (
                        float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            entries[spriteName] = ((float)Math.Round(bottom, 4), (float)Math.Round(top, 4));

            var builder = new System.Text.StringBuilder("{\"schemaVersion\":1,\"anchors\":{");
            var first = true;
            foreach (var pair in entries)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append('"').Append(pair.Key).Append("\":{\"b\":")
                    .Append(pair.Value.b.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(",\"t\":")
                    .Append(pair.Value.t.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                    .Append('}');
            }

            builder.Append("}}");
            System.IO.File.WriteAllText(JsonPath, builder.ToString());
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

            // Full-quality UI art (title screen background + logo): uncompressed, 4096 max.
            var isUiArt = path.ToLowerInvariant().Contains("/branding/");
            var changed = false;
            var maxSize = ResolveMaxTextureSize(path);
            if (isUiArt)
            {
                maxSize = 4096;
            }

            changed |= SetIfDifferent(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
            changed |= SetIfDifferent(importer.spriteImportMode, SpriteImportMode.Single, value => importer.spriteImportMode = value);
            var pixelsPerUnit = path.StartsWith(P112CombatFolder, StringComparison.OrdinalIgnoreCase) ? 128f : 1024f;
            changed |= SetIfDifferent(importer.spritePixelsPerUnit, pixelsPerUnit, value => importer.spritePixelsPerUnit = value);
            changed |= SetIfDifferent(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
            changed |= SetIfDifferent(importer.alphaIsTransparency, true, value => importer.alphaIsTransparency = value);
            changed |= SetIfDifferent(
                importer.textureCompression,
                isUiArt ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ,
                value => importer.textureCompression = value);
            changed |= SetIfDifferent(importer.filterMode, FilterMode.Bilinear, value => importer.filterMode = value);
            changed |= SetIfDifferent(importer.wrapMode, TextureWrapMode.Clamp, value => importer.wrapMode = value);
            changed |= SetIfDifferent(importer.maxTextureSize, maxSize, value => importer.maxTextureSize = value);

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            if (isUiArt)
            {
                // UI art: uncompressed, auto format, 4096.
                standalone.overridden = true;
                standalone.maxTextureSize = 4096;
                standalone.format = TextureImporterFormat.Automatic;
                standalone.textureCompression = TextureImporterCompression.Uncompressed;
                standalone.compressionQuality = 100;
                standalone.crunchedCompression = false;
                importer.SetPlatformTextureSettings(standalone);
                return true;
            }

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
