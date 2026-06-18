using System;

namespace TD
{
    [Serializable]
    public sealed class TDCampaignDefinition
    {
        public string schemaVersion;
        public string campaignId;
        public string displayName;
        public int totalLevels;
        public TDCampaignChapterDefinition[] chapters;
        public TDCampaignMapDefinition[] maps;
        public TDCampaignLevelDefinition[] levels;
        public TDCampaignGlobalRules globalRules;
    }

    [Serializable]
    public sealed class TDCampaignChapterDefinition
    {
        public string chapterId;
        public string displayName;
        public int startLevel;
        public int endLevel;
        public string[] themeTags;
    }

    [Serializable]
    public sealed class TDCampaignMapDefinition
    {
        public string mapId;
        public string displayName;
        public string sceneKey;
        public string tacticalHook;
    }

    [Serializable]
    public sealed class TDCampaignLevelDefinition
    {
        public int levelIndex;
        public string levelId;
        public string chapterId;
        public string mapId;
        public string waveSetId;
        public string[] goalTags;
        public string[] newTowerUnlocks;
        public string[] newEnemyUnlocks;
        public float recommendedPower;
        public bool bossLevel;
    }

    [Serializable]
    public sealed class TDCampaignGlobalRules
    {
        public int maxFailureReasonsShown;
        public int resonanceEnabledFromLevel;
        public bool allowEarlyWaveDispatch;
    }

    public sealed class TDCampaignRoute
    {
        public TDCampaignLevelDefinition level;
        public TDCampaignMapDefinition map;
        public int totalLevels;
        public string waveResourcePath;
    }
}
