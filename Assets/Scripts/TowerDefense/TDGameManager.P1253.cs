#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
using System;
using System.Linq;
using UnityEngine;

namespace TD
{
    [Serializable]
    public sealed class TDP1253RuntimeState
    {
        public int levelIndex;
        public string levelId;
        public string mapId;
        public int currentWave;
        public int waveCount;
        public int activeEnemies;
        public int activeTowers;
        public int activeProjectiles;
        public int integrity;
        public int budget;
        public bool deployed;
        public bool gameOver;
        public bool victory;
    }

    public sealed partial class TDGameManager
    {
        public TDP1253RuntimeState DebugGetP1253RuntimeState()
        {
            return new TDP1253RuntimeState
            {
                levelIndex = _campaignRoute?.level?.levelIndex ?? 0,
                levelId = _campaignRoute?.level?.levelId ?? string.Empty,
                mapId = _campaignRoute?.map?.mapId ?? string.Empty,
                currentWave = Mathf.Max(0, _wave),
                waveCount = GetConfiguredWaveCount(),
                activeEnemies = _activeEnemies.Count(enemy => enemy != null),
                activeTowers = FindObjectsByType<TDTower>(FindObjectsSortMode.None).Length,
                activeProjectiles = FindObjectsByType<TDProjectile>(FindObjectsSortMode.None).Length,
                integrity = _lineIntegrity,
                budget = _defenseBudget,
                deployed = _campaignDeploymentConfirmed,
                gameOver = _gameOver,
                victory = _victory
            };
        }
    }
}
#endif
