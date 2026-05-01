using System;
using DragonRescue.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DragonRescue.Entities.Board
{
    public readonly struct BoardInputSnapshot
    {
        public readonly string Source;
        public readonly Vector2 ScreenPosition;
        public readonly bool IsPressed;
        public readonly bool PressedThisFrame;
        public readonly bool IsOverUI;

        public BoardInputSnapshot(string source, Vector2 screenPosition, bool isPressed, bool pressedThisFrame, bool isOverUI)
        {
            Source = source;
            ScreenPosition = screenPosition;
            IsPressed = isPressed;
            PressedThisFrame = pressedThisFrame;
            IsOverUI = isOverUI;
        }
    }

    public class BoardInputReceiver : MonoBehaviour
    {
        public event Action<BoardInputSnapshot> TapPressed;

        public BoardInputSnapshot LastSnapshot { get; private set; }

        private Camera _mainCamera;
        private Func<Vector2, bool> _canRecoverStuckTouchAtScreen;
        private bool _inputEnabled;
        private bool _wasPointerPressedLastFrame;
        private bool _wasTouchPressedLastFrame;
        private float _touchPressStartTime = -999f;
        private Vector2 _touchPressStartPosition;
        private float _lastPointerProbeLogTime = -999f;
        private float _lastSyntheticTouchTapTime = -999f;

        private const float PointerProbeLogCooldown = 0.5f;
        private const float StuckTouchRecoveryDelay = 0.3f;
        private const float StaleTouchIgnoreDelay = 0.45f;
        private const float SyntheticTouchTapCooldown = 0.2f;
        private const float TouchMoveTolerancePixels = 12f;

        public void Init(Camera mainCamera, Func<Vector2, bool> canRecoverStuckTouchAtScreen)
        {
            _mainCamera = mainCamera;
            _canRecoverStuckTouchAtScreen = canRecoverStuckTouchAtScreen;
            _inputEnabled = true;
            _wasPointerPressedLastFrame = false;
            _wasTouchPressedLastFrame = false;
            _touchPressStartTime = -999f;
            _lastPointerProbeLogTime = -999f;
            _lastSyntheticTouchTapTime = -999f;
            LastSnapshot = new BoardInputSnapshot("None", Vector2.zero, false, false, false);
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
        }

        private void Update()
        {
            if (!_inputEnabled)
                return;

            BoardInputSnapshot snapshot = ReadPointerSnapshot();
            LastSnapshot = snapshot;

            if (snapshot.IsPressed && !snapshot.PressedThisFrame)
                LogPointerProbeOnce(snapshot, "Pointer held without a new press edge.");

            if (snapshot.PressedThisFrame)
                TapPressed?.Invoke(snapshot);

            _wasPointerPressedLastFrame = snapshot.IsPressed;
        }

        private BoardInputSnapshot ReadPointerSnapshot()
        {
            if (Touchscreen.current != null)
            {
                if (TryReadTouchBegan(out BoardInputSnapshot beganTouch))
                    return beganTouch;

                var touch = Touchscreen.current.primaryTouch;
                bool touchPressed = touch.press.isPressed;
                bool touchPressedThisFrame = touch.press.wasPressedThisFrame;
                Vector2 touchPosition = touch.position.ReadValue();

                if (touchPressed || touchPressedThisFrame)
                {
                    UpdateTouchPressTracking(touchPressed, touchPosition);

                    int touchId = touch.touchId.ReadValue();
                    bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);
                    bool manualEdge = touchPressed && !_wasPointerPressedLastFrame;
                    bool touchIsStale = IsTouchStale();
                    bool syntheticEdge = !touchIsStale && ShouldRecoverStuckTouch(touchPosition, overUI);

                    if (syntheticEdge)
                    {
                        float heldDuration = Time.unscaledTime - _touchPressStartTime;
                        _lastSyntheticTouchTapTime = Time.unscaledTime;
                        _touchPressStartTime = Time.unscaledTime;
                        _touchPressStartPosition = touchPosition;
                        DebugSystem.Warning(DebugCategory.Input, $"Recovered stuck touch by synthesizing tap edge. screen={touchPosition} held={heldDuration:0.000}", this);
                    }

                    if (!touchIsStale)
                    {
                        _wasTouchPressedLastFrame = touchPressed;

                        return new BoardInputSnapshot(
                            "Touch",
                            touchPosition,
                            touchPressed,
                            touchPressedThisFrame || manualEdge || syntheticEdge,
                            overUI);
                    }

                    LogPointerProbeOnce(
                        new BoardInputSnapshot("TouchStale", touchPosition, touchPressed, false, overUI),
                        "Ignoring stale held touch and allowing mouse/new touch fallback.");
                }
            }

            if (Mouse.current != null)
            {
                bool mousePressed = Mouse.current.leftButton.isPressed;
                bool mousePressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
                bool manualEdge = mousePressed && !_wasPointerPressedLastFrame;
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

                return new BoardInputSnapshot(
                    "Mouse",
                    Mouse.current.position.ReadValue(),
                    mousePressed,
                    mousePressedThisFrame || manualEdge,
                    overUI);
            }

            return new BoardInputSnapshot("None", Vector2.zero, false, false, false);
        }

        private bool TryReadTouchBegan(out BoardInputSnapshot snapshot)
        {
            snapshot = default;

            if (Touchscreen.current == null)
                return false;

            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                bool began = touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began ||
                             touch.press.wasPressedThisFrame;

                if (!began)
                    continue;

                Vector2 position = touch.position.ReadValue();
                int touchId = touch.touchId.ReadValue();
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);

                _wasTouchPressedLastFrame = true;
                _touchPressStartTime = Time.unscaledTime;
                _touchPressStartPosition = position;

                snapshot = new BoardInputSnapshot("TouchBegan", position, true, true, overUI);
                return true;
            }

            return false;
        }

        private void UpdateTouchPressTracking(bool touchPressed, Vector2 touchPosition)
        {
            if (!touchPressed)
            {
                _wasTouchPressedLastFrame = false;
                _touchPressStartTime = -999f;
                return;
            }

            if (!_wasTouchPressedLastFrame)
            {
                _touchPressStartTime = Time.unscaledTime;
                _touchPressStartPosition = touchPosition;
                return;
            }

            if (Vector2.Distance(touchPosition, _touchPressStartPosition) > TouchMoveTolerancePixels)
            {
                _touchPressStartTime = Time.unscaledTime;
                _touchPressStartPosition = touchPosition;
            }
        }

        private bool ShouldRecoverStuckTouch(Vector2 touchPosition, bool overUI)
        {
            if (overUI)
                return false;

            if (_touchPressStartTime < 0f)
                return false;

            if (Time.unscaledTime - _touchPressStartTime < StuckTouchRecoveryDelay)
                return false;

            if (Time.unscaledTime - _lastSyntheticTouchTapTime < SyntheticTouchTapCooldown)
                return false;

            return _canRecoverStuckTouchAtScreen?.Invoke(touchPosition) == true;
        }

        private bool IsTouchStale()
        {
            if (_touchPressStartTime < 0f)
                return false;

            return Time.unscaledTime - _touchPressStartTime >= StaleTouchIgnoreDelay;
        }

        private void LogPointerProbeOnce(BoardInputSnapshot snapshot, string reason)
        {
            if (_mainCamera == null)
                return;

            if (Time.unscaledTime - _lastPointerProbeLogTime < PointerProbeLogCooldown)
                return;

            _lastPointerProbeLogTime = Time.unscaledTime;

            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(snapshot.ScreenPosition);
            worldPos.z = 0f;

            RaycastHit2D[] hits = Physics2D.RaycastAll(worldPos, Vector2.zero);
            string firstHit = "none";
            if (hits.Length > 0 && hits[0].collider != null)
            {
                BlockIdentity identity = hits[0].collider.GetComponentInParent<BlockIdentity>();
                firstHit = $"{hits[0].collider.name},block={(identity != null ? identity.name : "null")},moving={(identity != null && identity.IsMoving)}";
            }

            DebugSystem.Log(
                DebugCategory.Input,
                $"Pointer probe reason={reason} source={snapshot.Source} pressed={snapshot.IsPressed} edge={snapshot.PressedThisFrame} overUI={snapshot.IsOverUI} screen={snapshot.ScreenPosition} world={worldPos} hits={hits.Length} firstHit={firstHit}",
                this);
        }
    }
}
