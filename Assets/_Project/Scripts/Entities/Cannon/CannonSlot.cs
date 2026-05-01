using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using DragonRescue.Data;
using DragonRescue.Core;
using DragonRescue.Entities.Dragon;
using DragonRescue.Entities.Projectile;
using DragonRescue.UI;

namespace DragonRescue.Entities.Cannon
{
    public class CannonSlot : MonoBehaviour
    {
        [SerializeField] private CannonVisual _visual;
        [SerializeField] private Transform _firePoint; // Optional: specific spawn point
        [SerializeField] private CannonAmmoBadgeView _ammoBadge;

        public bool IsUnlocked { get; private set; }
        public bool IsLoaded { get; private set; }
        public CannonColor CurrentColor { get; private set; }
        public int RemainingAmmo => _remainingAmmo;

        private int _index;
        private int _remainingAmmo;
        private float _fireRate;
        private int _damage;
        private float _projSpeed;
        private float _fireRange;
        private GameObject _projectilePrefab;
        private CancellationTokenSource _fireCts;

        public void Init(int index, bool isUnlocked, LevelConfig config, GameObject projectilePrefab)
        {
            _index = index;
            IsUnlocked = isUnlocked;
            IsLoaded = false;
            _projectilePrefab = projectilePrefab;

            _fireRate = config.defaultFireRate;
            _damage = config.defaultDamage;
            _projSpeed = config.defaultProjectileSpeed;
            _fireRange = config.defaultFireRange;

            if (_ammoBadge == null)
                _ammoBadge = GetComponentInChildren<CannonAmmoBadgeView>(true);

            _visual.SetUnlockedState(isUnlocked);
            _ammoBadge?.Init(index, isUnlocked);
        }

        public void LoadCannon(CannonColor color, int ammo)
        {
            if (!IsUnlocked || IsLoaded || ammo <= 0) return;

            CurrentColor = color;
            _remainingAmmo = ammo;
            IsLoaded = true;

            _visual.SetLoadedState(color);
            RefreshAmmoBadge();
            GameEvents.FireCannonLoaded(new CannonLoadedPayload { Color = color, SlotIndex = _index });

            StartFiring();
        }

        public void ClearCannon()
        {
            StopFiring();
            IsLoaded = false;
            _remainingAmmo = 0;
            _visual.SetEmptyState();
            RefreshAmmoBadge();
        }

        public void Unlock()
        {
            if (IsUnlocked) return;
            IsUnlocked = true;
            _visual.SetUnlockedState(true);
            RefreshAmmoBadge();
        }

        private void StartFiring()
        {
            StopFiring();
            _fireCts = new CancellationTokenSource();
            FireLoopAsync(_fireCts.Token).Forget();
        }

        private void StopFiring()
        {
            if (_fireCts != null)
            {
                _fireCts.Cancel();
                _fireCts.Dispose();
                _fireCts = null;
            }
        }

        private void OnDestroy()
        {
            StopFiring();
        }

        private async UniTaskVoid FireLoopAsync(CancellationToken ct)
        {
            while (IsLoaded && _remainingAmmo > 0)
            {
                var target = FindTarget();

                if (target != null)
                {
                    if (!FireAt(target))
                    {
                        await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: ct);
                        continue;
                    }

                    _remainingAmmo--;
                    RefreshAmmoBadge();

                    if (_remainingAmmo <= 0)
                    {
                        GameEvents.FireCannonDepleted(new CannonDepletedPayload { SlotIndex = _index });
                        ClearCannon();
                        break;
                    }

                    // Wait for fire rate duration before being able to fire again
                    await UniTask.Delay(System.TimeSpan.FromSeconds(_fireRate), cancellationToken: ct);
                }
                else
                {
                    // No valid target in range, wait a short moment and check again
                    await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: ct);
                }
            }
        }

        private void RefreshAmmoBadge()
        {
            if (_ammoBadge != null)
                _ammoBadge.SetAmmo(_remainingAmmo, IsLoaded);
        }

        private DragonSegmentIdentity FindTarget()
        {
            if (DragonManager.Instance != null)
            {
                float currentRange = _fireRange;
                if (DragonRescue.Booster.BoosterManager.Instance != null)
                {
                    currentRange *= DragonRescue.Booster.BoosterManager.Instance.FireRangeMultiplier;
                }
                return DragonManager.Instance.FindTargetByColor(CurrentColor, _damage, transform.position, currentRange);
            }

            Debug.LogWarning($"[CannonSlot {_index}] Cannot fire {CurrentColor}: DragonManager.Instance is null.");
            return null;
        }

        private bool FireAt(DragonSegmentIdentity target)
        {
            // Reserve exactly this shot's damage before the projectile enters the air.
            target.AddIncomingDamage(_damage);

            if (_projectilePrefab != null)
            {
                var projGO = PoolManager.Instance.Get(_projectilePrefab);
                projGO.transform.position = _firePoint != null ? _firePoint.position : transform.position;
                
                var identity = projGO.GetComponent<ProjectileIdentity>();
                var movement = projGO.GetComponent<ProjectileMovement>();
                var hitResolver = projGO.GetComponent<ProjectileHitResolver>();

                if (identity == null)
                {
                    target.ReleaseIncomingDamage(_damage);
                    PoolManager.Instance.Release(_projectilePrefab, projGO);
                    return false;
                }

                identity.Init(CurrentColor, _damage, _projSpeed, _projectilePrefab);

                if (movement == null)
                {
                    target.ReleaseIncomingDamage(_damage);
                    PoolManager.Instance.Release(_projectilePrefab, projGO);
                    return false;
                }

                if (hitResolver == null)
                {
                    target.ReleaseIncomingDamage(_damage);
                    PoolManager.Instance.Release(_projectilePrefab, projGO);
                    return false;
                }

                hitResolver.SetReservedTarget(target);
                return true;
            }
            else
            {
                Debug.LogWarning($"[CannonSlot {_index}] Cannot fire {CurrentColor}: projectile prefab is missing.");
                target.ReleaseIncomingDamage(_damage);
                return false;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _fireRange > 0 ? _fireRange : 10f); // Default 10f if not initialized
        }
#endif
    }
}
