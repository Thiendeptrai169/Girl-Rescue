using UnityEngine;
using DragonRescue.Booster;
using DragonRescue.Core;
using DragonRescue.Data;

namespace DragonRescue.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class BoosterBarView : MonoBehaviour
    {
        [SerializeField] private BoosterButtonView[] _buttons;

        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_buttons == null || _buttons.Length == 0)
                _buttons = GetComponentsInChildren<BoosterButtonView>(true);
        }

        private void OnEnable()
        {
            GameEvents.OnBoosterChargeChanged += OnBoosterChargeChanged;
            GameEvents.OnBoosterSelectionModeChanged += OnBoosterSelectionModeChanged;
            GameEvents.OnGameStateChanged += OnGameStateChanged;

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i].Clicked += OnButtonClicked;
            }

            RefreshAll();
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
            if (_canvasGroup == null) return;

            bool isPlaying = state == GameState.Playing;
            _canvasGroup.alpha = isPlaying ? 1f : 0f;
            _canvasGroup.interactable = isPlaying;
            _canvasGroup.blocksRaycasts = isPlaying;
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
