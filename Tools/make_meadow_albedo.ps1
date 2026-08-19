# Builds Assets/Textures/Generated/T_MeadowGrass.png out of the painted tile
# sheet at Assets/ground.png.
#
# Same crop and same seamless join as make_meadow_detail.ps1, and one decisive
# difference: this keeps the sheet's colour. The detail build divided the green
# out so the CPU palette could own the hue; this restores the global mean after
# flattening, so what lands on the board is the reference art itself. That is the
# look the sheet actually has - an even, vivid yellow-green (#7C9C04) meadow -
# rather than the palette's darker banded green.
#
# What still gets removed is the sheet's lighting: it is painted brighter at the
# top left and falls away to the right, and tiled at that gradient the board
# would pulse. Dividing by a wide blur and multiplying the global mean back takes
# the gradient without touching the leaf work.
#
#   pwsh Tools/make_meadow_albedo.ps1
#   copy the result over Assets/Textures/Generated/T_MeadowGrass.png
param(
  [string]$Src    = (Join-Path $PSScriptRoot '..\Assets\ground.png'),
  [int]$CropX     = 106,
  [int]$CropY     = 110,
  [int]$CropW     = 120,
  [int]$CropH     = 120,
  [int]$N         = 256,   # output size
  [int]$Blend     = 48,    # overlap blend band
  [int]$BlurR     = 40,    # illumination-flattening radius
  [double]$Saturation = 1.18, # chroma restored after flattening the lighting
  [int]$SharpR       = 2,
  [double]$SharpAmt  = 0.5,
  [string]$OutDir = (Join-Path $PSScriptRoot '..\Temp\meadow')
)

Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

