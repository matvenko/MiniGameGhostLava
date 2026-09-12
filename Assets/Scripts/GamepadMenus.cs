using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Walks the menus with a controller.
//
// Every popup that can hold the screen registers here: its root, how high it sits
// over the others, which button it starts on, and what B does in it. The topmost
// one that is open is the menu, and the controller only ever moves about inside
// it - the stick and the D-pad step from button to button, A presses the one with
// the ring round it, B goes back.
//
// The walls are the point. Unity's own navigation would happily step off the pause
// card onto the HUD buttons sitting under its backdrop, or off the shop onto the
// test bar; searching only the open menu's own buttons means the ring can't go
// anywhere a finger couldn't.
//
// None of it touches a player on a phone. A menu only picks a button, and the ring
// is only drawn, once the controller has been used; a tap puts it away again (see
// Pad.InUse).
public class GamepadMenus : MonoBehaviour
{
    private sealed class Menu
    {
        public GameObject root;
        public int priority;
        public Func<Selectable> first;
        public Action back;
        public GameObject last;
    }

    private static readonly List<Menu> Menus = new List<Menu>();

    // The last frame a menu had the controller. The ability bar reads the same face
    // buttons on the board and leaves this frame alone - otherwise the A that
    // pressed Resume would drop a trap on the way out.
    public static int BusyFrame { get; private set; } = -1;

    // How long a menu that has just come up ignores A and B, so a button being
    // hammered on the board - a trap going down as the last coin is picked up -
    // doesn't press through a card the player hasn't seen yet.
    private const float Grace = .3f;
    // How far the stick has to go to count as a step, and how a held one repeats.
    private const float StepThreshold = .5f;
    private const float FirstRepeat = .4f;
    private const float Repeat = .13f;

    private static readonly Color RingColour = new Color(1f, .83f, .24f);
    private static readonly Vector3[] Corners = new Vector3[4];

    private Menu _top;
    private float _openedAt;
    private Vector2 _heldStep;
    private float _nextStep;
    private Vector3 _lastMouse;
    private RectTransform _ring;
    private Image[] _ringEdges;

    // A popup's root going away - its scene unloading - is what forgets it, so
    // nothing has to remember to say goodbye.
    public static void Register(GameObject root, int priority, Func<Selectable> first, Action back = null)
    {
        Menus.Add(new Menu { root = root, priority = priority, first = first, back = back });
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Menus.Clear();
        BusyFrame = -1;
        Pad.InUse = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("Gamepad Menus");
        DontDestroyOnLoad(go);
        go.AddComponent<GamepadMenus>();
    }

    void Awake()
    {
        BuildRing();
    }

    void Update()
    {
        bool woke = NoticeDevice();

        Menu top = TopMenu();
        if (top != _top)
        {
            _top = top;
            _openedAt = Time.unscaledTime;
            _heldStep = Vector2.zero;
        }

        var events = EventSystem.current;
        if (top == null || events == null)
        {
            ShowRing(null);
            return;
        }
        BusyFrame = Time.frameCount;

        GameObject selected = events.currentSelectedGameObject;
        if (Pad.InUse && !IsIn(selected, top))
        {
            selected = Pick(top);
            events.SetSelectedGameObject(selected);
        }
        if (IsIn(selected, top)) top.last = selected;

        // The press that picked the controller up only brings the ring out; it
        // isn't also taken as a choice on a card the player may not have looked at.
        if (Pad.InUse && !woke)
        {
            Step(events, top);
            Press(events, top);
        }
        ShowRing(Pad.InUse ? events.currentSelectedGameObject : null);
    }

    // True on the frame the controller is picked up.
    private bool NoticeDevice()
    {
        Vector3 mouse = Input.mousePosition;
        bool pointer = Input.GetMouseButtonDown(0) || Input.touchCount > 0 || (mouse - _lastMouse).sqrMagnitude > 64f;
        _lastMouse = mouse;

        if (Pad.Touched())
        {
            if (Pad.InUse) return false;
            Pad.InUse = true;
            return true;
        }
        if (pointer) Pad.InUse = false;
        return false;
    }

    private static Menu TopMenu()
    {
        Menu top = null;
        for (int i = Menus.Count - 1; i >= 0; i--)
        {
            var menu = Menus[i];
            if (menu.root == null) { Menus.RemoveAt(i); continue; }
            if (!menu.root.activeInHierarchy) continue;
            if (top == null || menu.priority > top.priority) top = menu;
        }
        return top;
    }

    private static bool IsIn(GameObject go, Menu menu) =>
        go != null && go.activeInHierarchy && go.transform.IsChildOf(menu.root.transform);

    private static bool Usable(GameObject go, Menu menu)
    {
        if (!IsIn(go, menu)) return false;
        var selectable = go.GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None;
    }

