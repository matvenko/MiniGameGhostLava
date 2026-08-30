using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// The scene half of the HUD builders: find the canvas, put a drawn panel on it,
// hang a live value in the middle of it, save.
//
// Every one of these readouts is the same two objects - one Image showing a
// piece of art, one TMP label standing in for the value that was painted out of
// it - so the parts that never differ live here and each builder is left saying
// only what is particular to its own panel: which art, which field, where it
// sits and how big the number is.
//
// Nothing here creates a panel from scratch when one is already in the scene.
// The readouts are wired to LevelManager, RewardSystem and ShopUIController by
// reference, and a fresh GameObject with the right name is not the object those
// references point at.
internal static class HudScene
{
    public static Canvas FindCanvas()
    {
        // Everything below builds into the open scene and saves it, and neither
        // of those means anything while the game is running: the changes go into
        // the copy Unity is playing and are thrown away when it stops.
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Hud] Stop play mode before running a HUD builder - anything it did to the " +
                           "scene while the game is running would be discarded when you stop.");
            return null;
        }

        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            if (c.transform.parent == null) return c;

        Debug.LogError("[Hud] No Canvas in the open scene. Open a game scene first - " +
                       "this will not discard whatever you have open to do it.");
        return null;
    }

    // The panel itself. The caller places it; everything else about it is the
    // same for every readout.
    public static RectTransform Panel(Canvas canvas, string name, Sprite sprite)
    {
        Transform found = canvas.transform.Find(name);
        GameObject go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));

        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);

        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = true;
        // The joystick's press area lies under the whole HUD; a readout left as a
        // raycast target would put a dead patch for the thumb under it.
        image.raycastTarget = false;

        return rt;
    }

    // The live value, anchored in fractions of the panel rather than in units,
    // so it stays on the spot the artist painted its stand-in whatever size the
    // panel is given.
    public static TextMeshProUGUI Value(RectTransform root, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform found = root.Find(name);
        GameObject go = found != null
            ? found.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));

        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return tmp;
    }

    // Children the drawing has made redundant.
    public static void Remove(RectTransform root, params string[] names)
    {
        foreach (string name in names)
        {
            Transform stale = root.Find(name);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);
        }
    }

    // Adds an object to the list ShopUIController switches off while the shop is
    // up, so it does not float over the shop's own backdrop.
    public static void HideWithHud(GameObject go)
    {
        var shop = Object.FindAnyObjectByType<ShopUIController>(FindObjectsInactive.Include);
        if (shop == null) return;

        var so = new SerializedObject(shop);
        SerializedProperty hidden = so.FindProperty("hudElementsToHide");

        // Anything a builder has since deleted leaves a hole in this list. Tidied
        // up on the way past rather than left to accumulate.
        bool already = false;
        int write = 0;
        for (int read = 0; read < hidden.arraySize; read++)
        {
            Object entry = hidden.GetArrayElementAtIndex(read).objectReferenceValue;
            if (entry == null) continue;
            if (entry == go) already = true;
            hidden.GetArrayElementAtIndex(write++).objectReferenceValue = entry;
        }

        hidden.arraySize = already ? write : write + 1;
        if (!already) hidden.GetArrayElementAtIndex(write).objectReferenceValue = go;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Save(Canvas canvas)
    {
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        AssetDatabase.SaveAssets();
    }
}
