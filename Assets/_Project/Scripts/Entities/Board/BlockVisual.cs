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
            FitToCells(payload.Size, payload.CellSize);
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
                _ => 0f
            };

            if (_arrowSprite != null)
                _arrowSprite.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void FitToCells(Vector2Int size, float cellSize)
        {
            Vector2 spriteSize = GetSpriteSize();
            if (spriteSize.x <= 0 || spriteSize.y <= 0)
                return;

            float targetWidth = cellSize * size.x;
            float targetHeight = cellSize * size.y;

            float scaleX = (targetWidth / spriteSize.x) * 0.95f;
            float scaleY = (targetHeight / spriteSize.y) * 0.95f;
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
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
