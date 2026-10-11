using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds editable assets for the survivor meta progression and bow prototype.</summary>
public static class SurvivorMetaSetup
{
    private const string Art = "Assets/GameMain/Res/Weapons/SurvivorUnlocks/";
    private const string ShopPath = "Assets/GameMain/UI/Prefabs/SurvivorMetaShop.prefab";
    private const string ArrowPath = "Assets/GameMain/Entity/Weapons/ArrowProjectile.prefab";

    [MenuItem("Tools/Zombie/Setup Survivor Meta And Bow")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before setup.");
        Import(Art + "HuntingBow.png", new Vector2(0.65f, 0.5f));
        Import(Art + "HeavyHammer.png", new Vector2(0.5f, 0.27f));
        Import(Art + "Arrow.png", new Vector2(0.5f, 0.5f));
        Sprite bowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "HuntingBow.png");
        Sprite hammerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "HeavyHammer.png");
        Directory.CreateDirectory(Path.GetDirectoryName(ArrowPath));
        var arrow = new GameObject("ArrowProjectile", typeof(SpriteRenderer), typeof(SurvivorArrowProjectile));
        arrow.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Arrow.png");
        arrow.GetComponent<SpriteRenderer>().sortingOrder = 6000;
        arrow.transform.localScale = Vector3.one * 0.7f;
        GameObject arrowPrefab = PrefabUtility.SaveAsPrefabAsset(arrow, ArrowPath);
        UnityEngine.Object.DestroyImmediate(arrow);

        var bow = Definition("HuntingBow", SurvivorWeaponKind.Bow, "猎弓", bowSprite, 200, 0.5f, 0.36f, 0.68f, 0.9f, 1f, 0.7f);
        bow.projectilePrefab = arrowPrefab; bow.projectileSpeed = 12f; bow.projectileLifetime = 0.75f;
        bow.projectilePierce = 1;
        var hammer = Definition("HeavyHammer", SurvivorWeaponKind.Hammer, "重锤", hammerSprite, 145, 0.65f, 0.44f, 0.6f, 1.22f, 2.4f, 0.83f);
        hammer.arcDegrees = 145f;
        EditorUtility.SetDirty(bow); EditorUtility.SetDirty(hammer);

        var player = PrefabUtility.LoadPrefabContents(SurvivorWeaponSetup.PrefabPath);
        try
        {
            Assign(player.GetComponent<SurvivorMeleeAttack>(), "bow", bow);
            Assign(player.GetComponent<SurvivorMeleeAttack>(), "hammer", hammer);
            PrefabUtility.SaveAsPrefabAsset(player, SurvivorWeaponSetup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        var config = Resources.Load<ZombieRunConfig>("ZombieRunConfig");
        config.arrowProjectilePrefab = arrowPrefab;
        EditorUtility.SetDirty(config);
        var picker = PrefabUtility.LoadPrefabContents("Assets/GameMain/UI/Prefabs/StartingWeaponPicker.prefab");
        try
        {
            picker.GetComponent<StartingWeaponPicker>().ExpandChoices(config.uiFont, bowSprite, hammerSprite);
            PrefabUtility.SaveAsPrefabAsset(picker, "Assets/GameMain/UI/Prefabs/StartingWeaponPicker.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(picker); }
        var shop = new GameObject("SurvivorMetaShop", typeof(RectTransform), typeof(SurvivorMetaShop));
        shop.GetComponent<SurvivorMetaShop>().Build(config.uiFont, bowSprite, hammerSprite);
        GameObject shopPrefab = PrefabUtility.SaveAsPrefabAsset(shop, ShopPath);
        UnityEngine.Object.DestroyImmediate(shop);
        BuildMenu(shopPrefab, config.uiFont);
        PatchAttackLabel();
        AssetDatabase.SaveAssets();
        Debug.Log("Survivor meta progression ready: point settlement, 4-weapon picker, shop, bow and pooled arrows.");
    }

    private static MeleeWeaponDefinition Definition(string name, SurvivorWeaponKind kind, string title, Sprite sprite,
        int damage, float cooldown, float duration, float impact, float reach, float knockback, float length)
    {
        string path = Art + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<MeleeWeaponDefinition>(); AssetDatabase.CreateAsset(asset, path); }
        asset.kind = kind; asset.displayName = title; asset.sprite = sprite; asset.damage = damage;
        asset.cooldown = cooldown; asset.attackDuration = duration; asset.impactProgress = impact;
        asset.reach = reach; asset.knockbackMultiplier = knockback; asset.visualLength = length;
        asset.trailColor = new Color(1f, 0.76f, 0.35f, 0.9f); asset.trailDuration = 0.06f;
        return asset;
    }
    private static void BuildMenu(GameObject shop, Font font)
    {
        const string path = "Assets/GameMain/UI/Prefabs/ZombieMainMenu.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var flow = root.GetComponent<ZombieUiFlow>();
            var button = flow.mainPanel.transform.Find("GrowthButton");
            if (button == null) button = UnityEngine.Object.Instantiate(flow.optionsButton, flow.mainPanel.transform).transform;
            button.name = "GrowthButton";
            ((RectTransform)button).anchoredPosition = new Vector2(70f, -492f);
            button.GetComponentInChildren<Text>().text = "幸存者成长";
            var icon = button.Find("Icon");
            if (icon != null) icon.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameMain/UI/Art/ZombieUI_Icon_Safehouse.png");
            ((RectTransform)flow.optionsButton.transform).anchoredPosition = new Vector2(70f, -592f);
            ((RectTransform)flow.quitButton.transform).anchoredPosition = new Vector2(70f, -692f);
            flow.growthButton = button.GetComponent<Button>(); flow.growthShopPrefab = shop;
            var points = flow.mainPanel.transform.Find("SurvivorPoints");
            if (points == null)
            {
                var go = new GameObject("SurvivorPoints", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(flow.mainPanel.transform, false); points = go.transform;
            }
            var rect = (RectTransform)points;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(75f, -254f); rect.sizeDelta = new Vector2(540f, 30f);
            flow.survivorPoints = points.GetComponent<Text>();
            flow.survivorPoints.font = font; flow.survivorPoints.fontSize = 21;
            flow.survivorPoints.color = ZombieHudTheme.Text; flow.survivorPoints.raycastTarget = false;
            flow.survivorPoints.text = "幸存者点数  " + SurvivorMetaProgress.Points;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void PatchAttackLabel()
    {
        const string path = "Assets/GameMain/UI/Prefabs/ZombieBattleHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                if (text.text == "近战 / 左键") text.text = "攻击 / 左键";
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void BindBowFrames()
    {
        var library = AssetDatabase.LoadAssetAtPath<SurvivorWeaponPoseLibrary>(SurvivorWeaponSetup.CombatFolder + "SurvivorWeaponPoses.asset");
        foreach (var view in new[] { "Down", "Up", "Right", "Left" })
        {
            var frames = new Sprite[6];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = "Assets/GameMain/Res/Characters/Survivor/BowCombat/" + view + "/Survivor_Bow" + view + "_" + i.ToString("00") + ".png";
                Import(path, new Vector2(0.5f, 0.25f));
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i] == null) throw new InvalidOperationException("Missing bow frame: " + path);
            }
            var field = typeof(SurvivorWeaponPoseLibrary).GetField("bowAttack" + view);
            field.SetValue(library, frames);
        }
        EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
    }
    public static void Import(string path, Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
    }
    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
}
