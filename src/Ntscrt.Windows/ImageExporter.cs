// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ntscrt.Windows;

internal static class ImageExporter
{
    public const string Filter = "PNG (*.png)|*.png|JPEG (*.jpg, *.jpeg)|*.jpg;*.jpeg|WebP (*.webp)|*.webp|TIFF (*.tif, *.tiff)|*.tif;*.tiff|BMP (*.bmp)|*.bmp";

    public static async Task SaveAsync(PixelFrame frame, string path, int quality, bool losslessWebp, CancellationToken token)
    {
        path = Path.GetFullPath(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".tif" or ".tiff" or ".bmp"))
            throw new ArgumentException(Localization.T("Wähle PNG, JPEG, WebP, TIFF oder BMP als Bildformat."));
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);
        token.ThrowIfCancellationRequested();
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".tapetube-{Guid.NewGuid():N}{extension}");
        try
        {
            if (extension == ".webp") await SaveWebpAsync(frame, temporary, quality, losslessWebp, token).ConfigureAwait(false);
            else if (extension == ".png") frame.Save(temporary);
            else SaveWindowsImage(frame, temporary, extension, quality);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void SaveWindowsImage(PixelFrame frame, string path, string extension, int quality)
    {
        BitmapEncoder encoder = extension switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = quality },
            ".tif" or ".tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw },
            ".bmp" => new BmpBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(extension))
        };
        BitmapSource bitmap = frame.Bitmap();
        if (encoder is JpegBitmapEncoder or BmpBitmapEncoder)
            bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        encoder.Save(file);
    }

    private static async Task SaveWebpAsync(PixelFrame frame, string path, int quality, bool lossless, CancellationToken token)
    {
        string[] args = ["-v", "error", "-nostdin", "-n", "-f", "rawvideo", "-pixel_format", "bgra",
            "-video_size", FormattableString.Invariant($"{frame.Width}x{frame.Height}"), "-i", "pipe:0",
            "-frames:v", "1", "-c:v", "libwebp", "-lossless", lossless ? "1" : "0", "-quality",
            (lossless ? 75 : quality).ToString(CultureInfo.InvariantCulture), "-f", "webp", path];
        using var encoder = MediaTools.Start("ffmpeg", args, output: false, input: true);
        using var registration = token.Register(() => MediaTools.Stop(encoder));
        var errors = encoder.StandardError.ReadToEndAsync();
        try
        {
            IOException? inputError = null;
            try { await encoder.StandardInput.BaseStream.WriteAsync(frame.Pixels, token).ConfigureAwait(false); }
            catch (IOException ex) when (!token.IsCancellationRequested) { inputError = ex; }
            finally { encoder.StandardInput.Close(); }
            await encoder.WaitForExitAsync(token).ConfigureAwait(false);
            var error = (await errors.ConfigureAwait(false)).Trim();
            token.ThrowIfCancellationRequested();
            if (encoder.ExitCode != 0)
                throw new InvalidDataException(Localization.T("WebP konnte nicht gespeichert werden. Prüfe, ob FFmpeg libwebp unterstützt.") +
                    (error.Length == 0 ? "" : Environment.NewLine + error));
            if (inputError != null) throw inputError;
            if (new FileInfo(path).Length == 0) throw new InvalidDataException(Localization.T("WebP konnte nicht gespeichert werden. Prüfe, ob FFmpeg libwebp unterstützt."));
        }
        catch
        {
            MediaTools.Stop(encoder);
            token.ThrowIfCancellationRequested();
            throw;
        }
    }
}
