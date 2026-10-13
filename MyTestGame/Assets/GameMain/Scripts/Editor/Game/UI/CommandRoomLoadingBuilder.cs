#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CommandRoomMenuBuilder;
using Object = UnityEngine.Object;

/// <summary>Fullscreen scene transition with independently editable TMP labels and UGUI slider.</summary>
public static class CommandRoomLoadingBuilder
{
    public const string PrefabPath = "Assets/GameMain/Prefabs/UI/Screens/ZombieSceneLoading.prefab";
    public const string BackgroundPath = "Assets/GameMain/Res/UI/Art/CommandRoom/Loading/SceneLoading_Background.png";
    private static class Layout
    {
        public static readonly Vector2 Reference = new Vector2(2048, 1152);
        public const float Edge = 92, FooterTop = 878, FooterHeight = 274;
        public const float BarTop = 1000, BarWidth = 1864, BarHeight = 24;
        public const float TitleSize = 48, DestinationSize = 36, StatusSize = 24, HintSize = 22;
    }

    [MenuItem("Tools/Zombie/UI/Rebuild Command Room Loading Screen")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before rebuilding the loading screen.");
        if (!File.Exists(BackgroundPath)) throw new FileNotFoundException(BackgroundPath);
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        PrepareSharedUi();
        bool existing = File.Exists(PrefabPath);
        var root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath)
            : new GameObject("ZombieSceneLoading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Layout.Reference; scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var view = root.GetComponent<ZombieSceneLoadingScreen>();
            if (view == null) view = root.AddComponent<ZombieSceneLoadingScreen>();
            var blocker = Rect("InputBlocker", root.transform); Full(blocker);
            ImageAt(blocker, null, Color.black).raycastTarget = true;
            var background = Rect("Background", root.transform);
            background.anchorMin = background.anchorMax = background.pivot = new Vector2(.5f, .5f);
            background.sizeDelta = Layout.Reference;
            ImageAt(background, AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath), Color.white);
            var aspect = background.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; aspect.aspectRatio = 16f / 9f;
            var stage = Rect("Stage", root.transform);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f); stage.sizeDelta = Layout.Reference;
            Label("GameTitle", stage, "最后的避难所", Layout.TitleSize, new Vector2(Layout.Edge, 64), new Vector2(1100, 90)).fontStyle = FontStyles.Bold;
            Box("TitleAccent", stage, new Vector2(Layout.Edge, 172), new Vector2(170, 3), Palette.Amber);

            // Footer spans the whole viewport, including extra width on ultrawide screens.
            var footer = Rect("FooterShade", root.transform);
            footer.anchorMin = new Vector2(0, 0); footer.anchorMax = new Vector2(1, 0); footer.pivot = new Vector2(.5f, 0);
            footer.anchoredPosition = Vector2.zero; footer.sizeDelta = new Vector2(0, Layout.FooterHeight);
            ImageAt(footer, null, new Color(.012f, .018f, .019f, .78f));
            footer.SetSiblingIndex(2);
            view.destinationLabel = Label("Destination", stage, "闯关模式 · 房间 1 / 5", Layout.DestinationSize,
                new Vector2(Layout.Edge, 916), new Vector2(1230, 64));
            view.destinationLabel.fontStyle = FontStyles.Bold;
            view.statusLabel = Label("Status", stage, "正在加载街区...", Layout.StatusSize,
                new Vector2(1330, 932), new Vector2(520, 44), TextAlignmentOptions.Right);
            view.statusLabel.color = Palette.Muted;
            view.percentLabel = Label("Percent", stage, "0%", 27, new Vector2(1850, 928), new Vector2(110, 50), TextAlignmentOptions.Right);
            view.percentLabel.color = Palette.Amber;

            var progress = Rect("ProgressSlider", stage);
            Place(progress, new Vector2(Layout.Edge, Layout.BarTop), new Vector2(Layout.BarWidth, Layout.BarHeight));
            Box("Track", progress, Vector2.zero, new Vector2(Layout.BarWidth, Layout.BarHeight), Palette.Track);
            Box("TrackTop", progress, Vector2.zero, new Vector2(Layout.BarWidth, 1), new Color(.48f, .48f, .40f, .7f));
            var area = Rect("FillArea", progress); Full(area);
            area.offsetMin = new Vector2(3, 3); area.offsetMax = new Vector2(-3, -3);
            var fill = Rect("Fill", area); Full(fill);
            var fillImage = ImageAt(fill, null, Palette.Amber);
            var slider = progress.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.targetGraphic = fillImage;
            slider.minValue = 0; slider.maxValue = 1; slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = false; slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.SetValueWithoutNotify(0f); view.progressSlider = slider;
            Label("Hint", stage, "保持移动，留意退路。", Layout.HintSize,
                new Vector2(Layout.Edge, 1058), new Vector2(1450, 46)).color = Palette.Muted;
            var back = Surface(stage, "FailureReturn", new Vector2(1510, 1050), new Vector2(446, 68), "Button_Frame", Palette.Amber);
            Label("Label", back.transform, "返回主菜单", 26, new Vector2(22, 10), new Vector2(402, 48), TextAlignmentOptions.Center);
            back.gameObject.SetActive(false); view.failureReturn = back;
            root.SetActive(true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            var config = AssetDatabase.LoadAssetAtPath<ZombieRunConfig>("Assets/GameMain/Resources/ZombieRunConfig.asset");
            config.sceneLoadingPrefab = prefab; config.minimumLoadingSeconds = 1.5f;
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log("Scene loading prefab created and configured for adventure, endless and room transitions (minimum 1.5s).");
        }
        finally
        {
            if (existing) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
        }
    }
}
#endif
