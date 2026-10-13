#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Rebuild only the menu presentation while retaining its asset GUID and game-flow references.</summary>
public static class CommandRoomMenuBuilder
{
    private const string Art = "Assets/GameMain/Res/UI/Art/CommandRoom/";
    private const string Screens = "Assets/GameMain/Prefabs/UI/Screens/";
    private const string FontSource = "Assets/GameMain/Res/Fonts/NotoSansSC/NotoSansCJKsc-Regular.otf";
    private const string FontAssetPath = "Assets/GameMain/Res/Fonts/NotoSansSC/NotoSansSC_MenuSDF.asset";
    private static TMP_FontAsset font;

    // All geometry is expressed on this reference stage. Expand scaling keeps it
    // fully visible on 4:3 and ultrawide screens; the background independently fills.
    private static class Layout
    {
        public static readonly Vector2 Reference = new Vector2(2048f, 1152f);
        public static readonly Vector2 ModeSize = new Vector2(764f, 306f);
        public static readonly Vector2 SecondarySize = new Vector2(392f, 110f);
        public const float ModeLeft = 244f, ModeTop = 650f, ModeGap = 36f;
        public const float SecondaryLeft = 400f, SecondaryTop = 994f, SecondaryGap = 38f;
        public const float CardInset = 18f, PreviewWidth = 400f, CardTextLeft = 444f;
        public const float TitleSize = 128f, ModeTitleSize = 52f, ButtonSize = 34f, BodySize = 20f;
        public static readonly Vector2 DialogSize = new Vector2(780f, 700f);
    }

    internal static class Palette
    {
        public static readonly Color Ivory = new Color(.94f, .88f, .74f);
        public static readonly Color Muted = new Color(.70f, .72f, .67f);
        public static readonly Color Amber = new Color(.94f, .64f, .24f);
        public static readonly Color Teal = new Color(.56f, .80f, .78f);
        public static readonly Color Panel = new Color(.045f, .052f, .049f, .98f);
        public static readonly Color Track = new Color(.16f, .19f, .18f);
    }

    internal static void PrepareSharedUi() { font = PrepareFont(); }

