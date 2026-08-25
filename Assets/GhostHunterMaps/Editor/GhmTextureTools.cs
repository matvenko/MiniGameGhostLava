using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Everything the catalogue panel does to actual image files: bring them into
    // the project, cut a painted sheet into usable pieces, and pack pieces back
    // out into an atlas.
    //
    // Pixels are always read through a render-texture copy rather than by
    // flipping the importer's Read/Write flag. Toggling that flag re-imports the
    // asset, doubles its memory in the build, and leaves the project changed
    // behind the user's back; a blit copy is temporary and works on compressed
    // textures too.
    public static class GhmTextureTools
    {
        public const string TextureRoot = "Assets/GhostHunterMaps/Textures";

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".webp", ".exr" };

        // ------------------------------------------------------------------
        // Import
        // ------------------------------------------------------------------

        public static int ImportFiles(GhmMapProfile profile, GhmTextureCategory category)
        {
            string path = EditorUtility.OpenFilePanel("Import texture", "", "png,jpg,jpeg,tga,psd,tif,webp");
            if (string.IsNullOrEmpty(path)) return 0;
            return ImportPaths(profile, category, new[] { path });
        }

        public static int ImportFolder(GhmMapProfile profile, GhmTextureCategory category)
        {
            string folder = EditorUtility.OpenFolderPanel("Import every image in a folder", "", "");
            if (string.IsNullOrEmpty(folder)) return 0;

            var files = new List<string>();
            foreach (var file in Directory.GetFiles(folder))
            {
                if (Array.IndexOf(ImageExtensions, Path.GetExtension(file).ToLowerInvariant()) >= 0) files.Add(file);
            }
            return ImportPaths(profile, category, files.ToArray());
        }

        // Files already inside the project are catalogued where they are; files
        // from outside are copied in first. Either way the importer settings are
        // set to match what the category is used for.
        public static int ImportPaths(GhmMapProfile profile, GhmTextureCategory category, string[] paths)
        {
            string destinationFolder = EnsureFolder(TextureRoot + "/" + category);
            int added = 0;

            foreach (var source in paths)
            {
                string assetPath;
                if (source.Replace('\\', '/').StartsWith(Application.dataPath))
                {
                    assetPath = "Assets" + source.Replace('\\', '/').Substring(Application.dataPath.Length);
                }
                else
                {
                    string fileName = Path.GetFileName(source);
                    assetPath = AssetDatabase.GenerateUniqueAssetPath(destinationFolder + "/" + fileName);
                    File.Copy(source, assetPath, false);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }

                ConfigureImporter(assetPath, category);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null) continue;

                if (AddToCatalog(profile, texture, category)) added++;
            }

            if (added > 0) EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return added;
        }

        public static bool AddToCatalog(GhmMapProfile profile, Texture2D texture, GhmTextureCategory category)
        {
            foreach (var existing in profile.catalog)
            {
                if (existing.texture == texture) return false;
            }

            profile.catalog.Add(new GhmTextureEntry
            {
                name = texture.name,
                texture = texture,
                category = category
            });
            return true;
        }

        // Decor is cut-out art that must keep its alpha edge and must not tile;
        // ground and water are tiling surfaces. Getting this wrong is the usual
        // reason an imported sheet shows seams or a halo.
        public static void ConfigureImporter(string assetPath, GhmTextureCategory category)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool decor = category == GhmTextureCategory.Decor;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = decor ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = decor ? 1 : 4;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------
        // Slicing
        // ------------------------------------------------------------------

        // Even grid slice, for a sheet laid out in equal cells.
        public static int SliceGrid(GhmMapProfile profile, GhmTextureEntry entry, int columns, int rows, int padding, bool skipEmpty)
        {
            if (entry == null || entry.texture == null || columns < 1 || rows < 1) return 0;

            var source = MakeReadable(entry.texture);
            string folder = EnsureFolder(TextureRoot + "/" + entry.category + "/" + SafeName(entry.name) + "_slices");

            int cellW = source.width / columns;
            int cellH = source.height / rows;
            int written = 0;

            try
            {
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        int x = c * cellW + padding;
                        // Texture rows run bottom-up, sheets are read top-down.
                        int y = (rows - 1 - r) * cellH + padding;
                        int w = Mathf.Max(1, cellW - padding * 2);
                        int h = Mathf.Max(1, cellH - padding * 2);

                        var pixels = source.GetPixels(x, y, w, h);
                        if (skipEmpty && IsBlank(pixels)) continue;

                        string name = $"{SafeName(entry.name)}_{r}_{c}";
                        var created = WritePng(folder, name, pixels, w, h, entry.category);
                        if (created == null) continue;

                        profile.catalog.Add(new GhmTextureEntry
                        {
                            name = name,
                            texture = created,
                            category = entry.category,
                            sourceSheet = entry.texture,
                            sourceRect = new Rect(x, y, w, h)
                        });
                        written++;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            AssetDatabase.Refresh();
            EditorUtility.SetDirty(profile);
            return written;
        }

        // Alpha islands, for a painted sheet where the pieces are scattered at
        // whatever size the artist drew them. This is the one that matters for
        // hand-painted asset sheets, which are almost never on a neat grid.
        public static int SliceByAlpha(GhmMapProfile profile, GhmTextureEntry entry, int minSize, int padding, float alphaThreshold)
        {
            if (entry == null || entry.texture == null) return 0;

            var source = MakeReadable(entry.texture);
            string folder = EnsureFolder(TextureRoot + "/" + entry.category + "/" + SafeName(entry.name) + "_slices");
            int written = 0;

            try
            {
                var pixels = source.GetPixels();
                int w = source.width, h = source.height;
                var visited = new bool[w * h];
                var rects = new List<RectInt>();

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (visited[i] || pixels[i].a < alphaThreshold) continue;

                        int minX = x, maxX = x, minY = y, maxY = y;
                        var queue = new Queue<int>();
                        queue.Enqueue(i);
                        visited[i] = true;

                        while (queue.Count > 0)
                        {
                            int current = queue.Dequeue();
                            int cx = current % w, cy = current / w;
                            minX = Mathf.Min(minX, cx); maxX = Mathf.Max(maxX, cx);
                            minY = Mathf.Min(minY, cy); maxY = Mathf.Max(maxY, cy);

                            // Eight-way, so a piece drawn with a diagonal
                            // anti-aliased edge does not split into fragments.
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    int nx = cx + dx, ny = cy + dy;
                                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                    int ni = ny * w + nx;
                                    if (visited[ni] || pixels[ni].a < alphaThreshold) continue;
                                    visited[ni] = true;
                                    queue.Enqueue(ni);
                                }
                            }
                        }

                        if (maxX - minX + 1 < minSize || maxY - minY + 1 < minSize) continue;
                        rects.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
                    }
                }

                // Top-left first, so the generated names follow the order the
                // pieces appear on the sheet rather than scan order.
                rects.Sort((a, b) =>
                {
                    int byRow = (h - b.yMax).CompareTo(h - a.yMax);
                    return byRow != 0 ? byRow : a.x.CompareTo(b.x);
                });

                for (int i = 0; i < rects.Count; i++)
                {
                    var r = rects[i];
                    int x = Mathf.Max(0, r.x - padding);
                    int y = Mathf.Max(0, r.y - padding);
                    int rw = Mathf.Min(w - x, r.width + padding * 2);
                    int rh = Mathf.Min(h - y, r.height + padding * 2);

                    string name = $"{SafeName(entry.name)}_{i:00}";
                    var created = WritePng(folder, name, source.GetPixels(x, y, rw, rh), rw, rh, entry.category);
                    if (created == null) continue;

                    profile.catalog.Add(new GhmTextureEntry
                    {
                        name = name,
                        texture = created,
                        category = entry.category,
                        sourceSheet = entry.texture,
                        sourceRect = new Rect(x, y, rw, rh)
                    });
                    written++;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            AssetDatabase.Refresh();
            EditorUtility.SetDirty(profile);
            return written;
        }

        // ------------------------------------------------------------------
        // Export
        // ------------------------------------------------------------------

        [Serializable]
        public class AtlasFrame
        {
            public string name;
            public float x, y, width, height;
            public float pivotX = 0.5f, pivotY = 0f;
        }

        [Serializable]
        public class AtlasManifest
        {
            public string atlas;
            public int width;
            public int height;
            public List<AtlasFrame> frames = new List<AtlasFrame>();
        }

        // Packs the given entries into one texture plus a JSON manifest of the
        // frames. The manifest is what makes the atlas useful outside Unity -
        // it is the same shape most sprite tools read.
        public static string ExportAtlas(IList<GhmTextureEntry> entries, string atlasName, int padding, int maxSize)
        {
            if (entries == null || entries.Count == 0) return null;

            string path = EditorUtility.SaveFilePanel("Export atlas", "", atlasName + ".png", "png");
            if (string.IsNullOrEmpty(path)) return null;

            var copies = new List<Texture2D>();
            var names = new List<string>();
            foreach (var e in entries)
            {
                if (e.texture == null) continue;
                copies.Add(MakeReadable(e.texture));
                names.Add(e.name);
            }
            if (copies.Count == 0) return null;

            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect[] rects;
            try
            {
                rects = atlas.PackTextures(copies.ToArray(), Mathf.Max(0, padding), Mathf.Max(64, maxSize), false);
                File.WriteAllBytes(path, atlas.EncodeToPNG());

                var manifest = new AtlasManifest
                {
                    atlas = Path.GetFileName(path),
                    width = atlas.width,
                    height = atlas.height
                };

                for (int i = 0; i < rects.Length; i++)
                {
                    manifest.frames.Add(new AtlasFrame
                    {
                        name = names[i],
                        x = rects[i].x * atlas.width,
                        y = rects[i].y * atlas.height,
                        width = rects[i].width * atlas.width,
                        height = rects[i].height * atlas.height
                    });
                }

                File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonUtility.ToJson(manifest, true));
            }
            finally
            {
                foreach (var c in copies) UnityEngine.Object.DestroyImmediate(c);
                UnityEngine.Object.DestroyImmediate(atlas);
            }

            AssetDatabase.Refresh();
            return path;
        }

        public static int ExportTextures(IList<GhmTextureEntry> entries)
        {
            if (entries == null || entries.Count == 0) return 0;

            string folder = EditorUtility.SaveFolderPanel("Export textures to", "", "");
            if (string.IsNullOrEmpty(folder)) return 0;

            int written = 0;
            foreach (var e in entries)
            {
                if (e.texture == null) continue;
                var copy = MakeReadable(e.texture);
                try
                {
                    File.WriteAllBytes(Path.Combine(folder, SafeName(e.name) + ".png"), copy.EncodeToPNG());
                    written++;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
            }
            return written;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        public static Texture2D MakeReadable(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;

            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        private static Texture2D WritePng(string folder, string name, Color[] pixels, int width, int height, GhmTextureCategory category)
        {
            var slice = new Texture2D(width, height, TextureFormat.RGBA32, false);
            slice.SetPixels(pixels);
            slice.Apply();

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(name) + ".png");
            File.WriteAllBytes(assetPath, slice.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(slice);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(assetPath, category);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static bool IsBlank(Color[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0.02f) return false;
            }
            return true;
        }

        public static string EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return path;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return current;
        }

        public static string SafeName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "texture";
            foreach (var c in Path.GetInvalidFileNameChars()) raw = raw.Replace(c, '_');
            return raw.Replace(' ', '_');
        }
    }
}
