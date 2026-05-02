using DragonRescue.Core;
using DragonRescue.Entities.Cannon;
using DG.Tweening;
using UnityEngine;

namespace DragonRescue.VFX
{
    public class CannonFurtherBuffVFX : MonoBehaviour
    {
        [SerializeField] private CannonSlot _slot;
        [SerializeField] private float _pulseScale = 0.14f;
        [SerializeField] private float _pulseInterval = 0.28f;

        private Sequence _activeSequence;
        private Vector3 _baseScale = Vector3.one;
        private float _buffEndTime;

        private void Awake()
        {
            if (_slot == null)
                _slot = GetComponentInParent<CannonSlot>();

            CaptureBaseState();
        }

        private void OnEnable()
        {
            GameEvents.OnFurtherBuffStarted += OnFurtherBuffStarted;
            GameEvents.OnCannonSlotStateChanged += OnCannonSlotStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnFurtherBuffStarted -= OnFurtherBuffStarted;
            GameEvents.OnCannonSlotStateChanged -= OnCannonSlotStateChanged;
            StopBuffVFX();
        }

        private void OnFurtherBuffStarted(FurtherBuffPayload payload)
        {
            if (payload == null || payload.Duration <= 0f)
                return;

            _buffEndTime = payload.EndTime;

            if (_slot != null && _slot.IsLoaded)
                PlayBuffVFX(payload.Duration);
        }

        private void OnCannonSlotStateChanged(CannonSlotStatePayload payload)
        {
            if (_slot == null || payload == null || payload.SlotIndex != _slot.Index)
                return;

            if (!payload.IsLoaded)
            {
                StopBuffVFX();
                return;
            }

            float remaining = _buffEndTime - Time.time;
            if (remaining > 0f)
                PlayBuffVFX(remaining);
        }

        private void PlayBuffVFX(float duration)
        {
            StopBuffVFX();
            CaptureBaseState();

            int loopCount = Mathf.Max(1, Mathf.CeilToInt(duration / _pulseInterval));

            _activeSequence = DOTween.Sequence();
            _activeSequence.SetTarget(this);
            _activeSequence.SetUpdate(false);

            _activeSequence.Join(transform.DOPunchScale(Vector3.one * _pulseScale, _pulseInterval, 6, 0.7f).SetLoops(loopCount));
            _activeSequence.OnComplete(() =>
            {
                _activeSequence = null;
                RestoreBaseState();
            });
        }

        private void CaptureBaseState()
        {
            _baseScale = transform.localScale;
        }

        private void StopBuffVFX()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
                _activeSequence.Kill(false);

            _activeSequence = null;
            RestoreBaseState();
        }

        private void RestoreBaseState()
        {
            transform.localScale = _baseScale;
        }
    }
}
