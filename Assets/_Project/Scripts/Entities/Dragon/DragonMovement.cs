using UnityEngine;
using DragonRescue.Core;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// EC Movement component for the dragon root.
    /// Handles linear path movement (start → end) using progress 0..1.
    /// Fires directly into GameEvents — no local events.
    /// </summary>
    public class DragonMovement : MonoBehaviour
    {
        // ── Runtime State ────────────────────────────────────────────────────
        private float _moveSpeed;
        private float _progress;
        private Vector3 _startPos;
        private Vector3 _endPos;
        private bool _isMoving;

        public float Progress => _progress;

        // ── Public API ───────────────────────────────────────────────────────
        public void Init(Vector3 start, Vector3 end, float speed)
        {
            _startPos  = start;
            _endPos    = end;
            _moveSpeed = speed;
            _progress  = 0f;
            _isMoving  = true;

            transform.position = _startPos;
        }

        public void StopMoving()   => _isMoving = false;
        public void ResumeMoving() => _isMoving = true;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Update()
        {
            if (!_isMoving) return;

            _progress += _moveSpeed * Time.deltaTime;
            transform.position = Vector3.Lerp(_startPos, _endPos, _progress);

            if (_progress >= 1f)
            {
                _progress = 1f;
                _isMoving = false;
                Debug.Log("[DragonMovement] Dragon reached princess!");
                GameEvents.FireLevelLose();
            }
        }

        // ── Debug ────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Log Dragon Progress")]
        private void DebugLogProgress()
        {
            Debug.Log($"[DragonMovement] Progress: {_progress:P1} | Moving: {_isMoving}");
        }
    }
}
