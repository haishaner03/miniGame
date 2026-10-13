#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 复用商业街布局，按输入编号创建或重建 ZombieLevelXX。
/// 已有场景在重建前备份到工程根目录 Backups/ZombieLevelXX/。
/// </summary>
public static partial class ZombieLevel02Builder
{
    public enum MapVersion { Default, CommercialStreet, Random }

    private const string SceneRoot = "Assets/GameMain/Scenes/Level";
    private const string DefaultRoot = SceneRoot + "/Editor/Defaults";
    private const string TemplateScenePath = SceneRoot + "/ZombieLevel02/ZombieLevel02.unity";
    private const string MapRoot = "Assets/GameMain/Res/Map";
    private const string Polished = MapRoot + "/StreetBlock01/Polished";
    private const string Redesign = MapRoot + "/StreetBlock01/StreetRedesign";
    private const string CollisionTilePath = MapRoot + "/Tiles4x4/Level02_CollisionTile.asset";

    // 旧场景里需要整体移除的根节点（PlayerSpawnPoint 在 StreetBlock01 下，会随之删除并重建）。
    private static readonly string[] OldRoots =
    {
        "StreetBlock01", "StreetClutter", "ZombieStreetLayout", "BuildingVisuals"
    };

    [MenuItem("Tools/Zombie/Rebuild Level02 (Commercial Street)")]
    public static void Rebuild()
    {
        ZombieLevelBuildWindow.Open(2);
    }

    [MenuItem("Tools/Zombie/Build Level (Commercial Street)...")]
    public static void OpenBuildWindow()
    {
        string name = SceneManager.GetActiveScene().name;
        int number;
        if (!name.StartsWith("ZombieLevel", System.StringComparison.Ordinal) ||
            !TryParseLevelNumber(name.Substring("ZombieLevel".Length), out number))
            number = 2;
        ZombieLevelBuildWindow.Open(number);
    }

    [MenuItem("Tools/Zombie/Rebuild Level02 (Commercial Street)", true)]
    [MenuItem("Tools/Zombie/Build Level (Commercial Street)...", true)]
    private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

