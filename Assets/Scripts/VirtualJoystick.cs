using UnityEngine;
using UnityEngine.EventSystems;

// On-screen joystick for touch/mobile. Works with mouse too (Unity's UI
// event system treats mouse drags the same way), so it can be tested in
// the Editor or the Device Simulator without a physical device.
//
// The stick floats. It rests along the bottom of the screen, on the side the
// ability bar is not, where it is visible enough to say "this is the control"
// and clear of the buttons the other thumb wants; but a press anywhere picks it
// up and puts it under the finger. A thumb never lands twice in the same spot on
// a phone, and a stick nailed to one corner makes the player look down mid-run to
// find it; letting it come to the finger means they never have to.
//
// Steering is measured from where the finger came down, not from the picture.
// The picture is pulled in from the screen edge so the whole ring stays in view,
// and measured from there a thumb pressed low on the screen started the
// character off towards the edge before it had moved at all. And once the thumb
// goes past the rim the stick is towed along behind it: a thumb that had
// wandered two rings out had to come all the way back to turn round, and in the
// phone recordings that made a tenth-of-a-second reversal take nearly half a
// second.
//
// The player can also turn the picture of it off in the settings. Steering does
// not go with it - this component is the full-screen press area, not the drawing.
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

    [Tooltip("How far from the stick's centre the handle can travel, in canvas units. A thumb that goes further pulls the stick along behind it.")]
    [SerializeField] private float handleRange = 80f;
    [Tooltip("Where the stick waits: this far up from the bottom edge.")]
    [SerializeField] private float restMargin = 230f;
    [Tooltip("And this far in from whichever side edge the ability bar is not on.")]
    [SerializeField] private float restSideMargin = 260f;
    [Tooltip("How visible the stick is while nobody is holding it.")]
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.28f;
    [Tooltip("How visible the stick is while a finger is on it.")]
    [SerializeField, Range(0f, 1f)] private float activeAlpha = 0.8f;
    [Tooltip("How quickly the stick drifts back to its resting place after release.")]
    [SerializeField] private float returnSpeed = 14f;

    // -1..1 on each axis; (0,0) when not being touched
    public Vector2 InputDirection { get; private set; }

    // Where on the screen the steering finger is, in pixels, while one is down -
    // for the playtest trace, which wants to know what the thumb was covering.
    public bool Held => _pointer != NoPointer;
    public Vector2 PointerPosition { get; private set; }

    // Pointer ids are plain ints and 0 is a real touch, so "nobody is holding
    // it" needs a value no pointer can ever have.
    private const int NoPointer = int.MinValue;

    private RectTransform _area;
    private int _pointer = NoPointer;

    // The point steering is measured from, in the press area's space: where the
    // finger came down, and then wherever the thumb has towed it to. Kept apart
    // from the drawn stick, which may sit a little further in from the edge.
    private Vector2 _origin;

    // Which way along the bottom edge the stick rests: away from the ability bar,
    // so a left thumb on the buttons and a right thumb on the stick never meet.
    private int _restSide = -1;
    private bool _drawn = true;

    void Awake()
    {
        Instance = this;
        _area = (RectTransform)transform;
    }

    void OnEnable()
    {
        GameSettings.Changed += ApplyPreferences;
        ApplyPreferences();
        Release();
        if (stick != null) stick.anchoredPosition = RestPosition();
    }

    void OnDisable()
    {
        GameSettings.Changed -= ApplyPreferences;
    }

    // Hiding the stick takes the picture away and nothing else: the press area is
    // this object, not the stick, so steering carries on working exactly as it
    // did. The stick is still moved about underneath - it has to be somewhere
    // when the player turns it back on.
    private void ApplyPreferences()
    {
        _drawn = !GameSettings.HideJoystick && !Pad.InUse;
        _restSide = GameSettings.AbilitiesOnLeft ? 1 : -1;
        SetStickAlpha(_pointer == NoPointer ? idleAlpha : activeAlpha);
    }

    void Update()
    {
        // Picking up a controller puts the picture of the stick away the same way
        // the setting does, and a touch brings it back.
        if (_drawn != (!GameSettings.HideJoystick && !Pad.InUse)) ApplyPreferences();

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
        if (!AreaPoint(eventData, out Vector2 point)) return;

        _pointer = eventData.pointerId;
        _origin = point;
        SetStickAlpha(activeAlpha);
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointer) return;
        PointerPosition = eventData.position;
        if (!AreaPoint(eventData, out Vector2 point)) return;

        // Past the rim, the origin is dragged along so the thumb stays on it.
        // Which way the character goes is still exactly which way the thumb is
        // from the origin; what changes is that turning round never takes more
        // than a ring's width of travel, however far the thumb has wandered.
        Vector2 offset = point - _origin;
        if (offset.sqrMagnitude > handleRange * handleRange)
        {
            offset = offset.normalized * handleRange;
            _origin = point - offset;
        }

        InputDirection = offset / handleRange;
        Draw(offset);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointer) return;
        Release();
    }

    // Read off the press area rather than stored, so the stick still finds the
    // corner of the screen after a rotation or a resolution change.
    private Vector2 RestPosition() =>
        new Vector2(_restSide * (_area.rect.width * 0.5f - restSideMargin),
                    -(_area.rect.height * 0.5f - restMargin));

    private void SetStickAlpha(float alpha)
    {
        if (stickGroup != null) stickGroup.alpha = _drawn ? alpha : 0f;
    }

    private bool AreaPoint(PointerEventData eventData, out Vector2 point) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position,
            eventData.pressEventCamera, out point);

    // The ring goes on the origin, kept far enough inside the screen that the
    // whole of it stays visible - a stick half off the edge is one the player
    // cannot judge the centre of. The handle shows the push itself rather than
    // sitting under the finger, so near an edge, where the ring has been pulled
    // in, it still says truthfully which way and how hard the stick is pushed.
    private void Draw(Vector2 offset)
    {
        if (stick == null) return;

        Vector2 half = stick.rect.size * 0.5f;
        Rect area = _area.rect;
        stick.anchoredPosition = new Vector2(
            Mathf.Clamp(_origin.x, area.xMin + half.x, area.xMax - half.x),
            Mathf.Clamp(_origin.y, area.yMin + half.y, area.yMax - half.y));
        if (handle != null) handle.anchoredPosition = offset;
    }

    private void Release()
    {
        _pointer = NoPointer;
        InputDirection = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        SetStickAlpha(idleAlpha);
    }
}
