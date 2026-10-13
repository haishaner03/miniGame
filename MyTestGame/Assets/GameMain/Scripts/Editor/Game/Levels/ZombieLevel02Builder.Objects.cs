#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using D = ZombieLevel02LayoutData;

/// <summary>Scene objects: buildings, trees, spawns, waves, gates, doors and component wiring.</summary>
public static partial class ZombieLevel02Builder
{
    private static void Build(BuildTarget target, ZombieStreetLayout data)
    {
        ValidateLayout(data);

        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
            if (System.Array.IndexOf(OldRoots, root.name) >= 0 || IsGeneratedLayoutRoot(root.name) ||
                (data.IsRandom && (root.name == "RoomVariantModules" || root.name == "RandomMapBoundary")))
                Object.DestroyImmediate(root);

        Transform layout = new GameObject(target.LayoutRootName).transform;
        if (data.IsRandom) Child(layout, "RandomSeed_" + data.Seed);
        Transform buildings = Child(layout, "Buildings");
        Transform props = Child(layout, "Trees");
        Transform gates = Child(layout, "ZoneGates");
        Transform waves = Child(layout, "WaveTriggers");
        Transform doors = Child(layout, "Doors");

        Maps maps = FindMaps();
        PaintTilemaps(maps, data);
        PlaceBuildings(buildings, data);
        PlaceTrees(props, data);

        ZombieSpawner spawner = Object.FindFirstObjectByType<ZombieSpawner>();
        if (spawner == null)
            throw new System.Exception("Scene has no ZombieSpawner.");
        Dictionary<string, Transform> spawns = RebuildSpawnPoints(spawner, data);

        var triggers = new Dictionary<string, ZombieWaveTrigger>();
        foreach (D.WaveDef w in data.Waves)
            triggers[w.Id] = PlaceWave(waves, w, spawns);
        foreach (D.GateDef g in data.Gates)
            PlaceGate(gates, g, triggers[g.WaveId], data);

        SafeDoor start = PlaceDoor(doors, "StartDoor", data.StartDoor, 0, null, new Vector2(2.5f, 1.2f), Vector2.zero);
        // 逻辑点和触发框位于可走路面；贴图单独上移，避免寻路把出口当成建筑内部。
        SafeDoor exit = PlaceDoor(doors, "ExitDoor", data.ExitDoor, 1,
            Load<Sprite>(PropSprite("ExitSafetyDoor")), new Vector2(2f, 1.4f), Vector2.zero, new Vector2(0f, 1.2f));

        Transform playerSpawn = new GameObject("PlayerSpawnPoint").transform;
        playerSpawn.SetParent(layout, false);
        playerSpawn.position = data.PlayerSpawn;

        ConfigureSpawner(spawner, spawns, data);
        ConfigureSystems(start, exit, playerSpawn, triggers, target.NextScenePath, data);
    }

    private static bool IsGeneratedLayoutRoot(string name)
    {
        const string prefix = "Level", suffix = "_Layout";
        return name.StartsWith(prefix, System.StringComparison.Ordinal) &&
            name.EndsWith(suffix, System.StringComparison.Ordinal) &&
            name.Length > prefix.Length + suffix.Length &&
            TryParseLevelNumber(name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length), out _);
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static void Edit(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty Prop(SerializedObject so, string name)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
            throw new System.Exception(so.targetObject.GetType().Name + " has no serialized field '" + name + "'.");
        return p;
    }

