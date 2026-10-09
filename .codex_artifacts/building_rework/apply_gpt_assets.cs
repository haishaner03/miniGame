string buildingFolder = "Assets/GameMain/Res/Map/StreetBlock01/Polished/GptImage2";
string curbPath = buildingFolder + "/Curb_StreetEdge_4x4_GptImage2.png";

System.Func<string, UnityEngine.Sprite> importSingle = delegate(string path)
{
    UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    if (importer == null) throw new System.Exception("Importer missing: " + path);
    importer.textureType = UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
    importer.spritePixelsPerUnit = 256f;
    importer.filterMode = UnityEngine.FilterMode.Point;
    importer.mipmapEnabled = false;
    importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    importer.alphaIsTransparency = true;
    importer.maxTextureSize = 2048;
    importer.SaveAndReimport();
    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
    if (sprite == null) throw new System.Exception("Sprite missing after import: " + path);
    return sprite;
};

var rowShop = importSingle(buildingFolder + "/Building_RowShop_GptImage2.png");
var apartment = importSingle(buildingFolder + "/Building_LowApartment_GptImage2.png");
var safehouse = importSingle(buildingFolder + "/Building_SafehouseFront_GptImage2.png");

UnityEditor.AssetDatabase.ImportAsset(curbPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var curbImporter = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(curbPath);
curbImporter.textureType = UnityEditor.TextureImporterType.Sprite;
curbImporter.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
curbImporter.spritePixelsPerUnit = 64f;
curbImporter.filterMode = UnityEngine.FilterMode.Point;
curbImporter.mipmapEnabled = false;
curbImporter.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
curbImporter.wrapMode = UnityEngine.TextureWrapMode.Clamp;
curbImporter.alphaIsTransparency = true;
curbImporter.maxTextureSize = 2048;
var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
factories.Init();
var provider = factories.GetSpriteEditorDataProviderFromObject(curbImporter);
provider.InitSpriteEditorDataProvider();
var oldRects = provider.GetSpriteRects();
var rects = new UnityEditor.SpriteRect[16];
var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
for (int i = 0; i < 16; i++)
{
    string name = "Curb_GptImage2_" + i.ToString("00");
    var id = UnityEditor.GUID.Generate();
    foreach (var old in oldRects) if (old.name == name) { id = old.spriteID; break; }
    rects[i] = new UnityEditor.SpriteRect();
    rects[i].name = name;
    rects[i].rect = new UnityEngine.Rect((i % 4) * 64, (3 - i / 4) * 64, 64, 64);
    rects[i].alignment = UnityEngine.SpriteAlignment.Center;
    rects[i].pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    rects[i].spriteID = id;
    pairs.Add(new UnityEditor.SpriteNameFileIdPair(name, id));
}
provider.SetSpriteRects(rects);
provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
provider.Apply();
curbImporter.SaveAndReimport();

if (!UnityEditor.AssetDatabase.IsValidFolder(buildingFolder + "/Tiles"))
    UnityEditor.AssetDatabase.CreateFolder(buildingFolder, "Tiles");
string tileFolder = buildingFolder + "/Tiles";
System.Func<string, UnityEngine.Sprite> findCurb = delegate(string name)
{
    foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(curbPath))
        if (asset is UnityEngine.Sprite && asset.name == name) return (UnityEngine.Sprite)asset;
    throw new System.Exception("Missing curb sprite: " + name);
};
int tilesCreated = 0;
for (int i = 0; i < 16; i++)
{
    string name = "Tile_Curb_GptImage2_" + i.ToString("00");
    string tilePath = tileFolder + "/" + name + ".asset";
    var tile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tilePath);
    if (tile == null)
    {
        tile = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        UnityEditor.AssetDatabase.CreateAsset(tile, tilePath);
    }
    tile.sprite = findCurb("Curb_GptImage2_" + i.ToString("00"));
    tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
    UnityEditor.EditorUtility.SetDirty(tile);
    tilesCreated++;
}

var root = UnityEngine.GameObject.Find("BuildingVisuals");
int replaced = 0;
if (root != null)
{
    foreach (var r in root.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true))
    {
        string n = r.gameObject.name;
        bool isSafe = n.IndexOf("Safehouse", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool isApartment = n.IndexOf("Apartment", System.StringComparison.OrdinalIgnoreCase) >= 0;
        r.sprite = isSafe ? safehouse : (isApartment ? apartment : rowShop);
        r.drawMode = UnityEngine.SpriteDrawMode.Simple;
        float scale = isSafe ? 0.88f : (isApartment ? 0.82f : 0.78f);
        r.transform.localScale = new UnityEngine.Vector3(scale, scale, 1f);
        r.sortingOrder = 3;
        replaced++;
    }
}
UnityEditor.AssetDatabase.SaveAssets();
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "Imported 3 buildings; replaced=" + replaced + "; curb atlas=16 tiles; tile assets=" + tilesCreated + "; scene=" + scene.path;
