using System.Collections;
using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Board
{
    public class BlockVisual : MonoBehaviour
    {
        [SerializeField] private BlockIdentity _identity;
        [SerializeField] private SpriteRenderer _blockSprite;
        [SerializeField] private SpriteRenderer _arrowSprite;
        [SerializeField] private float _blockedFeedbackDuration = 0.15f;
        [SerializeField] private Vector2 _singleHorizontalFill = new Vector2(0.95f, 0.68f);
        [SerializeField] private Vector2 _singleVerticalFill = new Vector2(0.68f, 0.95f);
        [SerializeField] private Vector2 _singleDiagonalFill = new Vector2(0.82f, 0.82f);

        private Coroutine _feedbackRoutine;
        private Color _originalColor;

        private void Awake()
        {
            if (_identity == null)
                _identity = GetComponentInParent<BlockIdentity>();
        }

        private void OnEnable()
        {
            GameEvents.OnBlockSpawned += OnBlockSpawned;
            GameEvents.OnBlockBlocked += OnBlockFeedbackRequested;
            GameEvents.OnBlockSlotFull += OnBlockFeedbackRequested;
        }

        private void OnDisable()
        {
            GameEvents.OnBlockSpawned -= OnBlockSpawned;
            GameEvents.OnBlockBlocked -= OnBlockFeedbackRequested;
            GameEvents.OnBlockSlotFull -= OnBlockFeedbackRequested;
            StopFeedback();
        }

        private Vector2 GetSpriteSize()
        {
            if (_blockSprite != null && _blockSprite.sprite != null)
                return _blockSprite.sprite.bounds.size;
            return Vector2.one;
        }

        private void OnBlockSpawned(BlockSpawnedPayload payload)
        {
            if (payload == null || payload.Block != _identity)
                return;

            Init(payload.Color, payload.Direction);
            FitToCells(payload.Size, payload.CellSize, payload.Direction);
        }

        private void Init(CannonColor color, Direction direction)
        {
            _originalColor = ColorPalette.GetColor(color);
            StopFeedback();

            if (_blockSprite != null)
                _blockSprite.color = _originalColor;

            // Rotate arrow based on direction
            float angle = direction switch
            {
                Direction.Up => 180f,
                Direction.Right => 90f,
                Direction.Down => 0f,
                Direction.Left => 270f,
                Direction.UpRight => 135f,
                Direction.UpLeft => 225f,
                Direction.DownRight => 45f,
                Direction.DownLeft => 315f,
                _ => 0f
            };

            if (_arrowSprite != null)
                _arrowSprite.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void FitToCells(Vector2Int size, float cellSize, Direction direction)
        {
            Vector2 spriteSize = GetSpriteSize();
            if (spriteSize.x <= 0 || spriteSize.y <= 0)
                return;

            Vector2 fill = GetVisualFill(size, direction);
            float targetWidth = cellSize * size.x * fill.x;
            float targetHeight = cellSize * size.y * fill.y;

            float scaleX = (targetWidth / spriteSize.x) * 0.95f;
            float scaleY = (targetHeight / spriteSize.y) * 0.95f;
            transform.localScale = new Vector3(scaleX, scaleY, 1f);

            FitArrowToBlock(scaleX, scaleY);
        }

        private Vector2 GetVisualFill(Vector2Int size, Direction direction)
        {
            if (size.x > 1 || size.y > 1)
                return new Vector2(0.95f, 0.95f);

            if (direction == Direction.Left || direction == Direction.Right)
                return _singleHorizontalFill;

            if (direction == Direction.Up || direction == Direction.Down)
                return _singleVerticalFill;

            return _singleDiagonalFill;
        }

        private void FitArrowToBlock(float blockScaleX, float blockScaleY)
        {
            if (_arrowSprite == null || blockScaleX <= 0f || blockScaleY <= 0f)
                return;

            float inverseCompensation = Mathf.Min(blockScaleX, blockScaleY);
            _arrowSprite.transform.localScale = new Vector3(
                inverseCompensation / blockScaleX,
                inverseCompensation / blockScaleY,
                1f);
        }

        private void OnBlockFeedbackRequested(BlockFeedbackPayload payload)
        {
            if (payload == null || payload.Block != _identity)
                return;

            PlayBlockedFeedback(payload.Duration);
        }

        private void PlayBlockedFeedback(float duration)
        {
            if (duration <= 0f)
                duration = _blockedFeedbackDuration;

            StopFeedback();
            _feedbackRoutine = StartCoroutine(BlockedFeedbackRoutine(duration));
        }

        public void StopFeedback()
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }

            if (_blockSprite != null)
                _blockSprite.color = _originalColor;
        }

        private IEnumerator BlockedFeedbackRoutine(float duration)
        {
            if (_blockSprite == null) yield break;

            _blockSprite.color = Color.red;
            yield return new WaitForSecondsRealtime(duration);
            _blockSprite.color = _originalColor;
            _feedbackRoutine = null;
        }
    }
}
