using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Fits the drawn lives pill to the HUD and rebuilds it in the open scene.
//
// life-indicator.png is a purple capsule with five ghosts painted into it, and
// five is the one count the game never has: a run starts on three, the shop
// sells a fourth, a fifth and a sixth, and every death takes one away. So the
// ghosts do not survive as paint. The capsule is emptied out, one ghost is cut
// back out of it as a sprite of its own, and LivesManager lays as many of those
// along the pill as the player has left - which is what it already did with the
// flat icons that were there before.
//
// Emptying it is not a rectangle this time. The ghosts sit inside a capsule
// whose ends curve, and the first and last of them are inside that curve, so a
// box wide enough to cover them also covers the rim. Instead every row of the
// interior is refilled with the colour that row has in the gaps between the
// ghosts, from the dark ring just inside the border across to the dark ring on
// the other side. That erases the ghosts, leaves the rim and the caps alone, and
// leaves the interior flat across its width - which is what lets the pill be
// nine-sliced and stretched as the count changes.
//
// Same rule about scenes as JoystickArtBuilder: this works on whatever scene is
// open, never discards it, and is safe to run twice. Ghosts_Ui and Ghost0..5 are
// kept rather than made afresh, because LivesManager holds references to them.
public static class LivesBadgeBuilder
{
    private const string SourcePath = "Assets/Materials/life-indicator.png";
    private const string PanelPath = "Assets/UI/Icons/lives_panel.png";
    private const string GhostPath = "Assets/UI/Icons/life_ghost.png";

    private const string RootName = "Ghosts_Ui";
    private const string FillName = "Fill";
    private const string IconPrefix = "Ghost";

    // ---- the art, in source pixels (y counted from the bottom) --------------

    private const float SrcWidth = 2142f, SrcHeight = 734f;
    private const float ContentRight = 2073f, ContentTop = 638f;

    // The middles of the four gaps between the painted ghosts. Averaging all
    // four gives the colour of a row of clean capsule, and averaging that many
    // columns keeps one ghost's shadow from tinting the whole row.
    private static readonly int[] GapStart = { 554, 883, 1211, 1542 };
    private const int GapWidth = 31;

    // The ghost that gets cut out, and the pitch its neighbours sit on. The box
    // is exactly one pitch wide, so laying the cut-outs back down at that pitch
    // reproduces the spacing in the painting.
    private static readonly RectInt GhostBox = new RectInt(897, 195, 328, 320);
    private const int Pitch = 329;
    private const int FirstBoxLeft = 239;
    private const int PaintedGhosts = 5;

    // Where a cut-out ends and the capsule begins, as alpha. The ghosts carry a
    // dark outline that is not far off the interior in colour, so the knee is low
    // - anything appreciably different from the capsule behind it is the ghost.
    private const float CutFloor = 0.02f, CutKnee = 0.10f;

    // Nine-slice: the capsule's ends are round, so the part that may be stretched
    // starts past where the curve straightens out. The outer shape is 534 tall,
    // which puts the cap's own radius at 267 and the straight run from 331 to
    // 1806; these are just outside that, with the gloss on the left cap inside
    // the border where it cannot be pulled about.
    private const int SliceBorder = 350;
    private const int Downscale = 2;

    // ---- layout ------------------------------------------------------------

    // Canvas units against the 1920x1080 reference the scaler matches on width.
    // 96 puts the capsule itself at about 70 units tall, in line with the coin
    // counter's 65 and the level badge's 82.
    private const float PanelHeight = 96f;
    private const float Scale = PanelHeight / SrcHeight;
    // Room in the corner for the settings gear, which sits beside the pill at the
    // same height: 36 to the screen's edge, 70 of gear, 14 of gap.
    private const float ContentMarginRight = 120f;
    private const float ContentMarginTop = 19f;

