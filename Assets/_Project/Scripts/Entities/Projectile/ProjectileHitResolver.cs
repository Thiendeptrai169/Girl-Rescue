using UnityEngine;
using DragonRescue.Core;
using DragonRescue.Entities.Dragon;
using DragonRescue.SFX;

namespace DragonRescue.Entities.Projectile
{
    [RequireComponent(typeof(ProjectileIdentity))]
    [RequireComponent(typeof(ProjectileMovement))]
    public class ProjectileHitResolver : MonoBehaviour
    {
        private ProjectileIdentity _identity;
        private ProjectileMovement _movement;
        private DragonSegmentIdentity _reservedTarget;
        private bool _isResolved = true;

        private void Awake()
        {
            _identity = GetComponent<ProjectileIdentity>();
            _movement = GetComponent<ProjectileMovement>();

            _movement.Arrived += HitTarget;
            _movement.TargetLost += CancelProjectile;
        }

        public void SetReservedTarget(DragonSegmentIdentity target)
        {
            _reservedTarget = target;
            _isResolved = false;
            _movement.SetTarget(target != null ? target.transform : null);
        }

        private void HitTarget()
        {
            if (_isResolved) return;
            _isResolved = true;

            if (_reservedTarget != null)
            {
                _reservedTarget.ReleaseIncomingDamage(_identity.Damage);

                if (_reservedTarget.IsAlive)
                {
                    _reservedTarget.TakeDamage(_identity.Damage);
                    SoundManager.PlayProjectileHitTarget();

                    GameEvents.FireProjectileHit(new ProjectileHitPayload
                    {
                        Color = _identity.Color,
                        Damage = _identity.Damage,
                        HitPosition = transform.position
                    });
                }
            }

            _reservedTarget = null;
            ReturnToPool();
        }

        public void CancelProjectile()
        {
            if (_isResolved) return;
            _isResolved = true;

            if (_reservedTarget != null)
            {
                _reservedTarget.ReleaseIncomingDamage(_identity.Damage);
                _reservedTarget = null;
            }

            _movement.Stop();
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (_identity.PrefabRef != null)
            {
                PoolManager.Instance.Release(_identity.PrefabRef, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            if (!_isResolved && _reservedTarget != null)
            {
                CancelProjectile();
            }
        }

        private void OnDestroy()
        {
            if (_movement == null) return;

            _movement.Arrived -= HitTarget;
            _movement.TargetLost -= CancelProjectile;
        }
    }
}
