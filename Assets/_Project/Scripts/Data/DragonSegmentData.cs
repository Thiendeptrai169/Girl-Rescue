using System;
using UnityEngine;

namespace DragonRescue.Data
{
    /// <summary>
    /// Data for a single dragon segment.
    /// Embedded as a list inside LevelConfig — not a standalone asset,
    /// because segments are level-specific and not reused across levels.
    /// </summary>
    [Serializable]
    public class DragonSegmentData
    {
        public CannonColor color;
        [Tooltip("How many dragon blocks of this color to spawn")]
        public int count = 1;
    }
}
