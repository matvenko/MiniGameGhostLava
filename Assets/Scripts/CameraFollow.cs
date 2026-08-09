using Sample;   
using UnityEngine;


public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _ghostTransform;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, 0f);
    [SerializeField] private float smoothSpeed = 5f;

    private void LateUpdate()
    {
        if (_ghostTransform == null) return;
        Vector3 desiredPosition = _ghostTransform.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}
