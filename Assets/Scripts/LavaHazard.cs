using UnityEngine;
using Sample;

public class LavaHazard : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Touch(other);
    }

    // Also on stay: a character who is untouchable is set back down on the edge
    // rather than killed, and there are ways to end up inside the lava without
    // ever crossing its edge - teleporting onto it, or a new level laying it
    // down underfoot. Without this they would keep falling.
    private void OnTriggerStay(Collider other)
    {
        Touch(other);
    }

    private static void Touch(Collider other)
    {
        if (!other.CompareTag("Ghost")) return;

        var ghost = other.GetComponentInParent<GhostScript>();
        if (ghost != null) ghost.FallIntoLava();
    }
}
