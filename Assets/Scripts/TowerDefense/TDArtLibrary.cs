using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public static class TDArtLibrary
    {
        private const int FallbackTextureSize = 64;
        private const int ShadowTextureSize = 128;
        private static readonly Dictionary<string, Sprite> FallbackCache = new();
        private static Sprite _softShadowSprite;

        public static Sprite LoadSpriteOrFallback(string resourcePath, Color fallbackColor)
        {
            var resourceSprite = Resources.Load<Sprite>(resourcePath);
            if (resourceSprite != null)
            {
                return resourceSprite;
            }

            var cacheKey = $"{resourcePath}:{ColorUtility.ToHtmlStringRGBA(fallbackColor)}";
            if (FallbackCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var texture = new Texture2D(FallbackTextureSize, FallbackTextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"fallback_{resourcePath.Replace("/", "_")}"
            };

            var borderColor = fallbackColor * 0.65f;
            var pixels = new Color[FallbackTextureSize * FallbackTextureSize];
            for (var y = 0; y < FallbackTextureSize; y++)
            {
                for (var x = 0; x < FallbackTextureSize; x++)
                {
                    var isBorder = x < 3 || y < 3 || x >= FallbackTextureSize - 3 || y >= FallbackTextureSize - 3;
                    pixels[(y * FallbackTextureSize) + x] = isBorder ? borderColor : fallbackColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, FallbackTextureSize, FallbackTextureSize),
                new Vector2(0.5f, 0.5f),
                FallbackTextureSize);
            sprite.name = texture.name;

            FallbackCache[cacheKey] = sprite;
            return sprite;
        }

        public static Sprite GetSoftShadowSprite()
        {
            if (_softShadowSprite != null)
            {
                return _softShadowSprite;
            }

            var texture = new Texture2D(ShadowTextureSize, ShadowTextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "soft_shadow_blob"
            };

            var pixels = new Color[ShadowTextureSize * ShadowTextureSize];
            var center = (ShadowTextureSize - 1) * 0.5f;
            var invRadius = 1f / center;

            for (var y = 0; y < ShadowTextureSize; y++)
            {
                for (var x = 0; x < ShadowTextureSize; x++)
                {
                    var dx = (x - center) * invRadius;
                    var dy = (y - center) * invRadius;
                    var r = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var falloff = Mathf.Clamp01(1f - Mathf.Pow(r, 1.55f));
                    var alpha = falloff * falloff * 0.85f;
                    pixels[(y * ShadowTextureSize) + x] = new Color(0f, 0f, 0f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _softShadowSprite = Sprite.Create(
                texture,
                new Rect(0, 0, ShadowTextureSize, ShadowTextureSize),
                new Vector2(0.5f, 0.5f),
                ShadowTextureSize);
            _softShadowSprite.name = texture.name;
            return _softShadowSprite;
        }
    }
}
