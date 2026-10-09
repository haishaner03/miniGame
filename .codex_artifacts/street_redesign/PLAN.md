# ZombieLevel01 Map Optimization

1. Back up the current live scene, including unsaved edits. Completed.
2. Keep the 40 x 48 ground and eight-neighbour grass autotiles. Replace the road maze with a four-cell main street, three-cell local streets, two intersections, a service alley and parking courts.
3. Reuse the generated artwork. Make contiguous full-cell pavement and directional curb tiles. Split wide facades into two-unit modules and combine them with a shallow roof from the existing convenience-store image.
4. Organize buildings along street frontages, with the start safehouse in the south and exit safehouse in the northeast. Use modest car sizes, planting strips, abandoned parking and localized debris.
5. Give buildings, wrecks and fences real colliders. Two cordons force a western service-alley detour and an eastern residential detour. Maintain at least a two-cell passage at both detours.
6. Verify the actual scene collider geometry, street connectivity, spawn and exit access, camera follow reference, clean overview and player-scale views. Save the scene after validation.

## Locations

- Spawn: (7.5, 5.0), in front of the south safehouse.
- Exit approach: (30.0, 43.4), in front of the northeast safehouse.
- Main street: x = 18..21, y = 3..42.
- South cross street: y = 11..13.
- Market cross street: y = 30..32.
- Western service alley: x = 7..9, y = 11..32.
- Eastern residential street: x = 30..32, y = 11..42.
- Upper residential connector: y = 40..42, x = 18..32.
- South cordon: y = 20, western alley remains open.
- North cordon: y = 36, eastern street remains open.

## Scope

No new image-generation request is needed for this pass. Derived assets live in a new StreetRedesign folder; existing source images and tile assets are retained. Movement and camera smoothing code are retained. Zombies, buffs and chapter progression are outside this map-optimization pass.

## Completed Validation

- All six steps completed; live scene saved and left in Edit Mode.
- 19 buildings, 11 cars, 123 planted/debris objects, 76 nonempty solid colliders.
- All 544 asphalt cells belong to one connected street network.
- Actual collider sampling at 0.25-unit spacing with a 0.42 x 0.26 clearance box: start and exit clear; shortest traversable route 78 units.
- Route crosses the south cordon through the western alley and the north cordon through the eastern street.
- Play Mode physics probe stopped the player at the road wreck and allowed passage through the western alley. Camera followed both probes.
- Added ground-height sprite sorting. Camera target bound; size 3 and smoothing 0.16 retained.
- Fixed an existing Unity 6 GF debugger exception by constructing its clipboard TextEditor only when COPY is clicked in OnGUI.
- Final console check: no errors or warnings.
- Clean overview: Street_Overview.png. Annotated verified route: Street_Overview_Route.png. Green line exists only in the preview, not in the scene.
