using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The body of the test report card: what the run did, level by level, as six
// small charts - coins earned and spent, abilities used, lives lost, and how
// they were lost - with the same six for the last five runs on this device
// laid over one another, and the plain table a tap away.
//
// Kept to a few rules so the charts read at a glance on a phone: one quiet
// colour while a single run is shown; when runs are compared, five colours in
// a fixed order (the reference palette's first five, checked against this
// card's surface) with a legend naming every run; thin marks on a recessive
// grid; a single run's columns each carry their value, and a tap gives the
// detail. Text keeps to text colours - only the marks wear a run's colour.
public class PlaytestReportView
{
    private const int MaxRuns = 5;
    private const int Compare = -1;
    private const int TableView = -2;

    private static readonly string[] Titles =
        { "Coins earned", "Coins spent", "Abilities used", "Deaths", "Caught by hunters", "Fell into hazard" };
    private static readonly string[] Units = { "coins", "coins", "used", "lives lost", "caught", "fell in" };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Categorical slots 1-5, their dark steps, validated as a set on #121429.
    private static readonly Color[] Series = { Hex(0x3987e5), Hex(0xd95926), Hex(0x199e70), Hex(0xc98500), Hex(0xd55181) };
    private static readonly Color Primary = Color.white;
    private static readonly Color Secondary = Hex(0xc3c2b7);
    private static readonly Color Muted = Hex(0x898781);
    private static readonly Color CardFill = Hex(0x191c38);
    private static readonly Color Gridline = Hex(0x2a2e4d);
    private static readonly Color Baseline = Hex(0x474d72);
    private static readonly Color ChipOn = new Color(.24f, .28f, .42f);
    private static readonly Color ChipOff = new Color(.11f, .13f, .25f);

    private readonly RectTransform _root;
    private readonly Vector2 _size;
    private readonly List<PlaytestReport> _runs = new List<PlaytestReport>();
    private readonly List<KeyValuePair<int, Button>> _chips = new List<KeyValuePair<int, Button>>();
    private RectTransform _content;
    private int _view;
    private int _lastRun;
    // The first run is the one that just ended, rather than just the newest
    // report on the device (STATS).
    private readonly bool _justPlayed;

    // The given run leads; the rest are the newest reports on this device, up
    // to five in all.
    public PlaytestReportView(Transform parent, PlaytestReport current, bool justPlayed, Vector2 centre, Vector2 size)
    {
        _root = TestModeOverlay.Rect(parent, "Charts", centre, size);
        _size = size;
        _justPlayed = justPlayed;
        _runs.Add(current);
        foreach (var run in PlaytestLog.RecentReports(MaxRuns + 1))
            if (_runs.Count < MaxRuns && run.runId != current.runId) _runs.Add(run);
        BuildChips();
        Show(0);
    }

    // ---- which view --------------------------------------------------------------

    private void BuildChips()
    {
        var views = new List<int>();
        for (int i = 0; i < _runs.Count; i++) views.Add(i);
        if (_runs.Count > 1) views.Add(Compare);
        views.Add(TableView);

        const float gap = 12f;
        float total = views.Sum(v => ChipWidth(v) + gap) - gap;
        float x = -total * .5f;
        float y = _size.y * .5f - 26f;
        foreach (int v in views)
        {
            float w = ChipWidth(v);
            int view = v;
            var button = TestModeOverlay.Button(_root, "View " + v, ChipText(v), new Vector2(x + w * .5f, y), new Vector2(w, 50f),
                ChipOff, () => Show(view), out var label);
            label.fontSize = 19;
            _chips.Add(new KeyValuePair<int, Button>(v, button));
            x += w + gap;
        }
    }

    private static float ChipWidth(int view) => view >= 0 ? 240f : 170f;

    private string ChipText(int view)
    {
        if (view == Compare) return "LAST " + _runs.Count;
        if (view == TableView) return "TABLE";
        return view == 0 && _justPlayed ? "THIS RUN" : Clock(_runs[view]) + "  " + ShortWho(_runs[view]);
    }

    private void Show(int view)
    {
        _view = view;
        if (view >= 0) _lastRun = view;
        foreach (var chip in _chips) chip.Value.image.color = chip.Key == view ? ChipOn : ChipOff;

        if (_content != null) Object.Destroy(_content.gameObject);
        float h = _size.y - 60f;
        _content = TestModeOverlay.Rect(_root, "Content", new Vector2(0f, -30f), new Vector2(_size.x, h));

        if (view == Compare) Legend(h);
        else Describe(_runs[_lastRun], h);

        // The table is for the run last looked at.
        if (view == TableView)
        {
            TestModeOverlay.Table(_content, _runs[_lastRun], new Vector2(0f, -20f), new Vector2(_size.x - 60f, h - 40f));
            return;
        }

        float top = h * .5f - 38f;
        float cardH = (h - 38f - 16f) * .5f;
        float cardW = (_size.x - 40f) / 3f;
        for (int m = 0; m < Titles.Length; m++)
        {
            int col = m % 3, row = m / 3;
            Card(m, new Vector2((col - 1) * (cardW + 20f), top - cardH * .5f - row * (cardH + 16f)), new Vector2(cardW, cardH));
        }
    }