public static class DetailBuilder
{
    // Crop a region and resample it to size x size with wrap-safe bicubic.
    public static float[] LoadUpscaled(string path, int cx, int cy, int cw, int ch, int size, out int outSize)
    {
        outSize = size;
        using (var src = new Bitmap(path))
        using (var dst = new Bitmap(size, size, PixelFormat.Format24bppRgb))
        {
            using (var g = Graphics.FromImage(dst))
            using (var ia = new ImageAttributes())
            {
                // TileFlipXY stops the bicubic kernel from pulling transparent /
                // clamped garbage in at the crop border.
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, size, size),
                            cx, cy, cw, ch, GraphicsUnit.Pixel, ia);
            }
            return ToFloat(dst);
        }
    }

    static float[] ToFloat(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        var buf = new float[w * h * 3];
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        unsafe
        {
            byte* p = (byte*)data.Scan0;
            for (int y = 0; y < h; y++)
            {
                byte* row = p + y * data.Stride;
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 3;
                    buf[i + 0] = row[x * 3 + 2] / 255f; // B,G,R -> R,G,B
                    buf[i + 1] = row[x * 3 + 1] / 255f;
                    buf[i + 2] = row[x * 3 + 0] / 255f;
                }
            }
        }
        bmp.UnlockBits(data);
        return buf;
    }

    // Overlap cross-fade that makes the image tile exactly.
    //
    // Source is (N+b) wide; the output keeps the first N columns, and the
    // leading b columns are faded into what sits N pixels further along. Because
    // the source is continuous across that span, column N-1 flows into column 0
    // with no step - the join is real, not a mirrored fake.
    public static float[] MakeSeamless(float[] src, int srcSize, int n, int b)
    {
        var dst = new float[n * n * 3];
        for (int y = 0; y < n; y++)
        {
            float ty = y < b ? (float)y / b : 1f;
            ty = ty * ty * (3f - 2f * ty);
            for (int x = 0; x < n; x++)
            {
                float tx = x < b ? (float)x / b : 1f;
                tx = tx * tx * (3f - 2f * tx);

                for (int c = 0; c < 3; c++)
                {
                    float a = At(src, srcSize, x,     y,     c);
                    float bx = At(src, srcSize, x + n, y,     c);
                    float by = At(src, srcSize, x,     y + n, c);
                    float bb = At(src, srcSize, x + n, y + n, c);

                    float top = a * tx + bx * (1f - tx);
                    float bot = by * tx + bb * (1f - tx);
                    dst[(y * n + x) * 3 + c] = top * ty + bot * (1f - ty);
                }
            }
        }
        return dst;
    }

    static float At(float[] buf, int size, int x, int y, int c)
    {
        if (x >= size) x = size - 1;
        if (y >= size) y = size - 1;
        return buf[(y * size + x) * 3 + c];
    }

    // Separable box blur, wrapping at the edges so the blur itself stays
    // tileable and cannot bias the borders.
    public static float[] BlurWrap(float[] src, int n, int r, int passes)
    {
        var cur = (float[])src.Clone();
        var tmp = new float[cur.Length];
        for (int p = 0; p < passes; p++)
        {
            BoxH(cur, tmp, n, r);
            BoxV(tmp, cur, n, r);
        }
        return cur;
    }

    static void BoxH(float[] s, float[] d, int n, int r)
    {
        float inv = 1f / (2 * r + 1);
        for (int y = 0; y < n; y++)
            for (int c = 0; c < 3; c++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++) sum += s[(y * n + Wrap(k, n)) * 3 + c];
                for (int x = 0; x < n; x++)
                {
                    d[(y * n + x) * 3 + c] = sum * inv;
                    sum -= s[(y * n + Wrap(x - r, n)) * 3 + c];
                    sum += s[(y * n + Wrap(x + r + 1, n)) * 3 + c];
                }
            }
    }

    static void BoxV(float[] s, float[] d, int n, int r)
    {
        float inv = 1f / (2 * r + 1);
        for (int x = 0; x < n; x++)
            for (int c = 0; c < 3; c++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++) sum += s[(Wrap(k, n) * n + x) * 3 + c];
                for (int y = 0; y < n; y++)
                {
                    d[(y * n + x) * 3 + c] = sum * inv;
                    sum -= s[(Wrap(y - r, n) * n + x) * 3 + c];
                    sum += s[(Wrap(y + r + 1, n) * n + x) * 3 + c];
                }
            }
    }

    static int Wrap(int v, int n) { v %= n; return v < 0 ? v + n : v; }

    // Divide by the local mean. What survives is the painter's leaf work as a
    // ratio around 1.0 - the sheet's overall green and its lighting gradient
    // both divide out, which is exactly what has to happen before this can
    // multiply a palette colour that the CPU already chose.
    // The ratio is taken on luminance first and only partly per-channel. Taken
    // per-channel outright it turns the sheet's yellow-green/blue-green drift
    // into violent yellow and violet tints, and multiplying those over the
    // palette green muddies exactly the colour the CPU was careful to pick. The
    // painter's value structure is what reads from the game camera; the hue
    // stays the palette's job.
    // Gain is not guessed. The raw deviations are measured, then scaled so their
    // standard deviation lands on targetStd - the first pass was tuned by eye to
    // a "reasonable looking" gain and rendered at about 5% brightness swing, far
    // too faint to see under the palette bands. Contrast this map needs is a
    // number, so it gets measured like one.
    public static float[] HighPass(float[] src, float[] blur, int n, float targetStd, float chroma,
                                   out float rawStd, out float lo, out float hi, out float clipped)
    {
        int len = src.Length;
        var dev = new float[len];

        for (int i = 0; i < len; i += 3)
        {
            float bl = Math.Max(Lum(blur, i), 1e-4f);
            float lumRatio = Lum(src, i) / bl;

            for (int c = 0; c < 3; c++)
            {
                float bc = Math.Max(blur[i + c], 1e-4f);
                float chanRatio = src[i + c] / bc;
                dev[i + c] = (lumRatio + (chanRatio - lumRatio) * chroma) - 1f;
            }
        }

        double sum = 0, sumSq = 0;
        for (int i = 0; i < len; i++) { sum += dev[i]; sumSq += (double)dev[i] * dev[i]; }
        double mean = sum / len;
        rawStd = (float)Math.Sqrt(Math.Max(0, sumSq / len - mean * mean));

        float scale = rawStd > 1e-6f ? targetStd / rawStd : 1f;

        var d = new float[len];
        lo = 1f; hi = 0f;
        int clip = 0;
        for (int i = 0; i < len; i++)
        {
            float v = 0.5f + (float)(dev[i] - mean) * scale;
            if (v < 0f) { v = 0f; clip++; }
            else if (v > 1f) { v = 1f; clip++; }
            d[i] = v;
            if (v < lo) lo = v;
            if (v > hi) hi = v;
        }
        clipped = 100f * clip / len;
        return d;
    }

    // Divide out the local mean, then put the GLOBAL mean back.
    //
    // This is the one line that separates this build from the detail-map build.
    // There the mean was thrown away, which left pure light and shade for the
    // palette to colour. Here it is restored as a single flat colour, so the
    // sheet's own green survives while its lighting gradient - the sheet is lit
    // brighter at the top left and falls off to the right - does not. The result
    // is the painted grass at one even level across the whole tile, which is what
    // lets it read as the reference art rather than as a lit photograph of it.
    public static float[] Flatten(float[] src, float[] blur, int n,
                                  out float mr, out float mg, out float mb)
    {
        int px = src.Length / 3;
        double sr = 0, sg = 0, sb = 0;
        for (int i = 0; i < src.Length; i += 3) { sr += blur[i]; sg += blur[i + 1]; sb += blur[i + 2]; }
        mr = (float)(sr / px); mg = (float)(sg / px); mb = (float)(sb / px);
        float[] mean = { mr, mg, mb };

        var d = new float[src.Length];
        for (int i = 0; i < src.Length; i += 3)
            for (int c = 0; c < 3; c++)
            {
                float bl = Math.Max(blur[i + c], 1e-4f);
                float v = src[i + c] / bl * mean[c];
                d[i + c] = v < 0f ? 0f : (v > 1f ? 1f : v);
            }
        return d;
    }

    // Pushes chroma away from each pixel's own luminance. Flattening the
    // illumination costs a little saturation, and the reference art's appeal is
    // partly how vivid that yellow-green is.
    public static float[] Saturate(float[] src, float amount)
    {
        var d = new float[src.Length];
        for (int i = 0; i < src.Length; i += 3)
        {
            float l = Lum(src, i);
            for (int c = 0; c < 3; c++)
            {
                float v = l + (src[i + c] - l) * amount;
                d[i + c] = v < 0f ? 0f : (v > 1f ? 1f : v);
            }
        }
        return d;
    }

    // Small-radius unsharp. The crop is upscaled 2.5x off a 120px source, and
    // pushing its contrast up also pushes up that softness; this puts the edge
    // back on the leaf shapes without touching the large-scale balance.
    public static float[] Sharpen(float[] src, int n, int r, float amount)
    {
        var soft = BlurWrap(src, n, r, 1);
        var d = new float[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            float v = src[i] + (src[i] - soft[i]) * amount;
            d[i] = v < 0f ? 0f : (v > 1f ? 1f : v);
        }
        return d;
    }

    public static void Stats(float[] b, out float mean, out float std)
    {
        double s = 0, sq = 0;
        for (int i = 0; i < b.Length; i++) { s += b[i]; sq += (double)b[i] * b[i]; }
        mean = (float)(s / b.Length);
        std = (float)Math.Sqrt(Math.Max(0, sq / b.Length - (double)mean * mean));
    }

    static float Lum(float[] b, int i) => 0.2126f * b[i] + 0.7152f * b[i + 1] + 0.0722f * b[i + 2];

    public static void Save(float[] buf, int n, string path)
    {
        using (var bmp = new Bitmap(n, n, PixelFormat.Format24bppRgb))
        {
            var data = bmp.LockBits(new Rectangle(0, 0, n, n), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* p = (byte*)data.Scan0;
                for (int y = 0; y < n; y++)
                {
                    byte* row = p + y * data.Stride;
                    for (int x = 0; x < n; x++)
                    {
                        int i = (y * n + x) * 3;
                        row[x * 3 + 2] = (byte)Math.Round(Math.Max(0f, Math.Min(1f, buf[i + 0])) * 255f);
                        row[x * 3 + 1] = (byte)Math.Round(Math.Max(0f, Math.Min(1f, buf[i + 1])) * 255f);
                        row[x * 3 + 0] = (byte)Math.Round(Math.Max(0f, Math.Min(1f, buf[i + 2])) * 255f);
                    }
                }
            }
            bmp.UnlockBits(data);
            bmp.Save(path, ImageFormat.Png);
        }
    }

    // 3x3 repeat of the finished map, so tiling artefacts can actually be looked
    // at instead of assumed away.
    public static void SaveTiled(string src, string dst, int reps)
    {
        using (var s = new Bitmap(src))
        using (var o = new Bitmap(s.Width * reps, s.Height * reps, PixelFormat.Format24bppRgb))
        {
            using (var g = Graphics.FromImage(o))
                for (int y = 0; y < reps; y++)
                    for (int x = 0; x < reps; x++)
                        g.DrawImage(s, x * s.Width, y * s.Height, s.Width, s.Height);
            o.Save(dst, ImageFormat.Png);
        }
    }

    // Preview of what the shader will actually produce: a palette colour
    // multiplied by the detail overlay.
    public static void SavePreview(float[] detail, int n, float[] band, float strength, int reps, string path)
    {
        using (var o = new Bitmap(n * reps, n * reps, PixelFormat.Format24bppRgb))
        {
            var data = o.LockBits(new Rectangle(0, 0, n * reps, n * reps), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* p = (byte*)data.Scan0;
                for (int y = 0; y < n * reps; y++)
                {
                    byte* row = p + y * data.Stride;
                    for (int x = 0; x < n * reps; x++)
                    {
                        int i = ((y % n) * n + (x % n)) * 3;
                        for (int c = 0; c < 3; c++)
                        {
                            float m = 1f + (detail[i + c] - 0.5f) * 2f * strength;
                            float v = band[c] * m;
                            if (v < 0f) v = 0f; if (v > 1f) v = 1f;
                            row[x * 3 + (2 - c)] = (byte)Math.Round(v * 255f);
                        }
                    }
                }
            }
            o.UnlockBits(data);
            o.Save(path, ImageFormat.Png);
        }
    }
}
'@

