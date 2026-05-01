using TMPro;
using UnityEngine;
using DragonRescue.Core;
using DragonRescue.Data;

namespace DragonRescue.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class LevelHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private string _prefix = "LEVEL";

        private CanvasGroup _canvasGroup;
        private LevelConfig _currentConfig;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_levelText == null)
                _levelText = GetComponentInChildren<TMP_Text>(true);

            SetVisible(false);
        }

        private void OnEnable()
        {
            GameEvents.OnLevelStarted += SetLevel;
            GameEvents.OnGameStateChanged += OnGameStateChanged;

            if (GameManager.Instance != null && GameManager.Instance.CurrentLevelConfig != null)
                SetLevel(GameManager.Instance.CurrentLevelConfig);
        }

        private void OnDisable()
        {
            GameEvents.OnLevelStarted -= SetLevel;
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
        }

        public void SetLevel(LevelConfig config)
        {
            _currentConfig = config;

            if (_levelText != null && config != null)
                _levelText.text = $"{_prefix} {config.levelNumber}";

            if (GameManager.Instance != null)
                SetVisible(GameManager.Instance.CurrentState == GameState.Playing);
            else
                SetVisible(config != null);
        }

        private void OnGameStateChanged(GameState state)
        {
            SetVisible(state == GameState.Playing && _currentConfig != null);
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug / Set Level 7")]
        private void DebugSetLevel7()
        {
            if (_levelText != null)
                _levelText.text = $"{_prefix} 7";

            SetVisible(true);
        }
#endif
    }
}
