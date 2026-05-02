using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;
using System.Collections.Generic;

namespace DragonRescue.Entities.Cannon
{
    public class SlotBarManager : MonoBehaviour
    {
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private float _slotSpacing = 1.2f;

        public static SlotBarManager Instance { get; private set; }

        private CannonSlot[] _slots;
        private readonly HashSet<int> _reservedSlotIndexes = new();
        private float _lastSlotFullWarningTime = -999f;
        private const float SlotFullWarningCooldown = 1f;

        private void Awake()
        {
            Instance = this;
        }

        public void Init(LevelConfig config, WorldLayout layout, GameObject projectilePrefab)
        {
            // Position slot bar using viewport (e.g., center-bottom of play area)
            // Can be adjusted later if it overlaps HUD
            transform.position = layout.ViewportToWorld(new Vector2(0.5f, 0.5f));

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
            GameEvents.RequestSlotCapacity += CanAcceptBlock;
        }

        private void OnDestroy()
        {
            GameEvents.OnBlockEscaped -= OnBlockEscaped;
            GameEvents.RequestSlotCapacity -= CanAcceptBlock;

            if (Instance == this)
                Instance = null;
        }

        private void OnBlockEscaped(BlockEscapedPayload payload)
        {
            if (payload.Ammo <= 0) return;

            if (!TryLoadBlock(payload.Color, payload.Ammo))
                LogSlotFullWarning("No empty unlocked cannon slot available.");
        }

        public bool TryLoadBlock(CannonColor color, int ammo)
        {
            if (ammo <= 0) return true;

            CannonSlot targetSlot = FindEmptyUnlockedSlot();
            return TryLoadBlockIntoSlot(targetSlot, color, ammo);
        }

        public bool TryGetAvailableSlot(int ammo, out CannonSlot targetSlot)
        {
            targetSlot = ammo <= 0 ? null : FindEmptyUnlockedSlot();
            return ammo <= 0 || targetSlot != null;
        }

        public bool TryLoadBlockIntoSlot(CannonSlot targetSlot, CannonColor color, int ammo)
        {
            if (ammo <= 0) return true;

            if (targetSlot != null)
            {
                DebugSystem.Log(DebugCategory.Cannon, $"TryLoadBlock accepted color={color} ammo={ammo} slot={targetSlot.Index}", targetSlot);
                targetSlot.LoadCannon(color, ammo);
                return true;
            }

            LogSlotFullWarning($"TryLoadBlock failed no empty unlocked slot for color={color} ammo={ammo}. slots={BuildDebugState()}");
            return false;
        }

        public bool TryReserveAvailableSlot(int ammo, out CannonSlot targetSlot)
        {
            targetSlot = null;
            if (ammo <= 0) return true;

            targetSlot = FindEmptyUnlockedSlot();
            if (targetSlot == null)
                return false;

            _reservedSlotIndexes.Add(targetSlot.Index);
            return true;
        }

        public void ReleaseSlotReservation(CannonSlot slot)
        {
            if (slot == null)
                return;

            _reservedSlotIndexes.Remove(slot.Index);
        }

        public bool TryLoadReservedBlockIntoSlot(CannonSlot targetSlot, CannonColor color, int ammo)
        {
            if (ammo <= 0) return true;
            if (targetSlot == null) return false;

            _reservedSlotIndexes.Remove(targetSlot.Index);
            return TryLoadBlockIntoSlot(targetSlot, color, ammo);
        }

        public bool CanAcceptBlock(int ammo)
        {
            return ammo <= 0 || FindEmptyUnlockedSlot() != null;
        }

        public List<CannonColor> GetLoadedColorsInSlotOrder()
        {
            var colors = new List<CannonColor>();
            if (_slots == null) return colors;

            for (int i = 0; i < _slots.Length; i++)
            {
                CannonSlot slot = _slots[i];
                if (slot != null && slot.IsUnlocked && slot.IsLoaded)
                    colors.Add(slot.CurrentColor);
            }

            return colors;
        }

        public bool HasLoadedCannon()
        {
            if (_slots == null) return false;

            for (int i = 0; i < _slots.Length; i++)
            {
                CannonSlot slot = _slots[i];
                if (slot != null && slot.IsUnlocked && slot.IsLoaded)
                    return true;
            }

            return false;
        }

        private CannonSlot FindEmptyUnlockedSlot()
        {
            if (_slots == null) return null;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null &&
                    _slots[i].IsUnlocked &&
                    !_slots[i].IsLoaded &&
                    !_reservedSlotIndexes.Contains(_slots[i].Index))
                {
                    return _slots[i];
                }
            }

            return null;
        }

        public string BuildDebugState()
        {
            if (_slots == null)
                return "slots=null";

            string result = "";
            for (int i = 0; i < _slots.Length; i++)
            {
                CannonSlot slot = _slots[i];
                if (result.Length > 0)
                    result += " | ";

                if (slot == null)
                {
                    result += $"{i}:null";
                    continue;
                }

                result += $"{i}:unlocked={slot.IsUnlocked},loaded={slot.IsLoaded},color={slot.CurrentColor},ammo={slot.RemainingAmmo}";
            }

            return result;
        }

        public void ClearAllSlots()
        {
            if (_slots == null) return;
            foreach (var slot in _slots)
            {
                if (slot != null) slot.ClearCannon();
            }
        }

        public bool UnlockNextSlot()
        {
            if (_slots == null) return false;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && !_slots[i].IsUnlocked)
                {
                    _slots[i].Unlock();
                    return true;
                }
            }
            return false;
        }

        private void LogSlotFullWarning(string message)
        {
            if (Time.unscaledTime - _lastSlotFullWarningTime < SlotFullWarningCooldown)
                return;

            _lastSlotFullWarningTime = Time.unscaledTime;
            DebugSystem.Warning(DebugCategory.Cannon, message, this);
        }
    }
}
