using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Cannon
{
    /// <summary>
    /// Handles the visual representation of the cannon entity.
    /// Currently just sets the sprite color based on the definition.
    /// </summary>
    public class CannonVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _cannonSprite;

        public void Init(CannonDefinition definition)
        {
            if (_cannonSprite == null) return;

            // Placeholder: Set color based on Enum.
            // In a real project, the Definition would hold a Sprite reference directly.
            _cannonSprite.color = GetColorFromEnum(definition.Color);
        }

        public void ResetVisual()
        {
            if (_cannonSprite != null)
                _cannonSprite.color = Color.white;
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
