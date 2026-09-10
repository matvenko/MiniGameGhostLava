# Woodland board

Implemented in `Assets/LavaScene.unity`. `overview.png` is an actual Unity camera render of the saved scene, not generated concept art. The camera is temporarily moved for the overview capture; the gameplay camera keeps its existing overhead follow behavior. Its opening overview has slightly more padding to reveal the forest.

## Art sources

- Fantasy Forest Environment Free Sample: broad canopy tree and grass tufts; one layer of the blended ground texture.
- Nature Starter Kit 2: three tree types and six bushes.
- Hand Painted Nature Kit LITE: pine, fern, plantain, dandelion, mushrooms, stumps and the second grass layer.
- Existing game materials: stone wall, water, earth banks, characters and coins.

Project-owned mesh-only prefabs and compatible foliage materials are under `Assets/Art/Forest`. The original legacy Tree components are not used by the game. Nature Starter Kit's old LUT importer was updated to use `TextureImporterCompression.Uncompressed` to resolve two compilation errors in this Unity version.

## Behavior

`ForestEnvironment` creates deterministic scenery outside the current wall footprint. `LevelManager` rebuilds it after a layout change. A ring mesh surrounds the board with no terrain covering the water hazards. High trees are concentrated behind and beside the board; the foreground contains low plants. Decorative prefabs have no colliders. Scenery is disabled for the existing cave theme, from level 6 onward.

Generated scenery is not serialized into the scene. It is reconstructed from saved prefab references on editor enable and runtime layout changes. Geometry is shared; foliage materials support instancing and prefabs have distance culling. No per-frame scene rebuild is performed.

## Review and validation

- `Tools > Forest > Install woodland environment`: reproducible scene/material setup.
- `Tools > Forest > Rebuild preview`: rebuild editor scenery from current board bounds.
- `ForestSceneReview.Capture(path)`: export a real camera overview. Capture after asset/shader compilation has settled; editor LODs are temporarily forced for a camera teleport and restored afterward.
- `ForestSceneReview.ValidatePlayMode()`: checks levels 1, 5, 6, 8 and returning to 1. Results are in `validation.txt`. It pauses simulation and restores the starting level layout without writing level progression.
- Confirmed: valid vegetation meshes/materials, no decorative colliders or scenery origins inside the walls, forest resizing and cave removal/restoration.
- Inspected both PC and mobile rendering paths. `runtime-natural-lod.png` also checks normal LOD selection during play.
- No device FPS, memory or battery benchmark was performed.

Backup before installation: `Assets/_Recovery/LavaScene_before_Forest.unity`.
