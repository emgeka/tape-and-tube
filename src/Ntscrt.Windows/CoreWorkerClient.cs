// SPDX-License-Identifier: GPL-3.0-only
// Pipe client deliberately uses no WPF/native graphics types.
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ntscrt.Windows;

internal sealed record CorePixels(int Width, int Height, byte[] Rgba);
internal sealed class CoreWorkerClient : IDisposable
{
    private const int MaxHeader = 1_048_576, MaxData = 96_000_000;
    private readonly SemaphoreSlim _gate = new(1);
    private Process? _process;
    private Task<string>? _errors;
    private JsonObject? _configuration;
    private JsonArray _parameters = [];
    private long _id;
    public int? ProcessId => _process is { HasExited: false } p ? p.Id : null;
    public JsonObject? Adapter { get; private set; }
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task StartAsync(string executable, CancellationToken token)
    {
        if (_process is { HasExited: false }) return;
        Stop();
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--worker");
        // An explicit backend/CPU override remains visible in returned diagnostics.
        _process = Process.Start(info) ?? throw new IOException("Core worker could not be started.");
        _errors = _process.StandardError.ReadToEndAsync();
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(token);
        startup.CancelAfter(TimeSpan.FromSeconds(30));
        var (hello, bytes) = await ReceiveAsync(startup.Token).ConfigureAwait(false);
        if (hello["op"]?.ToString() != "hello" || hello["apiVersion"]?.GetValue<int>() != 1 || bytes.Length != 0)
            throw new InvalidDataException("Unsupported core worker protocol.");
        if (hello["rendererProfile"]?.ToString()!=DigitalCatalog.Profile || hello["pixelFormat"]?.ToString()!="RGBA8"
            || hello["description"]?["sources"]?["ntscrt"]?.ToString() != DigitalCatalog.Revision
            || hello["description"]?["sources"]?["librashader"]?.ToString() != "76462c030b75c4f2d56e5386c3d4d7d1128318b8"
            || hello["description"]?["sources"]?["ntscRs"]?.ToString() != "add90f5bf1bf7e3c573e4e945a16f44a82941b51")
            throw new InvalidDataException("The core worker does not match this renderer profile.");
    }
    private async Task SendAsync(JsonObject header, byte[] data, CancellationToken token)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(header);
        if (json.Length > MaxHeader || data.Length > MaxData) throw new InvalidDataException("Core packet exceeds limits.");
        var prefix = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, json.Length);
        BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(4), data.Length);
        var stream = _process!.StandardInput.BaseStream;
        await stream.WriteAsync(prefix, token).ConfigureAwait(false);
        await stream.WriteAsync(json, token).ConfigureAwait(false);
        await stream.WriteAsync(data, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
    private async Task<(JsonObject Header, byte[] Data)> ReceiveAsync(CancellationToken token)
    {
        var stream = _process!.StandardOutput.BaseStream;
        var prefix = new byte[8]; await stream.ReadExactlyAsync(prefix, token).ConfigureAwait(false);
        var h = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        var d = BinaryPrimitives.ReadInt32LittleEndian(prefix.AsSpan(4));
        if (h < 1 || h > MaxHeader || d < 0 || d > MaxData) throw new InvalidDataException("Invalid core packet length.");
        var header = new byte[h]; var data = new byte[d];
        await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        await stream.ReadExactlyAsync(data, token).ConfigureAwait(false);
        return ((JsonNode.Parse(header) as JsonObject) ?? throw new InvalidDataException("Invalid core header."), data);
    }
    private static void Check(JsonObject header, long id, string operation)
    {
        if (header["id"]?.GetValue<long>() != id) throw new InvalidDataException("Core response belongs to another request.");
        if (header["op"]?.ToString() == "error") throw new InvalidOperationException(header["message"]?.ToString());
        if (header["op"]?.ToString() != operation) throw new InvalidDataException("Unexpected core response.");
    }
    public async Task<(CorePixels Pixels, JsonArray Parameters)> RenderAsync(string executable, JsonObject configuration,
        int index, CorePixels current, Func<int, CancellationToken, Task<CorePixels>>? history, CancellationToken token, bool singleFrame)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            using var cancel = token.Register(Stop);
            token.ThrowIfCancellationRequested();
            await StartAsync(executable, token).ConfigureAwait(false);
            if (!JsonNode.DeepEquals(_configuration, configuration))
            {
                var configurationId = ++_id;
                await SendAsync(new() { ["op"] = "configure", ["id"] = configurationId, ["configuration"] = configuration.DeepClone() }, [], token).ConfigureAwait(false);
                var (configured, empty) = await ReceiveAsync(token).ConfigureAwait(false);
                Check(configured, configurationId, "configured");
                if (empty.Length != 0) throw new InvalidDataException("Unexpected configure payload.");
                if (configured["adapter"] is JsonObject adapter) Adapter = (JsonObject)adapter.DeepClone();
                _parameters = configured["parameters"]?.DeepClone() as JsonArray ?? [];
                _configuration = configuration;
            }
            var id = ++_id;
            await SendAsync(new() { ["op"] = singleFrame ? "previewFrame" : "render", ["id"] = id, ["frame"] = index }, current.Rgba, token).ConfigureAwait(false);
            while (true)
            {
                var (response, bytes) = await ReceiveAsync(token).ConfigureAwait(false);
                var op = response["op"]?.ToString();
                if (response["id"]?.GetValue<long>() != id) throw new InvalidDataException("Stale core response.");
                if (op == "progress") continue;
                if (op == "needFrame")
                {
                    if (response["sourceId"]?.ToString() != configuration["sourceId"]?.ToString()) throw new InvalidDataException("History source changed.");
                    var prior = response["frame"]!.GetValue<int>();
                    var count = configuration["frames"]!.GetValue<int>();
                    if (prior < 0 || prior >= count || history == null) throw new InvalidDataException("The core requires unavailable source history.");
                    var source = await history(prior, token).ConfigureAwait(false);
                    if (source.Width != current.Width || source.Height != current.Height || source.Rgba.Length != current.Rgba.Length)
                        throw new InvalidDataException("Invalid source history dimensions.");
                    await SendAsync(new() { ["op"] = "sourceFrame", ["id"] = id, ["frame"] = prior }, source.Rgba, token).ConfigureAwait(false);
                    continue;
                }
                Check(response, id, "frame");
                if (response["sourceId"]?.ToString() != configuration["sourceId"]?.ToString() || response["frame"]?.GetValue<int>() != index)
                    throw new InvalidDataException("Core frame belongs to another source or time.");
                var width = response["width"]!.GetValue<int>(); var height = response["height"]!.GetValue<int>();
                if (width != configuration["outputWidth"]!.GetValue<int>() || height != configuration["outputHeight"]!.GetValue<int>()
                    || bytes.Length != checked(width * height * 4)) throw new InvalidDataException("Invalid core output shape.");
                return (new(width, height, bytes), _parameters);
            }
        }
        catch (Exception ex)
        {
            var detail = _errors is { IsCompletedSuccessfully: true } ? _errors.Result.Trim() : "";
            Stop();
            token.ThrowIfCancellationRequested();
            if (ex is EndOfStreamException or IOException && detail.Length > 0)
                throw new IOException("Core worker stopped: " + detail[..Math.Min(detail.Length, 1500)], ex);
            throw;
        }
        finally { _gate.Release(); }
    }
    public void Stop()
    {
        _configuration = null;
        _parameters = [];
        var process = Interlocked.Exchange(ref _process, null);
        if (process == null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        finally { process.Dispose(); }
        Adapter = null;
    }
    public void Dispose() => Stop();
}
