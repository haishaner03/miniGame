UnityEngine.Physics2D.SyncTransforms();
const int width=160,height=192;
const float step=.25f;
var blocked=new bool[width*height];
var distance=new int[width*height];var previous=new int[width*height];
for(int i=0;i<previous.Length;i++){previous[i]=-1;distance[i]=-1;}
int obstacles=0;
for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
    int index=y*width+x;
    foreach(var c in UnityEngine.Physics2D.OverlapBoxAll(new UnityEngine.Vector2((x+.5f)*step,(y+.5f)*step),new UnityEngine.Vector2(.42f,.26f),0)) {
        if(c==null||c.name=="SurvivorPlayer"||c.isTrigger||c.name=="CollisionTilemap")continue;
        blocked[index]=true;obstacles++;break;
    }
}
var spawn=UnityEngine.GameObject.Find("ZombieStreetLayout/LevelMarkers/PlayerSpawnPoint").transform.position;
var exit=UnityEngine.GameObject.Find("ZombieStreetLayout/LevelMarkers/ExitPoint").transform.position;
int start=(int)(spawn.y/step)*width+(int)(spawn.x/step);
int goal=(int)(exit.y/step)*width+(int)(exit.x/step);
var queue=new System.Collections.Generic.Queue<int>();queue.Enqueue(start);distance[start]=0;
int[] dx={1,-1,0,0},dy={0,0,1,-1};
int reachable=0;
while(queue.Count>0) {
    int index=queue.Dequeue();reachable++;
    int x=index%width,y=index/width;
    for(int i=0;i<4;i++) {
        int nx=x+dx[i],ny=y+dy[i];if(nx<0||nx>=width||ny<0||ny>=height)continue;
        int next=ny*width+nx;if(blocked[next]||distance[next]>=0)continue;
        previous[next]=index;distance[next]=distance[index]+1;queue.Enqueue(next);
    }
}
if(blocked[start]||blocked[goal]||distance[goal]<0)throw new System.Exception("Spawn/exit route is obstructed.");
var path=new System.Collections.Generic.List<int>();for(int p=goal;p>=0;p=previous[p])path.Add(p);path.Reverse();
var csv=new System.Text.StringBuilder("x,y\n");
float southGap=-1,northGap=-1;
foreach(int p in path) {
    float x=(p%width+.5f)*step,y=(p/width+.5f)*step;
    csv.Append(x.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",").Append(y.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\n");
    if(System.Math.Abs(y-20.2f)<.2f)southGap=x;
    if(System.Math.Abs(y-36.2f)<.2f)northGap=x;
}
if(southGap<6.7f||southGap>10.4f||northGap<29.6f||northGap>33.1f)throw new System.Exception("Route bypasses an intended quarantine gap.");
System.IO.File.WriteAllText("D:/UnityProject/miniGame/.codex_artifacts/street_redesign/ValidatedRoute.csv",csv.ToString());
var report=new System.Text.StringBuilder();
report.AppendLine("Scene: "+UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path);
report.AppendLine("Spawn clear: "+!blocked[start]+"; Exit clear: "+!blocked[goal]);
report.AppendLine("Quarter-cell collision sampling, clearance box 0.42 x 0.26 (larger than player collider).");
report.AppendLine("Route length: "+(distance[goal]*step)+" units; reachable samples="+reachable+"; blocked samples="+obstacles);
report.AppendLine("South cordon crossed via western alley at x="+southGap);
report.AppendLine("North cordon crossed via eastern street at x="+northGap);
var ground=UnityEngine.GameObject.Find("GroundTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
int count=0,roadCount=0;
var roadCells=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();
foreach(var p in ground.cellBounds.allPositionsWithin) {var t=ground.GetTile(p);if(t==null)continue;count++;if(t.name.StartsWith("Tile_WornAsphalt")){roadCount++;roadCells.Add(p);}}
var seen=new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>();var roadQueue=new System.Collections.Generic.Queue<UnityEngine.Vector3Int>();
foreach(var p in roadCells){roadQueue.Enqueue(p);seen.Add(p);break;}
while(roadQueue.Count>0) {var p=roadQueue.Dequeue();for(int i=0;i<4;i++){var n=p+new UnityEngine.Vector3Int(dx[i],dy[i],0);if(roadCells.Contains(n)&&seen.Add(n))roadQueue.Enqueue(n);}}
if(seen.Count!=roadCount)throw new System.Exception("Disconnected asphalt road cells: "+(roadCount-seen.Count));
report.AppendLine("Ground tiles: "+count+"/1920; asphalt connectivity: "+seen.Count+"/"+roadCount);
var camera=UnityEngine.GameObject.Find("MainCamera");var so=new UnityEditor.SerializedObject(camera.GetComponent<CameraFollow2D>());
report.AppendLine("Camera target: "+so.FindProperty("target").objectReferenceValue+"; ortho size="+camera.GetComponent<UnityEngine.Camera>().orthographicSize+"; smoothing="+so.FindProperty("smoothTime").floatValue);
int activeColliders=0;foreach(var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Collider2D>(UnityEngine.FindObjectsSortMode.None))if(c.enabled&&!c.isTrigger&&c.bounds.size.sqrMagnitude>.001f)activeColliders++;
report.AppendLine("Active nonempty solid colliders: "+activeColliders);
System.IO.File.WriteAllText("D:/UnityProject/miniGame/.codex_artifacts/street_redesign/UnityValidation.txt",report.ToString());
return report.ToString();
