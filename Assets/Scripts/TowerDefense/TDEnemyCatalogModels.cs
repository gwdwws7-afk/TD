using System;

namespace TD
{
    [Serializable]
    public sealed class TDEnemyCatalogSet
    {
        public string schemaVersion;
        public string catalogId;
        public TDEnemyCatalogEntry[] enemies;
    }
}
