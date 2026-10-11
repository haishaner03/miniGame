using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Assemble authored street modules once, before enemies spawn.</summary>
[DisallowMultipleComponent]
public sealed class RoomLayoutVariant : MonoBehaviour
{
    [Serializable]
    public sealed class Layout
    {
        public string name;
        [Tooltip("整组开关视觉与碰撞，不单独隐藏建筑图片。")]
        public GameObject obstacleRoot;
        public GameObject[] hiddenProps;
        public Transform[] spawnAnchors;
    }

    [SerializeField] private string districtName;
    [SerializeField] private Layout[] layouts = Array.Empty<Layout>();
    [SerializeField, Min(2)] private int spawnPointCount = 6;
    [SerializeField, Min(0f)] private float spawnAnchorJitter = 2f;
    [SerializeField, Min(1f)] private float spawnPointSpacing = 4f;
    [SerializeField, Min(3f)] private float doorClearance = 5f;

    public int VariantCount => layouts.Length;
    public int ActiveVariantIndex { get; private set; } = -1;
    public string VariantName => ActiveVariantIndex >= 0 ? layouts[ActiveVariantIndex].name : "畅通街区";
    public string DisplayName => districtName + " · " + VariantName;
    public int AppliedSeed { get; private set; }
    public bool UsedFallback { get; private set; }

    private readonly Dictionary<GameObject, bool> originalActive = new Dictionary<GameObject, bool>();
    private readonly List<Vector2> route = new List<Vector2>();
    private readonly List<Vector2> reachableCells = new List<Vector2>();
    private readonly List<Vector2> chosenPoints = new List<Vector2>();
    private Transform[] originalSpawnPoints;
    private Transform spawnRoot;
    private bool captured;

    public bool Apply(int seed, int preferredIndex, LevelFlowController flow,
        ZombieSpawner spawner, ZombieGridPathfinder navigation)
    {
        if (flow == null || flow.StartDoor == null || flow.ExitDoor == null || navigation == null)
        {
            Debug.LogError("Room layout needs start/exit doors and a pathfinder.", this);
            return false;
        }
        CaptureOriginal(spawner);
        AppliedSeed = seed;
        UsedFallback = false;
        ActiveVariantIndex = -1;
        int first = layouts.Length > 0 ? Mathf.Clamp(preferredIndex, 0, layouts.Length - 1) : 0;
        for (int attempt = 0; attempt < layouts.Length; attempt++)
        {
            RestoreOriginal();
            int index = (first + attempt) % layouts.Length;
            Layout layout = layouts[index];
            if (layout.hiddenProps != null)
                foreach (GameObject prop in layout.hiddenProps) if (prop != null) prop.SetActive(false);
            if (layout.obstacleRoot != null) layout.obstacleRoot.SetActive(true);
            Rebuild(navigation);
            if (!HasExitRoute(flow, navigation)) continue;
            ActiveVariantIndex = index;
            UsedFallback = attempt > 0;
            break;
        }
        if (ActiveVariantIndex < 0)
        {
            RestoreOriginal();
            Rebuild(navigation);
            UsedFallback = true;
            if (!HasExitRoute(flow, navigation))
            {
                Debug.LogError("Room base layout has no reachable exit: " + gameObject.scene.path, this);
                return false;
            }
            Debug.LogWarning("Room modules blocked the route; using the base layout.", this);
        }
        if (spawner != null) ConfigureSpawnPoints(seed, flow, spawner, navigation);
        return true;
    }

    private void CaptureOriginal(ZombieSpawner spawner)
    {
        if (captured) return;
        foreach (Layout layout in layouts)
            if (layout.hiddenProps != null)
                foreach (GameObject prop in layout.hiddenProps)
                    if (prop != null && !originalActive.ContainsKey(prop)) originalActive.Add(prop, prop.activeSelf);
        originalSpawnPoints = spawner != null ? spawner.spawnPoints : null;
        captured = true;
    }

