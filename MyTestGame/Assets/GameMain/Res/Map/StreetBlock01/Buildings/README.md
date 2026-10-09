# Zombie street block buildings

These four transparent pixel-art sprites are imported as Unity Sprites with **PPU 64**, Point filtering, and uncompressed texture data.

- `Building_ConvenienceStore.png` — 320x256 px, 5x4 world units
- `Building_Apartment.png` — 384x256 px, 6x4 world units
- `Building_Warehouse.png` — 448x320 px, 7x5 world units
- `Building_Safehouse.png` — 256x192 px, 4x3 world units

The scene uses a `BuildingVisuals` parent with nine SpriteRenderers at sorting order 3. The old `BuildingTilemap` remains in the scene as a backup and its renderer is disabled; its tile data and the collision tilemap were not changed.
