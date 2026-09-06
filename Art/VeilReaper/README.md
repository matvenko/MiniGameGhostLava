# Veil Reaper

Blender 5.2 ghost enemy for miniGame01. A dark violet, ragged hooded reaper with pale skeletal claws, a recessed screaming skull, icy eyes and swept mantle strips. Built using the project's Shroud Revenant geometry helpers, with a new mantle, hood edging, ten-bone rig, claw deformation and three animations. SpectralHunter and existing scenes are preserved.

- `VeilReaper.blend`: editable source parts in a hidden SOURCE collection, detailed skinned mesh, hidden game mesh, rig, three actions, portrait and straight-down cameras.
- `VeilReaper_EnvironmentPreview.blend`: game mesh on the exported LavaScene board; approximate Blender environment materials, not a Unity runtime capture.
- `../../Assets/Characters/VeilReaper/VeilReaper.fbx`: detailed export (actual location: project Assets/Characters/VeilReaper).
- `VeilReaper_Game.fbx` in the same Assets folder: 6,462 triangles, 10 bones, all three clips. Detailed source: 21,546 triangles.

Rest width 0.964 m, crown 1.316 m above root, lowest cloth point 0.163 m above root. Fits the existing 1 m grid and approximately 0.96 m SpectralHunter footprint. The upturned face and silver hood outline remain visible from overhead; full facial details are intended for close shots. Opaque rough materials, no transparency sorting dependency.

## Animation

| Action | Frames at 30 fps | Behavior |
|---|---|---|
| Hover_Loop | 1–61, play 1–60 | 2 s bob, sway, delayed cloth and hand motion |
| Glide_Loop | 1–41, play 1–40 | 1.33 s glide with stronger trailing cloth, no footsteps |
| Catch | 1–55 | 1.8 s anticipation, forward reach, curled claws, hold and release |

Catch contact is frame 24 (0.767 s from clip start); hold reaches frame 34, release begins at 43. These are authoring markers, not automatically wired Unity damage events. The gameplay Root remains fixed in every frame. Motion is on visual bones, with no walking or foot contact. Move the gameplay parent along the path while playing Glide_Loop.

Select the rig and choose an action in Blender's Dope Sheet > Action Editor. Muted NLA tracks preserve the three export takes; leave them muted while using the active action. The default action is Hover_Loop.

## Unity handoff

Import FBX as Generic, scale 1, Apply Root Motion off. Loop Hover_Loop and Glide_Loop; Catch is a one-shot. Assign URP/Lit materials using the imported base colours, high roughness/low smoothness, and optional restrained eye emission. Export uses -Z forward, Y up, metres. Parent the visual below the gameplay collider root. The FBX is available in Assets, but Animator transitions and catch-event integration into EnemyChaser have not been changed.

`validation.json` checks source proportions, exact clip endpoints and stationary roots. `fbx_roundtrip.json` verifies three actions, ten bones, triangle count and matching boundary poses after FBX reimport. `environment_overhead.png` is a close overhead context view; `environment_camera_11m.png` uses the scene's 11 m height and 60 degree square camera view. `game_overhead_90px.png` is rendered directly at 90 pixels.

Rebuild with Blender background Python: `finish_build.py`, then `polish.py`, then `verify_export.py`. These write only the VeilReaper output paths.
