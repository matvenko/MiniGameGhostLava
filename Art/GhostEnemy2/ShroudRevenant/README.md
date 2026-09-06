# Shroud Revenant

Reference-led revision based on the user's supplied ivory, ragged, screaming ghost. Original Blender mesh geometry: rolled and fluted hollow hood, deep skull sockets, separate brow and cheek volumes, nasal opening, open jaw and irregular teeth, overlapping diagonal cloth panels, ragged sleeve hems, elevated skeletal hands with curled fingers, knuckles and tendons.

The hood and skull face 50 degrees above horizontal so the face survives the game's straight overhead view. Hand shapes are deliberately larger and more raised than realistic anatomy for the same reason. Ivory follows the newest reference; the previous dark, flat-palette concept is preserved separately.

## Files

- `ShroudRevenant.blend`: detailed source plus a hidden game mesh using the same six-bone rig. Default visible mesh: `ShroudRevenant_DetailedMesh`.
- `Assets/Characters/ShroudRevenant/ShroudRevenant.fbx`: detailed export, 19,082 triangles.
- `Assets/Characters/ShroudRevenant/ShroudRevenant_Game.fbx`: reduced gameplay export, approximately 3,700 triangles (3,723 in source; 3,713 after FBX reimport removes degenerate triangles).
- `portrait.png`, `overhead.png`: detailed Blender renders.
- `game_overhead_90px.png`: actual 90 × 90 render of the reduced mesh, not a downscaled portrait.
- `validation.json`, `fbx_roundtrip.json`: measured source and reimport checks.

Width 0.928 m, crown approximately 1.306 m above the root. Five opaque, matte, texture-free materials. Real cloth relief uses geometry. Game reduction sacrifices fine folds and bone contours; the full source preserves them for further art direction or higher-detail use.

## Animation and Unity

`Ghost_Hover_Loop`: 30 fps, 2 seconds, matching frames 1 and 61; playback frames 1–60. Visual hover, gentle roll, delayed tail, subtle head and hand motion. Root is static. Generic rig: Root, Hover, Head, Tail, Sleeve.L and Sleeve.R. Only rig and character are exported, with -Z forward / Y up, metre units and applied object transforms.

Use Generic import, Loop Time on the clip, and Apply Root Motion off. Place the mesh beneath the existing gameplay/collider root. Use URP/Lit with the imported five base colours, high roughness and no metallic response; the pale eyes can use emission. FBX preserves material colour slots but does not package an engine-specific Unity shader.

The gameplay scene has not been replaced. Existing Blender files remain separate.
