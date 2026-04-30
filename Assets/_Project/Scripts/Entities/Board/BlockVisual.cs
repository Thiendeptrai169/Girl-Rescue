using System.Collections;
using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Board
{
    public class BlockVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _blockSprite;
        [SerializeField] private SpriteRenderer _arrowSprite;

        private Coroutine _feedbackRoutine;
        private Color _originalColor;

        public Vector2 GetSpriteSize()
        {
            if (_blockSprite != null && _blockSprite.sprite != null)
                return _blockSprite.sprite.bounds.size;
            return Vector2.one;
        }

        public void Init(CannonColor color, Direction direction)
        {
            _originalColor = ColorPalette.GetColor(color);
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
            _arrowSprite.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        public void PlayBlockedFeedback()
        {
            if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = StartCoroutine(BlockedFeedbackRoutine());
        }

        private IEnumerator BlockedFeedbackRoutine()
        {
            // Flash red
            _blockSprite.color = Color.red;
            yield return new WaitForSeconds(0.15f);
            _blockSprite.color = _originalColor;
            _feedbackRoutine = null;
        }
    }
}
