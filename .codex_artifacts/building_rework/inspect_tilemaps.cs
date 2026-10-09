string[] names = new string[] {"GroundTilemap", "GroundDetailTilemap", "ObstacleTilemap", "BuildingTilemap"};
var lines = new System.Collections.Generic.List<string>();
foreach (var name in names)
{
    var go = UnityEngine.GameObject.Find(name);
    var tm = go == null ? null : go.GetComponent<UnityEngine.Tilemaps.Tilemap>();
    if (tm == null) { lines.Add(name + ":missing"); continue; }
    int count = 0;
    var sprites = new System.Collections.Generic.Dictionary<string,int>();
    foreach (var p in tm.cellBounds.allPositionsWithin)
    {
        var tile = tm.GetTile(p);
        if (tile == null) continue;
        count++;
        var sprite = tm.GetSprite(p);
        string sn = sprite == null ? "null" : sprite.name;
        if (!sprites.ContainsKey(sn)) sprites[sn] = 0;
        sprites[sn]++;
    }
    var top = new System.Collections.Generic.List<string>();
    foreach (var kv in sprites) top.Add(kv.Key + "=" + kv.Value);
    lines.Add(name + ":count=" + count + ",bounds=" + tm.cellBounds.min + ".." + tm.cellBounds.max + ",sprites=" + string.Join("|", top.ToArray()));
}
return string.Join("\n", lines.ToArray());
