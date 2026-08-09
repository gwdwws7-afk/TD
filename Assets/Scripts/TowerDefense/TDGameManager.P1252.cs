using UnityEngine;

namespace TD
{
    public sealed partial class TDGameManager
    {
        public string DebugApplyP1252TechnicalSmokeAssist(int requestedIntegrity)
        {
            if (!_campaignDeploymentConfirmed || _gameOver)
            {
                return "skip: P12.5.2 technical assist requires an active deployed mission";
            }

            var integrity = Mathf.Clamp(
                requestedIntegrity,
                Mathf.Max(1, _startingLineIntegrity),
                5000);
            _startingLineIntegrity = integrity;
            _lineIntegrity = integrity;
            return $"p12.5.2.technical_assist.applied=True integrity={integrity}";
        }
    }
}