    private void RestoreOriginal()
    {
        foreach (Layout layout in layouts) if (layout.obstacleRoot != null) layout.obstacleRoot.SetActive(false);
        foreach (var pair in originalActive) if (pair.Key != null) pair.Key.SetActive(pair.Value);
    }

    private static void Rebuild(ZombieGridPathfinder navigation)
    {
        Physics2D.SyncTransforms();
        navigation.Rebuild();
    }

    private bool HasExitRoute(LevelFlowController flow, ZombieGridPathfinder navigation)
    {
        Vector2 start = flow.StartDoor.transform.position, exit = flow.ExitDoor.transform.position;
        return navigation.IsWalkablePosition(start) && navigation.IsWalkablePosition(exit) &&
            navigation.TryFindPath(start, exit, route);
    }

    private void ConfigureSpawnPoints(int seed, LevelFlowController flow, ZombieSpawner spawner,
        ZombieGridPathfinder navigation)
    {
        var random = new System.Random(unchecked(seed ^ 0x2A71C953));
        Transform[] anchors = ActiveVariantIndex >= 0 ? layouts[ActiveVariantIndex].spawnAnchors : originalSpawnPoints;
        chosenPoints.Clear();
        if (anchors != null)
        {
            var order = new List<Transform>(anchors);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Transform swap = order[i]; order[i] = order[j]; order[j] = swap;
            }
            foreach (Transform anchor in order)
            {
                if (anchor == null || chosenPoints.Count >= spawnPointCount) continue;
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    Vector2 jitter = new Vector2((float)random.NextDouble() * 2f - 1f,
                        (float)random.NextDouble() * 2f - 1f) * spawnAnchorJitter;
                    Vector2 candidate = (Vector2)anchor.position + jitter;
                    if (!TryAddSpawn(candidate, flow, navigation)) continue;
                    break;
                }
            }
        }
        // Authored anchors can become occupied after later map edits. Fill gaps
        // from the spawn's reachable component, using a separate deterministic RNG.
        if (chosenPoints.Count < spawnPointCount)
        {
            navigation.CopyReachableCells(flow.StartDoor.transform.position, reachableCells);
            for (int i = reachableCells.Count - 1; i >= 0 && chosenPoints.Count < spawnPointCount; i--)
            {
                int j = random.Next(i + 1);
                Vector2 candidate = reachableCells[j];
                reachableCells[j] = reachableCells[i];
                TryAddSpawn(candidate, flow, navigation);
            }
        }
        if (spawnRoot == null)
        {
            spawnRoot = new GameObject("VariantSpawnPoints").transform;
            spawnRoot.SetParent(transform, false);
        }
        var points = new Transform[chosenPoints.Count];
        for (int i = 0; i < points.Length; i++)
        {
            Transform point;
            if (i < spawnRoot.childCount) point = spawnRoot.GetChild(i);
            else
            {
                point = new GameObject("Reinforcement_" + (i + 1)).transform;
                point.SetParent(spawnRoot, false);
            }
            point.gameObject.SetActive(true);
            point.position = chosenPoints[i];
            points[i] = point;
        }
        for (int i = points.Length; i < spawnRoot.childCount; i++) spawnRoot.GetChild(i).gameObject.SetActive(false);
        spawner.spawnPoints = points;
    }

    private bool TryAddSpawn(Vector2 candidate, LevelFlowController flow, ZombieGridPathfinder navigation)
    {
        float clearance = Mathf.Max(doorClearance, 7f);
        if ((candidate - (Vector2)flow.StartDoor.transform.position).sqrMagnitude < clearance * clearance ||
            (candidate - (Vector2)flow.ExitDoor.transform.position).sqrMagnitude < doorClearance * doorClearance)
            return false;
        foreach (Vector2 point in chosenPoints)
            if ((candidate - point).sqrMagnitude < spawnPointSpacing * spawnPointSpacing) return false;
        if (!navigation.IsWalkablePosition(candidate) ||
            !navigation.TryFindPath(candidate, flow.StartDoor.transform.position, route)) return false;
        chosenPoints.Add(candidate);
        return true;
    }
}
