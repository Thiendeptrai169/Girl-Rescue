using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Cannon
{
    public class SlotBarManager : MonoBehaviour
    {
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private float _slotSpacing = 1.2f;

        private CannonSlot[] _slots;

        public void Init(LevelConfig config, WorldLayout layout, GameObject projectilePrefab)
        {
            // Position slot bar using viewport (e.g., center-bottom of play area)
            // Can be adjusted later if it overlaps HUD
            transform.position = layout.ViewportToWorld(new Vector2(0.5f, 0.55f));

            int totalSlots = config.totalSlotCount;
            _slots = new CannonSlot[totalSlots];

            float totalWidth = (totalSlots - 1) * _slotSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < totalSlots; i++)
            {
                var slotGO = Instantiate(_slotPrefab, transform);
                slotGO.transform.localPosition = new Vector3(startX + (i * _slotSpacing), 0, 0);
                slotGO.name = $"CannonSlot_{i}";

                bool isUnlocked = i < config.unlockedSlotCount;
                var slot = slotGO.GetComponent<CannonSlot>();
                slot.Init(i, isUnlocked, config, projectilePrefab);
                _slots[i] = slot;
            }

            GameEvents.OnBlockEscaped += OnBlockEscaped;
        }

        private void OnDestroy()
        {
            GameEvents.OnBlockEscaped -= OnBlockEscaped;
        }

        private void OnBlockEscaped(BlockEscapedPayload payload)
        {
            if (payload.Ammo <= 0) return;

            CannonSlot targetSlot = FindEmptyUnlockedSlot();

            if (targetSlot != null)
            {
                targetSlot.LoadCannon(payload.Color, payload.Ammo);
            }
            else
            {
                Debug.LogWarning("[SlotBarManager] No empty slots available! Block wasted.");
            }
        }

        public bool CanAcceptBlock(int ammo)
        {
            return ammo <= 0 || FindEmptyUnlockedSlot() != null;
        }

        private CannonSlot FindEmptyUnlockedSlot()
        {
            if (_slots == null) return null;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].IsUnlocked && !_slots[i].IsLoaded)
                {
                    return _slots[i];
                }
            }

            return null;
        }

        public void ClearAllSlots()
        {
            if (_slots == null) return;
            foreach (var slot in _slots)
            {
                if (slot != null) slot.ClearCannon();
            }
        }
    }
}