    [MenuItem("Tools/Build Lives Badge")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        Color[] painted = HudArt.Load(SourcePath, out int w, out int h);
        if (painted == null) return;
        if (w != (int)SrcWidth || h != (int)SrcHeight)
        {
            Debug.LogError("[LivesBadge] " + SourcePath + " is " + w + "x" + h + ", not " + SrcWidth + "x" + SrcHeight +
                           ". Every figure in this builder was measured off that art - re-measure them before running it.");
            return;
        }

        Color[] refRow = RowReference(painted, w, h);
        Color[] empty = FlattenInterior(painted, w, h, refRow);

        Sprite panel = HudArt.Write(PanelPath, empty, w, h, Downscale,
                                    new Vector4(SliceBorder, 0f, SliceBorder, 0f));
        Sprite ghost = HudArt.Write(GhostPath, CutOutGhost(painted, w), GhostBox.width, GhostBox.height, Downscale);
        if (panel == null || ghost == null) return;

        RectTransform root = BuildPanel(canvas, panel);
        Image[] icons = BuildIcons(root, ghost);
        WireManager(root, icons, ghost);

        HudScene.Save(canvas);
        Debug.Log("[LivesBadge] Rebuilt in " + canvas.gameObject.scene.name + ": pill " + PanelHeight +
                  " tall with " + icons.Length + " ghost slots, art from " + SourcePath + ".");
    }

    // ---- art ---------------------------------------------------------------

    // What a row of capsule looks like where no ghost is standing on it.
    private static Color[] RowReference(Color[] px, int w, int h)
    {
        var rows = new Color[h];
        for (int y = 0; y < h; y++)
        {
            var sum = new Color(0f, 0f, 0f, 0f);
            foreach (int start in GapStart)
                for (int i = 0; i < GapWidth; i++) sum += px[y * w + start + i];
            rows[y] = sum / (GapStart.Length * GapWidth);
        }
        return rows;
    }

    private static Color[] FlattenInterior(Color[] px, int w, int h, Color[] refRow)
    {
        var outPixels = (Color[])px.Clone();

        var left = new int[h];
        var right = new int[h];
        for (int y = 0; y < h; y++)
            if (!InteriorSpan(px, w, y, refRow[y], out left[y], out right[y]))
                left[y] = right[y] = -1;

        // The ring's inner edge is a smooth curve, so a row whose edge lands a
        // long way off its neighbours' found something else - a stray pixel that
        // happened to match. Taking the median of a few rows drops those, which
        // is what stops the odd unfilled streak appearing across the interior.
        int[] smoothLeft = Median(left), smoothRight = Median(right);

        for (int y = 0; y < h; y++)
        {
            if (smoothLeft[y] < 0 || smoothRight[y] < 0) continue;
            for (int x = smoothLeft[y]; x <= smoothRight[y]; x++)
                outPixels[y * w + x] = refRow[y];
        }

        return outPixels;
    }

    // Walks in from each end of a row: past the transparent margin, past the dark
    // outline around the capsule, past the bright purple border, and stops at the
    // first pixel that is not border again - the dark ring around the interior.
    //
    // Coming in from the outside is what makes this work on the rows where a
    // ghost is standing: whatever is in the middle of the row is never looked at,
    // so a white ghost body cannot be mistaken for the far side of the capsule.
    // Median of five, over the rows that found an edge at all.
    private static int[] Median(int[] edge)
    {
        var outEdge = new int[edge.Length];
        var window = new System.Collections.Generic.List<int>(5);

        for (int y = 0; y < edge.Length; y++)
        {
            if (edge[y] < 0) { outEdge[y] = -1; continue; }

            window.Clear();
            for (int d = -2; d <= 2; d++)
            {
                int i = y + d;
                if (i >= 0 && i < edge.Length && edge[i] >= 0) window.Add(edge[i]);
            }
            window.Sort();
            outEdge[y] = window[window.Count / 2];
        }

        return outEdge;
    }

    private static bool InteriorSpan(Color[] px, int w, int y, Color reference, out int left, out int right)
    {
        left = Walk(px, w, y, 0, 1, reference);
        right = Walk(px, w, y, w - 1, -1, reference);
        return left >= 0 && right >= 0 && right - left > 40;
    }

