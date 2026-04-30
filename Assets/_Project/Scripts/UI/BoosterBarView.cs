using UnityEngine;
using UnityEngine.UI;
using DragonRescue.Booster;
using DragonRescue.Core;
using DragonRescue.Data;

namespace DragonRescue.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class BoosterBarView : MonoBehaviour
    {
        [SerializeField] private BoosterButtonView[] _buttons;
        [SerializeField] private float _barHeight = 300f;
        [SerializeField] private float _bottomPadding = 24f;
        [SerializeField] private Vector2 _buttonSize = new Vector2(150f, 115f);
        [SerializeField] private float _buttonSpacing = 18f;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private HorizontalLayoutGroup _layoutGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = transform as RectTransform;
            _layoutGroup = GetComponent<HorizontalLayoutGroup>();

            if (_buttons == null || _buttons.Length == 0)
                _buttons = GetComponentsInChildren<BoosterButtonView>(true);

            ApplyRuntimeLayout();
        }

        private void OnEnable()
        {
            ApplyRuntimeLayout();
            transform.SetAsLastSibling();

            GameEvents.OnBoosterChargeChanged += OnBoosterChargeChanged;
            GameEvents.OnBoosterSelectionModeChanged += OnBoosterSelectionModeChanged;
            GameEvents.OnGameStateChanged += OnGameStateChanged;

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i].Clicked += OnButtonClicked;
            }

            RefreshAll();

            if (GameManager.Instance != null)
                OnGameStateChanged(GameManager.Instance.CurrentState);
            else
                SetVisible(true);
        }

        private void OnDisable()
        {
            GameEvents.OnBoosterChargeChanged -= OnBoosterChargeChanged;
            GameEvents.OnBoosterSelectionModeChanged -= OnBoosterSelectionModeChanged;
            GameEvents.OnGameStateChanged -= OnGameStateChanged;

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i].Clicked -= OnButtonClicked;
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            SetVisible(state == GameState.Playing);
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void ApplyRuntimeLayout()
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;

            if (_layoutGroup == null)
                _layoutGroup = GetComponent<HorizontalLayoutGroup>();

            if (_buttons == null || _buttons.Length == 0)
                _buttons = GetComponentsInChildren<BoosterButtonView>(true);

            if (_rectTransform != null)
            {
                _rectTransform.anchorMin = new Vector2(0f, 0f);
                _rectTransform.anchorMax = new Vector2(1f, 0f);
                _rectTransform.pivot = new Vector2(0.5f, 0f);
                _rectTransform.anchoredPosition = Vector2.zero;
                _rectTransform.sizeDelta = new Vector2(0f, _barHeight);
            }

            if (_layoutGroup != null)
            {
                _layoutGroup.padding = new RectOffset(24, 24, 0, Mathf.RoundToInt(_bottomPadding));
                _layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                _layoutGroup.spacing = _buttonSpacing;
            }

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i].ApplyRuntimeLayout(_buttonSize);
            }
        }

        private void OnButtonClicked(BoosterType type)
        {
            if (BoosterManager.Instance == null)
            {
                Debug.LogWarning($"[BoosterBarView] Cannot activate {type}: BoosterManager.Instance is null.");
                return;
            }

            BoosterManager.Instance.TryActivateBooster(type);
        }

        private void OnBoosterChargeChanged(BoosterType type, int charges)
        {
            BoosterButtonView button = FindButton(type);
            if (button != null)
                button.SetCharge(charges);
        }

        private void OnBoosterSelectionModeChanged(BoosterType? activeType)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i].SetSelected(activeType.HasValue && _buttons[i].Type == activeType.Value);
            }
        }

        private void RefreshAll()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                if (button == null) continue;

                int charges = BoosterManager.Instance != null ? BoosterManager.Instance.GetCharge(button.Type) : 0;
                button.SetCharge(charges);
                button.SetSelected(BoosterManager.Instance != null &&
                                   BoosterManager.Instance.ActiveSelectionMode == button.Type);
            }
        }

        private BoosterButtonView FindButton(BoosterType type)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null && _buttons[i].Type == type)
                    return _buttons[i];
            }

            return null;
        }
    }
}
