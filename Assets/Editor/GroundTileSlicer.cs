using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Cuts ground2.png - the reference sheet of painted ground pieces - into the
// individual assets the board is actually built from.
//
// The sheet has no alpha: the "transparent" areas are a light grey/white
// checkerboard painted into the RGB. So the pieces are found by segmenting
// against that checkerboard rather than against alpha, with a flood fill that
// yields one component per painted piece.
//
// Two outputs come out of one pass:
//   1. The sheet is re-imported as a Multiple sprite with a rect per piece, so
//      every shape on the sheet exists as a named sub-sprite in the project.
//   2. The square pieces - the ones that fit a 1x1 board cell - are cropped out
//      to their own textures and stacked into a Texture2DArray, which is what
//      the ground shader samples. An array rather than an atlas because slices
//      cannot bleed into each other under mipmapping or bilinear filtering,
//      and the board picks a different slice for every cell.
//
// Slice order is by greenness, greenest first. That is not cosmetic: the ground
// mesh maps a cell's distance from the water straight onto the slice index, so
// the sheet's own grass-to-dirt run becomes the shoreline gradient.
public static class GroundTileSlicer
{
    private const string SheetPath = "Assets/ground2.png";
    private const string TileFolder = "Assets/Textures/GroundTiles";
    private const string ArrayPath = "Assets/Textures/T_GroundTileArray.asset";

    private const int SliceSize = 256;

    // Fraction trimmed off each side after the ragged silhouette is filled in.
    // The pieces are drawn with torn grass edges; on a solid board those edges
    // would tile as a visible fringe, so the outer rim is dropped and only
    // genuine painted interior is kept. A smaller trim was tried first and the
    // dark outline of the torn edge still landed inside the slice, drawing a
    // grid of dark lines across the board wherever two cells met.
    private const float InsetFraction = 0.07f;

    // A piece counts as a board tile when its bounding box is square and it
    // actually fills that box - the L and T shapes on the sheet also have
    // near-square boxes, and only the fill test separates them.
    private const float MinSquareRatio = 0.88f;
    private const float MaxSquareRatio = 1.15f;
    private const float MinFill = 0.88f;

    private const int MinPieceArea = 3000;

    [MenuItem("Tools/Ground/Slice ground2.png Into Tiles")]
    public static void Slice()
    {
        var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("GroundTileSlicer: " + SheetPath + " not found.");
            return;
        }

        // Reading the sheet needs raw, unresized, uncompressed pixels. The
        // sheet is 1536x1024 - non power of two - so any rescale on import
        // would move every rect found below off the pixels it was measured on.
        if (!importer.isReadable
            || importer.npotScale != TextureImporterNPOTScale.None
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        int w = sheet.width, h = sheet.height;
        var pixels = sheet.GetPixels32();

        var pieces = FindPieces(pixels, w, h);
        if (pieces.Count == 0)
        {
            Debug.LogError("GroundTileSlicer: no pieces found on the sheet.");
            return;
        }

        WriteSpriteSheet(importer, pieces);

        var tiles = new List<Piece>();
        foreach (var p in pieces)
        {
            float ratio = (float)p.Rect.width / p.Rect.height;
            if (ratio < MinSquareRatio || ratio > MaxSquareRatio) continue;
            if (p.Fill < MinFill) continue;
            tiles.Add(p);
        }

        if (tiles.Count == 0)
        {
            Debug.LogError("GroundTileSlicer: found " + pieces.Count + " pieces but none were square cell tiles.");
            return;
        }

        // Greenest first, so slice index reads as "how much grass is left".
        tiles.Sort((a, b) => b.Greenness.CompareTo(a.Greenness));

        Directory.CreateDirectory(TileFolder);

        var slices = new List<Color32[]>(tiles.Count);
        var log = new StringBuilder();
        log.AppendLine("GroundTileSlicer: " + pieces.Count + " pieces on the sheet, " + tiles.Count + " usable as cell tiles.");

        for (int i = 0; i < tiles.Count; i++)
        {
            var data = ExtractTile(pixels, w, h, tiles[i].Rect, tiles[i].Mask);
            slices.Add(data);

            string path = TileFolder + "/T_GroundTile_" + i.ToString("00") + ".png";
            WritePng(data, path);

            log.AppendLine(string.Format("  slice {0,2}  src=({1},{2},{3}x{4})  greenness={5:0.0}",
                i, tiles[i].Rect.x, tiles[i].Rect.y, tiles[i].Rect.width, tiles[i].Rect.height, tiles[i].Greenness));
        }

        AssetDatabase.Refresh();
        for (int i = 0; i < tiles.Count; i++) ConfigureTileImporter(i);

        BuildArray(slices);

        Debug.Log(log.ToString());
    }