    private static int Walk(Color[] px, int w, int y, int from, int step, Color reference)
    {
        const int gap = 8;      // the outline and the border meet through a few blended pixels
        const int clear = 3;    // a little further in, past the ring's own soft edge

        int x = from;
        while (InRange(x, w) && px[y * w + x].a <= 0.5f) x += step;   // outside the drawing

        // Runs on to the LAST border pixel rather than stopping at the first one
        // that is not: the outline blends into the purple through pixels that are
        // neither, and stopping on those would put the whole border inside the
        // interior and paint over it.
        int lastBorder = -1;
        for (int seen = 0; InRange(x, w) && (lastBorder < 0 || seen <= gap); x += step, seen++)
            if (IsBorder(px[y * w + x])) { lastBorder = x; seen = 0; }
        if (lastBorder < 0) return -1;

        // Then in past the dark ring, however thick it is on this row - which is
        // not a fixed number of pixels, because the ring runs vertically at the
        // caps and diagonally around them. It ends where the row first looks like
        // the capsule does between the ghosts.
        x = lastBorder + step;
        while (InRange(x, w) && !Matches(px[y * w + x], reference)) x += step;
        return x + clear * step;
    }

    private static bool InRange(int x, int w) => x >= 0 && x < w;

    private static bool Matches(Color c, Color reference) =>
        Mathf.Abs(c.r - reference.r) < 0.06f &&
        Mathf.Abs(c.g - reference.g) < 0.06f &&
        Mathf.Abs(c.b - reference.b) < 0.06f;

    // The border is the one thing here that is both bright and strongly blue: the
    // interior sits around 0.3 blue, the ring and the outline lower still, and
    // the outermost blended pixel of the outline is blue but dark. Luminance
    // cannot separate them - purple is dark - so this reads the channels.
    private static bool IsBorder(Color c) => c.a > 0.5f && c.b > 0.6f && c.r > 0.35f;

    // Lifts one painted ghost off the capsule. The background it is standing on
    // is rebuilt across the box by blending the gap either side of it, rather
    // than taken from the flattened panel, so the faint diagonal sheen across the
    // interior is subtracted where it actually falls instead of leaving a haze
    // around the cut-out.
    private static Color[] CutOutGhost(Color[] px, int w)
    {
        var outPixels = new Color[GhostBox.width * GhostBox.height];

        for (int y = GhostBox.yMin; y < GhostBox.yMax; y++)
        {
            Color left = Average(px, w, y, GapStart[1], GapWidth);
            Color right = Average(px, w, y, GapStart[2], GapWidth);
            float leftAt = GapStart[1] + GapWidth * 0.5f;
            float rightAt = GapStart[2] + GapWidth * 0.5f;

            for (int x = GhostBox.xMin; x < GhostBox.xMax; x++)
            {
                Color behind = Color.Lerp(left, right, (x - leftAt) / (rightAt - leftAt));
                Color c = px[y * w + x];

                float d = Mathf.Max(Mathf.Abs(c.r - behind.r),
                          Mathf.Max(Mathf.Abs(c.g - behind.g), Mathf.Abs(c.b - behind.b)));
                float a = Mathf.Clamp01((d - CutFloor) / (CutKnee - CutFloor));

                int i = (y - GhostBox.yMin) * GhostBox.width + (x - GhostBox.xMin);
                outPixels[i] = a <= 0f
                    ? new Color(0f, 0f, 0f, 0f)
                    // Straight alpha: back out whatever of the capsule was showing
                    // through, so the cut-out lays back down over it unchanged.
                    : new Color(Mathf.Clamp01((c.r - (1f - a) * behind.r) / a),
                                Mathf.Clamp01((c.g - (1f - a) * behind.g) / a),
                                Mathf.Clamp01((c.b - (1f - a) * behind.b) / a), a);
            }
        }

        return outPixels;
    }

