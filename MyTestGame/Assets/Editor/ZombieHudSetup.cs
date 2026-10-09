using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Imports the generated icon assets and saves an editable UGUI hierarchy.</summary>
public static class ZombieHudSetup
{
    public const string PrefabPath = "Assets/GameMain/UI/Prefabs/ZombieBattleHUD.prefab";
    private static readonly string[] IconNames =
    {
        "Icon_Health", "Icon_Melee", "Icon_Dash", "Icon_Time", "Icon_Kills", "Icon_Room", "Icon_Pause", "Icon_Retry",
        "Upgrade_Damage", "Upgrade_Range", "Upgrade_AttackSpeed", "Upgrade_MoveSpeed", "Upgrade_MaxHealth",
        "Upgrade_DashCooldown", "Upgrade_DashDistance", "Upgrade_LifeSteal"
    };

    [MenuItem("Tools/Zombie/Build Battle HUD")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding the HUD.");
        var config = AssetDatabase.LoadAssetAtPath<ZombieRunConfig>("Assets/GameMain/Resources/ZombieRunConfig.asset");
        if (config == null) throw new InvalidOperationException("Run config is missing.");
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/GameMain/Res/Fonts/NotoSansSC/NotoSansCJKsc-Regular.otf");
        if (font == null) throw new InvalidOperationException("Chinese HUD font is missing.");
        var icons = new Sprite[IconNames.Length + 1];
        for (int i = 0; i < IconNames.Length; i++)
        {
            string path = "Assets/GameMain/UI/Art/ZombieHUD/" + IconNames[i] + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Missing generated icon", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (icons[i] == null) throw new InvalidOperationException("Failed to import icon " + path);
        }
        GameObject root = new GameObject("ZombieBattleHUD");
        try
        {
            root.AddComponent<ZombieRunUI>().BuildLayout(font, icons);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Could not save HUD prefab.");
            config.battleUiPrefab = prefab;
            config.uiFont = font;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("Battle HUD ready: " + PrefabPath + " (16 generated sprites, Chinese font, UGUI).");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
