# Builds Assets/Textures/Generated/T_MeadowDetail.png out of the painted tile
# sheet at Assets/ground.png.
#
# The sheet is a 2D tile-set reference, not a tileable texture: separate patches
# with rounded borders on a lit background. What is usable is the leaf work in
# the middle of its largest patch, so this crops that, makes it tile, and divides
# out its own local mean until nothing is left but light and shade.
#
# The green has to go. LowPolyGround_URP takes its colour from palette bands the
# CPU picked; a map that carried its own green would fight them. What it gets
# instead is a ratio around 0.5 that can only brighten and darken.
#
# Contrast is normalised to a measured standard deviation rather than a gain that
# looked about right - the first build shipped a 5% brightness swing and was
# invisible in game.
#
#   pwsh Tools/make_meadow_detail.ps1
#   copy the result over Assets/Textures/Generated/T_MeadowDetail.png

param(
  [string]$Src    = (Join-Path $PSScriptRoot '..\Assets\ground.png'),
  [int]$CropX     = 106,
  [int]$CropY     = 110,
  [int]$CropW     = 120,
  [int]$CropH     = 120,
  [int]$N         = 256,   # output size
  [int]$Blend     = 48,    # overlap blend band
  [int]$BlurR     = 28,    # high-pass radius
  [double]$TargetStd = 0.15, # measured contrast the map is normalised to
  [double]$Chroma    = 0.25, # how much of the per-channel hue drift survives
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
$lo = 0.0; $hi = 0.0; $clip = 0.0; $rawStd = 0.0
$detail = [DetailBuilder]::HighPass($seam, $blur, $N, $TargetStd, $Chroma, [ref]$rawStd, [ref]$lo, [ref]$hi, [ref]$clip)
Write-Output ("raw deviation std : {0:F4}  -> normalised to {1:F3}" -f $rawStd, $TargetStd)
Write-Output ("range: {0:F3} .. {1:F3}   clipped: {2:F2}%" -f $lo, $hi, $clip)

$detail = [DetailBuilder]::Sharpen($detail, $N, $SharpR, $SharpAmt)

$mean = 0.0; $std = 0.0
[DetailBuilder]::Stats($detail, [ref]$mean, [ref]$std)
Write-Output ("final mean {0:F4} (0.5 = neutral)  std {1:F4}" -f $mean, $std)
Write-Output ("=> at _DetailStrength 1.0 that is a +/-{0:F0}% brightness swing, 1 sigma" -f ($std * 200))

$out = "$OutDir\T_MeadowDetail.png"
[DetailBuilder]::Save($detail, $N, $out)
[DetailBuilder]::SaveTiled($out, "$OutDir\preview_tiled3x3.png", 3)

# Grass Mid from M_GroundLowPoly
$band = [float[]]@(0.27, 0.60, 0.21)
[DetailBuilder]::SavePreview($detail, $N, $band, 0.55, 3, "$OutDir\preview_shaded3x3.png")

Write-Output "wrote $out"
