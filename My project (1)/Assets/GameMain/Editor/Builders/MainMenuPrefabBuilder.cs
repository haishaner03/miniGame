using GameMain.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.Editor
{
    [InitializeOnLoad]
    public static class MainMenuPrefabBuilder
    {
        private const string PrefabPath = "Assets/GameMain/Prefabs/UI/MainMenu.prefab";

        static MainMenuPrefabBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            EditorApplication.delayCall -= BuildIfMissing;

            if (!AssetDatabase.IsValidFolder("Assets/GameMain/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/GameMain", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder("Assets/GameMain/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/GameMain/Prefabs", "UI");
            }

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            {
                Build();
            }
        }

        [MenuItem("GameMain/Build UI Prefabs/Main Menu")]
        public static void Build()
        {
            BuildFolders();
            GameObject root = CreateMainMenu();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("[UGF] Built main menu prefab: " + PrefabPath);
        }

        private static void BuildFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/GameMain/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/GameMain", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder("Assets/GameMain/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/GameMain/Prefabs", "UI");
            }
        }

        private static GameObject CreateMainMenu()
        {
            GameObject root = new GameObject("MainMenu");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler canvasScaler = root.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1280f, 720f);
            canvasScaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            MainMenuView view = root.AddComponent<MainMenuView>();

            GameObject panel = CreateRect("Panel", root.transform, Vector2.zero, new Vector2(420f, 260f));
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.88f);

            Text title = CreateText("Title", panel.transform, "Mini Fighter", 36, new Vector2(0f, 64f), new Vector2(360f, 60f));
            title.alignment = TextAnchor.MiddleCenter;

            Button startButton = CreateButton(panel.transform, "Start Game", new Vector2(0f, -28f), new Vector2(220f, 54f));
            SerializedObject serializedObject = new SerializedObject(view);
            serializedObject.FindProperty("m_StartButton").objectReferenceValue = startButton;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject CreateRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);

            RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            return gameObject;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = CreateRect(name, parent, anchoredPosition, sizeDelta);
            Text text = gameObject.AddComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.color = Color.white;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject buttonObject = CreateRect("StartButton", parent, anchoredPosition, sizeDelta);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.48f, 0.86f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Label", buttonObject.transform, label, 22, Vector2.zero, sizeDelta);
            text.alignment = TextAnchor.MiddleCenter;

            return button;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
