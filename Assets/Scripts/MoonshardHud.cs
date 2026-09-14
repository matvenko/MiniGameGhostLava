using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The moonshard balance: the purple crystal_bar pill on the top row, just left
// of the ghost (lives) pill. Made at runtime so existing scene layouts and their
// prefab references do not need migration.
//
// The art is nine-sliced - the gem end and the round end stay whole and only the
// dark field between them stretches - so the pill is exactly as wide as the
// number in it needs, from "0" to a long hoard.
public sealed class MoonshardHud : MonoBehaviour
{
    // The painted part of the ghost pill beside it, not its whole rect, which
    // carries some air above and below.
    private const float Height = 64f;
    // The slice borders in crystal_bar.png's own pixels (see its import settings):
    // everything up to the end of the gem, and the round right end.
    private const float LeftCap = 426f, RightCap = 180f;
    private const float MinField = 56f, Gap = 14f;

    private static MoonshardHud instance;
    private RectTransform rect, ghosts;
    private Image image;
    private TextMeshProUGUI label;
    private float scale;

    // Hung on the canvas the coin wallet is on - the HUD's own. Any root canvas
    // will not do: the game makes others at runtime, and on one of those the pill
    // came out at the wrong scale with no ghost pill to line up against.
    public static void Create(EconomyManager wallet, Sprite art, TextMeshProUGUI walletText)
    {
        if (instance != null || art == null || walletText == null || walletText.canvas == null) return;
        Canvas canvas = walletText.canvas.rootCanvas;
        TMP_FontAsset font = walletText.font;

        var go = new GameObject("Moonshard balance", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<MoonshardHud>();
        instance.rect = (RectTransform)go.transform;
        instance.rect.anchorMin = instance.rect.anchorMax = instance.rect.pivot = new Vector2(1, 1);
        instance.scale = Height / art.rect.height;

        instance.image = go.AddComponent<Image>();
        instance.image.sprite = art;
        instance.image.type = Image.Type.Sliced;
        instance.image.raycastTarget = false;
        // Sliced borders are drawn at the sprite's own size unless told otherwise;
        // this makes them shrink with the pill, so the gem end stays in proportion.
        instance.image.pixelsPerUnitMultiplier = art.rect.height * canvas.referencePixelsPerUnit / art.pixelsPerUnit / Height;

        var text = new GameObject("Amount", typeof(RectTransform));
        text.transform.SetParent(go.transform, false);
        var textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(LeftCap * instance.scale, 0);
        textRect.offsetMax = new Vector2(-RightCap * instance.scale, 0);
        instance.label = text.AddComponent<TextMeshProUGUI>();
        if (font != null) instance.label.font = font;
        instance.label.fontSize = 32;
        instance.label.fontStyle = FontStyles.Bold;
        instance.label.alignment = TextAlignmentOptions.Center;
        instance.label.color = Color.white;
        instance.label.raycastTarget = false;

        instance.ghosts = canvas.transform.Find("Ghosts_Ui") as RectTransform;
        Refresh(wallet);
        instance.LateUpdate();
    }

    public static void Refresh(EconomyManager wallet)
    {
        if (instance == null || instance.label == null) return;
        instance.label.text = wallet.TotalMoonshards.ToString();
        float field = Mathf.Max(MinField, instance.label.preferredWidth + 16f);
        instance.rect.sizeDelta = new Vector2((LeftCap + RightCap) * instance.scale + field, Height);
    }

    // Follows the ghost pill rather than sitting at a fixed spot, so it stays
    // beside it whatever size that pill is, and hides whenever it is hidden -
    // the shop, for one, clears the top row while it is open.
    private void LateUpdate()
    {
        if (ghosts == null)
        {
            rect.anchoredPosition = new Vector2(-420, -18);
            return;
        }
        bool shown = ghosts.gameObject.activeInHierarchy;
        image.enabled = label.enabled = shown;
        float right = ghosts.anchoredPosition.x - ghosts.rect.width * ghosts.pivot.x - Gap;
        float middle = ghosts.anchoredPosition.y - ghosts.rect.height * (ghosts.pivot.y - .5f);
        rect.anchoredPosition = new Vector2(right, middle + Height * .5f);
    }

    private void OnDestroy() { if (instance == this) instance = null; }
}
