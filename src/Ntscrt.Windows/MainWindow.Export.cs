// SPDX-License-Identifier: GPL-3.0-only
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private CancellationTokenSource? _exportCancellation;
    private bool _closeAfterExport;

    private async void ExportVideo(object sender, RoutedEventArgs e)
    {
        if (_video == null || _exportCancellation != null || _loadCancellation != null || !_outputInputValid) return;
        PauseVideo();
        var sourcePath = _video.Info.Path;
        var dialog = new SaveFileDialog
        {
            Title = Localization.T("Video speichern…"),
            Filter = Localization.T("MP4-Video (H.264 mit Originalton)|*.mp4"), DefaultExt = ".mp4", AddExtension = true,
            FileName = Path.GetFileNameWithoutExtension(sourcePath) + "-tape-and-tube.mp4", OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        using var cancellation = new CancellationTokenSource();
        _exportCancellation = cancellation;
        var settings = Settings();
        InvalidateRender(); _debounce.Stop(); _pending = false;
        MediaActions.IsEnabled = false; Editor.IsEnabled = false; OutputOptions.IsEnabled = false;
        ExportPanel.Visibility = Visibility.Visible;
        ExportProgressBar.Value = 0; ExportProgressBar.IsIndeterminate = true;
        ExportProgressText.Text = Localization.T("Videoexport wird vorbereitet…");
        CancelExportButton.IsEnabled = true;
        Status.Text = Localization.T("Das gesamte Video wird mit den aktuellen Effekteinstellungen exportiert.");
        var clock = Stopwatch.StartNew();
        var progress = new Progress<ExportProgress>(p =>
        {
            if (!ReferenceEquals(_exportCancellation, cancellation) || cancellation.IsCancellationRequested) return;
            ExportProgressBar.IsIndeterminate = p.Finalizing;
            ExportProgressBar.Value = 100.0 * p.CompletedFrames / p.TotalFrames;
            var remaining = p.CompletedFrames == 0 ? "" : Localization.F($" · etwa {TimeSpan.FromSeconds(clock.Elapsed.TotalSeconds * (p.TotalFrames - p.CompletedFrames) / p.CompletedFrames):hh\\:mm\\:ss} verbleibend");
            ExportProgressText.Text = p.Finalizing ? Localization.T("MP4 wird abgeschlossen…") :
                Localization.F($"{100.0 * p.CompletedFrames / p.TotalFrames:0}% · Bild {p.CompletedFrames} / {p.TotalFrames} · {p.Width} × {p.Height}{remaining}");
        });
        try
        {
            var result = await Task.Run(() => VideoExporter.ExportAsync(sourcePath, dialog.FileName, settings,
                progress, cancellation.Token, overwrite: true));
            Status.Text = Localization.F($"Video gespeichert: {result.Path} · {result.Frames} Bilder · {result.Width} × {result.Height}");
        }
        catch (OperationCanceledException) { Status.Text = Localization.T("Videoexport abgebrochen. Die Zieldatei wurde nicht verändert."); }
        catch (Exception ex) { Error(ex); }
        finally
        {
            _exportCancellation = null;
            MediaActions.IsEnabled = true; Editor.IsEnabled = true; OutputOptions.IsEnabled = true;
            ExportPanel.Visibility = Visibility.Collapsed;
            VideoExportButton.IsEnabled = _video != null && _outputInputValid;
            if (_closeAfterExport) Close();
            else if (_source != null)
            {
                var message = Status.Text;
                _debounce.Stop(); await Render();
                Status.Text = message;
            }
        }
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
