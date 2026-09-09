using UnityEngine;

// Mobile devices are left to whatever vsync interval the platform picks
// otherwise, which on some Android GPUs settles at 30 fps instead of the
// intended 60 (see the Pixel 9 Pro results in Art/MobileReview/README.md,
// where Application.targetFrameRate read -1 - never set at all). Pinning
// both here, once, before either MainMenu or LavaScene loads, is what
// actually holds the frame rate on device.
internal static class FrameRateBootstrap
{
    private const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
