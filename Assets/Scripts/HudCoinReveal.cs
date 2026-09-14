using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Lets the board show through the HUD. The camera sits close over the character
// and the HUD is drawn over the edges of the frame, so a coin that drifts under
// the coin counter or an ability button is simply gone - and on a board where
// every coin has to be taken, that reads as a coin that is not there.
//
// The character goes under it too: at the edge of the map the camera stops
// following and the character walks on into the corner of the frame.
//
// So each piece of the HUD watches the character and the coins and moonshards
// still on the board, and while one of them is under it, thins out to let it
// show through, then fills back in once it has moved on. Only the pieces actually over something
// fade - the ability bar button by button - and only their alpha changes: a
// faded button still takes a tap.
//
// Made at runtime, beside the moonshard pill, so the scene needs no new
// component.
public sealed class HudCoinReveal : MonoBehaviour
{
    // The top row by name, and then every button in the ability bar on its own.
    private static readonly string[] Pieces =
        { "Coins_Ui", "Wallet_Ui", "Level_Ui", "Ghosts_Ui", "Settings_Ui", "Moonshard balance" };
    private const string AbilityBar = "Abilities_Ui";

    private const float FadedAlpha = .35f;
    private const float FadeOutSeconds = .18f;
    private const float FadeInSeconds = .35f;
    // A coin's disc and a moonshard's stone both reach about this far from their
    // centre, in world units, with a little over for the shine.
    private const float ItemRadius = .3f;
    // The character's body as seen from above - wider than its collider, which
    // only covers the middle of it.
    private const float PlayerRadius = .45f;
    // Canvas units a faded piece keeps clear around itself before it fills back
    // in, so a coin right on its edge, with the camera easing over it, does not
    // flicker it in and out.
    private const float HoldMargin = 28f;

    private sealed class Piece
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float fade;
    }

    private static readonly Vector3[] Corners = new Vector3[4];

    private readonly List<Piece> pieces = new List<Piece>();
    // Each item on the board as it is on screen: x and y the centre, z the radius,
    // all in pixels.
    private readonly List<Vector3> items = new List<Vector3>();
    private Canvas canvas;
    private Camera boardCamera;
    private float nextCameraSearch;
    private CharacterController player;
    private float nextPlayerSearch;

    public static void Create(Canvas hud)
    {
        if (hud == null || hud.GetComponent<HudCoinReveal>() != null) return;
        var reveal = hud.gameObject.AddComponent<HudCoinReveal>();
        reveal.canvas = hud;
        foreach (var name in Pieces) reveal.Add(hud.transform.Find(name));
        var bar = hud.transform.Find(AbilityBar);
        if (bar == null) return;
        foreach (Transform button in bar)
            if (button.GetComponent<Button>() != null) reveal.Add(button);
    }

    private void Add(Transform piece)
    {
        if (!(piece is RectTransform rect)) return;
        var group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        pieces.Add(new Piece { rect = rect, group = group });
    }

    // Unscaled, so a piece caught half faded when the game stops - pause, shop,
    // the tour - still settles rather than hanging there.
    private void LateUpdate()
    {
        if (!FindCamera()) return;
        GatherItems();

        float margin = HoldMargin * canvas.scaleFactor;
        float dt = Time.unscaledDeltaTime;
        foreach (var piece in pieces)
        {
            if (piece.rect == null) continue;
            bool covering = piece.rect.gameObject.activeInHierarchy
                            && Covers(ScreenRect(piece.rect), piece.fade > 0f ? margin : 0f);
            piece.fade = Mathf.MoveTowards(piece.fade, covering ? 1f : 0f,
                dt / (covering ? FadeOutSeconds : FadeInSeconds));

            // Written only when it moves: a changed alpha rebuilds the canvas, and
            // most frames nothing is under anything.
            float alpha = Mathf.Lerp(1f, FadedAlpha, Mathf.SmoothStep(0f, 1f, piece.fade));
            if (!Mathf.Approximately(piece.group.alpha, alpha)) piece.group.alpha = alpha;
        }
    }

    private void GatherItems()
    {
        items.Clear();
        if (FindPlayer())
            AddItem(player.transform.TransformPoint(player.center), PlayerRadius);
        foreach (var coin in Coin.Active)
            if (coin != null) AddItem(coin.transform.position, ItemRadius);
        foreach (var shard in Moonshard.Active)
            if (shard != null) AddItem(shard.transform.position, ItemRadius);
    }

    private void AddItem(Vector3 world, float radius)
    {
        Vector3 centre = boardCamera.WorldToScreenPoint(world);
        if (centre.z <= 0f) return;
        Vector3 edge = boardCamera.WorldToScreenPoint(world + boardCamera.transform.right * radius);
        items.Add(new Vector3(centre.x, centre.y, Vector2.Distance(centre, edge)));
    }

    // Whether any item's circle, grown by the margin, reaches into the rect.
    private bool Covers(Rect rect, float margin)
    {
        foreach (var item in items)
        {
            float dx = Mathf.Max(rect.xMin - item.x, 0f, item.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - item.y, 0f, item.y - rect.yMax);
            float reach = item.z + margin;
            if (dx * dx + dy * dy < reach * reach) return true;
        }
        return false;
    }

    private Rect ScreenRect(RectTransform rect)
    {
        rect.GetWorldCorners(Corners);
        Camera ui = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(ui, Corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(ui, Corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // The camera the board is played through. No camera in the scene is tagged
    // main, so it is found by what follows the player - at most once a second
    // while missing.
    private bool FindCamera()
    {
        if (boardCamera != null) return true;
        if (Time.unscaledTime < nextCameraSearch) return false;
        nextCameraSearch = Time.unscaledTime + 1f;
        var follow = FindAnyObjectByType<CameraFollow>();
        if (follow != null) boardCamera = follow.GetComponent<Camera>();
        return boardCamera != null;
    }

    // Found the way the coins find it - the character controller named Ghost,
    // so the friendly ghost never counts - at most once a second while missing.
    private bool FindPlayer()
    {
        if (player != null) return true;
        if (Time.unscaledTime < nextPlayerSearch) return false;
        nextPlayerSearch = Time.unscaledTime + 1f;
        foreach (var ctrl in FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
        {
            if (ctrl.gameObject.name != "Ghost") continue;
            player = ctrl;
            return true;
        }
        return false;
    }
}
