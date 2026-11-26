// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private VideoSource? _video;
    private int _frameIndex;
    private bool _playing, _updatingTransport;
    private readonly Stopwatch _playClock = new();
    private int _playStartFrame;
    private CancellationTokenSource? _loadCancellation, _renderCancellation;
    private PreviewAudio? _previewAudio;
    private Task _previousPreviewAudio = Task.CompletedTask;

    private void StopPreviewAudio()
    {
        if (_previewAudio is not { } audio) return;
        _previewAudio = null;
        _previousPreviewAudio = audio.StopAsync();
    }

    private void HoldPlaybackClock()
    {
        _playStartFrame = _frameIndex;
        _playClock.Reset();
        StopPreviewAudio();
    }

    private void StartPlaybackClock()
    {
        _playStartFrame = _frameIndex;
        _playClock.Restart();
        if (_video?.Info is { HasAudio: true } info && PreviewAudioChoice.IsChecked == true)
            _previewAudio = new PreviewAudio(info, _frameIndex, _previousPreviewAudio);
    }

    private void PreviewAudioChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (PreviewAudioChoice.IsChecked == true)
            PreviewAudioChoice.SetResourceReference(ToolTipProperty, "PreviewAudioHint");
        if (!_playing) return;
        HoldPlaybackClock();
        if (!_busy && !_debounce.IsEnabled && _completedPreviewVersion == _version) StartPlaybackClock();
    }

    private async void PlaybackRendering(object? sender, EventArgs e)
    {
        if (!_playing || !_playClock.IsRunning || _busy || _debounce.IsEnabled || TimelineInfo is not VideoInfo info) return;
        if (_previewAudio?.Failed == true)
        {
            PreviewAudioChoice.IsChecked = false;
            PreviewAudioChoice.SetResourceReference(ToolTipProperty, "PreviewAudioUnavailable");
        }
        var elapsed = _previewAudio?.Elapsed ?? _playClock.Elapsed.TotalSeconds;
        var target = _playStartFrame + (int)Math.Min(info.FrameCount - 1 - _playStartFrame, Math.Floor(elapsed * info.FramesPerSecond));
        if (target == _frameIndex)
        {
            var remaining = (info.FrameCount - _playStartFrame) / info.FramesPerSecond;
            var tolerance = _previewAudio == null ? 0 : 1.0 / PreviewAudio.SampleRate;
            if (target != info.FrameCount - 1 || elapsed + tolerance < remaining) return;
            if (LoopPlaybackButton.IsChecked != true) { PauseVideo(); return; }
            target = 0;
        }
        else if (!HistoricalDisplayActive && (_importedLook != null || OwnRenderer == DigitalCatalog.Profile) && target > _frameIndex + 1)
        {
            // The shared core needs every frame. Jumping ahead makes its history
            // request rewind the same decoder, even during ordinary playback.
            target = _frameIndex + 1;
            if (_previewAudio == null)
            {
                _playStartFrame = target;
                _playClock.Restart();
            }
        }
        SetFrame(target);
        if (target == 0) HoldPlaybackClock();
        InvalidateRender(discardPrepared: target == 0);
        await Render();
    }
    private void InvalidateRender(bool discardPrepared = true)
    {
        _version++;
        _renderCancellation?.Cancel();
        if (discardPrepared) DiscardPreparedFrames();
        _stillMotionFrame = 0;
        ExportButton.IsEnabled = false;
    }
    private void PauseVideo()
    {
        _playing = false;
        CompositionTarget.Rendering -= PlaybackRendering;
        _playClock.Stop();
        StopPreviewAudio();
        PlayButton.Content = Localization.T("Abspielen");
        // Only a completed rendering of the selected position may be exported.
        ExportButton.IsEnabled = _output != null && !_busy && !_debounce.IsEnabled && _outputInputValid;
    }
    private async Task ClearVideo()
    {
        _sourceIdentity=Guid.NewGuid().ToString("N");
        ResetPreviewNavigation();
        PauseVideo(); InvalidateRender();
        var old = _video; _video = null; _frameIndex = 0;
        _stillPath = null;
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
            var conversionLabel = _video.Info.ToneMappedToSdr ? " · HDR → SDR" : "";
            Title = $"{Path.GetFileName(path)}{conversionLabel} · Tape & Tube";
            EmptyHint.Visibility = Visibility.Collapsed;
            RefreshTimeline(); UpdatePreview();
            Schedule();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closing && !cancellation.IsCancellationRequested) Error(ex); }
        finally
        {
            if (next != null) await next.DisposeAsync();
            if (ReferenceEquals(_loadCancellation, cancellation)) _loadCancellation = null;
            RefreshVideoExportButton();
        }
    }

    private bool CanExportTimeline => TimelineInfo != null;

    private void RefreshVideoExportButton() =>
        VideoExportButton.IsEnabled = CanExportTimeline && _exportCancellation == null && _loadCancellation == null && _outputInputValid;

    private void RefreshTimeline()
    {
        RefreshEffectAvailability();
        UpdateScreenLoopControls();
        var info = TimelineInfo;
        VideoTransport.Visibility = info == null ? Visibility.Collapsed : Visibility.Visible;
        Mp4LoopOptions.Visibility = _video == null ? Visibility.Collapsed : Visibility.Visible;
        PreviewAudioChoice.IsEnabled = _video?.Info.HasAudio == true;
        if (info != null)
        {
            _updatingTransport = true;
            FrameSlider.Maximum = info.FrameCount - 1;
            _updatingTransport = false;
            SetFrame(_frameIndex);
        }
        else _frameIndex = 0;
        RefreshKeyframeAvailability();
        RefreshVideoExportButton();
        UpdateGifEstimate();
        UpdateStillMotionAvailability();
    }

    private void SetFrame(int index)
    {
        if (TimelineInfo is not VideoInfo info) return;
        _frameIndex = Math.Clamp(index, 0, info.FrameCount - 1);
        _updatingTransport = true; FrameSlider.Value = _frameIndex; _updatingTransport = false;
        UpdateTransport();
    }
    private void UpdateTransport()
    {
        if (TimelineInfo is not VideoInfo info) return;
        static string Time(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        VideoPosition.Text = Localization.F($"{Time(_frameIndex / info.FramesPerSecond)} / {Time(info.Duration)} · Bild {_frameIndex + 1} / {info.FrameCount}");
        FirstFrameButton.IsEnabled = PreviousFrameButton.IsEnabled = _frameIndex > 0;
        LastFrameButton.IsEnabled = NextFrameButton.IsEnabled = _frameIndex < info.FrameCount - 1;
    }
    private void TogglePlayback(object sender, RoutedEventArgs e)
    {
        if (TimelineInfo is not VideoInfo info) return;
        if (_playing)
        {
            PauseVideo();
            SchedulePreviewPreparation();
            // If a frame was still calculating, its completion enables export.
            return;
        }
        SetAudition(false);
        _prepareTimer.Stop();
        _playing = true;
        if (_frameIndex == info.FrameCount - 1)
        {
            SetFrame(0);
            HoldPlaybackClock();
            InvalidateRender();
            _ = Render();
        }
        else if (!_busy && !_debounce.IsEnabled && _completedPreviewVersion == _version) StartPlaybackClock();
        else HoldPlaybackClock();
        ExportButton.IsEnabled = false;
        PlayButton.Content = Localization.T("Pause");
        CompositionTarget.Rendering += PlaybackRendering;
    }
    private void SeekVideo(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingTransport || !_ready) return;
        JumpToFrame((int)Math.Round(FrameSlider.Value));
    }
    private void FirstFrame(object sender, RoutedEventArgs e) => JumpToFrame(0);
    private void LastFrame(object sender, RoutedEventArgs e)
    {
        if (TimelineInfo is VideoInfo info) JumpToFrame(info.FrameCount - 1);
    }
    private void PreviousFrame(object sender, RoutedEventArgs e) => JumpToFrame(_frameIndex - 1);
    private void NextFrame(object sender, RoutedEventArgs e) => JumpToFrame(_frameIndex + 1);
    private void JumpToFrame(int index)
    {
        if (TimelineInfo == null) return;
        PauseVideo();
        SetFrame(index);
        Schedule();
    }
}
