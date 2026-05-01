using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DragonRescue.Data;
using DragonRescue.Entities.Dragon;
using DragonRescue.Entities.Board;
using DragonRescue.Entities.Cannon;
using DragonRescue.Entities.Princess;
using DragonRescue.Entities.Projectile;
using DragonRescue.Booster;

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
            if (config == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Level, "InitLevel failed: LevelConfig is null.", this);
                return;
            }

            DebugSystem.Log(DebugCategory.Level, $"InitLevel requested levelNumber={config.levelNumber} id={config.levelId}", this);
            InitLevelAsync(config, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void ClearLevel()
        {
            DebugSystem.Log(
                DebugCategory.Level,
                $"ClearLevel begin segments={_activeSegments.Count} hasBoard={_boardInstance != null} hasSlotBar={_slotBarInstance != null} hasDragon={_dragonInstance != null} hasPrincess={_princessInstance != null}",
                this);

            // Release pooled segments
            foreach (var seg in _activeSegments)
            {
                if (seg != null)
                {
                    DebugSystem.Log(DebugCategory.Pooling, $"Release dragon segment {seg.name}", seg);
                    PoolManager.Instance.Release(_dragonSegmentPrefab, seg);
                }
            }
            _activeSegments.Clear();
            // Clear Board
            if (_boardInstance != null)
            {
                DebugSystem.Log(DebugCategory.Level, "Clearing board instance.", _boardInstance);
                if (_boardInstance.TryGetComponent(out BoardManager boardManager))
                    boardManager.ClearBoard();
                else
                    DebugSystem.AlwaysError(DebugCategory.Level, "ClearLevel found board instance without BoardManager. Check for missing script on board prefab.", _boardInstance);

                Destroy(_boardInstance);
                _boardInstance = null;
            }

            // Clear Slot Bar
            if (_slotBarInstance != null)
            {
                DebugSystem.Log(DebugCategory.Level, "Clearing slot bar instance.", _slotBarInstance);
                if (_slotBarInstance.TryGetComponent(out SlotBarManager slotBarManager))
                    slotBarManager.ClearAllSlots();
                else
                    DebugSystem.AlwaysError(DebugCategory.Level, "ClearLevel found slot bar instance without SlotBarManager. Check for missing script on slot bar prefab.", _slotBarInstance);

                Destroy(_slotBarInstance);
                _slotBarInstance = null;
            }

            // Destroy dragon root
            if (_dragonInstance != null)
            {
                DebugSystem.Log(DebugCategory.Level, "Destroying dragon instance.", _dragonInstance);
                Destroy(_dragonInstance);
                _dragonInstance = null;
            }

            // Destroy princess
            if (_princessInstance != null)
            {
                DebugSystem.Log(DebugCategory.Level, "Destroying princess instance.", _princessInstance);
                Destroy(_princessInstance);
                _princessInstance = null;
            }

            // Clear stale event subscriptions
            DebugSystem.Log(DebugCategory.Level, "Clearing level-scoped GameEvents.", this);
            GameEvents.ClearLevelEvents();

            DebugSystem.Log(DebugCategory.Level, "Level cleared.", this);
        }

        // ── Private — Async Init ──────────────────────────────────────────────
        private async UniTask InitLevelAsync(LevelConfig config, System.Threading.CancellationToken ct)
        {
            DebugSystem.Log(DebugCategory.Level, $"Loading Level {config.levelNumber}: {config.levelId}", this);

            if (!ValidateLevelSetup())
            {
                DebugSystem.AlwaysError(DebugCategory.Level, $"Level {config.levelNumber} load aborted because required scene/prefab references are invalid.", this);
                return;
            }

            SpawnPrincess(config);
            SpawnDragon(config);
            SpawnBoard(config);
            SpawnSlotBar(config);
            SetupBoosters(config);

            await UniTask.Yield(ct);

            DebugSystem.Log(DebugCategory.Level, $"Level {config.levelNumber} ready.", this);
            OnLevelReady?.Invoke(config);
            GameEvents.FireLevelStarted(config);
        }

        // ── Private — Spawning ────────────────────────────────────────────────
        private void SpawnPrincess(LevelConfig config)
        {
            if (_worldLayout == null) return;
            Vector3 pos = _worldLayout.ViewportToWorld(config.princessViewport);
            DebugSystem.Log(DebugCategory.Level, $"SpawnPrincess viewport={config.princessViewport} world={pos}", this);
            _princessInstance = Instantiate(_princessPrefab, pos, Quaternion.identity);
            _princessInstance.name = "Princess";
        }

        private void SpawnDragon(LevelConfig config)
        {
            if (_worldLayout == null) return;
            // Spawn dragon root GO with DragonManager
            Vector3 spawnPos = _worldLayout.ViewportToWorld(config.dragonSpawnViewport);
            DebugSystem.Log(DebugCategory.Level, $"SpawnDragon spawnViewport={config.dragonSpawnViewport} world={spawnPos} movement={config.dragonMovementType}", this);
            _dragonInstance = Instantiate(_dragonPrefab, spawnPos, Quaternion.identity);
            _dragonInstance.name = "Dragon";

            var dragonManager = _dragonInstance.GetComponent<DragonManager>();
            if (dragonManager == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Level, "Dragon prefab instance is missing DragonManager. Level load cannot continue.", _dragonInstance);
                return;
            }

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
                    if (identity == null)
                    {
                        DebugSystem.AlwaysError(DebugCategory.Level, "Dragon segment prefab instance is missing DragonSegmentIdentity. Check for missing script on the segment prefab.", segGO);
                        continue;
                    }

                    identity.Init(segData.color, 1);

                    segmentIdentities.Add(identity);
                    _activeSegments.Add(segGO);
                    DebugSystem.Log(DebugCategory.Level, $"SpawnDragonSegment index={globalIndex} color={segData.color}", segGO);

                    globalIndex++;
                }
            }

            // Init the dragon manager with all segments and strategy
            dragonManager.Init(config, _worldLayout, segmentIdentities, movementStrategy, _segmentSpacing);
        }

        private void SpawnBoard(LevelConfig config)
        {
            if (_worldLayout == null || _boardPrefab == null) return;

            var boardLayout = new BoardWorldLayout(
                _worldLayout.MainCamera,
                _worldLayout.ViewportToWorld(config.boardViewport),
                config.boardSize,
                config.boardWidthRatio,
                config.boardHeightRatio
            );
            DebugSystem.Log(DebugCategory.Level, $"SpawnBoard viewport={config.boardViewport} size={config.boardSize} cellSize={boardLayout.CellSize:0.###} origin={boardLayout.Origin}", this);

            _boardInstance = Instantiate(_boardPrefab, Vector3.zero, Quaternion.identity);
            _boardInstance.name = "BoardManager";

            var boardManager = _boardInstance.GetComponent<BoardManager>();
            if (boardManager == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Level, "Board prefab instance is missing BoardManager. Board input will not work until this prefab is fixed.", _boardInstance);
                return;
            }

            boardManager.Init(config, boardLayout, _worldLayout.MainCamera);
        }

        private void SpawnSlotBar(LevelConfig config)
        {
            if (_worldLayout == null || _slotBarPrefab == null) return;

            _slotBarInstance = Instantiate(_slotBarPrefab, Vector3.zero, Quaternion.identity);
            _slotBarInstance.name = "SlotBarManager";
            DebugSystem.Log(DebugCategory.Level, $"SpawnSlotBar totalSlots={config.totalSlotCount} unlocked={config.unlockedSlotCount}", _slotBarInstance);

            var slotBarManager = _slotBarInstance.GetComponent<SlotBarManager>();
            if (slotBarManager == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Level, "Slot bar prefab instance is missing SlotBarManager. Cannon slots will not work until this prefab is fixed.", _slotBarInstance);
                return;
            }

            slotBarManager.Init(config, _worldLayout, _projectilePrefab);
        }

        private void SetupBoosters(LevelConfig config)
        {
            if (BoosterManager.Instance == null)
            {
                DebugSystem.Log(DebugCategory.Level, "Creating BoosterManager runtime object.", this);
                var boosterGO = new GameObject("BoosterManager");
                boosterGO.AddComponent<BoosterManager>();
            }

            DebugSystem.Log(DebugCategory.Level, $"SetupBoosters count={(config.boosters != null ? config.boosters.Count : 0)}", this);
            BoosterManager.Instance.Init(config);
        }

        // ── Debug ─────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Log Pool Counts")]
        private void DebugLogCounts()
        {
            DebugSystem.Log(DebugCategory.Level, $"Segments: {_activeSegments.Count}", this);
        }

        private bool ValidateLevelSetup()
        {
            bool isValid = true;

            if (_worldLayout == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Level, "LevelManager missing WorldLayout reference.", this);
                isValid = false;
            }
            else if (_worldLayout.MainCamera == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Level, "WorldLayout missing MainCamera reference.", _worldLayout);
                isValid = false;
            }

            isValid &= ValidatePrefabComponent<PrincessIdentity>(_princessPrefab, "Princess Prefab");
            isValid &= ValidatePrefabComponent<DragonManager>(_dragonPrefab, "Dragon Prefab");
            isValid &= ValidatePrefabComponent<DragonSegmentIdentity>(_dragonSegmentPrefab, "Dragon Segment Prefab");
            isValid &= ValidatePrefabComponent<DragonSegmentVisual>(_dragonSegmentPrefab, "Dragon Segment Prefab");
            isValid &= ValidatePrefabComponent<BoardManager>(_boardPrefab, "Board Prefab");
            if (_boardPrefab != null && _boardPrefab.TryGetComponent(out BoardManager boardManager))
                isValid &= boardManager.ValidateSetup();

            isValid &= ValidatePrefabComponent<SlotBarManager>(_slotBarPrefab, "Slot Bar Prefab");
            isValid &= ValidatePrefabComponent<ProjectileIdentity>(_projectilePrefab, "Projectile Prefab");
            isValid &= ValidatePrefabComponent<ProjectileMovement>(_projectilePrefab, "Projectile Prefab");
            isValid &= ValidatePrefabComponent<ProjectileHitResolver>(_projectilePrefab, "Projectile Prefab");

            return isValid;
        }

        private bool ValidatePrefabComponent<T>(GameObject prefab, string label) where T : Component
        {
            if (prefab == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Level, $"{label} is not assigned on LevelManager.", this);
                return false;
            }

            if (prefab.GetComponent<T>() != null)
                return true;

            DebugSystem.AlwaysError(DebugCategory.Level, $"{label} is missing required component {typeof(T).Name}. Check for missing script on prefab '{prefab.name}'.", prefab);
            return false;
        }
    }
}
