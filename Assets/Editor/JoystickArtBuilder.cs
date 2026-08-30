using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Draws the on-screen stick and rebuilds it in the open scene.
//
// The art is generated for the same reason the coin's is: a ring, four
// arrowheads and a round knob are a handful of distance tests, and generating
// them means the shapes are stated in figures that can be argued with rather
// than baked into a PNG somebody has to open Photoshop to change.
//
// The scene half turns the old fixed stick into a floating one. That needs a
// full-screen press area above the game and below the buttons, which is not
// something the stick can be on its own, so the hierarchy changes shape:
//
//   Canvas
//     JoystickArea   full-screen, invisible, first child - VirtualJoystick lives here
//       Joystick     the ring, moved to wherever the finger lands
//         Handle     the knob
//
// Follows GhoulEnemySetup's rule about scenes: this works on whatever is open
// and never discards it. Open each scene with a stick in it in turn and run this
// again for each. Running it twice on one scene is safe.
public static class JoystickArtBuilder
{
    private const string IconFolder = "Assets/UI/Icons";
    private const string BasePath = IconFolder + "/joystick_base.png";
    private const string KnobPath = IconFolder + "/joystick_knob.png";

    private const string AreaName = "JoystickArea";
    private const string StickName = "Joystick";
    private const string HandleName = "Handle";

    // In canvas units against the 1920-wide reference the Canvas scaler matches
    // on. 260 is about a seventh of the screen width - big enough for a thumb to
    // find the centre of without covering the board.
    private const float StickSize = 260f;
    private const float HandleSize = 100f;

    // Short of (StickSize - HandleSize) / 2, so the knob stops just inside the
    // ring instead of sitting on it.
    private const float HandleRange = 72f;
    private const float RestMargin = 230f;
    private const float IdleAlpha = 0.5f;

