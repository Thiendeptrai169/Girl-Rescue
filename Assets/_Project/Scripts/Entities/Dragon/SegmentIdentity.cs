using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// Core data holder for a spawned dragon segment.
    /// Follows the EC pattern.
    /// </summary>
    public class SegmentIdentity : MonoBehaviour
    {
        public CannonColor Color { get; private set; }
        public int MaxHp { get; private set; }

        /// <summary>
        /// Called when spawned by the LevelManager.
        /// </summary>
        public void Init(DragonSegmentDefinition definition)
        {
            Color = definition.Color;
            MaxHp = definition.Hp;
        }

        /// <summary>
        /// Called when returned to the ObjectPool.
        /// </summary>
        public void ResetData()
        {
            Color = CannonColor.Red;
            MaxHp = 0;
        }
    }
}
