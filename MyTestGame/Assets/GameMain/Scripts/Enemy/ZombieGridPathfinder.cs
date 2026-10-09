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

    public bool TryFindPath(Vector2 start, Vector2 goal, List<Vector2> result)
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

        Vector2Int startCell = FindNearestWalkable(WorldToGrid(start));
        Vector2Int goalCell = FindNearestWalkable(WorldToGrid(goal));
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

        // 当前格只用于寻路，不需要让丧尸重复走回自己的脚下。
        if (result.Count > 1 && Vector2.Distance(start, result[0]) < cellSize * 0.75f)
            result.RemoveAt(0);
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
            Vector2 origin=GridToWorld(current), delta=GridToWorld(next)-origin;
            var filter=new ContactFilter2D();filter.SetLayerMask(obstacleMask);filter.useTriggers=false;
            int count=Physics2D.CircleCast(origin,agentRadius,delta.normalized,filter,edgeHits,delta.magnitude);
            bool clear=true;
            for(int h=0;h<count;h++)
            {
                var collider=edgeHits[h].collider;
                if(collider==null)continue;
                var body=collider.attachedRigidbody;
                if(body!=null && body.bodyType!=RigidbodyType2D.Static)continue;
                clear=false;break;
            }
            if(clear)connections[current.x,current.y]|=(byte)(1<< (offset+i));
        }
    }

    private bool IsBlocked(Vector2 position)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        int count = Physics2D.OverlapCircle(position, agentRadius, filter, overlapHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = overlapHits[i];
            if (hit == null || hit.isTrigger)
                continue;

            // 玩家和丧尸是动态刚体，不应把它们烘焙成静态墙。
            Rigidbody2D attachedBody = hit.attachedRigidbody;
            if (attachedBody != null && attachedBody.bodyType != RigidbodyType2D.Static)
                continue;

            return true;
        }

        return false;
    }

    private bool HasGround(Vector2 position)
    {
        return ground == null || ground.HasTile(ground.WorldToCell(position));
    }

    private Vector2Int FindNearestWalkable(Vector2Int origin)
    {
        if (blocked == null)
            return new Vector2Int(-1, -1);

        if (IsInside(origin) && !blocked[origin.x, origin.y])
            return origin;

        for (int radius = 1; radius <= 5; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                        continue;

                    Vector2Int candidate = origin + new Vector2Int(x, y);
                    if (IsInside(candidate) && !blocked[candidate.x, candidate.y])
                        return candidate;
                }
            }
        }

        return new Vector2Int(-1, -1);
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
