# Board surface polish

The saved `Assets/LavaScene.unity` now uses project-owned v2 meadow surface materials. Surrounding forest art and lighting are retained.

- Grass: new hand-painted clover/turf texture, rotated detail blending, restrained broad variation and lower contrast. A single non-colliding mesh of short grass blades softens grass/water boundaries.
- Water: turquoise palette, reduced shadow influence, two moving caustic layers, wave-normal highlights, shallow shoreline color and broken edge foam. The land mask is regenerated from actual cells at each level layout. Meadow shoreline effects are explicitly opted into by the meadow material; the cave's shared water shader does not enable them.
- Walls: one mesh of staggered stones with rounded bevels, varied lengths, textured rock grain and localized moss. Original wall colliders are retained. The cave uses its original wall geometry/material.
- Generated liquid/wall meshes and shoreline textures are released during rebuilds and teardown.

`Tools > Forest > Polish board surfaces` reapplies the setup. `ForestEnvironmentBuilder` also reapplies the surface polish after rebuilding the environment when the texture exists.

## Validation

`BoardSurfaceValidation.Run()` was run in Play Mode for levels 1, 2, 5, 6, 8 and returning to 1. It checks every land/water mask cell against the level, grass-bank presence/removal, wall geometry selection, finite vertices, triangle indices and absence of new liquid colliders. See `board-validation.txt`.

PC and mobile forward rendering were visually inspected. No device FPS or memory benchmark was performed. Camera snapshots under this folder are real Unity renders, without the screen-space HUD. `board-polished-close.png` shows gameplay-scale surface detail; `board-polished-overview.png` shows the whole board.

Pre-polish scene backup: `Assets/_Recovery/LavaScene_before_BoardPolish.unity`.

## Generated texture provenance

File: `Assets/Art/Forest/Textures/BoardGrass_v2.png`.
Tool: built-in image_gen, not the fallback CLI. Stone grain reuses the project's existing `mossy_rock_diff_2k.png`; masonry and grass-bank geometry are generated in Unity.

Final grass prompt:

> Use case: stylized-concept. Asset type: seamless tileable game albedo texture, ONE square image of short soft lush grass viewed perfectly straight from above. Fill entire canvas edge to edge with uniformly distributed fine hand-painted forest meadow turf, medium spring green mossy cushion base, small elegant directional grass blade strokes and tiny clover leaves occurring in subtle patches, clean premium stylized adventure mobile game texture. Texture should read as a soft calm grassy floor at distance, with delicate brushwork when close. Restrained fresh sage, fern and meadow green palette, mildly warm highlights, not neon, not yellow straw. Fine-scale details, no big bunches, no circular swirls, no strong repeating motifs, no large dark patches. Seamlessly tileable opposite edges, uniform diffuse lighting, no baked directional shadows, no ambient occlusion, no specular highlights. Orthographic texture swatch only, NOT a scene, no perspective, no geometry, no text, no grid, no stones, no dirt, no flowers, no border, no raised tall tufts. Professional high resolution game texture, all green ground, opaque image.
