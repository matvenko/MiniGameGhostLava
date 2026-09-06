# Original pink ghost restoration

PinkSpirit polish: rose body with lavender shading, soft directional highlights, restrained edge glow and two eye reflections sampled within the original eye UV island. The material is opaque with a soft gel-like highlight; it does not use actual refraction. The original texture and animation rig remain. PinkGhostBlink adds an occasional 0.18-second blink to the original eye bones and restores their animation scale each frame. PinkSpirit supports the existing _Dissolve contract and full disappearance at zero. polished.png is the Unity preview.

Removed StarRunner from the Ghost scene root and restored GhostMesh drawing. Original material, texture, animator, bones and gameplay script remain in use.

The first refinement pass applies one Blender subdivision level to the original mesh, preserving UVs and interpolated normalized bone weights. No replacement face or accessories. Source mesh is untouched in Assets/GhostCharacter_Free/Fbx/Ghost.fbx; the scene renderer uses Assets/Characters/PinkGhost/PinkGhost_Refined.asset with the original bind poses. 19,006 vertices, 36,576 triangles. source.json is the original mesh snapshot; refine.py builds PinkGhost.blend and refined.json. Coordinates deliberately stay in Unity mesh-local space for exact skeleton compatibility.

original.png and refined.png are Unity editor renders of the original and refined skinned surface in the same pose. Full animation/play-mode regression and device performance have not been tested.
