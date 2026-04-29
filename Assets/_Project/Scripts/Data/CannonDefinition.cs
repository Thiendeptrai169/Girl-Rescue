using UnityEngine;

namespace DragonRescue.Data
{
    /// <summary>
    /// Blueprint for a cannon type. Stored as a reusable SO asset (Flyweight Pattern).
    /// Multiple levels can reference the same Cannon_Red.asset — change stats once, all levels update.
    /// </summary>
    [CreateAssetMenu(fileName = "Cannon_New", menuName = "DragonRescue/Cannon Definition")]
    public class CannonDefinition : ScriptableObject
    {
        [SerializeField] private CannonColor _color;
        [SerializeField] private int _ammo = 10;
        [SerializeField] private float _fireRate = 1f;   // shots per second
        [SerializeField] private float _range = 5f;      // units
        [SerializeField] private int _damage = 1;
        [SerializeField] private float _projectileSpeed = 8f;

        public CannonColor Color => _color;
        public int Ammo => _ammo;
        public float FireRate => _fireRate;
        public float Range => _range;
        public int Damage => _damage;
        public float ProjectileSpeed => _projectileSpeed;
    }
}
