using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ZombieDirectionalArtSetup
{
    public const string Art = "Assets/GameMain/Res/Characters/Zombie/Directional";
    public const string AnimationFolder = Art + "/Animations";
    private static readonly string[] Views = { "Down", "Up", "Right", "Left" };
    private static readonly string[] Motions = { "Idle", "Walk", "Attack", "Death" };
    private static readonly string[] Prefabs =
    {
        "Assets/GameMain/Entity/Zombie.prefab",
        "Assets/GameMain/Entity/ZombieVariants/ZombieFast.prefab",
        "Assets/GameMain/Entity/ZombieVariants/ZombieTank.prefab",
        "Assets/GameMain/Entity/ZombieVariants/ZombieExploder.prefab",
        "Assets/GameMain/Entity/Encounters/ZombieEliteHunter.prefab",
        "Assets/GameMain/Entity/Encounters/ZombieBossTyrant.prefab"
    };

    [MenuItem("Tools/Zombie/Apply Image Generated Four Direction Animations")]
    public static void Build()
    {
        // Preserve a separately authored elite when rebuilding the common enemies.
        string eliteArt = "Assets/GameMain/Res/Characters/Zombie/EliteHunter";
        bool hasElite = Directory.Exists(eliteArt + "/IdleDown");
        BuildSet(Art, "ZombieDirectional", hasElite ? Prefabs.Where(path => !path.Contains("EliteHunter")).ToArray() : Prefabs);
        if (hasElite) BuildElite();
    }

    [MenuItem("Tools/Zombie/Apply Image Generated Elite Hunter Animations")]
    public static void BuildElite()
    {
        BuildSet("Assets/GameMain/Res/Characters/Zombie/EliteHunter", "EliteHunterDirectional",
            new[] { "Assets/GameMain/Entity/Encounters/ZombieEliteHunter.prefab" });
    }

    private static void BuildSet(string art, string controllerName, string[] prefabs)
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before replacing prefab assets.");
        AssetDatabase.Refresh();
        ImportFrames(art);
        string animationFolder = art + "/Animations";
        Directory.CreateDirectory(animationFolder);
        AssetDatabase.Refresh();
        string controllerPath = animationFolder + "/" + controllerName + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        for (int motion = 0; motion < Motions.Length; motion++)
        {
            for (int view = 0; view < Views.Length; view++)
            {
                string label = Motions[motion] + Views[view];
                var clip = CreateClip(Motions[motion], Views[view], art, animationFolder);
                var state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == label)
                    ?? machine.AddState(label, new Vector3(240 + view * 200, 60 + motion * 90, 0));
                state.motion = clip;
                state.writeDefaultValues = false;
                if (label == "IdleDown") machine.defaultState = state;
                EditorUtility.SetDirty(state);
            }
        }
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        foreach (string path in prefabs) ApplyPrefab(path, controller, art);
        AssetDatabase.SaveAssets();
        Debug.Log("Image-generated zombie art applied: " + controllerName + ", 16 clips, " + prefabs.Length + " prefab(s).");
    }

    private static void ImportFrames(string art)
    {
        foreach (string path in Directory.GetFiles(art, "*.png", SearchOption.AllDirectories))
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("/Sheets/")) continue;
            var importer = AssetImporter.GetAtPath(normalized) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing importer: " + normalized);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, .25f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = 64;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 128;
            importer.SaveAndReimport();
        }
    }

    private static Sprite[] Frames(string motion, string view, string art)
    {
        return Directory.GetFiles(art + "/" + motion + view, "*.png").OrderBy(path => path)
            .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'))).ToArray();
    }

    private static AnimationClip CreateClip(string motion, string view, string art, string animationFolder)
    {
        string path = animationFolder + "/" + motion + view + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        Sprite[] frames = Frames(motion, view == "Left" ? "Right" : view, art);
        int required = motion == "Idle" ? 1 : motion == "Death" ? 4 : 8;
        if (frames.Length != required || frames.Any(frame => frame == null))
            throw new InvalidOperationException("Incorrect source frames: " + motion + view);
        float fps = motion == "Idle" ? 2f : motion == "Death" ? 10f : 12f;
        float duration = frames.Length / fps;
        bool loop = motion == "Walk" || motion == "Idle";
        clip.frameRate = fps;
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe
        { time = duration, value = loop ? frames[0] : frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        clip.SetCurve("", typeof(SpriteRenderer), "m_FlipX",
            AnimationCurve.Constant(0, duration, view == "Left" ? 1 : 0));
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.startTime = 0f;
        clipSettings.stopTime = duration;
        clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void ApplyPrefab(string path, AnimatorController controller, string art)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var zombie = root.GetComponent<ZombieChaser>();
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var directional = root.GetComponent<ZombieDirectionalAnimator>();
            if (directional == null) directional = root.AddComponent<ZombieDirectionalAnimator>();
            var data = new SerializedObject(directional);
            data.FindProperty("attackDuration").floatValue = 8f / 12f;
            data.FindProperty("attackHitTime").floatValue = 3f / 12f;
            data.FindProperty("deathDuration").floatValue = 4f / 10f;
            data.FindProperty("attackSpeedMultiplier").floatValue = path.Contains("Boss") ? 1.35f : 1f;
            data.ApplyModifiedPropertiesWithoutUndo();
            zombie.walkFrames = Frames("Walk", "Down", art);
            zombie.attackFrames = Frames("Attack", "Down", art);
            zombie.deathFrames = Frames("Death", "Down", art);
            zombie.animationFps = 12f;
            zombie.attackScalePulse = 0;
            zombie.attackTint = Color.white;
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = Frames("Idle", "Down", art)[0];
            renderer.flipX = false;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            bool boss = path.Contains("Boss"), elite = path.Contains("Elite");
            float scale = boss ? 1.45f : elite ? 1.05f :
                zombie.archetype == ZombieChaser.ZombieArchetype.Tank ? 1.15f :
                zombie.archetype == ZombieChaser.ZombieArchetype.Fast ? .95f : 1f;
            root.transform.localScale = Vector3.one * scale;
            bool independentElite = art.EndsWith("/EliteHunter");
            renderer.color = independentElite ? Color.white : boss ? new Color(1f, .82f, .74f) :
                elite ? new Color(1f, .94f, .8f) : Color.white;
            var collider = root.GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                // Keep the navigation footprint small while anchoring the sprite at its feet.
                collider.radius = .18f / scale;
                collider.offset = new Vector2(0, -.12f / scale);
            }
            var bar = root.GetComponent<WorldHealthBar>();
            if (bar != null)
            {
                var serialized = new SerializedObject(bar);
                serialized.FindProperty("offsetY").floatValue = .62f * scale + .04f;
                serialized.FindProperty("width").floatValue = boss ? 1.05f : elite ? .75f : .54f;
                if (independentElite)
                    serialized.FindProperty("zombieFillColor").colorValue = new Color(1f, .65f, .2f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
