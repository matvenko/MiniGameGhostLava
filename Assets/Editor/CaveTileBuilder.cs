using System.IO;
using UnityEditor;
using UnityEngine;

// Draws the cave floor the same way the meadow floor was drawn: two painted
// tiles, a cap and a side, that the block shader lays on every walkable cube.
//
// Painted rather than photographed. The board is a toy - flat colour, hard
// edges, no grain to speak of - and a photoreal rock would read as a hole cut
// in it. So the cap is a field of basalt plates with dark joints between them,
// a few of the joints still warm from whatever is underneath, and the side is
// the same rock in strata, darker as it goes down.
//
// Both tiles wrap: every cell of the board carries a copy of them, edge to
// edge, so a seam that does not meet is a seam repeated the length of the
// board. The plate cells and the noise are therefore sampled on a torus.
public static class CaveTileBuilder
{
    private const string OutDir = "Assets/Materials/Themes/";
    private const int Size = 512;

    private static readonly Color Joint = new Color(.075f, .07f, .095f);
    private static readonly Color PlateDark = new Color(.185f, .18f, .215f);
    private static readonly Color PlateLight = new Color(.315f, .30f, .355f);
    private static readonly Color Ember = new Color(1f, .42f, .10f);

    [MenuItem("Tools/Pac Ghost/Build Cave Tiles")]
    public static void Build()
    {
        Directory.CreateDirectory(OutDir);
        var top = Write("T_CaveTop", Cap());
        var side = Write("T_CaveSide", Side());

        // The cave floor is the meadow floor with different art on it: same
        // shader, same shading setup, so the tiles keep their painted edges and
        // their grounding shadow.
        const string source = "Assets/Materials/M_BlockGrass.mat";
        const string target = OutDir + "M_BlockCaveRock.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(target) == null)
            AssetDatabase.CopyAsset(source, target);
        var material = AssetDatabase.LoadAssetAtPath<Material>(target);
        if (material == null)
        {
            Debug.LogError("Could not create " + target);
            return;
        }
        material.SetTexture("_TopMap", top);
        material.SetTexture("_SideMap", side);
        // Lit from a ceiling that isn't there and from the lava that is.
        material.SetColor("_AmbientColor", new Color(.34f, .33f, .40f));
        material.SetColor("_AmbientGround", new Color(.42f, .20f, .10f));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Cave tiles built into " + OutDir);
    }

    // ---- the cap -----------------------------------------------------------

    private static Color[] Cap()
    {
        var random = new System.Random(9051);
        int cells = 22;
        var points = new Vector2[cells];
        var shade = new float[cells];
        var hot = new bool[cells];
        for (int i = 0; i < cells; i++)
        {
            points[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            shade[i] = (float)random.NextDouble();
            // A fifth of the joints are still warm. More than that and the floor
            // starts to look like the lava rather than the rock beside it.
            hot[i] = random.NextDouble() < .2;
        }

        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + .5f) / Size, (y + .5f) / Size);
                int nearest = 0, second = 0;
                float best = 9f, next = 9f;
                for (int i = 0; i < cells; i++)
                {
                    float d = TorusDistance(p, points[i]);
                    if (d < best) { next = best; second = nearest; best = d; nearest = i; }
                    else if (d < next) { next = d; second = i; }
                }

                // Distance to the joint rather than to the seed, so every plate
                // is outlined evenly however far apart the seeds happen to be.
                float edge = next - best;
                float plate = Mathf.Lerp(.42f, .58f, shade[nearest]);
                Color colour = Color.Lerp(PlateDark, PlateLight, shade[nearest]);
                colour *= .94f + .12f * Noise(p * 26f);

                float joint = Mathf.SmoothStep(1f, 0f, edge / .035f);
                colour = Color.Lerp(colour, Joint, joint * .9f);
                if (hot[nearest] || hot[second])
                {
                    float glow = Mathf.SmoothStep(1f, 0f, edge / .022f);
                    colour = Color.Lerp(colour, Ember, glow * .75f);
                }

                // A flat highlight along the top-left of each plate, the way the
                // rest of the board's art is lit.
                colour *= .96f + .08f * plate;
                pixels[y * Size + x] = new Color(colour.r, colour.g, colour.b, 1f);
            }
        return pixels;
    }

    // ---- the side ----------------------------------------------------------

    private static Color[] Side()
    {
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + .5f) / Size, (y + .5f) / Size);

                // Strata: horizontal bands with a wobble, so the cut face reads
                // as layers of rock rather than as a wall.
                float band = p.y * 7f + Noise(new Vector2(p.x * 3f, p.y * 3f)) * .6f;
                float step = Mathf.Floor(band);
                float within = band - step;
                float layer = Mathf.Repeat(step * .37f, 1f);

                Color colour = Color.Lerp(PlateDark, PlateLight, layer * .8f);
                colour *= Mathf.Lerp(.72f, 1f, p.y);            // darker toward the bottom
                colour *= .95f + .1f * Noise(p * 34f);
                colour = Color.Lerp(colour, Joint, Mathf.SmoothStep(1f, 0f, within / .12f) * .8f);

                pixels[y * Size + x] = new Color(colour.r, colour.g, colour.b, 1f);
            }
        return pixels;
    }

    // ---- helpers -----------------------------------------------------------

    // Both axes wrap, so a point near one edge is a neighbour of the far edge.
    private static float TorusDistance(Vector2 a, Vector2 b)
    {
        float dx = Mathf.Abs(a.x - b.x); if (dx > .5f) dx = 1f - dx;
        float dy = Mathf.Abs(a.y - b.y); if (dy > .5f) dy = 1f - dy;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // Value noise on the same torus: sines of the wrapped angle, which repeat
    // exactly once across the tile.
    private static float Noise(Vector2 p)
    {
        float a = Mathf.Sin(p.x * 12.9898f) * Mathf.Cos(p.y * 78.233f);
        float b = Mathf.Sin((p.x + p.y) * 43.1234f);
        return Mathf.Repeat((a + b) * .5f + 1f, 1f);
    }

    private static Texture2D Write(string name, Color[] pixels)
    {
        string path = OutDir + name + ".png";
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
