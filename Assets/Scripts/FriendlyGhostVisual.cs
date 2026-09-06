using UnityEngine;

// Animate only the Blender visual; the flee/capture root owns gameplay motion.
public sealed class FriendlyGhostVisual : MonoBehaviour
{
    private Vector3 rest;
    private Transform left, right;
    private Quaternion leftRest, rightRest;
    private void Awake()
    {
        rest = transform.localPosition;
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (child.name == "Hug mitten L") left = child;
            if (child.name == "Hug mitten R") right = child;
        }
        if (left) leftRest = left.localRotation;
        if (right) rightRest = right.localRotation;
    }
    private void OnEnable() { transform.localPosition = rest; }
    private void LateUpdate()
    {
        float phase = Time.time * 2.6f;
        transform.localPosition = rest + Vector3.up * (0.045f * Mathf.Sin(phase));
        transform.localRotation = Quaternion.Euler(0, 0, 3f * Mathf.Sin(phase * .7f));
        if (left) left.localRotation = leftRest * Quaternion.Euler(0, 0, 7 * Mathf.Sin(phase));
        if (right) right.localRotation = rightRest * Quaternion.Euler(0, 0, -7 * Mathf.Sin(phase));
    }
}
