using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DragonRescue.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ResultPopupView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private string _winTitle = "YOU WIN";
        [SerializeField] private string _loseTitle = "YOU LOSE";
        [SerializeField] private string _winMessage = "Congratulations!";
        [SerializeField] private string _loseMessage = "Keep going, you can rescue her!";

        [Header("Buttons")]
        [SerializeField] private Button _homeButton;
        [SerializeField] private Button _rightButton;
        [SerializeField] private TMP_Text _rightButtonLabel;
        [SerializeField] private string _nextLevelLabel = "Next Level";
        [SerializeField] private string _retryLabel = "Retry";

        private CanvasGroup _canvasGroup;
        private UnityAction _rightButtonAction;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            Hide();
        }

        private void OnDisable()
        {
            ClearButtonListeners();
        }

        public void Init(UnityAction homeAction)
        {
            if (_homeButton != null)
            {
                _homeButton.onClick.RemoveListener(homeAction);
                _homeButton.onClick.AddListener(homeAction);
            }
        }

        public void ShowWin(UnityAction nextLevelAction)
        {
            Show(_winTitle, _winMessage, _nextLevelLabel, nextLevelAction);
        }

        public void ShowLose(UnityAction retryAction)
        {
            Show(_loseTitle, _loseMessage, _retryLabel, retryAction);
        }

        public void Hide()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null) return;

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void Show(string title, string message, string rightButtonLabel, UnityAction rightButtonAction)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_titleText != null)
                _titleText.text = title;

            if (_messageText != null)
                _messageText.text = message;

            if (_rightButtonLabel != null)
                _rightButtonLabel.text = rightButtonLabel;

            SetRightButtonAction(rightButtonAction);

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            transform.SetAsLastSibling();
        }

        private void SetRightButtonAction(UnityAction action)
        {
            if (_rightButton == null) return;

            if (_rightButtonAction != null)
                _rightButton.onClick.RemoveListener(_rightButtonAction);

            _rightButtonAction = action;

            if (_rightButtonAction != null)
                _rightButton.onClick.AddListener(_rightButtonAction);
        }

        private void ClearButtonListeners()
        {
            if (_rightButton != null && _rightButtonAction != null)
                _rightButton.onClick.RemoveListener(_rightButtonAction);

            _rightButtonAction = null;
        }
    }
}
