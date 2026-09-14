# Moonmallow — friendly ghost

The body uses MiniGame/Moonmilk, a URP shader with soft directional shaping and a bright ambient floor so the friendly character stays luminous in dark areas. Facial details use URP/Lit.

Created in Blender 5.2. Editable source: `Moonmallow.blend`; rebuild using Blender background Python with `build.py`. FBX and URP materials live in `Assets/Characters/FriendlyGhost`.

Warm pearl body, blackberry eyes with modeled glints, peach cheeks, rounded hug mittens and a honey star. Face is tilted upward for the overhead game camera. 18,428 triangles, 14 mesh parts, five materials. No texture dependencies or skeletal rig; FriendlyGhostVisual animates hovering and mittens independently of the gameplay root.

Installed as Moonmallow beneath FriendlyGhost in Assets/LavaScene.unity at scale 0.86. Previous Little_ghost_ANIMATOR is inactive for reversibility. Existing rigidbody, collider, flee logic, level visibility and capture reward are retained. Moonmallow.prefab is the reusable visual only.

Validation: Blender export completed; outward normals corrected after Unity inspection; Unity script compilation and URP material assignment succeeded. unity_preview.png is an actual Unity editor render using a temporary active copy at the authored position (the original is hidden until its spawn level), not a play-mode capture. portrait.png and overhead.png are Blender renders. Full chase/capture playthrough has not been run.
