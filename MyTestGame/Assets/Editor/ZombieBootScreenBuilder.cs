#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ZombieBootScreenBuilder
{
    private const string ScenePath = "Assets/GameMain/Scenes/GameStart/GameStart.unity";
    private const string BackgroundPath = "Assets/GameMain/UI/Art/ZombieBoot_Background.png";
    private const string PreviewBackgroundPath = "Assets/GameMain/UI/Art/ZombieMainMenu_Background.png";
    private const string FontPath = "Assets/GameMain/Res/Fonts/NotoSansSC/NotoSansCJKsc-Regular.otf";
    private const float TitleInset = 100f;

    [MenuItem("Tools/Zombie/Build Startup Loading Screen")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new System.InvalidOperationException("Open GameStart before building the startup screen.");

        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        if (background == null) background = AssetDatabase.LoadAssetAtPath<Sprite>(PreviewBackgroundPath);
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (background == null || font == null)
            throw new System.InvalidOperationException("Startup background or Chinese font is missing.");

        GameObject root = null;
        foreach (GameObject item in scene.GetRootGameObjects())
            if (item.name == "StartupLoadingScreen") root = item;
        if (root != null) Undo.DestroyObjectImmediate(root);

        root = new GameObject("StartupLoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ZombieBootLoadingScreen));
        Undo.RegisterCreatedObjectUndo(root, "Build startup loading screen");
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image image = CreateImage("Background", root.transform, Color.white);
        image.sprite = background;
        Stretch(image.rectTransform);
        AspectRatioFitter fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = background.rect.width / background.rect.height;
        image.raycastTarget = true;

        Image shade = CreateImage("LowerShade", root.transform, new Color(0.01f, 0.02f, 0.02f, 0.82f));
        RectTransform shadeRect = shade.rectTransform;
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = new Vector2(1f, 0.27f);
        shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;

        Text title = CreateText("Title", root.transform, "\u6700\u540e\u7684\u907f\u96be\u6240", font, 54, new Color(0.93f, 0.87f, 0.73f), TextAnchor.MiddleLeft);
        PlaceTitle(title.rectTransform, -210f, 86f);
        Text subtitle = CreateText("Subtitle", root.transform, "\u4e27\u5c38\u751f\u5b58", font, 22, new Color(0.48f, 0.72f, 0.7f), TextAnchor.MiddleLeft);
        PlaceTitle(subtitle.rectTransform, -305f, 44f);

        Image track = CreateImage("LoadingProgress", root.transform, new Color(0.17f, 0.23f, 0.22f, 1f));
        PlaceBottom(track.rectTransform, 136f, 10f);
        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        Image fill = CreateImage("Fill", track.transform, new Color(0.45f, 0.76f, 0.63f, 1f));
        Stretch(fill.rectTransform);
        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = fill;

        Text status = CreateText("Status", root.transform, "\u6b63\u5728\u542f\u52a8\u2026", font, 24, new Color(0.85f, 0.89f, 0.86f), TextAnchor.MiddleLeft);
        PlaceBottom(status.rectTransform, 178f, 46f);
        status.rectTransform.anchorMax = new Vector2(0.76f, 0f);
        Text percent = CreateText("Percent", root.transform, "0%", font, 28, new Color(0.45f, 0.76f, 0.63f), TextAnchor.MiddleRight);
        PlaceBottom(percent.rectTransform, 178f, 46f);
        percent.rectTransform.anchorMin = new Vector2(0.78f, 0f);

        var screen = root.GetComponent<ZombieBootLoadingScreen>();
        screen.progressSlider = slider;
        screen.statusLabel = status;
        screen.percentLabel = percent;

        GameObject cameraObject = new GameObject("StartupCamera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform, false);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 0;
        camera.depth = -100f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string content, Font font, int size, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void PlaceTitle(RectTransform rect, float y, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(TitleInset, y);
        rect.sizeDelta = new Vector2(660f, height);
    }

    private static void PlaceBottom(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0.09f, 0f);
        rect.anchorMax = new Vector2(0.91f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, height);
    }
}
#endif
