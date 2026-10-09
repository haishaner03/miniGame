# 3D pixel building pass

- All four sprites use a transparent background, hard-edged pixels, and PPU 64.
- The roof plane, front wall, side wall, eaves, windows, and ground shadow are drawn as separate pixel faces to create a three-quarter top-down volume.
- The scene swaps the nine `BuildingVisuals` sprites to these 3D variants.
- `AbandonedSedan.png` and `WreckedCar.png` use PPU 42 so the cars occupy about 1.1-1.4 world units.
- `StreetClutter` contains 67 deterministic SpriteRenderers (trees, grass, weeds, scrub, barrels, bins, rocks, trunks, and fence pieces) placed only on non-road grass cells outside the player spawn area and existing map objects.
- No collider was added to the decorative clutter. Existing collision tilemap and path remain unchanged.

- Grounding pass: removed the detached lower shadow, added a contact foundation/step strip and compact shadow, and shifted the nine scene placements down by 0.10 world units.
