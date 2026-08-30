using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Puts a shadow under the coin.
//
// Not a cast one. DayNightCycle turns the sun through a full circle every two
// minutes, so a real shadow would swing right round the coin and vanish
// altogether at night - and a swinging shadow under a pickup reads as the
// pickup moving. This is the blob every top-down game uses instead: a soft dark
// disc lying on the ground directly beneath, which is what a light straight
// overhead would give and never changes.
//
// It is drawn wider than the coin on purpose. Seen from directly above - the
// angle this game is played at - a shadow the size of the coin is entirely
// hidden behind it. The overhang is the whole point: it shows as a soft dark
// halo that also sets the gold off against the grass.
//
// The blob goes on the prefab root rather than on the disc, because the disc is
// what Coin.cs tumbles; parented there, the shadow would tumble with it.
public static class CoinShadowBuilder
{
    private const string TexturePath = "Assets/Materials/T_CoinShadow.png";
    private const string MaterialPath = "Assets/Materials/M_CoinShadow.mat";
    private const string CoinPrefabPath = "Assets/Coin.prefab";
    private const string ShadowName = "Shadow";

    // How far the coin floats above the tile it sits on. Blocks are centred at
    // y = -0.08 and are a unit thick, so their top face is at 0.42; LevelManager
    // drops coins 0.83 above the tile transform, which lands them at 0.75.
    private const float DropToGround = 0.33f;
    private const float ZFightLift = 0.005f;

    // As a multiple of the coin's own width. It has to clear the coin by a good
    // margin, because the overhang is the only part of it that is ever seen from
    // straight above - at a hair over the coin's own size there is nothing to
    // look at. At the coin's current 0.40 this works out to a shadow 1.0 across,
    // one board tile.
    private const float ShadowSpread = 2.5f;

    // The starting strength only. The knob that matters lives on the material,
    // as the alpha of M_CoinShadow's Base Map colour, so it can be dragged in the
    // Inspector and seen immediately instead of being baked into a texture that
    // has to be regenerated to change. This is just what that alpha is set to the
    // first time the material is written.
    //
    // Kept light: the coin floats a third of a unit off the ground, and a shadow
    // much darker than this under something that close reads as a hole in the
    // board rather than as shade.
    private const float DefaultShadowStrength = 0.3f;

    // Where the blob stops holding full strength and starts to fade: exactly
    // where the coin's own rim falls on it. Everything inside that is hidden
    // under the coin, so a gradient that has already faded by the time it comes
    // out from under the coin is a gradient nobody sees. Derived rather than
    // written down, so it stays right whatever ShadowSpread is set to.
    private const float SolidTo = 1f / ShadowSpread;

    [MenuItem("Tools/Build Coin Shadow")]
    public static void Build()
    {
        Texture2D tex = WriteTexture(TexturePath, BuildBlob(128));
        Material mat = WriteMaterial(MaterialPath, tex);

        GameObject contents = PrefabUtility.LoadPrefabContents(CoinPrefabPath);
        try
        {
            // Read the coin's real width rather than assuming one, so this can be
            // re-run after the coin is resized and the blob follows.
            Transform disc = contents.transform.GetChild(0);
            float coinWidth = disc.localScale.x;

            Transform found = contents.transform.Find(ShadowName);
            GameObject go = found != null ? found.gameObject : new GameObject(ShadowName);
            go.transform.SetParent(contents.transform, false);

            // Unity's quad has its normal along -Z, so a quarter turn forward is
            // what lays it flat facing up. Turned the other way it faces into the
            // ground and culls away, which looks exactly like a shadow that was
            // never built.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localPosition = new Vector3(0f, -DropToGround + ZFightLift, 0f);
            float spread = coinWidth * ShadowSpread;
            go.transform.localScale = new Vector3(spread, spread, 1f);

            var filter = go.GetComponent<MeshFilter>();
            if (filter == null) filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // So the pickup animation can take the blob with it instead of
            // leaving a full-strength shadow under a coin that has gone.
            var coin = contents.GetComponentInChildren<Coin>(true);
            if (coin != null)
            {
                var so = new SerializedObject(coin);
                so.FindProperty("shadow").objectReferenceValue = go.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, CoinPrefabPath);
            Debug.Log($"[Coin] Shadow {spread:0.000} across under a coin {coinWidth:0.000} across, " +
                      $"sitting {DropToGround} below it. Re-run after resizing the coin.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
    }

    // The shape only, at full strength, in the alpha channel. How dark it
    // actually goes is the material's business, which is what puts that control
    // in the Inspector.
    //
    // Solid in the middle and falling away to nothing with no hard edge
    // anywhere: a hard-edged disc under a coin reads as a second object rather
    // than as shade.
    private static Color[] BuildBlob(int size)
    {
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            // Smoothstep across the outer band only, so it leaves full strength
            // gently and arrives at nothing gently; a linear ramp shows a visible
            // rim where it reaches zero.
            float t = Mathf.Clamp01((d - SolidTo) / (1f - SolidTo));
            float a = 1f - t * t * (3f - 2f * t);
            // White, so nothing depends on the RGB: the colour comes from the
            // material and only the coverage comes from here.
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        return pixels;
    }

    private static Material WriteMaterial(string path, Texture2D tex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
        // Black at partial coverage over the ground works out to the same thing
        // as multiplying the ground down - dst * (1 - a) either way - and this
        // way the strength is a number in the Inspector rather than a value
        // baked into the texture.
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            // Keep whatever strength has been dialled in, so re-running this to
            // pick up a resized coin does not throw away a tuned shadow.
            Color kept = existing.GetColor("_BaseColor");
            EditorUtility.CopySerialized(mat, existing);
            Object.DestroyImmediate(mat);
            existing.SetColor("_BaseColor", new Color(0f, 0f, 0f, kept.a));
            EditorUtility.SetDirty(existing);
            return existing;
        }

        mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, DefaultShadowStrength));
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Texture2D WriteTexture(string path, Color[] pixels)
    {
        int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
