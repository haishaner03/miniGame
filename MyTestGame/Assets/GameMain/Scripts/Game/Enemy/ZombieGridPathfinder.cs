using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 适用于当前俯视 Tilemap 街区的轻量网格寻路器。
/// 地图是静态碰撞体，因此启动时缓存可走格，丧尸只需周期性查询路径。
/// </summary>
[DisallowMultipleComponent]
public sealed class ZombieGridPathfinder : MonoBehaviour
{
    [Header("当前地图范围")]
    [SerializeField] private Vector2 gridMin = new Vector2(-16f, -4f);
    [SerializeField] private Vector2 gridMax = new Vector2(42f, 50f);
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private float agentRadius = 0.22f;
    [SerializeField] private LayerMask obstacleMask = Physics2D.DefaultRaycastLayers;
    [SerializeField] private bool allowDiagonal = true;

    private bool[,] blocked;
    private byte[,] connections;
    private int width;
    private int height;
    private bool ready;
    private int[] routeParent;
    private bool[] visited;
    private readonly Queue<int> routeQueue = new Queue<int>();
    private readonly List<Vector2> walkableCells = new List<Vector2>();
    private readonly List<Vector2> spawnPath = new List<Vector2>();
    private Tilemap ground;
    private readonly Collider2D[] overlapHits = new Collider2D[64];
    private readonly RaycastHit2D[] edgeHits = new RaycastHit2D[32];
    private Vector2Int cachedGoal = new Vector2Int(-1, -1);

