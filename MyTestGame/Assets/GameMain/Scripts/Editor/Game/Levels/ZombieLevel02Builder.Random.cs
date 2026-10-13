#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static partial class ZombieLevel02Builder
{
    private static ZombieStreetLayout CreateRandomLayout(int seed)
    {
        LevelFlowController flow = Object.FindFirstObjectByType<LevelFlowController>();
        Vector2 start = flow != null && flow.StartDoor != null ? (Vector2)flow.StartDoor.transform.position : ZombieLevel02LayoutData.StartDoor;
        Vector2 exit = flow != null && flow.ExitDoor != null ? (Vector2)flow.ExitDoor.transform.position : ZombieLevel02LayoutData.ExitDoor;
        return ZombieStreetLayout.Random(seed, start, exit, definition =>
        {
            Sprite sprite = Load<Sprite>(BuildingSpritePath(definition.Sprite));
            return Mathf.CeilToInt(definition.W * sprite.bounds.size.y / sprite.bounds.size.x);
        });
    }

    private static void ValidateRandomNavigation(ZombieStreetLayout data)
    {
        foreach (TilemapCollider2D tileCollider in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None))
            tileCollider.ProcessTilemapChanges();
        foreach (CompositeCollider2D composite in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
            composite.GenerateGeometry();
        Physics2D.SyncTransforms();
        ZombieGridPathfinder pathfinder = Object.FindFirstObjectByType<ZombieGridPathfinder>();
        pathfinder.Rebuild();
        var route = new List<Vector2>();
        if (!pathfinder.IsWalkablePosition(data.StartDoor) || !pathfinder.IsWalkablePosition(data.ExitDoor) ||
            !pathfinder.TryFindPath(data.StartDoor, data.ExitDoor, route))
            throw new System.InvalidOperationException("生成地图的出入口未连通，已中止保存。请更换随机种子。");
        foreach (ZombieLevel02LayoutData.SpawnDef spawn in data.Spawns)
            if (!pathfinder.IsWalkablePosition(spawn.Position) || !pathfinder.TryFindPath(spawn.Position, data.StartDoor, route))
                throw new System.InvalidOperationException("刷怪点不可达：" + spawn.Id + "。已中止保存，请更换随机种子。");
    }
}
#endif
