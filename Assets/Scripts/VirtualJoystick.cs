using UnityEngine;
using UnityEngine.EventSystems;

// On-screen joystick for touch/mobile. Works with mouse too (Unity's UI
// event system treats mouse drags the same way), so it can be tested in
// the Editor or the Device Simulator without a physical device.
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static VirtualJoystick Instance { get; private set; }

    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleRange = 60f;

    // -1..1 on each axis; (0,0) when not being touched
    public Vector2 InputDirection { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            localPoint = Vector2.ClampMagnitude(localPoint, handleRange);
            handle.anchoredPosition = localPoint;
            InputDirection = localPoint / handleRange;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        handle.anchoredPosition = Vector2.zero;
        InputDirection = Vector2.zero;
    }
}
