using UnityEngine;

// The shop card is authored at a fixed 700x920 canvas-unit size, sized to sit
// comfortably inside the 1920x1080 reference the CanvasScaler is tuned for.
// A device whose aspect is wider than that reference (e.g. the Pixel 9 Pro at
// 2142x960 landscape) has less vertical canvas room than the reference gives
// it, so the fixed-height card clips off the bottom of the screen - see the
// device review in Art/MobileReview/README.md. Scaling the whole panel down
// uniformly to whatever room is actually available keeps its internal layout
// intact instead of reflowing it.
[RequireComponent(typeof(RectTransform))]
public class UIFitToScreen : MonoBehaviour
{
    [Tooltip("Fraction of the available canvas area the panel is allowed to fill, leaving a margin on every side.")]
    [SerializeField, Range(0.5f, 1f)] private float maxFill = 0.92f;

    private RectTransform _rect;
    private Vector2 _designSize;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _designSize = _rect.sizeDelta;
    }

    void OnEnable()
    {
        Fit();
    }

    private void Fit()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Rect canvasRect = canvas.rootCanvas.GetComponent<RectTransform>().rect;

        float availableWidth = canvasRect.width * maxFill;
        float availableHeight = canvasRect.height * maxFill;

        float scale = Mathf.Min(1f, availableWidth / _designSize.x, availableHeight / _designSize.y);
        _rect.localScale = new Vector3(scale, scale, 1f);
    }
}
