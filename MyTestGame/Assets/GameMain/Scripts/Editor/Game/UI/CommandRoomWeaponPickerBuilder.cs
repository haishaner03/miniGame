#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CommandRoomMenuBuilder;
using Object = UnityEngine.Object;

/// <summary>Starting loadout built from the same metal surfaces, typography and focus feedback as the menu.</summary>
public static class CommandRoomWeaponPickerBuilder
{
    public const string PrefabPath = "Assets/GameMain/Prefabs/UI/Screens/StartingWeaponPicker.prefab";
    private const string ArtPath = "Assets/GameMain/Res/UI/Art/CommandRoom/Weapons/";
    private const string WeaponPath = "Assets/GameMain/Res/Weapons/";

    private static class Layout
    {
        public static readonly Vector2 Reference = new Vector2(2048, 1152);
        public static readonly Vector2 Panel = new Vector2(1544, 898);
        public static readonly Vector2 Card = new Vector2(344, 556);
        public const float Inset = 54, Gap = 20, CardTop = 210;
        public const float Title = 54, Name = 37, Stat = 31, Body = 22, Caption = 21;
        public const float CardInset = 26, ArtTop = 84, ArtHeight = 214, WeaponHeight = 204;
        public const float NameTop = 288, StatsTop = 350, DescriptionTop = 410, ActionTop = 470;
        public const float TextWidth = 292;
    }

