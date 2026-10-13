using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;
using System.Linq;

public static class Create4x4Tiles
{
    [InitializeOnLoadMethod]
    private static void CreateTiles()
    {
        if (EditorApplication.isCompiling) return;
        EditorApplication.delayCall -= CreateTiles;
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        const string root = "Assets/GameMain/Res/Map";
        var groups = new[]
        {
            new { Image = "01_Grass_4x4.png", Folder = "Ground", Collider = Tile.ColliderType.None },
            new { Image = "02_Road_4x4.png", Folder = "Road", Collider = Tile.ColliderType.None },
            new { Image = "03_RocksTrees_4x4.png", Folder = "Props", Collider = Tile.ColliderType.Grid },
            new { Image = "04_CarsFencesDoors_4x4.png", Folder = "Gameplay", Collider = Tile.ColliderType.Grid },
        };

        var tileRoot = root + "/Tiles4x4";
        EnsureFolder(root, "Tiles4x4");
        foreach (var g in groups)
        {
            EnsureFolder(tileRoot, g.Folder);
            var imagePath = root + "/" + g.Image;
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(imagePath)
                .OfType<Sprite>().OrderBy(s => s.name).ToArray();
            var folder = tileRoot + "/" + g.Folder;
            foreach (var sprite in sprites)
            {
                var path = folder + "/" + sprite.name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Tile>(path) != null) continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = sprite.name;
                tile.sprite = sprite;
                tile.colliderType = g.Collider;
                AssetDatabase.CreateAsset(tile, path);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created 4x4 Tile assets in Assets/GameMain/Res/Map/Tiles4x4");
    }

    private static void EnsureFolder(string parent, string child)
    {
        var path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
