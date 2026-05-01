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

        public override void Init(LevelConfig config, WorldLayout worldLayout, DragonSegmentIdentity[] segments, float spacing)
        {
            var vps = config.dragonPathWaypointsViewport;
            _speed = config.dragonMoveSpeed;
            _segments = segments;
            _spacing = spacing;
            _isMoving = true;
            Progress = 0f;
            ConfigureRecoil(config);

            if (vps == null || vps.Length < 2)
            {
                Debug.LogError("[WaypointDragonMovement] Not enough waypoints set in LevelConfig!");
                _isMoving = false;
                return;
            }

            // Convert viewports to world points
            _waypoints = new Vector2[vps.Length];
            for (int i = 0; i < vps.Length; i++)
            {
                _waypoints[i] = worldLayout.ViewportToWorld(vps[i]);
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

            if (IsRecoilPausing())
            {
                UpdatePositions();
                return;
            }

            // Use speed as a percentage (0.05 = 5% of path per second) to match Linear movement
            Progress += _speed * Time.deltaTime;
            
            UpdatePositions();

            if (HasAliveSegmentReachedEnd())
            {
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

            // Move alive segments as a compact visible body. Dead segments no longer keep empty spacing.
            int aliveIndex = 0;
            for (int i = 0; i < _segments.Length; i++)
            {
                if (!_segments[i].IsAlive) continue;

                // Distance along the curve = HeadDistance - (Offset based on segment index)
                float segDist = headDist - ((aliveIndex + 1) * _spacing);
                _segments[i].transform.position = EvaluatePath(segDist);
                aliveIndex++;
            }
        }

        private bool HasAliveSegmentReachedEnd()
        {
            float headDist = Progress * _totalLength;
            return GetAliveSegmentCount() > 0 && headDist - _spacing >= _totalLength;
        }

        private int GetAliveSegmentCount()
        {
            int aliveCount = 0;
            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i].IsAlive)
                    aliveCount++;
            }
            return aliveCount;
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
