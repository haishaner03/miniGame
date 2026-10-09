using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SurvivorWeaponSetup
{
    public const string PrefabPath = "Assets/GameMain/Entity/SurvivorPlayer.prefab";
    public const string WeaponFolder = "Assets/GameMain/Res/Weapons/Melee/";
    public const string CombatFolder = "Assets/GameMain/Res/Characters/Survivor/Combat/";

    [Serializable] private class Entry
    {
        public string path, view;
        public float[] gripPixel;
        public float weaponAngle;
    }
    [Serializable] private class Manifest
    {
        public Entry[] poses, attackDown, attackUp, attackRight, attackLeft;
    }

    [MenuItem("Tools/Zombie/Setup Survivor Weapons")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before setup.");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("output/imagegen/Weapons/PoseManifest.json"));
        foreach (var entry in manifest.poses.Where(p => p.path.Contains("/Combat/")))
            ImportSprite(entry.path, new Vector2(.5f, .25f));
        ImportSprite(WeaponFolder + "Machete.png", new Vector2(.5f, 14f / 64f));
        ImportSprite(WeaponFolder + "IronBar.png", new Vector2(.5f, 14f / 64f));
        var machete = Definition("Machete", "砍刀", 80, .28f, .2f, .48f, .9f, 120f, 1f, .72f, new Color(.65f, .9f, 1f, .9f));
        var ironBar = Definition("IronBar", "铁棍", 100, .42f, .32f, .55f, 1.035f, 100f, 1.6f, .78f, new Color(1f, .77f, .38f, .9f));
        var library = LoadOrCreate<SurvivorWeaponPoseLibrary>(CombatFolder + "SurvivorWeaponPoses.asset");
        library.poses = manifest.poses.Select(Pose).ToArray();
        library.attackDown = manifest.attackDown.Select(Pose).ToArray();
        library.attackUp = manifest.attackUp.Select(Pose).ToArray();
        library.attackRight = manifest.attackRight.Select(Pose).ToArray();
        library.attackLeft = manifest.attackLeft.Select(Pose).ToArray();
        EditorUtility.SetDirty(library);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/GameMain/Res/Characters/Survivor/Ani/Player.controller");
        foreach (SurvivorView view in Enum.GetValues(typeof(SurvivorView)))
        {
            var poses = library.Attack(view);
            var clip = LoadOrCreate<AnimationClip>(CombatFolder + "Attack" + view + ".anim");
            clip.frameRate = 30;
            SetSpriteKeys(clip, poses.Select(p => p.sprite).ToArray(), new[] { 0f, .04f, .072f, .096f, .136f, .176f }, .2f, false);
            State(controller, "Attack" + view, clip);
            if (view == SurvivorView.Down) continue;
            var idle = LoadOrCreate<AnimationClip>(CombatFolder + "Idle" + view + ".anim");
            SetSpriteKeys(idle, new[] { poses[0].sprite }, new[] { 0f }, .6667f, true);
            State(controller, "Idle" + view, idle);
        }
        EditorUtility.SetDirty(controller);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var pivot = root.transform.Find("WeaponPivot");
            if (pivot == null) { pivot = new GameObject("WeaponPivot").transform; pivot.SetParent(root.transform, false); }
            var child = pivot.Find("WeaponSprite");
            if (child == null) { child = new GameObject("WeaponSprite").transform; child.SetParent(pivot, false); }
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = machete.sprite;
            renderer.sortingLayerID = root.GetComponent<SpriteRenderer>().sortingLayerID;
            renderer.sortingOrder = root.GetComponent<SpriteRenderer>().sortingOrder + 1;
            child.localScale = Vector3.one * machete.visualLength;
            pivot.localPosition = new Vector3(-.17f, .08f, 0);
            pivot.localRotation = Quaternion.Euler(0, 0, 165);
            var visual = root.GetComponent<SurvivorWeaponVisual>();
            if (visual == null) visual = root.AddComponent<SurvivorWeaponVisual>();
            Assign(visual, "poseLibrary", library);
            Assign(visual, "weaponPivot", pivot);
            Assign(visual, "weaponRenderer", renderer);
            Assign(root.GetComponent<SurvivorMeleeAttack>(), "machete", machete);
            Assign(root.GetComponent<SurvivorMeleeAttack>(), "ironBar", ironBar);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var hud = PrefabUtility.LoadPrefabContents(ZombieHudSetup.PrefabPath);
        try
        {
            var icon = hud.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .First(i => i.name == "Icon" && i.transform.parent.name == "MeleeSkill");
            Assign(hud.GetComponent<ZombieRunUI>(), "equippedWeaponIcon", icon);
            PrefabUtility.SaveAsPrefabAsset(hud, ZombieHudSetup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }
        AssetDatabase.SaveAssets();
        Debug.Log("Survivor weapons ready: two definitions, 24 attack sprites, four attack clips, directional idle and equipped prefab.");
    }

    private static void ImportSprite(string path, Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static MeleeWeaponDefinition Definition(string name, string label, int damage, float cooldown, float duration,
        float impact, float reach, float arc, float knockback, float length, Color trail)
    {
        var asset = LoadOrCreate<MeleeWeaponDefinition>(WeaponFolder + name + ".asset");
        asset.displayName = label;
        asset.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WeaponFolder + name + ".png");
        asset.damage = damage;
        asset.cooldown = cooldown;
        asset.attackDuration = duration;
        asset.impactProgress = impact;
        asset.reach = reach;
        asset.arcDegrees = arc;
        asset.knockbackMultiplier = knockback;
        asset.visualLength = length;
        asset.trailColor = trail;
        asset.trailDuration = .07f;
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static SurvivorWeaponPoseLibrary.Pose Pose(Entry entry)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.path);
        if (sprite == null) throw new InvalidOperationException("Missing pose sprite: " + entry.path);
        return new SurvivorWeaponPoseLibrary.Pose { sprite = sprite, view = (SurvivorView)Enum.Parse(typeof(SurvivorView), entry.view),
            gripPixel = new Vector2(entry.gripPixel[0], entry.gripPixel[1]), weaponAngle = entry.weaponAngle };
    }

    private static T LoadOrCreate<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = typeof(T) == typeof(AnimationClip) ? (T)(UnityEngine.Object)new AnimationClip() : (T)(UnityEngine.Object)ScriptableObject.CreateInstance(typeof(T));
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void State(AnimatorController controller, string name, AnimationClip clip)
    {
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
        state.motion = clip;
        EditorUtility.SetDirty(state);
    }

    private static void SetSpriteKeys(AnimationClip clip, Sprite[] sprites, float[] times, float duration, bool loop)
    {
        var binding = new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        var keys = sprites.Select((s, i) => new ObjectReferenceKeyframe { time = times[i], value = s }).ToList();
        keys.Add(new ObjectReferenceKeyframe { time = duration, value = sprites[sprites.Length - 1] });
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
