using System;
using UnityEngine;

namespace TD
{
    public static class TDEnemyCatalogLoader
    {
        public static bool TryLoadFromResources(string resourcePath, out TDEnemyCatalogSet catalog, out string error)
        {
            catalog = null;
            error = string.Empty;

            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                error = $"Enemy catalog not found at Resources/{resourcePath}.json";
                return false;
            }

            try
            {
                catalog = JsonUtility.FromJson<TDEnemyCatalogSet>(textAsset.text);
            }
            catch (Exception ex)
            {
                // Malformed/empty JSON throws from FromJson — route it through
                // the error channel instead of crashing the boot path.
                error = $"Failed to parse enemy catalog JSON: {ex.Message}";
                catalog = null;
                return false;
            }

            if (catalog == null)
            {
                error = "Failed to parse enemy catalog JSON.";
                return false;
            }

            if (catalog.enemies == null || catalog.enemies.Length == 0)
            {
                error = "Enemy catalog must contain at least one enemy.";
                catalog = null;
                return false;
            }

            return true;
        }
    }
}
