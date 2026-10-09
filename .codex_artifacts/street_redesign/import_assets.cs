string folder = "Assets/GameMain/Res/Map/StreetBlock01/StreetRedesign";
UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
if (!UnityEditor.AssetDatabase.IsValidFolder(folder + "/Tiles")) UnityEditor.AssetDatabase.CreateFolder(folder, "Tiles");
int count = 0;
foreach (string full in System.IO.Directory.GetFiles(UnityEngine.Application.dataPath + "/GameMain/Res/Map/StreetBlock01/StreetRedesign", "*.png"))
{
    string name = System.IO.Path.GetFileNameWithoutExtension(full);
    string path = folder + "/" + name + ".png";
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    importer.textureType = UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
    importer.spritePixelsPerUnit = 64;
    importer.filterMode = UnityEngine.FilterMode.Point;
    importer.mipmapEnabled = false;
    importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    importer.alphaIsTransparency = true;
    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    importer.maxTextureSize = 2048;
    var settings = new UnityEditor.TextureImporterSettings();
    importer.ReadTextureSettings(settings);
    settings.spriteMeshType = UnityEngine.SpriteMeshType.FullRect;
    bool isTile = name.StartsWith("Sidewalk") || name == "CourtyardConcrete";
    settings.spriteAlignment = isTile ? (int)UnityEngine.SpriteAlignment.Center : (int)UnityEngine.SpriteAlignment.BottomCenter;
    settings.spritePivot = isTile ? new UnityEngine.Vector2(.5f,.5f) : new UnityEngine.Vector2(.5f,0);
    importer.SetTextureSettings(settings);
    importer.SaveAndReimport();
    if (isTile)
    {
        string tilePath = folder + "/Tiles/Tile_" + name + ".asset";
        var tile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tilePath);
        if (tile == null) { tile = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>(); UnityEditor.AssetDatabase.CreateAsset(tile, tilePath); }
        tile.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
        tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
        UnityEditor.EditorUtility.SetDirty(tile);
    }
    count++;
}
UnityEditor.AssetDatabase.SaveAssets();
return "Imported " + count + " derived assets and contiguous sidewalk tiles.";