    private struct Piece
    {
        public RectInt Rect;
        public bool[] Mask;      // foreground mask, Rect-sized, row-major bottom-up
        public float Fill;
        public float Greenness;
    }

    // The sheet's background is the painted checkerboard: bright and neutral.
    // Painted ground is either green or brown, so both are far off neutral or
    // far off bright, and neither test alone would separate the pale dirt.
    private static bool IsBackground(Color32 p)
    {
        int max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
        int min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
        return max > 225 && (max - min) < 14;
    }

    private static List<Piece> FindPieces(Color32[] pixels, int w, int h)
    {
        var foreground = new bool[w * h];
        for (int i = 0; i < pixels.Length; i++) foreground[i] = !IsBackground(pixels[i]);

        var visited = new bool[w * h];
        var stack = new Stack<int>();
        var found = new List<Piece>();
        var members = new List<int>();

        for (int start = 0; start < w * h; start++)
        {
            if (visited[start] || !foreground[start]) continue;

            members.Clear();
            stack.Push(start);
            visited[start] = true;

            int minX = w, minY = h, maxX = -1, maxY = -1;
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                members.Add(p);
                int x = p % w, y = p / w;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int q = ny * w + nx;
                    if (visited[q] || !foreground[q]) continue;
                    visited[q] = true;
                    stack.Push(q);
                }
            }

            if (members.Count < MinPieceArea) continue;

            var rect = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            var mask = new bool[rect.width * rect.height];
            double rSum = 0, gSum = 0, bSum = 0;
            foreach (int p in members)
            {
                int x = p % w, y = p / w;
                mask[(y - minY) * rect.width + (x - minX)] = true;
                rSum += pixels[p].r;
                gSum += pixels[p].g;
                bSum += pixels[p].b;
            }

