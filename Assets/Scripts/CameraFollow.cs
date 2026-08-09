using Sample;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _ghostTransform;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, 0f);
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Map bounds (in world X/Z, set by the level generator)")]
    [SerializeField] private bool clampToMap;
    [SerializeField] private float mapMinX;
    [SerializeField] private float mapMaxX;
    [SerializeField] private float mapMinZ;
    [SerializeField] private float mapMaxZ;

    private Camera _cam;
    private bool _controlEnabled = true;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    // Lets GameOverManager take manual control of the camera transform
    // (e.g. to zoom in on death) without fighting the normal follow logic.
    public void SetControlEnabled(bool enabled)
    {
        _controlEnabled = enabled;
    }

    // Called by the level generator after building a map so the zoom and
    // scroll limits match the new size instead of staying tuned for the
    // very first hand-built level.
    public void ConfigureForMap(float minX, float maxX, float minZ, float maxZ)
    {
        mapMinX = minX;
        mapMaxX = maxX;
        mapMinZ = minZ;
        mapMaxZ = maxZ;
        clampToMap = true;

        float largerSide = Mathf.Max(maxX - minX, maxZ - minZ);
        float height = Mathf.Clamp(largerSide * 0.42f, 5f, 14f);
        offset = new Vector3(0f, height, 0f);
    }

    private void LateUpdate()
    {
        if (!_controlEnabled || _ghostTransform == null) return;

        Vector3 desiredPosition = _ghostTransform.position + offset;
        Vector3 newPos = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        if (clampToMap && _cam != null)
        {
            float halfViewZ = newPos.y * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfViewX = halfViewZ * _cam.aspect;

            newPos.x = ClampAxis(newPos.x, mapMinX, mapMaxX, halfViewX);
            newPos.z = ClampAxis(newPos.z, mapMinZ, mapMaxZ, halfViewZ);
        }

        transform.position = newPos;
    }

    // Keeps the camera's visible frustum inside [min, max] on one axis so the
    // empty world outside the level border never shows; if the view is wider
    // than the map itself it just centers instead of producing an inverted clamp.
    private static float ClampAxis(float value, float min, float max, float halfView)
    {
        float center = (min + max) * 0.5f;
        float span = (max - min) * 0.5f - halfView;
        if (span <= 0f) return center;
        return Mathf.Clamp(value, min + halfView, max - halfView);
    }
}
