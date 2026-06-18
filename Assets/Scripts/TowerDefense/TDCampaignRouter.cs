using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public static class TDCampaignRouter
    {
        private const string SavedLevelPrefKey = "td_campaign_selected_level";

        public static int GetSavedLevelIndex(int fallback)
        {
            return Mathf.Max(1, PlayerPrefs.GetInt(SavedLevelPrefKey, fallback));
        }

        public static void SaveLevelIndex(int levelIndex)
        {
            PlayerPrefs.SetInt(SavedLevelPrefKey, Mathf.Max(1, levelIndex));
            PlayerPrefs.Save();
        }

        public static bool TryResolveRoute(TDCampaignDefinition campaign, int requestedLevelIndex, out TDCampaignRoute route, out string error)
        {
            route = null;
            error = string.Empty;

            if (campaign == null || campaign.levels == null || campaign.levels.Length == 0)
            {
                error = "Campaign is empty.";
                return false;
            }

            var mapById = new Dictionary<string, TDCampaignMapDefinition>();
            for (var i = 0; i < campaign.maps.Length; i++)
            {
                var map = campaign.maps[i];
                if (map == null || string.IsNullOrWhiteSpace(map.mapId))
                {
                    continue;
                }

                mapById[map.mapId] = map;
            }

            var levelIndex = Mathf.Clamp(requestedLevelIndex, 1, campaign.totalLevels);
            TDCampaignLevelDefinition selectedLevel = null;
            for (var i = 0; i < campaign.levels.Length; i++)
            {
                var level = campaign.levels[i];
                if (level != null && level.levelIndex == levelIndex)
                {
                    selectedLevel = level;
                    break;
                }
            }

            if (selectedLevel == null)
            {
                error = $"Level {levelIndex} not found in campaign.";
                return false;
            }

            mapById.TryGetValue(selectedLevel.mapId, out var selectedMap);
            route = new TDCampaignRoute
            {
                level = selectedLevel,
                map = selectedMap,
                totalLevels = campaign.totalLevels,
                waveResourcePath = $"Data/waves/{selectedLevel.waveSetId}"
            };

            return true;
        }
    }
}
