string folder = "Assets/GameMain/Res/Map/StreetBlock01/Polished";
string curbPath = folder + "/GptImage2/Curb_StreetEdge_4x4_GptImage2.png";
int[] mapIndex = new int[] { 15, 0, 4, 8, 12, 1, 9, 10, 6, 8, 5, 10, 9, 11, 10, 11 };
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
    var sprite = findSprite("Curb_GptImage2_" + mapIndex[i].ToString("00"));
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
return "Remapped existing curb tiles=" + changed + "; mapping=00->15,01->00,02->04,03->08,04->12,05->01,06->09,07->10,08->06,09->08,10->05,11->10,12->09,13->11,14->10,15->11";
