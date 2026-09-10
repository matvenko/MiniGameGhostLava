using System;
using System.Collections;
using UnityEngine;

// The one door a rewarded video goes through. Two places ask for one - the game
// over screen, to carry straight on, and the main menu, to come back to a run
// that has no lives left - and both come through here, so the store SDK is
// dropped in once rather than twice.
//
// What is here now is a stand-in: it waits a moment and then pays out, which is
// enough to play the game against. Replacing it means replacing Show, and
// nothing that calls it has to change: load the ad, show it, call onRewarded
// when the SDK says the video was actually watched to the end, and onUnavailable
// when there is no ad to show or the player closed it early. The reward must
// never be handed out on the second path - that is the whole point of the door.
public static class RewardedAds
{
    private const float StandInLength = 2f;

    public static void Show(MonoBehaviour host, Action onRewarded, Action onUnavailable = null)
    {
        if (host == null || onRewarded == null)
        {
            if (onUnavailable != null) onUnavailable();
            return;
        }
        host.StartCoroutine(StandInPlay(onRewarded));
    }

    // Unscaled, because both screens that show an ad may have stopped the clock
    // the game plays on.
    private static IEnumerator StandInPlay(Action onRewarded)
    {
        yield return new WaitForSecondsRealtime(StandInLength);
        onRewarded();
    }
}
