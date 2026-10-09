var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var ground=UnityEngine.GameObject.Find("GroundTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
var obstacle=UnityEngine.GameObject.Find("ObstacleTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
var gameplay=UnityEngine.GameObject.Find("GameplayTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
int total=0,missing=0,road=0,props=0;var tileNames=new System.Collections.Generic.HashSet<string>();
foreach(var map in new[]{ground,obstacle,gameplay})foreach(var p in map.cellBounds.allPositionsWithin){var t=map.GetTile(p);if(t==null)continue;total++;tileNames.Add(t.name);if(map==ground&&(t.name.StartsWith("Tile_Concrete")||t.name.StartsWith("Tile_Worn")))road++;if(map!=ground&&t.name.StartsWith("Tile_"))props++;if(map.GetSprite(p)==null)missing++;}
var player=UnityEngine.GameObject.Find("SurvivorPlayer");var rb=player==null?null:player.GetComponent<UnityEngine.Rigidbody2D>();
string report="scene="+scene.path+" totalTiles="+total+" roadTiles="+road+" polishedPropTiles="+props+" missingSprites="+missing+" playerRigidbody="+(rb!=null)+" tileKinds="+tileNames.Count+"; sceneIsDirty="+scene.isDirty;
if(missing!=0||!scene.path.EndsWith("ZombieLevel01.unity"))throw new System.Exception(report);
System.IO.File.WriteAllText("D:/UnityProject/miniGame/.codex_artifacts/map_polish/UnityValidation.txt",report);
return report;
