#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using D = ZombieLevel02LayoutData;

/// <summary>商业街模板和随机街区共用的绘图数据；一格对应一个世界单位。</summary>
internal sealed class ZombieStreetLayout
{
    public int Width, Height, Seed;
    public bool IsRandom;
    public string[] Ground, Props, AmbientSpawns;
    public D.BuildingDef[] Buildings;
    public D.SpawnDef[] Spawns;
    public D.WaveDef[] Waves;
    public D.GateDef[] Gates;
    public Vector2 StartDoor, ExitDoor, PlayerSpawn;

    public char G(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? '#' : Ground[Height - 1 - y][x];
    public char P(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? '.' : Props[Height - 1 - y][x];

    public static ZombieStreetLayout Commercial() => new ZombieStreetLayout
    {
        Width = D.Width, Height = D.Height, Ground = D.Ground, Props = D.Props,
        Buildings = D.Buildings, Spawns = D.Spawns, Waves = D.Waves, Gates = D.Gates,
        AmbientSpawns = D.AmbientSpawns, StartDoor = D.StartDoor, ExitDoor = D.ExitDoor, PlayerSpawn = D.PlayerSpawn
    };

    public static ZombieStreetLayout Random(int seed, Vector2 start, Vector2 exit, Func<D.BuildingDef, int> visualHeight)
        => new Generator(seed, start, exit, visualHeight).Generate();

    private sealed class Generator
    {
        private readonly System.Random random;
        private readonly ZombieStreetLayout data;
        private readonly char[,] ground, props;
        private readonly bool[,] road;
        private readonly Func<D.BuildingDef, int> visualHeight;
        private readonly List<D.BuildingDef> buildings = new List<D.BuildingDef>();
        private readonly List<int> horizontalStreets = new List<int>();
        private readonly List<D.SpawnDef> spawns = new List<D.SpawnDef>();
        private readonly List<D.BuildingDef> palette = new List<D.BuildingDef>();

        public Generator(int seed, Vector2 start, Vector2 exit, Func<D.BuildingDef, int> visualHeight)
        {
            if (start.x < 3f || start.y < 3f || exit.x < 3f || exit.y < 3f)
                throw new InvalidOperationException("随机街区的出入口需要位于正坐标，距地图边界至少 3 格。");
            random = new System.Random(seed);
            data = new ZombieStreetLayout
            {
                Width = Mathf.Max(D.Width, Mathf.CeilToInt(Mathf.Max(start.x, exit.x)) + 5),
                Height = Mathf.Max(D.Height, Mathf.CeilToInt(Mathf.Max(start.y, exit.y)) + 8),
                Seed = seed, IsRandom = true, StartDoor = start, ExitDoor = exit, PlayerSpawn = start,
                Gates = Array.Empty<D.GateDef>()
            };
            this.visualHeight = visualHeight;
            ground = new char[data.Width, data.Height];
            props = new char[data.Width, data.Height];
            road = new bool[data.Width, data.Height];
            var keys = new HashSet<string>();
            foreach (D.BuildingDef b in D.Buildings)
                if (b.Sprite.IndexOf("Safehouse", StringComparison.OrdinalIgnoreCase) < 0 && keys.Add(b.Sprite))
                    palette.Add(b);
        }

        public ZombieStreetLayout Generate()
        {
            for (int x = 0; x < data.Width; x++) for (int y = 0; y < data.Height; y++)
            {
                ground[x, y] = x == 0 || y == 0 || x == data.Width - 1 || y == data.Height - 1 ? '#' : 'g';
                props[x, y] = '.';
            }

            // 两条纵向主路、三条横街和一条短支路组成有环路的街区。
            int west = random.Next(18, 24), east = random.Next(40, 47);
            int lower = random.Next(8, 11), middle = random.Next(25, 28), upper = random.Next(31, 34);
            Street(west, 2, west, data.Height - 3);
            Street(east, 2, east, data.Height - 3);
            foreach (int y in new[] { lower, middle, upper })
            {
                Street(2, y, data.Width - 3, y);
                horizontalStreets.Add(y);
            }
            Street(random.Next(51, 55), middle, random.Next(51, 55), upper); // 两次抽取可形成折线支路。
            ConnectDoor(data.StartDoor, west);
            ConnectDoor(data.ExitDoor, east);

            // 路沿按道路邻接方向绘制；街区内部仍可走，建筑占地单独阻挡。
            for (int x = 1; x < data.Width - 1; x++) for (int y = 1; y < data.Height - 1; y++)
                if (!road[x, y] && (road[x - 1, y] || road[x + 1, y] || road[x, y - 1] || road[x, y + 1]))
                    ground[x, y] = 'S';

            PlaceSafehouse(D.Buildings[0], data.StartDoor, "Start_Safehouse");
            PlaceSafehouse(D.Buildings[9], data.ExitDoor, "Evac_Safehouse");
            for (int attempt = 0; attempt < 240 && buildings.Count < 22; attempt++)
            {
                D.BuildingDef prefab = palette[random.Next(palette.Count)];
                int y = horizontalStreets[random.Next(horizontalStreets.Count)] + 3 + random.Next(0, 2);
                int x = random.Next(2, data.Width - prefab.W - 2);
                TryBuilding(prefab, x, y, "StreetBuilding_" + buildings.Count);
            }
            AddParkingLots();
            AddProps();
            AddSpawns();

            data.Ground = Rows(ground);
            data.Props = Rows(props);
            data.Buildings = buildings.ToArray();
            data.Spawns = spawns.ToArray();
            data.AmbientSpawns = spawns.ConvertAll(s => s.Id).ToArray();
            var waves = new List<D.WaveDef>();
            for (int i = 0; i < 3; i++)
            {
                D.SpawnDef anchor = spawns[i * 3];
                int x = Mathf.FloorToInt(anchor.Position.x), y = Mathf.FloorToInt(anchor.Position.y);
                waves.Add(new D.WaveDef("RandomWave_" + (i + 1), x - 1, y - 1, x + 1, y + 1,
                    7 + i * 3, new[] { spawns[i * 3].Id, spawns[i * 3 + 1].Id, spawns[i * 3 + 2].Id }));
            }
            data.Waves = waves.ToArray();
            return data;
        }

        private bool Inside(int x, int y) => x > 0 && y > 0 && x < data.Width - 1 && y < data.Height - 1;

        private void Street(int x0, int y0, int x1, int y1)
        {
            if (x0 != x1 && y0 != y1)
            {
                Street(x0, y0, x0, y1);
                Street(x0, y1, x1, y1);
                return;
            }
            bool horizontal = y0 == y1;
            int from = horizontal ? Mathf.Min(x0, x1) : Mathf.Min(y0, y1);
            int to = horizontal ? Mathf.Max(x0, x1) : Mathf.Max(y0, y1);
            for (int i = from; i <= to; i++) for (int offset = -1; offset <= 1; offset++)
            {
                int x = horizontal ? i : x0 + offset, y = horizontal ? y0 + offset : i;
                if (!Inside(x, y)) continue;
                bool wasRoad = road[x, y];
                ground[x, y] = offset == 0 && i % 4 < 2 && !wasRoad ? (horizontal ? 'h' : 'v') : 'R';
                road[x, y] = true;
            }
        }

        private void ConnectDoor(Vector2 point, int mainStreetX)
        {
            int x = Mathf.FloorToInt(point.x), y = Mathf.FloorToInt(point.y);
            Street(x, y, mainStreetX, y);
        }

        private bool NearDoor(int x, int y, float distance)
        {
            var p = new Vector2(x + .5f, y + .5f);
            return (p - data.StartDoor).sqrMagnitude < distance * distance ||
                (p - data.ExitDoor).sqrMagnitude < distance * distance;
        }

        private void PlaceSafehouse(D.BuildingDef source, Vector2 door, string name)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(door.x - source.W * .5f), 2, data.Width - source.W - 2);
            int y = Mathf.FloorToInt(door.y) + 3;
            if (!TryBuilding(source, x, y, name))
            {
                // 出入口保留优先；特殊手绘出入口附近无法容纳房屋时，仍可保留独立门标记。
                for (int offset = 1; offset <= 4; offset++)
                    if (TryBuilding(source, x + offset, y, name) || TryBuilding(source, x - offset, y, name)) return;
            }
        }

        private bool TryBuilding(D.BuildingDef source, int x, int y, string name)
        {
            int height = Mathf.Max(source.H, visualHeight(source));
            for (int px = x; px < x + source.W; px++) for (int py = y; py < y + height; py++)
            {
                if (!Inside(px, py) || road[px, py] || ground[px, py] == 'S' || ground[px, py] == 'B' || NearDoor(px, py, 2.5f)) return false;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    if (Inside(px + dx, py + dy) && ground[px + dx, py + dy] == 'B') return false;
            }
            buildings.Add(new D.BuildingDef(name, source.Sprite, x, y, source.W, height));
            for (int px = x; px < x + source.W; px++)
            {
                for (int py = y; py < y + height; py++) ground[px, py] = 'B';
                if (Inside(px, y - 1) && !road[px, y - 1]) ground[px, y - 1] = 'C';
            }
            return true;
        }

        private void AddParkingLots()
        {
            for (int i = 0; i < 10; i++)
            {
                int x = random.Next(2, data.Width - 9), y = random.Next(3, data.Height - 6);
                for (int dx = 0; dx < 7; dx++) for (int dy = 0; dy < 4; dy++)
                    if (ground[x + dx, y + dy] == 'g') ground[x + dx, y + dy] = 'd';
            }
        }

        private bool PropSpace(int x, int y, int width, int height)
        {
            for (int px = x - 1; px <= x + width; px++) for (int py = y - 1; py <= y + height; py++)
                if (!Inside(px, py) || road[px, py] || ground[px, py] == 'B' || ground[px, py] == 'C' ||
                    ground[px, py] == 'S' || props[px, py] != '.' || NearDoor(px, py, 4f)) return false;
            return true;
        }

        private bool NearRoad(int x, int y, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++) for (int dy = -radius; dy <= radius; dy++)
                if (Inside(x + dx, y + dy) && road[x + dx, y + dy]) return true;
            return false;
        }

