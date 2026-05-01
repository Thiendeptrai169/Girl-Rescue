using UnityEngine;

namespace DragonRescue.Entities.Dragon
{
    public class DragonEndpointVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _headRenderer;
        [SerializeField] private SpriteRenderer _tailRenderer;
        [SerializeField] private float _visualHeightMultiplier = 0.9f;

        private Vector3 _lastHeadPosition;
        private Vector3 _lastTailPosition;
        private bool _hasHeadPosition;
        private bool _hasTailPosition;

        public void Init(float spacing)
        {
            ResolveRenderers();

            float targetHeight = Mathf.Max(0.01f, spacing * _visualHeightMultiplier);
            FitRendererHeight(_headRenderer, targetHeight);
            FitRendererHeight(_tailRenderer, targetHeight);
        }

        public void SetHeadPosition(Vector3 position, Vector3 fallbackDirection)
        {
            SetEndpoint(_headRenderer, position, fallbackDirection, ref _lastHeadPosition, ref _hasHeadPosition);
        }

        public void SetTailPosition(Vector3 position, Vector3 fallbackDirection)
        {
            SetEndpoint(_tailRenderer, position, fallbackDirection, ref _lastTailPosition, ref _hasTailPosition);
        }

        private void ResolveRenderers()
        {
            if (_headRenderer == null)
            {
                Transform head = transform.Find("DragonHeadVisual");
                if (head != null)
                    _headRenderer = head.GetComponent<SpriteRenderer>();
            }

            if (_tailRenderer == null)
            {
                Transform tail = transform.Find("DragonTailVisual");
                if (tail != null)
                    _tailRenderer = tail.GetComponent<SpriteRenderer>();
            }
        }

        private void FitRendererHeight(SpriteRenderer renderer, float targetHeight)
        {
            if (renderer == null || renderer.sprite == null)
                return;

            float spriteHeight = renderer.sprite.bounds.size.y;
            if (spriteHeight <= 0f)
                return;

            float scale = targetHeight / spriteHeight;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void SetEndpoint(
            SpriteRenderer renderer,
            Vector3 position,
            Vector3 fallbackDirection,
            ref Vector3 lastPosition,
            ref bool hasPosition)
        {
            if (renderer == null)
                return;

            Transform target = renderer.transform;
            target.position = position;

            Vector3 direction = hasPosition ? position - lastPosition : fallbackDirection;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = fallbackDirection;

            if (direction.sqrMagnitude > 0.0001f)
            {
                float currentY = target.eulerAngles.y;
                float yRotation = direction.x > 0.001f ? 0f :
                    direction.x < -0.001f ? 180f :
                    currentY;

                target.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }

            lastPosition = position;
            hasPosition = true;
        }
    }
}
