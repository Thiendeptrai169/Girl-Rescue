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
        public float defaultFireRange = 10f;

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

            if (dragonSegments != null)
            {
                for (int i = 0; i < dragonSegments.Count; i++)
                {
                    if (dragonSegments[i] != null)
                        dragonSegments[i].count = Mathf.Max(1, dragonSegments[i].count);
                }
            }

            ValidateBlocks();
        }

        private void ValidateBlocks()
        {
            if (blocks == null || blocks.Count == 0) return;
            
            bool[,] occupied = new bool[boardSize.x, boardSize.y];

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block == null) continue;
                
                // Enforce minimum size
                block.size.x = Mathf.Max(1, block.size.x);
                block.size.y = Mathf.Max(1, block.size.y);

                // Enforce start position bounds
                block.position.x = Mathf.Clamp(block.position.x, 0, boardSize.x - 1);
                block.position.y = Mathf.Clamp(block.position.y, 0, boardSize.y - 1);

                // Enforce size bounds (cannot stick out of the board)
                if (block.position.x + block.size.x > boardSize.x) block.size.x = boardSize.x - block.position.x;
                if (block.position.y + block.size.y > boardSize.y) block.size.y = boardSize.y - block.position.y;

                bool overlap = false;
                for (int x = 0; x < block.size.x; x++)
                {
                    for (int y = 0; y < block.size.y; y++)
                    {
                        if (occupied[block.position.x + x, block.position.y + y])
                        {
                            overlap = true;
                            break;
                        }
                    }
                    if (overlap) break;
                }

                if (overlap)
                {
                    // Find first available spot that can fit this block
                    bool foundSpot = false;
                    for (int y = 0; y < boardSize.y; y++)
                    {
                        for (int x = 0; x < boardSize.x; x++)
                        {
                            if (CanPlace(x, y, block.size, occupied))
                            {
                                block.position = new Vector2Int(x, y);
                                foundSpot = true;
                                break;
                            }
                        }
                        if (foundSpot) break;
                    }

                    if (!foundSpot)
                    {
                        Debug.LogWarning($"[LevelConfig] Cannot fit block {i} on the {boardSize.x}x{boardSize.y} board! It overlaps.");
                    }
                }

                // Mark as occupied
                for (int x = 0; x < block.size.x; x++)
                {
                    for (int y = 0; y < block.size.y; y++)
                    {
                        if (block.position.x + x < boardSize.x && block.position.y + y < boardSize.y)
                        {
                            occupied[block.position.x + x, block.position.y + y] = true;
                        }
                    }
                }
            }
        }

        private bool CanPlace(int startX, int startY, Vector2Int size, bool[,] occupied)
        {
            if (startX + size.x > boardSize.x) return false;
            if (startY + size.y > boardSize.y) return false;

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    if (occupied[startX + x, startY + y]) return false;
                }
            }
            return true;
        }
#endif
    }
}
