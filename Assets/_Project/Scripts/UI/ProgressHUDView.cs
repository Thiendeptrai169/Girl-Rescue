using UnityEngine;
using TMPro;
using DragonRescue.Core;
using DragonRescue.Data;

namespace DragonRescue.UI
{
    /// <summary>
    /// Displays the winning progress (percentage of dragon blocks destroyed).
    /// Pure view component decoupled via GameEvents.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ProgressHUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _progressText;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f; // Set default zero
        }

        private void OnEnable()
        {
            GameEvents.OnProgressUpdated += UpdateProgressText;
            GameEvents.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnProgressUpdated -= UpdateProgressText;
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                _canvasGroup.alpha = 1f;
            }
        }

        private void UpdateProgressText(float percent)
        {
            if (_progressText != null)
            {
                // Format to a whole number string, e.g., "6%"
                int displayPercent = Mathf.Clamp(Mathf.RoundToInt(percent * 100f), 0, 100);
                _progressText.text = $"{displayPercent}%";
            }
        }
        
#if UNITY_EDITOR
        [ContextMenu("Debug / Test 50%")]
        private void DebugTest50()
        {
            UpdateProgressText(0.5f);
        }
#endif
    }
}
