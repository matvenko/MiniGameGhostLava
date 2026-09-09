using UnityEngine;

// Secondary movement belongs to a pivot above the rig, so animation and steering
// never fight over the same transform. No changes to the player's collision root.
public sealed class WardenMotion : MonoBehaviour
{
    [Range(0, 25)] public float forwardLean = 11;
    [Range(0, 30)] public float turnBank = 19;
    public float response = 9;
    Vector3 lastPosition;
    float lastYaw;
    bool incapacitated;
    Quaternion restRotation;

    void OnEnable()
    {
        restRotation = transform.localRotation;
        lastPosition = transform.position;
        lastYaw = transform.parent != null ? transform.parent.eulerAngles.y : transform.eulerAngles.y;
    }

    public void SetIncapacitated(bool value) => incapacitated = value;
    public void ResetPose()
    {
        incapacitated = false;
        transform.localRotation = restRotation;
        lastPosition = transform.position;
        lastYaw = transform.parent != null ? transform.parent.eulerAngles.y : transform.eulerAngles.y;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        Vector3 delta = transform.position - lastPosition;
        float yaw = transform.parent != null ? transform.parent.eulerAngles.y : transform.eulerAngles.y;
        // Teleports are relocation, not a burst of running speed.
        bool relocated = delta.sqrMagnitude > .25f;
        float speed = relocated ? 0 : new Vector2(delta.x, delta.z).magnitude / dt;
        float turn = relocated ? 0 : Mathf.DeltaAngle(lastYaw, yaw) / dt;
        Quaternion target = restRotation;
        if (!incapacitated)
            target *= Quaternion.Euler(Mathf.Clamp01(speed / 2f) * forwardLean, 0,
                -Mathf.Clamp(turn / 360f, -1, 1) * turnBank);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, 1 - Mathf.Exp(-response * dt));
        lastPosition = transform.position;
        lastYaw = yaw;
    }
}