        private void AddProps()
        {
            // 大件保留离道路至少一格的空隙，碰撞足迹与视觉一起生成。
            for (int i = 0; i < 110; i++)
            {
                int x = random.Next(2, data.Width - 3), y = random.Next(2, data.Height - 3);
                if (!NearRoad(x, y, 5) || !PropSpace(x, y, 2, 2)) continue;
                if (i < 40)
                {
                    props[x, y] = 'W'; props[x + 1, y] = props[x, y + 1] = props[x + 1, y + 1] = 'w';
                    for (int dx = -1; dx <= 2; dx++) for (int dy = -1; dy <= 2; dy++)
                        if (ground[x + dx, y + dy] == 'g') ground[x + dx, y + dy] = 'd';
                }
                else props[x, y] = "TJKkO"[random.Next(5)];
            }
            // 血迹和水洼很少落在道路上，杂草集中在空地和路边，无碰撞。
            for (int x = 1; x < data.Width - 1; x++) for (int y = 1; y < data.Height - 1; y++)
            {
                if (props[x, y] != '.' || ground[x, y] == 'B' || NearDoor(x, y, 3f)) continue;
                double roll = random.NextDouble();
                if (road[x, y]) { if (roll < .008) props[x, y] = roll < .004 ? 'o' : '*'; }
                else if (ground[x, y] != 'C' && roll < (NearRoad(x, y, 3) ? .16 : .045))
                    props[x, y] = ",;^"[random.Next(3)];
            }
        }

