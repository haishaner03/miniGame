using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Eight-neighbour grass tile: 47 normalized corner/edge shapes.</summary>
[CreateAssetMenu(fileName = "Grass_AutoTile", menuName = "Tiles/Survivor Grass Auto Tile")]
public sealed class SurvivorGrassAutoTile : TileBase
{
    [SerializeField] private int[] masks;
    [SerializeField] private Sprite[] sprites;
    [SerializeField] private Sprite[] centerVariations;
    [SerializeField] private TileBase[] compatibleGrassTiles;

    private static readonly Vector3Int[] Neighbours =
    {
        new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(0, -1, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0),
        new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0)
    };

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        // Painting or erasing a cell can change the corners of all eight neighbours.
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                tilemap.RefreshTile(position + new Vector3Int(x, y, 0));
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        int mask = 0;
        for (int i = 0; i < Neighbours.Length; i++)
            if (IsGrass(tilemap.GetTile(position + Neighbours[i])))
                mask |= 1 << i;

        mask = NormalizeMask(mask);
        Sprite sprite = null;
        if (mask == 255 && centerVariations != null && centerVariations.Length > 0)
        {
            uint hash = unchecked((uint)((position.x * 73856093) ^ (position.y * 19349663)));
            sprite = centerVariations[hash % (uint)centerVariations.Length];
        }
        else if (masks != null && sprites != null)
        {
            for (int i = 0; i < masks.Length && i < sprites.Length; i++)
            {
                if (masks[i] != mask) continue;
                sprite = sprites[i];
                break;
            }
        }

        tileData.sprite = sprite;
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.LockColor | TileFlags.LockTransform;
        tileData.colliderType = Tile.ColliderType.None;
    }

    private bool IsGrass(TileBase tile)
    {
        if (tile is SurvivorGrassAutoTile) return true;
        if (tile == null || compatibleGrassTiles == null) return false;
        for (int i = 0; i < compatibleGrassTiles.Length; i++)
            if (tile == compatibleGrassTiles[i]) return true;
        return false;
    }

    public static int NormalizeMask(int mask)
    {
        // A diagonal only changes a corner when both adjoining cardinal cells exist.
        if ((mask & 3) != 3) mask &= ~16;
        if ((mask & 6) != 6) mask &= ~32;
        if ((mask & 12) != 12) mask &= ~64;
        if ((mask & 9) != 9) mask &= ~128;
        return mask;
    }
}
