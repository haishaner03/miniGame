string root = "Assets/GameMain/Res/Map/StreetBlock01/Polished";
string[] props = {"DeadTree","TreeStump","FallenTrunk","RockLarge","RockPile","RockTall","DryGrass","DryGrassTall","Weeds","Scrub","AbandonedSedan","WreckedCar","ChainFence","WoodFence","RustBarrel","WasteBin","RoadBarricade","DamagedBarricade","Sandbags","StartSafetyDoor","ExitSafetyDoor"};
int[] ppu = {26,55,48,56,60,53,88,84,85,84,25,27,64,64,80,84,52,52,55,48,48};
for (int i=0;i<props.Length;i++)
{
    string path=root+"/"+props[i]+".png";
    UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    if (importer==null) throw new System.Exception("No importer: "+path);
    importer.textureType=UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode=UnityEditor.SpriteImportMode.Single;
    importer.spritePixelsPerUnit=ppu[i]; importer.filterMode=UnityEngine.FilterMode.Point;
    importer.mipmapEnabled=false; importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;
    importer.wrapMode=UnityEngine.TextureWrapMode.Clamp; importer.isReadable=true; importer.SaveAndReimport();
    var sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
    string tilePath=root+"/Tile_"+props[i]+".asset";
    var tile=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tilePath);
    if(tile==null){tile=UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>(); tile.name="Tile_"+props[i];UnityEditor.AssetDatabase.CreateAsset(tile,tilePath);}
    tile.sprite=sprite; tile.colliderType=UnityEngine.Tilemaps.Tile.ColliderType.None; UnityEditor.EditorUtility.SetDirty(tile);
}
for(int i=0;i<3;i++)
{
    string path=root+"/WornAsphalt_"+i+".png"; UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path); importer.textureType=UnityEditor.TextureImporterType.Sprite; importer.spriteImportMode=UnityEditor.SpriteImportMode.Single; importer.spritePixelsPerUnit=64; importer.filterMode=UnityEngine.FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed; importer.wrapMode=UnityEngine.TextureWrapMode.Repeat; importer.SaveAndReimport();
    string tilePath=root+"/Tile_WornAsphalt_"+i+".asset"; var tile=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tilePath); if(tile==null){tile=UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();tile.name="Tile_WornAsphalt_"+i;UnityEditor.AssetDatabase.CreateAsset(tile,tilePath);} tile.sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);tile.colliderType=UnityEngine.Tilemaps.Tile.ColliderType.None;UnityEditor.EditorUtility.SetDirty(tile);
}
for(int m=0;m<16;m++)
{
    string path=root+"/ConcreteCurb_"+m.ToString("00")+".png"; UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path); importer.textureType=UnityEditor.TextureImporterType.Sprite; importer.spriteImportMode=UnityEditor.SpriteImportMode.Single; importer.spritePixelsPerUnit=64; importer.filterMode=UnityEngine.FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed; importer.wrapMode=UnityEngine.TextureWrapMode.Clamp; importer.SaveAndReimport();
    string tilePath=root+"/Tile_ConcreteCurb_"+m.ToString("00")+".asset";var tile=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tilePath);if(tile==null){tile=UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();tile.name="Tile_ConcreteCurb_"+m.ToString("00");UnityEditor.AssetDatabase.CreateAsset(tile,tilePath);}tile.sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);tile.colliderType=UnityEngine.Tilemaps.Tile.ColliderType.None;UnityEditor.EditorUtility.SetDirty(tile);
}
UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.AssetDatabase.Refresh();
return new {props=props.Length, asphaltVariants=3, curbVariants=16, folder=root};
