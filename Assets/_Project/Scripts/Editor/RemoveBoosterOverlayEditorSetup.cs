using DragonRescue.Core;
using DragonRescue.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRescue.EditorScripts
{
    [InitializeOnLoad]
    public static class RemoveBoosterOverlayEditorSetup
    {
        private const string CanvasPrefabPath = "Assets/_Project/Prefabs/UI/Canvas.prefab";
        private const string OverlayName = "UI_RemoveSelectionOverlay";
        private const string AutoSetupKey = "DragonRescue.RemoveBoosterOverlayEditorSetup.Done";

        static RemoveBoosterOverlayEditorSetup()
        {
            EditorApplication.delayCall += AutoSetupOnce;
        }

        [MenuItem("Dragon Rescue/UI/Setup Remove Booster Overlay")]
        public static void SetupFromMenu()
        {
            SetupCanvasPrefab(force: true);
        }

        private static void AutoSetupOnce()
        {
            if (Application.isPlaying || EditorPrefs.GetBool(AutoSetupKey, false))
                return;

            if (SetupCanvasPrefab(force: false))
                EditorPrefs.SetBool(AutoSetupKey, true);
        }

        private static bool SetupCanvasPrefab(bool force)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            if (prefabRoot == null)
            {
                DebugSystem.Warning(DebugCategory.UI, $"Could not load {CanvasPrefabPath}.");
                return false;
            }

            Transform existing = prefabRoot.transform.Find(OverlayName);
            if (existing != null && !force)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return true;
            }

            GameObject overlayRoot = existing != null
                ? existing.gameObject
                : new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup), typeof(RemoveBoosterSelectionOverlayView));

            overlayRoot.transform.SetParent(prefabRoot.transform, false);
            overlayRoot.SetActive(true);
            overlayRoot.transform.SetAsLastSibling();

            RectTransform rootRect = GetOrAdd<RectTransform>(overlayRoot);
            Stretch(rootRect);

            CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(overlayRoot);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GetOrAdd<RemoveBoosterSelectionOverlayView>(overlayRoot);

            EnsurePanel(overlayRoot.transform, "TopDim");
            EnsurePanel(overlayRoot.transform, "BottomDim");
            EnsurePanel(overlayRoot.transform, "LeftDim");
            EnsurePanel(overlayRoot.transform, "RightDim");
            EnsurePromptImage(overlayRoot.transform, "PromptBorder", new Color(1f, 0.56f, 0.1f, 1f));
            EnsurePromptImage(overlayRoot.transform, "PromptBackground", new Color(1f, 0.94f, 0.78f, 1f));
            EnsurePrompt(overlayRoot.transform);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CanvasPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            DebugSystem.Log(DebugCategory.UI, "Remove booster overlay setup complete.");
            return true;
        }

        private static void EnsurePanel(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject panel = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            panel.transform.SetParent(parent, false);
            panel.SetActive(false);

            Image image = GetOrAdd<Image>(panel);
            image.color = new Color(0f, 0f, 0f, 0.58f);
            image.raycastTarget = true;
            Stretch(image.rectTransform);
        }

        private static void EnsurePrompt(Transform parent)
        {
            Transform existing = parent.Find("Prompt");
            GameObject prompt = existing != null
                ? existing.gameObject
                : new GameObject("Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

            prompt.transform.SetParent(parent, false);
            prompt.SetActive(false);

            TMP_Text text = GetOrAdd<TextMeshProUGUI>(prompt);
            text.text = "Please select the box";
            text.fontSize = 46f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.43f, 0.25f, 0.13f, 1f);
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(text.rectTransform);
            prompt.transform.SetAsLastSibling();
        }

        private static void EnsurePromptImage(Transform parent, string name, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject promptImage = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            promptImage.transform.SetParent(parent, false);
            promptImage.SetActive(false);

            Image image = GetOrAdd<Image>(promptImage);
            image.color = color;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
