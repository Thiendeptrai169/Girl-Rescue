using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using DragonRescue.Data;
// Temporarily commented out until Steps E and F are complete to prevent Editor errors
// using DragonRescue.Entities.Cannon;
// using DragonRescue.Entities.Dragon;
// using DragonRescue.Entities.Princess;
// using DragonRescue.UI;

namespace DragonRescue.Core
{
    /// <summary>
    /// Responsible for spawning and clearing all level entities.
    /// Uses Unity's built-in ObjectPool to avoid Instantiate/Destroy overhead.
    /// </summary>
    public class LevelManager : Singleton<LevelManager>
    {
        // ── Inspector — Spawn Points ──────────────────────────────────────────
        [Header("Spawn Points")]
        [SerializeField] private Transform _princessSpawnPoint;
        [SerializeField] private Transform _dragonSpawnRoot;     // first segment placed here
        [SerializeField] private float _segmentSpacing = 1.2f;   // gap between segments

        // ── Inspector — UI Parents ────────────────────────────────────────────
        [Header("UI Parents")]
        [SerializeField] private Transform _slotBarParent;
        [SerializeField] private Transform _cannonTrayParent;

        // ── Inspector — Prefabs ───────────────────────────────────────────────
        [Header("Prefabs")]
        [SerializeField] private GameObject _princessPrefab;
        [SerializeField] private GameObject _dragonSegmentPrefab;
        [SerializeField] private GameObject _cannonSlotPrefab;
        [SerializeField] private GameObject _cannonCardPrefab;

        // ── Inspector — View References ───────────────────────────────────────
        // [Header("Views")]
        // [SerializeField] private SlotBarView _slotBarView; // Temporarily commented out

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Fired after all entities have been spawned and are ready.</summary>
        public event Action<LevelDefinition> OnLevelReady;

        // ── Pools ─────────────────────────────────────────────────────────────
        private ObjectPool<GameObject> _segmentPool;
        private ObjectPool<GameObject> _slotPool;
        private ObjectPool<GameObject> _cardPool;

        // ── Runtime tracking (for ClearLevel) ────────────────────────────────
        private GameObject _princessInstance;
        private readonly List<GameObject> _activeSegments = new();
        private readonly List<GameObject> _activeSlots    = new();
        private readonly List<GameObject> _activeCards    = new();

        // ── Public API ────────────────────────────────────────────────────────
        public void InitLevel(LevelDefinition definition)
        {
            InitLevelAsync(definition, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void ClearLevel()
        {
            foreach (var seg  in _activeSegments) PoolManager.Instance.Release(_dragonSegmentPrefab, seg);
            foreach (var slot in _activeSlots)    PoolManager.Instance.Release(_cannonSlotPrefab, slot);
            foreach (var card in _activeCards)    PoolManager.Instance.Release(_cannonCardPrefab, card);

            _activeSegments.Clear();
            _activeSlots.Clear();
            _activeCards.Clear();

            if (_princessInstance != null)
            {
                Destroy(_princessInstance);
                _princessInstance = null;
            }

            // Temporarily commented out until Step F is complete
            // _slotBarView.ClearSlots();

            Debug.Log("[LevelManager] Level cleared.");
        }

        // ── Private — Async Init ──────────────────────────────────────────────
        private async UniTask InitLevelAsync(LevelDefinition definition, System.Threading.CancellationToken ct)
        {
            Debug.Log($"[LevelManager] Loading: {definition.LevelName}");

            SpawnPrincess();
            SpawnDragonSegments(definition);
            SpawnSlots(definition);
            SpawnCannonCards(definition);

            await UniTask.Yield(ct);

            Debug.Log($"[LevelManager] {definition.LevelName} ready.");
            OnLevelReady?.Invoke(definition);
        }

        // ── Private — Spawning ────────────────────────────────────────────────
        private void SpawnPrincess()
        {
            _princessInstance = Instantiate(_princessPrefab, _princessSpawnPoint.position, Quaternion.identity);
            _princessInstance.name = "Princess";
        }

        private void SpawnDragonSegments(LevelDefinition definition)
        {
            for (int i = 0; i < definition.DragonSegments.Length; i++)
            {
                var segData = definition.DragonSegments[i];
                var go      = PoolManager.Instance.Get(_dragonSegmentPrefab);

                go.transform.position = _dragonSpawnRoot.position + Vector3.right * (i * _segmentSpacing);
                
                // Temporarily commented out until Step E is complete
                // if (go.TryGetComponent<SegmentIdentity>(out var identity)) identity.Init(segData);
                // if (go.TryGetComponent<SegmentVisual>(out var visual)) visual.Init(segData.Color);

                _activeSegments.Add(go);
            }
        }

        private void SpawnSlots(LevelDefinition definition)
        {
            for (int i = 0; i < definition.SlotCount; i++)
            {
                var go = PoolManager.Instance.Get(_cannonSlotPrefab, _slotBarParent);

                // Temporarily commented out until Step F is complete
                // if (go.TryGetComponent<CannonSlot>(out var slot)) _slotBarView.RegisterSlot(slot);

                _activeSlots.Add(go);
            }
        }

        private void SpawnCannonCards(LevelDefinition definition)
        {
            foreach (var cannonDef in definition.AvailableCannons)
            {
                var go = PoolManager.Instance.Get(_cannonCardPrefab, _cannonTrayParent);

                // Temporarily commented out until Step F is complete
                // if (go.TryGetComponent<CannonCardView>(out var card)) card.Init(cannonDef, _slotBarView);

                _activeCards.Add(go);
            }
        }

        // ── Debug ─────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Log Pool Counts")]
        private void DebugLogCounts()
        {
            Debug.Log($"[LevelManager] Segments: {_activeSegments.Count} | Slots: {_activeSlots.Count} | Cards: {_activeCards.Count}");
        }
    }
}
