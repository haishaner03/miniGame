string folder = "Assets/GameMain/Res/Map/Tiles4x4/Ground/AutoGrass";
var maskList = new System.Collections.Generic.SortedSet<int>();
for (int i = 0; i < 256; i++) maskList.Add(SurvivorGrassAutoTile.NormalizeMask(i));
var masks = new System.Collections.Generic.List<int>(maskList).ToArray();

System.Action<string, string[], int, int> importSheet = delegate(string path, string[] spriteNames, int columns, int rows)
{
    UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    importer.textureType = UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
    importer.spritePixelsPerUnit = 64f;
    importer.filterMode = UnityEngine.FilterMode.Point;
    importer.mipmapEnabled = false;
    importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    importer.maxTextureSize = 2048;
    importer.isReadable = true;
    importer.SaveAndReimport();
    var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
    factories.Init();
    var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
    provider.InitSpriteEditorDataProvider();
    var oldRects = provider.GetSpriteRects();
    var rects = new UnityEditor.SpriteRect[spriteNames.Length];
    var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
    for (int i = 0; i < spriteNames.Length; i++)
    {
        var id = UnityEditor.GUID.Generate();
        foreach (var old in oldRects) if (old.name == spriteNames[i]) { id = old.spriteID; break; }
        rects[i] = new UnityEditor.SpriteRect();
        rects[i].name = spriteNames[i];
        rects[i].rect = new UnityEngine.Rect((i % columns) * 64, (rows - 1 - i / columns) * 64, 64, 64);
        rects[i].alignment = UnityEngine.SpriteAlignment.Center;
        rects[i].pivot = new UnityEngine.Vector2(0.5f, 0.5f);
        rects[i].spriteID = id;
        pairs.Add(new UnityEditor.SpriteNameFileIdPair(spriteNames[i], id));
    }
    provider.SetSpriteRects(rects);
    provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
    provider.Apply();
    importer.SaveAndReimport();
};

var names = new string[masks.Length];
for (int i = 0; i < names.Length; i++) names[i] = "Grass_Mask_" + masks[i].ToString("000");
importSheet(folder + "/GrassEdges_47.png", names, 8, 6);
var centerNames = new string[] { "Grass_Center_0", "Grass_Center_1", "Grass_Center_2" };
importSheet(folder + "/GrassCenters_3x1.png", centerNames, 3, 1);
importSheet(folder + "/SoilSeamless.png", new string[] { "Soil_Center" }, 1, 1);
importSheet(folder + "/SoilCrackedSeamless.png", new string[] { "Soil_CrackedCenter" }, 1, 1);

System.Func<string, string, UnityEngine.Sprite> findSprite = delegate(string path, string name)
{
    foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        if (asset is UnityEngine.Sprite && asset.name == name) return (UnityEngine.Sprite)asset;
    throw new System.Exception("Missing imported sprite " + name);
};

string autoPath = folder + "/Grass_AutoTile.asset";
var auto = UnityEditor.AssetDatabase.LoadAssetAtPath<SurvivorGrassAutoTile>(autoPath);
if (auto == null)
{
    auto = UnityEngine.ScriptableObject.CreateInstance<SurvivorGrassAutoTile>();
    UnityEditor.AssetDatabase.CreateAsset(auto, autoPath);
}
var so = new UnityEditor.SerializedObject(auto);
var maskProp = so.FindProperty("masks");
var spriteProp = so.FindProperty("sprites");
maskProp.arraySize = masks.Length; spriteProp.arraySize = masks.Length;
for (int i = 0; i < masks.Length; i++)
{
    maskProp.GetArrayElementAtIndex(i).intValue = masks[i];
    spriteProp.GetArrayElementAtIndex(i).objectReferenceValue = findSprite(folder + "/GrassEdges_47.png", names[i]);
}
var centerProp = so.FindProperty("centerVariations");
centerProp.arraySize = 3;
for (int i = 0; i < 3; i++) centerProp.GetArrayElementAtIndex(i).objectReferenceValue = findSprite(folder + "/GrassCenters_3x1.png", centerNames[i]);
var compatible = so.FindProperty("compatibleGrassTiles");
var oldNames = new string[] { "01_Grass_4x4_00_03", "01_Grass_4x4_01_03", "01_Grass_4x4_03_03" };
compatible.arraySize = oldNames.Length;
for (int i = 0; i < oldNames.Length; i++) compatible.GetArrayElementAtIndex(i).objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/GameMain/Res/Map/Tiles4x4/Ground/" + oldNames[i] + ".asset");
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(auto);

System.Func<string, string, string, UnityEngine.Tilemaps.Tile> makeSoil = delegate(string tileName, string imageName, string spriteName)
{
    string path = folder + "/" + tileName + ".asset";
    var tile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(path);
    if (tile == null)
    {
        tile = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        UnityEditor.AssetDatabase.CreateAsset(tile, path);
    }
    tile.sprite = findSprite(folder + "/" + imageName, spriteName);
    tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
    UnityEditor.EditorUtility.SetDirty(tile);
    return tile;
};
makeSoil("Soil_SeamlessTile", "SoilSeamless.png", "Soil_Center");
makeSoil("Soil_CrackedTile", "SoilCrackedSeamless.png", "Soil_CrackedCenter");

// Individual static tiles are also available for manually painting a specific edge.
for (int i = 0; i < masks.Length; i++)
{
    string path = folder + "/" + names[i] + ".asset";
    var tile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(path);
    if (tile == null)
    {
        tile = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        UnityEditor.AssetDatabase.CreateAsset(tile, path);
    }
    tile.sprite = findSprite(folder + "/GrassEdges_47.png", names[i]);
    tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
    UnityEditor.EditorUtility.SetDirty(tile);
}
so.Update();
compatible = so.FindProperty("compatibleGrassTiles");
compatible.arraySize = oldNames.Length + masks.Length;
for (int i = 0; i < masks.Length; i++) compatible.GetArrayElementAtIndex(oldNames.Length + i).objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(folder + "/" + names[i] + ".asset");
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.AssetDatabase.SaveAssets();
return new { maskCount = masks.Length, autoTile = autoPath, ppu = 64, filtering = "Point", compression = "None" };
