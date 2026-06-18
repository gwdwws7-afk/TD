using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public static class TDCampaignLoader
    {
        public static bool TryLoadFromResources(string resourcePath, out TDCampaignDefinition campaign, out string error)
        {
            campaign = null;
            error = string.Empty;

            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                error = $"Campaign config not found at Resources/{resourcePath}.json";
                return false;
            }

            campaign = JsonUtility.FromJson<TDCampaignDefinition>(textAsset.text);
            if (campaign == null)
            {
                error = "Failed to parse campaign config JSON.";
                return false;
            }

            if (!ValidateCampaign(campaign, out error))
            {
                campaign = null;
                return false;
            }

            return true;
        }

        private static bool ValidateCampaign(TDCampaignDefinition campaign, out string error)
        {
            error = string.Empty;
            if (campaign.chapters == null || campaign.maps == null || campaign.levels == null)
            {
                error = "Campaign config missing chapters/maps/levels.";
                return false;
            }

            if (campaign.totalLevels <= 0 || campaign.levels.Length != campaign.totalLevels)
            {
                error = "Campaign totalLevels must match levels length.";
                return false;
            }

            if (!string.Equals(campaign.schemaVersion, "campaign-schema-v1"))
            {
                error = "Campaign schemaVersion must be campaign-schema-v1.";
                return false;
            }

            var chapterIds = new HashSet<string>();
            var chapterById = new Dictionary<string, TDCampaignChapterDefinition>();
            for (var i = 0; i < campaign.chapters.Length; i++)
            {
                var chapter = campaign.chapters[i];
                if (chapter == null || string.IsNullOrWhiteSpace(chapter.chapterId))
                {
                    error = "Campaign chapter contains null/empty chapterId.";
                    return false;
                }

                if (!chapterIds.Add(chapter.chapterId))
                {
                    error = $"Campaign chapterId duplicated: {chapter.chapterId}";
                    return false;
                }

                if (chapter.startLevel <= 0 || chapter.endLevel < chapter.startLevel)
                {
                    error = $"Campaign chapter level range invalid: {chapter.chapterId}";
                    return false;
                }

                chapterById[chapter.chapterId] = chapter;
            }

            var mapIds = new HashSet<string>();
            var mapUsage = new Dictionary<string, int>();
            for (var i = 0; i < campaign.maps.Length; i++)
            {
                var map = campaign.maps[i];
                if (map == null || string.IsNullOrWhiteSpace(map.mapId))
                {
                    error = "Campaign map contains null/empty mapId.";
                    return false;
                }

                if (!mapIds.Add(map.mapId))
                {
                    error = $"Campaign mapId duplicated: {map.mapId}";
                    return false;
                }

                mapUsage[map.mapId] = 0;
            }

            var levelIndexes = new HashSet<int>();
            for (var i = 0; i < campaign.levels.Length; i++)
            {
                var level = campaign.levels[i];
                if (level == null || level.levelIndex <= 0)
                {
                    error = "Campaign level contains null/invalid levelIndex.";
                    return false;
                }

                if (!levelIndexes.Add(level.levelIndex))
                {
                    error = $"Campaign levelIndex duplicated: {level.levelIndex}";
                    return false;
                }

                if (!chapterIds.Contains(level.chapterId))
                {
                    error = $"Campaign level {level.levelIndex} references missing chapterId: {level.chapterId}";
                    return false;
                }

                var chapter = chapterById[level.chapterId];
                if (level.levelIndex < chapter.startLevel || level.levelIndex > chapter.endLevel)
                {
                    error = $"Campaign level {level.levelIndex} is outside chapter range: {level.chapterId}";
                    return false;
                }

                if (!mapIds.Contains(level.mapId))
                {
                    error = $"Campaign level {level.levelIndex} references missing mapId: {level.mapId}";
                    return false;
                }

                mapUsage[level.mapId] = mapUsage[level.mapId] + 1;

                if (string.IsNullOrWhiteSpace(level.waveSetId))
                {
                    error = $"Campaign level {level.levelIndex} has empty waveSetId.";
                    return false;
                }

                var waveConfig = Resources.Load<TextAsset>($"Data/waves/{level.waveSetId}");
                if (waveConfig == null)
                {
                    error = $"Campaign level {level.levelIndex} missing wave config: Data/waves/{level.waveSetId}.json";
                    return false;
                }
            }

            for (var idx = 1; idx <= campaign.totalLevels; idx++)
            {
                if (!levelIndexes.Contains(idx))
                {
                    error = $"Campaign levelIndex is not contiguous. Missing level {idx}.";
                    return false;
                }
            }

            if (campaign.totalLevels >= 20)
            {
                foreach (var kv in mapUsage)
                {
                    if (kv.Value < 4)
                    {
                        error = $"Campaign mapId must appear at least 4 times in 20-level scope: {kv.Key}";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
