# Character design constraints

The playable family is a small animal–robot hybrid: rounded body, large black digital face, simple illuminated expressions, little side limbs and a distinctive tail. The existing LanternWarden is the scale and visual-language reference.

For the fox robot, use `01-fox-robot.png` as the three-quarter design reference and `01-fox-robot-overhead-v2.png` as the current overhead proportion reference. The earlier `01-fox-robot-overhead.png` has an oversized tail and is superseded.

Gameplay constraint: the entire visible silhouette, including ears, side paws and animated tail, must fit within one board cell with a small margin in the normal idle and locomotion poses. Keep the segmented orange-and-cream tail short, compact and slightly curled. Verify against the actual Unity cell and gameplay camera before locking model scale; the concept image's grid is illustrative and does not establish world-unit measurements. Preserve the large face display's readability from above.

## Overhead silhouette review for new concepts

For every new character proposal, include an overhead silhouette check before preparing the final reconstruction views. The tail must project visibly behind or beside the body in the board plane; do not stand it vertically against the back, where it is foreshortened or occluded from the gameplay camera. Keep this visible extension short enough that the full animated silhouette can fit one cell. The face must remain directed upward and readable. New proposals may use different body shapes while retaining the floating animal–robot identity. Present alternative animal concepts for selection before producing a full multiview package for one design.

## Shop show-off animation

Every playable character needs its own short, repeatable show-off animation in the shop. Tapping the character triggers a species-specific action that expresses its personality through its body, tail or ears and digital face. It must read clearly at shop-preview size, return smoothly to idle, and allow another tap without getting stuck. Treat this as a character-design requirement alongside the model and gameplay animations, not as a shared generic emote.

Fox-robot direction: perk up both ears, give one quick compact tail wag or curl, and switch the digital eyes to a playful fox-like expression before settling back to idle. The tail must stay within the character's shop-preview framing during this motion; gameplay idle and locomotion remain constrained to one board cell.

## Blender model fidelity

The latest fox references are saved in `../FoxRobot/approved-reference.png`, `../FoxRobot/reference-front.png`, `../FoxRobot/reference-side-left.png` and `../FoxRobot/reference-side-right.png`. These are the authoritative shape references. Use Blender without needing a reminder. Match the actual volume from both side views as well as the face view: rounded thick ears with recessed teal and ivory inserts, one continuous spherical housing and cream chin, nested convex black display, integrated small paws, and a plump segmented tapered tail. Do not flatten or distort the creature to optimize only the overhead screenshot. Keep its face directed diagonally upward and solve cell fit with uniform scale and centering.

Build each character in Blender to match its approved concept sketch as closely as practical. Preserve the defining silhouette, head-to-body proportions, ear and tail shapes, face-screen size and placement, color blocking, materials and characteristic details. Do not substitute a simple recolor or a loosely related model for the approved design.

For the fox robot, use the three-quarter sketch for appearance and the revised overhead sketch for compact tail proportions. Compare Blender renders from both matching camera angles against the sketches before export. Also inspect the model at actual gameplay size in Unity; if a visual detail or proportion must change for the one-cell constraint or animation, document that deviation and keep the character's identity intact.