    private static readonly Vector2Int[] FourDirections =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] DiagonalDirections =
    {
        new Vector2Int(1, 1), new Vector2Int(-1, 1),
        new Vector2Int(1, -1), new Vector2Int(-1, -1)
    };

    private void Awake()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        if (ground == null)
            foreach (Tilemap map in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                if (map.name == "GroundTilemap") { ground = map; break; }
        cellSize = Mathf.Max(0.25f, cellSize);
        width = Mathf.Max(1, Mathf.CeilToInt((gridMax.x - gridMin.x) / cellSize));
        height = Mathf.Max(1, Mathf.CeilToInt((gridMax.y - gridMin.y) / cellSize));
        blocked = new bool[width, height];
        connections = new byte[width, height];
        routeParent = new int[width * height];
        visited = new bool[width * height];
        cachedGoal = new Vector2Int(-1, -1);
        walkableCells.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 center = GridToWorld(new Vector2Int(x, y));
                blocked[x, y] = !HasGround(center) || IsBlocked(center);
                if (!blocked[x, y]) walkableCells.Add(center);
            }
        }

        // Sampling only cell centers can miss a thin fence or car edge between
        // two walkable cells. Cache swept-circle edges once per map rebuild.
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
        {
            if (blocked[x,y]) continue;
            var current = new Vector2Int(x,y);
            CacheConnections(current, FourDirections, 0);
            if (allowDiagonal) CacheConnections(current, DiagonalDirections, 4);
        }

        ready = true;
    }

    public bool TryFindPath(Vector2 start, Vector2 goal, List<Vector2> result,
        float stoppingDistance = -1f, float clearanceRadius = -1f)
    {
        if (result == null)
            return false;

        result.Clear();
        // 进入/退出 Play Mode、脚本热重载或场景重新加载时，缓存可能短暂被清空。
        // 不仅检查 ready，还检查数组本身，避免丧尸在这一帧访问空缓存。
        if (!ready || blocked == null || connections == null || blocked.GetLength(0) != width || blocked.GetLength(1) != height)
            Rebuild();

        if (blocked == null)
            return false;

        clearanceRadius = clearanceRadius < 0f ? agentRadius : Mathf.Max(0.01f, clearanceRadius);
        stoppingDistance = stoppingDistance < 0f ? agentRadius : Mathf.Max(0f, stoppingDistance);
        // A nearby cell can be on the opposite side of a car. Both endpoints
        // must connect to the actual world positions, not merely be unblocked.
        Vector2Int startCell = FindConnectedCell(start, clearanceRadius, 0f, false);
        Vector2Int goalCell = FindConnectedCell(goal, clearanceRadius, stoppingDistance, true);
        if (!IsInside(startCell) || !IsInside(goalCell))
            return false;

        int startIndex = ToIndex(startCell);
        int goalIndex = ToIndex(goalCell);
        // All pursuing zombies share a reverse route field to the player's current cell.
        // Recompute only when the goal cell changes instead of allocating a BFS per zombie.
        if (cachedGoal != goalCell)
        {
            System.Array.Clear(visited, 0, visited.Length);
            for (int i = 0; i < routeParent.Length; i++) routeParent[i] = -1;
            routeQueue.Clear();
            routeQueue.Enqueue(goalIndex);
            visited[goalIndex] = true;
            while (routeQueue.Count > 0)
            {
                int currentIndex = routeQueue.Dequeue();
                Vector2Int current = FromIndex(currentIndex);
                VisitNeighbors(current, FourDirections, routeQueue, visited, routeParent, currentIndex, goalCell);
                if (allowDiagonal) VisitNeighbors(current, DiagonalDirections, routeQueue, visited, routeParent, currentIndex, goalCell);
            }
            cachedGoal = goalCell;
        }
        if (!visited[startIndex])
            return false;
        int trace = startIndex;
        while (trace >= 0)
        {
            result.Add(GridToWorld(FromIndex(trace)));
            if (trace == goalIndex)
                break;
            trace = routeParent[trace];
        }

        Vector2 approach;
        if (!TryGetApproachPoint(result[result.Count - 1], goal, stoppingDistance, clearanceRadius, out approach))
        {
            result.Clear();
            return false;
        }
        if ((approach - result[result.Count - 1]).sqrMagnitude > 0.0001f)
            result.Add(approach);

        // Preserve forward progress on refresh, but never shortcut through a
        // vehicle corner from the agent's off-center position.
        int skip = 0;
        while (skip + 1 < result.Count && CanTraverseSegment(start, result[skip + 1], clearanceRadius))
            skip++;
        if (skip > 0) result.RemoveRange(0, skip);
        return result.Count > 0;
    }

    public bool IsWalkablePosition(Vector2 position)
    {
        if (!ready || blocked == null) Rebuild();
        Vector2Int cell = WorldToGrid(position);
        return IsInside(cell) && !blocked[cell.x, cell.y] && HasGround(position) && !IsBlocked(position);
    }

    public bool TryRandomWalkable(Vector2 player, float minimumDistance, out Vector3 position)
    {
        if (!ready || blocked == null) Rebuild();
        for (int i = 0; i < 128 && walkableCells.Count > 0; i++)
        {
            Vector2 candidate = walkableCells[Random.Range(0, walkableCells.Count)];
            if ((candidate - player).sqrMagnitude < minimumDistance * minimumDistance) continue;
            if (IsBlocked(candidate)) continue;
            // Exclude enclosed or unreachable cells from the ground spawn population.
            if (!TryFindPath(candidate, player, spawnPath)) continue;
            position = candidate;
            return true;
        }
        position = Vector3.zero;
        return false;
    }

    /// <summary>Copy the current reachable component for one-time room assembly.</summary>
    public void CopyReachableCells(Vector2 origin, List<Vector2> result)
    {
        if (result == null) return;
        result.Clear();
        if (!IsWalkablePosition(origin) || !TryFindPath(origin, origin, spawnPath)) return;
        for (int i = 0; i < visited.Length; i++)
            if (visited[i]) result.Add(GridToWorld(FromIndex(i)));
    }

    private void VisitNeighbors(
        Vector2Int current,
        Vector2Int[] directions,
        Queue<int> queue,
        bool[] visited,
        int[] parent,
        int currentIndex,
        Vector2Int goalCell)
    {
        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int next = current + directions[i];
            if (!IsInside(next) || blocked[next.x, next.y])
                continue;
            int edge = i + (directions == DiagonalDirections ? 4 : 0);
            if ((connections[current.x,current.y] & (1 << edge)) == 0) continue;

            // 防止斜向穿过两个墙角之间的缝隙。
            if (directions[i].x != 0 && directions[i].y != 0 &&
                (blocked[current.x + directions[i].x, current.y] ||
                 blocked[current.x, current.y + directions[i].y]))
                continue;

            int nextIndex = ToIndex(next);
            if (visited[nextIndex])
                continue;

            visited[nextIndex] = true;
            parent[nextIndex] = currentIndex;
            queue.Enqueue(nextIndex);
        }
    }

    private void CacheConnections(Vector2Int current, Vector2Int[] directions, int offset)
    {
        for (int i=0;i<directions.Length;i++)
        {
            var next=current+directions[i];
            if (!IsInside(next) || blocked[next.x,next.y]) continue;
            if (directions[i].x != 0 && directions[i].y != 0 &&
                (blocked[current.x+directions[i].x,current.y] || blocked[current.x,current.y+directions[i].y])) continue;
            if(CanTraverseSegment(GridToWorld(current),GridToWorld(next),agentRadius))
                connections[current.x,current.y]|=(byte)(1<< (offset+i));
        }
    }

    /// <summary>Checks static geometry without treating other enemies as walls.</summary>
    public bool CanTraverseSegment(Vector2 origin, Vector2 destination, float radius = -1f)
    {
        radius = radius < 0f ? agentRadius : Mathf.Max(0.01f, radius);
        Vector2 delta = destination - origin;
        float distance = delta.magnitude;
        if (distance <= 0.0001f) return HasGround(origin) && !IsBlocked(origin, radius);

        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.125f, cellSize * 0.5f)));
        for (int i = 0; i <= samples; i++)
            if (!HasGround(Vector2.Lerp(origin, destination, (float)i / samples))) return false;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        int count = Physics2D.CircleCast(origin, radius, delta / distance, filter, edgeHits, distance);
        if (count >= edgeHits.Length) return false;
        float escapeDistance = 0f;
        for (int i = 0; i < count; i++)
        {
            Collider2D obstacle = edgeHits[i].collider;
            if (!IsStaticObstacle(obstacle)) continue;
            // Physics contacts and navigation's safety margin can overlap by a
            // few pixels after recoil. Permit only an outward escape, then cast
            // the remaining segment again so this never permits wall crossing.
            Vector2 outward = origin - obstacle.ClosestPoint(origin);
            float separation = outward.magnitude;
            if (edgeHits[i].distance > 0.001f || separation < Mathf.Max(0.001f, radius - 0.02f)) return false;
            float outwardSpeed = Vector2.Dot(delta / distance, outward / separation);
            if (outwardSpeed <= 0.001f) return false;
            escapeDistance = Mathf.Max(escapeDistance, (radius + 0.002f - separation) / outwardSpeed);
        }
        if (escapeDistance > 0f)
        {
            if (escapeDistance >= distance) return false;
            Vector2 escaped = origin + delta / distance * escapeDistance;
            count = Physics2D.CircleCast(escaped, radius, delta / distance, filter, edgeHits, distance - escapeDistance);
            if (count >= edgeHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (IsStaticObstacle(edgeHits[i].collider)) return false;
        }
        return true;
    }

    private bool TryGetApproachPoint(Vector2 origin, Vector2 goal, float stoppingDistance,
        float radius, out Vector2 approach)
    {
        Vector2 delta = goal - origin;
        float distance = delta.magnitude;
        approach = distance <= stoppingDistance || distance <= 0.0001f
            ? origin : goal - delta / distance * stoppingDistance;
        if (!HasGround(goal)) return false;

        // Allow a player to stand closer to a wall than a zombie's radius, but
        // do not select an attack position behind that wall.
        var filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        int count = Physics2D.Linecast(origin, goal, filter, edgeHits);
        if (count >= edgeHits.Length) return false;
        for (int i = 0; i < count; i++)
            if (IsStaticObstacle(edgeHits[i].collider)) return false;
        return CanTraverseSegment(origin, approach, radius);
    }

    private static bool IsStaticObstacle(Collider2D hit)
    {
        if (hit == null || hit.isTrigger) return false;
        Rigidbody2D attachedBody = hit.attachedRigidbody;
        return attachedBody == null || attachedBody.bodyType == RigidbodyType2D.Static;
    }

    private bool IsBlocked(Vector2 position)
    {
        return IsBlocked(position, agentRadius);
    }

    private bool IsBlocked(Vector2 position, float radius)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        int count = Physics2D.OverlapCircle(position, radius, filter, overlapHits);
        if (count >= overlapHits.Length) return true;
        for (int i = 0; i < count; i++)
        {
            if (IsStaticObstacle(overlapHits[i])) return true;
        }

        return false;
    }

    private bool HasGround(Vector2 position)
    {
        return ground == null || ground.HasTile(ground.WorldToCell(position));
    }

    private Vector2Int FindConnectedCell(Vector2 position, float radius, float stoppingDistance, bool isGoal)
    {
        if (blocked == null)
            return new Vector2Int(-1, -1);

        Vector2Int origin = WorldToGrid(position);
        Vector2Int best = new Vector2Int(-1, -1);
        float bestDistance = float.PositiveInfinity;
        for (int ring = 0; ring <= 5; ring++)
        {
            for (int x = -ring; x <= ring; x++)
            {
                for (int y = -ring; y <= ring; y++)
                {
                    if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
                        continue;

                    Vector2Int candidate = origin + new Vector2Int(x, y);
                    if (!IsInside(candidate) || blocked[candidate.x, candidate.y]) continue;
                    Vector2 center = GridToWorld(candidate);
                    float distance = (center - position).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    Vector2 approach;
                    bool connected = isGoal
                        ? TryGetApproachPoint(center, position, stoppingDistance, radius, out approach)
                        : CanTraverseSegment(position, center, radius);
                    if (!connected) continue;
                    best = candidate;
                    bestDistance = distance;
                }
            }
            // No cell in the next ring can be closer than this lower bound.
            float nextRingDistance = (ring + 0.5f) * cellSize;
            if (bestDistance <= nextRingDistance * nextRingDistance) break;
        }

        return best;
    }

    private Vector2Int WorldToGrid(Vector2 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt((position.x - gridMin.x) / cellSize),
            Mathf.FloorToInt((position.y - gridMin.y) / cellSize));
    }

    private Vector2 GridToWorld(Vector2Int cell)
    {
        return gridMin + new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
    }

    private bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    private int ToIndex(Vector2Int cell)
    {
        return cell.y * width + cell.x;
    }

    private Vector2Int FromIndex(int index)
    {
        return new Vector2Int(index % width, index / width);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Vector3 center = (gridMin + gridMax) * 0.5f;
        Vector3 size = new Vector3(gridMax.x - gridMin.x, gridMax.y - gridMin.y, 0.05f);
        Gizmos.DrawWireCube(center, size);
    }
}
