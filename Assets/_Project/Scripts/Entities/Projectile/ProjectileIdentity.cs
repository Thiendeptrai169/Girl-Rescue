using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Projectile
{
    public class ProjectileIdentity : MonoBehaviour
    {
        [SerializeField] private ProjectileVisual _visual;
        
        public CannonColor Color { get; private set; }
        public int Damage { get; private set; }
        public float Speed { get; private set; }
        public GameObject PrefabRef { get; private set; }
        
        public void Init(CannonColor color, int damage, float speed, GameObject prefabRef)
        {
            Color = color;
            Damage = damage;
            Speed = speed;
            PrefabRef = prefabRef;

            if (_visual != null) _visual.Init(color);
        }
    }
}
