if (UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Stop Play Mode before applying grass.");
var go = UnityEngine.GameObject.Find("GroundTilemap");
if (go == null || go.scene.path != "Assets/GameMain/Scenes/Level/ZombieLevel01.unity") throw new System.Exception("Expected ZombieLevel01 GroundTilemap.");
var map = go.GetComponent<UnityEngine.Tilemaps.Tilemap>();
string folder = "Assets/GameMain/Res/Map/Tiles4x4/Ground/AutoGrass";
var auto = UnityEditor.AssetDatabase.LoadAssetAtPath<SurvivorGrassAutoTile>(folder + "/Grass_AutoTile.asset");
var soil = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(folder + "/Soil_SeamlessTile.asset");
var crack = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(folder + "/Soil_CrackedTile.asset");
if (auto == null || soil == null || crack == null) throw new System.Exception("Create and validate grass assets first.");
UnityEditor.Undo.RegisterCompleteObjectUndo(map, "Apply continuous grass terrain");
var positions = new System.Collections.Generic.List<UnityEngine.Vector3Int>();
var replacements = new System.Collections.Generic.List<UnityEngine.Tilemaps.TileBase>();
int grassCount = 0, soilCount = 0;
foreach (var p in map.cellBounds.allPositionsWithin)
{
    var tile = map.GetTile(p);
    if (tile == null) continue;
    if (tile.name == "01_Grass_4x4_00_03" || tile.name == "01_Grass_4x4_01_03" || tile.name == "01_Grass_4x4_03_03")
    {
        positions.Add(p); replacements.Add(auto); grassCount++;
    }
    else if (tile.name == "01_Grass_4x4_02_03" || tile.name == "01_Grass_4x4_02_01")
    {
        positions.Add(p); replacements.Add(tile.name == "01_Grass_4x4_02_01" ? crack : soil); soilCount++;
    }
}
map.SetTiles(positions.ToArray(), replacements.ToArray());
map.RefreshAllTiles();
UnityEditor.EditorUtility.SetDirty(map);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(go.scene);

// Add a dedicated row alongside the existing 4x4 palette entries.
string palettePath = "Assets/GameMain/Res/Map/Tiles4x4/Ground/Ground.prefab";
var palette = UnityEditor.PrefabUtility.LoadPrefabContents(palettePath);
try
{
    var paletteMap = palette.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>();
    if (paletteMap == null) throw new System.Exception("Ground palette has no Tilemap.");
    paletteMap.SetTile(new UnityEngine.Vector3Int(6, 0, 0), auto);
    paletteMap.SetTile(new UnityEngine.Vector3Int(7, 0, 0), soil);
    paletteMap.SetTile(new UnityEngine.Vector3Int(8, 0, 0), crack);
    paletteMap.RefreshAllTiles();
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(palette, palettePath);
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(palette); }
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.Selection.activeObject = auto;
UnityEditor.EditorGUIUtility.PingObject(auto);
return new { grassChanged = grassCount, soilChanged = soilCount, scene = go.scene.path, palette = palettePath, autoTile = folder + "/Grass_AutoTile.asset" };
