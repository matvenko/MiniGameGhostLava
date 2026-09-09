# Lantern Warden — C / palette 4

Native Blender model, FBX rig and Unity player integration. Default is apricot orange with cyan eyes and inner rim. Four palettes and three eye shapes are independent assets.

## Edit the character

Open `Assets/LavaScene.unity`, select `Ghost > LanternWarden_Visual > Model`. The WardenAppearance inspector has palette and eye buttons. Edit the prefab to change the shared default; scene overrides affect that scene. Skin assets live in `Assets/Characters/LanternWarden/Skins`; eye styles in `Eyes`. Shell, shadow, face, rim emission and eye emission are independently editable. Changes use property blocks and preserve ongoing teleport/death visibility.

Future inventory code can call `appearance.SetSkin(skin)` and `appearance.SetEyes(style)`. Stable skin IDs are `warden.ivory`, `warden.ice`, `warden.midnight`, `warden.apricot`. Purchasing, ownership checks and persistence are not implemented yet.

Eyes are separate MeshRenderers under animated Eye.L/R bones. Meshes are stored in socket-local space; symmetric diamond eyes share one mesh. Their bones provide blinking and expressions without replacing the body. Do not replace GhostScript's renderer references with only SkinnedMeshRenderers: its ten renderers include both eye MeshRenderers.

## Motion

Eight bones; 14,684 triangles with default oval eyes; four distinct clips: idle (3 s), move (1 s), caught (0.5 s), death (1.2 s). Root motion is disabled. WardenMotion supplies speed-dependent forward lean and turn banking on a separate presentation pivot. CharacterController remains on the existing Ghost root. Catch gets at least 0.35 s before death dissolution; the existing game-over camera sequence may extend that hold. Death collapses the rig and fades all surfaces and the light. Respawn restores the presentation pivot and visibility.

The original visuals remain disabled for recovery. The pre-install scene copy is `Assets/_Recovery/LavaScene_before_Warden_install.unity`. The main menu uses the same prefab with owned LDR portrait materials.

## Source and verification

`LanternWarden.blend` is the editable source. `build.py` regenerates it and the FBX. `ImportUnity.cs.txt` configures Unity clips/materials/cosmetics and regenerates the prefab; running it resets palette tuning to its authored defaults. `InstallPlayer.cs.txt` connects the prefab to the current LavaScene and menu. Run these C# commands through the Unity editor command integration, in that order. No studio cameras or lights are exported into the game model.

`integration_validation.json` records 34 passed play-mode checks in an isolated scene: binding, palette and eye replacement, blink, movement, facing, banking, countdown, teleport, invincibility, shield, catch, death and respawn. The probe is editor-only and is not attached to the shipping player. No target-device performance test has been run.

`unity_palettes.png` shows native Unity portraits and cropped overhead board views; `unity_actions_eyes.png` shows sampled poses and eye shapes. `motion_preview.gif` is a 15 fps native Unity animation review with illustrative turn banking and dissolve timing, not a gameplay recording. `unity_menu.png` is a live menu portrait capture. The still pose sheet intentionally keeps the death mesh opaque so its deformation can be inspected.

Critical visual review: the silhouette and eyes read much better than the previous white blob, but the body is smoother and more helmet-like than the painted concept. The small smile is visible in the menu and largely disappears at board scale. Dark blue was lifted for readability. Do not treat this as a pixel-identical reproduction or as mobile performance approval; evaluate the rig at actual game size before final art lock. Ten separate renderers favor replaceability in this prototype; body mesh consolidation is a possible later optimization.