    // Where the ring goes when a menu comes up: back where it was if the player has
    // been here before, otherwise the menu's own first choice, otherwise anything
    // in it that can be pressed.
    private static GameObject Pick(Menu menu)
    {
        if (Usable(menu.last, menu)) return menu.last;
        var first = menu.first != null ? menu.first() : null;
        if (first != null && Usable(first.gameObject, menu)) return first.gameObject;
        foreach (var selectable in Selectable.allSelectablesArray)
            if (Usable(selectable.gameObject, menu)) return selectable.gameObject;
        return null;
    }

    private void Step(EventSystem events, Menu menu)
    {
        Vector2 stick = Pad.Move;
        Vector2 step = Vector2.zero;
        if (stick.magnitude >= StepThreshold)
            step = Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2(Mathf.Sign(stick.x), 0f) : new Vector2(0f, Mathf.Sign(stick.y));
        if (step == Vector2.zero)
        {
            _heldStep = Vector2.zero;
            return;
        }

        float now = Time.unscaledTime;
        if (step == _heldStep && now < _nextStep) return;
        _nextStep = now + (step == _heldStep ? Repeat : FirstRepeat);
        _heldStep = step;

        var to = Neighbour(events.currentSelectedGameObject, step, menu);
        if (to != null) events.SetSelectedGameObject(to.gameObject);
    }

    // The nearest button that way, counting only this menu's. Scored the way
    // Unity's own navigation scores them - how squarely in line over how far - so
    // it steps where a player would expect, and never more than a wide cone off to
    // the side.
    private static Selectable Neighbour(GameObject from, Vector2 step, Menu menu)
    {
        if (from == null || !(from.transform is RectTransform fromRect)) return null;
        Vector2 origin = ScreenCentre(fromRect);
        Selectable best = null;
        float bestScore = 0f;
        foreach (var candidate in Selectable.allSelectablesArray)
        {
            if (candidate.gameObject == from || !Usable(candidate.gameObject, menu)) continue;
            Vector2 to = ScreenCentre((RectTransform)candidate.transform) - origin;
            float along = Vector2.Dot(step, to);
            if (along <= to.magnitude * .2f) continue;
            float score = along / to.sqrMagnitude;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private void Press(EventSystem events, Menu menu)
    {
        if (Time.unscaledTime - _openedAt < Grace) return;
        if (Pad.Pressed(PadButton.South))
        {
            var selected = events.currentSelectedGameObject;
            if (IsIn(selected, menu))
                ExecuteEvents.Execute(selected, new BaseEventData(events), ExecuteEvents.submitHandler);
        }
        else if (Pad.Pressed(PadButton.East) && menu.back != null)
        {
            menu.back();
        }
    }

    // ---- the ring --------------------------------------------------------------

    // Drawn over everything rather than left to each button's own Selected tint:
    // the buttons were drawn for fingers, and most of them tint to white or not at
    // all, which says nothing about where the controller is.
    private void BuildRing()
    {
        var holder = new GameObject("Focus ring", typeof(RectTransform), typeof(Canvas));
        holder.transform.SetParent(transform, false);
        var canvas = holder.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        _ring = new GameObject("Ring", typeof(RectTransform)).GetComponent<RectTransform>();
        _ring.SetParent(holder.transform, false);
        _ring.anchorMin = _ring.anchorMax = _ring.pivot = Vector2.zero;
        _ringEdges = new Image[4];
        for (int i = 0; i < _ringEdges.Length; i++)
        {
            var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            edge.transform.SetParent(_ring, false);
            edge.raycastTarget = false;
            _ringEdges[i] = edge;
        }
        _ring.gameObject.SetActive(false);
    }

    private void ShowRing(GameObject target)
    {
        bool show = target != null && target.activeInHierarchy && target.transform is RectTransform;
        if (_ring.gameObject.activeSelf != show) _ring.gameObject.SetActive(show);
        if (!show) return;

        ScreenRect((RectTransform)target.transform, out Vector2 min, out Vector2 max);
        float unit = Mathf.Max(1f, Screen.height / 1080f);
        float gap = 6f * unit, width = 4f * unit;
        _ring.anchoredPosition = min - Vector2.one * gap;
        _ring.sizeDelta = max - min + Vector2.one * gap * 2f;

        Place(_ringEdges[0], new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -width), Vector2.zero);
        Place(_ringEdges[1], new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, width));
        Place(_ringEdges[2], new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(width, 0));
        Place(_ringEdges[3], new Vector2(1, 0), new Vector2(1, 1), new Vector2(-width, 0), Vector2.zero);

        var colour = RingColour;
        colour.a = .7f + .3f * Mathf.Sin(Time.unscaledTime * 6f);
        foreach (var edge in _ringEdges) edge.color = colour;
    }

    private static void Place(Image edge, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = edge.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Vector2 ScreenCentre(RectTransform rect)
    {
        ScreenRect(rect, out Vector2 min, out Vector2 max);
        return (min + max) * .5f;
    }

    // Where a piece of UI is on the screen, in pixels, whatever canvas it is on.
    private static void ScreenRect(RectTransform rect, out Vector2 min, out Vector2 max)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera : null;
        rect.GetWorldCorners(Corners);
        min = max = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
        for (int i = 1; i < Corners.Length; i++)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
    }
}
