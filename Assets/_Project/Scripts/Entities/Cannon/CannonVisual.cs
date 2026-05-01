using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Cannon
{
    public class CannonVisual : MonoBehaviour
    {
        [SerializeField] private CannonSlot _slot;
        [SerializeField] private SpriteRenderer _slotBackground;
        [SerializeField] private SpriteRenderer _cannonSprite;
        
        [Header("Colors")]
        [SerializeField] private Color _lockedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _emptyUnlockedColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        private void Awake()
        {
            if (_slot == null)
                _slot = GetComponentInParent<CannonSlot>();
        }

        private void OnEnable()
        {
            GameEvents.OnCannonSlotStateChanged += OnCannonSlotStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnCannonSlotStateChanged -= OnCannonSlotStateChanged;
        }

        private void OnCannonSlotStateChanged(CannonSlotStatePayload payload)
        {
            if (_slot == null || payload == null || payload.SlotIndex != _slot.Index)
                return;

            SetUnlockedState(payload.IsUnlocked);

            if (payload.IsLoaded)
                SetLoadedState(payload.Color);
            else
                SetEmptyState();
        }

        private void SetUnlockedState(bool isUnlocked)
        {
            if (_slotBackground != null)
                _slotBackground.color = isUnlocked ? _emptyUnlockedColor : _lockedColor;
            
            SetEmptyState();
        }

        private void SetLoadedState(CannonColor color)
        {
            if (_cannonSprite != null)
            {
                _cannonSprite.gameObject.SetActive(true);
                _cannonSprite.color = ColorPalette.GetColor(color);
            }
        }

        private void SetEmptyState()
        {
            if (_cannonSprite != null)
            {
                _cannonSprite.gameObject.SetActive(false);
            }
        }
    }
}
