using System;
using System.Collections.Generic;
using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Entities.Cannon;

namespace DragonRescue.UI
{
    /// <summary>
    /// Manages the row of active slots.
    /// UI cards call TryPlaceCannon on this to find an empty slot.
    /// </summary>
    public class SlotBarView : MonoBehaviour
    {
        private readonly List<CannonSlot> _slots = new();

        /// <summary>Fired when a player tries to place a cannon but all slots are full.</summary>
        public event Action OnAllSlotsFull;

        public void RegisterSlot(CannonSlot slot)
        {
            if (!_slots.Contains(slot))
                _slots.Add(slot);
        }

        public void ClearSlots()
        {
            foreach (var slot in _slots)
            {
                slot.RemoveCannon();
            }
            _slots.Clear();
        }

        /// <summary>
        /// Attempts to place a cannon in the first available slot.
        /// </summary>
        /// <returns>True if successfully placed, false if slots are full.</returns>
        public bool TryPlaceCannon(CannonDefinition definition)
        {
            foreach (var slot in _slots)
            {
                if (!slot.IsOccupied)
                {
                    slot.DeployCannon(definition);
                    return true;
                }
            }

            Debug.LogWarning("[SlotBarView] All slots are full!");
            OnAllSlotsFull?.Invoke();
            return false;
        }

        [ContextMenu("Debug / Log Slot Status")]
        private void DebugLogStatus()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Debug.Log($"Slot {i}: {(_slots[i].IsOccupied ? "FULL" : "EMPTY")}");
            }
        }
    }
}
