if (UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Stop Play Mode before changing the map.");
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (scene.path != "Assets/GameMain/Scenes/Level/ZombieLevel01.unity") throw new System.Exception("Unexpected active scene.");
string art = "Assets/GameMain/Res/Map/StreetBlock01/StreetRedesign";
string oldArt = "Assets/GameMain/Res/Map/StreetBlock01/Polished";
string grassArt = "Assets/GameMain/Res/Map/Tiles4x4/Ground/AutoGrass";
System.Func<string,UnityEngine.Tilemaps.TileBase> tile = delegate(string path) {
    var value = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(path);
    if (value == null) throw new System.Exception("Missing tile: " + path);
    return value;
};
System.Func<string,UnityEngine.Sprite> sprite = delegate(string name) {
    var value = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(art + "/" + name + ".png");
    if (value == null) value = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(oldArt + "/" + name + ".png");
    if (value == null) throw new System.Exception("Missing sprite: " + name);
    return value;
};
var asphalt = new UnityEngine.Tilemaps.TileBase[3];
for (int i=0;i<3;i++) asphalt[i]=tile(oldArt+"/Tile_WornAsphalt_"+i+".asset");
var pavements = new UnityEngine.Tilemaps.TileBase[16];
for (int i=0;i<16;i++) pavements[i]=tile(art+"/Tiles/Tile_Sidewalk_"+i.ToString("00")+".asset");
var concrete=tile(art+"/Tiles/Tile_CourtyardConcrete.asset");
var grass=tile(grassArt+"/Grass_AutoTile.asset");
var soil=tile(grassArt+"/Soil_SeamlessTile.asset");
var cracked=tile(grassArt+"/Soil_CrackedTile.asset");
var ground=UnityEngine.GameObject.Find("GroundTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
UnityEditor.Undo.RegisterCompleteObjectUndo(ground,"Rebuild zombie street ground");
bool[,] roads=new bool[40,48];
bool[,] courts=new bool[40,48];
System.Action<bool[,],int,int,int,int> rect=delegate(bool[,] grid,int x0,int y0,int x1,int y1) {
    for(int y=System.Math.Max(0,y0);y<=System.Math.Min(47,y1);y++)
        for(int x=System.Math.Max(0,x0);x<=System.Math.Min(39,x1);x++) grid[x,y]=true;
};
rect(roads,18,3,21,42);
rect(roads,7,3,20,5);
rect(roads,3,11,35,13);
rect(roads,7,11,9,32);
rect(roads,8,22,20,24);
rect(roads,3,30,36,32);
rect(roads,30,11,32,42);
rect(roads,18,40,32,42);
rect(courts,24,7,35,10);
rect(courts,24,25,28,29);
rect(courts,2,25,5,29);
rect(courts,11,38,16,41);
rect(courts,28,43,33,44);
var positions=new UnityEngine.Vector3Int[1920];
var tiles=new UnityEngine.Tilemaps.TileBase[1920];
int index=0,roadCount=0,pavementCount=0,grassCount=0;
System.Func<int,int,bool> isRoad=delegate(int x,int y) {return x>=0&&x<40&&y>=0&&y<48&&roads[x,y];};
for(int y=0;y<48;y++) for(int x=0;x<40;x++) {
    var p=new UnityEngine.Vector3Int(x,y,0);
    uint h=unchecked((uint)((x*73856093)^(y*19349663)));
    UnityEngine.Tilemaps.TileBase value;
    if(roads[x,y]) {value=asphalt[h%17==0?1:h%23==0?2:0];roadCount++;}
    else {
        int mask=(isRoad(x,y+1)?1:0)|(isRoad(x+1,y)?2:0)|(isRoad(x,y-1)?4:0)|(isRoad(x-1,y)?8:0);
        if(mask!=0) {value=pavements[mask];pavementCount++;}
        else if(isRoad(x+1,y+1)) value=tile(art+"/Tiles/Tile_Sidewalk_Corner_NE.asset");
        else if(isRoad(x+1,y-1)) value=tile(art+"/Tiles/Tile_Sidewalk_Corner_SE.asset");
        else if(isRoad(x-1,y-1)) value=tile(art+"/Tiles/Tile_Sidewalk_Corner_SW.asset");
        else if(isRoad(x-1,y+1)) value=tile(art+"/Tiles/Tile_Sidewalk_Corner_NW.asset");
        else if(courts[x,y]) value=concrete;
        else {
            bool planted=x<3||x>36||y<3||y>45
                ||(x>=11&&x<=16&&y>=6&&y<=10)
                ||(x>=11&&x<=16&&y>=17&&y<=21)
                ||(x>=23&&x<=28&&y>=18&&y<=22)
                ||(x>=11&&x<=16&&y>=26&&y<=29)
                ||(x>=23&&x<=28&&y>=34&&y<=39)
                ||(x>=2&&x<=5&&y>=35&&y<=41);
            value=planted?grass:(h%13==0?cracked:soil);
            if(planted) grassCount++;
        }
    }
    positions[index]=p;tiles[index]=value;index++;
}
ground.ClearAllTiles();ground.SetTiles(positions,tiles);ground.RefreshAllTiles();
ground.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sortingOrder=0;
foreach(string name in new[]{"ObstacleTilemap","GameplayTilemap","BuildingTilemap","GroundDetailTilemap","CollisionTilemap"}) {
    var go=UnityEngine.GameObject.Find(name); if(go==null)continue;
    var tm=go.GetComponent<UnityEngine.Tilemaps.Tilemap>();UnityEditor.Undo.RegisterCompleteObjectUndo(tm,"Clear legacy map objects");tm.ClearAllTiles();
    if(name=="BuildingTilemap"||name=="CollisionTilemap")go.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().enabled=false;
    UnityEditor.EditorUtility.SetDirty(tm);
}
foreach(string name in new[]{"BuildingVisuals","StreetClutter"}) {
    var go=UnityEngine.GameObject.Find(name);if(go!=null){UnityEditor.Undo.RecordObject(go,"Disable previous map visuals");go.SetActive(false);}
}
var oldRoot=UnityEngine.GameObject.Find("StreetBlock01");
if(oldRoot!=null)foreach(UnityEngine.Transform t in oldRoot.transform) {
    if(t.name.StartsWith("Boundary"))continue;
    if(t.name=="PlayerSpawnPoint"||t.name=="ExitPoint")continue;
    UnityEditor.Undo.RecordObject(t.gameObject,"Disable legacy street objects");t.gameObject.SetActive(false);
}
var previous=UnityEngine.GameObject.Find("ZombieStreetLayout");
if(previous!=null)UnityEditor.Undo.DestroyObjectImmediate(previous);
var root=new UnityEngine.GameObject("ZombieStreetLayout");UnityEditor.Undo.RegisterCreatedObjectUndo(root,"Create redesigned street");
System.Func<string,UnityEngine.Transform> group=delegate(string name) {var go=new UnityEngine.GameObject(name);go.transform.SetParent(root.transform);return go.transform;};
var buildings=group("Buildings");var vehicles=group("AbandonedVehicles");var planting=group("PlantingAndDebris");var cordons=group("StreetCordons");var markings=group("RoadMarkings");var markers=group("LevelMarkers");
System.Func<string,string,UnityEngine.Transform,float,float,float,UnityEngine.GameObject> prop=delegate(string name,string asset,UnityEngine.Transform parent,float x,float y,float scale) {
    var go=new UnityEngine.GameObject(name);go.transform.SetParent(parent);go.transform.position=new UnityEngine.Vector3(x,y,0);go.transform.localScale=new UnityEngine.Vector3(scale,scale,1);
    var sr=go.AddComponent<UnityEngine.SpriteRenderer>();sr.sprite=sprite(asset);sr.sortingOrder=parent==markings?1:parent==buildings?8:4;
    return go;
};
System.Action<UnityEngine.GameObject,float,float,float,float> box=delegate(UnityEngine.GameObject go,float width,float height,float ox,float oy) {
    var c=go.AddComponent<UnityEngine.BoxCollider2D>();c.size=new UnityEngine.Vector2(width,height);c.offset=new UnityEngine.Vector2(ox,oy);
};
System.Action<string,string,float,float,float> house=delegate(string name,string asset,float x,float y,float scale) {
    var go=prop(name,asset,buildings,x,y,scale);var sr=go.GetComponent<UnityEngine.SpriteRenderer>();
    float w=sr.sprite.rect.width/64f;float h=sr.sprite.rect.height/64f;
    box(go,w*.96f,h*.96f,0,h*.48f);
};
house("South_StartSafehouse","Safehouse_CentralGate",7.5f,6.4f,.85f);
house("South_RowResidence","Apartment_TwoUnits_B",14f,5.1f,.88f);
house("South_Garage","Shop_TwoUnits_B",26f,3.4f,.9f);
house("South_Workshop","Shop_TwoUnits_A",32f,3.4f,.9f);
house("West_CornerShop","Shop_TwoUnits_A",3.9f,15.4f,.96f);
house("Market_SouthResidence","Apartment_TwoUnits_A",14f,15.5f,.92f);
house("East_SouthResidence","Apartment_TwoUnits_B",26f,15.5f,.96f);
house("East_Garage","Shop_TwoUnits_B",36f,15.4f,.92f);
house("West_ServiceShop","Shop_TwoUnits_B",3.9f,23.8f,.9f);
house("Market_NorthShopA","Shop_TwoUnits_A",13.7f,25.2f,.95f);
house("East_CornerShop","Shop_TwoUnits_B",26f,22.7f,.88f);
house("East_NorthResidence","Apartment_TwoUnits_A",36f,24.1f,.92f);
house("West_UpperResidence","Apartment_TwoUnits_A",3.9f,33.8f,.9f);
house("Market_UpperResidence","Apartment_TwoUnits_B",14f,33.8f,.94f);
house("North_CornerShop","Shop_TwoUnits_A",26f,33.8f,.9f);
house("East_UpperGarage","Shop_TwoUnits_B",36f,33.8f,.9f);
house("North_WestShop","Shop_TwoUnits_B",6f,43.2f,.85f);
house("North_RowResidence","Apartment_TwoUnits_A",13f,43f,.82f);
house("North_ExitSafehouse","Safehouse_CentralGate",30.4f,44f,.72f);

System.Action<string,string,float,float,float,float> car=delegate(string name,string asset,float x,float y,float scale,float angle) {
    var go=prop(name,asset,vehicles,x,y,scale);go.transform.rotation=UnityEngine.Quaternion.Euler(0,0,angle);
    var sr=go.GetComponent<UnityEngine.SpriteRenderer>();
    box(go,sr.sprite.rect.width/sr.sprite.pixelsPerUnit*.83f,sr.sprite.rect.height/sr.sprite.pixelsPerUnit*.9f,0,0);
};
car("Parking_Sedan01","AbandonedSedan",25.5f,8.7f,.72f,0);
car("Parking_Wreck02","WreckedCar",29f,8.8f,.74f,-7);
car("Parking_Sedan03","AbandonedSedan",33f,8.5f,.72f,0);
car("South_CurbWreck","WreckedCar",17.2f,8.3f,.72f,8);
car("Market_BlockageCar","WreckedCar",20f,19.9f,.78f,82);
car("West_AlleyWreck","AbandonedSedan",7.4f,27.1f,.7f,-5);
car("Market_ParkedCar","AbandonedSedan",17.2f,27.6f,.7f,0);
car("North_BlockageCar","WreckedCar",20f,35.9f,.76f,94);
car("East_ResidentialCar","AbandonedSedan",32.4f,26.4f,.7f,-4);
car("East_UpperWreck","WreckedCar",34.2f,39f,.7f,9);
car("North_CourtyardCar","AbandonedSedan",24f,43.8f,.72f,86);

System.Action<string,float,float,float,bool> clutter=delegate(string asset,float x,float y,float scale,bool solid) {
    var go=prop(asset+"_"+planting.childCount,asset,planting,x,y,scale);
    if(solid) {
        var sr=go.GetComponent<UnityEngine.SpriteRenderer>();float w=sr.sprite.rect.width/sr.sprite.pixelsPerUnit;float h=sr.sprite.rect.height/sr.sprite.pixelsPerUnit;
        if(asset=="DeadTree") box(go,.35f,.28f,0,-h*.32f);
        else box(go,w*.75f,h*.55f,0,asset.StartsWith("Debris")?h*.25f:0);
    }
};
float[,] trees={{1.4f,4f},{1.2f,10f},{2f,21f},{1.3f,30f},{1.2f,41f},{2f,46f},{37.7f,5.5f},{38f,11.5f},{38.1f,22f},{38.2f,30f},{38.2f,41.5f},{37f,46f},{12f,10f},{27.8f,21.5f},{12.1f,29.5f},{26.5f,39f},{11f,41.5f},{17f,45.8f}};
for(int i=0;i<trees.GetLength(0);i++)clutter("DeadTree",trees[i,0],trees[i,1],.9f+(i%3)*.08f,true);
float[,] waste={{5.7f,6.4f},{10.7f,6.5f},{12f,15.4f},{16.7f,15.4f},{24f,15.3f},{37.8f,15.4f},{2.2f,24f},{15.7f,25.1f},{24f,22.5f},{37.4f,24f},{11.5f,33.7f},{28.1f,33.8f},{33.8f,33.8f},{28f,44f}};
for(int i=0;i<waste.GetLength(0);i++) {
    clutter(i%2==0?"WasteBin":"RustBarrel",waste[i,0],waste[i,1],.88f,true);
    clutter("Debris_Pile",waste[i,0]+.7f,waste[i,1]-.25f,.78f,false);
    clutter("Discarded_Planks",waste[i,0]-.4f,waste[i,1]-.4f,.85f,false);
}
var rng=new System.Random(106021);
for(int i=0;i<125;i++) {
    float x=.5f+(float)rng.NextDouble()*39f,y=.5f+(float)rng.NextDouble()*47f;
    int cx=(int)x,cy=(int)y;if(roads[cx,cy]||courts[cx,cy])continue;
    if(UnityEngine.Physics2D.OverlapPoint(new UnityEngine.Vector2(x,y))!=null)continue;
    clutter(new[]{"Weeds","Scrub","DryGrass","DryGrassTall"}[i%4],x,y,.72f+(float)rng.NextDouble()*.35f,false);
}

System.Action<string,float,float,float> fenceRun=delegate(string name,float x0,float x1,float y) {
    var holder=new UnityEngine.GameObject(name);holder.transform.SetParent(cordons);holder.transform.position=new UnityEngine.Vector3((x0+x1)/2,y,0);
    box(holder,x1-x0,.45f,0,0);
    for(float x=x0+.45f;x<x1;x+=.9f)prop("FencePanel","ChainFence",holder.transform,x,y,.95f);
};
fenceRun("South_ClosedCourtyard",0,6.7f,20.2f);
fenceRun("South_QuarantineCordon",10.4f,40f,20.2f);
fenceRun("North_QuarantineCordon",0,29.6f,36.2f);
fenceRun("North_ClosedCourtyard",33.1f,40f,36.2f);
for(int i=0;i<4;i++) {
    prop("South_RoadBarrier",i%2==0?"RoadBarricade":"DamagedBarricade",cordons,18f+i*1.1f,19.7f,.9f);
    prop("North_RoadBarrier",i%2==0?"DamagedBarricade":"RoadBarricade",cordons,18f+i*1.1f,35.7f,.9f);
}

var markSprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/GameMain/Res/Map/StreetBlock01/LaneMark.asset");
System.Action<string,float,float,float,float> mark=delegate(string name,float x,float y,float width,float height) {
    var go=new UnityEngine.GameObject(name);go.transform.SetParent(markings);go.transform.position=new UnityEngine.Vector3(x,y,0);go.transform.localScale=new UnityEngine.Vector3(width,height,1);
    var sr=go.AddComponent<UnityEngine.SpriteRenderer>();sr.sprite=markSprite;sr.color=new UnityEngine.Color(.68f,.64f,.49f,.58f);sr.sortingOrder=1;
};
for(float y=6.5f;y<40;y+=3)if(System.Math.Abs(y-12)>2&&System.Math.Abs(y-31)>2&&System.Math.Abs(y-20)>2&&System.Math.Abs(y-36)>2)mark("FadedCenterDash",20,y,.07f,1.05f);
for(float x=4;x<36;x+=3)if(System.Math.Abs(x-20)>3){mark("LowerStreetDash",x,12.5f,.8f,.06f);mark("MarketStreetDash",x,31.5f,.8f,.06f);}
for(int i=0;i<5;i++) {mark("Crosswalk_South",18.25f+i*.75f,14.1f,.42f,.6f);mark("Crosswalk_Market",18.25f+i*.75f,29.7f,.42f,.6f);}
for(float x=24;x<=36;x+=3.5f){mark("ParkingBay",x,8.5f,.05f,2.8f);mark("ParkingBackLine",x+1.4f,10.1f,2.8f,.05f);}
var spawn=prop("PlayerSpawnPoint","StartSafetyDoor",markers,7.5f,6.5f,.6f);spawn.GetComponent<UnityEngine.SpriteRenderer>().enabled=false;
var exit=prop("ExitPoint","ExitSafetyDoor",markers,30.4f,44f,.6f);exit.GetComponent<UnityEngine.SpriteRenderer>().enabled=false;
if(oldRoot!=null){var s=oldRoot.transform.Find("PlayerSpawnPoint");if(s!=null)s.position=new UnityEngine.Vector3(7.5f,5f,0);var e=oldRoot.transform.Find("ExitPoint");if(e!=null)e.position=new UnityEngine.Vector3(30.4f,43.4f,0);}
var player=UnityEngine.GameObject.Find("SurvivorPlayer");UnityEditor.Undo.RecordObject(player.transform,"Position at safehouse");player.transform.position=new UnityEngine.Vector3(7.5f,5f,0);
var cam=UnityEngine.GameObject.Find("MainCamera");var follow=cam.GetComponent<CameraFollow2D>();
if(follow!=null){UnityEditor.Undo.RecordObject(follow,"Bind camera follow");follow.SetTarget(player.transform);UnityEditor.EditorUtility.SetDirty(follow);}
cam.transform.position=new UnityEngine.Vector3(7.5f,5f,-10);
UnityEngine.Physics2D.SyncTransforms();
UnityEditor.EditorUtility.SetDirty(ground);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
if(UnityEditor.SceneView.lastActiveSceneView!=null){var sv=UnityEditor.SceneView.lastActiveSceneView;sv.in2DMode=true;sv.LookAt(new UnityEngine.Vector3(20,24,0),UnityEngine.Quaternion.identity,27,true,true);}
return "Saved redesigned map: road="+roadCount+", sidewalk="+pavementCount+", grass="+grassCount+", buildings="+buildings.childCount+", vehicles="+vehicles.childCount+", clutter="+planting.childCount+". Camera target bound.";
