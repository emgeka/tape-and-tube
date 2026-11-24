// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class HdrVideoSmoke
{
    public static async Task RunAsync(string directory, Action<bool, string> check, CancellationToken token)
    {
        foreach (var (name, transfer, isHdr) in new[]
        {
            ("pq", "smpte2084", true), ("hlg", "arib-std-b67", true), ("bt2020-sdr", "bt2020-10", false)
        })
        {
            var input = Path.Combine(directory, name + "-source.mp4");
            var output = Path.Combine(directory, name + "-output.mp4");
            const string from709 = "zscale=matrixin=bt709:transferin=bt709:primariesin=bt709";
            var toSource = $"{from709}:matrix=bt2020nc:primaries=bt2020:transfer={transfer}:npl=100,format=yuv420p10le";
            await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i",
                "testsrc2=size=160x90:rate=12:duration=1", "-itsoffset", "0.25", "-f", "lavfi", "-i",
                "sine=frequency=440:sample_rate=48000:duration=0.5", "-vf", toSource,
                "-c:v", "libx265", "-x265-params", "pools=1:log-level=error", "-tag:v", "hvc1", "-c:a", "aac", input], token);

            await using var source = await VideoSource.OpenAsync(input, token);
            check(source.Info.ToneMappedToSdr == isHdr, name + ": HDR detection distinguishes transfer from wide gamut");
            var settings = new RenderSettings(false, false, "{}", "", 320, 3, 0, [], null,
                DigitalCatalog.Profile, DigitalCatalog.Find("webcam").Values);
            using var pipeline = new EffectPipeline();
            var expected = new List<byte[]>();
            PixelFrame? frameAtFour = null;
            for (var i = 0; i < source.Info.FrameCount; i++)
            {
                var pixels = await source.ReadFrameAsync(i, token);
                if (i == 4) frameAtFour = pixels;
                var frame = await pipeline.RenderAsync(pixels, settings with { Frame = (uint)i }, name, source.Info,
                    source.ReadFrameAsync, token);
                expected.Add(frame.Frame.Pixels);
            }
            var seek = await source.ReadFrameAsync(4, token);
            check(seek.Pixels.SequenceEqual(frameAtFour!.Pixels) && seek.Pixels.Where((_, i) => i % 4 != 3).Distinct().Count() > 40,
                name + ": converted picture is visible and seeking matches sequential color conversion");
            var equal = true;
            var result = await VideoExporter.ExportAsync(input, output, settings, cancellation: token, overwrite: true,
                frameObserver: (i, frame) => equal &= frame.Pixels.SequenceEqual(expected[i]));
            check(equal && result.Frames == 12, name + ": preview and export agree for all twelve converted frames");

            var metadata = await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-show_entries",
                "stream=codec_type,codec_name,color_space,color_primaries,color_transfer", "-of", "json", output], token);
            using var probe = JsonDocument.Parse(metadata);
            var streams = probe.RootElement.GetProperty("streams");
            var video = streams.EnumerateArray().Single(s => s.GetProperty("codec_type").GetString() == "video");
            check(video.GetProperty("color_space").GetString() == "bt709"
                && video.GetProperty("color_primaries").GetString() == "bt709"
                && video.GetProperty("color_transfer").GetString() == "bt709", name + ": export is tagged as BT.709 SDR");
            var audio = streams.EnumerateArray().Single(s => s.GetProperty("codec_type").GetString() == "audio");
            check(audio.GetProperty("codec_name").GetString() == "aac", name + ": original audio is present in export");

            using var decoder = MediaTools.Start("ffmpeg", ["-v", "error", "-i", output, "-map", "0:a:0", "-ar", "48000", "-f", "s16le", "pipe:1"]);
            using var stop = token.Register(() => MediaTools.Stop(decoder));
            using var pcm = new MemoryStream();
            await decoder.StandardOutput.BaseStream.CopyToAsync(pcm, token);
            await decoder.WaitForExitAsync(token);
            var samples = pcm.ToArray();
            static double AverageMagnitude(byte[] pcm, int start, int end) =>
                Enumerable.Range(start, end - start).Average(i => Math.Abs((int)BitConverter.ToInt16(pcm, i * 2)));
            check(decoder.ExitCode == 0 && samples.Length >= 48000 &&
                AverageMagnitude(samples, 2400, 7200) < 10 && AverageMagnitude(samples, 16800, 21600) > 500,
                name + ": the quarter-second audio offset is preserved");
        }
    }
}
