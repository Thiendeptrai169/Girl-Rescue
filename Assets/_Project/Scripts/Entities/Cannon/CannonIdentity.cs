using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Cannon
{
    /// <summary>
    /// Core data holder for a spawned cannon entity.
    /// Follows the EC pattern — other components read data from this.
    /// </summary>
    public class CannonIdentity : MonoBehaviour
    {
        private CannonDefinition _definition;

        public CannonColor Color => _definition != null ? _definition.Color : CannonColor.Red;
        public CannonDefinition Definition => _definition;

        /// <summary>
        /// Called when the cannon is deployed into a slot.
        /// </summary>
        public void Init(CannonDefinition definition)
        {
            _definition = definition;
        }

        /// <summary>
        /// Called when returned to the ObjectPool.
        /// </summary>
        public void ResetData()
        {
            _definition = null;
        }
    }
}
