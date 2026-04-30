using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DragonRescue.Data;
using DragonRescue.Entities.Dragon;

namespace DragonRescue.Core
{
    /// <summary>
    /// Responsible for spawning and clearing all level entities.
    /// Uses PoolManager for dragon segments to avoid Instantiate/Destroy overhead.
    /// </summary>
    public class LevelManager : Singleton<LevelManager>
    {
        [SerializeField] private float _segmentSpacing = 1.2f;

        [Header("Layout")]
        [SerializeField] private WorldLayout _worldLayout;

        // ── Inspector — Prefabs ───────────────────────────────────────────────
        [Header("Prefabs")]
        [SerializeField] private GameObject _princessPrefab;
        [SerializeField] private GameObject _dragonPrefab;         // Root with DragonManager + DragonMovement
        [SerializeField] private GameObject _dragonSegmentPrefab;  // Child with Identity + Visual

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Fired after all entities have been spawned and are ready.</summary>
        public event System.Action<LevelConfig> OnLevelReady;

        // ── Runtime tracking (for ClearLevel) ────────────────────────────────
        private GameObject _princessInstance;
        private GameObject _dragonInstance;
        private readonly List<GameObject> _activeSegments = new();

        // ── Public API ────────────────────────────────────────────────────────
        public void InitLevel(LevelConfig config)
        {
            InitLevelAsync(config, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void ClearLevel()
        {
            // Release pooled segments
            foreach (var seg in _activeSegments)
            {
                if (seg != null)
                    PoolManager.Instance.Release(_dragonSegmentPrefab, seg);
            }
            _activeSegments.Clear();

            // Destroy dragon root
            if (_dragonInstance != null)
            {
                Destroy(_dragonInstance);
                _dragonInstance = null;
            }

            // Destroy princess
            if (_princessInstance != null)
            {
                Destroy(_princessInstance);
                _princessInstance = null;
            }

            // Clear stale event subscriptions
            GameEvents.ClearLevelEvents();

            Debug.Log("[LevelManager] Level cleared.");
        }

        // ── Private — Async Init ──────────────────────────────────────────────
        private async UniTask InitLevelAsync(LevelConfig config, System.Threading.CancellationToken ct)
        {
            Debug.Log($"[LevelManager] Loading Level {config.levelNumber}: {config.levelId}");

            SpawnPrincess(config);
            SpawnDragon(config);

            // TODO Day 2: SpawnBoard(config);
            // TODO Day 3: SpawnSlots(config);
            // TODO Day 4: SetupBoosters(config);

            await UniTask.Yield(ct);

            Debug.Log($"[LevelManager] Level {config.levelNumber} ready.");
            OnLevelReady?.Invoke(config);
        }

        // ── Private — Spawning ────────────────────────────────────────────────
        private void SpawnPrincess(LevelConfig config)
        {
            if (_worldLayout == null) return;
            Vector3 pos = _worldLayout.ViewportToWorld(config.princessViewport);
            _princessInstance = Instantiate(_princessPrefab, pos, Quaternion.identity);
            _princessInstance.name = "Princess";
        }

        private void SpawnDragon(LevelConfig config)
        {
            if (_worldLayout == null) return;
            // Spawn dragon root GO with DragonManager
            Vector3 startPos = _worldLayout.ViewportToWorld(config.dragonStartViewport);
            _dragonInstance = Instantiate(_dragonPrefab, startPos, Quaternion.identity);
            _dragonInstance.name = "Dragon";

            var dragonManager = _dragonInstance.GetComponent<DragonManager>();

            DragonMovementBase movementStrategy = config.dragonMovementType switch
            {
                DragonMovementType.Linear => _dragonInstance.AddComponent<LinearDragonMovement>(),
                DragonMovementType.Waypoint => _dragonInstance.AddComponent<WaypointDragonMovement>(),
                _ => _dragonInstance.AddComponent<LinearDragonMovement>()
            };

            var segmentIdentities = new List<DragonSegmentIdentity>();

            // Spawn segments as children
            for (int i = 0; i < config.dragonSegments.Count; i++)
            {
                var segData = config.dragonSegments[i];
                var segGO = PoolManager.Instance.Get(_dragonSegmentPrefab, _dragonInstance.transform);

                segGO.transform.localPosition = Vector3.right * (i * _segmentSpacing);
                segGO.name = $"Segment_{i}_{segData.color}";

                var identity = segGO.GetComponent<DragonSegmentIdentity>();
                identity.Init(segData);

                segmentIdentities.Add(identity);
                _activeSegments.Add(segGO);
            }

            // Init the dragon manager with all segments and strategy
            dragonManager.Init(config, _worldLayout, segmentIdentities, movementStrategy, _segmentSpacing);
        }

        // ── Debug ─────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Log Pool Counts")]
        private void DebugLogCounts()
        {
            Debug.Log($"[LevelManager] Segments: {_activeSegments.Count}");
        }
    }
}