    internal static bool TryParseLevelNumber(string input, out int number)
    {
        return int.TryParse(input?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
            number >= 1 && number <= 99;
    }

    internal static string GetScenePath(int number)
    {
        if (number < 1 || number > 99)
            throw new System.ArgumentOutOfRangeException(nameof(number), "关卡编号须为 1–99。");
        // 第一关已有场景放在 Level 根目录，沿用其路径和 GUID。
        return number == 1 ? SceneRoot + "/ZombieLevel01.unity" :
            $"{SceneRoot}/ZombieLevel{number:00}/ZombieLevel{number:00}.unity";
    }

    private sealed class BuildTarget
    {
        public readonly string Name;
        public readonly string ScenePath;
        public readonly string SourcePath;
        public readonly string LayoutRootName;
        public readonly string NextScenePath;

        public BuildTarget(int number, MapVersion version)
        {
            Name = $"ZombieLevel{number:00}";
            ScenePath = GetScenePath(number);
            string savedDefault = GetDefaultScenePath(number);
            bool hasDefault = AssetDatabase.LoadAssetAtPath<SceneAsset>(savedDefault) != null;
            if (version == MapVersion.Default && !hasDefault)
                throw new System.InvalidOperationException("此关卡尚未保存默认版本，请先保存当前地图为默认版本。");
            SourcePath = version != MapVersion.CommercialStreet && hasDefault ? savedDefault :
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null ? ScenePath : TemplateScenePath;
            LayoutRootName = $"Level{number:00}_Layout";
            string next = number < 99 ? GetScenePath(number + 1) : null;
            NextScenePath = next != null && AssetDatabase.LoadAssetAtPath<SceneAsset>(next) != null ? next : string.Empty;
        }
    }

    /// <summary>创建或重建指定编号的关卡；返回原场景备份路径，新建时返回空字符串，取消保存时返回 null。</summary>
    public static string BuildLevel(int levelNumber)
        => BuildLevel(levelNumber, MapVersion.CommercialStreet, 0);

    public static string BuildLevel(int levelNumber, MapVersion version, int seed)
    {
        if (!CanBuild())
            throw new System.InvalidOperationException("请先停止运行并等待编译结束，再构造关卡。");
        var target = new BuildTarget(levelNumber, version);

        List<string> missing = version == MapVersion.Default ? new List<string>() : PreloadAssets(target.SourcePath);
        if (missing.Count > 0)
            throw new System.InvalidOperationException("以下资源未找到，场景未做任何修改：\n\n" + string.Join("\n", missing));
        if (version != MapVersion.Default) ValidateLayout(ZombieStreetLayout.Commercial());

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return null;

        string backup = BackupScene(target);
        SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(target.SourcePath, OpenSceneMode.Single);
            // 第一次改图前保留原图；之后生成随机地图不会覆盖已保存的默认版本。
            string defaultPath = GetDefaultScenePath(levelNumber);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(defaultPath) == null)
                SaveDefaultCopy(scene, defaultPath);
            if (version == MapVersion.Default)
                RetargetDefault(target);
            else
            {
                ZombieStreetLayout layout = version == MapVersion.Random ? CreateRandomLayout(seed) : ZombieStreetLayout.Commercial();
                Build(target, layout);
                if (version == MapVersion.Random) ValidateRandomNavigation(layout);
            }
            EnsureFolder(Path.GetDirectoryName(target.ScenePath).Replace('\\', '/'));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, target.ScenePath))
                throw new System.IO.IOException("无法保存场景：" + target.ScenePath);
            AddBuildScene(target.ScenePath);
            Debug.Log(target.Name + " built (" + version + (version == MapVersion.Random ? ", seed=" + seed : string.Empty) + "): " + target.ScenePath +
                (string.IsNullOrEmpty(backup) ? " (new scene)" : " Backup: " + backup));
            return backup;
        }
        catch
        {
            // 丢弃尚未保存的构造结果，回到构造前打开的场景。
            EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            throw;
        }
    }

    private static string BackupScene(BuildTarget target)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(target.ScenePath) == null)
            return string.Empty;
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string dir = Path.Combine(projectRoot, "Backups", target.Name);
        Directory.CreateDirectory(dir);
        string dst = Path.Combine(dir, target.Name + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
        File.Copy(Path.Combine(projectRoot, target.ScenePath), dst, false);
        return dst;
    }

    internal static string GetDefaultScenePath(int number)
    {
        GetScenePath(number); // 与关卡编号共用验证。
        return $"{DefaultRoot}/ZombieLevel{number:00}_Default.unity";
    }

    public static string SaveCurrentAsDefault(int number)
    {
        if (!CanBuild()) throw new System.InvalidOperationException("请先停止游戏并等待编译结束。");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GetScenePath(number))
            throw new System.InvalidOperationException($"请先打开 ZombieLevel{number:00}，再保存它的默认版本。");
        string path = GetDefaultScenePath(number);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Backups", "Defaults");
            Directory.CreateDirectory(dir);
            File.Copy(path, Path.Combine(dir, $"ZombieLevel{number:00}_Default_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"));
        }
        SaveDefaultCopy(scene, path);
        return path;
    }

    private static void SaveDefaultCopy(Scene scene, string path)
    {
        EnsureFolder(DefaultRoot);
        if (!EditorSceneManager.SaveScene(scene, path, true))
            throw new IOException("无法保存默认版本：" + path);
    }

    private static void RetargetDefault(BuildTarget target)
    {
        bool renamed = false;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (IsGeneratedLayoutRoot(root.name) && root.name != target.LayoutRootName)
            {
                root.name = target.LayoutRootName;
                renamed = true;
            }
        // 新编号的原图可能来自商业街模板；自己的默认快照保持原有流程配置。
        if (renamed)
        {
            LevelFlowController flow = Object.FindFirstObjectByType<LevelFlowController>();
            if (flow != null) Edit(flow, so => Prop(so, "nextScenePath").stringValue = target.NextScenePath);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new System.IO.IOException("无法创建关卡目录：" + path);
    }

    private static void AddBuildScene(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = scenes.FindIndex(s => s.path == path);
        if (index >= 0)
        {
            if (scenes[index].enabled) return;
            scenes[index] = new EditorBuildSettingsScene(path, true);
        }
        else
            scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------- assets

    private static readonly Dictionary<string, Object> Cache = new Dictionary<string, Object>();

    private static T Load<T>(string path) where T : Object
    {
        if (Cache.TryGetValue(path, out Object cached) && cached is T hit)
            return hit;

        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null && typeof(T) == typeof(Sprite))
        {
            foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (o is Sprite s)
                {
                    asset = s as T;
                    break;
                }
            }
        }

        Cache[path] = asset;
        return asset;
    }

    private static string Grass(int col, int row) => $"{MapRoot}/Tiles4x4/Ground/01_Grass_4x4_{col:00}_{row:00}.asset";
    private static string Road(int col, int row) => $"{MapRoot}/Tiles4x4/Road/02_Road_4x4_{col:00}_{row:00}.asset";
    private static string Sidewalk(string suffix) => $"{Redesign}/Tiles/Tile_Sidewalk_{suffix}.asset";
    private static string PropTile(string name) => $"{Polished}/Tile_{name}.asset";
    private static string PropSprite(string name) => $"{Polished}/{name}.png";

    private static string BuildingSpritePath(string key)
    {
        if (key.Contains("GptImage2"))
            return $"{Polished}/GptImage2/{key}.png";
        if (key.StartsWith("Building_"))
            return $"{MapRoot}/StreetBlock01/Buildings/{key}.png";
        return $"{Redesign}/{key}.png";
    }

    private static List<string> PreloadAssets(string sourceScenePath)
    {
        Cache.Clear();
        var tiles = new List<string>
        {
            Grass(0, 3), Grass(3, 3), Grass(3, 1), Grass(2, 3), Grass(3, 0), Grass(2, 1), Grass(0, 0), Grass(3, 2), Grass(0, 2),
            Road(3, 2), Road(0, 0), Road(1, 0), Road(0, 1), Road(1, 1), Road(2, 1), Road(3, 1),
            Sidewalk("Corner_NE"), Sidewalk("Corner_NW"), Sidewalk("Corner_SE"), Sidewalk("Corner_SW"),
            $"{Redesign}/Tiles/Tile_CourtyardConcrete.asset",
        };
        for (int i = 0; i < 16; i++)
            tiles.Add(Sidewalk(i.ToString("00")));
        foreach (string n in new[] { "WreckedCar", "AbandonedSedan", "ChainFence", "RoadBarricade", "DamagedBarricade",
                     "Sandbags", "RustBarrel", "WasteBin", "RockLarge", "RockPile", "RockTall", "DryGrass", "DryGrassTall", "Scrub" })
            tiles.Add(PropTile(n));

        var sprites = new List<string>
        {
            PropSprite("DeadTree"), PropSprite("ExitSafetyDoor"), PropSprite("RoadBarricade"),
            PropSprite("Sandbags"), PropSprite("WreckedCar"), PropSprite("DamagedBarricade"),
        };
        foreach (ZombieLevel02LayoutData.BuildingDef b in ZombieLevel02LayoutData.Buildings)
            sprites.Add(BuildingSpritePath(b.Sprite));

        var missing = new List<string>();
        foreach (string t in tiles)
            if (Load<TileBase>(t) == null && !missing.Contains(t)) missing.Add(t);
        foreach (string s in sprites)
            if (Load<Sprite>(s) == null && !missing.Contains(s)) missing.Add(s);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sourceScenePath) == null)
            missing.Add(sourceScenePath);
        return missing;
    }
}
#endif
