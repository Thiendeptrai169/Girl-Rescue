using DragonRescue.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRescue.UI
{
    public class GameplayPromptView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _promptGroup;
        [SerializeField] private TMP_Text _promptText;
        [SerializeField] private Image _screenFlash;
        [SerializeField] private float _showSeconds = 1.2f;
        [SerializeField] private Color _flashColor = new Color(1f, 0f, 0f, 0.35f);

        private Sequence _promptSequence;
        private Sequence _flashSequence;

        private void Awake()
        {
            HideImmediate();
        }

        private void OnEnable()
        {
            GameEvents.OnGameplayPromptRequested += OnGameplayPromptRequested;
        }

        private void OnDisable()
        {
            GameEvents.OnGameplayPromptRequested -= OnGameplayPromptRequested;
            KillSequences();
        }

        private void OnGameplayPromptRequested(GameplayPromptPayload payload)
        {
            if (payload == null)
                return;

            ShowPrompt(payload.Message);

            if (payload.FlashScreen)
                FlashScreen();
        }

        private void ShowPrompt(string message)
        {
            if (_promptGroup == null || _promptText == null)
                return;

            _promptSequence?.Kill(false);
            _promptText.text = message;
            _promptGroup.alpha = 0f;
            _promptGroup.gameObject.SetActive(true);

            _promptSequence = DOTween.Sequence();
            _promptSequence.SetUpdate(true);
            _promptSequence.Append(_promptGroup.DOFade(1f, 0.12f));
            _promptSequence.AppendInterval(_showSeconds);
            _promptSequence.Append(_promptGroup.DOFade(0f, 0.18f));
            _promptSequence.OnComplete(() => _promptGroup.gameObject.SetActive(false));
        }

        private void FlashScreen()
        {
            if (_screenFlash == null)
                return;

            _flashSequence?.Kill(false);
            _screenFlash.gameObject.SetActive(true);
            _screenFlash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, 0f);

            _flashSequence = DOTween.Sequence();
            _flashSequence.SetUpdate(true);
            _flashSequence.Append(_screenFlash.DOFade(_flashColor.a, 0.06f));
            _flashSequence.Append(_screenFlash.DOFade(0f, 0.18f));
            _flashSequence.OnComplete(() => _screenFlash.gameObject.SetActive(false));
        }

        private void HideImmediate()
        {
            if (_promptGroup != null)
            {
                _promptGroup.alpha = 0f;
                _promptGroup.gameObject.SetActive(false);
            }

            if (_screenFlash != null)
            {
                _screenFlash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, 0f);
                _screenFlash.gameObject.SetActive(false);
            }
        }

        private void KillSequences()
        {
            _promptSequence?.Kill(false);
            _flashSequence?.Kill(false);
            _promptSequence = null;
            _flashSequence = null;
        }
    }
}
