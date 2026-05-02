using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// Abstract base class for Dragon movement.
    /// Allows swapping between Linear, Curved, Spline, or Jump movement 
    /// without modifying the DragonManager.
    /// </summary>
    public abstract class DragonMovementBase : MonoBehaviour
    {
        public float Progress { get; protected set; }

        private float _recoilProgress;
        private float _recoilPauseSeconds;
        private float _recoilPauseTimer;

        public abstract void Init(LevelConfig config, WorldLayout worldLayout, DragonSegmentIdentity[] segments, float spacing);
        public abstract void StopMoving();
        public abstract void ResumeMoving();

        protected virtual void OnEnable()
        {
            GameEvents.OnDragonSegmentsSorted += OnDragonSegmentsSorted;
        }

        protected virtual void OnDisable()
        {
            GameEvents.OnDragonSegmentsSorted -= OnDragonSegmentsSorted;
        }

        public virtual void SetSegmentOrder(DragonSegmentIdentity[] segments)
        {
        }

        public virtual void RefreshVisuals()
        {
        }

        public virtual void ApplyRecoil()
        {
            if (_recoilProgress <= 0f && _recoilPauseSeconds <= 0f) return;

            Progress = Mathf.Clamp01(Progress - _recoilProgress);
            _recoilPauseTimer = Mathf.Max(_recoilPauseTimer, _recoilPauseSeconds);
        }

        protected void ConfigureRecoil(LevelConfig config)
        {
            _recoilProgress = config != null ? Mathf.Max(0f, config.dragonRecoilProgress) : 0f;
            _recoilPauseSeconds = config != null ? Mathf.Max(0f, config.dragonRecoilPauseSeconds) : 0f;
            _recoilPauseTimer = 0f;
        }

        protected bool IsRecoilPausing()
        {
            if (_recoilPauseTimer <= 0f) return false;

            _recoilPauseTimer -= Time.deltaTime;
            return true;
        }

        private void OnDragonSegmentsSorted(DragonSegmentsSortedPayload payload)
        {
            if (payload == null || payload.Manager == null || payload.OrderedSegments == null)
                return;

            if (payload.Manager != GetComponent<DragonManager>())
                return;

            SetSegmentOrder(payload.OrderedSegments);
        }
    }
}
