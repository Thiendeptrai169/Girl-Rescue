using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DragonRescue.Data;

namespace DragonRescue.UI
{
    /// <summary>
    /// The tappable UI card representing a cannon in the tray.
    /// </summary>
    public class CannonCardView : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _ammoLabel;
        [SerializeField] private Button _button;

        private CannonDefinition _definition;
        private SlotBarView _slotBar;
        private bool _isUsed;

        public void Init(CannonDefinition definition, SlotBarView slotBar)
        {
            _definition = definition;
            _slotBar = slotBar;
            _isUsed = false;

            if (_ammoLabel != null)
                _ammoLabel.text = definition.Ammo.ToString();

            if (_iconImage != null)
            {
                // Placeholder: Use color enum. In reality, use definition.Icon
                _iconImage.color = GetColorFromEnum(definition.Color);
            }

            // Ensure button is active and hooked up
            _button.interactable = true;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnCardTapped);
        }

        private void OnCardTapped()
        {
            if (_isUsed) return;

            bool success = _slotBar.TryPlaceCannon(_definition);
            
            if (success)
            {
                _isUsed = true;
                _button.interactable = false;
                if (_iconImage != null) _iconImage.color = Color.gray; // Visual feedback
            }
            else
            {
                // TODO: DOTween shake for negative feedback
                Debug.Log("Cannot place cannon — slots are full.");
            }
        }

        private Color GetColorFromEnum(CannonColor cannonColor)
        {
            return cannonColor switch
            {
                CannonColor.Red => Color.red,
                CannonColor.Blue => Color.blue,
                CannonColor.Green => Color.green,
                CannonColor.Yellow => Color.yellow,
                CannonColor.Purple => new Color(0.5f, 0, 0.5f),
                _ => Color.white
            };
        }
    }
}