# PS7 forwards System.Drawing to System.Drawing.Common, so the compiler needs the
# real assembly paths rather than the framework-era short names.
# System.Drawing.Common in turn leans on System.Private.Windows.GdiPlus, so pull
# in every loaded assembly rather than trying to name the chain by hand.
$gdiDir = Split-Path ([System.Drawing.Bitmap].Assembly.Location)
$refs = @(
  [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.Location }
  Get-ChildItem "$gdiDir\System.Private.Windows.*.dll" -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique

Add-Type -TypeDefinition $code -ReferencedAssemblies $refs -CompilerOptions "/unsafe" -ErrorAction Stop

$srcSize = $N + $Blend
$up = [DetailBuilder]::LoadUpscaled($Src, $CropX, $CropY, $CropW, $CropH, $srcSize, [ref]$null)
Write-Output "upscaled crop -> $srcSize x $srcSize"

$seam = [DetailBuilder]::MakeSeamless($up, $srcSize, $N, $Blend)
[DetailBuilder]::Save($seam, $N, "$OutDir\stage_seamless.png")

$blur = [DetailBuilder]::BlurWrap($seam, $N, $BlurR, 3)

$r = 0.0; $g = 0.0; $b = 0.0
$flat = [DetailBuilder]::Flatten($seam, $blur, $N, [ref]$r, [ref]$g, [ref]$b)
Write-Output ("mean colour kept: #{0:X2}{1:X2}{2:X2}  ({3:F3}, {4:F3}, {5:F3})" -f `
  [int][Math]::Round($r * 255), [int][Math]::Round($g * 255), [int][Math]::Round($b * 255), $r, $g, $b)

$flat = [DetailBuilder]::Saturate($flat, $Saturation)
$flat = [DetailBuilder]::Sharpen($flat, $N, $SharpR, $SharpAmt)

$mean = 0.0; $std = 0.0
[DetailBuilder]::Stats($flat, [ref]$mean, [ref]$std)
Write-Output ("final mean {0:F4}  std {1:F4}" -f $mean, $std)

$out = "$OutDir\T_MeadowGrass.png"
[DetailBuilder]::Save($flat, $N, $out)
[DetailBuilder]::SaveTiled($out, "$OutDir\preview_grass3x3.png", 3)

Write-Output "wrote $out"
