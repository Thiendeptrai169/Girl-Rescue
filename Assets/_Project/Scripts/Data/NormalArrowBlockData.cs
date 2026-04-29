using System;
using UnityEngine;

namespace DragonRescue.Data
{
    /// <summary>
    /// Data for a single NormalArrowBlock on the puzzle board.
    /// Embedded as a list inside LevelConfig.
    /// </summary>
    [Serializable]
    public class NormalArrowBlockData
    {
        public string id;

        public Vector2Int position;
        public Vector2Int size = Vector2Int.one;

        public CannonColor color;
        public Direction direction;

        public int ammo = 6;
    }
}
