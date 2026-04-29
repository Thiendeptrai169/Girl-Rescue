using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// Handles the visual representation of a dragon segment.
    /// </summary>
    public class SegmentVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _segmentSprite;

        public void Init(CannonColor color)
        {
            if (_segmentSprite == null) return;

            // Placeholder: Set color based on Enum.
            _segmentSprite.color = GetColorFromEnum(color);
        }

        public void ResetVisual()
        {
            if (_segmentSprite != null)
                _segmentSprite.color = Color.white;
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
