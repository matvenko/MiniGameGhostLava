using UnityEngine;
using Sample;

public class LavaHazard : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ghost")) return;

        var ghost = other.GetComponentInParent<GhostScript>();
        if (ghost != null) ghost.FallIntoLava();
    }
}