            found.Add(new Piece
            {
                Rect = rect,
                Mask = mask,
                Fill = members.Count / (float)(rect.width * rect.height),
                // Green minus red separates painted grass from painted soil
                // cleanly; the blue term only breaks ties between two tiles
                // that carry the same amount of grass.
                Greenness = (float)((gSum - rSum) / members.Count + (gSum - bSum) / members.Count * 0.15),
            });
        }

        // Reading order over the sheet, so the sub-sprite names line up with
        // how the sheet looks in an image viewer.
        found.Sort((a, b) =>
        {
            if (Mathf.Abs(a.Rect.yMax - b.Rect.yMax) > 40) return b.Rect.yMax.CompareTo(a.Rect.yMax);
            return a.Rect.x.CompareTo(b.Rect.x);
        });
        return found;
    }

    // Crops one piece to a square slice. The torn silhouette is filled first by
    // pushing the nearest painted colour outward into the checkerboard, so the
    // notches around the edge carry grass instead of white; then the outer rim
    // is trimmed off entirely and what is left is resampled to SliceSize.
    private static Color32[] ExtractTile(Color32[] pixels, int w, int h, RectInt rect, bool[] mask)
    {
        int rw = rect.width, rh = rect.height;
        var crop = new Color32[rw * rh];
        var filled = new bool[rw * rh];
        var queue = new Queue<int>();

        for (int y = 0; y < rh; y++)
        for (int x = 0; x < rw; x++)
        {
            int local = y * rw + x;
            if (!mask[local]) continue;
            crop[local] = pixels[(rect.y + y) * w + rect.x + x];
            filled[local] = true;
            queue.Enqueue(local);
        }

        // Multi-source BFS: every unpainted pixel takes the colour of the
        // painted pixel nearest to it.
        while (queue.Count > 0)
        {
            int p = queue.Dequeue();
            int x = p % rw, y = p / rw;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= rw || ny >= rh) continue;
                int q = ny * rw + nx;
                if (filled[q]) continue;
                filled[q] = true;
                crop[q] = crop[p];
                queue.Enqueue(q);
            }
        }

        int insetX = Mathf.RoundToInt(rw * InsetFraction);
        int insetY = Mathf.RoundToInt(rh * InsetFraction);
        int innerW = Mathf.Max(1, rw - insetX * 2);
        int innerH = Mathf.Max(1, rh - insetY * 2);

        var outPixels = new Color32[SliceSize * SliceSize];
        for (int y = 0; y < SliceSize; y++)
        for (int x = 0; x < SliceSize; x++)
        {
            // Box filter rather than a point sample: the pieces are ~180px and
            // the slice is 256, but the sheet's leaf work is high frequency and
            // point sampling it crawls once the camera moves.
            float u0 = x / (float)SliceSize, u1 = (x + 1) / (float)SliceSize;
            float v0 = y / (float)SliceSize, v1 = (y + 1) / (float)SliceSize;

            int sx0 = insetX + Mathf.FloorToInt(u0 * innerW);
            int sx1 = Mathf.Max(sx0 + 1, insetX + Mathf.CeilToInt(u1 * innerW));
            int sy0 = insetY + Mathf.FloorToInt(v0 * innerH);
            int sy1 = Mathf.Max(sy0 + 1, insetY + Mathf.CeilToInt(v1 * innerH));
            sx1 = Mathf.Min(sx1, rw);
            sy1 = Mathf.Min(sy1, rh);

            int r = 0, g = 0, b = 0, n = 0;
            for (int sy = sy0; sy < sy1; sy++)
            for (int sx = sx0; sx < sx1; sx++)
            {
                var c = crop[sy * rw + sx];
                r += c.r; g += c.g; b += c.b; n++;
            }
            if (n == 0) n = 1;
            outPixels[y * SliceSize + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
        }

        return outPixels;
    }

    private static void WritePng(Color32[] data, string path)
    {
        var tex = new Texture2D(SliceSize, SliceSize, TextureFormat.RGBA32, false);
        tex.SetPixels32(data);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void ConfigureTileImporter(int index)
    {
        string path = TileFolder + "/T_GroundTile_" + index.ToString("00") + ".png";
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType = TextureImporterType.Default;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipmapEnabled = true;
        ti.SaveAndReimport();
    }

    private static void WriteSpriteSheet(TextureImporter importer, List<Piece> pieces)
    {
        var meta = new SpriteMetaData[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            meta[i] = new SpriteMetaData
            {
                name = "ground2_" + i.ToString("00"),
                rect = new Rect(pieces[i].Rect.x, pieces[i].Rect.y, pieces[i].Rect.width, pieces[i].Rect.height),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            };
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 0618
        importer.spritesheet = meta;
#pragma warning restore 0618
        importer.isReadable = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void BuildArray(List<Color32[]> slices)
    {
        var array = new Texture2DArray(SliceSize, SliceSize, slices.Count, TextureFormat.RGBA32, true, false)
        {
            name = "T_GroundTileArray",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
        };

        for (int i = 0; i < slices.Count; i++) array.SetPixels32(slices[i], i, 0);
        array.Apply(true, false);

        Directory.CreateDirectory(Path.GetDirectoryName(ArrayPath));
        var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(ArrayPath);
        if (existing != null) AssetDatabase.DeleteAsset(ArrayPath);
        AssetDatabase.CreateAsset(array, ArrayPath);
        AssetDatabase.SaveAssets();

        Debug.Log("GroundTileSlicer: wrote " + slices.Count + " slices to " + ArrayPath);
    }
}
