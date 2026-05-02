using DragonRescue.Core;
using DragonRescue.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRescue.EditorScripts
{
    public static class GameplayPromptViewEditorSetup
    {
        private const string CanvasPrefabPath = "Assets/_Project/Prefabs/UI/Canvas.prefab";
        private const string RootName = "UI_GameplayPrompt";
        private const string PromptGroupName = "PromptGroup";
        private const string PromptBackgroundName = "PromptBackground";
        private const string PromptTextName = "PromptText";
        private const string ScreenFlashName = "ScreenFlash";

        [MenuItem("Dragon Rescue/UI/Setup Gameplay Prompt View")]
        public static void SetupFromMenu()
        {
            SetupInOpenScene();
            SetupInCanvasPrefab();
        }

        private static void SetupInOpenScene()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                DebugSystem.Warning(DebugCategory.UI, "No Canvas found in the open scene. Canvas prefab setup will still run.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Setup Gameplay Prompt View");
            SetupOnCanvas(canvas.transform, useUndo: true);
            EditorUtility.SetDirty(canvas.gameObject);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        }

        private static void SetupInCanvasPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            if (prefabRoot == null)
            {
                DebugSystem.Warning(DebugCategory.UI, $"Could not load {CanvasPrefabPath}.");
                return;
            }

            SetupOnCanvas(prefabRoot.transform, useUndo: false);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CanvasPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void SetupOnCanvas(Transform canvasRoot, bool useUndo)
        {
            Transform existing = canvasRoot.Find(RootName);
            GameObject root = existing != null
                ? existing.gameObject
                : CreateGameObject(RootName, canvasRoot, useUndo, typeof(RectTransform), typeof(GameplayPromptView));

            root.layer = canvasRoot.gameObject.layer;
            root.transform.SetParent(canvasRoot, false);
            root.transform.SetAsLastSibling();

            RectTransform rootRect = GetOrAdd<RectTransform>(root, useUndo);
            Stretch(rootRect);

            GameplayPromptView promptView = GetOrAdd<GameplayPromptView>(root, useUndo);

            Image screenFlash = EnsureScreenFlash(root.transform, useUndo);
            CanvasGroup promptGroup = EnsurePromptGroup(root.transform, useUndo);
            TMP_Text promptText = EnsurePromptText(promptGroup.transform, useUndo);

            SerializedObject serialized = new SerializedObject(promptView);
            serialized.FindProperty("_promptGroup").objectReferenceValue = promptGroup;
            serialized.FindProperty("_promptText").objectReferenceValue = promptText;
            serialized.FindProperty("_screenFlash").objectReferenceValue = screenFlash;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DebugSystem.Log(DebugCategory.UI, "Gameplay prompt view setup complete.");
        }

        private static Image EnsureScreenFlash(Transform parent, bool useUndo)
        {
            Transform existing = parent.Find(ScreenFlashName);
            GameObject go = existing != null
                ? existing.gameObject
                : CreateGameObject(ScreenFlashName, parent, useUndo, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            go.transform.SetAsFirstSibling();
            Image image = GetOrAdd<Image>(go, useUndo);
            image.color = new Color(1f, 0f, 0f, 0f);
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            go.SetActive(false);
            return image;
        }

        private static CanvasGroup EnsurePromptGroup(Transform parent, bool useUndo)
        {
            Transform existing = parent.Find(PromptGroupName);
            GameObject go = existing != null
                ? existing.gameObject
                : CreateGameObject(PromptGroupName, parent, useUndo, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));

            go.transform.SetAsLastSibling();
            RectTransform rect = GetOrAdd<RectTransform>(go, useUndo);
            rect.anchorMin = new Vector2(0.12f, 0.78f);
            rect.anchorMax = new Vector2(0.88f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image background = GetOrAdd<Image>(go, useUndo);
            background.color = new Color(1f, 0.94f, 0.78f, 0.96f);
            background.raycastTarget = false;

            CanvasGroup group = GetOrAdd<CanvasGroup>(go, useUndo);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            go.SetActive(false);
            return group;
        }

        private static TMP_Text EnsurePromptText(Transform parent, bool useUndo)
        {
            Transform existing = parent.Find(PromptTextName);
            GameObject go = existing != null
                ? existing.gameObject
                : CreateGameObject(PromptTextName, parent, useUndo, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

            TMP_Text text = GetOrAdd<TextMeshProUGUI>(go, useUndo);
            text.text = "Cannon slot is full";
            text.fontSize = 42f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.43f, 0.25f, 0.13f, 1f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(text.rectTransform, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            return text;
        }

        private static GameObject CreateGameObject(string name, Transform parent, bool useUndo, params System.Type[] components)
        {
            GameObject go = new GameObject(name, components);
            if (useUndo)
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static T GetOrAdd<T>(GameObject go, bool useUndo) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component != null)
                return component;

            return useUndo ? Undo.AddComponent<T>(go) : go.AddComponent<T>();
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
