using UnityEngine;

namespace DragonRescue.Entities.Princess
{
    /// <summary>
    /// Marker component to identify the Princess entity in the scene.
    /// Also provides a clean reference to her position for distance calculations.
    /// </summary>
    public class PrincessIdentity : MonoBehaviour
    {
        public Vector3 Position => transform.position;
    }
}
