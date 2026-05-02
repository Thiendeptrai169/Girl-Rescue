using System.Collections.Generic;
using UnityEngine;
using DragonRescue.Core;

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
        [Tooltip("Viewport coords for the dragon's initial spawn. Values may extend slightly outside 0..1 for hidden offscreen entry.")]
        public Vector2 dragonSpawnViewport = new Vector2(1.2f, 0.88f);
        [Tooltip("Viewport coords. X=0 is left, Y=0 is bottom. Dragon values may extend slightly outside 0..1 for offscreen entry/exit.")]
        public Vector2 dragonStartViewport = new Vector2(0.88f, 0.68f);
        [Tooltip("Viewport coords. X=0 is left, Y=0 is bottom. Dragon values may extend slightly outside 0..1 for offscreen entry/exit.")]
        public Vector2 dragonEndViewport = new Vector2(0.22f, 0.68f);
        [Tooltip("Viewport coords. Used when movement type is Waypoint. Values may extend slightly outside 0..1 for offscreen entry/exit.")]
        public Vector2[] dragonPathWaypointsViewport;
        public float dragonMoveSpeed = 0.05f;
        public float loseDistance = 0.5f;
        public List<DragonSegmentData> dragonSegments = new();

        [Header("Dragon Recoil")]
        [Tooltip("How far the dragon is pushed back when a segment is destroyed. This is normalized path progress, so 0.025 means 2.5% of the path.")]
        [Range(0f, 0.2f)]
        public float dragonRecoilProgress = 0.025f;
        [Tooltip("Short pause after recoil so the hit feels like it interrupts the dragon.")]
        [Range(0f, 0.5f)]
        public float dragonRecoilPauseSeconds = 0.08f;

        [Header("Slots")]
        public int totalSlotCount = 6;
        [Range(1, 6)]
        public int unlockedSlotCount = 3;

        [Header("Board")]
        [Tooltip("Viewport coords for board center. X=0 is left, Y=0 is bottom.")]
        public Vector2 boardViewport = new(0.5f, 0.28f);
        [Range(0.1f, 1f)]
        public float boardWidthRatio = 0.88f;
        [Range(0.1f, 1f)]
        public float boardHeightRatio = 0.5f;
        public Vector2Int boardSize = new(4, 4);
        public List<NormalArrowBlockData> blocks = new();

        [Header("Board Viewport Limits")]
        [Tooltip("Viewport Y of the slot bar. Board top must stay below this line.")]
        public float slotBarViewportY = 0.55f;
        [Tooltip("Extra viewport gap below the slot bar.")]
        public float slotBarBottomPaddingViewport = 0.03f;
        [Tooltip("Bottom UI booster bar height in Canvas reference pixels.")]
        public float boosterBarHeightPixels = 300f;
        [Tooltip("Canvas Scaler reference height used to convert booster pixels into viewport height.")]
        public float uiReferenceHeightPixels = 1920f;
        [Tooltip("Extra viewport gap above the booster bar.")]
        public float boosterBarTopPaddingViewport = 0.02f;

        [Header("Cannon Defaults")]
        public float defaultFireRate = 1f;
        public int defaultDamage = 1;
        public float defaultProjectileSpeed = 8f;
        public float defaultFireRange = 5f;

        [Header("Boosters")]
        public List<BoosterData> boosters = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Dragon viewport values can extend slightly outside the camera for offscreen entry.
            princessViewport.x = Mathf.Clamp01(princessViewport.x);
            princessViewport.y = Mathf.Clamp01(princessViewport.y);

            dragonSpawnViewport.x = Mathf.Clamp(dragonSpawnViewport.x, -0.25f, 1.25f);
            dragonSpawnViewport.y = Mathf.Clamp(dragonSpawnViewport.y, -0.25f, 1.25f);

            dragonStartViewport.x = Mathf.Clamp(dragonStartViewport.x, -0.25f, 1.25f);
            dragonStartViewport.y = Mathf.Clamp(dragonStartViewport.y, -0.25f, 1.25f);

            dragonEndViewport.x = Mathf.Clamp(dragonEndViewport.x, -0.25f, 1.25f);
            dragonEndViewport.y = Mathf.Clamp(dragonEndViewport.y, -0.25f, 1.25f);
            dragonRecoilProgress = Mathf.Max(0f, dragonRecoilProgress);
            dragonRecoilPauseSeconds = Mathf.Max(0f, dragonRecoilPauseSeconds);
            boardViewport.x = Mathf.Clamp01(boardViewport.x);
            boardViewport.y = Mathf.Clamp01(boardViewport.y);
            boardSize.x = Mathf.Max(1, boardSize.x);
            boardSize.y = Mathf.Max(1, boardSize.y);
            ValidateSlotsAndBoosters();
            ValidateBoardViewportBand();

            if (dragonPathWaypointsViewport != null)
            {
                for (int i = 0; i < dragonPathWaypointsViewport.Length; i++)
                {
                    dragonPathWaypointsViewport[i].x = Mathf.Clamp(dragonPathWaypointsViewport[i].x, -0.25f, 1.25f);
                    dragonPathWaypointsViewport[i].y = Mathf.Clamp(dragonPathWaypointsViewport[i].y, -0.25f, 1.25f);
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

            RemoveBlocksWithMissingDragonColors();
            ValidateBlocks();
            ValidateBlockAmmoAgainstDragon();
            ValidateBoardHasReleaseSolution();
        }

        private void ValidateSlotsAndBoosters()
        {
            totalSlotCount = Mathf.Max(1, totalSlotCount);
            unlockedSlotCount = Mathf.Clamp(unlockedSlotCount, 0, totalSlotCount);

            if (boosters == null)
                return;

            int maxUnlockCharges = Mathf.Max(0, totalSlotCount - unlockedSlotCount);
            for (int i = 0; i < boosters.Count; i++)
            {
                BoosterData booster = boosters[i];
                if (booster == null)
                    continue;

                booster.charges = Mathf.Max(0, booster.charges);

                if (booster.type != BoosterType.Unlock || booster.charges <= maxUnlockCharges)
                    continue;

                DebugSystem.Warning(
                    DebugCategory.Data,
                    $"Clamped Unlock booster charges from {booster.charges} to {maxUnlockCharges}. Unlock charges cannot exceed locked cannon slots.",
                    this);
                booster.charges = maxUnlockCharges;
            }
        }

        private Dictionary<CannonColor, int> BuildDragonColorCounts()
        {
            Dictionary<CannonColor, int> counts = new();

            if (dragonSegments == null) return counts;

            for (int i = 0; i < dragonSegments.Count; i++)
            {
                var segment = dragonSegments[i];
                if (segment == null) continue;

                if (!counts.ContainsKey(segment.color))
                    counts[segment.color] = 0;

                counts[segment.color] += Mathf.Max(1, segment.count);
            }

            return counts;
        }

        private void ValidateBoardViewportBand()
        {
            slotBarViewportY = Mathf.Clamp01(slotBarViewportY);
            slotBarBottomPaddingViewport = Mathf.Max(0f, slotBarBottomPaddingViewport);
            boosterBarHeightPixels = Mathf.Max(0f, boosterBarHeightPixels);
            uiReferenceHeightPixels = Mathf.Max(1f, uiReferenceHeightPixels);
            boosterBarTopPaddingViewport = Mathf.Max(0f, boosterBarTopPaddingViewport);

            float minBoardBottomY = Mathf.Clamp01((boosterBarHeightPixels / uiReferenceHeightPixels) + boosterBarTopPaddingViewport);
            float maxBoardTopY = Mathf.Clamp01(slotBarViewportY - slotBarBottomPaddingViewport);
            float allowedHeight = maxBoardTopY - minBoardBottomY;

            if (allowedHeight <= 0f)
            {
                DebugSystem.Warning(DebugCategory.Data, "Board viewport limits are invalid: booster safe zone reaches or passes the slot bar. Reduce boosterBarHeightPixels/padding or raise slotBarViewportY.", this);
                return;
            }

            if (boardHeightRatio > allowedHeight)
            {
                DebugSystem.Warning(DebugCategory.Data, $"Clamped boardHeightRatio from {boardHeightRatio:0.###} to {allowedHeight:0.###} so board fits between slot bar and booster bar.", this);
                boardHeightRatio = allowedHeight;
            }

            float halfBoardHeight = boardHeightRatio * 0.5f;
            float minCenterY = minBoardBottomY + halfBoardHeight;
            float maxCenterY = maxBoardTopY - halfBoardHeight;
            float oldBoardY = boardViewport.y;

            boardViewport.y = Mathf.Clamp(boardViewport.y, minCenterY, maxCenterY);

            if (!Mathf.Approximately(oldBoardY, boardViewport.y))
            {
                DebugSystem.Warning(DebugCategory.Data, $"Clamped boardViewport.y from {oldBoardY:0.###} to {boardViewport.y:0.###} so board stays below slot bar and above booster bar.", this);
            }
        }

        private void RemoveBlocksWithMissingDragonColors()
        {
            if (blocks == null || blocks.Count == 0) return;

            Dictionary<CannonColor, int> dragonColorCounts = BuildDragonColorCounts();

            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                var block = blocks[i];
                if (block == null) continue;

                if (!dragonColorCounts.ContainsKey(block.color))
                {
                    DebugSystem.Warning(DebugCategory.Data, $"Removed block '{GetBlockLabel(block, i)}' because dragon has no {block.color} segment.", this);
                    blocks.RemoveAt(i);
                }
            }
        }

        private void ValidateBlockAmmoAgainstDragon()
        {
            if (blocks == null || blocks.Count == 0) return;

            Dictionary<CannonColor, int> dragonColorCounts = BuildDragonColorCounts();
            Dictionary<CannonColor, int> usedAmmoByColor = new();

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block == null) continue;

                block.ammo = Mathf.Max(0, block.ammo);

                if (!dragonColorCounts.TryGetValue(block.color, out int dragonCount))
                    continue;

                if (!usedAmmoByColor.ContainsKey(block.color))
                    usedAmmoByColor[block.color] = 0;

                int remainingAllowedAmmo = dragonCount - usedAmmoByColor[block.color];

                if (remainingAllowedAmmo <= 0)
                {
                    if (block.ammo > 0)
                    {
                        DebugSystem.Warning(DebugCategory.Data, $"Clamped '{GetBlockLabel(block, i)}' ammo to 0 because {block.color} ammo already matches dragon count {dragonCount}.", this);
                    }

                    block.ammo = 0;
                    continue;
                }

                if (block.ammo > remainingAllowedAmmo)
                {
                    DebugSystem.Warning(DebugCategory.Data, $"Clamped '{GetBlockLabel(block, i)}' ammo from {block.ammo} to {remainingAllowedAmmo}. Total {block.color} ammo cannot exceed dragon count {dragonCount}.", this);
                    block.ammo = remainingAllowedAmmo;
                }

                usedAmmoByColor[block.color] += block.ammo;
            }

            foreach (var pair in dragonColorCounts)
            {
                usedAmmoByColor.TryGetValue(pair.Key, out int availableAmmo);
                if (availableAmmo < pair.Value)
                {
                    DebugSystem.Warning(DebugCategory.Data, $"Level may be unwinnable: {pair.Key} dragon count is {pair.Value}, but board only has {availableAmmo} matching ammo.", this);
                }
            }
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
                        DebugSystem.Warning(DebugCategory.Data, $"Cannot fit block {i} on the {boardSize.x}x{boardSize.y} board! It overlaps.", this);
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

        private void ValidateBoardHasReleaseSolution()
        {
            if (blocks == null || blocks.Count == 0) return;

            int[,] grid = BuildBlockIndexGrid(out bool hasInvalidFootprint);
            if (hasInvalidFootprint) return;

            bool[] activeBlocks = new bool[blocks.Count];
            int targetReleaseCount = 0;
            for (int i = 0; i < activeBlocks.Length; i++)
            {
                activeBlocks[i] = blocks[i] != null;
                if (activeBlocks[i])
                    targetReleaseCount++;
            }

            int releasedCount = 0;
            bool madeProgress = true;

            while (madeProgress)
            {
                madeProgress = false;

                for (int i = 0; i < blocks.Count; i++)
                {
                    if (!activeBlocks[i]) continue;

                    if (CanBlockEscapeInSimulation(i, grid))
                    {
                        ClearBlockFromSimulation(i, grid);
                        activeBlocks[i] = false;
                        releasedCount++;
                        madeProgress = true;
                    }
                }
            }

            if (releasedCount == targetReleaseCount) return;

            string stuckBlocks = "";
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!activeBlocks[i]) continue;
                if (stuckBlocks.Length > 0) stuckBlocks += ", ";
                stuckBlocks += GetBlockLabel(blocks[i], i);
            }

            DebugSystem.Warning(DebugCategory.Data, $"Board has no full release solution. Stuck blocks: {stuckBlocks}. Move or rotate blocks until at least one valid release order exists.", this);
        }

        private int[,] BuildBlockIndexGrid(out bool hasInvalidFootprint)
        {
            int[,] grid = new int[boardSize.x, boardSize.y];
            hasInvalidFootprint = false;

            for (int x = 0; x < boardSize.x; x++)
            {
                for (int y = 0; y < boardSize.y; y++)
                {
                    grid[x, y] = -1;
                }
            }

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block == null) continue;

                if (!IsBlockWithinBounds(block.position, block.size))
                {
                    hasInvalidFootprint = true;
                    DebugSystem.Warning(DebugCategory.Data, $"Cannot solve-check '{GetBlockLabel(block, i)}' because its footprint is outside the board.", this);
                    continue;
                }

                for (int x = 0; x < block.size.x; x++)
                {
                    for (int y = 0; y < block.size.y; y++)
                    {
                        Vector2Int cell = new(block.position.x + x, block.position.y + y);

                        if (grid[cell.x, cell.y] != -1 && grid[cell.x, cell.y] != i)
                        {
                            hasInvalidFootprint = true;
                            DebugSystem.Warning(DebugCategory.Data, $"Cannot solve-check because '{GetBlockLabel(block, i)}' overlaps another block at {cell}.", this);
                        }

                        grid[cell.x, cell.y] = i;
                    }
                }
            }

            return grid;
        }

        private bool CanBlockEscapeInSimulation(int blockIndex, int[,] grid)
        {
            var block = blocks[blockIndex];
            Vector2Int currentPos = block.position;
            int guard = Mathf.Max(1, boardSize.x + boardSize.y + block.size.x + block.size.y + 4);

            for (int step = 0; step < guard; step++)
            {
                if (IsDiagonalDirection(block.direction) &&
                    !IsDiagonalPointingOutFromEdge(block, currentPos))
                {
                    GetDiagonalComponents(block.direction, out Direction horizontal, out Direction vertical);

                    Vector2Int horizontalPos = GetNextBoardPosition(currentPos, horizontal);
                    if (IsFootprintBlockedInSimulation(blockIndex, horizontalPos, grid))
                        return false;

                    Vector2Int verticalPos = GetNextBoardPosition(currentPos, vertical);
                    if (IsFootprintBlockedInSimulation(blockIndex, verticalPos, grid))
                        return false;
                }

                currentPos = GetNextBoardPosition(currentPos, block.direction);

                if (IsFootprintFullyOutside(block, currentPos))
                    return true;

                if (IsFootprintBlockedInSimulation(blockIndex, currentPos, grid))
                    return false;
            }

            return false;
        }

        private void ClearBlockFromSimulation(int blockIndex, int[,] grid)
        {
            var block = blocks[blockIndex];

            for (int x = 0; x < block.size.x; x++)
            {
                for (int y = 0; y < block.size.y; y++)
                {
                    Vector2Int cell = new(block.position.x + x, block.position.y + y);
                    if (IsCellWithinBounds(cell) && grid[cell.x, cell.y] == blockIndex)
                    {
                        grid[cell.x, cell.y] = -1;
                    }
                }
            }
        }

        private Vector2Int GetNextBoardPosition(Vector2Int pos, Direction direction)
        {
            return direction switch
            {
                Direction.Up => new Vector2Int(pos.x, pos.y - 1),
                Direction.Down => new Vector2Int(pos.x, pos.y + 1),
                Direction.Left => new Vector2Int(pos.x - 1, pos.y),
                Direction.Right => new Vector2Int(pos.x + 1, pos.y),
                Direction.UpLeft => new Vector2Int(pos.x - 1, pos.y - 1),
                Direction.UpRight => new Vector2Int(pos.x + 1, pos.y - 1),
                Direction.DownLeft => new Vector2Int(pos.x - 1, pos.y + 1),
                Direction.DownRight => new Vector2Int(pos.x + 1, pos.y + 1),
                _ => pos
            };
        }

        private bool IsFootprintBlockedInSimulation(int blockIndex, Vector2Int candidatePos, int[,] grid)
        {
            var block = blocks[blockIndex];

            for (int x = 0; x < block.size.x; x++)
            {
                for (int y = 0; y < block.size.y; y++)
                {
                    Vector2Int checkCell = new(candidatePos.x + x, candidatePos.y + y);
                    if (!IsCellWithinBounds(checkCell))
                        continue;

                    int occupant = grid[checkCell.x, checkCell.y];
                    if (occupant != -1 && occupant != blockIndex)
                        return true;
                }
            }

            return false;
        }

        private bool IsFootprintFullyOutside(NormalArrowBlockData block, Vector2Int candidatePos)
        {
            for (int x = 0; x < block.size.x; x++)
            {
                for (int y = 0; y < block.size.y; y++)
                {
                    Vector2Int cell = new(candidatePos.x + x, candidatePos.y + y);
                    if (IsCellWithinBounds(cell))
                        return false;
                }
            }

            return true;
        }

        private bool IsDiagonalPointingOutFromEdge(NormalArrowBlockData block, Vector2Int candidatePos)
        {
            bool touchesTop = candidatePos.y <= 0;
            bool touchesBottom = candidatePos.y + block.size.y >= boardSize.y;
            bool touchesLeft = candidatePos.x <= 0;
            bool touchesRight = candidatePos.x + block.size.x >= boardSize.x;

            return block.direction switch
            {
                Direction.UpLeft => touchesTop || touchesLeft,
                Direction.UpRight => touchesTop || touchesRight,
                Direction.DownLeft => touchesBottom || touchesLeft,
                Direction.DownRight => touchesBottom || touchesRight,
                _ => false
            };
        }

        private bool IsDiagonalDirection(Direction direction)
        {
            return direction == Direction.UpLeft ||
                   direction == Direction.UpRight ||
                   direction == Direction.DownLeft ||
                   direction == Direction.DownRight;
        }

        private void GetDiagonalComponents(Direction direction, out Direction horizontal, out Direction vertical)
        {
            switch (direction)
            {
                case Direction.UpLeft:
                    horizontal = Direction.Left;
                    vertical = Direction.Up;
                    break;
                case Direction.UpRight:
                    horizontal = Direction.Right;
                    vertical = Direction.Up;
                    break;
                case Direction.DownLeft:
                    horizontal = Direction.Left;
                    vertical = Direction.Down;
                    break;
                case Direction.DownRight:
                    horizontal = Direction.Right;
                    vertical = Direction.Down;
                    break;
                default:
                    horizontal = Direction.Left;
                    vertical = Direction.Up;
                    break;
            }
        }

        private bool IsCellWithinBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < boardSize.x && pos.y >= 0 && pos.y < boardSize.y;
        }

        private string GetBlockLabel(NormalArrowBlockData block, int index)
        {
            if (block != null && !string.IsNullOrWhiteSpace(block.id))
                return block.id;

            return $"Block {index}";
        }

        private bool IsBlockWithinBounds(Vector2Int pos, Vector2Int size)
        {
            return pos.x >= 0 && pos.x + size.x <= boardSize.x &&
                   pos.y >= 0 && pos.y + size.y <= boardSize.y;
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
