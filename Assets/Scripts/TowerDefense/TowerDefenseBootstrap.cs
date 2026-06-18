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

            var root = new GameObject("TD_Runtime");
            root.AddComponent<TDGameManager>();
        }
    }
}