    [MenuItem("Tools/Zombie/UI/Rebuild Command Room Weapon Picker")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before rebuilding the weapon picker.");
        AssetDatabase.Refresh();
        ImportWeaponArt();
        PrepareSharedUi();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var picker = root.GetComponent<StartingWeaponPicker>();
            if (picker == null) throw new InvalidOperationException("StartingWeaponPicker is missing.");
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 350;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Layout.Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var shade = Rect("Shade", root.transform); Full(shade);
            ImageAt(shade, null, new Color(.006f, .012f, .014f, .78f)).raycastTarget = true;

            var panel = Rect("Loadout", root.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = Layout.Panel;
            ImageAt(panel, null, Palette.Panel).raycastTarget = true;
            var rim = Rect("Frame", panel); Full(rim);
            ImageAt(rim, Sprite("Button_Frame"), Color.white).type = Image.Type.Sliced;

            Label("Title", panel, "选择起始武器", Layout.Title,
                new Vector2(Layout.Inset, 38), new Vector2(1000, 94)).fontStyle = FontStyles.Bold;
            Label("Subtitle", panel, "每局固定装备 · 升级决定你的打法", 25,
                new Vector2(Layout.Inset + 2, 139), new Vector2(1000, 46)).color = Palette.Muted;
            Label("Preparation", panel, "出发准备", 24, new Vector2(1250, 65), new Vector2(238, 46),
                TextAlignmentOptions.Right).color = Palette.Amber;
            Box("HeaderRule", panel, new Vector2(Layout.Inset, 189),
                new Vector2(Layout.Panel.x - 2 * Layout.Inset, 1), new Color(.72f, .57f, .34f, .35f));

            var cards = new StartingWeaponCardView[4];
            cards[0] = Card(panel, 0, "MacheteChoice", "Melee/Machete.asset", "Machete", "快速近战",
                "出手迅速，适合连续近战", Palette.Amber);
            cards[1] = Card(panel, 1, "IronBarChoice", "Melee/IronBar.asset", "IronBar", "击退控制",
                "攻击更长，击退更强", new Color(.73f, .78f, .76f));
            cards[2] = Card(panel, 2, "BowChoice", "SurvivorUnlocks/HuntingBow.asset", "HuntingBow", "穿透远程",
                "鼠标瞄准，基础穿透 1 只", Palette.Teal);
            cards[3] = Card(panel, 3, "HammerChoice", "SurvivorUnlocks/HeavyHammer.asset", "HeavyHammer", "范围重击",
                "大范围重击，强力击退", new Color(.88f, .52f, .32f));

            Button cancel = Surface(panel, "Cancel", new Vector2(Layout.Inset, 806),
                new Vector2(230, 66), "Button_Frame", Palette.Amber);
            Label("Label", cancel.transform, "←  返回", 28, new Vector2(18, 9),
                new Vector2(194, 46), TextAlignmentOptions.Center);
            Label("KeyboardHint", panel, "方向键选择 · Enter 装备 · Esc 返回", 22,
                new Vector2(530, 817), new Vector2(956, 42), TextAlignmentOptions.Right).color = Palette.Muted;
            picker.ConfigureChoices(cards[0].GetComponent<Button>(), cards[1].GetComponent<Button>(),
                cards[2].GetComponent<Button>(), cards[3].GetComponent<Button>(), cancel, cards);
            Link(cancel, cards[0].GetComponent<Button>(), cards[0].GetComponent<Button>(), null, null);
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("Command-room weapon picker rebuilt: four data-driven weapon cards, unlock states and shared menu controls.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static StartingWeaponCardView Card(Transform parent, int index, string name, string definitionPath,
        string artworkName, string role, string description, Color accent)
    {
        var definition = AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>(WeaponPath + definitionPath);
        if (definition == null) throw new FileNotFoundException(WeaponPath + definitionPath);
        var button = Surface(parent, name, new Vector2(Layout.Inset + index * (Layout.Card.x + Layout.Gap), Layout.CardTop),
            Layout.Card, "Mode_Frame", accent);
        var rect = (RectTransform)button.transform;
        Label("Role", rect, role, Layout.Caption, new Vector2(Layout.CardInset, 24),
            new Vector2(Layout.TextWidth, 42), TextAlignmentOptions.Center).color = accent;
        Box("ArtBackdrop", rect, new Vector2(20, Layout.ArtTop),
            new Vector2(Layout.Card.x - 40, Layout.ArtHeight), new Color(.10f, .13f, .13f, .50f));
        // A restrained rack grid gives the narrow pixel sprites a deliberate display area.
        for (int line = 0; line < 5; line++)
            Box("RackLine" + line, rect, new Vector2(30, Layout.ArtTop + 24 + line * 40),
                new Vector2(Layout.Card.x - 60, 1), new Color(accent.r, accent.g, accent.b, .08f));
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + artworkName + ".png");
        if (sprite == null) throw new FileNotFoundException(ArtPath + artworkName + ".png");
        var artwork = Rect("WeaponArtwork", rect);
        artwork.anchorMin = artwork.anchorMax = artwork.pivot = new Vector2(.5f, .5f);
        artwork.anchoredPosition = new Vector2(0, Layout.Card.y * .5f - Layout.ArtTop - Layout.ArtHeight * .5f);
        artwork.sizeDelta = new Vector2(Layout.WeaponHeight * sprite.rect.width / sprite.rect.height, Layout.WeaponHeight);
        artwork.localEulerAngles = new Vector3(0, 0, -24);
        var image = ImageAt(artwork, sprite, Color.white); image.preserveAspect = true;

        var view = rect.gameObject.AddComponent<StartingWeaponCardView>();
        view.definition = definition; view.artwork = image;
        view.availableColor = accent; view.unavailableColor = Palette.Muted;
        view.weaponName = Label("Name", rect, definition.displayName, Layout.Name,
            new Vector2(Layout.CardInset, Layout.NameTop), new Vector2(Layout.TextWidth, 58), TextAlignmentOptions.Center);
        view.weaponName.fontStyle = FontStyles.Bold;
        Label("DamageCaption", rect, "基础伤害", 18, new Vector2(Layout.CardInset, Layout.StatsTop), new Vector2(140, 32)).color = Palette.Muted;
        Label("CooldownCaption", rect, "攻击间隔", 18, new Vector2(182, Layout.StatsTop), new Vector2(138, 32)).color = Palette.Muted;
        view.damage = Label("Damage", rect, "0", Layout.Stat, new Vector2(Layout.CardInset, Layout.StatsTop + 30), new Vector2(138, 48));
        view.cooldown = Label("Cooldown", rect, "0.00 秒", 27, new Vector2(182, Layout.StatsTop + 30), new Vector2(138, 48));
        var feature = Label("Description", rect, description, Layout.Body,
            new Vector2(Layout.CardInset, Layout.DescriptionTop), new Vector2(Layout.TextWidth, 52), TextAlignmentOptions.Center);
        feature.color = Palette.Muted;
        feature.enableWordWrapping = true;
        feature.overflowMode = TextOverflowModes.Overflow;
        view.statusLine = Box("ActionRule", rect, new Vector2(Layout.CardInset, Layout.ActionTop - 8), new Vector2(Layout.TextWidth, 1), accent);
        view.action = Label("Action", rect, "装备出发  →", 26,
            new Vector2(Layout.CardInset, Layout.ActionTop), new Vector2(Layout.TextWidth, 48), TextAlignmentOptions.Center);
        view.unlockHint = Label("UnlockHint", rect, "", 19,
            new Vector2(Layout.CardInset, Layout.ActionTop + 42), new Vector2(Layout.TextWidth, 40), TextAlignmentOptions.Center);
        view.unlockHint.color = Palette.Muted;
        view.RefreshAvailability();
        rect.Find("Frame").SetAsLastSibling();
        rect.Find("Highlight").SetAsLastSibling();
        return view;
    }

    private static void ImportWeaponArt()
    {
        foreach (string path in Directory.GetFiles(ArtPath, "*.png"))
        {
            var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
    }
}
#endif
