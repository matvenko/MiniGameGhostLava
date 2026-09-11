using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Dresses the spawn countdown: the number and the disc it sits on.
//
// Both were placeholders. The number was drawing in LiberationSans - Unity's
// fallback, not a font this game uses anywhere else - and the disc was the
// editor's built-in Knob sprite, 64 pixels across, stretched to 230 and tinted
// almost black. Over a bright board that reads as a smudge with a number on it.
//
// So: the disc is drawn here at a size worth stretching, as a soft pool of
// shade with a warm ring around it - a spotlight on the board rather than a
// hole in it - and the number goes into Fredoka with the gold gradient, dark
// outline and drop shadow the rest of the game's lettering has.
//
// Safe to run twice: it rewrites its sprite and material, and re-points the
// same two objects at them.
public static class CountdownArtBuilder
{
    private const string ScenePath = "Assets/LavaScene.unity";
    private const string SpritePath = "Assets/UI/Icons/countdown_glow.png";
    private const string MaterialPath = "Assets/UI/fonts/TMP/Fredoka-SemiBold SDF - Countdown.mat";
    private const string FontPath = "Assets/UI/fonts/TMP/Fredoka-SemiBold SDF.asset";

    private const int Resolution = 512;
    private const float BackdropSize = 300f;
    private const float NumberSize = 210f;

    [MenuItem("Tools/Maze Boo/Build Countdown Art")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Leave play mode before rebuilding the countdown art.");
            return;
        }

        var controller = Object.FindAnyObjectByType<SpawnCountdownController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("No SpawnCountdownController in the open scene - open " + ScenePath + " first.");
            return;
        }

        var serialized = new SerializedObject(controller);
        var text = serialized.FindProperty("countdownText").objectReferenceValue as TextMeshProUGUI;
        var backdrop = serialized.FindProperty("countdownBackdrop").objectReferenceValue as Image;

        if (backdrop != null) DressBackdrop(backdrop);
        if (text != null) DressNumber(text);

        // The sprite carries its own falloff now, so the controller's fade can
        // take the whole thing rather than half of it.
        serialized.FindProperty("backdropMaxAlpha").floatValue = .92f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("Countdown art rebuilt.");
    }

    private static void DressBackdrop(Image backdrop)
    {
        backdrop.sprite = WriteGlowSprite();
        backdrop.type = Image.Type.Simple;
        backdrop.color = Color.white;
        backdrop.raycastTarget = false;
        var rect = (RectTransform)backdrop.transform;
        rect.sizeDelta = new Vector2(BackdropSize, BackdropSize);
    }

    // A pool of shade: dark and nearly solid in the middle, gone by the edge,
    // with one warm ring inside the falloff so the shape has an edge to read
    // rather than dissolving into the grass.
    private static Sprite WriteGlowSprite()
    {
        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
        var pixels = new Color[Resolution * Resolution];
        float centre = (Resolution - 1) * .5f;

        // Deep violet rather than black - the number carries its own contrast
        // now, so this only has to settle the board down under it.
        Color core = new Color(.11f, .07f, .21f);
        Color ring = new Color(1f, .80f, .38f);

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;

                // Smootherstep out to the rim, squared so the middle holds its
                // weight and only the last quarter thins away.
                float t = Mathf.Clamp01((d - .10f) / .90f);
                float fade = 1f - t * t * t * (t * (t * 6f - 15f) + 10f);
                float alpha = fade * fade * .42f;

                // One soft band, just inside the edge: the ring is what makes it
                // a light thrown on the board rather than a stain left on it.
                float band = Mathf.Exp(-((d - .76f) * (d - .76f)) / (2f * .05f * .05f));
                Color colour = Color.Lerp(core, ring, Mathf.Clamp01(band * 1.1f));
                alpha = Mathf.Clamp01(alpha + band * .5f);

                pixels[y * Resolution + x] = new Color(colour.r, colour.g, colour.b, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
        File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = Resolution / BackdropSize * 100f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    private static void DressNumber(TextMeshProUGUI text)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) text.font = font;

        text.fontSize = NumberSize;
        text.enableAutoSizing = false;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        // Gold on top, ember at the bottom - the same two colours the coins and
        // the play button are lit with.
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(
            new Color32(0xFF, 0xE8, 0x7A, 0xFF), new Color32(0xFF, 0xE8, 0x7A, 0xFF),
            new Color32(0xFF, 0x9A, 0x1F, 0xFF), new Color32(0xFF, 0x9A, 0x1F, 0xFF));

        text.fontSharedMaterial = WriteNumberMaterial(font != null ? font : text.font);

        var rect = (RectTransform)text.transform;
        rect.sizeDelta = new Vector2(520f, 380f);
    }

    // A preset of the font's material rather than an instance hidden inside the
    // scene, so the outline and the shadow can be seen and edited like any other
    // material - and so a second thing can wear the same lettering later.
    private static Material WriteNumberMaterial(TMP_FontAsset font)
    {
        var material = new Material(font.material) { name = Path.GetFileNameWithoutExtension(MaterialPath) };

        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, .22f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(.16f, .07f, .26f));
        material.SetFloat(ShaderUtilities.ID_FaceDilate, .08f);

        // Underlay: the shadow that lifts the number off the board.
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, .55f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .5f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.5f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, .1f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .35f);

        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
        {
            existing.CopyPropertiesFromMaterial(material);
            existing.shaderKeywords = material.shaderKeywords;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(material);
            AssetDatabase.SaveAssets();
            return existing;
        }

        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
