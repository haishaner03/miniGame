var go = UnityEngine.GameObject.Find("GroundTilemap");
var map = go.GetComponent<UnityEngine.Tilemaps.Tilemap>();
var counts = new System.Collections.Generic.Dictionary<string,int>();
var samples = new System.Collections.Generic.Dictionary<string,string>();
foreach (var p in map.cellBounds.allPositionsWithin)
{
    var tile = map.GetTile(p);
    if (tile == null) continue;
    string n = tile.name;
    if (n.IndexOf("Curb", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
    if (!counts.ContainsKey(n)) { counts[n] = 0; samples[n] = p.ToString(); }
    counts[n]++;
}
var lines = new System.Collections.Generic.List<string>();
foreach (var kv in counts) lines.Add(kv.Key + "=" + kv.Value + " sample=" + samples[kv.Key]);
return string.Join("\n", lines.ToArray());
