using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Reuses the existing completion panel and button so progression stays wired.
public static class LevelCompletePopupBuilder
{
    [MenuItem("Tools/Build Level Complete Popup")]
    public static void Build()
    {
        var canvas = HudScene.FindCanvas();
        if (canvas == null) return;
        var manager = Object.FindAnyObjectByType<LevelManager>(FindObjectsInactive.Include);
        if (manager == null) return;
        var serialized = new SerializedObject(manager);
        var panel = (GameObject)serialized.FindProperty("levelCompletePanel").objectReferenceValue;
        var button = (Button)serialized.FindProperty("nextLevelButton").objectReferenceValue;
        if (panel == null || button == null) return;
        var card = (RectTransform)panel.transform.Find("Card");
        Undo.RegisterFullObjectHierarchyUndo(panel, "Style completion popup");
        const string dir = "Assets/UI/Icons/Completion/";
        var body = PanelArt.Panel(dir + "panel.png", 760, 640, new PanelArt.Style {
            radius = 46, pad = 24, fillTop = Hex(0x1E3265), fillBottom = Hex(0x101A3C),
            rim = Hex(0x6399E6), rimWidth = 2, glow = new Color(.22f,.5f,1f,.28f),
            glowSize = 20, sheen = new Color(.5f,.7f,1f,.12f), sheenHeight = 70
        });
        var action = PanelArt.Panel(dir + "next.png", 520, 110, new PanelArt.Style {
            radius = 36, pad = 10, fillTop = Hex(0x34EBE1), fillBottom = Hex(0x04AFC5),
            rim = Hex(0x8FFFF5), rimWidth = 2, glow = new Color(0,.9f,1,.25f),
            glowSize = 9, sheen = new Color(1,1,1,.35f), sheenHeight = 22
        });
        panel.GetComponent<Image>().color = new Color(.015f,.025f,.065f,.76f);
        Place(card, 0, 0, 760, 640);
        card.GetComponent<Image>().sprite = body;
        card.GetComponent<Image>().color = Color.white;
        if (card.GetComponent<UIFitToScreen>() == null) card.gameObject.AddComponent<UIFitToScreen>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/fonts/TMP/Fredoka-SemiBold SDF.asset");
        Label(card, "Eyebrow", "WELL PLAYED!", 0, 244, 600, 35, 22, Hex(0x8DC8FF), font);
        Picture(card, "CoinGlow", "Assets/UI/Icons/countdown_glow.png", 0, 138, 220, 220);
        Picture(card, "VictoryCoin", "Assets/UI/Icons/coin_icon.png", 0, 140, 124, 124);
        Label(card, "CongratsTitle", "LEVEL COMPLETE!", 0, 30, 680, 76, 52, Hex(0xFFE08A), font);
        Label(card, "SubText", "All coins collected!", 0, -40, 600, 44, 29, Hex(0xC2D3EE), font);
        Label(card, "SuccessNote", "A perfect sweep. On to the next adventure.", 0, -91, 650, 36, 21, Hex(0x8CA5CC), font);
        Place((RectTransform)button.transform, 0, -192, 520, 110);
        button.image.sprite = action;
        button.image.color = Color.white;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.85f,1,1);
        colors.pressedColor = new Color(.62f,.83f,.9f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        text.font = font;
        text.text = "Next Level  >";
        text.fontSize = 34;
        text.fontStyle = FontStyles.Normal;
        text.color = Hex(0x073850);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Place(text.rectTransform, 0, 2, 460, 65);
        panel.SetActive(false);
        HudScene.Save(canvas);
        Debug.Log("[Completion] Styled and saved; existing Next Level binding retained.");
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f,.5f);
        rt.anchoredPosition = new Vector2(x,y);
        rt.sizeDelta = new Vector2(w,h);
        rt.localScale = Vector3.one;
    }

    static GameObject Child(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Label(Transform root, string name, string value, float x, float y, float w, float h, float size, Color color, TMP_FontAsset font)
    {
        var go = Child(root,name);
        var label = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        Place(label.rectTransform,x,y,w,h);
        label.text = value;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyles.Normal;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    static void Picture(Transform root, string name, string path, float x, float y, float w, float h)
    {
        var go = Child(root,name);
        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        image.preserveAspect = true;
        image.raycastTarget = false;
        Place(image.rectTransform,x,y,w,h);
    }

    static Color Hex(uint value) => new Color((value >> 16 & 255)/255f, (value >> 8 & 255)/255f, (value & 255)/255f);
}
