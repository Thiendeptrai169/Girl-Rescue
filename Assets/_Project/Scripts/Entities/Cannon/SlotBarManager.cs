using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Cannon
{
    public class SlotBarManager : MonoBehaviour
    {
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private float _slotSpacing = 1.2f;

        public static SlotBarManager Instance { get; private set; }

        private CannonSlot[] _slots;
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

            if (targetSlot != null)
            {
                DebugSystem.Log(DebugCategory.Cannon, $"TryLoadBlock accepted color={color} ammo={ammo} slot={targetSlot.Index}", targetSlot);
                targetSlot.LoadCannon(color, ammo);
                return true;
            }

            LogSlotFullWarning($"TryLoadBlock failed no empty unlocked slot for color={color} ammo={ammo}. slots={BuildDebugState()}");
            return false;
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
