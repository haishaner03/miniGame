using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine.TextCore.LowLevel;
using UnityEngine;

/// <summary>Imports the generated icon assets and saves an editable UGUI hierarchy.</summary>
public static class ZombieHudSetup
{
    public const string PrefabPath = "Assets/GameMain/Prefabs/UI/Screens/ZombieBattleHUD.prefab";
    public const string ArtPath = "Assets/GameMain/Res/UI/Art/BattleHUD/";
    private const string FontPath = "Assets/GameMain/Res/Fonts/NotoSansSC/NotoSansSC_BattleSDF.asset";
    private static readonly string[] IconNames =
    {
        "Icon_Health", "Icon_Melee", "Icon_Dash", "Icon_Time", "Icon_Kills", "Icon_Room", "Icon_Pause", "Icon_Retry",
        "Upgrade_Damage", "Upgrade_Range", "Upgrade_AttackSpeed", "Upgrade_MoveSpeed", "Upgrade_MaxHealth",
        "Upgrade_DashCooldown", "Upgrade_DashDistance", "Upgrade_LifeSteal", "Upgrade_Frost", "Upgrade_Fire",
        "Upgrade_Frostfire", "Icon_Bow", "Icon_IronBar", "Icon_Hammer", "Upgrade_FrostShield", "Upgrade_Burst"
    };
    private static readonly string[] ChromeNames = { "Panel_Brass", "Slot_Brass", "Cooldown_Ring", "Bar_Fill", "Portrait_Survivor" };

    [MenuItem("Tools/Zombie/Build Battle HUD")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding the HUD.");
        var config = AssetDatabase.LoadAssetAtPath<ZombieRunConfig>("Assets/GameMain/Resources/ZombieRunConfig.asset");
        if (config == null) throw new InvalidOperationException("Run config is missing.");
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/GameMain/Res/Fonts/NotoSansSC/NotoSansCJKsc-Regular.otf");
        if (font == null) throw new InvalidOperationException("Chinese HUD font is missing.");
        var icons = new Sprite[IconNames.Length];
        for (int i = 0; i < IconNames.Length; i++)
            icons[i] = ImportSprite(IconNames[i], false);
        var chrome = new Sprite[ChromeNames.Length];
        for (int i = 0; i < ChromeNames.Length; i++)
            chrome[i] = ImportSprite(ChromeNames[i], i < 2);
        var textFont = PrepareFont(font);
        GameObject root = new GameObject("ZombieBattleHUD");
        try
        {
            root.AddComponent<ZombieRunUI>().BuildLayout(font, icons, textFont, chrome);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Could not save HUD prefab.");
            config.battleUiPrefab = prefab;
            config.uiFont = font;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("Battle HUD ready: " + PrefabPath + " (24 icons, 5 skin sprites, Chinese TMP, UGUI).");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Sprite ImportSprite(string name, bool sliced)
    {
        string path = ArtPath + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Missing HUD artwork", path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = sliced ? Vector4.one * 12f : Vector4.zero;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Failed to import " + path);
        return sprite;
    }

    private static TMP_FontAsset PrepareFont(Font font)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(font, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = "NotoSansSC Battle SDF";
            AssetDatabase.CreateAsset(asset, FontPath);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
        // Prewarm the actual HUD/card text to avoid generating Chinese glyphs during a fight.
        string characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ×↔←→·%/+[]:.-\n";
        foreach (string source in Directory.GetFiles("Assets/GameMain/Scripts/UI/Battle", "*.cs"))
            characters += File.ReadAllText(source);
        foreach (string source in Directory.GetFiles("Assets/GameMain", "RunUpgrade.txt", SearchOption.AllDirectories))
            characters += File.ReadAllText(source);
        string missing;
        asset.TryAddCharacters(characters, out missing);
        foreach (var texture in asset.atlasTextures)
            if (texture != null && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, asset);
        EditorUtility.SetDirty(asset);
        return asset;
    }
}
