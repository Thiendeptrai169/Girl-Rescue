using DragonRescue.Core;
using DragonRescue.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRescue.UI
{
    public class RemoveBoosterSelectionOverlayView : MonoBehaviour
    {
        [SerializeField] private Color _dimColor = new Color(0f, 0f, 0f, 0.58f);
        [SerializeField] private Color _promptBackgroundColor = new Color(1f, 0.94f, 0.78f, 1f);
        [SerializeField] private Color _promptBorderColor = new Color(1f, 0.56f, 0.1f, 1f);
        [SerializeField] private string _prompt = "Please select the box";

        private RectTransform _rectTransform;
        private Image _topPanel;
        private Image _bottomPanel;
        private Image _leftPanel;
        private Image _rightPanel;
        private Image _promptBackground;
        private Image _promptBorder;
        private TMP_Text _promptText;
        private CanvasGroup _canvasGroup;
        private float _previousTimeScale = 1f;
        private bool _showing;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
            _canvasGroup = GetComponent<CanvasGroup>();
            EnsureBuilt();
            HideImmediate();
        }

        private void OnEnable()
        {
            GameEvents.OnBoosterSelectionModeChanged += OnBoosterSelectionModeChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnBoosterSelectionModeChanged -= OnBoosterSelectionModeChanged;
            ResumeTimeIfNeeded();
        }

        private void OnDestroy()
        {
            ResumeTimeIfNeeded();
        }

        private void Update()
        {
            if (_showing)
                LayoutAroundBoard();
        }

        private void OnBoosterSelectionModeChanged(BoosterType? activeType)
        {
            if (activeType == BoosterType.Remove)
                Show();
            else
                Hide();
        }

        private void Show()
        {
            EnsureBuilt();
            _showing = true;
            transform.SetAsLastSibling();
            LayoutAroundBoard();

            SetCanvasVisible(true);

            if (!Mathf.Approximately(Time.timeScale, 0f))
                _previousTimeScale = Time.timeScale;

            Time.timeScale = 0f;
        }

        private void Hide()
        {
            if (!_showing) return;

            _showing = false;
            HideImmediate();
            ResumeTimeIfNeeded();
        }

        private void HideImmediate()
        {
            SetPanelVisible(_topPanel, false);
            SetPanelVisible(_bottomPanel, false);
            SetPanelVisible(_leftPanel, false);
            SetPanelVisible(_rightPanel, false);

            if (_promptText != null)
                _promptText.gameObject.SetActive(false);

            if (_promptBackground != null)
                _promptBackground.gameObject.SetActive(false);

            if (_promptBorder != null)
                _promptBorder.gameObject.SetActive(false);

            SetCanvasVisible(false);
        }

        private void ResumeTimeIfNeeded()
        {
            if (!_showing && Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = Mathf.Approximately(_previousTimeScale, 0f) ? 1f : _previousTimeScale;
        }

        private void EnsureBuilt()
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_rectTransform != null)
            {
                _rectTransform.anchorMin = Vector2.zero;
                _rectTransform.anchorMax = Vector2.one;
                _rectTransform.offsetMin = Vector2.zero;
                _rectTransform.offsetMax = Vector2.zero;
            }

            if (_topPanel == null)
                _topPanel = CreatePanel("TopDim");

            if (_bottomPanel == null)
                _bottomPanel = CreatePanel("BottomDim");

            if (_leftPanel == null)
                _leftPanel = CreatePanel("LeftDim");

            if (_rightPanel == null)
                _rightPanel = CreatePanel("RightDim");

            if (_promptBorder == null)
                _promptBorder = CreatePromptImage("PromptBorder", _promptBorderColor);

            if (_promptBackground == null)
                _promptBackground = CreatePromptImage("PromptBackground", _promptBackgroundColor);

            if (_promptText == null)
            {
                GameObject textObject = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(transform, false);
                _promptText = textObject.GetComponent<TMP_Text>();
                _promptText.text = _prompt;
                _promptText.fontSize = 46f;
                _promptText.fontStyle = FontStyles.Bold;
                _promptText.alignment = TextAlignmentOptions.Center;
                _promptText.color = new Color(0.43f, 0.25f, 0.13f, 1f);
                _promptText.raycastTarget = false;
                _promptText.textWrappingMode = TextWrappingModes.NoWrap;
                _promptText.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private Image CreatePanel(string panelName)
        {
            GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);

            Image image = panel.GetComponent<Image>();
            image.color = _dimColor;
            image.raycastTarget = true;
            return image;
        }

        private Image CreatePromptImage(string imageName, Color color)
        {
            GameObject panel = new GameObject(imageName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);

            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void LayoutAroundBoard()
        {
            LevelConfig config = GameManager.Instance != null ? GameManager.Instance.CurrentLevelConfig : null;
            Rect hole = GetBoardViewportRect(config);

            SetAnchors(_bottomPanel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, hole.yMin));
            SetAnchors(_topPanel.rectTransform, new Vector2(0f, hole.yMax), new Vector2(1f, 1f));
            SetAnchors(_leftPanel.rectTransform, new Vector2(0f, hole.yMin), new Vector2(hole.xMin, hole.yMax));
            SetAnchors(_rightPanel.rectTransform, new Vector2(hole.xMax, hole.yMin), new Vector2(1f, hole.yMax));

            SetPanelVisible(_topPanel, true);
            SetPanelVisible(_bottomPanel, true);
            SetPanelVisible(_leftPanel, true);
            SetPanelVisible(_rightPanel, true);

            if (_promptText != null)
            {
                RectTransform borderRect = _promptBorder.rectTransform;
                borderRect.anchorMin = new Vector2(hole.xMin, Mathf.Clamp01(hole.yMax + 0.008f));
                borderRect.anchorMax = new Vector2(hole.xMax, Mathf.Clamp01(hole.yMax + 0.085f));
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;

                RectTransform backgroundRect = _promptBackground.rectTransform;
                backgroundRect.anchorMin = borderRect.anchorMin;
                backgroundRect.anchorMax = borderRect.anchorMax;
                backgroundRect.offsetMin = new Vector2(8f, 8f);
                backgroundRect.offsetMax = new Vector2(-8f, -8f);

                RectTransform promptRect = _promptText.rectTransform;
                promptRect.anchorMin = backgroundRect.anchorMin;
                promptRect.anchorMax = backgroundRect.anchorMax;
                promptRect.offsetMin = new Vector2(16f, 0f);
                promptRect.offsetMax = new Vector2(-16f, 0f);

                _promptBorder.transform.SetAsLastSibling();
                _promptBackground.transform.SetAsLastSibling();
                _promptText.transform.SetAsLastSibling();
                _promptBorder.gameObject.SetActive(true);
                _promptBackground.gameObject.SetActive(true);
                _promptText.gameObject.SetActive(true);
            }
        }

        private Rect GetBoardViewportRect(LevelConfig config)
        {
            if (config == null)
                return new Rect(0.08f, 0.18f, 0.84f, 0.36f);

            float halfWidth = config.boardWidthRatio * 0.5f;
            float halfHeight = config.boardHeightRatio * 0.5f;
            float padding = 0.015f;

            float xMin = Mathf.Clamp01(config.boardViewport.x - halfWidth - padding);
            float xMax = Mathf.Clamp01(config.boardViewport.x + halfWidth + padding);
            float yMin = Mathf.Clamp01(config.boardViewport.y - halfHeight - padding);
            float yMax = Mathf.Clamp01(config.boardViewport.y + halfHeight + padding);

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetPanelVisible(Image panel, bool visible)
        {
            if (panel != null)
                panel.gameObject.SetActive(visible);
        }

        private void SetCanvasVisible(bool visible)
        {
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
