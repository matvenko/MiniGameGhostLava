using UnityEngine;
using UnityEngine.EventSystems;

// On-screen joystick for touch/mobile. Works with mouse too (Unity's UI
// event system treats mouse drags the same way), so it can be tested in
// the Editor or the Device Simulator without a physical device.
//
// The stick floats. It rests on the right of the screen, where it is visible
// enough to say "this is the control", but a press anywhere picks it up and
// puts it under the finger. A thumb never lands twice in the same spot on a
// phone, and a stick nailed to one corner makes the player look down mid-run to
// find it; letting it come to the finger means they never have to.
//
// This component lives on the full-screen press area, not on the stick itself -
// it has to hear about presses that land nowhere near the stick's current
// resting place. The area is the Canvas's first child, so it sits under every
// button and panel in the raycast order and never steals their taps.
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static VirtualJoystick Instance { get; private set; }

    [Tooltip("The visible stick: ring and handle together. This is what moves to the finger.")]
    [SerializeField] private RectTransform stick;
    [SerializeField] private RectTransform handle;
    [Tooltip("Fades the stick between its resting and held states.")]
    [SerializeField] private CanvasGroup stickGroup;

    [Tooltip("How far from the stick's centre the handle can travel, in canvas units.")]
    [SerializeField] private float handleRange = 80f;
    [Tooltip("Where the stick waits: this far in from the right edge, halfway up.")]
    [SerializeField] private float restMargin = 230f;
    [Tooltip("How visible the stick is while nobody is holding it.")]
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.5f;
    [Tooltip("How quickly the stick drifts back to its resting place after release.")]
    [SerializeField] private float returnSpeed = 14f;

    // -1..1 on each axis; (0,0) when not being touched
    public Vector2 InputDirection { get; private set; }

    // Pointer ids are plain ints and 0 is a real touch, so "nobody is holding
    // it" needs a value no pointer can ever have.
    private const int NoPointer = int.MinValue;

    private RectTransform _area;
    private int _pointer = NoPointer;

    void Awake()
    {
        Instance = this;
        _area = (RectTransform)transform;
    }

    void OnEnable()
    {
        Release();
        if (stick != null) stick.anchoredPosition = RestPosition();
    }

    void Update()
    {
        if (stick == null || _pointer != NoPointer) return;

        // Unscaled, because the stick should still be settling back into place
        // while a level-complete or pause panel holds the game at timeScale 0.
        stick.anchoredPosition = Vector2.Lerp(stick.anchoredPosition, RestPosition(),
            Time.unscaledDeltaTime * returnSpeed);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // First finger down owns the stick. A second one landing elsewhere is
        // somebody reaching for the trap button, not a second steering hand.
        if (_pointer != NoPointer) return;

        _pointer = eventData.pointerId;
        PlaceUnder(eventData);
        if (stickGroup != null) stickGroup.alpha = 1f;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointer || stick == null) return;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(stick, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            localPoint = Vector2.ClampMagnitude(localPoint, handleRange);
            handle.anchoredPosition = localPoint;
            InputDirection = localPoint / handleRange;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointer) return;
        Release();
    }

    // Read off the press area rather than stored, so the stick still finds the
    // right edge after a rotation or a resolution change.
    private Vector2 RestPosition() =>
        new Vector2(_area.rect.width * 0.5f - restMargin, 0f);

    // Drops the stick on the press point, kept far enough inside the screen that
    // the whole ring stays visible - a stick half off the edge is one the player
    // cannot judge the centre of.
    private void PlaceUnder(PointerEventData eventData)
    {
        if (stick == null) return;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position, eventData.pressEventCamera, out localPoint))
            return;

        Vector2 half = stick.rect.size * 0.5f;
        Rect area = _area.rect;
        stick.anchoredPosition = new Vector2(
            Mathf.Clamp(localPoint.x, area.xMin + half.x, area.xMax - half.x),
            Mathf.Clamp(localPoint.y, area.yMin + half.y, area.yMax - half.y));
    }

    private void Release()
    {
        _pointer = NoPointer;
        InputDirection = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        if (stickGroup != null) stickGroup.alpha = idleAlpha;
    }
}
