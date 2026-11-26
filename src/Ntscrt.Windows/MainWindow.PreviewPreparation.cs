// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Windows.Threading;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private const int MaxPreparedFrames = 12;
    private const long PreviewBufferBytes = 96L * 1024 * 1024;
    private readonly Queue<PreparedFrame> _preparedFrames = new();
    private readonly DispatcherTimer _prepareTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
    private CancellationTokenSource? _prepareCancellation;
    private Task _preparation = Task.CompletedTask;
    private long _preparedBytes;
    private int _completedPreviewVersion = -1;

    private sealed record PreparedFrame(int Index, PixelFrame Source, RenderResult Rendered)
    {
        public long Bytes => Source.Pixels.LongLength + Rendered.Frame.Pixels.LongLength;
    }

    private void SchedulePreviewPreparation()
    {
        if (_playing || _closing || _video == null || _busy || _loadCancellation != null || _exportCancellation != null
            || _completedPreviewVersion != _version || KeyframeAudition.IsChecked == true || _prepareCancellation != null) return;
        _prepareTimer.Start();
    }

    private async Task PreparePreviewAsync()
    {
        _prepareTimer.Stop();
        if (_playing || _closing || _video is not { } video || _busy || _loadCancellation != null || _exportCancellation != null
            || _completedPreviewVersion != _version || KeyframeAudition.IsChecked == true || _prepareCancellation != null
            || _source == null || _output == null) return;

        var frameBytes = _source.Pixels.LongLength + _output.Pixels.LongLength;
        var start = _preparedFrames.Count == 0 ? _frameIndex + 1 : _preparedFrames.Last().Index + 1;
        using var cancellation = new CancellationTokenSource();
        _prepareCancellation = cancellation;
        try
        {
            var settings = Settings();
            var sourceId = _sourceIdentity;
            for (var index = start; index < video.Info.FrameCount && !_playing && !_busy && _preparedFrames.Count < MaxPreparedFrames
                && _preparedBytes + frameBytes <= PreviewBufferBytes; index++)
            {
                var frame = await ComputePreviewAsync(index, null, video, settings, sourceId, video.Info, false, cancellation.Token);
                if (cancellation.IsCancellationRequested || _closing || frame.Bytes + _preparedBytes > PreviewBufferBytes) break;
                _preparedFrames.Enqueue(frame);
                _preparedBytes += frame.Bytes;
                UpdatePreparedFramesLabel();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex) { Debug.WriteLine(ex); }
        finally { _prepareCancellation = null; }
    }

    private Task<PreparedFrame> ComputePreviewAsync(int index, PixelFrame? source, VideoSource? video,
        RenderSettings settings, string sourceId, VideoInfo? timeline, bool singleFrame, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            settings = settings with { Frame = (uint)index };
            if (settings.HasHistoricalStages && video != null)
            {
                // Decode the held picture first so that a jump within its refresh interval stays forward-only.
                var rendered = await _pipeline.RenderHistoricalVideoAsync(settings, sourceId, video.Info, video.ReadFrameAsync, token)
                    .ConfigureAwait(false);
                return new PreparedFrame(index, await video.ReadFrameAsync(index, token).ConfigureAwait(false), rendered);
            }
            var decoded = video == null ? source! : await video.ReadFrameAsync(index, token).ConfigureAwait(false);
            var result = await _pipeline.RenderAsync(decoded, settings, sourceId, timeline,
                video == null || singleFrame ? null : video.ReadFrameAsync, token, singleFrame).ConfigureAwait(false);
            return new PreparedFrame(index, decoded, result);
        }, token);
    }

    private PreparedFrame? TakePreparedFrame(int index)
    {
        while (_preparedFrames.TryPeek(out var frame) && frame.Index <= index)
        {
            _preparedFrames.Dequeue();
            _preparedBytes -= frame.Bytes;
            if (frame.Index == index)
            {
                UpdatePreparedFramesLabel();
                return frame;
            }
        }
        UpdatePreparedFramesLabel();
        return null;
    }

    private void DiscardPreparedFrames()
    {
        _prepareTimer.Stop();
        _prepareCancellation?.Cancel();
        _preparedFrames.Clear();
        _preparedBytes = 0;
        UpdatePreparedFramesLabel();
    }

    private void UpdatePreparedFramesLabel() => PreparedFramesLabel.Text = _preparedFrames.Count == 0
        ? "" : Localization.F($" · {_preparedFrames.Count} im Vorschaupuffer");
}
