using DragonRescue.Entities.Cannon;
using DragonRescue.Core;
using DragonRescue.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DragonRescue.EditorScripts
{
    public static class LevelAmmoHUDSetup
    {
        private const string LevelHUDName = "UI_LevelHUD";
        private const string CannonAmmoBadgeName = "AmmoBadge";
        private const string CanvasPrefabPath = "Assets/_Project/Prefabs/UI/Canvas.prefab";
        private const string CannonSlotPrefabPath = "Assets/_Project/Prefabs/Gameplay/CannonSlot.prefab";

        [MenuItem("Dragon Rescue/UI/Setup Level And Ammo HUD")]
        public static void SetupFromMenu()
        {
            SetupLevelHUDInOpenScene();
            SetupLevelHUDInCanvasPrefab();
            SetupCannonAmmoBadgePrefab();
        }

        private static void SetupLevelHUDInOpenScene()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                DebugSystem.Warning(DebugCategory.UI, "No Canvas found in the open scene. Canvas prefab setup will still run.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Setup Level HUD");
            SetupLevelHUD(canvas.transform);
            EditorUtility.SetDirty(canvas.gameObject);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        }

        private static void SetupLevelHUDInCanvasPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            if (prefabRoot == null)
            {
                DebugSystem.Warning(DebugCategory.UI, $"Could not load {CanvasPrefabPath}.");
                return;
            }

            SetupLevelHUD(prefabRoot.transform);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CanvasPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void SetupLevelHUD(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find(LevelHUDName);
            GameObject root = existing != null ? existing.gameObject : new GameObject(LevelHUDName, typeof(RectTransform), typeof(CanvasGroup), typeof(LevelHUDView));
            root.layer = canvasRoot.gameObject.layer;

            if (root.transform.parent != canvasRoot)
                root.transform.SetParent(canvasRoot, false);

            RectTransform rootRect = GetOrAdd<RectTransform>(root);
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(28f, -28f);
            rootRect.sizeDelta = new Vector2(260f, 72f);

            CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(root);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            LevelHUDView levelHUD = GetOrAdd<LevelHUDView>(root);
            TMP_Text levelText = root.GetComponentInChildren<TMP_Text>(true);
            if (levelText == null)
                levelText = CreateUGUIText("LevelText", root.transform, "LEVEL 1", 46f, FontStyles.Bold, Color.white);

            RectTransform textRect = levelText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;

            levelText.text = "LEVEL 1";
            levelText.fontSize = 46f;
            levelText.fontStyle = FontStyles.Bold;
            levelText.alignment = TextAlignmentOptions.MidlineLeft;
            levelText.textWrappingMode = TextWrappingModes.NoWrap;
            levelText.overflowMode = TextOverflowModes.Overflow;
            levelText.color = Color.white;
            levelText.outlineWidth = 0.22f;
            levelText.outlineColor = Color.black;

            SerializedObject serialized = new SerializedObject(levelHUD);
            serialized.FindProperty("_levelText").objectReferenceValue = levelText;
            serialized.FindProperty("_prefix").stringValue = "LEVEL";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupCannonAmmoBadgePrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CannonSlotPrefabPath);
            if (prefabRoot == null)
            {
                DebugSystem.Warning(DebugCategory.UI, $"Could not load {CannonSlotPrefabPath}.");
                return;
            }

            CannonSlot cannonSlot = prefabRoot.GetComponent<CannonSlot>();
            if (cannonSlot == null)
            {
                DebugSystem.Warning(DebugCategory.UI, "CannonSlot prefab has no CannonSlot component.");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            Transform existing = prefabRoot.transform.Find(CannonAmmoBadgeName);
            GameObject badgeRoot = existing != null ? existing.gameObject : new GameObject(CannonAmmoBadgeName, typeof(CanvasGroup), typeof(CannonAmmoBadgeView));
            badgeRoot.transform.SetParent(prefabRoot.transform, false);
            badgeRoot.transform.localPosition = new Vector3(-0.18f, -0.16f, -0.05f);
            badgeRoot.transform.localRotation = Quaternion.identity;
            badgeRoot.transform.localScale = Vector3.one * 0.08f;

            CanvasGroup ammoCanvasGroup = GetOrAdd<CanvasGroup>(badgeRoot);
            ammoCanvasGroup.alpha = 0f;
            ammoCanvasGroup.interactable = false;
            ammoCanvasGroup.blocksRaycasts = false;

            CannonAmmoBadgeView badgeView = GetOrAdd<CannonAmmoBadgeView>(badgeRoot);
            TMP_Text ammoText = badgeRoot.GetComponentInChildren<TMP_Text>(true);
            if (ammoText == null)
            {
                TextMeshPro worldText = badgeRoot.AddComponent<TextMeshPro>();
                ammoText = worldText;
            }

            ammoText.text = "6";
            ammoText.fontSize = 4.4f;
            ammoText.fontStyle = FontStyles.Bold;
            ammoText.alignment = TextAlignmentOptions.Center;
            ammoText.textWrappingMode = TextWrappingModes.NoWrap;
            ammoText.overflowMode = TextOverflowModes.Overflow;
            ammoText.color = Color.white;
            ammoText.outlineWidth = 0.18f;
            ammoText.outlineColor = Color.black;

            RectTransform textRect = ammoText.rectTransform;
            if (textRect != null)
                textRect.sizeDelta = new Vector2(3f, 1.4f);

            SerializedObject badgeSerialized = new SerializedObject(badgeView);
            badgeSerialized.FindProperty("_slot").objectReferenceValue = cannonSlot;
            badgeSerialized.FindProperty("_ammoText").objectReferenceValue = ammoText;
            badgeSerialized.FindProperty("_root").objectReferenceValue = badgeRoot;
            badgeSerialized.FindProperty("_canvasGroup").objectReferenceValue = ammoCanvasGroup;
            badgeSerialized.ApplyModifiedPropertiesWithoutUndo();

            badgeView.Clear();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CannonSlotPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            DebugSystem.Log(DebugCategory.UI, "Level HUD and cannon ammo badge setup complete.");
        }

        private static TMP_Text CreateUGUIText(string name, Transform parent, string value, float fontSize, FontStyles style, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
