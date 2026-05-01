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

        private int _initialTotalBlocks;

        private void Awake()
        {
            Instance = this;
            GameEvents.OnProjectileHit += OnProjectileHit;
        }

        // ── Public API ───────────────────────────────────────────────────────
        public void Init(LevelConfig config, WorldLayout worldLayout, List<DragonSegmentIdentity> segments, DragonMovementBase movementStrategy, float spacing)
        {
            _segments.Clear();
            _segments.AddRange(segments);

            _initialTotalBlocks = 0;
            for (int i = 0; i < _segments.Count; i++)
            {
                _initialTotalBlocks += _segments[i].MaxHp;
            }

            _movement = movementStrategy;
            // Init movement
            _movement.Init(config, worldLayout, _segments.ToArray(), spacing);

            // Listen for any segment death via central bus
            GameEvents.OnSegmentDestroyed += OnSegmentDestroyed;

            // Fire initial progress
            GameEvents.FireProgressUpdated(0f);
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

        public string BuildTargetDebugSummary(CannonColor color, int damage, Vector3 position, float range)
        {
            int aliveMatching = 0;
            int inRange = 0;
            int blockedByIncoming = 0;
            float nearestDistance = float.PositiveInfinity;
            float rangeSq = range * range;

            for (int i = 0; i < _segments.Count; i++)
            {
                DragonSegmentIdentity segment = _segments[i];
                if (segment == null || !segment.IsAlive || segment.Color != color)
                    continue;

                aliveMatching++;

                float sqrDistance = (segment.transform.position - position).sqrMagnitude;
                nearestDistance = Mathf.Min(nearestDistance, Mathf.Sqrt(sqrDistance));

                if (sqrDistance <= rangeSq)
                {
                    inRange++;

                    if (!segment.CanAcceptIncomingDamage(damage))
                        blockedByIncoming++;
                }
            }

            string nearest = float.IsPositiveInfinity(nearestDistance) ? "none" : nearestDistance.ToString("0.###");
            return $"color={color} aliveMatching={aliveMatching} inRange={inRange} blockedByIncoming={blockedByIncoming} nearest={nearest} range={range:0.###}";
        }

        public bool AreAllSegmentsDestroyed()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].IsAlive) return false;
            }
            return true;
        }

        public bool SortSegmentsByColor()
        {
            List<DragonSegmentIdentity> aliveSegments = new();
            HashSet<CannonColor> uniqueColors = new();

            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                if (!segment.IsAlive) continue;

                aliveSegments.Add(segment);
                uniqueColors.Add(segment.Color);
            }

            if (aliveSegments.Count <= 1 || uniqueColors.Count <= 1)
            {
                return false;
            }

            List<CannonColor> sortedColors = new(aliveSegments.Count);
            for (int i = 0; i < aliveSegments.Count; i++)
            {
                sortedColors.Add(aliveSegments[i].Color);
            }

            sortedColors.Sort();

            bool changed = false;
            for (int i = 0; i < aliveSegments.Count; i++)
            {
                if (aliveSegments[i].Color != sortedColors[i])
                {
                    changed = true;
                    aliveSegments[i].SetColor(sortedColors[i]);
                }
            }

            return changed;
        }

        public void StopDragon()   => _movement.StopMoving();
        public void ResumeDragon() => _movement.ResumeMoving();

        // ── Private ──────────────────────────────────────────────────────────
        private void OnSegmentDestroyed(SegmentDestroyedPayload payload)
        {
            if (!AreAllSegmentsDestroyed())
            {
                if (_movement != null)
                    _movement.ApplyRecoil();

                RecalculateProgress();
                return;
            }

            DebugSystem.Log(DebugCategory.Dragon, "All segments destroyed — WIN!", this);
            if (_movement != null)
                _movement.StopMoving();
            GameEvents.FireLevelWin();
        }

        private void OnDestroy()
        {
            GameEvents.OnSegmentDestroyed -= OnSegmentDestroyed;
            GameEvents.OnProjectileHit -= OnProjectileHit;
        }

        private void OnProjectileHit(ProjectileHitPayload payload)
        {
            RecalculateProgress();
        }

        private void RecalculateProgress()
        {
            if (_initialTotalBlocks <= 0) return;

            int currentTotalBlocks = 0;
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].IsAlive)
                {
                    currentTotalBlocks += _segments[i].Count;
                }
            }

            int destroyedBlocks = _initialTotalBlocks - currentTotalBlocks;
            float progress = (float)destroyedBlocks / _initialTotalBlocks;
            GameEvents.FireProgressUpdated(Mathf.Clamp01(progress));
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
                DebugSystem.Log(DebugCategory.Dragon, $"Segment {i}: {s.Color} | Count: {s.Count}/{s.MaxHp} | Incoming: {s.IncomingDamage} | Alive: {s.IsAlive}", this);
            }
        }
    }
}
