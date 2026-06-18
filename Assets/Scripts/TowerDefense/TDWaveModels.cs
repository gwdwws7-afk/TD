using System;

namespace TD
{
    [Serializable]
    public sealed class TDWaveSet
    {
        public string schemaVersion;
        public string waveSetId;
        public string mapId;
        public string displayName;
        public TDGlobalDefaults globalDefaults;
        public TDEnemyCatalogEntry[] enemyCatalog;
        public TDWaveDefinition[] waves;
    }

    [Serializable]
    public sealed class TDGlobalDefaults
    {
        public float prepSeconds;
        public int baseRewardGold;
        public float spawnMinSpacing;
        public int lineDamageDefault;
        public int maxConcurrentEnemiesHint;
    }

    [Serializable]
    public sealed class TDEnemyCatalogEntry
    {
        public string enemyId;
        public string displayName;
        public int hp;
        public float speed;
        public int armorFlat;
        public int rewardGold;
        public int lineDamage;
        public float threatCost;
        public string[] tags;
    }

    [Serializable]
    public sealed class TDWaveDefinition
    {
        public int waveIndex;
        public string phase;
        public string goalTag;
        public string[] threatTags;
        public float prepSeconds;
        public int rewardGold;
        public float budgetTarget;
        public float budgetTolerance;
        public string hint;
        public TDWaveGroup[] groups;
    }

    [Serializable]
    public sealed class TDWaveGroup
    {
        public string enemyId;
        public int count;
        public float startDelay;
        public float spawnInterval;
        public string formation;
        public string lane;
    }
}
