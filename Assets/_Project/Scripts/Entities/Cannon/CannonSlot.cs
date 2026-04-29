using System;
using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Cannon
{
    /// <summary>
    /// The physical slot in the world/UI where a cannon is placed.
    /// Manages the visual spawning of the cannon prefab inside itself.
    /// </summary>
    public class CannonSlot : MonoBehaviour
    {
        public bool IsOccupied { get; private set; }
        private CannonDefinition _currentCannon;
        private GameObject _cannonInstance;

        [SerializeField] private GameObject _cannonEntityPrefab;
        [SerializeField] private Transform _cannonParent;

        public event Action<CannonDefinition> OnCannonDeployed;
        public event Action OnSlotFreed;

        public void DeployCannon(CannonDefinition definition)
        {
            if (IsOccupied) return;

            _currentCannon = definition;
            IsOccupied = true;

            // Get a CannonEntity from the pool
            _cannonInstance = PoolManager.Instance.Get(_cannonEntityPrefab, _cannonParent);
            _cannonInstance.transform.localPosition = Vector3.zero;

            if (_cannonInstance.TryGetComponent<CannonIdentity>(out var identity))
                identity.Init(definition);

            if (_cannonInstance.TryGetComponent<CannonVisual>(out var visual))
                visual.Init(definition);

            OnCannonDeployed?.Invoke(definition);
        }

        public void RemoveCannon()
        {
            if (!IsOccupied) return;

            PoolManager.Instance.Release(_cannonEntityPrefab, _cannonInstance);
            
            _cannonInstance = null;
            _currentCannon = null;
            IsOccupied = false;

            OnSlotFreed?.Invoke();
        }
    }
}
