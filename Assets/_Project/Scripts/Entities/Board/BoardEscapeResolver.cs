using DragonRescue.Data;
using UnityEngine;

namespace DragonRescue.Entities.Board
{
    public readonly struct BoardEscapeResult
    {
        public readonly bool CanEscape;
        public readonly Vector2Int LastPosition;
        public readonly BlockIdentity BlockingBlock;
        public readonly Vector2Int BlockingCell;
        public readonly Direction CheckedDirection;

        public BoardEscapeResult(
            bool canEscape,
            Vector2Int lastPosition,
            BlockIdentity blockingBlock,
            Vector2Int blockingCell,
            Direction checkedDirection)
        {
            CanEscape = canEscape;
            LastPosition = lastPosition;
            BlockingBlock = blockingBlock;
            BlockingCell = blockingCell;
            CheckedDirection = checkedDirection;
        }
    }

    public static class BoardEscapeResolver
    {
        public static BoardEscapeResult Resolve(BlockIdentity block, BlockIdentity[,] grid, Vector2Int boardSize)
        {
            if (block == null || grid == null)
                return new BoardEscapeResult(false, Vector2Int.zero, null, Vector2Int.zero, Direction.Up);

            Vector2Int currentPos = block.GridPos;
            int guard = Mathf.Max(1, boardSize.x + boardSize.y + block.Size.x + block.Size.y + 4);

            for (int step = 0; step < guard; step++)
            {
                if (IsDiagonal(block.Direction) &&
                    !IsDiagonalPointingOutFromEdge(currentPos, block.Size, block.Direction, boardSize) &&
                    TryGetDiagonalComponents(block.Direction, out Direction horizontal, out Direction vertical))
                {
                    Vector2Int horizontalPos = GetNextPos(currentPos, horizontal);
                    if (TryFindBlockingOccupant(block, horizontalPos, horizontal, grid, boardSize, out BoardEscapeResult horizontalBlocked))
                        return horizontalBlocked;

                    Vector2Int verticalPos = GetNextPos(currentPos, vertical);
                    if (TryFindBlockingOccupant(block, verticalPos, vertical, grid, boardSize, out BoardEscapeResult verticalBlocked))
                        return verticalBlocked;
                }

                currentPos = GetNextPos(currentPos, block.Direction);

                if (IsFootprintFullyOutside(currentPos, block.Size, boardSize))
                    return new BoardEscapeResult(true, currentPos, null, Vector2Int.zero, block.Direction);

                if (TryFindBlockingOccupant(block, currentPos, block.Direction, grid, boardSize, out BoardEscapeResult blocked))
                    return blocked;
            }

            return new BoardEscapeResult(false, currentPos, null, Vector2Int.zero, block.Direction);
        }

        public static Vector2Int GetNextPos(Vector2Int pos, Direction direction)
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

        public static bool IsDiagonal(Direction direction)
        {
            return direction == Direction.UpLeft ||
                   direction == Direction.UpRight ||
                   direction == Direction.DownLeft ||
                   direction == Direction.DownRight;
        }

        public static bool TryGetDiagonalComponents(Direction direction, out Direction horizontal, out Direction vertical)
        {
            switch (direction)
            {
                case Direction.UpLeft:
                    horizontal = Direction.Left;
                    vertical = Direction.Up;
                    return true;
                case Direction.UpRight:
                    horizontal = Direction.Right;
                    vertical = Direction.Up;
                    return true;
                case Direction.DownLeft:
                    horizontal = Direction.Left;
                    vertical = Direction.Down;
                    return true;
                case Direction.DownRight:
                    horizontal = Direction.Right;
                    vertical = Direction.Down;
                    return true;
                default:
                    horizontal = Direction.Left;
                    vertical = Direction.Up;
                    return false;
            }
        }

        private static bool TryFindBlockingOccupant(
            BlockIdentity movingBlock,
            Vector2Int candidatePos,
            Direction checkedDirection,
            BlockIdentity[,] grid,
            Vector2Int boardSize,
            out BoardEscapeResult result)
        {
            for (int x = 0; x < movingBlock.Size.x; x++)
            {
                for (int y = 0; y < movingBlock.Size.y; y++)
                {
                    Vector2Int checkCell = new Vector2Int(candidatePos.x + x, candidatePos.y + y);
                    if (!IsCellWithinBounds(checkCell, boardSize))
                        continue;

                    BlockIdentity occupant = grid[checkCell.x, checkCell.y];
                    if (occupant != null && occupant != movingBlock)
                    {
                        result = new BoardEscapeResult(false, candidatePos, occupant, checkCell, checkedDirection);
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }

        private static bool IsFootprintFullyOutside(Vector2Int pos, Vector2Int size, Vector2Int boardSize)
        {
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Vector2Int cell = new Vector2Int(pos.x + x, pos.y + y);
                    if (IsCellWithinBounds(cell, boardSize))
                        return false;
                }
            }

            return true;
        }

        private static bool IsDiagonalPointingOutFromEdge(Vector2Int pos, Vector2Int size, Direction direction, Vector2Int boardSize)
        {
            bool touchesTop = pos.y <= 0;
            bool touchesBottom = pos.y + size.y >= boardSize.y;
            bool touchesLeft = pos.x <= 0;
            bool touchesRight = pos.x + size.x >= boardSize.x;

            return direction switch
            {
                Direction.UpLeft => touchesTop || touchesLeft,
                Direction.UpRight => touchesTop || touchesRight,
                Direction.DownLeft => touchesBottom || touchesLeft,
                Direction.DownRight => touchesBottom || touchesRight,
                _ => false
            };
        }

        private static bool IsCellWithinBounds(Vector2Int pos, Vector2Int boardSize)
        {
            return pos.x >= 0 && pos.x < boardSize.x && pos.y >= 0 && pos.y < boardSize.y;
        }
    }
}
