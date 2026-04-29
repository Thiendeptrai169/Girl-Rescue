using System;

namespace DragonRescue.Data
{
    /// <summary>
    /// Configuration data for a single booster in a level.
    /// Embedded as a list inside LevelConfig.
    /// </summary>
    [Serializable]
    public class BoosterData
    {
        public BoosterType type;
        public int charges = 1;
        public bool enabled = true;

        public int amount;
        public float duration;
        public float multiplier;
    }
}
