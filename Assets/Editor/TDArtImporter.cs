#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TD.Editor
{
    public sealed class TDArtImporter : AssetPostprocessor
    {
        private const string ArtFolder = "Assets/Resources/Art/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1024f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
        }
    }
}
#endif
