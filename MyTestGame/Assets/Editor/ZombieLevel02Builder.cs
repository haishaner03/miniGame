#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 按 ZombieLevel02LayoutData 重建 ZombieLevel02：沦陷商业街，三段线性推进 + 区域波次 + 路障门。
/// 菜单：Tools/Zombie/Rebuild Level02 (Commercial Street)
/// 执行前会把原场景复制到 工程根目录/Backups/ZombieLevel02/ 下（不在 Assets 内，不会被 Unity 导入）。
/// </summary>
public static partial class ZombieLevel02Builder
{
    private const string ScenePath = "Assets/GameMain/Scenes/Level/ZombieLevel02/ZombieLevel02.unity";
    private const string NextScenePath = "Assets/GameMain/Scenes/Level/ZombieLevel03/ZombieLevel03.unity";
    private const string MapRoot = "Assets/GameMain/Res/Map";
    private const string Polished = MapRoot + "/StreetBlock01/Polished";
    private const string Redesign = MapRoot + "/StreetBlock01/StreetRedesign";
    private const string CollisionTilePath = MapRoot + "/Tiles4x4/Level02_CollisionTile.asset";
    private const string LayoutRootName = "Level02_Layout";

    // 旧场景里需要整体移除的根节点（PlayerSpawnPoint 在 StreetBlock01 下，会随之删除并重建）。
    private static readonly string[] OldRoots =
    {
        "StreetBlock01", "StreetClutter", "ZombieStreetLayout", "BuildingVisuals", LayoutRootName
    };

    [MenuItem("Tools/Zombie/Rebuild Level02 (Commercial Street)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog(
                "重建 ZombieLevel02",
                "将删除 Level02 旧的街区物体，清空并重画所有 Tilemap，重新配置刷怪点、区域波次、路障门和出入口。\n\n" +
                "执行前会备份原场景到 工程根目录/Backups/ZombieLevel02/。",
                "重建", "取消"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        List<string> missing = PreloadAssets();
        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("缺少资源，已中止", "以下资源未找到，场景未做任何修改：\n\n" + string.Join("\n", missing), "好");
            return;
        }

        string backup = BackupScene();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        try
        {
            Build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ZombieLevel02 rebuilt. Backup: " + backup);
            EditorUtility.DisplayDialog("完成", "ZombieLevel02 已重建并保存。\n备份：" + backup, "好");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("重建失败", e.Message + "\n\n场景未保存。可从备份恢复：\n" + backup, "好");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }

    private static string BackupScene()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string dir = Path.Combine(projectRoot, "Backups", "ZombieLevel02");
        Directory.CreateDirectory(dir);
        string dst = Path.Combine(dir, "ZombieLevel02_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        File.Copy(Path.Combine(projectRoot, ScenePath), dst, true);
        return dst;
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

    private static List<string> PreloadAssets()
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
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            missing.Add(ScenePath);
        return missing;
    }
}
#endif
