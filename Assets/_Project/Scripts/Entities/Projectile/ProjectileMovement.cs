using UnityEngine;
using System;

namespace DragonRescue.Entities.Projectile
{
    [RequireComponent(typeof(ProjectileIdentity))]
    public class ProjectileMovement : MonoBehaviour
    {
        [SerializeField] private float _hitRadius = 0.1f;

        private ProjectileIdentity _identity;
        private Transform _target;
        private bool _isMoving;

        public event Action Arrived;
        public event Action TargetLost;

        private void Awake()
        {
            _identity = GetComponent<ProjectileIdentity>();
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            _isMoving = _target != null;
        }

        public void Stop()
        {
            _target = null;
            _isMoving = false;
        }

        private void Update()
        {
            if (!_isMoving) return;

            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                Stop();
                TargetLost?.Invoke();
                return;
            }

            Vector3 targetPos = _target.position;
            Vector3 direction = targetPos - transform.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                _identity.Speed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPos) <= _hitRadius)
            {
                Stop();
                Arrived?.Invoke();
            }
        }
    }
}