    [MenuItem("Tools/Zombie/UI/Rebuild Command Room Main Menu")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before rebuilding menu prefabs.");
        if (!File.Exists(Art + "MainMenu_Background.png"))
            throw new FileNotFoundException("Command-room art has not been prepared.");
        AssetDatabase.Refresh();
        ImportArt();
        font = PrepareFont();
        UpgradePrefab(Screens + "ZombieMainMenu.prefab");
        UpgradePrefab(Screens + "ZombieChapterSelect.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("Command-room main menu rebuilt. Mode, chapter, progression, leaderboard and loadout references preserved.");
    }

    private static void ImportArt()
    {
        foreach (string path in Directory.GetFiles(Art, "*.png"))
        {
            var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = path.Contains("Background") ? FilterMode.Bilinear : FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spriteBorder = path.Contains("Mode_Frame") ? new Vector4(38, 38, 38, 38)
                : path.Contains("Button_Frame") ? new Vector4(27, 27, 27, 27) : Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    private static TMP_FontAsset PrepareFont()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (asset == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
            if (source == null) throw new FileNotFoundException(FontSource);
            asset = TMP_FontAsset.CreateFontAsset(source, 90, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = "NotoSansSC Menu SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
        string characters = "0123456789 %·—/\\.,:!?ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            "最后的避难所闯关模式无尽章节选择生存榜幸者成长设置退出游戏点数" +
            "穿越街区抵达庇护坚持更久挑战纪录装备武器探索城活着回来" +
            "音频总量背景音乐战斗效界面按钮实时预览关闭后自动保存并返回" +
            "离开？当前没有进行中的对局确定要主菜单取消确认" +
            "出发准备每局固定装备升级决定你的打法快速近战击退控制穿透远程范围重击" +
            "砍刀铁棍猎弓锤基础伤害攻击间隔秒迅连续更长强鼠标瞄准只大尚未解锁" +
            "幸存者成长方向键选择返回←→" +
            "出手迅速，适合连续近战攻击更长，击退更强鼠标瞄准，基础穿透 1 只" +
            "大范围重击，强力击退尚未解锁幸存者成长解锁 · 120 点装备出发 →";
        asset.TryAddCharacters(characters, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("Menu font missing glyphs: " + missing);
        foreach (var texture in asset.atlasTextures)
            if (texture != null && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, asset);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void UpgradePrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var flow = root.GetComponent<ZombieUiFlow>();
            if (flow == null) throw new InvalidOperationException(path + " has no ZombieUiFlow.");
            // The older standalone chapter prefab predates growth/endless mode.
            // Its new main page needs the same external screens as the boot menu.
            if (path.EndsWith("ZombieChapterSelect.prefab", StringComparison.Ordinal))
            {
                var menu = AssetDatabase.LoadAssetAtPath<GameObject>(Screens + "ZombieMainMenu.prefab").GetComponent<ZombieUiFlow>();
                if (flow.weaponPickerPrefab == null) flow.weaponPickerPrefab = menu.weaponPickerPrefab;
                if (flow.growthShopPrefab == null) flow.growthShopPrefab = menu.growthShopPrefab;
                if (flow.leaderboardPrefab == null) flow.leaderboardPrefab = menu.leaderboardPrefab;
            }
            if (flow.mainPanel != null) Object.DestroyImmediate(flow.mainPanel);
            RemoveChild(root.transform, "Background");
            RemoveChild(root.transform, "Shade");

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Layout.Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var bg = Rect("Background", root.transform);
            ImageAt(bg, Sprite("MainMenu_Background"), Color.white);
            bg.anchorMin = bg.anchorMax = bg.pivot = new Vector2(.5f, .5f);
            bg.sizeDelta = Layout.Reference;
            var aspect = bg.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = Layout.Reference.x / Layout.Reference.y;
            bg.SetAsFirstSibling();

            var main = Rect("MainPanel", root.transform); Full(main);
            var stage = Rect("CommandRoomStage", main);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f);
            stage.sizeDelta = Layout.Reference;
            CanvasGroup navigation = stage.gameObject.AddComponent<CanvasGroup>();

            var title = Label("GameTitle", stage, "最后的避难所", Layout.TitleSize, new Vector2(72, 40), new Vector2(910, 210));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = -3f;
            Picture("TitleDivider", stage, "Title_Divider", new Vector2(72, 238), new Vector2(827, 51));
            flow.survivorPoints = null;
            flow.survivorPointsLabel = Label("SurvivorPoints", stage, "幸存者点数  0", 23f, new Vector2(82, 298), new Vector2(700, 42));
            flow.survivorPointsLabel.color = Palette.Muted;

            Button campaign = ModeCard(stage, "StartButton", "闯关模式", "穿越街区 · 抵达庇护所", "Campaign", Layout.ModeLeft, Palette.Amber);
            Button endless = ModeCard(stage, "EndlessButton", "无尽模式", "坚持更久 · 挑战生存纪录", "Endless", Layout.ModeLeft + Layout.ModeSize.x + Layout.ModeGap, Palette.Teal);
            Button chapters = Secondary(stage, "ChapterButton", "章节选择", "Icon_Chapters", 0);
            Button leaderboard = Secondary(stage, "LeaderboardButton", "无尽生存榜", "Icon_Leaderboard", 1);
            Button growth = Secondary(stage, "GrowthButton", "幸存者成长", "Icon_Growth", 2);
            Button options = Compact(stage, "OptionsButton", "设置", "Icon_Settings", new Vector2(1552, 32), new Vector2(196, 80));
            Button quit = Compact(stage, "QuitButton", "退出游戏", "Icon_Exit", new Vector2(1768, 32), new Vector2(240, 80));

            flow.mainPanel = main.gameObject;
            flow.startButton = campaign; flow.endlessButton = endless;
            flow.chapterButton = chapters; flow.leaderboardButton = leaderboard; flow.growthButton = growth;
            flow.optionsButton = options; flow.quitButton = quit;

            var view = root.GetComponent<CommandRoomMenuView>();
            if (view == null) view = root.AddComponent<CommandRoomMenuView>();
            view.flow = flow; view.navigation = navigation; view.defaultButton = campaign;
            if (flow.chapterPanel != null)
            {
                view.chapterNavigation = flow.chapterPanel.GetComponent<CanvasGroup>();
                if (view.chapterNavigation == null) view.chapterNavigation = flow.chapterPanel.AddComponent<CanvasGroup>();
            }
            BuildDialogs(main, view);

            Link(campaign, chapters, options, endless, endless);
            Link(endless, leaderboard, quit, campaign, campaign);
            Link(chapters, campaign, campaign, growth, leaderboard);
            Link(leaderboard, endless, endless, chapters, growth);
            Link(growth, endless, endless, leaderboard, chapters);
            Link(options, campaign, campaign, quit, quit);
            Link(quit, endless, endless, options, options);

            // The existing chapter page and its scene paths are intentionally retained.
            if (flow.chapterPanel != null)
            {
                flow.chapterPanel.transform.SetAsLastSibling();
                flow.chapterPanel.SetActive(flow.startOnChapterPanel);
            }
            main.gameObject.SetActive(!flow.startOnChapterPanel);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Button ModeCard(Transform parent, string name, string caption, string description, string art, float left, Color accent)
    {
        Button button = Surface(parent, name, new Vector2(left, Layout.ModeTop), Layout.ModeSize, "Mode_Frame", accent);
        var rect = (RectTransform)button.transform;
        Picture("Illustration", rect, art + "_Illustration", new Vector2(Layout.CardInset, Layout.CardInset),
            new Vector2(Layout.PreviewWidth, Layout.ModeSize.y - Layout.CardInset * 2f), false);
        var divider = Box("Divider", rect, new Vector2(Layout.PreviewWidth + Layout.CardInset + 12, 26), new Vector2(1, Layout.ModeSize.y - 52), accent * .55f);
        divider.color = new Color(accent.r, accent.g, accent.b, .35f);
        Picture("Icon", rect, "Icon_" + art, new Vector2(490, 30), new Vector2(220, 126));
        Label("Label", rect, caption, Layout.ModeTitleSize, new Vector2(Layout.CardTextLeft, 157), new Vector2(296, 90), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        var text = Label("Description", rect, description, 18f, new Vector2(Layout.CardTextLeft - 12, 248), new Vector2(318, 32), TextAlignmentOptions.Center);
        text.color = Palette.Muted;
        // Frame stays above the vignette; its transparent interior never intercepts input.
        rect.Find("Frame").SetAsLastSibling();
        rect.Find("Highlight").SetAsLastSibling();
        return button;
    }

    private static Button Secondary(Transform parent, string name, string caption, string icon, int index)
    {
        float left = Layout.SecondaryLeft + index * (Layout.SecondarySize.x + Layout.SecondaryGap);
        Button button = Surface(parent, name, new Vector2(left, Layout.SecondaryTop), Layout.SecondarySize, "Button_Frame", Palette.Amber);
        Picture("Icon", button.transform, icon, new Vector2(26, 17), new Vector2(86, 76));
        Label("Label", button.transform, caption, Layout.ButtonSize, new Vector2(126, 24), new Vector2(252, 62), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        return button;
    }

    private static Button Compact(Transform parent, string name, string caption, string icon, Vector2 position, Vector2 size)
    {
        Button button = Surface(parent, name, position, size, "Button_Frame", Palette.Amber);
        Picture("Icon", button.transform, icon, new Vector2(20, 15), new Vector2(52, 50));
        Label("Label", button.transform, caption, 29f, new Vector2(80, 16), new Vector2(size.x - 92, 48), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        return button;
    }

    internal static Button Surface(Transform parent, string name, Vector2 position, Vector2 size, string frame, Color accent)
    {
        var rect = Rect(name, parent); Place(rect, position, size);
        Image image = ImageAt(rect, null, Palette.Panel);
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.4f, 1.35f, 1.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.72f, .72f, .67f);
        colors.disabledColor = new Color(.4f, .4f, .4f, .65f);
        colors.fadeDuration = .10f;
        button.colors = colors;
        UiButtonSound.Ensure(button);
        var border = Rect("Frame", rect); Full(border);
        Image edge = ImageAt(border, Sprite(frame), Color.white); edge.type = Image.Type.Sliced;
        var glow = Rect("Highlight", rect); Full(glow);
        Image highlight = ImageAt(glow, Sprite(frame == "Mode_Frame" ? "Mode_Frame_Glow" : frame), new Color(accent.r, accent.g, accent.b, 0f));
        highlight.type = Image.Type.Sliced;
        var feedback = rect.gameObject.AddComponent<CommandRoomButtonFeedback>();
        feedback.highlight = highlight;
        return button;
    }

    private static void BuildDialogs(Transform main, CommandRoomMenuView view)
    {
        var overlay = Rect("DialogLayer", main); Full(overlay);
        ImageAt(overlay, null, new Color(.006f, .012f, .014f, .86f)).raycastTarget = true;
        view.dialogLayer = overlay.gameObject;

        var settings = DialogPanel("AudioSettings", overlay, Layout.DialogSize);
        view.settingsPanel = settings.gameObject;
        Label("Title", settings, "音频设置", 42f, new Vector2(58, 36), new Vector2(660, 66)).fontStyle = FontStyles.Bold;
        Label("Subtitle", settings, "实时预览 · 关闭后自动保存", Layout.BodySize, new Vector2(62, 112), new Vector2(650, 40)).color = Palette.Muted;
        view.master = VolumeRow(settings, "MasterVolume", "总音量", 190, out var masterValue);
        view.music = VolumeRow(settings, "MusicVolume", "背景音乐", 280, out var musicValue);
        view.effects = VolumeRow(settings, "EffectsVolume", "战斗音效", 370, out var effectsValue);
        view.ui = VolumeRow(settings, "UiVolume", "界面音效", 460, out var uiValue);
        view.masterValue = masterValue; view.musicValue = musicValue; view.effectsValue = effectsValue; view.uiValue = uiValue;
        view.settingsClose = DialogButton(settings, "CloseSettings", "保存并返回", new Vector2(234, 584), new Vector2(312, 78));
        Link(view.master, view.music, view.settingsClose, null, null);
        Link(view.music, view.effects, view.master, null, null);
        Link(view.effects, view.ui, view.music, null, null);
        Link(view.ui, view.settingsClose, view.effects, null, null);
        Link(view.settingsClose, view.master, view.ui, null, null);

        var quit = DialogPanel("QuitConfirmation", overlay, new Vector2(780, 420));
        view.quitPanel = quit.gameObject;
        Label("Title", quit, "离开避难所？", 42f, new Vector2(62, 48), new Vector2(650, 66)).fontStyle = FontStyles.Bold;
        Label("Subtitle", quit, "确定要退出游戏吗？", 26f, new Vector2(65, 150), new Vector2(650, 48)).color = Palette.Muted;
        view.quitCancel = DialogButton(quit, "CancelQuit", "返回主菜单", new Vector2(58, 284), new Vector2(312, 78));
        view.quitConfirm = DialogButton(quit, "ConfirmQuit", "退出游戏", new Vector2(410, 284), new Vector2(312, 78));
        Link(view.quitCancel, view.quitConfirm, view.quitConfirm, view.quitConfirm, view.quitConfirm);
        Link(view.quitConfirm, view.quitCancel, view.quitCancel, view.quitCancel, view.quitCancel);
        settings.gameObject.SetActive(false);
        quit.gameObject.SetActive(false);
        overlay.gameObject.SetActive(false);
    }

    private static RectTransform DialogPanel(string name, Transform parent, Vector2 size)
    {
        var panel = Rect(name, parent);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.sizeDelta = size;
        ImageAt(panel, null, Palette.Panel).raycastTarget = true;
        var rim = Rect("Frame", panel); Full(rim);
        var image = ImageAt(rim, Sprite("Button_Frame"), Color.white); image.type = Image.Type.Sliced;
        return panel;
    }

    private static Button DialogButton(Transform parent, string name, string caption, Vector2 position, Vector2 size)
    {
        Button button = Surface(parent, name, position, size, "Button_Frame", Palette.Amber);
        Label("Label", button.transform, caption, 29f, new Vector2(18, 12), size - new Vector2(36, 24), TextAlignmentOptions.Center);
        return button;
    }

    private static Slider VolumeRow(Transform parent, string name, string caption, float top, out TMP_Text value)
    {
        Label(name + "Label", parent, caption, 27f, new Vector2(62, top - 12), new Vector2(180, 55));
        value = Label(name + "Value", parent, "0%", 25f, new Vector2(648, top - 12), new Vector2(92, 55), TextAlignmentOptions.Right);
        var rect = Rect(name, parent); Place(rect, new Vector2(250, top), new Vector2(370, 34));
        var track = Rect("Track", rect);
        track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f);
        track.sizeDelta = new Vector2(-10, 8); ImageAt(track, null, Palette.Track);
        var fillArea = Rect("FillArea", rect);
        fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f);
        fillArea.sizeDelta = new Vector2(-24, 8);
        var fill = Rect("Fill", fillArea); Full(fill); ImageAt(fill, null, Palette.Amber);
        var handleArea = Rect("HandleArea", rect); Full(handleArea);
        handleArea.offsetMin = new Vector2(12, 0); handleArea.offsetMax = new Vector2(-12, 0);
        var handle = Rect("Handle", handleArea);
        handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(.5f, .5f);
        handle.sizeDelta = new Vector2(20, 30);
        var graphic = ImageAt(handle, null, Palette.Ivory); graphic.raycastTarget = true;
        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.targetGraphic = graphic; slider.fillRect = fill; slider.handleRect = handle;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = .5f;
        // The track is a generous target, so clicking any position sets the volume.
        var hit = ImageAt(rect, null, Color.clear); hit.raycastTarget = true;
        return slider;
    }

    internal static void Link(Selectable control, Selectable down, Selectable up, Selectable left, Selectable right)
    {
        control.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnDown = down, selectOnUp = up, selectOnLeft = left, selectOnRight = right };
    }

    internal static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Vector2 position, Vector2 dimensions,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var rect = Rect(name, parent); Place(rect, position, dimensions);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size;
        label.color = Palette.Ivory; label.alignment = alignment;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.extraPadding = true;
        return label;
    }

    internal static void Picture(string name, Transform parent, string art, Vector2 position, Vector2 dimensions, bool preserveAspect = true)
    {
        var rect = Rect(name, parent); Place(rect, position, dimensions);
        var image = ImageAt(rect, Sprite(art), Color.white);
        image.preserveAspect = preserveAspect;
    }

    internal static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var rect = Rect(name, parent); Place(rect, position, size);
        return ImageAt(rect, null, color);
    }

    internal static Image ImageAt(RectTransform rect, Sprite sprite, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = false;
        return image;
    }

    internal static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    internal static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(position.x, -position.y);
        rect.sizeDelta = size;
    }

    internal static void Full(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    internal static Sprite Sprite(string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
        if (asset == null) throw new FileNotFoundException(Art + name + ".png");
        return asset;
    }

    private static void RemoveChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }
}
#endif
