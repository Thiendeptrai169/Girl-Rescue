using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DragonRescue.Data;
using DragonRescue.SFX;

namespace DragonRescue.UI
{
    public class BoosterButtonView : MonoBehaviour
    {
        [SerializeField] private BoosterType _type;
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _defText;
        [SerializeField] private TMP_Text _remainUseText;
        [SerializeField] private TMP_Text _chargeText;
        [SerializeField] private GameObject _disabledOverlay;
        [SerializeField] private GameObject _selectedOverlay;

        public BoosterType Type => _type;

        public event Action<BoosterType> Clicked;

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            RefreshLabel();
        }

        private void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);
        }

        public void SetCharge(int charge)
        {
            bool hasCharge = charge > 0;

            if (_remainUseText != null)
                _remainUseText.text = charge.ToString();
            else if (_chargeText != null)
                _chargeText.text = charge.ToString();

            if (_button != null)
                _button.interactable = hasCharge;

            if (_disabledOverlay != null)
                _disabledOverlay.SetActive(!hasCharge);

            if (_icon != null)
                _icon.color = hasCharge ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        public void SetSelected(bool selected)
        {
            if (_selectedOverlay != null)
                _selectedOverlay.SetActive(selected);
        }

        private void HandleClick()
        {
            SoundManager.PlayBooster(_type);
            Clicked?.Invoke(_type);
        }

        private void RefreshLabel()
        {
            if (_defText != null)
            {
                _defText.text = _type.ToString();
                _defText.textWrappingMode = TextWrappingModes.NoWrap;
                _defText.overflowMode = TextOverflowModes.Overflow;
                _defText.alignment = TextAlignmentOptions.Center;
            }

            if (_remainUseText != null)
            {
                _remainUseText.textWrappingMode = TextWrappingModes.NoWrap;
                _remainUseText.overflowMode = TextOverflowModes.Overflow;
                _remainUseText.alignment = TextAlignmentOptions.Center;
            }

            if (_chargeText != null)
            {
                _chargeText.textWrappingMode = TextWrappingModes.NoWrap;
                _chargeText.overflowMode = TextOverflowModes.Overflow;
                _chargeText.alignment = TextAlignmentOptions.Center;
            }
        }

        public void ApplyRuntimeLayout(Vector2 size)
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null) return;

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;

            RefreshLabel();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            RefreshLabel();
        }
#endif
    }
}
