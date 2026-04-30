using System.Collections.Generic;
using UnityEngine;

namespace DragonRescue.Data
{
    /// <summary>
    /// Single source of truth for a level's configuration.
    /// Create one asset per level via: Assets > Create > Dragon Rescue > Level Config
    /// </summary>
    [CreateAssetMenu(fileName = "Level_New", menuName = "Dragon Rescue/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        [Header("Basic")]
        public string levelId;
        public int levelNumber;

        [Header("Princess")]
        [Tooltip("Viewport coords (0 to 1). X=0 is left, Y=0 is bottom.")]
        public Vector2 princessViewport = new Vector2(0.18f, 0.66f);
        public int princessHearts = 3;

        [Header("Dragon")]
        public DragonMovementType dragonMovementType = DragonMovementType.Linear;
        [Tooltip("Viewport coords (0 to 1). X=0 is left, Y=0 is bottom.")]
        public Vector2 dragonStartViewport = new Vector2(0.88f, 0.68f);
        [Tooltip("Viewport coords (0 to 1). X=0 is left, Y=0 is bottom.")]
        public Vector2 dragonEndViewport = new Vector2(0.22f, 0.68f);
        [Tooltip("Viewport coords (0 to 1). Used when movement type is Waypoint.")]
        public Vector2[] dragonPathWaypointsViewport;
        public float dragonMoveSpeed = 0.05f;
        public float loseDistance = 0.5f;
        public List<DragonSegmentData> dragonSegments = new();

        [Header("Slots")]
        public int totalSlotCount = 6;
        [Range(1, 6)]
        public int unlockedSlotCount = 3;

        [Header("Board")]
        public Vector2Int boardSize = new(4, 4);
        public List<NormalArrowBlockData> blocks = new();

        [Header("Cannon Defaults")]
        public float defaultFireRate = 1f;
        public int defaultDamage = 1;
        public float defaultProjectileSpeed = 8f;

        [Header("Boosters")]
        public List<BoosterData> boosters = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Restrict viewport values to be between 0 (bottom/left) and 1 (top/right)
            princessViewport.x = Mathf.Clamp01(princessViewport.x);
            princessViewport.y = Mathf.Clamp01(princessViewport.y);

            dragonStartViewport.x = Mathf.Clamp01(dragonStartViewport.x);
            dragonStartViewport.y = Mathf.Clamp01(dragonStartViewport.y);

            dragonEndViewport.x = Mathf.Clamp01(dragonEndViewport.x);
            dragonEndViewport.y = Mathf.Clamp01(dragonEndViewport.y);

            if (dragonPathWaypointsViewport != null)
            {
                for (int i = 0; i < dragonPathWaypointsViewport.Length; i++)
                {
                    dragonPathWaypointsViewport[i].x = Mathf.Clamp01(dragonPathWaypointsViewport[i].x);
                    dragonPathWaypointsViewport[i].y = Mathf.Clamp01(dragonPathWaypointsViewport[i].y);
                }
            }
        }
#endif
    }
}
