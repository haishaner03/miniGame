#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ZombieUiPrefabBuilder
{
    private const string PrefabFolder = "Assets/GameMain/UI/Prefabs";
    private const string ArtFolder = "Assets/GameMain/UI/Art";
    private const string FontPath = "Assets/GameMain/Res/Fonts/NotoSansSC/NotoSansCJKsc-Regular.otf";
    private const string FontBoldPath = FontPath;

    [MenuItem("Tools/Zombie/Build UGUI Main Menu Prefabs")]
    public static void Build()
    {
        EnsureFolder("Assets/GameMain/UI", "Prefabs");
        ConfigureSpriteImport(ArtFolder + "/ZombieMainMenu_Background.png", false);
        ConfigureSpriteImport(ArtFolder + "/ZombieUI_Icon_Play.png", true);
        ConfigureSpriteImport(ArtFolder + "/ZombieUI_Icon_ChapterMap.png", true);
        ConfigureSpriteImport(ArtFolder + "/ZombieUI_Icon_Settings.png", true);
        ConfigureSpriteImport(ArtFolder + "/ZombieUI_Icon_Safehouse.png", true);
        ConfigureSpriteImport(ArtFolder + "/ZombieUI_Icon_Lock.png", true);
        ConfigureSpriteImport(ArtFolder + "/ZombieUI_Icon_Back.png", true);
        AssetDatabase.Refresh();

        Font regular = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        Font bold = AssetDatabase.LoadAssetAtPath<Font>(FontBoldPath);
        Sprite background = LoadSprite("ZombieMainMenu_Background.png");
        Sprite play = LoadSprite("ZombieUI_Icon_Play.png");
        Sprite map = LoadSprite("ZombieUI_Icon_ChapterMap.png");
        Sprite settings = LoadSprite("ZombieUI_Icon_Settings.png");
        Sprite safehouse = LoadSprite("ZombieUI_Icon_Safehouse.png");
        Sprite lockIcon = LoadSprite("ZombieUI_Icon_Lock.png");
        Sprite back = LoadSprite("ZombieUI_Icon_Back.png");

        GameObject root = CreateRoot("ZombieMainMenu");
        BuildContents(root, regular, bold, background, play, map, settings, safehouse, lockIcon, back);
        SavePrefab(root, PrefabFolder + "/ZombieMainMenu.prefab");

        GameObject chapterRoot = Object.Instantiate(root);
        chapterRoot.name = "ZombieChapterSelect";
        ZombieUiFlow chapterFlow = chapterRoot.GetComponent<ZombieUiFlow>();
        if (chapterFlow != null)
        {
            chapterFlow.startOnChapterPanel = true;
            if (chapterFlow.mainPanel != null)
                chapterFlow.mainPanel.SetActive(false);
            if (chapterFlow.chapterPanel != null)
                chapterFlow.chapterPanel.SetActive(true);
        }
        SavePrefab(chapterRoot, PrefabFolder + "/ZombieChapterSelect.prefab");

        Object.DestroyImmediate(chapterRoot);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built ZombieMainMenu.prefab and ZombieChapterSelect.prefab with UGUI.");
    }

    private static GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ZombieUiFlow));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return root;
    }

    private static void BuildContents(GameObject root, Font regular, Font bold, Sprite background, Sprite play, Sprite map, Sprite settings, Sprite safehouse, Sprite lockIcon, Sprite back)
    {
        RectTransform rootRect = root.GetComponent<RectTransform>();
        GameObject bg = CreateImage("Background", root.transform, background, Color.white);
        SetFull(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().preserveAspect = false;
        bg.GetComponent<Image>().raycastTarget = false;

        GameObject shade = CreateImage("Shade", root.transform, null, new Color(0.015f, 0.03f, 0.05f, 0.2f));
        SetFull(shade.GetComponent<RectTransform>());
        shade.GetComponent<Image>().raycastTarget = false;

        GameObject mainPanel = CreateImage("MainPanel", root.transform, null, new Color(0.015f, 0.035f, 0.06f, 0.76f));
        RectTransform mainRect = mainPanel.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0f, 0f);
        mainRect.anchorMax = new Vector2(0.43f, 1f);
        mainRect.offsetMin = Vector2.zero;
        mainRect.offsetMax = Vector2.zero;

        GameObject accent = CreateImage("AccentLine", mainPanel.transform, null, new Color(0.95f, 0.36f, 0.18f, 0.9f));
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 5f);

        CreateMainText("Title", mainPanel.transform, "\u6700\u540e\u7684\u907f\u96be\u6240", bold, 54, new Color(0.93f, 0.87f, 0.73f, 1f), new Vector2(70f, -92f), new Vector2(620f, 86f), TextAnchor.MiddleLeft);
        CreateMainText("Subtitle", mainPanel.transform, "\u4e27\u5c38\u751f\u5b58 // \u7b2c\u4e00\u8857\u533a", regular, 18, new Color(0.48f, 0.72f, 0.7f, 1f), new Vector2(75f, -174f), new Vector2(620f, 42f), TextAnchor.MiddleLeft);
        CreateMainText("Tagline", mainPanel.transform, "\u62b5\u8fbe\u4e0b\u4e00\u5904\u5b89\u5168\u5c4b\u3002", regular, 20, new Color(0.78f, 0.78f, 0.72f, 0.85f), new Vector2(75f, -220f), new Vector2(600f, 40f), TextAnchor.MiddleLeft);

        Button start = CreateButton("StartButton", mainPanel.transform, play, "\u5f00\u59cb\u5192\u9669", new Vector2(70f, -292f), regular);
        Button chapters = CreateButton("ChapterButton", mainPanel.transform, map, "\u7ae0\u8282\u9009\u62e9", new Vector2(70f, -392f), regular);
        Button options = CreateButton("OptionsButton", mainPanel.transform, settings, "\u8bbe\u7f6e", new Vector2(70f, -492f), regular);
        Button quit = CreateButton("QuitButton", mainPanel.transform, back, "\u9000\u51fa\u6e38\u620f", new Vector2(70f, -592f), regular);
        CreateMainText("Footer", mainPanel.transform, "\u5bc2\u9759\u8857\u533a\uff0c\u5371\u673a\u56db\u4f0f\u3002\u7ee7\u7eed\u524d\u884c\u3002", regular, 16, new Color(0.52f, 0.56f, 0.55f, 0.9f), new Vector2(75f, 42f), new Vector2(620f, 34f), TextAnchor.MiddleLeft);

        GameObject chapterPanel = CreateImage("ChapterPanel", root.transform, null, new Color(0.02f, 0.045f, 0.065f, 0.93f));
        SetFull(chapterPanel.GetComponent<RectTransform>());
        CreateText("ChapterTitle", chapterPanel.transform, "\u7ae0\u8282\u9009\u62e9", bold, 54, new Color(0.93f, 0.87f, 0.73f, 1f), new Vector2(0f, -85f), new Vector2(900f, 80f), TextAnchor.MiddleCenter);
        CreateText("ChapterSubtitle", chapterPanel.transform, "\u9009\u62e9\u7a7f\u8d8a\u5e9f\u5f03\u8857\u533a\u7684\u8def\u7ebf", regular, 20, new Color(0.48f, 0.72f, 0.7f, 1f), new Vector2(0f, -150f), new Vector2(900f, 42f), TextAnchor.MiddleCenter);

        Button c1 = CreateCenteredButton("Chapter01", chapterPanel.transform, safehouse, "\u7b2c\u4e00\u7ae0 // \u5357\u90e8\u8857\u533a", new Vector2(-270f, 130f), regular);
        Button c2 = CreateCenteredButton("Chapter02", chapterPanel.transform, lockIcon, "\u7b2c\u4e8c\u7ae0 // \u96c6\u5e02\u5c0f\u5df7", new Vector2(270f, 130f), regular);
        Button c3 = CreateCenteredButton("Chapter03", chapterPanel.transform, lockIcon, "\u7b2c\u4e09\u7ae0 // \u79ef\u6c34\u5ead\u9662", new Vector2(-270f, -40f), regular);
        Button c4 = CreateCenteredButton("Chapter04", chapterPanel.transform, lockIcon, "\u7b2c\u56db\u7ae0 // \u5317\u90e8\u5927\u95e8", new Vector2(270f, -40f), regular);
        Button backButton = CreateCenteredButton("BackButton", chapterPanel.transform, back, "\u8fd4\u56de", new Vector2(0f, -275f), regular);

        ZombieUiFlow flow = root.GetComponent<ZombieUiFlow>();
        flow.mainPanel = mainPanel;
        flow.chapterPanel = chapterPanel;
        flow.startButton = start;
        flow.chapterButton = chapters;
        flow.optionsButton = options;
        flow.quitButton = quit;
        flow.backButton = backButton;
        flow.chapterButtons = new[] { c1, c2, c3, c4 };
        flow.chapterScenePaths = new[]
        {
            "Assets/GameMain/Scenes/Level/ZombieLevel01.unity",
            "Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity",
            "Assets/GameMain/Scenes/Level/ZombieLevel03/ZombieLevel03.unity",
            "Assets/GameMain/Scenes/Level/ZombieLevel04/ZombieLevel04.unity"
        };
        chapterPanel.SetActive(false);
        foreach (string name in new[] { "ChapterTitle", "ChapterSubtitle" })
        {
            RectTransform rect = chapterPanel.transform.Find(name).GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        }
    }

    private static Button CreateButton(string name, Transform parent, Sprite icon, string label, Vector2 position, Font font)
    {
        GameObject go = CreateImage(name, parent, null, new Color(0.06f, 0.13f, 0.16f, 0.92f));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(540f, 78f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        button.colors = MakeColors();
        AddIcon(go.transform, icon, new Vector2(45f, 0f), new Vector2(58f, 58f));
        RectTransform iconRect = go.transform.Find("Icon").GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        GameObject labelObject = CreateText("Label", go.transform, label, font, 24, new Color(0.9f, 0.88f, 0.76f, 1f), new Vector2(92f, 0f), new Vector2(420f, 78f), TextAnchor.MiddleLeft);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0f, 0.5f);
        return button;
    }

    private static Button CreateCenteredButton(string name, Transform parent, Sprite icon, string label, Vector2 position, Font font)
    {
        GameObject go = CreateImage(name, parent, null, new Color(0.06f, 0.13f, 0.16f, 0.94f));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(540f, 130f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        button.colors = MakeColors();
        AddIcon(go.transform, icon, new Vector2(-205f, 0f), new Vector2(92f, 92f));
        GameObject labelObject = CreateText("Label", go.transform, label, font, 21, new Color(0.9f, 0.88f, 0.76f, 1f), new Vector2(-130f, 0f), new Vector2(330f, 100f), TextAnchor.MiddleLeft);
        labelObject.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        return button;
    }

    private static ColorBlock MakeColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.06f, 0.13f, 0.16f, 0.92f);
        colors.highlightedColor = new Color(0.12f, 0.28f, 0.29f, 0.98f);
        colors.pressedColor = new Color(0.72f, 0.23f, 0.12f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static void AddIcon(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject go = CreateImage("Icon", parent, sprite, Color.white);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().preserveAspect = true;
    }

    private static GameObject CreateMainText(string name, Transform parent, string content, Font font, int fontSize, Color color, Vector2 position, Vector2 size, TextAnchor alignment)
    {
        GameObject go = CreateText(name, parent, content, font, fontSize, color, position, size, alignment);
        RectTransform rect = go.GetComponent<RectTransform>();
        Vector2 anchor = name == "Footer" ? Vector2.zero : new Vector2(0f, 1f);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        return go;
    }

    private static GameObject CreateText(string name, Transform parent, string content, Font font, int fontSize, Color color, Vector2 position, Vector2 size, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = go.GetComponent<Text>();
        text.text = content;
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return go;
    }

    private static void SetFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static Sprite LoadSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + fileName);
    }

    private static void ConfigureSpriteImport(string path, bool pointFilter)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.filterMode = pointFilter ? FilterMode.Point : FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void SavePrefab(GameObject root, string path)
    {
        AssetDatabase.DeleteAsset(path);
        PrefabUtility.SaveAsPrefabAsset(root, path);
    }
}
#endif