    // One run: a line saying which run this is.
    private void Describe(PlaytestReport r, float h)
    {
        string text = (r == _runs[0] && _justPlayed ? "This run" : Clock(r) + " run") + "  ·  " + Who(r) + "  ·  " + r.difficulty
                      + "  ·  level " + r.startLevel + " → " + r.endLevel + "  ·  " + TestModeOverlay.EndReason(r.endReason);
        TestModeOverlay.Label(_content, "Legend", text, new Vector2(0f, h * .5f - 16f), new Vector2(_size.x, 28f), 19, Secondary);
    }

    // Several runs: every one named beside the colour it is drawn in.
    private void Legend(float h)
    {
        const float itemW = 330f;
        float left = -itemW * _runs.Count * .5f;
        float y = h * .5f - 16f;
        for (int i = 0; i < _runs.Count; i++)
        {
            float x = left + itemW * i;
            Dot(_content, new Vector2(x + 14f, y), 14f, Series[i]);
            var r = _runs[i];
            var label = TestModeOverlay.Label(_content, "Run " + (i + 1),
                "#" + (i + 1) + "  " + (i == 0 && _justPlayed ? "This run" : Clock(r)) + "  ·  " + ShortWho(r) + "  ·  L" + r.startLevel + "-" + r.endLevel,
                new Vector2(x + 30f + 145f, y), new Vector2(290f, 28f), 18, Secondary);
            label.alignment = TextAlignmentOptions.Left;
        }
    }

    // ---- a card -------------------------------------------------------------------

    private void Card(int metric, Vector2 centre, Vector2 size)
    {
        var card = TestModeOverlay.Box(_content, Titles[metric], centre, size, CardFill).transform;
        float w = size.x, h = size.y;

        var title = TestModeOverlay.Label(card, "Title", Titles[metric], new Vector2(-w * .5f + 20f + 170f, h * .5f - 26f),
            new Vector2(340f, 30f), 22, Secondary);
        title.alignment = TextAlignmentOptions.Left;
        var total = TestModeOverlay.Label(card, "Total", "", new Vector2(w * .5f - 20f - 110f, h * .5f - 30f),
            new Vector2(220f, 44f), 36, Primary);
        total.alignment = TextAlignmentOptions.Right;
        var readout = TestModeOverlay.Label(card, "Readout", "", new Vector2(-w * .5f + 20f + 250f, h * .5f - 60f),
            new Vector2(500f, 24f), 17, Muted);
        readout.alignment = TextAlignmentOptions.Left;

        var plot = TestModeOverlay.Rect(card, "Plot", new Vector2(24f, -26f), new Vector2(w - 108f, h - 138f));
        if (_view >= 0) Columns(plot, metric, _runs[_view], total, readout);
        else Lines(plot, metric, total, readout);
    }

    // One run: a column per level with its value on top, and a tap on any of
    // them for what it is made of.
    private void Columns(RectTransform plot, int metric, PlaytestReport run, TextMeshProUGUI total, TextMeshProUGUI readout)
    {
        var levels = run.levels;
        int n = levels.Count;
        var values = new int[n];
        int sum = 0, peak = -1;
        for (int i = 0; i < n; i++)
        {
            values[i] = Value(levels[i], metric);
            sum += values[i];
            if (values[i] > 0 && (peak < 0 || values[i] > values[peak])) peak = i;
        }
        total.text = sum.ToString("N0", Inv);
        readout.text = n == 0 ? "No level was played" : peak < 0 ? "None this run" : "Most on level " + levels[peak].level + "  ·  tap a bar";
        if (n == 0) return;

        float max = NiceMax(peak >= 0 ? values[peak] : 0);
        Axes(plot, max);
        float pw = plot.sizeDelta.x, ph = plot.sizeDelta.y;
        float band = pw / n;
        float barW = Mathf.Min(24f, band * .6f);
        for (int i = 0; i < n; i++)
        {
            float x = -pw * .5f + band * (i + .5f);
            if (values[i] > 0) Bar(plot, new Vector2(x, -ph * .5f), barW, Mathf.Max(values[i] / max * ph, 3f), Series[0]);
            if (n <= 12 || i % 2 == 0) LevelLabel(plot, x, ph, levels[i].level + (levels[i].joinedMidLevel ? "*" : ""));

            var level = levels[i];
            int v = values[i];
            Hit(plot, new Vector2(x, 0f), new Vector2(band, ph + 36f),
                () => readout.text = "Level " + level.level + ":  " + v.ToString("N0", Inv) + " " + Units[metric] + Detail(level, metric));
        }
        // Every column carries its value on its cap - a size smaller when the
        // columns are packed tight enough for the numbers to meet.
        float valueSize = band < 50f ? 14f : 16f;
        for (int i = 0; i < n; i++)
        {
            if (values[i] <= 0) continue;
            float x = -pw * .5f + band * (i + .5f);
            float y = -ph * .5f + Mathf.Max(values[i] / max * ph, 3f) + 11f;
            TestModeOverlay.Label(plot, "Value", values[i].ToString("N0", Inv), new Vector2(x, y),
                new Vector2(Mathf.Max(band, 40f), 20f), valueSize, Primary);
        }
    }

