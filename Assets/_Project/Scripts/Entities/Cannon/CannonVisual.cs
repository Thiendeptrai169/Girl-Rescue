using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Cannon
{
    public class CannonVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _slotBackground;
        [SerializeField] private SpriteRenderer _cannonSprite;
        
        [Header("Colors")]
        [SerializeField] private Color _lockedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _emptyUnlockedColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        public void SetUnlockedState(bool isUnlocked)
        {
            if (_slotBackground != null)
                _slotBackground.color = isUnlocked ? _emptyUnlockedColor : _lockedColor;
            
            SetEmptyState();
        }

        public void SetLoadedState(CannonColor color)
        {
            if (_cannonSprite != null)
            {
                _cannonSprite.gameObject.SetActive(true);
                _cannonSprite.color = ColorPalette.GetColor(color);
            }
        }

        public void SetEmptyState()
        {
            if (_cannonSprite != null)
            {
                _cannonSprite.gameObject.SetActive(false);
            }
        }
    }
}