    private static void SetArray<T>(SerializedObject so, string name, IList<T> items) where T : Object
    {
        SerializedProperty p = Prop(so, name);
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static SpriteRenderer AddSprite(GameObject go, Sprite sprite)
    {
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        return sr;
    }

    // Sprite pivots differ (centre vs bottom), so align by bounds instead of pivot.
    private static void PlaceBuildings(Transform parent, ZombieStreetLayout data)
    {
        foreach (D.BuildingDef b in data.Buildings)
        {
            Sprite sprite = Load<Sprite>(BuildingSpritePath(b.Sprite));
            GameObject go = new GameObject(b.Name);
            go.transform.SetParent(parent, false);
            AddSprite(go, sprite);

            float scale = b.W / sprite.bounds.size.x;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            Bounds local = sprite.bounds;
            float px = b.X + b.W * 0.5f - local.center.x * scale;
            float py = b.Y - local.min.y * scale;
            go.transform.position = new Vector3(px, py, 0f);

            go.AddComponent<SurvivorDepthSort>().Configure(local.min.y * scale);
        }
    }

    private static void PlaceTrees(Transform parent, ZombieStreetLayout data)
    {
        Sprite tree = Load<Sprite>(PropSprite("DeadTree"));
        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                if (P(x, y, data) != 'T')
                    continue;
                GameObject go = new GameObject($"DeadTree_{x}_{y}");
                go.transform.SetParent(parent, false);
                SpriteRenderer sr = AddSprite(go, tree);
                sr.flipX = Hash(x, y, data.Seed) % 2 == 0;
                // Trunk base sits slightly above the bottom of its blocked cell.
                go.transform.position = new Vector3(x + 0.5f, y + 0.15f - tree.bounds.min.y, 0f);
                go.AddComponent<SurvivorDepthSort>().Configure(tree.bounds.min.y);
            }
        }
    }

    private static Dictionary<string, Transform> RebuildSpawnPoints(ZombieSpawner spawner, ZombieStreetLayout data)
    {
        for (int i = spawner.transform.childCount - 1; i >= 0; i--)
        {
            Transform c = spawner.transform.GetChild(i);
            if (c.name.StartsWith("SpawnPoint"))
                Object.DestroyImmediate(c.gameObject);
        }

        var map = new Dictionary<string, Transform>();
        foreach (D.SpawnDef s in data.Spawns)
        {
            Transform t = new GameObject("SpawnPoint_" + s.Id).transform;
            t.SetParent(spawner.transform, false);
            t.position = s.Position;
            map[s.Id] = t;
        }
        return map;
    }

    private static ZombieWaveTrigger PlaceWave(Transform parent, D.WaveDef w, Dictionary<string, Transform> spawns)
    {
        GameObject go = new GameObject("Trigger_" + w.Id);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((w.X0 + w.X1 + 1) * 0.5f, (w.Y0 + w.Y1 + 1) * 0.5f, 0f);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(w.X1 - w.X0 + 1, w.Y1 - w.Y0 + 1);

        ZombieWaveTrigger trigger = go.AddComponent<ZombieWaveTrigger>();
        var points = new List<Transform>();
        foreach (string id in w.Spawns)
            points.Add(spawns[id]);
        Edit(trigger, so =>
        {
            Prop(so, "waveId").stringValue = w.Id;
            Prop(so, "waveCount").intValue = w.Count;
            Prop(so, "triggerOnEnter").boolValue = true;
            Prop(so, "destroyAfterTriggered").boolValue = false;
            SetArray(so, "spawnPointsOverride", points);
        });
        return trigger;
    }

    private static void PlaceGate(Transform parent, D.GateDef g, ZombieWaveTrigger wave, ZombieStreetLayout data)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int y = 0; y < data.Height; y++)
            for (int x = 0; x < data.Width; x++)
                if (P(x, y, data) == g.Mark)
                {
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
        if (minX == int.MaxValue)
            throw new System.Exception("Gate mark '" + g.Mark + "' not found in Props layout.");

        GameObject go = new GameObject(g.Name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f, 0f);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(maxX - minX + 1, maxY - minY + 1);
        Edit(go.AddComponent<ZoneGate>(), so => Prop(so, "requiredWave").objectReferenceValue = wave);

        // Barricade line along the long axis, a wreck in the middle as the visual anchor.
        Sprite[] cycle = { Load<Sprite>(PropSprite("RoadBarricade")), Load<Sprite>(PropSprite("Sandbags")), Load<Sprite>(PropSprite("DamagedBarricade")) };
        bool horizontal = maxX - minX >= maxY - minY;
        int from = horizontal ? minX : minY, to = horizontal ? maxX : maxY;
        int mid = (from + to) / 2;
        for (int i = from; i <= to; i++)
        {
            bool wreck = i == mid;
            Sprite s = wreck ? Load<Sprite>(PropSprite("WreckedCar")) : cycle[(i - from) % cycle.Length];
            GameObject d = new GameObject(wreck ? "Wreck" : s.name);
            d.transform.SetParent(go.transform, false);
            float cx = horizontal ? i + 0.5f : (minX + maxX + 1) * 0.5f;
            float cy = horizontal ? (minY + maxY + 1) * 0.5f : i + 0.5f;
            d.transform.position = new Vector3(cx, cy, 0f);
            float scale = wreck ? 1.6f : 1.15f;
            d.transform.localScale = new Vector3(scale, scale, 1f);
            if (wreck)
                d.transform.rotation = Quaternion.Euler(0f, 0f, horizontal ? 90f : 0f);
            AddSprite(d, s);
            d.AddComponent<SurvivorDepthSort>().Configure(s.bounds.min.y * scale, 2);
        }
    }

    private static SafeDoor PlaceDoor(Transform parent, string name, Vector2 pos, int role, Sprite sprite, Vector2 size, Vector2 offset, Vector2 visualOffset = default)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        SpriteRenderer marker = null;
        if (sprite != null)
        {
            Transform visual = Child(go.transform, "Visual");
            visual.localPosition = visualOffset;
            marker = AddSprite(visual.gameObject, sprite);
            visual.gameObject.AddComponent<SurvivorDepthSort>().Configure(sprite.bounds.min.y, 5);
        }
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        box.offset = offset;
        SafeDoor door = go.AddComponent<SafeDoor>();
        Edit(door, so =>
        {
            Prop(so, "role").enumValueIndex = role;
            Prop(so, "locked").boolValue = role == 1;
            Prop(so, "markerRenderer").objectReferenceValue = marker;
        });
        return door;
    }

    private static void ConfigureSpawner(ZombieSpawner spawner, Dictionary<string, Transform> spawns, ZombieStreetLayout data)
    {
        var ambient = new List<Transform>();
        foreach (string id in data.AmbientSpawns)
            ambient.Add(spawns[id]);
        spawner.spawnPoints = ambient.ToArray();
        spawner.initialSpawnCount = 3;
        spawner.maxAlive = 16;
        spawner.spawnInterval = 2f;
        spawner.minSpawnDistance = 6f;
        spawner.spawnJitter = 0.35f;
        spawner.spawnOnStart = true;
        spawner.respawnAfterDeath = false;
        // Timed waves would keep ActiveCount above zero and stall the zone gates.
        spawner.enableTimedWaves = false;
        spawner.useObjectPool = true;
        spawner.poolPrewarm = 16;
        EditorUtility.SetDirty(spawner);

        ZombieGridPathfinder pathfinder = Object.FindFirstObjectByType<ZombieGridPathfinder>();
        if (pathfinder == null)
            pathfinder = spawner.gameObject.AddComponent<ZombieGridPathfinder>();
        Edit(pathfinder, so =>
        {
            Prop(so, "gridMin").vector2Value = Vector2.zero;
            Prop(so, "gridMax").vector2Value = new Vector2(data.Width, data.Height);
        });
    }

    private static void ConfigureSystems(SafeDoor start, SafeDoor exit, Transform playerSpawn, Dictionary<string, ZombieWaveTrigger> triggers, string nextScenePath, ZombieStreetLayout data)
    {
        SurvivorHealth player = Object.FindFirstObjectByType<SurvivorHealth>();
        if (player != null)
        {
            Edit(player, so => Prop(so, "spawnPoint").objectReferenceValue = playerSpawn);
            player.transform.position = new Vector3(data.PlayerSpawn.x, data.PlayerSpawn.y, player.transform.position.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
        }

        CameraFollow2D cam = Object.FindFirstObjectByType<CameraFollow2D>();
        if (cam != null)
        {
            Edit(cam, so =>
            {
                Prop(so, "mapMin").vector2Value = Vector2.zero;
                Prop(so, "mapMax").vector2Value = new Vector2(data.Width, data.Height);
                if (player != null)
                    Prop(so, "target").objectReferenceValue = player.transform;
            });
            Vector3 p = cam.transform.position;
            cam.transform.position = new Vector3(data.PlayerSpawn.x, data.PlayerSpawn.y, p.z);
        }

        LevelFlowController flow = Object.FindFirstObjectByType<LevelFlowController>();
        if (flow == null)
            flow = new GameObject("LevelFlowController").AddComponent<LevelFlowController>();
        if (data.IsRandom && flow.TryGetComponent<RoomLayoutVariant>(out var variant))
            Object.DestroyImmediate(variant);
        var required = new List<ZombieWaveTrigger>();
        foreach (D.WaveDef w in data.Waves)
            required.Add(triggers[w.Id]);
        Edit(flow, so =>
        {
            Prop(so, "startDoor").objectReferenceValue = start;
            Prop(so, "exitDoor").objectReferenceValue = exit;
            Prop(so, "nextScenePath").stringValue = nextScenePath;
            Prop(so, "requireAllZombiesDefeated").boolValue = true;
            Prop(so, "disableSpawnerRespawn").boolValue = true;
            Prop(so, "stopSpawningWhenExitUnlocked").boolValue = true;
            if (data.IsRandom) Prop(so, "requireRegionalWaves").boolValue = false;
            SetArray(so, "requiredWaves", required);
        });
        foreach (SafeDoor door in new[] { start, exit })
            Edit(door, so => Prop(so, "flowController").objectReferenceValue = flow);
    }
}
#endif
