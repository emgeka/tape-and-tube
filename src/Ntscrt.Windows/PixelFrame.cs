// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ntscrt.Windows;

internal sealed record PixelFrame(int Width, int Height, byte[] Pixels)
{
    public static PixelFrame Read(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return FromBitmap(decoder.Frames[0]);
    }
    public static PixelFrame FromBitmap(BitmapSource bitmap)
    {
        // Bound allocation before decompression/copying into our processing buffer.
        if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 24_000_000)
            throw new InvalidOperationException(Localization.T("Der Prototyp unterstützt Bilder bis 24 Megapixel."));
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[checked(converted.PixelWidth * converted.PixelHeight * 4)];
        converted.CopyPixels(bytes, converted.PixelWidth * 4, 0);
        // The upstream signal processor treats input as an opaque television picture.
        for (int p = 0; p < bytes.Length; p += 4)
        {
            var alpha = bytes[p + 3];
            for (int c = 0; c < 3; c++) bytes[p + c] = (byte)((bytes[p + c] * alpha + 127) / 255);
            bytes[p + 3] = 255;
        }
        return new(converted.PixelWidth, converted.PixelHeight, bytes);
    }
    public BitmapSource Bitmap()
    {
        var result = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, Pixels, Width * 4);
        result.Freeze();
        return result;
    }
    public PixelFrame Resize(int width)
    {
        if (width >= Width) return this;
        var transformed = new TransformedBitmap(Bitmap(), new ScaleTransform((double)width / Width, (double)width / Width));
        return FromBitmap(transformed);
    }
    public void Save(string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Bitmap()));
        using var file = File.Create(path);
        encoder.Save(file);
    }
    public static PixelFrame TestPattern()
    {
        const int width = 320, height = 240;
        var bytes = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var i = (y * width + x) * 4;
            bytes[i] = (byte)(x * 255 / (width - 1));
            bytes[i + 1] = (byte)(y * 255 / (height - 1));
            bytes[i + 2] = (byte)(((x / 12 + y / 12) % 2) * 255);
            bytes[i + 3] = 255;
        }
        return new(width, height, bytes);
    }
}
