using UnityEngine;

namespace DragonRescue.Core
{
    /// <summary>
    /// Dynamically calculates a world-space grid based on the camera width.
    /// Adapts the cell size so the board always fits nicely on screen.
    /// </summary>
    public class BoardWorldLayout
    {
        private readonly Camera _cam;
        private readonly Vector3 _boardCenter;
        private readonly Vector2Int _boardSize;
        private readonly float _boardWidthRatio;
        private readonly float _boardHeightRatio;

        public float CellSize { get; private set; }
        public Vector3 Origin { get; private set; }

        public BoardWorldLayout(Camera cam, Vector3 boardCenter, Vector2Int boardSize, float boardWidthRatio = 0.86f, float boardHeightRatio = 0.48f)
        {
            _cam = cam;
            _boardCenter = boardCenter;
            _boardSize = boardSize;
            _boardWidthRatio = boardWidthRatio;
            _boardHeightRatio = boardHeightRatio;

            Calculate();
        }

        private void Calculate()
        {
            float worldHeight = _cam.orthographicSize * 2f;
            float worldWidth = worldHeight * _cam.aspect;

            float maxBoardWorldWidth = worldWidth * _boardWidthRatio;
            float maxBoardWorldHeight = worldHeight * _boardHeightRatio;
            CellSize = Mathf.Min(maxBoardWorldWidth / _boardSize.x, maxBoardWorldHeight / _boardSize.y);

            float boardWorldWidth = CellSize * _boardSize.x;
            float boardWorldHeight = CellSize * _boardSize.y;

            // Matrix Style: Origin is TOP-LEFT of the board
            Origin = _boardCenter + new Vector3(-boardWorldWidth / 2f, boardWorldHeight / 2f, 0f);
        }

        public Vector3 GetCellCenter(Vector2Int cell)
        {
            return GetCellCenter(cell.x, cell.y);
        }

        public Vector3 GetCellCenter(int col, int row)
        {
            // Y goes DOWN as row increases
            return Origin + new Vector3(
                (col + 0.5f) * CellSize,
                -(row + 0.5f) * CellSize,
                0f
            );
        }

        public Vector3 GetBlockCenter(Vector2Int position, Vector2Int size)
        {
            return Origin + new Vector3(
                (position.x + size.x * 0.5f) * CellSize,
                -(position.y + size.y * 0.5f) * CellSize,
                0f
            );
        }
    }
}
