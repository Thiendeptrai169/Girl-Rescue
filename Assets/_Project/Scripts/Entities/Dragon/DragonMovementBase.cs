using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// Abstract base class for Dragon movement.
    /// Allows swapping between Linear, Curved, Spline, or Jump movement 
    /// without modifying the DragonManager.
    /// </summary>
    public abstract class DragonMovementBase : MonoBehaviour
    {
        public float Progress { get; protected set; }

        public abstract void Init(LevelConfig config, WorldLayout worldLayout, DragonSegmentIdentity[] segments, float spacing);
        public abstract void StopMoving();
        public abstract void ResumeMoving();
    }
}
