#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using D = ZombieLevel02LayoutData;

/// <summary>Tilemap painting: ground, decor, blocking props, invisible collision layer.</summary>
public static partial class ZombieLevel02Builder
{
    private const string WalkGround = "RhvySCdg";
    private const string BlockProps = "WwNnFXDQOJKkUT";
    private const string RoadChars = "Rhvy";

    private struct Maps
    {
        public Tilemap Ground, Detail, Building, Obstacle, Gameplay, Collision;
    }

    private static char G(int x, int y, ZombieStreetLayout data) => data.G(x, y);

    private static char P(int x, int y, ZombieStreetLayout data) => data.P(x, y);

    private static int Hash(int x, int y, int seed = 0) => (int)((uint)(x * 73856093 ^ y * 19349663 ^ seed ^ 0x5bd1e995) % 1000u);

    private static bool IsBlocked(int x, int y, ZombieStreetLayout data) =>
        (WalkGround.IndexOf(G(x, y, data)) < 0 && !(data.IsRandom && G(x, y, data) == '#')) || BlockProps.IndexOf(P(x, y, data)) >= 0;

    private static void ValidateLayout(ZombieStreetLayout data)
    {
        if (data.Ground.Length != data.Height || data.Props.Length != data.Height)
            throw new System.Exception("Layout row count does not match Height.");
        for (int i = 0; i < data.Height; i++)
        {
            if (data.Ground[i].Length != data.Width || data.Props[i].Length != data.Width)
                throw new System.Exception("Layout row " + i + " width does not match Width.");
        }
    }

    private static Maps FindMaps()
    {
        GameObject grid = GameObject.Find("Grid");
        if (grid == null)
            throw new System.Exception("Scene has no 'Grid' root.");
        Tilemap Get(string n)
        {
            Transform t = grid.transform.Find(n);
            Tilemap m = t != null ? t.GetComponent<Tilemap>() : null;
            if (m == null)
                throw new System.Exception("Missing tilemap: Grid/" + n);
            return m;
        }
        return new Maps
        {
            Ground = Get("GroundTilemap"), Detail = Get("GroundDetailTilemap"), Building = Get("BuildingTilemap"),
            Obstacle = Get("ObstacleTilemap"), Gameplay = Get("GameplayTilemap"), Collision = Get("CollisionTilemap"),
        };
    }

    private static void Put(Tilemap map, int x, int y, string tilePath, Matrix4x4? transform = null)
    {
        var cell = new Vector3Int(x, y, 0);
        map.SetTile(cell, Load<TileBase>(tilePath));
        if (transform.HasValue)
        {
            map.SetTileFlags(cell, TileFlags.None);
            map.SetTransformMatrix(cell, transform.Value);
        }
    }

    private static Matrix4x4 Rot(float degrees, Vector2 offset = default, float scale = 1f) =>
        Matrix4x4.TRS(offset, Quaternion.Euler(0f, 0f, degrees), new Vector3(scale, scale, 1f));

