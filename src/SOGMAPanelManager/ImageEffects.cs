using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SOGMAPanelManager;

public static class ImageEffects
{
    public static BitmapSource Apply(BitmapSource source, IEnumerable<PanelEditRegion> regions)
    {
        var list = regions?.ToList() ?? new List<PanelEditRegion>();
        if (list.Count == 0) return source;

        BitmapSource bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int width = bgra.PixelWidth;
        int height = bgra.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bgra.CopyPixels(pixels, stride, 0);

        foreach (var region in list)
        {
            int x0 = Math.Clamp((int)Math.Round(region.X * width), 0, Math.Max(0, width - 1));
            int y0 = Math.Clamp((int)Math.Round(region.Y * height), 0, Math.Max(0, height - 1));
            int x1 = Math.Clamp((int)Math.Round((region.X + region.Width) * width), x0 + 1, width);
            int y1 = Math.Clamp((int)Math.Round((region.Y + region.Height) * height), y0 + 1, height);

            string effect = region.Effect ?? "Pixelado";
            if (effect.Equals("Negro", StringComparison.OrdinalIgnoreCase))
                FillBlack(pixels, stride, x0, y0, x1, y1);
            else if (effect.Equals("Desenfoque", StringComparison.OrdinalIgnoreCase))
                Blur(pixels, width, height, stride, x0, y0, x1, y1, Math.Clamp(region.Strength / 4, 2, 14));
            else
                Pixelate(pixels, stride, x0, y0, x1, y1, Math.Clamp(region.Strength, 4, 64));
        }

        var wb = new WriteableBitmap(width, height, source.DpiX > 0 ? source.DpiX : 96, source.DpiY > 0 ? source.DpiY : 96, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        wb.Freeze();
        return wb;
    }

    private static void FillBlack(byte[] p, int stride, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y < y1; y++)
        {
            int row = y * stride;
            for (int x = x0; x < x1; x++)
            {
                int i = row + x * 4;
                p[i] = 0; p[i + 1] = 0; p[i + 2] = 0; p[i + 3] = 255;
            }
        }
    }

    private static void Pixelate(byte[] p, int stride, int x0, int y0, int x1, int y1, int block)
    {
        for (int by = y0; by < y1; by += block)
        {
            for (int bx = x0; bx < x1; bx += block)
            {
                int ex = Math.Min(x1, bx + block);
                int ey = Math.Min(y1, by + block);
                long b = 0, g = 0, r = 0, a = 0, count = 0;
                for (int y = by; y < ey; y++)
                {
                    int row = y * stride;
                    for (int x = bx; x < ex; x++)
                    {
                        int i = row + x * 4;
                        b += p[i]; g += p[i + 1]; r += p[i + 2]; a += p[i + 3]; count++;
                    }
                }
                if (count == 0) continue;
                byte bb = (byte)(b / count), gg = (byte)(g / count), rr = (byte)(r / count), aa = (byte)(a / count);
                for (int y = by; y < ey; y++)
                {
                    int row = y * stride;
                    for (int x = bx; x < ex; x++)
                    {
                        int i = row + x * 4;
                        p[i] = bb; p[i + 1] = gg; p[i + 2] = rr; p[i + 3] = aa;
                    }
                }
            }
        }
    }

    private static void Blur(byte[] p, int width, int height, int stride, int x0, int y0, int x1, int y1, int radius)
    {
        byte[] src = (byte[])p.Clone();
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int sx0 = Math.Max(x0, x - radius), sx1 = Math.Min(x1 - 1, x + radius);
                int sy0 = Math.Max(y0, y - radius), sy1 = Math.Min(y1 - 1, y + radius);
                long b = 0, g = 0, r = 0, a = 0, c = 0;
                for (int yy = sy0; yy <= sy1; yy++)
                {
                    int row = yy * stride;
                    for (int xx = sx0; xx <= sx1; xx++)
                    {
                        int i = row + xx * 4;
                        b += src[i]; g += src[i + 1]; r += src[i + 2]; a += src[i + 3]; c++;
                    }
                }
                int o = y * stride + x * 4;
                p[o] = (byte)(b / c); p[o + 1] = (byte)(g / c); p[o + 2] = (byte)(r / c); p[o + 3] = (byte)(a / c);
            }
        }
    }
}
