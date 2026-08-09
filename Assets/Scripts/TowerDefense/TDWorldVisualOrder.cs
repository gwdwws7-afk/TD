using UnityEngine;

namespace TD
{
    public static class TDWorldVisualOrder
    {
        public const int BuildSpot = 5;
        public const int RangePreview = 8;
        public const int BuildPreview = 9;
        public const int GroundInteraction = 10;
        public const int ProjectileBack = 20;
        public const int Projectile = 21;
        public const int ProjectileFx = 22;
        public const int PresentationFx = 23;
        public const int EnemyTrait = 26;
        public const int EnemyStatus = 27;
        public const int EnemyThreat = 28;
        public const int EnemyCritical = 29;

        public static int ResolveBodyOrder(float worldY)
        {
            return Mathf.Clamp(15 - Mathf.RoundToInt(worldY * 1.25f), 11, 19);
        }
    }
}
