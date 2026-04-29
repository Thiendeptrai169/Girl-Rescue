using UnityEngine;
using DragonRescue.Core;
using DragonRescue.Data;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// Zuma-style movement strategy.
    /// The root (head) follows a series of waypoints.
    /// The child segments calculate their position backwards along the path based on their index.
    /// </summary>
    public class WaypointDragonMovement : DragonMovementBase
    {
        private Vector2[] _waypoints;
        private float _speed;
        private DragonSegmentIdentity[] _segments;
        private float _spacing;
        private bool _isMoving;
        
        private float[] _distances;
        private float _totalLength;

        public override void Init(LevelConfig config, DragonSegmentIdentity[] segments, float spacing)
        {
            _waypoints = config.dragonPathWaypoints;
            _speed = config.dragonMoveSpeed;
            _segments = segments;
            _spacing = spacing;
            _isMoving = true;
            Progress = 0f;

            if (_waypoints == null || _waypoints.Length < 2)
            {
                Debug.LogError("[WaypointDragonMovement] Not enough waypoints set in LevelConfig!");
                _isMoving = false;
                return;
            }

            // Precalculate distances between waypoints to normalize progress
            _distances = new float[_waypoints.Length];
            _totalLength = 0f;
            for (int i = 0; i < _waypoints.Length - 1; i++)
            {
                float dist = Vector2.Distance(_waypoints[i], _waypoints[i + 1]);
                _totalLength += dist;
                _distances[i + 1] = _totalLength;
            }

            UpdatePositions();
        }

        public override void StopMoving()   => _isMoving = false;
        public override void ResumeMoving() => _isMoving = true;

        private void Update()
        {
            if (!_isMoving || _totalLength <= 0f) return;

            // Use speed as a percentage (0.05 = 5% of path per second) to match Linear movement
            Progress += _speed * Time.deltaTime;
            
            UpdatePositions();

            if (Progress >= 1f)
            {
                Progress = 1f;
                _isMoving = false;
                Debug.Log("[WaypointDragonMovement] Dragon reached the end of the path!");
                GameEvents.FireLevelLose();
            }
        }

        private void UpdatePositions()
        {
            // Move Head (Root)
            float headDist = Progress * _totalLength;
            transform.position = EvaluatePath(headDist);

            // Move each segment along the path
            for (int i = 0; i < _segments.Length; i++)
            {
                // Distance along the curve = HeadDistance - (Offset based on segment index)
                float segDist = headDist - ((i + 1) * _spacing);
                _segments[i].transform.position = EvaluatePath(segDist);
            }
        }

        private Vector3 EvaluatePath(float distance)
        {
            if (distance <= 0f) return _waypoints[0];
            if (distance >= _totalLength) return _waypoints[_waypoints.Length - 1];
            
            for (int i = 0; i < _waypoints.Length - 1; i++)
            {
                if (distance >= _distances[i] && distance <= _distances[i + 1])
                {
                    float segmentLength = _distances[i + 1] - _distances[i];
                    float t = (distance - _distances[i]) / segmentLength;
                    return Vector3.Lerp(_waypoints[i], _waypoints[i + 1], t);
                }
            }
            return _waypoints[_waypoints.Length - 1];
        }
    }
}
