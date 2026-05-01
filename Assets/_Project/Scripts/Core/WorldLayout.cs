using UnityEngine;

namespace DragonRescue.Core
{
    /// <summary>
    /// Translates viewport coordinates to world space coordinates.
    /// Values outside 0..1 are allowed for intentional offscreen entry/exit points.
    /// </summary>
    public class WorldLayout : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;
        
        public Camera MainCamera => _mainCamera;

        public float DistanceToWorldPlane()
        {
            if (_mainCamera == null) return 10f;
            return Mathf.Abs(_mainCamera.transform.position.z);
        }

        public Vector3 ViewportToWorld(Vector2 viewport)
        {
            if (_mainCamera == null) return Vector3.zero;
            
            // Viewport coords: 0,0 is bottom-left. 1,1 is top-right.
            Vector3 world = _mainCamera.ViewportToWorldPoint(
                new Vector3(viewport.x, viewport.y, DistanceToWorldPlane())
            );
            world.z = 0f;
            return world;
        }
    }
}
