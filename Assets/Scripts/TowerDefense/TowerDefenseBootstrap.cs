using UnityEngine;

namespace TD
{
    public static class TowerDefenseBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<TDGameManager>() != null)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
            var smokeRequested = TDStandaloneSmokeProbe.IsRequested();
            var p1254Requested = TDP1254StandaloneProbe.IsRequested();
            if (smokeRequested || p1254Requested)
            {
                TDStandaloneSmokeProbe.PrepareCleanProfile();
            }
#endif

            var root = new GameObject("TD_Runtime");
            root.AddComponent<TDReleaseTelemetry>();
            root.AddComponent<TDReleaseDiagnostics>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
            if (smokeRequested)
            {
                root.AddComponent<TDStandaloneSmokeProbe>();
            }
            else if (p1254Requested)
            {
                root.AddComponent<TDP1254StandaloneProbe>();
            }
#endif
            root.AddComponent<TDGameManager>();
        }
    }
}
