using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using DragonRescue.Data;
using DragonRescue.Core;
using DragonRescue.Entities.Dragon;
using DragonRescue.Entities.Projectile;

namespace DragonRescue.Entities.Cannon
{
    public class CannonSlot : MonoBehaviour
    {
        [SerializeField] private Transform _firePoint; // Optional: specific spawn point

        public bool IsUnlocked { get; private set; }
        public bool IsLoaded { get; private set; }
        public CannonColor CurrentColor { get; private set; }
        public int RemainingAmmo => _remainingAmmo;
        public int Index => _index;

        private int _index;
        private int _remainingAmmo;
        private float _fireRate;
        private int _damage;
        private float _projSpeed;
        private float _fireRange;
        private GameObject _projectilePrefab;
        private CancellationTokenSource _fireCts;
        private float _lastNoTargetLogTime = -999f;
        private const float NoTargetLogCooldown = 1f;

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

            FireStateChanged();
            FireAmmoChanged();
        }

        public void LoadCannon(CannonColor color, int ammo)
        {
            if (!IsUnlocked || IsLoaded || ammo <= 0)
            {
                DebugSystem.Log(DebugCategory.Cannon, $"Load rejected slot={_index} unlocked={IsUnlocked} loaded={IsLoaded} requestedColor={color} requestedAmmo={ammo}", this);
                return;
            }

            CurrentColor = color;
            _remainingAmmo = ammo;
            IsLoaded = true;
            DebugSystem.Log(DebugCategory.Cannon, $"Load slot={_index} color={color} ammo={ammo}", this);

            FireStateChanged();
            FireAmmoChanged();
            GameEvents.FireCannonLoaded(new CannonLoadedPayload { Color = color, SlotIndex = _index });

            StartFiring();
        }

        public void ClearCannon()
        {
            DebugSystem.Log(DebugCategory.Cannon, $"Clear slot={_index} wasLoaded={IsLoaded} color={CurrentColor} ammo={_remainingAmmo}", this);
            StopFiring();
            IsLoaded = false;
            _remainingAmmo = 0;
            FireStateChanged();
            FireAmmoChanged();
        }

        public void Unlock()
        {
            if (IsUnlocked) return;
            IsUnlocked = true;
            FireStateChanged();
            FireAmmoChanged();
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
                    FireAmmoChanged();

                    if (_remainingAmmo <= 0)
                    {
                        DebugSystem.Log(DebugCategory.Cannon, $"Slot {_index} depleted color={CurrentColor}", this);
                        ClearCannon();
                        GameEvents.FireCannonDepleted(new CannonDepletedPayload { SlotIndex = _index });
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

        private void FireStateChanged()
        {
            GameEvents.FireCannonSlotStateChanged(new CannonSlotStatePayload
            {
                SlotIndex = _index,
                IsUnlocked = IsUnlocked,
                IsLoaded = IsLoaded,
                Color = CurrentColor
            });
        }

        private void FireAmmoChanged()
        {
            GameEvents.FireCannonAmmoChanged(new CannonAmmoChangedPayload
            {
                SlotIndex = _index,
                Ammo = _remainingAmmo,
                IsLoaded = IsLoaded
            });
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
                DragonSegmentIdentity target = DragonManager.Instance.FindTargetByColor(CurrentColor, _damage, transform.position, currentRange);
                if (target == null)
                    LogNoTarget(currentRange);

                return target;
            }

            DebugSystem.Warning(DebugCategory.Cannon, $"Slot {_index} cannot fire {CurrentColor}: DragonManager.Instance is null.", this);
            return null;
        }

        private void LogNoTarget(float currentRange)
        {
            if (Time.unscaledTime - _lastNoTargetLogTime < NoTargetLogCooldown)
                return;

            _lastNoTargetLogTime = Time.unscaledTime;
            string targetSummary = DragonManager.Instance != null
                ? DragonManager.Instance.BuildTargetDebugSummary(CurrentColor, _damage, transform.position, currentRange)
                : "dragon=null";

            DebugSystem.Log(DebugCategory.Cannon, $"Slot {_index} no target ammo={_remainingAmmo} pos={transform.position} {targetSummary}", this);
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
                DebugSystem.Warning(DebugCategory.Cannon, $"Slot {_index} cannot fire {CurrentColor}: projectile prefab is missing.", this);
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
