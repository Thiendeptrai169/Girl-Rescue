using UnityEngine;
using DragonRescue.Core;
using DragonRescue.Data;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// Linear implementation of dragon movement.
    /// Uses basic Vector3.Lerp from start to end position.
    /// </summary>
    public class LinearDragonMovement : DragonMovementBase
    {
        // ── Runtime State ────────────────────────────────────────────────────
        private float _moveSpeed;
        private Vector3 _startPos;
        private Vector3 _endPos;
        private DragonSegmentIdentity[] _segments;
        private float _spacing;
        private bool _isMoving;

        // ── Public API ───────────────────────────────────────────────────────
        public override void Init(LevelConfig config, WorldLayout worldLayout, DragonSegmentIdentity[] segments, float spacing)
        {
            _startPos  = worldLayout.ViewportToWorld(config.dragonStartViewport);
            _endPos    = worldLayout.ViewportToWorld(config.dragonEndViewport);
            _moveSpeed = config.dragonMoveSpeed;
            _segments  = segments;
            _spacing   = spacing;
            Progress   = 0f;
            _isMoving  = true;

            transform.position = _startPos;
            UpdateSegmentOffsets();
        }

        public override void StopMoving()   => _isMoving = false;
        public override void ResumeMoving() => _isMoving = true;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Update()
        {
            if (!_isMoving) return;

            Progress += _moveSpeed * Time.deltaTime;
            transform.position = Vector3.Lerp(_startPos, _endPos, Progress);
            UpdateSegmentOffsets();

            if (HasAliveSegmentReachedEnd())
            {
                Progress = 1f;
                _isMoving = false;
                Debug.Log("[LinearDragonMovement] Dragon reached princess!");
                GameEvents.FireLevelLose();
            }
        }

        private void UpdateSegmentOffsets()
        {
            if (_segments == null) return;

            int aliveIndex = 0;
            for (int i = 0; i < _segments.Length; i++)
            {
                if (!_segments[i].IsAlive) continue;

                _segments[i].transform.localPosition = Vector3.right * (aliveIndex * _spacing);
                aliveIndex++;
            }
        }

        private bool HasAliveSegmentReachedEnd()
        {
            return GetAliveSegmentCount() > 0 && Progress >= 1f;
        }

        private int GetAliveSegmentCount()
        {
            if (_segments == null) return 0;

            int aliveCount = 0;
            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i].IsAlive)
                    aliveCount++;
            }
            return aliveCount;
        }

        // ── Debug ────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Log Dragon Progress")]
        private void DebugLogProgress()
        {
            Debug.Log($"[LinearDragonMovement] Progress: {Progress:P1} | Moving: {_isMoving}");
        }
    }
}