        private void AddSpawns()
        {
            var candidates = new List<Vector2>();
            for (int x = 2; x < data.Width - 2; x++) for (int y = 2; y < data.Height - 2; y++)
                if (road[x, y] && !NearDoor(x, y, 7f)) candidates.Add(new Vector2(x + .5f, y + .5f));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Vector2 p = candidates[i]; candidates[i] = candidates[j]; candidates[j] = p;
            }
            foreach (Vector2 p in candidates)
            {
                bool spaced = true;
                foreach (D.SpawnDef s in spawns) if ((s.Position - p).sqrMagnitude < 25f) { spaced = false; break; }
                if (!spaced) continue;
                spawns.Add(new D.SpawnDef("Random_" + (spawns.Count + 1).ToString("00"), p));
                if (spawns.Count == 12) break;
            }
            if (spawns.Count < 9) throw new InvalidOperationException("随机街区没有足够的可用刷怪点。");
        }

        private string[] Rows(char[,] cells)
        {
            var result = new string[data.Height];
            for (int y = 0; y < data.Height; y++)
            {
                var row = new char[data.Width];
                for (int x = 0; x < data.Width; x++) row[x] = cells[x, y];
                result[data.Height - 1 - y] = new string(row);
            }
            return result;
        }
    }
}
#endif
