using System;
using DG.Tweening;
using UnityEngine;

namespace DragonRescue.VFX
{
    public class DragonSegmentHitVFX : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private float _hitDuration = 0.16f;
        [SerializeField] private float _destroyDuration = 0.24f;
        [SerializeField] private float _hitPunchScale = 0.18f;
        [SerializeField] private float _destroyPunchScale = 0.35f;
        [SerializeField] private float _shakeRotationDegrees = 8f;
        [SerializeField] private Color _hitFlashColor = Color.white;

        private Sequence _activeSequence;
        private Color _baseColor = Color.white;
        private Vector3 _baseLocalScale;
        private Quaternion _baseLocalRotation;
        private bool _hasCachedTransform;

        public void CaptureBaseState(Color baseColor)
        {
            CacheTransform();
            _baseColor = baseColor;
            ResetVisualState();
        }

        public void PlayHit(bool destroyed, Action onComplete = null)
        {
            CacheTransform();
            KillActiveSequence();

            if (_sprite == null)
            {
                onComplete?.Invoke();
                return;
            }

            ResetVisualState();

            _activeSequence = destroyed
                ? BuildDestroySequence(onComplete)
                : BuildHitSequence(onComplete);
        }

        public void ResetVisualState()
        {
            if (_sprite != null)
                _sprite.color = _baseColor;

            if (!_hasCachedTransform)
                return;

            transform.localScale = _baseLocalScale;
            transform.localRotation = _baseLocalRotation;
        }

        private Sequence BuildHitSequence(Action onComplete)
        {
            Sequence sequence = DOTween.Sequence();
            sequence.SetTarget(this);
            sequence.Join(transform.DOPunchScale(Vector3.one * _hitPunchScale, _hitDuration, 8, 0.6f));
            sequence.Join(transform.DOPunchRotation(Vector3.forward * _shakeRotationDegrees, _hitDuration, 10, 0.7f));
            sequence.Join(_sprite.DOColor(_hitFlashColor, _hitDuration * 0.35f).SetLoops(2, LoopType.Yoyo));
            sequence.OnComplete(() =>
            {
                ResetVisualState();
                onComplete?.Invoke();
            });

            return sequence;
        }

        private Sequence BuildDestroySequence(Action onComplete)
        {
            Sequence sequence = DOTween.Sequence();
            sequence.SetTarget(this);
            sequence.Append(_sprite.DOColor(_hitFlashColor, _destroyDuration * 0.25f));
            sequence.Join(transform.DOPunchScale(Vector3.one * _destroyPunchScale, _destroyDuration, 10, 0.85f));
            sequence.Join(transform.DOPunchRotation(Vector3.forward * (_shakeRotationDegrees * 1.5f), _destroyDuration, 14, 0.8f));
            sequence.Append(_sprite.DOFade(0f, _destroyDuration * 0.45f));
            sequence.Join(transform.DOScale(_baseLocalScale * 0.15f, _destroyDuration * 0.45f).SetEase(Ease.InBack));
            sequence.OnComplete(() =>
            {
                onComplete?.Invoke();
            });

            return sequence;
        }

        private void CacheTransform()
        {
            if (_hasCachedTransform)
                return;

            _baseLocalScale = transform.localScale;
            _baseLocalRotation = transform.localRotation;
            _hasCachedTransform = true;
        }

        private void KillActiveSequence()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
                _activeSequence.Kill(false);

            _activeSequence = null;
        }

        private void OnDisable()
        {
            KillActiveSequence();
        }
    }
}
