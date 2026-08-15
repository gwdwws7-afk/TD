using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    public enum TDUiSurfaceIdentity
    {
        None = -1,
        Battle = 0,
        WaveIntel = 1,
        TowerUpgrade = 2,
        Tutorial = 3,
        Campaign = 4,
        Formation = 5,
        Archive = 6,
        Settings = 7,
        Result = 8
    }

    public enum TDUiP132Icon
    {
        Wave = 0,
        Integrity = 1,
        Budget = 2,
        Enemy = 3,
        Build = 4,
        Armor = 5,
        FireResistance = 6,
        FrostResistance = 7,
        ShockResistance = 8,
        GravityResistance = 9,
        ArmorBreak = 10,
        Slow = 11,
        Exposed = 12,
        Resonance = 13,
        Stagger = 14,
        EmberCommand = 15,
        FractureCommand = 16,
        RouteSwitch = 17,
        Purge = 18,
        BossBreak = 19,
        Rating = 20,
        Damage = 21,
        Kills = 22,
        Hotspot = 23,
        Gamepad = 24
    }

    public static class TDUiP132Art
    {
        public const string IconPrefix = "p13-icon:";
        public const string SurfacePrefix = "p13-surface:";
        public const string IdentityAtlasPath = "Art/UI/P13/surface_identity_atlas_v2";
        public const string IconAtlasPath = "Art/UI/P13/hud_icon_atlas_v2";

        private static readonly Dictionary<string, Sprite> Cache = new();

        public static string IconPath(TDUiP132Icon icon)
        {
            return IconPrefix + icon;
        }

        public static Sprite LoadVirtualSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            if (Cache.TryGetValue(resourcePath, out var cached) && cached != null)
            {
                return cached;
            }

            Sprite sprite = null;
            if (resourcePath.StartsWith(IconPrefix, StringComparison.Ordinal) &&
                Enum.TryParse(resourcePath.Substring(IconPrefix.Length), out TDUiP132Icon icon))
            {
                sprite = LoadAtlasCell(IconAtlasPath, 5, 5, (int)icon, "P13 Icon");
            }
            else if (resourcePath.StartsWith(SurfacePrefix, StringComparison.Ordinal) &&
                     Enum.TryParse(resourcePath.Substring(SurfacePrefix.Length), out TDUiSurfaceIdentity surface))
            {
                sprite = LoadAtlasCell(IdentityAtlasPath, 3, 3, (int)surface, "P13 Surface");
            }

            if (sprite != null)
            {
                Cache[resourcePath] = sprite;
            }

            return sprite;
        }

        public static void DecorateSurface(RectTransform panel)
        {
            if (panel == null || panel.Find("P13 Identity Rail") != null)
            {
                return;
            }

            var identity = ResolveSurface(panel.name);
            if (identity == TDUiSurfaceIdentity.None)
            {
                return;
            }

            var width = panel.rect.width > 1f ? panel.rect.width : Mathf.Abs(panel.sizeDelta.x);
            var height = panel.rect.height > 1f ? panel.rect.height : Mathf.Abs(panel.sizeDelta.y);
            var compact = height <= 190f || width <= 430f;

            if (!compact)
            {
                var iconObject = new GameObject("P13 Surface Identity", typeof(RectTransform));
                iconObject.transform.SetParent(panel, false);
                var iconRect = iconObject.GetComponent<RectTransform>();
                var size = Mathf.Clamp(Mathf.Min(width, height) * 0.09f, 50f, 58f);
                iconRect.anchorMin = new Vector2(0f, 1f);
                iconRect.anchorMax = new Vector2(0f, 1f);
                iconRect.pivot = new Vector2(0f, 1f);
                iconRect.anchoredPosition = new Vector2(18f, -12f);
                iconRect.sizeDelta = new Vector2(size, size);

                var iconImage = iconObject.AddComponent<Image>();
                iconImage.sprite = LoadVirtualSprite(SurfacePrefix + identity);
                iconImage.color = new Color(1f, 1f, 1f, 0.92f);
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                iconObject.transform.SetSiblingIndex(Mathf.Min(1, panel.childCount - 1));
            }

            var accent = ResolveAccent(identity);
            var railObject = new GameObject("P13 Identity Rail", typeof(RectTransform));
            railObject.transform.SetParent(panel, false);
            var railRect = railObject.GetComponent<RectTransform>();
            railRect.anchorMin = new Vector2(0f, 1f);
            railRect.anchorMax = new Vector2(1f, 1f);
            railRect.pivot = new Vector2(0.5f, 1f);
            railRect.anchoredPosition = new Vector2(0f, compact ? -2f : -4f);
            railRect.sizeDelta = new Vector2(compact ? -30f : -64f, compact ? 2f : 3f);
            var railImage = railObject.AddComponent<Image>();
            railImage.color = new Color(accent.r, accent.g, accent.b, compact ? 0.62f : 0.78f);
            railImage.raycastTarget = false;
            TDUiWorldSkin.ApplyRule(railImage, railImage.color);
            railObject.transform.SetSiblingIndex(Mathf.Min(2, panel.childCount - 1));
        }

        public static Color ResolveAccent(TDUiSurfaceIdentity identity)
        {
            return identity switch
            {
                TDUiSurfaceIdentity.Battle => new Color(0.92f, 0.61f, 0.22f, 1f),
                TDUiSurfaceIdentity.WaveIntel => new Color(0.96f, 0.42f, 0.16f, 1f),
                TDUiSurfaceIdentity.TowerUpgrade => new Color(0.28f, 0.78f, 0.86f, 1f),
                TDUiSurfaceIdentity.Tutorial => new Color(0.38f, 0.82f, 0.92f, 1f),
                TDUiSurfaceIdentity.Campaign => new Color(0.82f, 0.62f, 0.28f, 1f),
                TDUiSurfaceIdentity.Formation => new Color(0.32f, 0.80f, 0.62f, 1f),
                TDUiSurfaceIdentity.Archive => new Color(0.72f, 0.50f, 0.30f, 1f),
                TDUiSurfaceIdentity.Settings => new Color(0.38f, 0.76f, 0.88f, 1f),
                TDUiSurfaceIdentity.Result => new Color(0.96f, 0.70f, 0.26f, 1f),
                _ => TDUiWorldSkin.Brass
            };
        }

        public static TDUiSurfaceIdentity ResolveSurface(string panelName)
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                return TDUiSurfaceIdentity.None;
            }

            if (panelName == "Run Result")
            {
                return TDUiSurfaceIdentity.Result;
            }

            if (panelName == "Wave Intel" || panelName == "Combat Cinematic")
            {
                return TDUiSurfaceIdentity.WaveIntel;
            }

            if (panelName == "Tower Upgrade Panel")
            {
                return TDUiSurfaceIdentity.TowerUpgrade;
            }

            if (panelName == "Interactive Tutorial")
            {
                return TDUiSurfaceIdentity.Tutorial;
            }

            if (panelName == "Prebattle Formation")
            {
                return TDUiSurfaceIdentity.Formation;
            }

            if (panelName == "Campaign Profile")
            {
                return TDUiSurfaceIdentity.Archive;
            }

            if (panelName == "Mission Board")
            {
                return TDUiSurfaceIdentity.Campaign;
            }

            if (panelName == "P12.3 Command Options" || panelName == "Playback And Accessibility")
            {
                return TDUiSurfaceIdentity.Settings;
            }

            if (panelName == "Primary HUD" ||
                panelName == "Tactical Feed" ||
                panelName == "Scenario Mechanic" ||
                panelName == "Resonance Command Panel")
            {
                return TDUiSurfaceIdentity.Battle;
            }

            return TDUiSurfaceIdentity.None;
        }

        public static string BuildAuditReport(GameObject root, out bool pass)
        {
            if (root == null)
            {
                pass = false;
                return "p13.2.ui.root=False\np13.2.ui.pass=False";
            }

            var panels = root.GetComponentsInChildren<RectTransform>(true);
            var eligible = new List<RectTransform>();
            foreach (var panel in panels)
            {
                if (panel.gameObject.activeInHierarchy && ResolveSurface(panel.name) != TDUiSurfaceIdentity.None)
                {
                    eligible.Add(panel);
                }
            }

            var decorated = 0;
            foreach (var panel in eligible)
            {
                var width = panel.rect.width > 1f ? panel.rect.width : Mathf.Abs(panel.sizeDelta.x);
                var height = panel.rect.height > 1f ? panel.rect.height : Mathf.Abs(panel.sizeDelta.y);
                var compact = height <= 190f || width <= 430f;
                if (panel.Find("P13 Identity Rail") != null &&
                    (compact || panel.Find("P13 Surface Identity") != null))
                {
                    decorated++;
                }
            }

            var iconAtlas = Resources.Load<Texture2D>(IconAtlasPath) != null;
            var identityAtlas = Resources.Load<Texture2D>(IdentityAtlasPath) != null;
            pass = iconAtlas && identityAtlas && decorated == eligible.Count;
            return
                $"p13.2.ui.iconAtlas={iconAtlas}\n" +
                $"p13.2.ui.identityAtlas={identityAtlas}\n" +
                $"p13.2.ui.decorated={decorated == eligible.Count} [{decorated}/{eligible.Count}]\n" +
                $"p13.2.ui.pass={pass}";
        }

        private static Sprite LoadAtlasCell(string resourcePath, int columns, int rows, int index, string label)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null || index < 0 || index >= columns * rows)
            {
                return null;
            }

            var column = index % columns;
            var rowFromTop = index / columns;
            var xMin = Mathf.RoundToInt(column * texture.width / (float)columns);
            var xMax = Mathf.RoundToInt((column + 1) * texture.width / (float)columns);
            var yMin = Mathf.RoundToInt((rows - rowFromTop - 1) * texture.height / (float)rows);
            var yMax = Mathf.RoundToInt((rows - rowFromTop) * texture.height / (float)rows);
            var sprite = Sprite.Create(
                texture,
                new Rect(xMin, yMin, xMax - xMin, yMax - yMin),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = $"{label} {index:00}";
            return sprite;
        }
    }
}
