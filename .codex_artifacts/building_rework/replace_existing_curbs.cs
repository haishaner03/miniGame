string folder = "Assets/GameMain/Res/Map/StreetBlock01/Polished";
string curbPath = folder + "/GptImage2/Curb_StreetEdge_4x4_GptImage2.png";
System.Func<string, UnityEngine.Sprite> findSprite = delegate(string name)
{
    foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(curbPath))
        if (asset is UnityEngine.Sprite && asset.name == name) return (UnityEngine.Sprite)asset;
    return null;
};
int changed = 0;
for (int i = 0; i < 16; i++)
{
    string tilePath = folder + "/Tile_ConcreteCurb_" + i.ToString("00") + ".asset";
    var tile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tilePath);
    var sprite = findSprite("Curb_GptImage2_" + i.ToString("00"));
    if (tile == null || sprite == null) continue;
    tile.sprite = sprite;
    tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
    UnityEditor.EditorUtility.SetDirty(tile);
    changed++;
}
var ground = UnityEngine.GameObject.Find("GroundTilemap");
if (ground != null)
{
    var map = ground.GetComponent<UnityEngine.Tilemaps.Tilemap>();
    if (map != null) map.RefreshAllTiles();
}
UnityEditor.AssetDatabase.SaveAssets();
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "Updated existing curb tile assets=" + changed + "; GroundTilemap refreshed; scene=" + scene.path;
