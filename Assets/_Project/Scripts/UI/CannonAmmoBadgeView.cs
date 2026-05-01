using TMPro;
using UnityEngine;
using DragonRescue.Core;
using DragonRescue.Entities.Cannon;

namespace DragonRescue.UI
{
    public class CannonAmmoBadgeView : MonoBehaviour
    {
        [SerializeField] private CannonSlot _slot;
        [SerializeField] private TMP_Text _ammoText;
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (_ammoText == null)
                _ammoText = GetComponentInChildren<TMP_Text>(true);

            if (_root == null)
                _root = gameObject;

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_slot == null)
                _slot = GetComponentInParent<CannonSlot>();
        }

        private void OnEnable()
        {
            GameEvents.OnCannonAmmoChanged += OnCannonAmmoChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnCannonAmmoChanged -= OnCannonAmmoChanged;
        }

        private void OnCannonAmmoChanged(CannonAmmoChangedPayload payload)
        {
            if (_slot == null || payload == null || payload.SlotIndex != _slot.Index)
                return;

            SetAmmo(payload.Ammo, payload.IsLoaded);
        }

        public void SetAmmo(int ammo, bool isLoaded)
        {
            bool visible = isLoaded && ammo > 0;

            if (_ammoText != null)
            {
                _ammoText.text = "x" + ammo.ToString();
                _ammoText.transform.localScale = Vector3.one * 0.18f;
            }

            SetVisible(visible);
        }

        public void Clear()
        {
            SetAmmo(0, false);
        }

        private void SetVisible(bool visible)
        {
            if (_root != null && _root != gameObject)
                _root.SetActive(visible);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug / Test Ammo 6")]
        private void DebugTestAmmo()
        {
            SetAmmo(6, true);
        }

        [ContextMenu("Debug / Clear")]
        private void DebugClear()
        {
            Clear();
        }
#endif
    }
}