    [MenuItem("Tools/Build Joystick")]
    public static void Build()
    {
        var existing = Object.FindAnyObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
        var canvas = existing != null
            ? existing.GetComponentInParent<Canvas>()
            : Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

        if (canvas == null)
        {
            Debug.LogError("[Joystick] No Canvas in the open scene. Open a game scene first - " +
                           "this will not discard whatever you have open to do it.");
            return;
        }

        Directory.CreateDirectory(IconFolder);
        Sprite baseSprite = WriteSprite(BasePath, BuildBaseTexture(256));
        Sprite knobSprite = WriteSprite(KnobPath, BuildKnobTexture(160));

        RectTransform area = BuildArea(canvas);
        RectTransform stick = BuildStick(area, baseSprite, out CanvasGroup group);
        RectTransform handle = BuildHandle(stick, knobSprite);

        WireComponent(area, stick, handle, group);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Joystick] Rebuilt in {canvas.gameObject.scene.name}: floating stick resting {RestMargin} " +
                  "in from the right edge. Run this again in any other scene that has a stick.");
    }

    // ---- scene ------------------------------------------------------------

    private static RectTransform BuildArea(Canvas canvas)
    {
        Transform found = canvas.transform.Find(AreaName);
        GameObject go = found != null ? found.gameObject : new GameObject(AreaName, typeof(RectTransform));

        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        // First child, so every button and panel added after it wins the
        // raycast. Otherwise the press area would swallow taps meant for the
        // trap button and the pause menu.
        rt.SetAsFirstSibling();

        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        return rt;
    }

    private static RectTransform BuildStick(RectTransform area, Sprite sprite, out CanvasGroup group)
    {
        Transform found = area.Find(StickName);
        if (found == null)
        {
            // The old fixed stick is somewhere else under the Canvas; take it
            // rather than leaving a second one behind.
            Transform canvasChild = area.parent.Find(StickName);
            found = canvasChild;
        }

        GameObject go = found != null ? found.gameObject : new GameObject(StickName, typeof(RectTransform));

        var rt = (RectTransform)go.transform;
        rt.SetParent(area, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(StickSize, StickSize);
        rt.anchoredPosition = new Vector2(area.rect.width * 0.5f - RestMargin, 0f);

        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        // The press area below it already catches everything; leaving the stick
        // itself a raycast target only creates a second thing to hit.
        image.raycastTarget = false;

        group = go.GetComponent<CanvasGroup>();
        if (group == null) group = go.AddComponent<CanvasGroup>();
        group.alpha = IdleAlpha;
        group.blocksRaycasts = false;
        group.interactable = false;

        return rt;
    }

    private static RectTransform BuildHandle(RectTransform stick, Sprite sprite)
    {
        Transform found = stick.Find(HandleName);
        GameObject go = found != null ? found.gameObject : new GameObject(HandleName, typeof(RectTransform));

        var rt = (RectTransform)go.transform;
        rt.SetParent(stick, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(HandleSize, HandleSize);
        rt.anchoredPosition = Vector2.zero;

        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;

        return rt;
    }

    private static void WireComponent(RectTransform area, RectTransform stick, RectTransform handle, CanvasGroup group)
    {
        // The component has to end up on the press area, and a MonoBehaviour
        // cannot be moved between objects - so any copy left on the stick from
        // the old fixed layout is destroyed rather than rewired.
        var stale = stick.GetComponent<VirtualJoystick>();
        if (stale != null) Object.DestroyImmediate(stale);

        var joystick = area.GetComponent<VirtualJoystick>();
        if (joystick == null) joystick = area.gameObject.AddComponent<VirtualJoystick>();

        var so = new SerializedObject(joystick);
        so.FindProperty("stick").objectReferenceValue = stick;
        so.FindProperty("handle").objectReferenceValue = handle;
        so.FindProperty("stickGroup").objectReferenceValue = group;
        so.FindProperty("handleRange").floatValue = HandleRange;
        so.FindProperty("restMargin").floatValue = RestMargin;
        so.FindProperty("idleAlpha").floatValue = IdleAlpha;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- art --------------------------------------------------------------

    // The ring, with an arrowhead let into it at each quarter. The arrowheads
    // are what tell the player which way this thing steers before they have
    // touched it; a bare hoop could be anything.
    private static Color[] BuildBaseTexture(int size)
    {
        // Measured off the reference as fractions of the outer radius, which is
        // why they are not round numbers: a slimmer, shorter arrowhead than the
        // obvious one, so the four of them mark the axes without crowding the
        // knob or turning the ring into a cog.
        const float outer = 0.95f;
        const float inner = 0.90f;
        const float arrowBase = 0.115f; // half-width where the arrowhead meets the ring
        const float arrowTip = 0.74f;   // how far in the point reaches
        const float fillAlpha = 0.13f;

        return Render(size, p =>
        {
            float d = p.magnitude;
            if (d > outer) return 0f;
            if (d >= inner) return 0.9f;

            // Four arrowheads, one per axis. Testing both axes with the same
            // pair of numbers is what keeps them identical to each other.
            if (InArrow(p.y, p.x) || InArrow(-p.y, p.x) || InArrow(p.x, p.y) || InArrow(-p.x, p.y))
                return 0.9f;

            return fillAlpha;
        });

        bool InArrow(float along, float across)
        {
            if (along < arrowTip || along > inner) return false;
            float halfWidth = arrowBase * (along - arrowTip) / (inner - arrowTip);
            return Mathf.Abs(across) <= halfWidth;
        }
    }

    // The knob: a pale body inside a brighter rim, lit from the upper left. The
    // highlight is the only thing here that is not symmetrical, and it is what
    // stops the knob reading as a flat hole in the middle of the ring.
    private static Color[] BuildKnobTexture(int size)
    {
        const float body = 0.72f;
        const float rim = 0.84f;

        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var p = new Vector2((x + 0.5f - half) / half, (y + 0.5f - half) / half);
            float d = p.magnitude;

            float alpha;
            float shade;
            if (d > rim)
            {
                alpha = 0f;
                shade = 1f;
            }
            else if (d >= body)
            {
                alpha = 0.95f;
                shade = 1f;
            }
            else
            {
                alpha = 0.82f;
                // Falls off from a point up and to the left of centre, so the
                // knob reads as a dome rather than a disc.
                float lit = Mathf.Clamp01(1f - (p - new Vector2(-0.22f, 0.3f)).magnitude / 0.85f);
                shade = Mathf.Lerp(0.74f, 1f, lit * lit);
            }

            pixels[y * size + x] = new Color(shade, shade, shade, alpha);
        }

        SoftenEdges(pixels, size);
        return pixels;
    }

    // Coverage by supersampling rather than an analytic edge. The arrowheads
    // meet the ring at a shallow angle, where a distance-based smoothstep has no
    // single width that is right for both the straight sides and the point.
    private static Color[] Render(int size, System.Func<Vector2, float> alphaAt)
    {
        const int samples = 3;
        var pixels = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float total = 0f;
            for (int sy = 0; sy < samples; sy++)
            for (int sx = 0; sx < samples; sx++)
            {
                float px = (x + (sx + 0.5f) / samples - half) / half;
                float py = (y + (sy + 0.5f) / samples - half) / half;
                total += alphaAt(new Vector2(px, py));
            }
            pixels[y * size + x] = new Color(1f, 1f, 1f, total / (samples * samples));
        }

        return pixels;
    }

    // One box blur pass over alpha only, to take the stair-stepping off the
    // knob's rim without touching the shading underneath it.
    private static void SoftenEdges(Color[] pixels, int size)
    {
        var alpha = new float[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a;

        for (int y = 1; y < size - 1; y++)
        for (int x = 1; x < size - 1; x++)
        {
            float sum = 0f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                sum += alpha[(y + dy) * size + x + dx];
            pixels[y * size + x].a = sum / 9f;
        }
    }

    private static Sprite WriteSprite(string path, Color[] pixels)
    {
        int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
