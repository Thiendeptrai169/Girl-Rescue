using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace DragonRescue.Animation
{
    public class BlockMoveAnimator : MonoBehaviour
    {
        [SerializeField] private float _escapeBoardCellsPerSecond = 5.5f;
        [SerializeField] private float _escapeToSlotSpeed = 7f;
        [SerializeField] private float _blockedCellsPerSecond = 6.5f;
        [SerializeField] private float _returnCellsPerSecond = 10f;
        [SerializeField] private float _minimumSegmentDuration = 0.08f;
        [SerializeField] private float _slotArrivalScale = 0.35f;

        private Sequence _activeSequence;
        private Vector3 _baseScale;
        private bool _hasBaseScale;

        private void OnDisable()
        {
            KillActiveTween();

            if (_hasBaseScale)
                transform.localScale = _baseScale;
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

            Vector3[] path = BuildPath(boardPath, slotPosition);
            if (path.Length > 0)
            {
                float boardDistance = CalculateDistance(transform.position, boardPath);
                Vector3 lastBoardPosition = boardPath != null && boardPath.Count > 0
                    ? boardPath[boardPath.Count - 1]
                    : transform.position;
                float slotDistance = Vector3.Distance(lastBoardPosition, slotPosition);
                float duration = CalculateDuration(boardDistance, cellSize, _escapeBoardCellsPerSecond) +
                                 CalculateDuration(slotDistance, cellSize, _escapeToSlotSpeed);

                sequence.Append(transform.DOPath(path, duration, PathType.Linear, PathMode.Full3D)
                    .SetEase(Ease.InOutSine));
            }

            sequence.Join(transform.DOScale(_baseScale * _slotArrivalScale, sequence.Duration())
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
                transform.localScale = _baseScale;
        }

        private void CacheBaseScale()
        {
            if (_hasBaseScale)
                return;

            _baseScale = transform.localScale;
            _hasBaseScale = true;
        }

        private Vector3[] BuildPath(IReadOnlyList<Vector3> boardPath, Vector3 slotPosition)
        {
            int boardCount = boardPath != null ? boardPath.Count : 0;
            var points = new List<Vector3>(boardCount + 1);

            for (int i = 0; i < boardCount; i++)
            {
                points.Add(WithCurrentZ(boardPath[i]));
            }

            points.Add(WithCurrentZ(slotPosition));
            return points.ToArray();
        }

        private float CalculateDistance(Vector3 startPosition, IReadOnlyList<Vector3> path)
        {
            if (path == null || path.Count == 0)
                return 0f;

            float distance = 0f;
            Vector3 previous = startPosition;
            for (int i = 0; i < path.Count; i++)
            {
                distance += Vector3.Distance(previous, path[i]);
                previous = path[i];
            }

            return distance;
        }

        private float CalculateDuration(float distance, float cellSize, float cellsPerSecond)
        {
            if (cellSize <= 0f || cellsPerSecond <= 0f)
                return _minimumSegmentDuration;

            return Mathf.Max(_minimumSegmentDuration, distance / (cellSize * cellsPerSecond));
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
