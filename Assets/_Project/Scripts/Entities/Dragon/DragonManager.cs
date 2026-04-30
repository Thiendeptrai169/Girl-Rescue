using System.Collections.Generic;
using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// Orchestrator for the dragon entity.
    /// Holds segment references, delegates movement to DragonMovement,
    /// provides targeting API, and checks win condition via GameEvents.
    /// All communication through GameEvents — no local events.
    /// </summary>
    public class DragonManager : MonoBehaviour
    {
        // ── Runtime State ────────────────────────────────────────────────────
        public static DragonManager Instance { get; private set; }

        private DragonMovementBase _movement;
        private readonly List<DragonSegmentIdentity> _segments = new();

        private void Awake()
        {
            Instance = this;
        }

        // ── Public API ───────────────────────────────────────────────────────
        public void Init(LevelConfig config, WorldLayout worldLayout, List<DragonSegmentIdentity> segments, DragonMovementBase movementStrategy, float spacing)
        {
            _segments.Clear();
            _segments.AddRange(segments);

            _movement = movementStrategy;
            // Init movement
            _movement.Init(config, worldLayout, _segments.ToArray(), spacing);

            // Listen for any segment death via central bus
            GameEvents.OnSegmentDestroyed += OnSegmentDestroyed;
        }

        /// <summary>
        /// Find the nearest alive segment matching the given color.
        /// Used by cannons for auto-targeting.
        /// </summary>
        public DragonSegmentIdentity FindTargetByColor(CannonColor color, int damage, Vector3 position, float range)
        {
            float rangeSq = range * range;
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                if (seg.IsAlive && seg.Color == color && seg.CanAcceptIncomingDamage(damage))
                {
                    if ((seg.transform.position - position).sqrMagnitude <= rangeSq)
                    {
                        return seg;
                    }
                }
            }
            return null;
        }

        public bool AreAllSegmentsDestroyed()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].IsAlive) return false;
            }
            return true;
        }

        public void StopDragon()   => _movement.StopMoving();
        public void ResumeDragon() => _movement.ResumeMoving();

        // ── Private ──────────────────────────────────────────────────────────
        private void OnSegmentDestroyed(SegmentDestroyedPayload payload)
        {
            if (!AreAllSegmentsDestroyed()) return;

            Debug.Log("[DragonManager] All segments destroyed — WIN!");
            _movement.StopMoving();
            GameEvents.FireLevelWin();
        }

        private void OnDestroy()
        {
            GameEvents.OnSegmentDestroyed -= OnSegmentDestroyed;
        }

        // ── Debug ────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Kill All Segments")]
        private void DebugKillAll()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].IsAlive)
                    _segments[i].TakeDamage(999);
            }
        }

        [ContextMenu("Debug / Log Segment Status")]
        private void DebugLogSegments()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                Debug.Log($"Segment {i}: {s.Color} | Count: {s.Count}/{s.MaxHp} | Incoming: {s.IncomingDamage} | Alive: {s.IsAlive}");
            }
        }
    }
}
