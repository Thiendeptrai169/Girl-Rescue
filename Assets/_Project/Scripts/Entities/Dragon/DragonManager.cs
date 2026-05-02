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
        private sealed class DragonSegmentGroup
        {
            public CannonColor Color;
            public readonly List<DragonSegmentIdentity> Members = new();
        }

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

        public bool SortSegmentsByCannonPriority(IReadOnlyList<CannonColor> loadedSlotColors)
        {
            List<DragonSegmentGroup> leadingGroups = GetLeadingAliveGroups(4);
            if (leadingGroups.Count <= 1)
                return false;

            List<DragonSegmentGroup> priorityGroups = BuildCannonPriorityGroups(leadingGroups, loadedSlotColors);
            if (priorityGroups.Count == 0)
                priorityGroups = BuildFallbackSortIllusionGroups(leadingGroups);

            bool sorted = MoveGroupsToFront(priorityGroups);
            DebugSystem.Log(DebugCategory.Booster, $"Sort applied loadedSlots={FormatColors(loadedSlotColors)} groups={FormatGroups(priorityGroups)} sorted={sorted}.", this);
            return sorted;
        }

        public int DestroyLeadingSegmentsByColor(CannonColor color, int count)
        {
            if (count <= 0)
                return 0;

            int destroyed = 0;

            for (int i = 0; i < _segments.Count && destroyed < count; i++)
            {
                DragonSegmentIdentity segment = _segments[i];
                if (segment == null || !segment.IsAlive || segment.Color != color)
                    continue;

                segment.TakeDamage(999);
                destroyed++;
            }

            if (destroyed > 0)
                RecalculateProgress();

            DebugSystem.Log(DebugCategory.Dragon, $"Remove booster destroyed {destroyed}/{count} {color} dragon segments.", this);
            return destroyed;
        }

        public void StopDragon()   => _movement.StopMoving();
        public void ResumeDragon() => _movement.ResumeMoving();

        private List<DragonSegmentGroup> GetLeadingAliveGroups(int maxGroupCount)
        {
            var result = new List<DragonSegmentGroup>();
            DragonSegmentGroup currentGroup = null;

            for (int i = 0; i < _segments.Count; i++)
            {
                DragonSegmentIdentity segment = _segments[i];
                if (segment == null || !segment.IsAlive)
                    continue;

                if (currentGroup == null || currentGroup.Color != segment.Color)
                {
                    if (result.Count >= maxGroupCount)
                        break;

                    currentGroup = new DragonSegmentGroup { Color = segment.Color };
                    result.Add(currentGroup);
                }

                currentGroup.Members.Add(segment);
            }

            return result;
        }

        private List<DragonSegmentGroup> BuildCannonPriorityGroups(
            List<DragonSegmentGroup> leadingGroups,
            IReadOnlyList<CannonColor> loadedSlotColors)
        {
            var result = new List<DragonSegmentGroup>();
            if (loadedSlotColors == null || loadedSlotColors.Count == 0)
                return result;

            var usedGroups = new HashSet<DragonSegmentGroup>();
            for (int slotIndex = 0; slotIndex < loadedSlotColors.Count; slotIndex++)
            {
                CannonColor color = loadedSlotColors[slotIndex];

                for (int i = 0; i < leadingGroups.Count; i++)
                {
                    DragonSegmentGroup group = leadingGroups[i];
                    if (group.Color == color && usedGroups.Add(group))
                        result.Add(group);
                }
            }

            if (result.Count == 0)
                return result;

            for (int i = 0; i < leadingGroups.Count; i++)
            {
                if (!usedGroups.Contains(leadingGroups[i]))
                    result.Add(leadingGroups[i]);
            }

            return result;
        }

        private List<DragonSegmentGroup> BuildFallbackSortIllusionGroups(List<DragonSegmentGroup> leadingGroups)
        {
            var result = new List<DragonSegmentGroup>();

            if (leadingGroups.Count >= 4)
            {
                result.Add(leadingGroups[3]);
                result.Add(leadingGroups[2]);
                result.Add(leadingGroups[0]);
                result.Add(leadingGroups[1]);
            }
            else
            {
                for (int i = leadingGroups.Count - 1; i >= 0; i--)
                    result.Add(leadingGroups[i]);
            }

            return result;
        }

        private bool MoveGroupsToFront(List<DragonSegmentGroup> priorityGroups)
        {
            if (priorityGroups == null || priorityGroups.Count == 0)
                return false;

            var newOrder = new List<DragonSegmentIdentity>(_segments.Count);
            var moved = new HashSet<DragonSegmentIdentity>();

            for (int i = 0; i < priorityGroups.Count; i++)
            {
                DragonSegmentGroup group = priorityGroups[i];
                for (int j = 0; j < group.Members.Count; j++)
                {
                    DragonSegmentIdentity member = group.Members[j];
                    if (member != null && moved.Add(member))
                        newOrder.Add(member);
                }
            }

            for (int i = 0; i < _segments.Count; i++)
            {
                DragonSegmentIdentity segment = _segments[i];
                if (segment != null && !moved.Contains(segment))
                    newOrder.Add(segment);
            }

            if (newOrder.Count != _segments.Count)
                return false;

            _segments.Clear();
            _segments.AddRange(newOrder);

            GameEvents.FireDragonSegmentsSorted(new DragonSegmentsSortedPayload
            {
                Manager = this,
                OrderedSegments = _segments.ToArray()
            });

            return true;
        }

        private string FormatColors(IReadOnlyList<CannonColor> colors)
        {
            if (colors == null || colors.Count == 0)
                return "none";

            string result = "";
            for (int i = 0; i < colors.Count; i++)
            {
                if (result.Length > 0)
                    result += ",";

                result += colors[i];
            }

            return result;
        }

        private string FormatGroups(List<DragonSegmentGroup> groups)
        {
            if (groups == null || groups.Count == 0)
                return "none";

            string result = "";
            for (int i = 0; i < groups.Count; i++)
            {
                if (result.Length > 0)
                    result += ",";

                result += $"{groups[i].Color}x{groups[i].Members.Count}";
            }

            return result;
        }

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
                _movement.RefreshVisuals();

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
