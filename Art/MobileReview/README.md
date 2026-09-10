# Warden Android device review

Target connected for this review: Pixel 9 Pro, Android 17, reported display 960 × 2142. Build entry scenes: MainMenu, LavaScene. APK output: Builds/Android/WardenReview.apk (development build).

## Test sequence

1. Launch from the menu; verify orange/cyan default, both eyes, no missing or pink materials.
2. Enter gameplay and wait for the camera approach. Confirm player/enemy recognition at the normal gameplay zoom.
3. Hold the joystick, reverse direction and make several tight turns. Check motion, input response and collider behavior.
4. Allow one enemy catch: recoil should precede collapse/dissolve, and the face must disappear with the body.
5. Respawn: body, eyes, glow and movement must return together.
6. Test teleport and shield, then inspect a larger map at the farthest gameplay zoom.
7. Profile sustained gameplay on the device, including crowded scenes. Record frame-time distribution and stalls; menu FPS and editor timing are not gameplay performance evidence.

## Acceptance targets for review

- Player is immediately distinguishable from hunters; two eyes and movement direction remain readable.
- No visual fragments remain after death or teleport, and no missing eyes after respawn.
- No crashes, unhandled exceptions or missing shaders during the sequence.
- Performance target: stable 60 fps (16.7 ms budget); record actual device results before deciding whether geometry, draw calls or effects need optimization.

## Device results — 2026-09-10

Built and installed via adb update (`install -r`), preserving app data. Final instrumented build completed with zero errors and 18 warnings. The first full build reported 999 warnings, largely repeated compiler/shader diagnostics; the smaller incremental warning count does not mean they were fixed. APK is about 72.5 MB; larger BuildReport byte totals include auxiliary outputs.

The orange/cyan character and both eyes render in the live menu and board on Vulkan / Mali-G715. Movement input was injected through Android touch events. Death dissolution and subsequent restored character were captured; isolated catch/teleport/shield acceptance and large-map coverage are not fully established by this short device review.

Two complete active-gameplay windows yielded 30.010 and 30.011 FPS, p95 frame time 33.42 and 33.47 ms, worst frame 34.12 and 34.26 ms. Each window contained 301 frames over about 10.03 seconds. `Application.targetFrameRate` reported -1. These are short development-build observations, not a 60 FPS pass or sustained thermal benchmark. Set an explicit mobile frame-rate target and measure again before attributing the 30 FPS result to model complexity.

`MobileFrameProbe` is compiled only into development device builds. It excludes other scenes, paused popups, the initial eight seconds after scene load and two seconds after focus resumes. It measures Unity update intervals rather than GPU execution time. Raw structured observations are in `device_results.json`; the initial device recording is `device_run.mp4`.

Confirmed UI defect: the shop card clips vertically at 2142 × 960; its fixed 700 × 920 canvas-unit design is not fitted to the device's available height. The bottom debug level bar also overlaps the shop. Evidence is `device_respawn.png` (captured while the shop was open). This was diagnosed, not fixed during the device review.

Diagnostics to investigate: Android build warns about Active Input Handling = Both; startup logs report a missing `com.google.android.play.core.assetpacks.AssetPackManager` class, but the app proceeds into gameplay. A separate Little_Ghost collider also exceeds the convex-hull polygon limit. No claim of an error-free runtime is made.

Next priorities: fit the shop to the safe screen area and prevent the debug bar from covering popups; configure the desired mobile frame rate and run a longer moving/crowded-scene profile; then repeat catch, respawn, teleport and shield on device. Final performance acceptance also needs a non-development build.
