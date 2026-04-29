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
        public Vector2 princessPosition;
        public int princessHearts = 3;

        [Header("Dragon")]
        public DragonMovementType dragonMovementType = DragonMovementType.Linear;
        public Vector2 dragonStartPosition;
        public Vector2 dragonEndPosition;
        [Tooltip("Used when movement type is Waypoint")]
        public Vector2[] dragonPathWaypoints;
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
    }
}
