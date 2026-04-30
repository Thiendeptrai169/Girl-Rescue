using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Projectile
{
    public class ProjectileVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;

        public void Init(CannonColor color)
        {
            if (_sprite != null)
                _sprite.color = ColorPalette.GetColor(color);
        }
    }
}
