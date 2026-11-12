// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private VideoSource? _video;
    private int _frameIndex;
    private bool _playing, _updatingTransport;
    private readonly DispatcherTimer _playback = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly Stopwatch _playClock = new();
    private int _playStartFrame;
    private CancellationTokenSource? _loadCancellation, _renderCancellation;

    private void InitializeVideo()
    {
        _playback.Tick += async (_, _) =>
        {
            if (!_playing || _video == null || _busy || _debounce.IsEnabled) return;
            var target = Math.Min(_video.Info.FrameCount - 1,
                _playStartFrame + (int)Math.Floor(_playClock.Elapsed.TotalSeconds * _video.Info.FramesPerSecond));
            if (target == _frameIndex)
            {
                if (target == _video.Info.FrameCount - 1)
                {
                    if (LoopPlaybackButton.IsChecked == true)
                    {
                        if (_playClock.Elapsed.TotalSeconds < (_video.Info.FrameCount - _playStartFrame) / _video.Info.FramesPerSecond) return;
                        SetFrame(0); InvalidateRender();
                        _playStartFrame = 0; _playClock.Restart();
                        await Render();
                    }
                    else PauseVideo();
                }
                return;
            }
            SetFrame(target);
            InvalidateRender();
            await Render();
        };
    }
    private void InvalidateRender()
    {
        _version++;
        _renderCancellation?.Cancel();
        ExportButton.IsEnabled = false;
    }
    private void PauseVideo()
    {
        _playing = false; _playback.Stop(); _playClock.Stop();
        PlayButton.Content = Localization.T("Abspielen");
        // Only a completed rendering of the selected position may be exported.
        ExportButton.IsEnabled = _output != null && !_busy && !_debounce.IsEnabled && _outputInputValid;
    }
    private async Task ClearVideo()
    {
        PauseVideo(); InvalidateRender();
        var old = _video; _video = null; _frameIndex = 0;
        VideoExportButton.IsEnabled = false;
        VideoTransport.Visibility = Visibility.Collapsed;
        if (old != null) await old.DisposeAsync();
    }
    private async void OpenVideo(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        PauseVideo();
        var dialog = new OpenFileDialog { Title = Localization.T("Video öffnen…"), Filter = Localization.T("Videos|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.wmv|Alle Dateien|*.*") };
        if (dialog.ShowDialog(this) == true) await LoadVideo(dialog.FileName);
    }

    internal async Task LoadVideo(string path)
    {
        if (!_ready || _closing) return;
        PauseVideo();
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        VideoExportButton.IsEnabled = false;
        VideoSource? next = null;
        Status.Text = Localization.T("Video wird geöffnet…");
        try
        {
            next = await VideoSource.OpenAsync(path, cancellation.Token);
            var first = await next.ReadFrameAsync(0, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            await ClearVideo();
            cancellation.Token.ThrowIfCancellationRequested();
            _video = next; next = null;
            _source = first; _output = null;
            Title = $"{Path.GetFileName(path)} · Tape & Tube";
            EmptyHint.Visibility = Visibility.Collapsed;
            VideoTransport.Visibility = Visibility.Visible;
            _updatingTransport = true;
            FrameSlider.Maximum = _video.Info.FrameCount - 1;
            FrameSlider.Value = 0;
            _updatingTransport = false;
            UpdateTransport(); UpdatePreview();
            Schedule();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closing && !cancellation.IsCancellationRequested) Error(ex); }
        finally
        {
            if (next != null) await next.DisposeAsync();
            if (ReferenceEquals(_loadCancellation, cancellation)) _loadCancellation = null;
            VideoExportButton.IsEnabled = _video != null && _exportCancellation == null && _loadCancellation == null && _outputInputValid;
        }
    }
    private void SetFrame(int index)
    {
        if (_video == null) return;
        _frameIndex = Math.Clamp(index, 0, _video.Info.FrameCount - 1);
        _updatingTransport = true; FrameSlider.Value = _frameIndex; _updatingTransport = false;
        UpdateTransport();
    }
    private void UpdateTransport()
    {
        if (_video == null) return;
        static string Time(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        VideoPosition.Text = Localization.F($"{Time(_frameIndex / _video.Info.FramesPerSecond)} / {Time(_video.Info.Duration)} · Bild {_frameIndex + 1} / {_video.Info.FrameCount}");
        PreviousFrameButton.IsEnabled = _frameIndex > 0;
        NextFrameButton.IsEnabled = _frameIndex < _video.Info.FrameCount - 1;
    }
    private void TogglePlayback(object sender, RoutedEventArgs e)
    {
        if (_video == null) return;
        if (_playing)
        {
            PauseVideo();
            // If a frame was still calculating, its completion enables export.
            return;
        }
        if (_frameIndex == _video.Info.FrameCount - 1) { SetFrame(0); InvalidateRender(); _ = Render(); }
        _playStartFrame = _frameIndex; _playClock.Restart(); _playing = true;
        ExportButton.IsEnabled = false; PlayButton.Content = Localization.T("Pause"); _playback.Start();
    }
    private void SeekVideo(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingTransport || _video == null || !_ready) return;
        PauseVideo(); SetFrame((int)Math.Round(FrameSlider.Value)); Schedule();
    }
    private void PreviousFrame(object sender, RoutedEventArgs e) => StepVideo(-1);
    private void NextFrame(object sender, RoutedEventArgs e) => StepVideo(1);
    private void StepVideo(int direction)
    {
        if (_video == null) return;
        PauseVideo(); SetFrame(_frameIndex + direction); Schedule();
    }
}
