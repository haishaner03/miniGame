var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (scene.path != "Assets/GameMain/Scenes/Level/ZombieLevel01.unity") throw new System.Exception("Active scene is not ZombieLevel01: " + scene.path);
var old = UnityEngine.GameObject.Find("BuildingTilemap");
if (old == null) throw new System.Exception("BuildingTilemap not found");
var map = old.GetComponent<UnityEngine.Tilemaps.Tilemap>();
if (map == null) throw new System.Exception("BuildingTilemap has no Tilemap");
string rootPath = "Assets/GameMain/Res/Map/StreetBlock01/Buildings/";
string[] assets = {"Building_ConvenienceStore.png","Building_Apartment.png","Building_Warehouse.png","Building_Safehouse.png"};
foreach (string fn in assets)
{
    string path = rootPath + fn;
    UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var imp = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
    if (imp == null) throw new System.Exception("Texture importer missing: " + path);
    imp.textureType=UnityEditor.TextureImporterType.Sprite; imp.spriteImportMode=UnityEditor.SpriteImportMode.Single; imp.spritePixelsPerUnit=64; imp.filterMode=UnityEngine.FilterMode.Point; imp.mipmapEnabled=false; imp.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed; imp.alphaIsTransparency=true; imp.wrapMode=UnityEngine.TextureWrapMode.Clamp; imp.SaveAndReimport();
}
var sprites = new System.Collections.Generic.Dictionary<string,UnityEngine.Sprite>();
foreach (string fn in assets){var sp=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(rootPath+fn);if(sp==null)throw new System.Exception("Sprite not imported: "+fn);sprites[fn]=sp;}
var renderer=old.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>(); if(renderer!=null)renderer.enabled=false;
var root=UnityEngine.GameObject.Find("BuildingVisuals"); if(root==null)root=new UnityEngine.GameObject("BuildingVisuals"); root.transform.position=UnityEngine.Vector3.zero; root.transform.rotation=UnityEngine.Quaternion.identity; root.transform.localScale=UnityEngine.Vector3.one;
for(int i=root.transform.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
var occupied=new System.Collections.Generic.HashSet<UnityEngine.Vector2Int>(); foreach(var p in map.cellBounds.allPositionsWithin)if(map.GetTile(p)!=null)occupied.Add(new UnityEngine.Vector2Int(p.x,p.y));
var comps=new System.Collections.Generic.List<System.Collections.Generic.List<UnityEngine.Vector2Int>>(); var dirs=new[]{new UnityEngine.Vector2Int(1,0),new UnityEngine.Vector2Int(-1,0),new UnityEngine.Vector2Int(0,1),new UnityEngine.Vector2Int(0,-1)};
while(occupied.Count>0){var start=new System.Collections.Generic.List<UnityEngine.Vector2Int>(occupied)[0];var q=new System.Collections.Generic.Queue<UnityEngine.Vector2Int>();var comp=new System.Collections.Generic.List<UnityEngine.Vector2Int>();q.Enqueue(start);occupied.Remove(start);while(q.Count>0){var cur=q.Dequeue();comp.Add(cur);foreach(var d in dirs){var n=cur+d;if(occupied.Remove(n))q.Enqueue(n);}}comps.Add(comp);}
comps.Sort(delegate(System.Collections.Generic.List<UnityEngine.Vector2Int> a,System.Collections.Generic.List<UnityEngine.Vector2Int> b){int ax=99999,ay=99999,bx=99999,by=99999;foreach(var p in a){if(p.x<ax)ax=p.x;if(p.y<ay)ay=p.y;}foreach(var p in b){if(p.x<bx)bx=p.x;if(p.y<by)by=p.y;}return ay!=by?ay.CompareTo(by):ax.CompareTo(bx);});
System.Func<int,int,int,int,int,string> Pick=delegate(int minX,int minY,int w,int h,int idx){if((minX==14&&minY==2)||(minX==14&&minY==42))return "Building_Safehouse.png";if(w>=7||(minX==31&&minY==26))return "Building_Warehouse.png";if(w>=6||(minX==33&&minY==3)||(minX==9&&minY==27))return "Building_Apartment.png";if(minX==2&&minY==12)return "Building_Safehouse.png";return idx%2==0?"Building_ConvenienceStore.png":"Building_Apartment.png";};
var summary=new System.Collections.Generic.List<object>();int created=0;
for(int i=0;i<comps.Count;i++){var comp=comps[i];int minX=99999,minY=99999,maxX=-99999,maxY=-99999;foreach(var p in comp){if(p.x<minX)minX=p.x;if(p.y<minY)minY=p.y;if(p.x>maxX)maxX=p.x;if(p.y>maxY)maxY=p.y;}int w=maxX-minX+1,h=maxY-minY+1;string fn=Pick(minX,minY,w,h,i);var sp=sprites[fn];var go=new UnityEngine.GameObject("Building_"+i.ToString("00")+"_"+fn.Replace("Building_","").Replace(".png","") );go.transform.SetParent(root.transform,false);var worldMin=map.CellToWorld(new UnityEngine.Vector3Int(minX,minY,0));go.transform.position=worldMin+new UnityEngine.Vector3(w*0.5f,h*0.5f,-0.05f);var sr=go.AddComponent<UnityEngine.SpriteRenderer>();sr.sprite=sp;sr.sortingOrder=3;sr.drawMode=UnityEngine.SpriteDrawMode.Simple;sr.maskInteraction=UnityEngine.SpriteMaskInteraction.None;float sx=(w*0.96f)/sp.bounds.size.x;float sy=(h*0.96f)/sp.bounds.size.y;go.transform.localScale=new UnityEngine.Vector3(sx,sy,1f);summary.Add(new{index=i,x=minX,y=minY,width=w,height=h,asset=fn,scale=new[]{sx,sy}});created++;}
UnityEditor.EditorUtility.SetDirty(old);UnityEditor.EditorUtility.SetDirty(root);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);UnityEditor.AssetDatabase.SaveAssets();UnityEditor.AssetDatabase.Refresh();
return new{created=created,components=comps.Count,buildingTilemapRendererDisabled=renderer==null?false:!renderer.enabled,visualRoot=root.name,placements=summary};
