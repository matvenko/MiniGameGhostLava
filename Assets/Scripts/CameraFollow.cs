using Sample;
using UnityEngine;

// Follows the player from straight overhead - and opens every level with a
// look at the board first: the whole map held in frame at a tilted angle while
// the spawn countdown runs, then a move in to the shot the game is played at.
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

    [Header("Zoom in near walls")]
    [Tooltip("How close to the map edge, in world units (tiles), the zoom-in starts ramping in. Applies on whichever axis - X or Z - the player is nearer the edge on, so a corner engages it exactly like a straight wall does.")]
    [SerializeField] private float edgeZoomMargin = 5f;
    [Tooltip("How much camera height is shed once the player is right up against a wall, pulling the view in tighter for the precision a corner needs.")]
    [SerializeField] private float maxZoomInHeight = 2f;
    [Tooltip("Camera height never drops below this, whatever maxZoomInHeight asks for - keeps the zoom-in from ever crowding the shot.")]
    [SerializeField] private float minHeight = 2.5f;
    [Tooltip("How quickly the zoom eases toward its target. Slower than the position follow so it reads as a deliberate pull-in rather than a snap.")]
    [SerializeField] private float zoomSmoothSpeed = 3f;

    private float _zoomInAmount;

    [Header("Level intro")]
    [Tooltip("Seconds the whole board is held in frame. The spawn countdown runs across this.")]
    [SerializeField] private float overviewDuration = 3f;
    [Tooltip("Seconds the camera takes to come down from the board shot to the player.")]
    [SerializeField] private float approachDuration = 2f;
    [Tooltip("Pitch of the board shot, in degrees. The game itself is played at 90 - straight down.")]
    [SerializeField] private float overviewPitch = 55f;
    [Tooltip("Room left around the board in that shot. 1 is a tight fit against the frame.")]
    [SerializeField] private float overviewPadding = 1.06f;

    [Header("Tour close-up")]
    [Tooltip("Real seconds the camera takes to glide into a close-up and back out of it. The board is stopped while it does, so this runs on unscaled time.")]
    [SerializeField] private float focusDuration = 0.8f;

    private Camera _cam;
    private bool _controlEnabled = true;

    // A close-up the first-time tour has asked for, or the glide back out of one.
    private bool _focusing, _releasing;
    private float _focusTime;
    private Vector3 _focusFrom, _focusPosition;
    private Quaternion _focusFromRotation, _focusRotation;

    // Whatever angle the camera is authored at is the angle the game is played
    // at, and the one the intro lands on - so it is read off the scene rather
    // than written down a second time here.
    private Quaternion _playRotation;

    // Seconds into the intro, or below zero when none is running.
    private float _introTime = -1f;
    private Vector3 _introFrom;
    private Quaternion _introFromRotation;

    // The opening beat, for whoever times the rest of the level against this
    // shot: the countdown covers the part where the board is on show, and the
    // enemies are due the moment the camera settles.
    public float OverviewDuration => overviewDuration;
    public float IntroDuration => overviewDuration + approachDuration;
    public bool IsPlayingIntro => _introTime >= 0f;

    // In a close-up, or still on the way back out of one.
    public bool IsFocusing => _focusing;
    public bool FocusSettled => _focusing && !_releasing && _focusTime >= focusDuration;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _playRotation = transform.rotation;
    }

    // Lets GameOverManager take manual control of the camera transform
    // (e.g. to zoom in on death) without fighting the normal follow logic.
    // Taking control also calls off any intro in flight, so a death zoom is
    // not overwritten a frame later by a camera still flying in.
    public void SetControlEnabled(bool enabled)
    {
        _controlEnabled = enabled;
        if (!enabled)
        {
            _introTime = -1f;
            _focusing = false;
        }
    }

    // Glides in beside something on the board - tipped back to the given pitch,
    // the way the board shot is, and standing that far off it - with the subject
    // framed off to the left by the given share of half the screen, leaving the
    // right-hand side for whatever is being said about it.
    public void FocusOn(Vector3 point, float distance, float pitch, float shift)
    {
        if (_cam == null) return;
        _introTime = -1f;

        Vector3 play = _playRotation.eulerAngles;
        _focusRotation = Quaternion.Euler(pitch, play.y, play.z);
        float halfWidth = distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Max(_cam.aspect, 0.01f);
        Vector3 look = point + _focusRotation * Vector3.right * (shift * halfWidth);
        _focusPosition = look - _focusRotation * Vector3.forward * distance;

        StartGlide();
        _focusing = true;
        _releasing = false;
    }

    // Back to the shot the game is played at. Nothing to do if there is no
    // close-up to leave.
    public void ReleaseFocus()
    {
        if (!_focusing || _releasing) return;
        StartGlide();
        _releasing = true;
    }

    private void StartGlide()
    {
        _focusFrom = transform.position;
        _focusFromRotation = transform.rotation;
        _focusTime = 0f;
    }

    private void UpdateFocus()
    {
        _focusTime += Time.unscaledDeltaTime;
        float p = focusDuration > 0f ? Mathf.Clamp01(_focusTime / focusDuration) : 1f;
        float ease = p * p * (3f - 2f * p);

        Vector3 goal = _focusPosition;
        Quaternion goalRotation = _focusRotation;
        if (_releasing)
        {
            goal = ClampToMap(_ghostTransform.position + offset - Vector3.up * _zoomInAmount);
            goalRotation = _playRotation;
        }

        transform.SetPositionAndRotation(Vector3.Lerp(_focusFrom, goal, ease),
                                         Quaternion.Slerp(_focusFromRotation, goalRotation, ease));
        if (_releasing && p >= 1f)
        {
            _focusing = false;
            _releasing = false;
        }
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

    // Opens a level on the board instead of on the player. Called at the top of
    // every spawn sequence, so a retry after a death gets the same beat as a
    // fresh level does.
    public void PlayIntro()
    {
        if (_cam == null || !_controlEnabled) return;

        _focusing = false;
        _introTime = 0f;
        _introFromRotation = OverviewRotation();
        _introFrom = OverviewPosition(_introFromRotation);
        transform.SetPositionAndRotation(_introFrom, _introFromRotation);
    }

    private void LateUpdate()
    {
        if (!_controlEnabled || _ghostTransform == null) return;

        if (_focusing)
        {
            UpdateFocus();
            return;
        }

        if (_introTime >= 0f)
        {
            UpdateIntro();
            return;
        }

        Vector3 ghostPos = _ghostTransform.position;
        _zoomInAmount = Mathf.Lerp(_zoomInAmount, EdgeZoomIn(ghostPos), zoomSmoothSpeed * Time.deltaTime);

        Vector3 desiredPosition = ghostPos + offset - Vector3.up * _zoomInAmount;
        transform.position = ClampToMap(
            Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime));
    }

    // How much height the wall-approach zoom wants to shed right now: 0 well
    // clear of every edge, ramping up to maxZoomInHeight exactly at one, and
    // never past what would take the camera below minHeight. Corners take the
    // same reading as a straight wall does - the player is never closer than
    // edgeZoomMargin to *some* edge for it to matter, and pulling the camera
    // in helps regardless of which axis triggered it.
    private float EdgeZoomIn(Vector3 ghostPos)
    {
        if (!clampToMap || edgeZoomMargin <= 0f) return 0f;

        float proximity = Mathf.Max(
            EdgeProximity(ghostPos.x, mapMinX, mapMaxX),
            EdgeProximity(ghostPos.z, mapMinZ, mapMaxZ));
        float available = Mathf.Max(0f, offset.y - minHeight);
        return Mathf.SmoothStep(0f, Mathf.Min(maxZoomInHeight, available), proximity);
    }

    // 0 at edgeZoomMargin or further from either bound, 1 sitting on one.
    private float EdgeProximity(float value, float min, float max)
    {
        float distanceFromNearestEdge = Mathf.Min(value - min, max - value);
        return 1f - Mathf.Clamp01(distanceFromNearestEdge / edgeZoomMargin);
    }

    // Hold, then travel. The board is re-framed on every frame of the hold
    // rather than once at the start, so a level that finishes building a frame
    // late is still framed properly; the travel then sets off from wherever the
    // hold left the camera and eases out, arriving at the follow shot instead
    // of stopping dead in it.
    private void UpdateIntro()
    {
        _introTime += Time.deltaTime;

        if (_introTime < overviewDuration)
        {
            _introFromRotation = OverviewRotation();
            _introFrom = OverviewPosition(_introFromRotation);
            transform.SetPositionAndRotation(_introFrom, _introFromRotation);
            return;
        }

        Vector3 target = ClampToMap(_ghostTransform.position + offset);
        float progress = approachDuration > 0f ? (_introTime - overviewDuration) / approachDuration : 1f;

        if (progress >= 1f)
        {
            _introTime = -1f;
            transform.SetPositionAndRotation(target, _playRotation);
            return;
        }

        float ease = 1f - Mathf.Pow(1f - progress, 3f);
        transform.SetPositionAndRotation(Vector3.Lerp(_introFrom, target, ease),
                                         Quaternion.Slerp(_introFromRotation, _playRotation, ease));
    }

    // The playing angle tipped back to the overview pitch. Only the pitch
    // moves, so the board keeps the same way up on screen throughout.
    private Quaternion OverviewRotation()
    {
        Vector3 play = _playRotation.eulerAngles;
        return Quaternion.Euler(overviewPitch, play.y, play.z);
    }

    // Backs the camera off along its own view axis until all four corners of
    // the board sit just inside the frame. Solving that outright means
    // intersecting a tilted frustum with a rectangle; stepping by however much
    // the worst corner is out gets there in a pass or two, pulls in as readily
    // as it backs off, and keeps working whatever aspect ratio the device
    // turns out to have.
    private Vector3 OverviewPosition(Quaternion rotation)
    {
        float groundY = _ghostTransform != null ? _ghostTransform.position.y : 0f;
        var centre = new Vector3((mapMinX + mapMaxX) * 0.5f, groundY, (mapMinZ + mapMaxZ) * 0.5f);
        Vector3 forward = rotation * Vector3.forward;

        float tan = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = Mathf.Max(_cam.aspect, 0.01f);

        // A straight-down fit to start from: roughly right, and never inside
        // the board, which is what the passes below need to measure from.
        float distance = Mathf.Max((mapMaxZ - mapMinZ) * 0.5f / tan,
                                   (mapMaxX - mapMinX) * 0.5f / (tan * aspect));

        var corners = new[]
        {
            new Vector3(mapMinX, groundY, mapMinZ), new Vector3(mapMaxX, groundY, mapMinZ),
            new Vector3(mapMinX, groundY, mapMaxZ), new Vector3(mapMaxX, groundY, mapMaxZ),
        };

        Vector3 position = centre - forward * distance;
        for (int pass = 0; pass < 4; pass++)
        {
            float correction = FrameOvershoot(position, rotation, corners, tan, aspect) * overviewPadding;
            if (Mathf.Abs(correction - 1f) < 0.01f) break;

            distance *= correction;
            position = centre - forward * distance;
        }
        return position;
    }

    // How far out the worst of the given points sits, where 1 is exactly on the
    // edge of the frame - worked out from the pose rather than by moving the
    // camera there to look through it.
    private static float FrameOvershoot(Vector3 position, Quaternion rotation, Vector3[] points,
        float tan, float aspect)
    {
        Quaternion intoView = Quaternion.Inverse(rotation);
        float worst = 0f;

        foreach (var point in points)
        {
            Vector3 local = intoView * (point - position);
            // Behind the camera, so there is no frame to measure against yet:
            // ask for a long step back and let the next pass measure properly.
            if (local.z <= 0.01f) return 2f;

            worst = Mathf.Max(worst, Mathf.Abs(local.y) / (local.z * tan),
                                     Mathf.Abs(local.x) / (local.z * tan * aspect));
        }
        return worst;
    }

    // Keeps the camera's visible frustum inside the map bounds so the empty
    // world outside the level border never shows.
    private Vector3 ClampToMap(Vector3 position)
    {
        if (!clampToMap || _cam == null) return position;

        float halfViewZ = position.y * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfViewX = halfViewZ * _cam.aspect;

        position.x = ClampAxis(position.x, mapMinX, mapMaxX, halfViewX);
        position.z = ClampAxis(position.z, mapMinZ, mapMaxZ, halfViewZ);
        return position;
    }

    // Clamps one axis; if the view is wider than the map itself it just centers
    // instead of producing an inverted clamp.
    private static float ClampAxis(float value, float min, float max, float halfView)
    {
        float center = (min + max) * 0.5f;
        float span = (max - min) * 0.5f - halfView;
        if (span <= 0f) return center;
        return Mathf.Clamp(value, min + halfView, max - halfView);
    }
}
