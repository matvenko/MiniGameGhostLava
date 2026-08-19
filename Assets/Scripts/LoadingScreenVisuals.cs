using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Builds and animates the look of the loading screen: the lit backdrop, the
// ring loader, and the styling of the progress bar and percentage that
// LoadingScreenController drives.
//
// Everything here is generated (see UIShapes) rather than imported, for the
// same reason the board itself is: no art pipeline, no atlas to keep in
// sync, and the whole design is a handful of numbers that can be changed
// without opening an image editor. LoadingScreenController stays in charge
// of *when* things happen - this class only decides how they look.
public class LoadingScreenVisuals : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private RectTransform barRect;
    [SerializeField] private Image barTrack;
    [SerializeField] private Image barFill;
    [SerializeField] private TextMeshProUGUI percentText;

    [SerializeField] private int emberCount = 28;
    [SerializeField] private float ringSize = 250f;
    [SerializeField] private Vector2 barSize = new Vector2(620f, 26f);
    [SerializeField] private float fadeOutDuration = 0.35f;

    // One drifting light: where it started, how far and how fast it wanders.
    private struct Drifter
    {
        public RectTransform Rect;
        public Vector2 Home;
        public Vector2 Amplitude;
        public Vector2 Speed;
        public float Phase;
        public float BaseScale;
    }

    private readonly List<Drifter> _blobs = new List<Drifter>();
    private readonly List<RectTransform> _embers = new List<RectTransform>();
    private readonly List<float> _emberSpeeds = new List<float>();
    private readonly List<float> _emberSway = new List<float>();

    private RectTransform _backdrop;
    private RectTransform _loaderGroup;
    private CanvasGroup _loaderFade;
    private RectTransform _spinner;
    private Image _ringProgress;
    private Image _ringGlow;
    private RectTransform _barHead;
    private Sprite _emberSprite;

    private float _targetProgress;
    private float _shownProgress;
    private bool _embersPlaced;

    void Awake()
    {
        if (canvasRoot == null)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            canvasRoot = (RectTransform)canvas.transform;
        }

        BuildBackdrop();
        StyleTitle();
        BuildLoader();
        StyleBar();
    }

    // Called by LoadingScreenController on every progress update. The shown
    // value chases the real one instead of snapping to it, so a load that
    // jumps 0 -> 90% in one frame still reads as a sweep round the ring.
    public void SetProgress(float t)
    {
        _targetProgress = Mathf.Clamp01(t);
    }

    // Loading is over and the difficulty cards are about to take the middle
    // of the screen: fade the loader out rather than blinking it away. The
    // backdrop and title stay - they are the menu, not the loader.
    public void HideLoader()
    {
        if (_loaderGroup == null) return;
        StartCoroutine(FadeOutLoader());
    }

    private IEnumerator FadeOutLoader()
    {
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            if (_loaderFade != null) _loaderFade.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }
        _loaderGroup.gameObject.SetActive(false);
    }

    void Update()
    {
        float time = Time.unscaledTime;

        PlaceEmbersOnce();
        AnimateBlobs(time);
        AnimateEmbers();

        if (_spinner != null) _spinner.localRotation = Quaternion.Euler(0f, 0f, -time * 150f);

        // ease toward the reported progress, fast enough to feel responsive
        _shownProgress = Mathf.MoveTowards(_shownProgress, _targetProgress, Time.unscaledDeltaTime * 1.6f);
        if (_ringProgress != null) _ringProgress.fillAmount = _shownProgress;
        if (_ringGlow != null)
        {
            float pulse = 0.18f + 0.10f * Mathf.Sin(time * 2.4f);
            _ringGlow.color = MenuPalette.WithAlpha(MenuPalette.Violet, pulse);
        }
        if (_barHead != null)
        {
            _barHead.anchoredPosition = new Vector2(barSize.x * _shownProgress, 0f);
            _barHead.gameObject.SetActive(_shownProgress > 0.02f);
        }
    }

    // ---------------------------------------------------------------- backdrop

    private void BuildBackdrop()
    {
        _backdrop = NewRect("Backdrop", canvasRoot);
        Stretch(_backdrop);
        // behind every authored element, including the title
        _backdrop.SetAsFirstSibling();

        var sky = NewImage("Sky", _backdrop, UIShapes.VerticalGradient(MenuPalette.Backdrop()));
        Stretch((RectTransform)sky.transform);
        sky.color = Color.white;

        var glow = UIShapes.RadialGlow(256, 1.8f);
        AddBlob(glow, MenuPalette.Violet, 0.30f, new Vector2(-0.28f, 0.30f), 1100f, new Vector2(90f, 60f), new Vector2(0.11f, 0.07f), 0f);
        AddBlob(glow, MenuPalette.Magenta, 0.26f, new Vector2(0.32f, -0.12f), 1250f, new Vector2(70f, 90f), new Vector2(0.08f, 0.13f), 1.7f);
        AddBlob(glow, MenuPalette.Cyan, 0.20f, new Vector2(0.22f, 0.36f), 850f, new Vector2(110f, 50f), new Vector2(0.14f, 0.09f), 3.1f);
        AddBlob(glow, MenuPalette.Amber, 0.22f, new Vector2(-0.30f, -0.40f), 950f, new Vector2(80f, 70f), new Vector2(0.09f, 0.12f), 4.6f);

        // embers rise out of the heat at the bottom of the ramp
        _emberSprite = UIShapes.RadialGlow(64, 2.2f);
        for (int i = 0; i < emberCount; i++)
        {
            var ember = NewImage("Ember" + i, _backdrop, _emberSprite);
            float size = Random.Range(5f, 16f);
            ((RectTransform)ember.transform).sizeDelta = new Vector2(size, size);
            Color tint = i % 3 == 0 ? MenuPalette.Cyan : (i % 3 == 1 ? MenuPalette.Amber : MenuPalette.Magenta);
            ember.color = MenuPalette.WithAlpha(tint, Random.Range(0.25f, 0.7f));
            _embers.Add((RectTransform)ember.transform);
            _emberSpeeds.Add(Random.Range(18f, 55f));
            _emberSway.Add(Random.Range(0f, Mathf.PI * 2f));
        }

        var vignette = NewImage("Vignette", _backdrop, UIShapes.Vignette(256));
        Stretch((RectTransform)vignette.transform);
        vignette.color = new Color(0.02f, 0f, 0.06f, 0.75f);
    }

    private void AddBlob(Sprite sprite, Color color, float alpha, Vector2 anchor, float size, Vector2 amplitude, Vector2 speed, float phase)
    {
        var image = NewImage("Glow", _backdrop, sprite);
        var rect = (RectTransform)image.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f + anchor.x, 0.5f + anchor.y);
        rect.sizeDelta = new Vector2(size, size);
        image.color = MenuPalette.WithAlpha(color, alpha);

        _blobs.Add(new Drifter
        {
            Rect = rect,
            Home = Vector2.zero,
            Amplitude = amplitude,
            Speed = speed,
            Phase = phase,
            BaseScale = 1f
        });
    }

    private void AnimateBlobs(float time)
    {
        for (int i = 0; i < _blobs.Count; i++)
        {
            var b = _blobs[i];
            if (b.Rect == null) continue;
            b.Rect.anchoredPosition = b.Home + new Vector2(
                Mathf.Sin(time * b.Speed.x * Mathf.PI * 2f + b.Phase) * b.Amplitude.x,
                Mathf.Cos(time * b.Speed.y * Mathf.PI * 2f + b.Phase) * b.Amplitude.y);
            float s = b.BaseScale + Mathf.Sin(time * 0.6f + b.Phase) * 0.08f;
            b.Rect.localScale = new Vector3(s, s, 1f);
        }
    }

    // The canvas has no size until the first layout pass, so ember start
    // positions can only be scattered once it does.
    private void PlaceEmbersOnce()
    {
        if (_embersPlaced || _backdrop == null) return;
        Rect area = _backdrop.rect;
        if (area.height < 1f) return;

        foreach (var ember in _embers)
        {
            ember.anchoredPosition = new Vector2(
                Random.Range(-area.width * 0.5f, area.width * 0.5f),
                Random.Range(-area.height * 0.5f, area.height * 0.5f));
        }
        _embersPlaced = true;
    }

    private void AnimateEmbers()
    {
        if (!_embersPlaced) return;
        Rect area = _backdrop.rect;
        float top = area.height * 0.5f;
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < _embers.Count; i++)
        {
            var ember = _embers[i];
            Vector2 pos = ember.anchoredPosition;
            pos.y += _emberSpeeds[i] * dt;
            pos.x += Mathf.Sin(Time.unscaledTime * 0.7f + _emberSway[i]) * 12f * dt;

            // wrap back in under the bottom edge, re-scattered horizontally
            // so the same column never streams past twice
            if (pos.y > top + 20f)
            {
                pos.y = -top - 20f;
                pos.x = Random.Range(-area.width * 0.5f, area.width * 0.5f);
            }
            ember.anchoredPosition = pos;
        }
    }

    // ------------------------------------------------------------------ title

    private void StyleTitle()
    {
        if (titleText == null) return;

        titleText.enableVertexGradient = true;
        titleText.colorGradient = new VertexGradient(
            MenuPalette.Hex("FFF3C4"), MenuPalette.Hex("FFD166"),
            MenuPalette.Hex("FF7A59"), MenuPalette.Magenta);
        titleText.characterSpacing = 6f;

        // a wash of colour behind the words, so the title sits in the light
        // rather than on top of it
        var halo = NewImage("TitleHalo", (RectTransform)titleText.transform.parent, UIShapes.RadialGlow(256, 2f));
        var rect = (RectTransform)halo.transform;
        var titleRect = (RectTransform)titleText.transform;
        rect.anchorMin = titleRect.anchorMin;
        rect.anchorMax = titleRect.anchorMax;
        rect.anchoredPosition = titleRect.anchoredPosition;
        rect.sizeDelta = new Vector2(1100f, 460f);
        halo.color = MenuPalette.WithAlpha(MenuPalette.Magenta, 0.22f);
        halo.raycastTarget = false;
        rect.SetSiblingIndex(titleRect.GetSiblingIndex());
    }

    // ----------------------------------------------------------------- loader

    private void BuildLoader()
    {
        _loaderGroup = NewRect("Loader", canvasRoot);
        Stretch(_loaderGroup);
        _loaderFade = _loaderGroup.gameObject.AddComponent<CanvasGroup>();

        // the ring sits a little above centre, leaving the lower half to the
        // bar and, once loading ends, to the difficulty cards
        var ringHome = new Vector2(0f, 60f);

        _ringGlow = NewImage("RingGlow", _loaderGroup, UIShapes.RadialGlow(256, 2f));
        Centre((RectTransform)_ringGlow.transform, ringHome, new Vector2(ringSize * 2.1f, ringSize * 2.1f));
        _ringGlow.color = MenuPalette.WithAlpha(MenuPalette.Violet, 0.2f);

        var track = NewImage("RingTrack", _loaderGroup, UIShapes.Ring(256, 0.74f, 0.9f));
        Centre((RectTransform)track.transform, ringHome, new Vector2(ringSize, ringSize));
        track.color = new Color(1f, 1f, 1f, 0.12f);

        _ringProgress = NewImage("RingProgress", _loaderGroup, UIShapes.Ring(256, 0.74f, 0.9f, MenuPalette.RingSweep()));
        Centre((RectTransform)_ringProgress.transform, ringHome, new Vector2(ringSize, ringSize));
        _ringProgress.type = Image.Type.Filled;
        _ringProgress.fillMethod = Image.FillMethod.Radial360;
        _ringProgress.fillOrigin = (int)Image.Origin360.Top;
        _ringProgress.fillClockwise = true;
        _ringProgress.fillAmount = 0f;

        // the one part that never stops: the ring can sit still at 100% while
        // the minimum display time runs out, and a frozen loader looks hung
        var spinner = NewImage("Spinner", _loaderGroup, UIShapes.Arc(256, 0.9f, 0.99f, 0.42f));
        Centre((RectTransform)spinner.transform, ringHome, new Vector2(ringSize * 1.18f, ringSize * 1.18f));
        spinner.color = MenuPalette.WithAlpha(MenuPalette.Cyan, 0.85f);
        _spinner = (RectTransform)spinner.transform;

        BuildCaption();
    }

    private void BuildCaption()
    {
        var go = new GameObject("LoadingCaption", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_loaderGroup, false);
        Centre(rect, new Vector2(0f, -110f), new Vector2(600f, 40f));

        var text = go.AddComponent<TextMeshProUGUI>();
        if (percentText != null) text.font = percentText.font;
        text.text = "LOADING";
        text.fontSize = 24f;
        text.characterSpacing = 18f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 1f, 1f, 0.55f);
        text.raycastTarget = false;
    }

    // -------------------------------------------------------------------- bar

    private void StyleBar()
    {
        if (percentText != null)
        {
            // the number belongs in the middle of the ring, not on its own
            // line - it is what the ring is measuring
            percentText.transform.SetParent(_loaderGroup, false);
            Centre((RectTransform)percentText.transform, new Vector2(0f, 60f), new Vector2(ringSize, 120f));
            percentText.fontSize = 62f;
            percentText.fontStyle = FontStyles.Bold;
            percentText.alignment = TextAlignmentOptions.Center;
            percentText.color = Color.white;
            percentText.raycastTarget = false;
        }

        if (barRect == null) return;

        barRect.SetParent(_loaderGroup, false);
        Centre(barRect, new Vector2(0f, -170f), barSize);

        if (barTrack != null)
        {
            barTrack.sprite = UIShapes.Capsule(96, 32);
            barTrack.type = Image.Type.Sliced;
            barTrack.color = new Color(1f, 1f, 1f, 0.12f);
        }

        if (barFill != null)
        {
            barFill.sprite = UIShapes.Capsule(256, 32, MenuPalette.BarFill());
            barFill.type = Image.Type.Sliced;
            barFill.color = Color.white;
        }

        // a bright head riding the end of the fill, so the bar reads as
        // moving even over the last few percent
        var head = NewImage("BarHead", barRect, UIShapes.RadialGlow(64, 1.6f));
        _barHead = (RectTransform)head.transform;
        _barHead.anchorMin = _barHead.anchorMax = new Vector2(0f, 0.5f);
        _barHead.pivot = new Vector2(0.5f, 0.5f);
        _barHead.sizeDelta = new Vector2(barSize.y * 3.4f, barSize.y * 3.4f);
        head.color = MenuPalette.WithAlpha(MenuPalette.Amber, 0.75f);
        head.raycastTarget = false;
    }

    // ------------------------------------------------------------- rect utils

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image NewImage(string name, RectTransform parent, Sprite sprite)
    {
        var rect = NewRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Centre(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
