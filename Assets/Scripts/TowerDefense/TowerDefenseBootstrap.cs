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

            var smokeRequested = TDStandaloneSmokeProbe.IsRequested();
            var p1254Requested = TDP1254StandaloneProbe.IsRequested();
            if (smokeRequested || p1254Requested)
            {
                TDStandaloneSmokeProbe.PrepareCleanProfile();
            }

            var root = new GameObject("TD_Runtime");
            root.AddComponent<TDReleaseTelemetry>();
            root.AddComponent<TDReleaseDiagnostics>();
            if (smokeRequested)
            {
                root.AddComponent<TDStandaloneSmokeProbe>();
            }
            else if (p1254Requested)
            {
                root.AddComponent<TDP1254StandaloneProbe>();
            }

            root.AddComponent<TDGameManager>();
        }
    }
}
