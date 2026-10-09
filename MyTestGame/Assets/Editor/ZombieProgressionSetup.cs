using System;
using UnityEditor;
using UnityEngine;

public static class ZombieProgressionSetup
{
    public const string PrefabPath = "Assets/GameMain/Entity/Drops/ExperiencePickup.prefab";

    [MenuItem("Tools/Zombie/Setup Experience Progression")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before setup.");
        const string art = "Assets/GameMain/Res/Drops/ExperienceShard.png";
        AssetDatabase.ImportAsset(art);
        var importer = (TextureImporter)AssetImporter.GetAtPath(art);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 80f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        System.IO.Directory.CreateDirectory("Assets/GameMain/Entity/Drops");
        AssetDatabase.Refresh();
        var root = new GameObject("ExperiencePickup");
        try
        {
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art);
            renderer.sortingOrder = 10;
            var pickup = root.AddComponent<ExperiencePickup>();
            var serialized = new SerializedObject(pickup);
            serialized.FindProperty("visual").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var config = AssetDatabase.LoadAssetAtPath<ZombieRunConfig>("Assets/GameMain/Resources/ZombieRunConfig.asset");
            config.experiencePickupPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            config.firstLevelExperience = 80;
            config.experienceGrowthPerLevel = 30;
            config.normalExperience = 10;
            config.eliteExperience = 30;
            config.experiencePoolPrewarm = 64;
            config.initialWanderers = 40;
            config.maxLivingZombies = 100;
            config.reinforcementDelay = new Vector2(2f, 4f);
            config.reinforcementCount = new Vector2Int(3, 6);
            config.hordeDelay = new Vector2(12f, 22f);
            config.hordeCount = new Vector2Int(15, 25);
            config.roomKillTarget = 60;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            ZombieHudSetup.Build();
            Debug.Log("Experience progression ready: pooled pickup, level rewards, high-density spawns, UGUI HUD.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
