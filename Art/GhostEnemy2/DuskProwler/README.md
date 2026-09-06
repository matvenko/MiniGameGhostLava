# Dusk Prowler

Overhead-first revision for a bright cartoon maze. Facing is toward the orange eyes; the broad leading hood and two short swept sleeves narrow into a long rear taper. Exactly five opaque, texture-free colours: deep indigo, charcoal violet, near-black, ivory and ember orange. The thin ivory crown crescent is confined to the leading hood. No transparency, metallic or specular response.

## Deliverables

- `DuskProwler.blend`: editable character, Generic armature and overhead preview camera.
- `Assets/Characters/DuskProwler/DuskProwler.fbx`: one skinned mesh and rig only.
- `overhead_90px.png`: actual 90 × 90 overhead render, not a resized large render.
- `overhead_90px_grass.png` and `overhead_90px_water.png`: same camera and scale against saturated board colours.
- `validation.json` and `fbx_roundtrip.json`: measured checks from Blender and reimported FBX.

## Specifications

2,700 triangles; 5 materials; 5 bones. Width 0.90 m, longitudinal extent 1.11 m, crown 1.23 m, lowest rest vertex 0.20 m. The mask faces 50 degrees above horizontal. Orange occupies approximately 1.04% of mesh surface area. Object transforms applied; Blender units are metres. FBX axis settings: -Z forward, Y up.

`Ghost_Hover_Loop` spans frames 1–61 at 30 fps (2 seconds). Playback uses 1–60 to avoid a duplicate boundary sample. Total vertical bob is 8 cm peak-to-peak; roll is ±3 degrees; the tail's local motion lags by 90 degrees. Root remains fixed. Boundary matrices match exactly, including after FBX roundtrip. Blender prefixes the reimported action with the rig name (`DuskProwler|Ghost_Hover_Loop`); the FBX take is `Ghost_Hover_Loop`.

## Unity

Use Generic rig and Loop Time; disable Apply Root Motion. Parent this visual beneath the gameplay/collider root at scale 1. For the exact flat palette use URP/Unlit with the five imported base colours. The Blender preview uses constant colour emission with a camera-only world, and the exported material colours survive roundtrip; FBX cannot carry a complete engine-specific toon shader. Do not add bloom that overwhelms the narrow eye shapes.

The existing scene/enemies are not replaced. The previous Spectral Hunter source is preserved separately.
