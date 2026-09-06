# Apex Ghoul

Blender redesign of EnemyGhoul in the Spectral Hunter family. Obsidian/violet mantle, pale armored swept crown and shoulder fins, six hooked claws, four fangs, amber gaze and split trailing wisps. The upturned mask and crown preserve recognition from overhead. Source: ApexGhoul.blend; reproducible generator: build.py.

Unity assets: Assets/Characters/ApexGhoul, including FBX, five URP/Lit materials with persistent emission, Generic controller and reusable visual prefab. Installed beneath the existing EnemyGhoul in Assets/LavaScene.unity. Old Ghoul_low and Ghoul_rig are inactive. Removed the old root Animator so EnemyChaser's GetComponentInChildren finds the new visual Animator for stun/freeze. Root collider, Rigidbody, spawn reference and serialized EnemyChaser settings are preserved (Optimal, speed 3).

3,208 triangles; five bones; one-second Apex_Hunt_Loop with stationary root. Blender loop matrix error < 1e-5. Unity import and rendering verified; animation sampling moved bones 0.034m, while speed=0 produced zero displacement. Unity portraits are isolated editor previews using scene lighting, not a full gameplay run. Chase/contact and freeze-effect playthrough remain untested.

The legacy Tools/Ghost Lava/Set Up Ghoul Enemy command restores the original imported ghoul; do not run it to install this variant.
