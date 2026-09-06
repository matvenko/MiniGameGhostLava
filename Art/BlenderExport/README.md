# LavaScene environment for Blender

Open `LavaScene_Environment.blend` in Blender. Textures are packed into the file.

Includes the environment from the open Unity LavaScene, generated ground and liquid meshes, coins, hierarchy, source camera and lights. An extra overview camera is selected for the preview. Unity Y-up coordinates are converted to Blender Z-up, with mesh winding and transforms preserved.

Excluded character roots: Ghost, Enemies, FriendlyGhost. No character rigs or meshes are included. Gameplay, UI, audio, and Unity post-processing are not executable in Blender. Custom ground/wall/water shaders use approximate Blender materials; water animation and screen-space effects are not transferred.

Objects whose renderers were disabled in Unity are retained, hidden, in the `Hidden source tiles (Unity render disabled)` collection. This preserves source geometry without overlapping the generated visible surfaces.

`verification.json` records object counts, bounds, and texture checks. `preview.png` is the rendered overview. `scene.json` is the extracted source snapshot. `ExportScene.cs.txt` is the read-only Unity extraction command; `import_scene.py` rebuilds the blend file from that snapshot using Blender's background Python mode.
