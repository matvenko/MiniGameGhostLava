using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One of the guide book's 3D windows: a character - or a coin, a portal - on a
// turntable of its own far off the board, filmed by a camera of its own into a
// texture that this RawImage shows.
//
// Only what is on screen is filmed. A window scrolled out of the viewport hands
// its texture back to Unity's pool and switches its slot off - camera, lights
// and model - so the book costs two or three small renders a frame however many
// pages it has, and nothing at all for the pages nobody is looking at.
//
// The book is read with the game paused, Time.timeScale at 0, so everything
// here runs on unscaled time, and the builder set the models' Animators to
// unscaled time as well so they keep hovering while they are read about.
//
// A sideways drag turns the model and lets it go with a little spin; a vertical
// one is handed on to the scroll view, so a thumb that lands on a picture still
// scrolls the page. Left alone it sways gently either side of facing you, and a
// model that was spun round finds its way back to the front.
[RequireComponent(typeof(RawImage))]
public class GuideBookShowcase : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("The model's slot on the stage: its turntable, its camera and its lights. Off whenever the window is.")]
    [SerializeField] private GameObject slot;
    [SerializeField] private Camera stageCamera;
    [SerializeField] private Transform turntable;
    [Tooltip("The copy of the model, under the turntable. Moved sideways on first show so it turns about its own middle.")]
    [SerializeField] private Transform subject;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private ScrollRect scroll;

    [Tooltip("Degrees the camera looks down at the model. The characters' faces are tilted up for the overhead game camera, so they read best from a little above.")]
    [SerializeField] private float elevation = 24f;
    [Tooltip("How much of the window the model fills; above 1 is closer.")]
    [SerializeField] private float zoom = 1f;
    [SerializeField] private float swayDegrees = 24f;
    [SerializeField] private float swayPeriod = 7f;
    [SerializeField] private int textureSize = 512;

    private const float FieldOfView = 24f;
    // Canvas units dragged to degrees turned: a drag across the whole window is
    // about half a turn.
    private const float DegreesPerUnit = 0.55f;
    private const float ReturnDelay = 1.6f;
    private const float FadeIn = 0.18f;

    private RawImage _image;
    private RenderTexture _texture;
    private Canvas _canvas;
    private bool _framed, _prepared;
    private float _spin, _spinVelocity, _releasedAt = -99f, _shownAt;
    private bool _dragging, _forwarding;
    private readonly Vector3[] _mine = new Vector3[4], _view = new Vector3[4];

    void Awake()
    {
        _image = GetComponent<RawImage>();
        _image.texture = null;
        _image.color = Color.clear;
        if (slot != null) slot.SetActive(false);
    }

    void OnDisable() => Hide();

    void LateUpdate()
    {
        bool onScreen = IsOnScreen();
        if (onScreen && _texture == null) Show();
        else if (!onScreen && _texture != null) Hide();
        if (_texture == null) return;

        Turn(Time.unscaledDeltaTime);
        // The texture comes out of the pool holding whatever was last drawn into
        // it, and this camera's first picture only lands at the end of this
        // frame - so the window starts clear and fades in.
        _image.color = new Color(1f, 1f, 1f, Mathf.Clamp01((Time.unscaledTime - _shownAt) / FadeIn - 0.1f));
    }

    // Starts filming a little before the window scrolls into view, so it has
    // already faded in by the time it is really on screen.
    private bool IsOnScreen()
    {
        if (viewport == null) return true;
        ((RectTransform)transform).GetWorldCorners(_mine);
        viewport.GetWorldCorners(_view);
        float margin = (_view[1].y - _view[0].y) * 0.25f;
        return _mine[1].y > _view[0].y - margin && _mine[0].y < _view[1].y + margin;
    }

    private void Show()
    {
        if (slot == null || stageCamera == null) return;

        var descriptor = new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = 4,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
        };
        _texture = RenderTexture.GetTemporary(descriptor);

        slot.SetActive(true);
        if (!_prepared) Prepare();
        if (!_framed) Frame();

        stageCamera.targetTexture = _texture;
        _image.texture = _texture;
        _image.color = Color.clear;
        _shownAt = Time.unscaledTime;
    }

    private void Hide()
    {
        if (_texture == null) return;
        if (stageCamera != null) stageCamera.targetTexture = null;
        if (slot != null) slot.SetActive(false);
        if (_image != null)
        {
            _image.texture = null;
            _image.color = Color.clear;
        }
        RenderTexture.ReleaseTemporary(_texture);
        _texture = null;
    }

    // The player's own character paints its skin through property blocks and
    // glows brighter than a plain texture can hold. Its portrait recipe - the
    // same one the main menu uses - bakes the skin into owned materials with the
    // glow brought into range.
    private void Prepare()
    {
        _prepared = true;
        if (subject == null) return;
        foreach (var appearance in subject.GetComponentsInChildren<WardenAppearance>(true))
            appearance.PreparePortrait();
    }

    // Measured off the posed model rather than decided by the builder: the
    // bounds of a skinned mesh are only honest once its Animator has put it in
    // a pose, and that only happens at runtime.
    private void Frame()
    {
        if (subject == null) return;
        foreach (var animator in subject.GetComponentsInChildren<Animator>())
            animator.Update(0f);

        turntable.localRotation = Quaternion.identity;
        if (!Measure(out Bounds bounds)) return;

        // Centre the model on the turntable's axis, so it turns on the spot
        // instead of swinging round in a circle.
        Vector3 off = turntable.InverseTransformPoint(bounds.center);
        subject.localPosition -= new Vector3(off.x, 0f, off.z);
        if (!Measure(out bounds)) return;

        // Framed on the widest it gets as it turns, so nothing turns out of shot.
        float across = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float radius = Mathf.Max(0.05f, Mathf.Sqrt(across * across + bounds.extents.y * bounds.extents.y));
        float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.1f, zoom);

        float e = elevation * Mathf.Deg2Rad;
        stageCamera.fieldOfView = FieldOfView;
        stageCamera.transform.position = bounds.center + new Vector3(0f, Mathf.Sin(e), Mathf.Cos(e)) * distance;
        stageCamera.transform.LookAt(bounds.center);
        stageCamera.nearClipPlane = Mathf.Max(0.02f, distance - radius * 2f);
        stageCamera.farClipPlane = distance + radius * 3f;
        _framed = true;
    }

    private bool Measure(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var r in subject.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found && bounds.extents.sqrMagnitude > 0f;
    }

    private void Turn(float dt)
    {
        if (!_dragging)
        {
            _spin += _spinVelocity * dt;
            _spinVelocity *= Mathf.Exp(-4f * dt);

            // Once it has been let go a moment, back round to the nearest front -
            // the short way, however many turns it was given.
            if (Time.unscaledTime - _releasedAt > ReturnDelay)
            {
                float home = Mathf.Round(_spin / 360f) * 360f;
                _spin = Mathf.Lerp(_spin, home, 1f - Mathf.Exp(-2.5f * dt));
            }
        }

        float sway = Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / swayPeriod)) * swayDegrees;
        turntable.localRotation = Quaternion.Euler(0f, sway + _spin, 0f);
    }

    // ---- the drag, or the scroll it turns out to be ------------------------

    public void OnInitializePotentialDrag(PointerEventData e)
    {
        // Passed on regardless: it is what stops the page coasting when a thumb
        // lands on it, which is right whichever way the drag then goes.
        if (scroll != null) scroll.OnInitializePotentialDrag(e);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        _forwarding = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x);
        if (_forwarding)
        {
            if (scroll != null) scroll.OnBeginDrag(e);
            return;
        }
        _dragging = true;
        _spinVelocity = 0f;
    }

    public void OnDrag(PointerEventData e)
    {
        if (_forwarding)
        {
            if (scroll != null) scroll.OnDrag(e);
            return;
        }
        if (!_dragging) return;

        // The camera looks back along the model's forward axis, so turning it
        // positively carries its front to screen left: a drag to the right is a
        // negative turn.
        float degrees = -e.delta.x / ScaleFactor() * DegreesPerUnit;
        _spin += degrees;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
        _spinVelocity = Mathf.Lerp(_spinVelocity, degrees / dt, 0.5f);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_forwarding)
        {
            if (scroll != null) scroll.OnEndDrag(e);
            _forwarding = false;
            return;
        }
        _dragging = false;
        _releasedAt = Time.unscaledTime;
    }

    private float ScaleFactor()
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        return _canvas != null ? Mathf.Max(0.01f, _canvas.rootCanvas.scaleFactor) : 1f;
    }
}
