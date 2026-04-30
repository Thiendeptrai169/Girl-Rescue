using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DragonRescue.Data;

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
            Clicked?.Invoke(_type);
        }

        private void RefreshLabel()
        {
            if (_defText != null)
                _defText.text = _type.ToString();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshLabel();
        }
#endif
    }
}
