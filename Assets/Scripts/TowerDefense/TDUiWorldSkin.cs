using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    public static class TDUiWorldSkin
    {
        public const string CommandFramePath = "Art/UI/P12/frame_command";
        public const string CompactFramePath = "Art/UI/P12/frame_compact";
        public const string ActionFramePath = "Art/UI/P12/frame_action";
        public const string FontPath = "Fonts/BarlowSemiCondensed/BarlowSemiCondensed-Regular";

        public static readonly Color Coal = new(0.035f, 0.041f, 0.043f, 0.96f);
        public static readonly Color Gunmetal = new(0.12f, 0.14f, 0.14f, 0.98f);
        public static readonly Color Brass = new(0.78f, 0.55f, 0.22f, 1f);
        public static readonly Color Ember = new(0.96f, 0.34f, 0.08f, 1f);
        public static readonly Color Instrument = new(0.25f, 0.78f, 0.88f, 1f);

        private static readonly Dictionary<string, Sprite> SpriteCache = new();
        private static readonly Dictionary<string, Sprite> FramePartCache = new();
        private static Font _font;

        public static Font ResolveFont(Font fallback)
        {
            if (_font == null)
            {
                _font = Resources.Load<Font>(FontPath);
            }

            return _font != null ? _font : fallback;
        }

        public static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            if (SpriteCache.TryGetValue(resourcePath, out var cached) && cached != null)
            {
                return cached;
            }

            Sprite sprite = null;
            if (resourcePath.StartsWith("Art/UI/P12/"))
            {
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    var border = resourcePath == CommandFramePath
                        ? new Vector4(180f, 82f, 180f, 83f)
                        : resourcePath == ActionFramePath
                            ? new Vector4(76f, 52f, 76f, 52f)
                            : new Vector4(42f, 30f, 42f, 30f);
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0u,
                        SpriteMeshType.FullRect,
                        border);
                    sprite.name = $"P12 Sliced {texture.name}";
                }
            }

            sprite ??= Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                var sprites = Resources.LoadAll<Sprite>(resourcePath);
                if (sprites != null && sprites.Length > 0)
                {
                    sprite = sprites[0];
                }
            }

            if (sprite != null)
            {
                SpriteCache[resourcePath] = sprite;
            }

            return sprite;
        }

        public static void ApplyPanel(RectTransform panel, Color accent, bool compact = false, bool alert = false)
        {
            if (panel == null)
            {
                return;
            }

            if (panel.TryGetComponent<Image>(out var backdrop))
            {
                backdrop.color = new Color(
                    Mathf.Lerp(backdrop.color.r, Coal.r, 0.34f),
                    Mathf.Lerp(backdrop.color.g, Coal.g, 0.34f),
                    Mathf.Lerp(backdrop.color.b, Coal.b, 0.34f),
                    Mathf.Max(0.90f, backdrop.color.a));
            }

            var width = ResolveDimension(panel.rect.width, panel.sizeDelta.x);
            var height = ResolveDimension(panel.rect.height, panel.sizeDelta.y);
            var commandSurface = !compact && width >= 560f && height >= 240f;
            if (commandSurface)
            {
                CreateCommandFrame(panel, alert ? 0.98f : 0.92f);
            }
            else
            {
                CreateAuthoredFrame(
                    "Emberline Instrument Frame",
                    panel,
                    CompactFramePath,
                    accent,
                    alert ? 0.98f : 0.88f,
                    alert,
                    false);
            }

            TDUiP132Art.DecorateSurface(panel);

            var outline = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, alert ? 0.54f : 0.20f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        public static void ApplyInsetSurface(RectTransform panel)
        {
            if (panel == null)
            {
                return;
            }

            if (panel.TryGetComponent<Image>(out var backdrop))
            {
                backdrop.enabled = false;
            }
        }

        private static void CreateCommandFrame(RectTransform panel, float alpha)
        {
            var texture = Resources.Load<Texture2D>(CommandFramePath);
            if (texture == null)
            {
                return;
            }

            var frameObject = new GameObject("Emberline Command Frame", typeof(RectTransform));
            frameObject.transform.SetParent(panel, false);
            var root = frameObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            root.SetAsFirstSibling();

            var width = panel.rect.width > 1f ? panel.rect.width : Mathf.Abs(panel.sizeDelta.x);
            var height = panel.rect.height > 1f ? panel.rect.height : Mathf.Abs(panel.sizeDelta.y);
            // The source has deliberately oversized corner machinery. Production
            // panels use it as a restrained bezel so controls never sit inside it.
            var scale = Mathf.Clamp(Mathf.Min(width / 2200f, height / 1600f), 0.30f, 0.46f);
            var leftCornerWidth = 175f * scale;
            var rightCornerWidth = 190f * scale;
            var topLeftHeight = 78f * scale;
            var topRightHeight = 83f * scale;
            var bottomLeftHeight = 82f * scale;
            var bottomRightHeight = 88f * scale;
            var topEdgeHeight = 50f * scale;
            var bottomEdgeHeight = 52f * scale;
            var leftEdgeWidth = 55f * scale;
            var rightEdgeWidth = 55f * scale;
            var tint = new Color(1f, 1f, 1f, alpha);

            CreateCommandPart(
                "Frame Center",
                root,
                GetFramePartSprite(texture, CommandFramePath, "center", new Rect(160f, 55f, 450f, 150f)),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                tint);
            CreateCommandPart(
                "Frame Edge Top",
                root,
                GetFramePartSprite(texture, CommandFramePath, "edge_top", new Rect(160f, 205f, 450f, 50f)),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2((leftCornerWidth - rightCornerWidth) * 0.5f, -topEdgeHeight * 0.5f),
                new Vector2(-(leftCornerWidth + rightCornerWidth), topEdgeHeight),
                tint);
            CreateCommandPart(
                "Frame Edge Bottom",
                root,
                GetFramePartSprite(texture, CommandFramePath, "edge_bottom", new Rect(160f, 0f, 470f, 52f)),
                Vector2.zero,
                new Vector2(1f, 0f),
                new Vector2((leftCornerWidth - rightCornerWidth) * 0.5f, bottomEdgeHeight * 0.5f),
                new Vector2(-(leftCornerWidth + rightCornerWidth), bottomEdgeHeight),
                tint);
            CreateCommandPart(
                "Frame Edge Left",
                root,
                GetFramePartSprite(texture, CommandFramePath, "edge_left", new Rect(0f, 70f, 55f, 130f)),
                Vector2.zero,
                new Vector2(0f, 1f),
                new Vector2(leftEdgeWidth * 0.5f, (bottomLeftHeight - topLeftHeight) * 0.5f),
                new Vector2(leftEdgeWidth, -(bottomLeftHeight + topLeftHeight)),
                tint);
            CreateCommandPart(
                "Frame Edge Right",
                root,
                GetFramePartSprite(texture, CommandFramePath, "edge_right", new Rect(725f, 70f, 55f, 130f)),
                new Vector2(1f, 0f),
                Vector2.one,
                new Vector2(-rightEdgeWidth * 0.5f, (bottomRightHeight - topRightHeight) * 0.5f),
                new Vector2(rightEdgeWidth, -(bottomRightHeight + topRightHeight)),
                tint);
            CreateCommandPart(
                "Frame Corner TL",
                root,
                GetFramePartSprite(texture, CommandFramePath, "corner_tl", new Rect(0f, 185f, 175f, 78f)),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(leftCornerWidth * 0.5f, -topLeftHeight * 0.5f),
                new Vector2(leftCornerWidth, topLeftHeight),
                tint);
            CreateCommandPart(
                "Frame Corner TR",
                root,
                GetFramePartSprite(texture, CommandFramePath, "corner_tr", new Rect(590f, 180f, 190f, 83f)),
                Vector2.one,
                Vector2.one,
                new Vector2(-rightCornerWidth * 0.5f, -topRightHeight * 0.5f),
                new Vector2(rightCornerWidth, topRightHeight),
                tint);
            CreateCommandPart(
                "Frame Corner BL",
                root,
                GetFramePartSprite(texture, CommandFramePath, "corner_bl", new Rect(0f, 0f, 180f, 82f)),
                Vector2.zero,
                Vector2.zero,
                new Vector2((180f * scale) * 0.5f, bottomLeftHeight * 0.5f),
                new Vector2(180f * scale, bottomLeftHeight),
                tint);
            CreateCommandPart(
                "Frame Corner BR",
                root,
                GetFramePartSprite(texture, CommandFramePath, "corner_br", new Rect(600f, 0f, 180f, 88f)),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-(180f * scale) * 0.5f, bottomRightHeight * 0.5f),
                new Vector2(180f * scale, bottomRightHeight),
                tint);
        }

        private static Sprite GetFramePartSprite(Texture2D texture, string path, string key, Rect rect)
        {
            var cacheKey = $"{path}:{key}";
            if (FramePartCache.TryGetValue(cacheKey, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Sprite.Create(
                texture,
                rect,
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = $"P12 Command {key}";
            FramePartCache[cacheKey] = sprite;
            return sprite;
        }

        private static Image CreateCommandPart(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta, color);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            return image;
        }

        public static void ApplyMetric(RectTransform panel, Color accent)
        {
            if (panel == null)
            {
                return;
            }

            CreateAuthoredFrame("Emberline Gauge Cell", panel, CompactFramePath, accent, 0.92f, false, false);
        }

        public static void ApplyButton(Button button, Color accent, bool strong = false)
        {
            if (button == null || button.targetGraphic is not Image image)
            {
                return;
            }

            if (button.transform is RectTransform rect)
            {
                image.color = strong ? new Color(0.18f, 0.15f, 0.11f, 0.98f) : new Color(0.12f, 0.15f, 0.15f, 0.98f);
                CreateAuthoredFrame(
                    strong ? "Emberline Authored Action Bezel" : "Emberline Authored Control Bezel",
                    rect,
                    strong ? ActionFramePath : CompactFramePath,
                    accent,
                    strong ? 0.98f : 0.92f,
                    strong,
                    strong);
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.14f, 1.10f, 1.02f, 1f);
            colors.pressedColor = new Color(0.78f, 0.74f, 0.68f, 1f);
            colors.selectedColor = new Color(1.08f, 1.04f, 0.94f, 1f);
            colors.disabledColor = strong
                ? new Color(0.74f, 0.71f, 0.64f, 0.86f)
                : new Color(0.44f, 0.44f, 0.42f, 0.68f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
        }

        public static void ApplyRule(Image image, Color color, bool vertical = false)
        {
            if (image == null)
            {
                return;
            }

            var texture = Resources.Load<Texture2D>(CompactFramePath);
            if (texture == null)
            {
                return;
            }

            var rect = vertical ? new Rect(13f, 30f, 14f, 39f) : new Rect(42f, 12f, 138f, 12f);
            image.sprite = GetFramePartSprite(texture, CompactFramePath, vertical ? "rule_vertical" : "rule_horizontal", rect);
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = color;
        }

        public static void ApplySliderHandle(RectTransform handle)
        {
            if (handle == null || !handle.TryGetComponent<Image>(out var image))
            {
                return;
            }

            image.color = new Color(0.12f, 0.14f, 0.14f, 0.98f);
            CreateAuthoredFrame(
                "Emberline Authored Control Knob",
                handle,
                CompactFramePath,
                Brass,
                0.98f,
                false,
                false);
        }

        public static void ApplyText(Text label, bool strong = false)
        {
            if (label == null)
            {
                return;
            }

            label.font = TDLocalization.ResolveFont(ResolveFont(label.font));
            var shadow = label.gameObject.GetComponent<Shadow>() ?? label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, strong ? 0.82f : 0.64f);
            shadow.effectDistance = strong ? new Vector2(1f, -1f) : new Vector2(0.7f, -0.7f);
            shadow.useGraphicAlpha = true;
        }

        public static string BuildAuditReport(GameObject root, out bool pass)
        {
            if (root == null)
            {
                pass = false;
                return "ui.skin.audit.root=False\nui.skin.audit.pass=False";
            }

            var buttons = root.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.activeInHierarchy)
                .ToArray();
            var authoredButtons = buttons.Count(button =>
                button.GetComponentsInChildren<RectTransform>(true).Any(child =>
                    child.gameObject.activeInHierarchy &&
                    (child.name.StartsWith("Emberline Authored") || child.name.StartsWith("Authored "))));

            var panelNames = new[]
            {
                "Primary HUD",
                "Wave Intel",
                "Scenario Mechanic",
                "Tactical Feed",
                "Tower Build Bar",
                "Tower Upgrade Panel",
                "Resonance Command Panel",
                "Run Result",
                "Mission Board",
                "Prebattle Formation",
                "Campaign Profile",
                "P12.3 Command Options",
                "Playback And Accessibility",
                "Interactive Tutorial",
                "Combat Cinematic"
            };
            var activePanels = root.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.gameObject.activeInHierarchy && panelNames.Contains(rect.name))
                .ToArray();
            var authoredPanels = activePanels.Count(rect =>
                rect.GetComponentsInChildren<RectTransform>(true).Any(child =>
                    child.gameObject.activeInHierarchy &&
                    (child.name.StartsWith("Emberline ") || child.name.StartsWith("Authored "))));

            var texts = root.GetComponentsInChildren<Text>(true)
                .Where(text => text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
                .ToArray();
            var worldFontTexts = texts.Count(text =>
                text.font != null &&
                !text.font.name.Contains("LegacyRuntime") &&
                !text.font.name.Contains("Arial"));
            var canvas = root.GetComponent<Canvas>();
            var scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            var minimumPhysicalText = texts.Length == 0
                ? 0f
                : texts.Min(text => text.fontSize * scaleFactor);

            var images = root.GetComponentsInChildren<Image>(true)
                .Where(image => image.gameObject.activeInHierarchy)
                .ToArray();
            var legacyChrome = images.Count(image =>
                image.name == "Signal Rail" ||
                image.name == "Frame Rivet" ||
                image.name == "Action State Rail" ||
                image.name == "Action Header Rail" ||
                image.name == "Action Iron Frame" ||
                image.name == "Emberline Iron Frame");
            var rawDecor = images.Count(image =>
                image.sprite == null &&
                (image.name.Contains("Rule") ||
                 image.name.Contains("Divider") ||
                 image.name.StartsWith("Focus ")));

            var buttonPass = buttons.Length == authoredButtons;
            var panelPass = activePanels.Length == authoredPanels;
            var fontPass = texts.Length == worldFontTexts;
            var physicalTextPass = minimumPhysicalText >= 8.5f;
            var legacyPass = legacyChrome == 0 && rawDecor == 0;
            var p132Report = TDUiP132Art.BuildAuditReport(root, out var p132Pass);
            pass = buttonPass && panelPass && fontPass && physicalTextPass && legacyPass && p132Pass;
            return
                $"ui.skin.audit.buttons={buttonPass} [{authoredButtons}/{buttons.Length}]\n" +
                $"ui.skin.audit.panels={panelPass} [{authoredPanels}/{activePanels.Length}]\n" +
                $"ui.skin.audit.fonts={fontPass} [{worldFontTexts}/{texts.Length}]\n" +
                $"ui.skin.audit.minPhysicalText={physicalTextPass} [{minimumPhysicalText:0.0}px]\n" +
                $"ui.skin.audit.legacyChrome={legacyPass} [legacy={legacyChrome},rawDecor={rawDecor}]\n" +
                p132Report + "\n" +
                $"ui.skin.audit.pass={pass}";
        }

        private static void CreateAuthoredFrame(
            string name,
            RectTransform panel,
            string resourcePath,
            Color accent,
            float alpha,
            bool alert,
            bool action)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return;
            }

            var frameObject = new GameObject(name, typeof(RectTransform));
            frameObject.transform.SetParent(panel, false);
            var root = frameObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            root.SetAsFirstSibling();

            GetAuthoredRects(
                action,
                out var centerRect,
                out var topRect,
                out var bottomRect,
                out var leftRect,
                out var rightRect,
                out var topLeftRect,
                out var topRightRect,
                out var bottomLeftRect,
                out var bottomRightRect);

            var width = ResolveDimension(panel.rect.width, panel.sizeDelta.x);
            var height = ResolveDimension(panel.rect.height, panel.sizeDelta.y);
            var corner = Mathf.Clamp(height * 0.22f, 5f, action ? 15f : 13f);
            var rail = Mathf.Clamp(height * 0.075f, 2f, action ? 6f : 5f);
            var side = Mathf.Clamp(height * 0.11f, 3f, action ? 7f : 6f);
            if (width < 54f)
            {
                corner = Mathf.Min(corner, width * 0.22f);
            }

            var surfaceTint = new Color(0.70f, 0.73f, 0.74f, action ? 0.82f : 0.68f);
            CreateFramePart(
                "Authored Metal Surface",
                root,
                GetFramePartSprite(texture, resourcePath, "surface", centerRect),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-(side * 2f), -(rail * 2f)),
                surfaceTint);

            var frameTint = new Color(1f, 1f, 1f, alpha);
            CreateFramePart("Authored Rail Top", root, GetFramePartSprite(texture, resourcePath, "top", topRect), new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -rail * 0.5f), new Vector2(-(corner * 2f), rail), frameTint);
            CreateFramePart("Authored Rail Bottom", root, GetFramePartSprite(texture, resourcePath, "bottom", bottomRect), Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, rail * 0.5f), new Vector2(-(corner * 2f), rail), frameTint);
            CreateFramePart("Authored Rail Left", root, GetFramePartSprite(texture, resourcePath, "left", leftRect), Vector2.zero, new Vector2(0f, 1f), new Vector2(side * 0.5f, 0f), new Vector2(side, -(corner * 2f)), frameTint);
            CreateFramePart("Authored Rail Right", root, GetFramePartSprite(texture, resourcePath, "right", rightRect), new Vector2(1f, 0f), Vector2.one, new Vector2(-side * 0.5f, 0f), new Vector2(side, -(corner * 2f)), frameTint);

            CreateFrameCorner("Authored Corner TL", root, GetFramePartSprite(texture, resourcePath, "corner_tl", topLeftRect), new Vector2(0f, 1f), new Vector2(corner * 0.5f, -corner * 0.5f), corner, frameTint);
            CreateFrameCorner("Authored Corner TR", root, GetFramePartSprite(texture, resourcePath, "corner_tr", topRightRect), Vector2.one, new Vector2(-corner * 0.5f, -corner * 0.5f), corner, frameTint);
            CreateFrameCorner("Authored Corner BL", root, GetFramePartSprite(texture, resourcePath, "corner_bl", bottomLeftRect), Vector2.zero, new Vector2(corner * 0.5f, corner * 0.5f), corner, frameTint);
            CreateFrameCorner("Authored Corner BR", root, GetFramePartSprite(texture, resourcePath, "corner_br", bottomRightRect), new Vector2(1f, 0f), new Vector2(-corner * 0.5f, corner * 0.5f), corner, frameTint);

            var channel = CreateFramePart(
                "Authored State Channel",
                root,
                GetFramePartSprite(texture, resourcePath, action ? "channel_action" : "channel", bottomRect),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, Mathf.Max(1f, rail * 0.42f)),
                new Vector2(-(corner * 2.4f), alert ? 3f : 2f),
                new Color(accent.r, accent.g, accent.b, alert ? 0.96f : 0.72f));
            channel.transform.SetAsLastSibling();
        }

        private static void GetAuthoredRects(
            bool action,
            out Rect center,
            out Rect top,
            out Rect bottom,
            out Rect left,
            out Rect right,
            out Rect topLeft,
            out Rect topRight,
            out Rect bottomLeft,
            out Rect bottomRight)
        {
            if (action)
            {
                center = new Rect(64f, 38f, 260f, 94f);
                top = new Rect(72f, 142f, 244f, 14f);
                bottom = new Rect(72f, 14f, 244f, 14f);
                left = new Rect(14f, 46f, 18f, 78f);
                right = new Rect(356f, 46f, 18f, 78f);
                topLeft = new Rect(2f, 130f, 48f, 38f);
                topRight = new Rect(338f, 130f, 48f, 38f);
                bottomLeft = new Rect(2f, 2f, 48f, 38f);
                bottomRight = new Rect(338f, 2f, 48f, 38f);
                return;
            }

            center = new Rect(36f, 27f, 150f, 45f);
            top = new Rect(42f, 75f, 138f, 12f);
            bottom = new Rect(42f, 12f, 138f, 12f);
            left = new Rect(13f, 30f, 14f, 39f);
            right = new Rect(195f, 30f, 14f, 39f);
            topLeft = new Rect(7f, 66f, 34f, 28f);
            topRight = new Rect(181f, 66f, 34f, 28f);
            bottomLeft = new Rect(7f, 5f, 34f, 28f);
            bottomRight = new Rect(181f, 5f, 34f, 28f);
        }

        private static Image CreateFramePart(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, position, size, color);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            return image;
        }

        private static void CreateFrameCorner(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 position, float size, Color color)
        {
            CreateFramePart(name, parent, sprite, anchor, anchor, position, new Vector2(size, size), color);
        }

        private static float ResolveDimension(float rectValue, float sizeDelta)
        {
            return rectValue > 1f ? rectValue : Mathf.Abs(sizeDelta);
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var image = gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