    private static Color Average(Color[] px, int w, int y, int x0, int count)
    {
        var sum = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < count; i++) sum += px[y * w + x0 + i];
        return sum / count;
    }

    // ---- scene -------------------------------------------------------------

    private static RectTransform BuildPanel(Canvas canvas, Sprite sprite)
    {
        RectTransform rt = HudScene.Panel(canvas, RootName, sprite);

        // Pinned to the top-right corner by its right edge, because that is the
        // edge that stays put: LivesManager narrows the pill as lives are spent.
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(PanelWidth(PaintedGhosts), PanelHeight);
        rt.anchoredPosition = new Vector2(
            -(ContentMarginRight - (SrcWidth - ContentRight) * Scale),
            -(ContentMarginTop - (SrcHeight - ContentTop) * Scale));

        var image = rt.GetComponent<Image>();
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        // Sliced draws a border at its size in sprite pixels, and this sprite is
        // shown at about a quarter of the size it was painted at; without this the
        // two caps alone would be wider than the whole pill.
        image.pixelsPerUnitMultiplier = 1f / (Downscale * Scale);

        // Under every popup, so the pause menu and the shop cover the readout
        // instead of it floating over their backdrops.
        rt.SetSiblingIndex(1);

        HudScene.Remove(rt, FillName);
        return rt;
    }

    private static Image[] BuildIcons(RectTransform root, Sprite sprite)
    {
        var icons = new Image[LivesManager.HardCap];
        for (int i = 0; i < icons.Length; i++)
        {
            string name = IconPrefix + i;
            Transform found = root.Find(name);
            GameObject go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));

            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            // Centred on itself, so LivesManager's punch on a lost life swells the
            // ghost in place rather than shoving it sideways.
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(IconWidth, GhostBox.height * Scale);
            rt.anchoredPosition = new Vector2(Padding + i * Pitch * Scale + IconWidth * 0.5f,
                                              (GhostBox.center.y - SrcHeight * 0.5f) * Scale);
            rt.localScale = Vector3.one;   // a punch that was interrupted leaves this scaled up

            var image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // LivesManager decides this on its first frame. Leaving the five the
            // artist drew showing is so the Scene view matches the painting
            // rather than hanging a sixth ghost off the end of the pill.
            go.SetActive(i < PaintedGhosts);

            icons[i] = image;
        }
        return icons;
    }

    private static void WireManager(RectTransform root, Image[] icons, Sprite ghost)
    {
        var lives = Object.FindAnyObjectByType<LivesManager>(FindObjectsInactive.Include);
        if (lives == null)
        {
            Debug.LogWarning("[LivesBadge] No LivesManager in the scene; the pill is built but nothing drives it.");
            return;
        }

        var so = new SerializedObject(lives);
        SerializedProperty array = so.FindProperty("ghostIcons");
        array.arraySize = icons.Length;
        for (int i = 0; i < icons.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];

        so.FindProperty("ghostFullSprite").objectReferenceValue = ghost;
        so.FindProperty("panelRect").objectReferenceValue = root;
        // The pill's own measurements, so its width formula lands on the painted
        // one exactly when all five slots the artist drew are showing.
        so.FindProperty("panelPadding").floatValue = Padding;
        so.FindProperty("iconSize").floatValue = IconWidth;
        so.FindProperty("iconSpacing").floatValue = Pitch * Scale - IconWidth;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static float IconWidth => GhostBox.width * Scale;

    // Measured from the panel's edge, not the capsule's: the export carries a
    // little transparent margin, and the width formula works on the rect.
    // Averaged between the two ends, which the drawing has a few pixels apart, so
    // that five slots come out at exactly the painted width.
    private static float Padding =>
        ((FirstBoxLeft + (SrcWidth - (FirstBoxLeft + (PaintedGhosts - 1) * Pitch + GhostBox.width))) * 0.5f) * Scale;

    private static float PanelWidth(int lives) =>
        2f * Padding + lives * IconWidth + (lives - 1) * (Pitch * Scale - IconWidth);
}
