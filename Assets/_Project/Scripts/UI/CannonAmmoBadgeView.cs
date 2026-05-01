using TMPro;
using UnityEngine;

namespace DragonRescue.UI
{
    public class CannonAmmoBadgeView : MonoBehaviour
    {
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
        }

        public void Init(int slotIndex, bool isUnlocked)
        {
            SetVisible(false);
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
            if (_root != null)
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