    // The last few runs: a line each across the levels, this run drawn last so
    // it sits on top. Tapping a level lists every run's value there.
    private void Lines(RectTransform plot, int metric, TextMeshProUGUI total, TextMeshProUGUI readout)
    {
        var byRun = _runs.Select(r => ValuesByLevel(r, metric)).ToList();
        int first = int.MaxValue, last = int.MinValue, peak = 0;
        foreach (var values in byRun)
            foreach (var kv in values)
            {
                first = Mathf.Min(first, kv.Key);
                last = Mathf.Max(last, kv.Key);
                peak = Mathf.Max(peak, kv.Value);
            }
        total.text = byRun[0].Values.Sum().ToString("N0", Inv);
        if (first > last)
        {
            readout.text = "No level was played";
            return;
        }
        readout.text = (_justPlayed ? "Total is this run's" : "Total is the newest run's") + "  ·  tap a level";

        float max = NiceMax(peak);
        Axes(plot, max);
        float pw = plot.sizeDelta.x, ph = plot.sizeDelta.y;
        int count = last - first + 1;
        float band = pw / count;
        Vector2 At(int level, int v) => new Vector2(-pw * .5f + band * (level - first + .5f), -ph * .5f + v / max * ph);

        for (int i = _runs.Count - 1; i >= 0; i--)
            for (int level = first; level < last; level++)
                if (byRun[i].TryGetValue(level, out int a) && byRun[i].TryGetValue(level + 1, out int b))
                    Segment(plot, At(level, a), At(level + 1, b), Series[i]);
        for (int i = _runs.Count - 1; i >= 0; i--)
            foreach (var kv in byRun[i])
            {
                var p = At(kv.Key, kv.Value);
                Dot(plot, p, 14f, CardFill);
                Dot(plot, p, 9f, Series[i]);
            }

        for (int level = first; level <= last; level++)
        {
            float x = -pw * .5f + band * (level - first + .5f);
            if (count <= 12 || (level - first) % 2 == 0) LevelLabel(plot, x, ph, level.ToString(Inv));
            int lvl = level;
            Hit(plot, new Vector2(x, 0f), new Vector2(band, ph + 36f), () =>
                readout.text = "Level " + lvl + ":   " + string.Join("   ", byRun.Select((values, i) =>
                    "#" + (i + 1) + " " + (values.TryGetValue(lvl, out int v) ? v.ToString("N0", Inv) : "-"))));
        }
    }

    // ---- marks ------------------------------------------------------------------------

    // Three hairlines - nought, half and the top - labelled on the left.
    private static void Axes(RectTransform plot, float max)
    {
        float pw = plot.sizeDelta.x, ph = plot.sizeDelta.y;
        var ticks = max >= 2f ? new[] { 0f, max * .5f, max } : new[] { 0f, max };
        foreach (float v in ticks)
        {
            float y = -ph * .5f + v / max * ph;
            TestModeOverlay.Box(plot, "Grid", new Vector2(0f, y), new Vector2(pw, 1f), v == 0f ? Baseline : Gridline);
            var label = TestModeOverlay.Label(plot, "Tick", ((int)v).ToString("N0", Inv), new Vector2(-pw * .5f - 40f, y),
                new Vector2(64f, 20f), 15, Muted);
            label.alignment = TextAlignmentOptions.Right;
        }
    }

