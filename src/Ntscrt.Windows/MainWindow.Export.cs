// SPDX-License-Identifier: GPL-3.0-only
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private CancellationTokenSource? _exportCancellation;
    private bool _closeAfterExport;

    private async void ExportImage(object sender, RoutedEventArgs e)
    {
        if (_output is not { } snapshot || !ExportButton.IsEnabled || !_outputInputValid || _exportCancellation != null) return;
        var dialog = new SaveFileDialog
        {
            Title = Localization.T("Bild speichern…"), Filter = ImageExporter.Filter, AddExtension = true,
            FileName = "tape-and-tube", OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        var quality = (int)ImageQualityChoice.Value;
        var losslessWebp = LosslessWebpChoice.IsChecked == true;
        using var cancellation = new CancellationTokenSource();
        BeginExport(cancellation, Localization.T("Bild wird gespeichert…"));
        try
        {
            await Task.Run(() => ImageExporter.SaveAsync(snapshot, dialog.FileName, quality, losslessWebp, cancellation.Token));
            Status.Text = Localization.F($"Bild gespeichert: {dialog.FileName}");
        }
        catch (OperationCanceledException) { Status.Text = Localization.T("Bildexport abgebrochen."); }
        catch (Exception ex) { Error(ex); }
        finally
        {
            EndExport();
            if (_closeAfterExport) Close();
        }
    }

    private async void ExportVideo(object sender, RoutedEventArgs e)
    {
        if (!CanExportTimeline || TimelineInfo is not VideoInfo info || _exportCancellation != null || _loadCancellation != null || !_outputInputValid) return;
        PauseVideo();
        var sourcePath = info.IsStill ? _stillPath : info.Path;
        var still = info.IsStill ? _source : null;
        var dialog = new SaveFileDialog
        {
            Title = Localization.T("Video oder GIF speichern…"),
            Filter = Localization.T("MP4-Video (H.264 mit Originalton)|*.mp4") + "|MP4 (H.265 / HEVC)|*.mp4|" +
                "MOV (ProRes 422)|*.mov|WebM (VP9 / Opus)|*.webm|" + Localization.T("Animiertes GIF|*.gif"),
            AddExtension = true,
            FileName = (Path.GetFileNameWithoutExtension(sourcePath) ?? (InScreenLoop ? "screen-loop" : "animation"))
                + "-tape-and-tube", OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        var format = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
        {
            ".mp4" => dialog.FilterIndex == 2 ? VideoExportFormat.Hevc : VideoExportFormat.Mp4,
            ".mov" => VideoExportFormat.ProRes,
            ".webm" => VideoExportFormat.WebM,
            ".gif" => VideoExportFormat.Gif,
            _ => (VideoExportFormat?)null
        };
        if (format is not { } selectedFormat)
        {
            Status.Text = Localization.T("Wähle MP4, MOV, WebM oder GIF als Videoformat.");
            return;
        }
        var isGif = selectedFormat == VideoExportFormat.Gif;
        var quality = (Mp4Quality)Mp4QualityChoice.SelectedIndex;
        var loops = isGif ? 1 : Mp4LoopChoice.SelectedIndex + 1;
        GifExportSettings? gifSettings = null;
        if (isGif && !TryGetGifSettings(out gifSettings))
        {
            Status.Text = Localization.T("GIF-Breite: 64–1920 px. GIF-Bildrate: 1–50 Bilder/s.");
            return;
        }
        using var cancellation = new CancellationTokenSource();
        var settings = Settings(preview: false);
        InvalidateRender(); _debounce.Stop(); _pending = false;
        BeginExport(cancellation, Localization.T("Videoexport wird vorbereitet…"));
        Status.Text = Localization.T("Das gesamte Video wird mit den aktuellen Effekteinstellungen exportiert.");
        var clock = Stopwatch.StartNew();
        var progress = new Progress<ExportProgress>(p =>
        {
            if (!ReferenceEquals(_exportCancellation, cancellation) || cancellation.IsCancellationRequested) return;
            ExportProgressBar.IsIndeterminate = p.Finalizing;
            ExportProgressBar.Value = 100.0 * p.CompletedFrames / p.TotalFrames;
            var remaining = p.CompletedFrames == 0 ? "" : Localization.F($" · etwa {TimeSpan.FromSeconds(clock.Elapsed.TotalSeconds * (p.TotalFrames - p.CompletedFrames) / p.CompletedFrames):hh\\:mm\\:ss} verbleibend");
            ExportProgressText.Text = p.Finalizing ? Localization.T("Export wird abgeschlossen…") :
                Localization.F($"{100.0 * p.CompletedFrames / p.TotalFrames:0}% · Bild {p.CompletedFrames} / {p.TotalFrames} · {p.Width} × {p.Height}{remaining}");
        });
        var stillSeconds = settings.ImportedLook?.Seconds ?? (OwnAnimationEnabled ? _effectTimeline.Seconds : _screenLoopSeconds);
        try
        {
            var result = await Task.Run(() => still == null
                ? VideoExporter.ExportAsync(info.Path, dialog.FileName, settings, progress, cancellation.Token,
                    overwrite: true, format: selectedFormat, quality: quality, loops: loops, gifSettings: gifSettings)
                : VideoExporter.ExportStillAsync(still, sourcePath, dialog.FileName, stillSeconds, settings,
                    progress, cancellation.Token, overwrite: true, fps: (int)info.FramesPerSecond, format: selectedFormat,
                    quality: quality, gifSettings: gifSettings));
            Status.Text = isGif
                ? Localization.F($"GIF gespeichert: {result.Path} · {result.Frames} Bilder · {result.Width} × {result.Height}")
                : Localization.F($"Video gespeichert: {result.Path} · {result.Frames} Bilder · {result.Width} × {result.Height}");
        }
        catch (OperationCanceledException) { Status.Text = Localization.T("Videoexport abgebrochen. Die Zieldatei wurde nicht verändert."); }
        catch (Exception ex) { Error(ex); }
        finally
        {
            EndExport();
            if (_closeAfterExport) Close();
            else if (_source != null)
            {
                var message = Status.Text;
                _debounce.Stop(); await Render();
                Status.Text = message;
            }
        }
    }

    private void BeginExport(CancellationTokenSource cancellation, string message)
    {
        _exportCancellation = cancellation;
        MediaActions.IsEnabled = false;
        Editor.IsEnabled = false;
        ExportPanel.Visibility = Visibility.Visible;
        ExportProgressBar.Value = 0;
        ExportProgressBar.IsIndeterminate = true;
        ExportProgressText.Text = message;
        CancelExportButton.IsEnabled = true;
    }

    private void EndExport()
    {
        _exportCancellation = null;
        MediaActions.IsEnabled = true;
        Editor.IsEnabled = true;
        ExportPanel.Visibility = Visibility.Collapsed;
        RefreshVideoExportButton();
    }

    private bool TryGetGifSettings([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GifExportSettings? settings)
    {
        settings = null;
        if (!ReadOptionalInt(GifWidthField.Text, GifExportSettings.MinWidth, GifExportSettings.MaxWidth, out var width)
            || !ReadOptionalInt(GifFpsField.Text, 1, GifExportSettings.MaxFps, out var fps)) return false;
        settings = new(width, fps);
        return true;
    }

    private static bool ReadOptionalInt(string text, int min, int max, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < min || number > max)
            return false;
        value = number;
        return true;
    }

    private void CancelExport(object sender, RoutedEventArgs e)
    {
        _exportCancellation?.Cancel();
        CancelExportButton.IsEnabled = false;
        ExportProgressText.Text = Localization.T("Export wird abgebrochen…");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_exportCancellation != null)
        {
            e.Cancel = true; _closeAfterExport = true;
            _exportCancellation.Cancel();
            CancelExportButton.IsEnabled = false;
            ExportProgressText.Text = Localization.T("Export wird abgebrochen und die App geschlossen…");
        }
        base.OnClosing(e);
    }
}
