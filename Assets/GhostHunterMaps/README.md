# Ghost Hunter Maps

A standalone map editor for the board: floor, water, the border wall, worn
paths and scattered decoration, previewed live and pushed into the game scene
with one button.

Open it with **Window → Ghost Hunter Maps** (⌘⇧M).

## The rule this was built under

**No file in `Assets/Scripts` is modified.** Everything here is additive. The
game keeps generating its own board and drawing its own surfaces; this tool
moves the tiles those surfaces read, writes their settings through
`SerializedObject`, and adds the two things the game has no concept of - paths
and decor - as its own objects.

That is also why `GhmSurfaceMeshes` is a copy of the mesh code in
`GroundSurface` / `WallSurface` / `LiquidSurface` rather than a call into them:
those components resolve their input through `GameObject.Find` in the active
scene, so they cannot run inside the editor's preview scene. The maths is
copied verbatim - same hash, same noise, same vertex colour layout - because
the preview is only honest if it produces the triangles the game will.

## The window

| Panel | What it holds |
| --- | --- |
| Top left | Camera compression, board size, generation, **level bands**, import/export, lighting, publish target |
| Bottom left | Texture catalogue: import, slice a painted sheet, export an atlas |
| Centre | The map. **Scene** renders it for real; **Plan** draws it flat and fast |
| Top right | The layer stack |
| Bottom right | The selected layer's settings |

### Compression

One slider, and the thing it means is visible: **compression is the factor the
board is foreshortened by**. 1.0 is straight down (what the game ships with).
0.5 squashes the board to half height along Z, which is a 60° tilt off
vertical. The camera pitch, the follow offset, the plan view's squash and the
angle decor sprites stand up at are all derived from it, so they can never
disagree.

### Level bands

"Levels 1-5 use this ground, 6-10 use that one." A band owns a level range,
the materials worn in it, the generation algorithm and the water density.
Empty material slots fall through to the layer's own. Gaps between bands are
flagged, because a level that falls through to the last band is a silent bug.

### Layers

Five kinds: Ground, Water, Wall, Path, Decor. Within a kind the **first visible
layer wins** - the rest are alternatives to switch between - and the list marks
which one is live. Every layer has a level range, so a layer can appear only
from level 8 onwards.

The surface layers expose the same knobs the game's components have, under the
same names, because publishing writes them straight onto those components.

### Decor

A Decor layer holds rules. A rule is one thing to scatter: a texture (batched
into a single mesh with every other instance of that rule) or a prefab. It
carries frequency per 100 walkable cells, minimum spacing, cluster size, where
it is allowed to land (`Inland`, `Shore`, `OnPath`, `OffPath`, `PathEdge`,
`Corner`, combined freely), size and tint variation, and its own level range.

`Stance: FollowCamera` stands the sprite up by exactly the rig's tilt, so
painted art keeps its shape as the compression changes.

### Paths

A path layer routes between spread-out anchors with a weighted Dijkstra:
routes prefer to merge into paths already laid down (junctions, not parallel
stripes), avoid the shoreline, and wander by a noise term instead of running
down a perfect staircase. The result is painted as a translucent skin on the
ground's own vertex lattice, so it can never float or z-fight.

## Generation

Five algorithms; all of them are repaired by the same pass afterwards, so any
of them is safe to ship:

| Algorithm | Shape |
| --- | --- |
| `ShuffleConnected` | What the game already does. Flip random cells to water, keep only flips that leave the floor fully connected. |
| `Caves` | Cellular automata. Rounded lakes, soft coastlines. |
| `Rivers` | Winding channels rim to rim plus lakes. Reads most like a labyrinth. |
| `Rooms` | Rectangular water blocks on a lattice, leaving a corridor grid. |
| `Archipelago` | Noise threshold: broad water with islands in it. |

The repair pass carves a causeway to every island worth keeping, floods the
rest, tops the floor back up if the carve went too far, and fills puddles too
small to read as water. A board is never published split.

Everything is a pure function of `(profile, level)`, so previewing level 7 and
building level 7 in the game give the same map without shipping the layout.

## Publish

`Publish to game` writes, in one undo step:

- the tiles, re-typed between `Blocks` and `Lava` - adding or removing them and
  rebuilding the border wall if the board was resized
- material assets for any path or decor that was still using an on-the-fly
  material, so a build can find the shaders
- the ground, water and wall settings and materials onto the scene's surface
  components
- the camera angle, offset and clamp bounds
- a `GhostHunterMaps` object carrying `GhmRuntimeBinder`

The scene is marked dirty but not saved. The profile asset is saved.

### At runtime

`GhmRuntimeBinder` polls `LevelManager.CurrentLevel` in `LateUpdate` - after
the level manager has finished its own reshuffle - and re-lays the layout,
swaps the band's materials, and rebuilds paths and decor. Anything left
standing in water (coins, the player, enemies) is moved to the nearest floor
cell.

Turn off **Override in game** to leave the game's own layout alone and publish
only the look.

Outside play mode the binder maintains only its own paths and decor, which are
flagged `DontSave`. Tiles, materials and the camera are scene state that
publishing owns - otherwise just having the component in the scene would move
tiles on every domain reload. Its inspector has an **Apply to scene** button
for doing that by hand.

## Checks

`Window → Ghost Hunter Maps Self-test`, or headless:

```
Unity -batchmode -nographics -projectPath . -logFile out.log \
  -executeMethod GhostHunterMaps.EditorTools.GhmSelfTest.RunAll
```

It checks the camera maths against its own definition, the grid against the
coordinates of the scene's existing tiles, every algorithm at three sizes and
four levels for connectivity and puddles, determinism, resizing, that every
mesh builds, that the path skin hugs the ground, and that decor honours its
placement masks.
