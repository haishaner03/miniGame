using System;
using System.Collections.Generic;

/// <summary>Seeded shuffle bags: visit every template before drawing it again.</summary>
public static class RunRoomPlanner
{
    public static void BuildRoute(IReadOnlyList<string> templates, int count, string firstRoom,
        int seed, List<string> result)
    {
        if (templates == null || templates.Count < 2 || count < 2)
            throw new ArgumentException("A run needs at least two room templates and two rooms.");
        if (result == null) throw new ArgumentNullException(nameof(result));
        var pool = new List<string>();
        foreach (string path in templates)
            if (!string.IsNullOrEmpty(path) && !pool.Contains(path)) pool.Add(path);
        if (pool.Count < 2 || (firstRoom != null && !pool.Contains(firstRoom)))
            throw new ArgumentException("Invalid room template pool or starting room.");

        result.Clear();
        var random = new Random(seed);
        result.Add(firstRoom ?? pool[random.Next(pool.Count)]);
        var bag = new List<string>(pool);
        bag.Remove(result[0]);
        Shuffle(bag, random);
        while (result.Count < count)
        {
            if (bag.Count == 0)
            {
                bag.AddRange(pool);
                Shuffle(bag, random);
                if (bag[0] == result[result.Count - 1])
                {
                    int other = random.Next(1, bag.Count);
                    string swap = bag[0]; bag[0] = bag[other]; bag[other] = swap;
                }
            }
            result.Add(bag[0]);
            bag.RemoveAt(0);
        }
    }

    // String.GetHashCode can change between processes; use an explicit stable hash.
    public static int LayoutSeed(int runSeed, string scenePath, int roomIndex)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in scenePath) hash = (hash ^ c) * 16777619;
            return (int)(hash ^ (uint)runSeed ^ ((uint)roomIndex * 7919));
        }
    }

    public static int VariantIndex(int runSeed, IReadOnlyList<string> route, int roomIndex, int count)
    {
        if (count <= 0) return -1;
        string path = route[roomIndex];
        int visits = 0;
        for (int i = 0; i < roomIndex; i++) if (route[i] == path) visits++;
        int first = new Random(LayoutSeed(runSeed, path, 0)).Next(count);
        return (first + visits) % count;
    }

    private static void Shuffle(List<string> items, Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            string swap = items[i]; items[i] = items[j]; items[j] = swap;
        }
    }
}
