using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using DragonRescue.Entities.Board;

namespace DragonRescue.Animation
{
    public class BlockMoveAnimator : MonoBehaviour
    {
        [SerializeField] private float _escapeBoardCellsPerSecond = 7.5f;
        [SerializeField] private float _escapeToSlotSpeed = 9.5f;
        [SerializeField] private float _blockedCellsPerSecond = 8.5f;
        [SerializeField] private float _returnCellsPerSecond = 13f;
        [SerializeField] private float _minimumSegmentDuration = 0.06f;
        [SerializeField] private float _slotArrivalScale = 0.35f;
        [SerializeField] private float _rotationDuration = 0.08f;

        private Sequence _activeSequence;
        private Vector3 _baseScale;
        private Quaternion _baseLocalRotation;
        private bool _hasBaseScale;
        private BlockVisual _blockVisual;
        private BlockIdentity _identity;

        private void OnDisable()
        {
            KillActiveTween();

            if (_hasBaseScale)
            {
                transform.localScale = _baseScale;
                transform.localRotation = _baseLocalRotation;
                RestoreArrowDirection();
            }
        }

        public async UniTask PlayEscapeToSlotAsync(
            IReadOnlyList<Vector3> boardPath,
            Vector3 slotPosition,
            float cellSize,
            CancellationToken cancellationToken)
        {
            CacheBaseScale();
            KillActiveTween();

            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(false);

            AppendMovementPath(sequence, boardPath, slotPosition, cellSize);

            float totalDuration = sequence.Duration();
            sequence.Insert(0f, transform.DOScale(_baseScale * _slotArrivalScale, totalDuration)
                .SetEase(Ease.InQuad));

            _activeSequence = sequence;
            await AwaitSequenceAsync(sequence, cancellationToken);
            _activeSequence = null;
        }

        public async UniTask PlayBlockedReturnAsync(
            Vector3 impactPosition,
            Vector3 originalPosition,
            float cellSize,
            CancellationToken cancellationToken)
        {
            CacheBaseScale();
            KillActiveTween();

            float hitDuration = CalculateDuration(Vector3.Distance(transform.position, impactPosition), cellSize, _blockedCellsPerSecond);
            float returnDuration = CalculateDuration(Vector3.Distance(impactPosition, originalPosition), cellSize, _returnCellsPerSecond);

            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(false);
            sequence.Append(transform.DOMove(impactPosition, hitDuration).SetEase(Ease.OutSine));
            sequence.Append(transform.DOMove(originalPosition, returnDuration).SetEase(Ease.OutBack, 1.25f));

            _activeSequence = sequence;
            await AwaitSequenceAsync(sequence, cancellationToken);
            _activeSequence = null;
        }

        public void ResetVisualState()
        {
            KillActiveTween();

            if (_hasBaseScale)
            {
                transform.localScale = _baseScale;
                transform.localRotation = _baseLocalRotation;
                RestoreArrowDirection();
            }
        }

        private void CacheBaseScale()
        {
            if (_hasBaseScale)
                return;

            _baseScale = transform.localScale;
            _baseLocalRotation = transform.localRotation;
            _hasBaseScale = true;
        }

        private void AppendMovementPath(Sequence sequence, IReadOnlyList<Vector3> boardPath, Vector3 slotPosition, float cellSize)
        {
            int boardCount = boardPath != null ? boardPath.Count : 0;
            Vector3 previousPosition = transform.position;

            for (int i = 0; i < boardCount; i++)
            {
                Vector3 target = WithCurrentZ(boardPath[i]);
                float duration = CalculateDuration(Vector3.Distance(previousPosition, target), cellSize, _escapeBoardCellsPerSecond);
                AppendMoveWithFacing(sequence, previousPosition, target, duration, Ease.InOutSine);
                previousPosition = target;
            }

            Vector3 slotTarget = WithCurrentZ(slotPosition);
            float slotDuration = CalculateDuration(Vector3.Distance(previousPosition, slotTarget), cellSize, _escapeToSlotSpeed);
            AppendMoveWithFacing(sequence, previousPosition, slotTarget, slotDuration, Ease.InOutSine);
        }

