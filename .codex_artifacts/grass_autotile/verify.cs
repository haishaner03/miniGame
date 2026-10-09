var folder = "Assets/GameMain/Res/Map/Tiles4x4/Ground/AutoGrass";
var auto = UnityEditor.AssetDatabase.LoadAssetAtPath<SurvivorGrassAutoTile>(folder + "/Grass_AutoTile.asset");
if (auto == null) throw new System.Exception("Auto grass tile missing.");
var so = new UnityEditor.SerializedObject(auto);
var masks = so.FindProperty("masks");
var sprites = so.FindProperty("sprites");
if (masks.arraySize != 47 || sprites.arraySize != 47) throw new System.Exception("Incomplete grass tile set.");
for (int i = 0; i < 47; i++) if (sprites.GetArrayElementAtIndex(i).objectReferenceValue == null) throw new System.Exception("Missing sprite " + i);

var offsets = new UnityEngine.Vector3Int[] {
    new UnityEngine.Vector3Int(0,1,0), new UnityEngine.Vector3Int(1,0,0),
    new UnityEngine.Vector3Int(0,-1,0), new UnityEngine.Vector3Int(-1,0,0),
    new UnityEngine.Vector3Int(1,1,0), new UnityEngine.Vector3Int(1,-1,0),
    new UnityEngine.Vector3Int(-1,-1,0), new UnityEngine.Vector3Int(-1,1,0)
};
var testRoot = new UnityEngine.GameObject("TemporaryGrassValidationGrid");
testRoot.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
testRoot.AddComponent<UnityEngine.Grid>();
var testObject = new UnityEngine.GameObject("TemporaryGrassValidationMap");
testObject.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
testObject.transform.SetParent(testRoot.transform);
var testMap = testObject.AddComponent<UnityEngine.Tilemaps.Tilemap>();
int combinations = 0;
try
{
    for (int bits = 0; bits < 256; bits++)
    {
        testMap.ClearAllTiles();
        testMap.SetTile(UnityEngine.Vector3Int.zero, auto);
        for (int i = 0; i < offsets.Length; i++) if ((bits & (1 << i)) != 0) testMap.SetTile(offsets[i], auto);
        testMap.RefreshAllTiles();
        int expectedMask = SurvivorGrassAutoTile.NormalizeMask(bits);
        var sprite = testMap.GetSprite(UnityEngine.Vector3Int.zero);
        string expectedName = expectedMask == 255 ? "Grass_Center_" : "Grass_Mask_" + expectedMask.ToString("000");
        if (sprite == null || !sprite.name.StartsWith(expectedName)) throw new System.Exception("Incorrect selection for mask " + bits);
        combinations++;
    }
    testMap.ClearAllTiles();
    testMap.SetTile(UnityEngine.Vector3Int.zero, auto);
    foreach (var offset in offsets) testMap.SetTile(offset, auto);
    testMap.RefreshAllTiles();
    testMap.SetTile(offsets[4], null);
    if (testMap.GetSprite(UnityEngine.Vector3Int.zero).name != "Grass_Mask_239") throw new System.Exception("Diagonal erase did not update inner corner.");
}
finally { UnityEngine.Object.DestroyImmediate(testRoot); }

var map = UnityEngine.GameObject.Find("GroundTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
int groundCount = 0, grassCount = 0, nullSprites = 0;
var shapesInScene = new System.Collections.Generic.HashSet<string>();
foreach (var p in map.cellBounds.allPositionsWithin)
{
    var tile = map.GetTile(p);
    if (tile == null) continue;
    groundCount++;
    var sprite = map.GetSprite(p);
    if (sprite == null) nullSprites++;
    if (tile is SurvivorGrassAutoTile) { grassCount++; shapesInScene.Add(sprite == null ? "null" : sprite.name); }
}
if (nullSprites != 0 || groundCount != 1920 || grassCount != 894) throw new System.Exception("Scene ground coverage changed unexpectedly.");
string report = "256 neighbour combinations passed; diagonal erase refresh passed; 47 shape sprites assigned; scene grass=894, ground=1920, nullSprites=0; used shapes=" + shapesInScene.Count;
System.IO.File.WriteAllText("D:/UnityProject/miniGame/.codex_artifacts/grass_autotile/UnityValidation.txt", report);
return report;