    private static void PaintTilemaps(Maps m, ZombieStreetLayout data)
    {
        // Ground and weeds are visual layers. Only CollisionTilemap blocks movement.
        foreach (Tilemap visual in new[] { m.Ground, m.Detail })
            foreach (Collider2D collider in visual.GetComponents<Collider2D>()) Object.DestroyImmediate(collider);
        MakeGrassTilesNonBlocking();
        TilemapRenderer[] order = { m.Ground.GetComponent<TilemapRenderer>(), m.Detail.GetComponent<TilemapRenderer>(),
            m.Building.GetComponent<TilemapRenderer>(), m.Obstacle.GetComponent<TilemapRenderer>(), m.Gameplay.GetComponent<TilemapRenderer>() };
        for (int i = 0; i < order.Length; i++)
            if (order[i] != null) order[i].sortingOrder = i;

        foreach (Tilemap t in new[] { m.Ground, m.Detail, m.Building, m.Obstacle, m.Gameplay, m.Collision })
            t.ClearAllTiles();

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                PaintGround(m.Ground, x, y, data);
                PaintProp(m, x, y, data);
            }
        }

        PaintCollision(m.Collision, data);
    }

    // Sheet sprite names are col_row with row counted from the bottom of the 256px sheet.
    private static void PaintGround(Tilemap map, int x, int y, ZombieStreetLayout data)
    {
        char g = G(x, y, data);
        char p = P(x, y, data);
        int h = Hash(x, y, data.Seed);

        // Road decals replace the base tile (the sheet tiles are opaque).
        if (RoadChars.IndexOf(g) >= 0 && (p == 'o' || p == '*'))
        {
            Put(map, x, y, p == 'o' ? Road(h % 2, 1) : Road(2 + h % 2, 1));
            return;
        }

        switch (g)
        {
            case 'R': Put(map, x, y, Road(3, 2)); return;
            case 'y': Put(map, x, y, Road(0, 0), Rot(90f)); return;
            case 'h': Put(map, x, y, Road(1, 0), Rot(90f)); return;
            case 'v': Put(map, x, y, Road(1, 0)); return;
            case 'g': Put(map, x, y, Grass(0, 3)); return;
            case 'd':
                if (p == '*') { Put(map, x, y, Grass(3, 2)); return; }
                if (p == 'o') { Put(map, x, y, Grass(0, 2)); return; }
                Put(map, x, y, h % 3 == 0 ? Grass(2, 3) : h % 3 == 1 ? Grass(3, 0) : Grass(2, 1));
                return;
            case 'S':
            case 'C':
                Put(map, x, y, SidewalkTile(x, y, g == 'C', data));
                return;
            case 'B':
                Put(map, x, y, $"{Redesign}/Tiles/Tile_CourtyardConcrete.asset");
                return;
            default:
                if (p == '*') { Put(map, x, y, Grass(0, 0)); return; }
                Put(map, x, y, h % 4 == 1 ? Grass(3, 3) : h % 4 == 3 ? Grass(3, 1) : Grass(0, 3));
                return;
        }
    }

    // Curb mask: N=1 E=2 S=4 W=8 (verified against Sidewalk_NN pixel edges). Corner tiles for diagonal-only roads.
    private static string SidewalkTile(int x, int y, bool plaza, ZombieStreetLayout data)
    {
        bool R(int dx, int dy) => RoadChars.IndexOf(G(x + dx, y + dy, data)) >= 0;
        int mask = (R(0, 1) ? 1 : 0) | (R(1, 0) ? 2 : 0) | (R(0, -1) ? 4 : 0) | (R(-1, 0) ? 8 : 0);
        if (mask != 0)
            return Sidewalk(mask.ToString("00"));
        if (R(1, 1)) return Sidewalk("Corner_NE");
        if (R(-1, 1)) return Sidewalk("Corner_NW");
        if (R(1, -1)) return Sidewalk("Corner_SE");
        if (R(-1, -1)) return Sidewalk("Corner_SW");
        return plaza ? $"{Redesign}/Tiles/Tile_CourtyardConcrete.asset" : Sidewalk("00");
    }

    private static void PaintProp(Maps m, int x, int y, ZombieStreetLayout data)
    {
        int h = Hash(x, y, data.Seed);
        switch (P(x, y, data))
        {
            // walkable decor
            case ',': Put(m.Detail, x, y, PropTile("DryGrass")); break;
            case ';': Put(m.Detail, x, y, PropTile("DryGrassTall")); break;
            case '^': Put(m.Detail, x, y, PropTile("Scrub")); break;
            // vehicles: anchor is the bottom-left cell of the footprint
            case 'W': Put(m.Gameplay, x, y, PropTile("WreckedCar"), Rot(90f * (h % 4), new Vector2(0.5f, 0.5f), 1.35f)); break;
            case 'N': Put(m.Gameplay, x, y, PropTile("AbandonedSedan"), Rot(h % 2 == 0 ? 0f : 180f, new Vector2(0f, 0.5f), 1.25f)); break;
            // barriers and clutter
            case 'F': Put(m.Gameplay, x, y, PropTile("ChainFence"), Rot(0f, default, 1.1f)); break;
            case 'X': Put(m.Gameplay, x, y, PropTile("RoadBarricade")); break;
            case 'D': Put(m.Gameplay, x, y, PropTile("DamagedBarricade")); break;
            case 'Q': Put(m.Gameplay, x, y, PropTile("Sandbags")); break;
            case 'O': Put(m.Gameplay, x, y, PropTile("RustBarrel")); break;
            case 'J': Put(m.Gameplay, x, y, PropTile("WasteBin")); break;
            case 'K': Put(m.Obstacle, x, y, PropTile("RockLarge")); break;
            case 'k': Put(m.Obstacle, x, y, PropTile("RockPile")); break;
            case 'U': Put(m.Obstacle, x, y, PropTile("RockTall")); break;
            // 'T' trees are depth-sorted sprites (see PlaceTrees); 'w'/'n' are footprint fill; digits are gates.
        }
    }

    private static void PaintCollision(Tilemap map, ZombieStreetLayout data)
    {
        TileBase solid = GetOrCreateCollisionTile();
        for (int y = 0; y < data.Height; y++)
            for (int x = 0; x < data.Width; x++)
                if (IsBlocked(x, y, data))
                    map.SetTile(new Vector3Int(x, y, 0), solid);

        TilemapRenderer r = map.GetComponent<TilemapRenderer>();
        if (r != null)
            r.enabled = false;

        Rigidbody2D body = map.GetComponent<Rigidbody2D>();
        if (body == null)
            body = map.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;

        TilemapCollider2D tc = map.GetComponent<TilemapCollider2D>();
        if (tc == null)
            tc = map.gameObject.AddComponent<TilemapCollider2D>();

        CompositeCollider2D composite = map.GetComponent<CompositeCollider2D>();
        if (composite == null)
            composite = map.gameObject.AddComponent<CompositeCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        // Polygons, not Outlines: the pathfinder uses OverlapCircle at cell centres, which misses hollow outlines.
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        composite.isTrigger = false;
        tc.ProcessTilemapChanges();
        composite.GenerateGeometry();
        if (data.IsRandom) CreateMapBoundary(data.Width, data.Height);
    }

    private static void MakeGrassTilesNonBlocking()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { MapRoot + "/Tiles4x4/Ground" }))
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile == null || tile.colliderType == Tile.ColliderType.None) continue;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
        }
    }

    private static void CreateMapBoundary(int width, int height)
    {
        GameObject root = GameObject.Find("RandomMapBoundary");
        if (root != null) Object.DestroyImmediate(root);
        root = new GameObject("RandomMapBoundary");
        Vector2[] centers = { new Vector2(-.5f, height * .5f), new Vector2(width + .5f, height * .5f),
            new Vector2(width * .5f, -.5f), new Vector2(width * .5f, height + .5f) };
        Vector2[] sizes = { new Vector2(1, height + 2), new Vector2(1, height + 2),
            new Vector2(width + 2, 1), new Vector2(width + 2, 1) };
        for (int i = 0; i < centers.Length; i++)
        {
            var edge = new GameObject("Boundary_" + i); edge.transform.SetParent(root.transform, false);
            edge.transform.position = centers[i]; edge.AddComponent<BoxCollider2D>().size = sizes[i];
        }
    }

    public static int RemoveCurrentRandomGrassCollisions()
    {
        if (!CanBuild()) throw new System.InvalidOperationException("请先停止游戏并等待编译结束。");
        Transform seedMarker = null;
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (candidate.name.StartsWith("RandomSeed_", System.StringComparison.Ordinal)) { seedMarker = candidate; break; }
        if (seedMarker == null || !int.TryParse(seedMarker.name.Substring("RandomSeed_".Length), out int seed))
            throw new System.InvalidOperationException("当前场景没有随机地图种子。请先生成随机地图。");
        var data = CreateRandomLayout(seed);
        Maps maps = FindMaps();
        int before = 0, after = 0;
        foreach (Vector3Int p in maps.Collision.cellBounds.allPositionsWithin) if (maps.Collision.HasTile(p)) before++;
        Undo.RegisterCompleteObjectUndo(maps.Collision, "Remove grass collision");
        maps.Collision.ClearAllTiles();
        PaintCollision(maps.Collision, data);
        foreach (Vector3Int p in maps.Collision.cellBounds.allPositionsWithin) if (maps.Collision.HasTile(p)) after++;
        foreach (Tilemap visual in new[] { maps.Ground, maps.Detail })
            foreach (Collider2D collider in visual.GetComponents<Collider2D>()) Object.DestroyImmediate(collider);
        MakeGrassTilesNonBlocking();
        Physics2D.SyncTransforms();
        Object.FindFirstObjectByType<ZombieGridPathfinder>()?.Rebuild();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return before - after;
    }

    private static TileBase GetOrCreateCollisionTile()
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(CollisionTilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, CollisionTilePath);
        }
        Sprite s = Load<Sprite>($"{Redesign}/CourtyardConcrete.png");
        if (s != null)
            tile.sprite = s;
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        return tile;
    }
}
#endif
