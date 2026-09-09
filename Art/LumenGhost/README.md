# Lumen Ghost

Editable Blender model based on the user's selected white Halloween ghost and approved luminous concept. Pearl white continuous body, curved tail, fused rounded hands with fingers, expressive eyes, eyebrows and a smile. The upper body bends upward 40 degrees so the face reads from the game's vertical camera.

## Deliverables

- `LumenGhost.blend`: native model, materials, six-bone rig, three-second hover animation, portrait and overhead cameras, studio lighting and subtle bloom compositor.
- `build.py`: reproducible Blender 5.2 generator. Run from the project root with Blender `--background --python Art/LumenGhost/build.py`.
- `approved_concept.png`: the approved AI concept reference, not an implemented-game screenshot.
- `portrait.png`, `overhead.png`: actual Blender renders.
- `unity_portrait.png`, `unity_board.png`: actual Unity Editor renders of the FBX and Unity materials. The board capture uses a temporary copy on the current meadow at the player's position; its camera is cropped to a 7.4-unit square and uses a temporary Bloom volume.
- `Assets/Characters/LumenGhost/LumenGhost.fbx`: skinned export, Unity Y-up, forward +Z.
- `Assets/Characters/LumenGhost/LumenGhost_Visual.prefab`: reusable visual with Generic Animator, loop controller, pearl/cyan shader materials and a small shadowless cyan point light. Prefab scale is 0.5; approximately 0.95 units across the hands.
- `ImportUnity.cs.txt`, `CaptureUnity.cs.txt`: reproducible Unity MCP commands.

## Validation

28,348 triangles, 13 mesh objects, five materials, six bones. Source loop pose error is below 1e-5. Unity shader compilation passed. Imported three-second animation produced 0.0222m world-space bone motion at prefab scale, with zero loop-end position error. An isolated visibility render had 2,646 visible pixels at `_Dissolve=1` and zero at `_Dissolve=0` (128px validation camera).

The Unity shader gives the body a light floor and HDR cyan edges independently of scene lighting. A soft halo needs HDR and Bloom enabled on the gameplay camera; the preview uses Bloom intensity 0.45 and threshold 1. The prefab's point light only affects ground materials that accept additional lights.

Installed under `Ghost/LumenGhost_Visual` in LavaScene. The original visual and root Animator are disabled; CharacterController and gameplay references remain on the original Ghost root. GhostScript uses the child Animator, all 13 skinned renderers and the glow light. Idle/move states use different hover speeds; attack/surprised states return to idle, and dissolve is held while the shader fades. Renderer property blocks synchronize visibility across every material without creating runtime material instances. The light fades and blinks with the body. The main-menu portrait also uses Lumen with dedicated LDR material copies and an elevated camera.

`integration_validation.json` records 24 passing checks in an isolated Play Mode scene: joystick movement/facing, animation transitions, countdown freeze/resume, teleport destination and synchronized fade, grace blinking, respawn during blinking, shield protection/expiry, lava death, complete dissolve, respawn and enemy catch. The editor-only probe is `Assets/Tests/LumenPlayerIntegrationProbe.cs`; its test scene is `Assets/_Recovery/LumenIntegrationTest.unity` and is excluded from build settings. Device performance and physical gamepad input have not been tested. The root remains stationary during the hover animation.

`InstallPlayer.cs.txt` reproduces scene wiring. The pre-install scene, including then-unsaved editor state, is preserved at `Assets/_Recovery/LavaScene_before_Lumen_install.unity`. If rebuilding/importing the FBX, rerun the installer after the importer to retain the gameplay controller setup.

The open Blender session was not overwritten. Open `LumenGhost.blend` to edit the model; the studio collection is excluded from the FBX export.
