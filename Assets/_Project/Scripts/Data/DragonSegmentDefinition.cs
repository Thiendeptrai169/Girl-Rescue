using System;

namespace DragonRescue.Data
{
    /// <summary>
    /// Data for a single dragon segment.
    /// Embedded as an array inside LevelDefinition — not a standalone asset,
    /// because segments are level-specific and not reused across levels.
    /// </summary>
    [Serializable]
    public struct DragonSegmentDefinition
    {
        public CannonColor Color;
        public int Hp;
    }
}
