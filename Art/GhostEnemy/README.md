# Spectral Hunter

Original ghost enemy created in Blender 5.2 for miniGame01.

- Source: `SpectralHunter.blend`; editable meshes, five-bone armature, studio and two cameras.
- Unity export: `Assets/Characters/SpectralHunter/SpectralHunter.fbx`.
- Animation: `Ghost_Hover_Loop`, 30 fps, 2 seconds. Frames 1 and 61 match; Blender plays 1–60 to avoid repeating the boundary pose.
- Stationary Root; Hover provides vertical bob and gentle rocking. Sleeves and tail move with different phases.
- 2,680 triangles, 9 meshes. Opaque materials keep the silhouette readable without transparency sorting.
- About 0.96m wide including sleeves, 1.2m ground-to-crown. Designed around the game's 1m grid, player capsule height 0.7m/radius 0.24m, and straight-down camera (scene offset 11m).
- Face tilted upward, large coral eyes, ivory/lilac silhouette and dark plum mask; swept tail indicates orientation from above.

## Unity import

Use Generic animation, enable Loop Time on the imported clip, and disable Apply Root Motion. Put the visual under the existing gameplay root so bobbing does not move its collider. Start at scale 1; recommended capsule radius 0.3m. The FBX contains only the rig and character, with no studio floor, cameras or lights. Blender forward is -Y; the export uses -Z forward / Y up axis conversion.

Assign URP/Lit materials if the project's importer does not convert FBX materials. Use the FBX base colours; enable coral emission for eyes (optional bloom). The existing gameplay scene and enemies have not been replaced.

## Verification

`validation.json` records geometry and exact pose comparison at the loop boundary. Portrait and top PNGs are Blender renders. The gameplay test uses approximate green/blue checker colours, not a screenshot of the actual game.