        private void AppendMoveWithFacing(
            Sequence sequence,
            Vector3 startPosition,
            Vector3 targetPosition,
            float moveDuration,
            Ease moveEase)
        {
            Tweener moveTween = transform.DOMove(targetPosition, moveDuration).SetEase(moveEase);
            float facingAngle = CalculateFacingAngle(targetPosition - startPosition);

            if (float.IsNaN(facingAngle))
            {
                sequence.Append(moveTween);
                return;
            }

            float rotateDuration = Mathf.Min(Mathf.Max(0f, _rotationDuration), moveDuration);

            sequence.AppendCallback(() => MatchArrowToWorldAngle(facingAngle));
            sequence.Append(moveTween);

            if (ShouldRotateBody())
            {
                Tweener rotateTween = transform.DOLocalRotate(
                    new Vector3(_baseLocalRotation.eulerAngles.x, _baseLocalRotation.eulerAngles.y, facingAngle),
                    rotateDuration)
                    .SetEase(Ease.OutSine)
                    .OnUpdate(() => MatchArrowToWorldAngle(facingAngle));

                sequence.Join(rotateTween);
            }
        }

        private float CalculateDuration(float distance, float cellSize, float cellsPerSecond)
        {
            if (cellSize <= 0f || cellsPerSecond <= 0f)
                return _minimumSegmentDuration;

            return Mathf.Max(_minimumSegmentDuration, distance / (cellSize * cellsPerSecond));
        }

        private float CalculateFacingAngle(Vector3 movement)
        {
            movement.z = 0f;
            if (movement.sqrMagnitude <= 0.0001f)
                return float.NaN;

            float angle = Mathf.Atan2(movement.x, -movement.y) * Mathf.Rad2Deg;
            return Mathf.Repeat(angle, 360f);
        }

        private void MatchArrowToWorldAngle(float worldAngle)
        {
            if (_blockVisual == null)
                _blockVisual = GetComponent<BlockVisual>();

            if (_blockVisual != null)
                _blockVisual.MatchArrowToWorldAngle(worldAngle);
        }

        private bool ShouldRotateBody()
        {
            if (_identity == null)
                _identity = GetComponent<BlockIdentity>();

            return _identity == null ||
                   _identity.Size.x == _identity.Size.y;
        }

        private void RestoreArrowDirection()
        {
            if (_blockVisual == null)
                _blockVisual = GetComponent<BlockVisual>();

            if (_blockVisual != null)
                _blockVisual.RestoreArrowDirection();
        }

        private Vector3 WithCurrentZ(Vector3 position)
        {
            position.z = transform.position.z;
            return position;
        }

        private async UniTask AwaitSequenceAsync(Sequence sequence, CancellationToken cancellationToken)
        {
            var completion = new UniTaskCompletionSource();
            bool completed = false;
            CancellationTokenRegistration cancellationRegistration = default;

            try
            {
                sequence.OnComplete(() =>
                {
                    completed = true;
                    completion.TrySetResult();
                });

                sequence.OnKill(() =>
                {
                    if (!completed && !cancellationToken.IsCancellationRequested)
                        completion.TrySetResult();
                });

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(() =>
                    {
                        if (sequence != null && sequence.IsActive())
                            sequence.Kill(false);

                        completion.TrySetCanceled(cancellationToken);
                    });
                }

                await completion.Task;
            }
            catch (OperationCanceledException)
            {
                if (sequence != null && sequence.IsActive())
                    sequence.Kill(false);

                throw;
            }
            finally
            {
                cancellationRegistration.Dispose();
            }
        }

        private void KillActiveTween()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
                _activeSequence.Kill(false);

            _activeSequence = null;
        }
    }
}
