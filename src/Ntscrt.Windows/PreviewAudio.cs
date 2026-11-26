// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.IO;

namespace Ntscrt.Windows;

internal sealed class PreviewAudio
{
    internal const int SampleRate = 48_000, BlockAlign = 4;
    private const int BlockSamples = SampleRate / 50;
    private const int LeadSamples = SampleRate * 60 / 1000;
    private readonly VideoInfo _info;
    private readonly long _startSample, _sampleCount;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private long _allowed, _played;
    private volatile bool _failed;

    public bool Failed => _failed;
    public double Elapsed => Interlocked.Read(ref _played) / (double)SampleRate;

    public PreviewAudio(VideoInfo info, int startFrame, Task previousPlayback)
    {
        _info = info;
        _startSample = FrameSample(info, startFrame);
        _sampleCount = FrameSample(info, info.FrameCount) - _startSample;
        Present(startFrame);
        _worker = Task.Run(() => PlayAsync(previousPlayback));
    }

    public void Present(int frame) => Interlocked.Exchange(ref _allowed,
        Math.Min(_sampleCount, FrameSample(_info, frame + 1) - _startSample + LeadSamples));

    public async Task StopAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        await _worker.ConfigureAwait(false);
        _stop.Dispose();
    }

    internal static long FrameSample(VideoInfo info, int frame) =>
        (long)Math.Round(frame * (double)SampleRate / info.FramesPerSecond);

    internal static Process StartDecoder(VideoInfo info, long startSample, long endSample)
    {
        var filter = FormattableString.Invariant(
            $"asetpts=PTS-({info.StartTime:R})/TB,aresample={SampleRate}:async=1:first_pts=0,apad=whole_len={endSample},atrim=start_sample={startSample}:end_sample={endSample},asetpts=PTS-STARTPTS");
        return MediaTools.Start("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-copyts",
            "-protocol_whitelist", "file,pipe", "-i", info.Path, "-map", "0:a:0", "-vn", "-sn", "-dn",
            "-af", filter, "-ac", "2", "-f", "s16le", "pipe:1"]);
    }

    private async Task PlayAsync(Task previousPlayback)
    {
        var token = _stop.Token;
        try
        {
            await previousPlayback.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            using var output = new WavePreviewOutput(BlockSamples * BlockAlign);
            using var decoder = StartDecoder(_info, _startSample, _startSample + _sampleCount);
            using var registration = token.Register(() => MediaTools.Stop(decoder));
            var errors = decoder.StandardError.ReadToEndAsync();
            try
            {
                var pcm = new byte[BlockSamples * BlockAlign];
                long queued = 0;
                while (queued < _sampleCount || output.HasPending)
                {
                    token.ThrowIfCancellationRequested();
                    var played = Math.Min(queued, output.PlayedSamples());
                    Interlocked.Exchange(ref _played, Math.Max(Interlocked.Read(ref _played), played));
                    var count = (int)Math.Min(BlockSamples, _sampleCount - queued);
                    // The visible frame grants a short horizon. Under load the device drains
                    // its queue and its clock stops until the video presents another frame.
                    if (count > 0 && queued + count <= Interlocked.Read(ref _allowed) && output.CanWrite)
                    {
                        await decoder.StandardOutput.BaseStream.ReadExactlyAsync(pcm.AsMemory(0, count * BlockAlign), token)
                            .ConfigureAwait(false);
                        output.Write(pcm, count * BlockAlign);
                        queued += count;
                    }
                    else await Task.Delay(6, token).ConfigureAwait(false);
                }
                await decoder.WaitForExitAsync(token).ConfigureAwait(false);
                if (decoder.ExitCode != 0) throw new InvalidDataException(await errors.ConfigureAwait(false));
                Interlocked.Exchange(ref _played, _sampleCount);
            }
            finally
            {
                MediaTools.Stop(decoder);
                await errors.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { _failed = !token.IsCancellationRequested; }
    }
}
