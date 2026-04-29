using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// EC Visual component for a dragon segment.
    /// Handles sprite rendering only — no data, no logic.
    /// </summary>
    public class DragonSegmentVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;

        public void Init(CannonColor color)
        {
            if (_sprite == null) return;
            _sprite.color = ColorPalette.GetColor(color);
        }

        public void ResetVisual()
        {
            if (_sprite != null)
                _sprite.color = Color.white;
        }
    }
}
