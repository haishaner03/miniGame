var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var root=UnityEngine.GameObject.Find("ZombieStreetLayout");
var ground=UnityEngine.GameObject.Find("GroundTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
string art="Assets/GameMain/Res/Map/StreetBlock01/StreetRedesign";
string oldArt="Assets/GameMain/Res/Map/StreetBlock01/Polished";
var road0=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(oldArt+"/Tile_WornAsphalt_0.asset");
var road1=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(oldArt+"/Tile_WornAsphalt_1.asset");
var road2=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(oldArt+"/Tile_WornAsphalt_2.asset");
foreach(var p in ground.cellBounds.allPositionsWithin) {
    var t=ground.GetTile(p);if(t==null||!t.name.StartsWith("Tile_WornAsphalt"))continue;
    uint hash=unchecked((uint)((p.x*73856093)^(p.y*19349663)));
    ground.SetTile(p,hash%17==0?road1:hash%23==0?road2:road0);
}
ground.RefreshAllTiles();
foreach(var sr in root.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true)) {
    if(sr.transform.parent.name=="RoadMarkings"||sr.transform.parent.name=="LevelMarkers")continue;
    var depth=sr.GetComponent<SurvivorDepthSort>();if(depth==null)depth=sr.gameObject.AddComponent<SurvivorDepthSort>();
    bool isBuilding=sr.transform.parent.name=="Buildings";
    float offset=isBuilding?0:sr.sprite.bounds.min.y*sr.transform.localScale.y;
    depth.Configure(offset,isBuilding?0:1);
    UnityEditor.EditorUtility.SetDirty(sr);UnityEditor.EditorUtility.SetDirty(depth);
}
var player=UnityEngine.GameObject.Find("SurvivorPlayer");
var playerDepth=player.GetComponent<SurvivorDepthSort>();if(playerDepth==null)playerDepth=player.AddComponent<SurvivorDepthSort>();
playerDepth.Configure(0,2);UnityEditor.EditorUtility.SetDirty(playerDepth);
root.transform.Find("LevelMarkers/PlayerSpawnPoint").position=new UnityEngine.Vector3(7.5f,5,0);
root.transform.Find("LevelMarkers/ExitPoint").position=new UnityEngine.Vector3(30.4f,43.4f,0);
// Stretch fence artwork to a continuous run; retain one collider per cordon.
foreach(UnityEngine.Transform run in root.transform.Find("StreetCordons")) {
    if(run.GetComponent<UnityEngine.BoxCollider2D>()==null)continue;
    foreach(UnityEngine.Transform panel in run)panel.localScale=new UnityEngine.Vector3(1.3f,1.05f,1);
}
UnityEngine.Physics2D.SyncTransforms();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "Refined sidewalk/asphalt repetition; configured depth sorting for player and street objects.";