    // A clean top for the scale that halves to a whole number: 4, 8, 20, 60...
    private static float NiceMax(float v)
    {
        if (v <= 1f) return 1f;
        if (v <= 2f) return 2f;
        float p = v < 10f ? 1f : Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(v)));
        foreach (float m in new[] { 2f, 4f, 6f, 8f, 10f })
            if (m * p >= v) return m * p;
        return 10f * p;
    }

    private static void LevelLabel(RectTransform plot, float x, float ph, string text) =>
        TestModeOverlay.Label(plot, "Level", text, new Vector2(x, -ph * .5f - 16f), new Vector2(50f, 22f), 15, Muted);

    // Grows up from the baseline, rounded at the top and square at the foot.
    private static void Bar(RectTransform plot, Vector2 foot, float width, float height, Color colour)
    {
        var bar = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(plot, false);
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f);
        bar.pivot = new Vector2(.5f, 0f);
        bar.anchoredPosition = foot;
        bar.sizeDelta = new Vector2(width, height);
        var image = bar.gameObject.AddComponent<Image>();
        image.sprite = RoundedCap();
        image.type = Image.Type.Sliced;
        image.color = colour;
        image.raycastTarget = false;
    }

    private static void Segment(RectTransform plot, Vector2 from, Vector2 to, Color colour)
    {
        var line = new GameObject("Line", typeof(RectTransform)).GetComponent<RectTransform>();
        line.SetParent(plot, false);
        line.anchorMin = line.anchorMax = new Vector2(.5f, .5f);
        line.pivot = new Vector2(0f, .5f);
        line.anchoredPosition = from;
        Vector2 d = to - from;
        line.sizeDelta = new Vector2(d.magnitude, 2f);
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var image = line.gameObject.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
    }

    private static void Dot(Transform parent, Vector2 centre, float size, Color colour) =>
        TestModeOverlay.Box(parent, "Dot", centre, new Vector2(size, size), colour).sprite = Circle();

    // Bigger than the mark it belongs to - a whole level's band - so a thumb
    // finds it.
    private static void Hit(RectTransform plot, Vector2 centre, Vector2 size, Action pressed)
    {
        var area = TestModeOverlay.Box(plot, "Hit", centre, size, new Color(0f, 0f, 0f, .001f));
        area.raycastTarget = true;
        area.gameObject.AddComponent<Tap>().Pressed = pressed;
    }

    private sealed class Tap : MonoBehaviour, IPointerDownHandler
    {
        public Action Pressed;
        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();
    }

    // ---- the numbers --------------------------------------------------------------------

    private static int Value(PlaytestLevel l, int metric)
    {
        switch (metric)
        {
            case 0: return l.walletEarned;
            case 1: return l.coinsSpent;
            case 2: return l.traps + l.freezes + l.shields + l.teleports;
            case 3: return l.deaths;
            case 4: return l.enemyDeaths;
            default: return l.lavaDeaths;
        }
    }

    private static Dictionary<int, int> ValuesByLevel(PlaytestReport run, int metric)
    {
        var values = new Dictionary<int, int>();
        foreach (var l in run.levels)
            values[l.level] = (values.TryGetValue(l.level, out int v) ? v : 0) + Value(l, metric);
        return values;
    }

    // What makes up a level's value, for the readout.
    private static string Detail(PlaytestLevel l, int metric)
    {
        string detail;
        switch (metric)
        {
            case 0: detail = "  ·  " + l.coinsCollected + " picked up" + (l.friendlyGhostsCaught > 0 ? ", friendly ghost caught" : ""); break;
            case 1: detail = "  ·  " + l.purchases + " bought"; break;
            case 2: detail = "  ·  trap " + l.traps + ", freeze " + l.freezes + ", shield " + l.shields + ", teleport " + l.teleports; break;
            case 3: detail = "  ·  " + l.lavaDeaths + " hazard, " + l.enemyDeaths + " hunters"; break;
            default: detail = ""; break;
        }
        return detail + (l.joinedMidLevel ? "  ·  joined part way" : "");
    }

    private static string Clock(PlaytestReport r)
    {
        if (!DateTime.TryParse(r.startedAtUtc, Inv, DateTimeStyles.RoundtripKind, out var started)) return "?";
        var local = started.ToLocalTime();
        return local.ToString(local.Date == DateTime.Now.Date ? "HH:mm" : "d MMM HH:mm", Inv);
    }

    private static string Who(PlaytestReport r) =>
        r.botProfile == TestModeSession.HumanName ? "Player" : PlaytestBotProfile.DisplayName(r.botProfile) + " bot";
    private static string ShortWho(PlaytestReport r) => Who(r);

    private static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

    // ---- sprites, drawn once --------------------------------------------------------------

    private static Sprite _cap;
    private static Sprite _circle;

    // A column's top: 4 px rounded corners over a square foot, sliced so every
    // height keeps the same corners.
    private static Sprite RoundedCap()
    {
        if (_cap != null) return _cap;
        const int size = 16, r = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 1f;
                if (y >= size - r && (x < r || x >= size - r))
                {
                    var corner = new Vector2(x < r ? r : size - r, size - r);
                    a = Mathf.Clamp01(r + .5f - Vector2.Distance(new Vector2(x + .5f, y + .5f), corner));
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _cap = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, 0, r, r));
        return _cap;
    }

    private static Sprite Circle()
    {
        if (_circle != null) return _circle;
        const int size = 32;
        float r = size * .5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r)) + .5f)));
        tex.Apply();
        _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        return _circle;
    }
}
