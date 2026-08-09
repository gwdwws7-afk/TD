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
        public TDCampaignDifficultyDefinition[] difficultyTiers;
        public TDCampaignMetaProgressionDefinition metaProgression;
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
        public TDCampaignChapterRewardDefinition reward;
        public TDCampaignMutatorDefinition challengeRemix;
    }

    [Serializable]
    public sealed class TDCampaignMetaProgressionDefinition
    {
        public TDCampaignTacticalProtocolDefinition[] tacticalProtocols;
        public TDCampaignMetaRewardDefinition[] ratingRewards;
        public TDCampaignMetaRewardDefinition[] codexRewards;
    }

    [Serializable]
    public sealed class TDCampaignMetaRewardDefinition
    {
        public string rewardId;
        public string displayName;
        public string description;
        public string sourceType;
        public int threshold;
        public string unlockProtocolId;
    }

    [Serializable]
    public sealed class TDCampaignTacticalProtocolDefinition
    {
        public string protocolId;
        public string displayName;
        public string description;
        public string unlockHint;
        public int startingBudgetDelta;
        public int prepSecondsDelta;
        public int scenarioChargeDelta;
        public float enemyHpMultiplier;
        public float rewardMultiplier;
        public float scenarioCostMultiplier;
    }

    [Serializable]
    public sealed class TDCampaignDifficultyDefinition
    {
        public int tier;
        public string difficultyId;
        public string displayName;
        public string description;
        public TDCampaignMutatorDefinition modifiers;
    }

    [Serializable]
    public sealed class TDCampaignChapterRewardDefinition
    {
        public string rewardId;
        public string displayName;
        public string description;
        public int startingBudgetBonus;
        public int startingIntegrityBonus;
        public float resonanceGainMultiplier;
    }

    [Serializable]
    public sealed class TDCampaignMapDefinition
    {
        public string mapId;
        public string displayName;
        public string sceneKey;
        public string tacticalHook;
        public TDCampaignScenarioMechanicDefinition mechanic;
    }

    [Serializable]
    public sealed class TDCampaignScenarioMechanicDefinition
    {
        public string mechanicId;
        public string displayName;
        public string description;
        public string commandLabel;
        public string mechanicType;
        public int maxCharges;
        public int budgetCost;
        public float reinforcementDelaySeconds;
        public float effectDurationSeconds;
        public float[] bossPhaseThresholds;
    }

    [Serializable]
    public sealed class TDCampaignScenarioPlan
    {
        public bool milestoneExam;
        public string failureFocus;
        public int intensity;
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
        public TDCampaignScenarioPlan scenario;
        public TDCampaignContractDefinition contract;
        public TDCampaignMutatorDefinition[] mutators;
    }

    [Serializable]
    public sealed class TDCampaignContractDefinition
    {
        public string contractId;
        public string displayName;
        public string metric;
        public string comparison;
        public int target;
    }

    [Serializable]
    public sealed class TDCampaignMutatorDefinition
    {
        public string mutatorId;
        public string displayName;
        public float enemyHpMultiplier;
        public float enemySpeedMultiplier;
        public int enemyArmorBonus;
        public int startingBudgetDelta;
        public int startingIntegrityDelta;
        public float rewardMultiplier;
        public float resonanceGainMultiplier;
        public float scenarioCostMultiplier;
    }

    [Serializable]
    public sealed class TDCampaignGlobalRules
    {
        public int maxFailureReasonsShown;
        public int resonanceEnabledFromLevel;
        public int startingBudgetPerLevel;
        public int startingIntegrityPerChapter;
        public float towerPowerPerLevelPct;
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
