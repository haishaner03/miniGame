var maps=UnityEngine.Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(UnityEngine.FindObjectsSortMode.None);
var mapList=new System.Collections.Generic.List<object>();
foreach(var map in maps)
{
    if(!map.gameObject.scene.IsValid())continue;
    var cells=new System.Collections.Generic.List<object>();
    foreach(var p in map.cellBounds.allPositionsWithin)
    {
        var tile=map.GetTile(p); if(tile==null)continue;
        var sprite=map.GetSprite(p);
        cells.Add(new { x=p.x,y=p.y,z=p.z,tile=tile.name,asset=UnityEditor.AssetDatabase.GetAssetPath(tile),sprite=sprite==null?null:sprite.name});
    }
    var renderer=map.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
    mapList.Add(new {name=map.name,position=map.transform.position.ToString(),scale=map.transform.lossyScale.ToString(),order=renderer==null?0:renderer.sortingOrder,cells=cells});
}
var objects=new System.Collections.Generic.List<object>();
foreach(var sr in UnityEngine.Object.FindObjectsByType<UnityEngine.SpriteRenderer>(UnityEngine.FindObjectsSortMode.None))
{
    if(!sr.gameObject.scene.IsValid()||sr.sprite==null)continue;
    objects.Add(new {name=sr.name,position=sr.transform.position.ToString(),scale=sr.transform.lossyScale.ToString(),sprite=sr.sprite.name,asset=UnityEditor.AssetDatabase.GetAssetPath(sr.sprite),bounds=sr.bounds.ToString(),order=sr.sortingOrder});
}
var colliders=new System.Collections.Generic.List<object>();
foreach(var col in UnityEngine.Object.FindObjectsByType<UnityEngine.Collider2D>(UnityEngine.FindObjectsSortMode.None))
    if(col.gameObject.scene.IsValid())colliders.Add(new {name=col.name,type=col.GetType().Name,position=col.transform.position.ToString(),bounds=col.bounds.ToString(),enabled=col.enabled,trigger=col.isTrigger});
return new {maps=mapList,objects=objects,colliders=colliders};
