using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DragonRescue.Data;
using DragonRescue.Entities.Dragon;
using DragonRescue.Entities.Board;
using DragonRescue.Entities.Cannon;

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
        [SerializeField] private GameObject _boardPrefab;          // BoardManager with grid orchestration
        [SerializeField] private GameObject _slotBarPrefab;        // Manages all Cannon Slots
        [SerializeField] private GameObject _projectilePrefab;     // Fired by cannons

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Fired after all entities have been spawned and are ready.</summary>
        public event System.Action<LevelConfig> OnLevelReady;

        // ── Runtime tracking (for ClearLevel) ────────────────────────────────
        private GameObject _princessInstance;
        private GameObject _dragonInstance;
        private GameObject _boardInstance;
        private GameObject _slotBarInstance;
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
            // Clear Board
            if (_boardInstance != null)
            {
                _boardInstance.GetComponent<BoardManager>().ClearBoard();
                Destroy(_boardInstance);
                _boardInstance = null;
            }

            // Clear Slot Bar
            if (_slotBarInstance != null)
            {
                _slotBarInstance.GetComponent<SlotBarManager>().ClearAllSlots();
                Destroy(_slotBarInstance);
                _slotBarInstance = null;
            }

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
            SpawnBoard(config);
            SpawnSlotBar(config);

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
            int globalIndex = 0;

            // Spawn segments as children. Each config row spawns count blocks of that color.
            for (int i = 0; i < config.dragonSegments.Count; i++)
            {
                var segData = config.dragonSegments[i];
                int segmentCount = Mathf.Max(1, segData.count);

                for (int j = 0; j < segmentCount; j++)
                {
                    var segGO = PoolManager.Instance.Get(_dragonSegmentPrefab, _dragonInstance.transform);

                    segGO.transform.localPosition = Vector3.right * (globalIndex * _segmentSpacing);
                    segGO.name = $"Segment_{globalIndex}_{segData.color}";

                    var identity = segGO.GetComponent<DragonSegmentIdentity>();
                    identity.Init(segData.color, 1);

                    segmentIdentities.Add(identity);
                    _activeSegments.Add(segGO);

                    globalIndex++;
                }
            }

            // Init the dragon manager with all segments and strategy
            dragonManager.Init(config, _worldLayout, segmentIdentities, movementStrategy, _segmentSpacing);
        }

        private void SpawnBoard(LevelConfig config)
        {
            if (_worldLayout == null || _boardPrefab == null) return;

            // Use hardcoded 0.5, 0.27 viewport for board center as defined in architecture
            // In a real scenario, this could be added to WorldLayout inspector fields
            Vector2 boardCenterVp = new Vector2(0.5f, 0.27f);
            
            var boardLayout = new BoardWorldLayout(
                _worldLayout.MainCamera,
                _worldLayout.ViewportToWorld(boardCenterVp),
                config.boardSize
            );

            _boardInstance = Instantiate(_boardPrefab, Vector3.zero, Quaternion.identity);
            _boardInstance.name = "BoardManager";

            var boardManager = _boardInstance.GetComponent<BoardManager>();
            boardManager.Init(config, boardLayout, _worldLayout.MainCamera);
        }

        private void SpawnSlotBar(LevelConfig config)
        {
            if (_worldLayout == null || _slotBarPrefab == null) return;

            _slotBarInstance = Instantiate(_slotBarPrefab, Vector3.zero, Quaternion.identity);
            _slotBarInstance.name = "SlotBarManager";

            var slotBarManager = _slotBarInstance.GetComponent<SlotBarManager>();
            slotBarManager.Init(config, _worldLayout, _projectilePrefab);
        }

        // ── Debug ─────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Log Pool Counts")]
        private void DebugLogCounts()
        {
            Debug.Log($"[LevelManager] Segments: {_activeSegments.Count}");
        }
    }
}
