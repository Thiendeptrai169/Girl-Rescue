using DragonRescue.Core;
using DragonRescue.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRescue.EditorScripts
{
    [InitializeOnLoad]
    public static class ResultPopupEditorSetup
    {
        private const string RootName = "UI_ResultPopup";
        private const string AutoSetupKey = "DragonRescue.ResultPopupEditorSetup.Done";

        static ResultPopupEditorSetup()
        {
            EditorApplication.delayCall += AutoSetupOnce;
        }

        [MenuItem("Dragon Rescue/UI/Setup Result Popup")]
        public static void SetupFromMenu()
        {
            SetupPopup(forceRebuild: true);
        }

        private static void AutoSetupOnce()
        {
            if (Application.isPlaying || EditorPrefs.GetBool(AutoSetupKey, false))
                return;

            if (GameObject.Find(RootName) == null)
                return;

            if (SetupPopup(forceRebuild: false))
                EditorPrefs.SetBool(AutoSetupKey, true);
        }

        private static bool SetupPopup(bool forceRebuild)
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogWarning($"[ResultPopupEditorSetup] Could not find {RootName} in the open scene.");
                return false;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Setup Result Popup");

            RectTransform rootRect = GetOrAdd<RectTransform>(root);
            Stretch(rootRect, Vector2.zero, Vector2.zero);

            CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(root);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            ResultPopupView popupView = GetOrAdd<ResultPopupView>(root);

            if (forceRebuild || root.transform.childCount <= 1)
                ClearChildren(root.transform);

            Image dim = CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.62f));
            Stretch(dim.rectTransform, Vector2.zero, Vector2.zero);

            Image board = CreateImage("Board", root.transform, new Color(0.98f, 0.965f, 0.92f, 1f));
            RectTransform boardRect = board.rectTransform;
            Center(boardRect, new Vector2(760f, 720f), new Vector2(0f, -15f));

            Image contentGlow = CreateImage("ContentGlow", board.transform, new Color(1f, 1f, 1f, 0.35f));
            Center(contentGlow.rectTransform, new Vector2(560f, 390f), new Vector2(0f, 10f));
            contentGlow.raycastTarget = false;

            Image titlePlate = CreateImage("TitlePlate", root.transform, new Color(0.33f, 0.31f, 0.62f, 1f));
            RectTransform titlePlateRect = titlePlate.rectTransform;
            Center(titlePlateRect, new Vector2(600f, 132f), new Vector2(0f, 345f));

            TMP_Text titleText = CreateText("TitleText", titlePlate.transform, "YOU WIN", 58f, FontStyles.Bold, Color.white);
            Stretch(titleText.rectTransform, new Vector2(26f, 12f), new Vector2(-26f, -12f));

            TMP_Text messageText = CreateText("MessageText", board.transform, "Congratulations!", 38f, FontStyles.Bold, new Color(0.43f, 0.31f, 0.22f, 1f));
            Center(messageText.rectTransform, new Vector2(650f, 260f), new Vector2(0f, 65f));
            messageText.textWrappingMode = TextWrappingModes.Normal;
            messageText.overflowMode = TextOverflowModes.Truncate;

            Image buttonStrip = CreateImage("ButtonStrip", board.transform, new Color(0.24f, 0.45f, 1f, 1f));
            RectTransform stripRect = buttonStrip.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 0f);
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.pivot = new Vector2(0.5f, 0f);
            stripRect.anchoredPosition = Vector2.zero;
            stripRect.sizeDelta = new Vector2(0f, 160f);

            Button homeButton = CreateButton("HomeButton", "HOME", buttonStrip.transform, new Vector2(-185f, 0f), new Color(0.2f, 0.86f, 0.1f, 1f));
            Button rightButton = CreateButton("RightButton", "NEXT LEVEL", buttonStrip.transform, new Vector2(185f, 0f), new Color(1f, 0.73f, 0.12f, 1f));
            TMP_Text rightLabel = rightButton.GetComponentInChildren<TMP_Text>(true);

            WirePopupView(popupView, titleText, messageText, homeButton, rightButton, rightLabel);
            WireGameManager(popupView);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[ResultPopupEditorSetup] Result popup created and wired. Save the scene to keep the changes.");
            return true;
        }

        private static void WirePopupView(ResultPopupView popupView, TMP_Text titleText, TMP_Text messageText, Button homeButton, Button rightButton, TMP_Text rightLabel)
        {
            SerializedObject serialized = new SerializedObject(popupView);
            serialized.FindProperty("_titleText").objectReferenceValue = titleText;
            serialized.FindProperty("_messageText").objectReferenceValue = messageText;
            serialized.FindProperty("_winTitle").stringValue = "YOU WIN";
            serialized.FindProperty("_loseTitle").stringValue = "YOU LOSE";
            serialized.FindProperty("_winMessage").stringValue = "Congratulations!";
            serialized.FindProperty("_loseMessage").stringValue = "Keep going, you can rescue her!";
            serialized.FindProperty("_homeButton").objectReferenceValue = homeButton;
            serialized.FindProperty("_rightButton").objectReferenceValue = rightButton;
            serialized.FindProperty("_rightButtonLabel").objectReferenceValue = rightLabel;
            serialized.FindProperty("_nextLevelLabel").stringValue = "NEXT LEVEL";
            serialized.FindProperty("_retryLabel").stringValue = "RETRY";
            serialized.ApplyModifiedProperties();
        }

        private static void WireGameManager(ResultPopupView popupView)
        {
            GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogWarning("[ResultPopupEditorSetup] Result popup built, but no GameManager was found to wire.");
                return;
            }

            SerializedObject serialized = new SerializedObject(gameManager);
            serialized.FindProperty("_resultPopup").objectReferenceValue = popupView;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(gameManager);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize, FontStyles style, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private static Button CreateButton(string name, string label, Transform parent, Vector2 position, Color color)
        {
            Image background = CreateImage(name, parent, color);
            RectTransform rect = background.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(270f, 95f);

            Button button = background.gameObject.AddComponent<Button>();
            TMP_Text text = CreateText("Label", background.transform, label, 36f, FontStyles.Bold, Color.white);
            Stretch(text.rectTransform, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
