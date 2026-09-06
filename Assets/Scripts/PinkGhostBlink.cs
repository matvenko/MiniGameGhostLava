using UnityEngine;

// A small additive blink on the original eye bones; gameplay animation stays in charge.
public sealed class PinkGhostBlink : MonoBehaviour
{
    public Transform leftEye;
    public Transform rightEye;
    Vector3 leftScale, rightScale;
    bool applied;
    float nextBlink, elapsed;
    void OnEnable() { nextBlink = Time.time + 3.2f; elapsed = -1f; }
    void Update() { Restore(); }
    void LateUpdate()
    {
        if (!leftEye || !rightEye) return;
        if (elapsed < 0 && Time.time >= nextBlink) elapsed = 0;
        if (elapsed < 0) return;
        elapsed += Time.deltaTime;
        if (elapsed >= .18f) { elapsed = -1; nextBlink = Time.time + Random.Range(3.1f, 5.2f); return; }
        float openness = 1f - .92f * Mathf.Sin(Mathf.PI * elapsed / .18f);
        leftScale = leftEye.localScale; rightScale = rightEye.localScale;
        leftEye.localScale = CloseVertically(leftEye, leftScale, openness);
        rightEye.localScale = CloseVertically(rightEye, rightScale, openness);
        applied = true;
    }
    Vector3 CloseVertically(Transform eye, Vector3 scale, float openness)
    {
        Vector3 up = transform.up;
        float x = Mathf.Abs(Vector3.Dot(eye.right, up));
        float y = Mathf.Abs(Vector3.Dot(eye.up, up));
        float z = Mathf.Abs(Vector3.Dot(eye.forward, up));
        if (x > y && x > z) scale.x *= openness;
        else if (y > z) scale.y *= openness;
        else scale.z *= openness;
        return scale;
    }
    void Restore()
    {
        if (!applied) return;
        if (leftEye) leftEye.localScale = leftScale;
        if (rightEye) rightEye.localScale = rightScale;
        applied = false;
    }
    void OnDisable() { Restore(); }
}
