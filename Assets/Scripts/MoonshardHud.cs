using TMPro;
using UnityEngine;

// A small persistent balance under the existing wallet, made at runtime so
// existing scene layouts and their prefab references do not need migration.
public sealed class MoonshardHud : MonoBehaviour
{
    private static MoonshardHud instance;
    private TextMeshProUGUI label;

    public static void Create(EconomyManager wallet)
    {
        if (instance != null) return;
        Canvas canvas = null;
        foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (candidate.isRootCanvas && candidate.renderMode != RenderMode.WorldSpace)
            { canvas = candidate; break; }
        if (canvas == null) return;
        var go = new GameObject("Moonshard balance", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-18, -132);
        rect.sizeDelta = new Vector2(180, 42);
        instance = go.AddComponent<MoonshardHud>();
        instance.label = go.AddComponent<TextMeshProUGUI>();
        instance.label.fontSize = 24;
        instance.label.alignment = TextAlignmentOptions.Right;
        instance.label.color = new Color(.69f, .91f, 1f);
        Refresh(wallet);
    }

    public static void Refresh(EconomyManager wallet)
    {
        if (instance != null && instance.label != null)
            instance.label.text = "◆  " + wallet.TotalMoonshards;
    }

    private void OnDestroy() { if (instance == this) instance = null; }
}
