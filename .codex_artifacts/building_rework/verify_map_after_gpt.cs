var ground = UnityEngine.GameObject.Find("GroundTilemap");
var collision = UnityEngine.GameObject.Find("CollisionTilemapCollider2D");
var composite = UnityEngine.GameObject.Find("CollisionTilemapCollider2D");
int groundCount = -1;
if (ground != null) { var tm = ground.GetComponent<UnityEngine.Tilemaps.Tilemap>(); if (tm != null) foreach (var p in tm.cellBounds.allPositionsWithin) if (tm.GetTile(p) != null) groundCount++; }
bool colliderEnabled = false;
if (collision != null) { var c = collision.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>(); colliderEnabled = c != null && c.enabled; }
return "groundTiles=" + groundCount + "; collisionTilemapColliderEnabled=" + colliderEnabled + "; buildingVisuals=" + (UnityEngine.GameObject.Find("BuildingVisuals") != null);
