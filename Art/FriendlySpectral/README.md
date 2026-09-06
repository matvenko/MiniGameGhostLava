# Friendly Spectral

Unity palette brightened to pearl/mint. All five materials explicitly retain the _EMISSION keyword and BakedEmissive flag (clearing EmissiveIsBlack), so saved materials provide a light floor in shadow. Body emission is 0.65, rim 0.7, jade tail 0.6; the dark teal mask preserves face contrast. unity_bright.png verifies the materials on an isolated Unity copy with the scene's lighting.

Direct Blender derivative of Art/GhostEnemy2/SpectralHunter.blend. Retains the original mantle, recessed face, ivory rim, sleeves, trailing wisp, five-bone rig and seamless two-second hover. Replaces hostile wedge eyes and howl with mint crescent eyes and a gentle luminous smile. Ivory/jade shell and deep teal face distinguish the friendly character.

Source: FriendlySpectral.blend. Rebuild: Blender --background --python Art/FriendlySpectral/build.py. Unity assets: Assets/Characters/FriendlySpectral (FBX, URP/Lit materials, looping Animator controller, visual prefab).

Installed below FriendlyGhost in Assets/LavaScene.unity, replacing the rejected Moonmallow visual. Original gameplay root, collider, flee behavior, reward and level spawning are retained. No extra procedural bob script: the imported rig supplies animation.

Validation: 3,024 triangles, five bones, loop boundary matrix error below 1e-5. FBX imported as Generic; two-second clip loops with root motion disabled. Blender portrait/overhead renders and Unity editor preview inspected. Unity preview uses a temporary active visual copy, since the gameplay root is inactive before its spawn level. Full capture playthrough not performed.
