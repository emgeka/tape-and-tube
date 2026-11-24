// SPDX-License-Identifier: GPL-3.0-only
// Independent managed consumer of ../digital-host/include/tapetube_digital.h.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    private const int Width = 64, Height = 48, Stride = Width * 4 + 13, Frames = 42;
    private static readonly List<string> Checks = [];
    private static readonly List<string> ExpectedErrors = [];

    private static int Main(string[] args)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            if (args.Length != 1) throw new ArgumentException("Usage: TapeTube.DigitalAbiSmoke <absolute native library path>");
            if (!Path.IsPathFullyQualified(args[0])) throw new ArgumentException("Native library path must be absolute.");
            Require(IntPtr.Size == 8, "64-bit consumer");
            // Exact declarations/layout from the version-1 C header, not the Rust internals.
            Require(Marshal.SizeOf<FrameContext>() == 56, "C frame context size 56");
            Require(Marshal.SizeOf<FrameView>() == 40, "C history frame view size 40");
            using var api = new NativeApi(args[0]);
            Require(api.Version() == 1, "host API 1");
            using var metadata = JsonDocument.Parse(api.Take(api.Metadata()));
            using var defaults = JsonDocument.Parse(api.Take(api.Defaults()));
            Require(metadata.RootElement.GetProperty("hostApi").GetUInt32() == 1, "metadata API");
            Require(metadata.RootElement.GetProperty("upstreamRevision").GetString() == "0ab60bf952b09a0c41ea4290979f23dadd963614", "pinned upstream revision");
            Require(metadata.RootElement.GetProperty("parameters").GetArrayLength() == 32, "32 upstream parameter descriptors");
            var values = defaults.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());
            Require(values.Count == 32, "32 default settings");
            var cases = new List<object>();
            var stateful = Settings(values, ("codec", 0), ("frame_rate", 6), ("keyframe_interval", .5), ("datamosh", 1), ("data_loss", .25), ("fixed_bitrate", .3));
            cases.Add(RunCase(api, "dct-independent", Settings(values, ("codec", 0), ("frame_rate", 12), ("keyframe_interval", 0))));
            cases.Add(RunCase(api, "video1-predicted", Settings(values, ("codec", 1), ("frame_rate", 12), ("keyframe_interval", 1.25), ("data_loss", .15))));
            cases.Add(RunCase(api, "dct-datamosh-history", stateful));
            RunErrors(api, stateful);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true, hostApi = 1, library = Path.GetFullPath(args[0]),
                operatingSystem = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                frame = new { width = Width, height = Height, stride = Stride, sourceFps = "12/1", count = Frames, format = "RGBA8" },
                checks = Checks.Count, expectedErrors = ExpectedErrors, cases, elapsedMilliseconds = clock.ElapsedMilliseconds
            }));
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = error.ToString(), completedChecks = Checks.Count, expectedErrors = ExpectedErrors }));
            return 1;
        }
    }

    private static Dictionary<string, double> Settings(Dictionary<string, double> defaults, params (string Key, double Value)[] overrides)
    {
        var settings = new Dictionary<string, double>(defaults)
        {
            ["fields"] = 0, ["quality"] = .57, ["sensor_noise"] = .1,
            ["low_light_noise"] = .15, ["aliasing"] = .1
        };
        foreach (var (key, value) in overrides) settings[key] = value;
        return settings;
    }

    private static object RunCase(NativeApi api, string name, Dictionary<string, double> settings)
    {
        var clock = Stopwatch.StartNew();
        using var sequential = new Session(api, settings);
        using var seeking = new Session(api, settings);
        using var provider = new ProviderLease();
        Require(sequential.Handle != seeking.Handle, name + ": distinct concurrent instances");
        var reference = new byte[Frames][];
        var compared = 0;
        var changed = false;
        for (var index = 0; index < Frames; index++)
        {
            reference[index] = Render(api, sequential.Handle, index, provider);
            changed |= !reference[index].AsSpan().SequenceEqual(Source(index));
            if (index % 7 == 0)
            {
                Equal(reference[index], Render(api, seeking.Handle, index, provider), name + ": interleaved instance " + index);
                compared++;
            }
        }
        Require(changed, name + ": visible pixel processing");
        // Compare fresh original frames, including after backward seek, end->start and skips.
        int[] indices = [0, 11, 5, 24, 6, 41, 0, 1, 2, 18, 39, 40, 4, 0, 41];
        foreach (var index in indices)
        {
            Equal(reference[index], Render(api, seeking.Handle, index, provider), name + ": nonsequential " + index);
            compared++;
        }
        api.Check(api.Reset(seeking.Handle));
        foreach (var index in Enumerable.Range(0, 11).Select(i => i * 4).Append(41))
        {
            Equal(reference[index], Render(api, seeking.Handle, index, provider), name + ": skipped display " + index);
            compared++;
        }
        foreach (var index in Enumerable.Range(0, 7))
        {
            Equal(reference[index], Render(api, seeking.Handle, index, provider), name + ": loop " + index);
            compared++;
        }
        api.Check(api.Reset(seeking.Handle));
        Equal(reference[41], Render(api, seeking.Handle, 41, provider), name + ": reset replay");
        compared++;
        // Resetting one live instance must not reset the other one's stream.
        api.Check(api.Reset(sequential.Handle));
        Equal(reference[40], Render(api, seeking.Handle, 40, provider), name + ": instance isolation");
        compared++;
        Require(provider.State.Exceptions.Count == 0, name + ": managed provider never unwound");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var frame in reference) hash.AppendData(frame);
        return new
        {
            name, sequentialFrames = Frames, exactComparisons = compared,
            realSourceHistoryRequests = provider.State.Calls, distinctRequestedSourceFrames = provider.State.Indices.Count,
            paddingAndTailPreserved = true, sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            elapsedMilliseconds = clock.ElapsedMilliseconds
        };
    }

    private static void RunErrors(NativeApi api, Dictionary<string, double> settings)
    {
        var badJson = Encoding.UTF8.GetBytes("{bad JSON");
        using (var pinned = new Pin(badJson))
        {
            Expect(api, api.Create(pinned.Pointer, (nuint)badJson.Length, out var invalid), "invalid JSON");
            Require(invalid == 0, "invalid create clears handle");
        }
        using var valid = new Session(api, settings);
        using var provider = new ProviderLease();
        var source = Source(41);
        using var pixels = new Pin(source);
        var context = Context(41);
        var bad = context; bad.Format = 9;
        Expect(api, api.Process(valid.Handle, in bad, pixels.Pointer, (nuint)source.Length, provider.Pointer, provider.User), "unsupported pixel format");
        bad = context; bad.FrameIndex = -1;
        Expect(api, api.Process(valid.Handle, in bad, pixels.Pointer, (nuint)source.Length, provider.Pointer, provider.User), "negative frame index");
        bad = context; bad.StructSize--;
        Expect(api, api.Process(valid.Handle, in bad, pixels.Pointer, (nuint)source.Length, provider.Pointer, provider.User), "invalid context size");
        bad = context; bad.FpsDen = 0;
        Expect(api, api.Process(valid.Handle, in bad, pixels.Pointer, (nuint)source.Length, provider.Pointer, provider.User), "zero frame-rate denominator");
        Expect(api, api.Process(valid.Handle, in context, pixels.Pointer, (nuint)(Stride * Height - 1), provider.Pointer, provider.User), "short pixel buffer");
        Equal(Source(41), source, "validation errors preserve original pixels");
        api.Check(api.Reset(valid.Handle));
        Expect(api, api.Process(valid.Handle, in context, pixels.Pointer, (nuint)source.Length, 0, 0), "missing genuine source history");
        foreach (var mode in new[] { ProviderMode.Missing, ProviderMode.InvalidFormat, ProviderMode.DoubleSink, ProviderMode.ManagedFailure })
        {
            api.Check(api.Reset(valid.Handle));
            provider.State.Mode = mode;
            var before = (byte[])source.Clone();
            Expect(api, api.Process(valid.Handle, in context, pixels.Pointer, (nuint)source.Length, provider.Pointer, provider.User), "provider " + mode);
            Equal(before, source, "provider failure is transactional: " + mode);
        }
        Require(provider.State.CaughtManagedFailures > 0, "managed callback exceptions caught before native boundary");
        provider.State.Mode = ProviderMode.Valid;
        var recovered = Render(api, valid.Handle, 41, provider);
        using var fresh = new Session(api, settings);
        Equal(Render(api, fresh.Handle, 41, provider), recovered, "session recovers after callback failure");
        var destroyed = valid.Handle;
        valid.Dispose();
        Expect(api, api.Destroy(destroyed), "destroy twice");
        Expect(api, api.Reset(destroyed), "reset destroyed instance");
        Expect(api, api.Process(destroyed, in context, pixels.Pointer, (nuint)source.Length, provider.Pointer, provider.User), "process destroyed instance");
    }

    private static byte[] Render(NativeApi api, ulong handle, int index, ProviderLease provider)
    {
        var bytes = Source(index);
        using var pinned = new Pin(bytes);
        var context = Context(index);
        api.Check(api.Process(handle, in context, pinned.Pointer, (nuint)bytes.Length, provider.Pointer, provider.User));
        for (var y = 0; y < Height; y++)
        {
            for (var x = Width * 4; x < Stride; x++)
                if (bytes[y * Stride + x] != 0xa5) throw new InvalidOperationException("Native processing wrote stride padding.");
            for (var x = 0; x < Width; x++)
                if (bytes[y * Stride + x * 4 + 3] != 255) throw new InvalidOperationException("RGBA output alpha is not opaque.");
        }
        Require(bytes.AsSpan(Stride * Height).IndexOfAnyExcept((byte)0xcc) < 0, "tail sentinel preserved");
        return bytes;
    }

    private static FrameContext Context(int index) => new()
    {
        StructSize = (uint)Marshal.SizeOf<FrameContext>(), Format = 1, Width = Width, Height = Height,
        Stride = Stride, CaptureWidth = Width, CaptureHeight = Height,
        FrameIndex = index, FpsNum = 12, FpsDen = 1, Flags = 0, Reserved = 0
    };

    private static byte[] Source(long index)
    {
        if (index < 0 || index >= Frames) throw new ArgumentOutOfRangeException(nameof(index));
        var bytes = new byte[Stride * Height + 31];
        bytes.AsSpan(0, Stride * Height).Fill(0xa5);
        bytes.AsSpan(Stride * Height).Fill(0xcc);
        for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++)
        {
            var at = y * Stride + x * 4;
            var scene = index >= 18 ? 93 : 0;
            bytes[at] = (byte)((x * 5 + y * 3 + index * 7 + scene) & 255);
            bytes[at + 1] = (byte)((y * 6 + index * 11 + (x / 5 % 2) * 40) & 255);
            bytes[at + 2] = (byte)(((x ^ y) * 9 + index * 3 + scene * 2) & 255);
            if ((x + index * 3) % Width < 12 && y is >= 11 and < 29)
            { bytes[at] = 240; bytes[at + 1] = 31; bytes[at + 2] = 67; }
            bytes[at + 3] = 255;
        }
        return bytes;
    }

    private static void Equal(byte[] expected, byte[] actual, string name) => Require(expected.AsSpan().SequenceEqual(actual), name);
    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Check failed: " + name);
        Checks.Add(name);
    }
    private static void Expect(NativeApi api, nint error, string name)
    {
        Require(error != 0, "expected native error: " + name);
        var message = api.Take(error);
        Require(!string.IsNullOrWhiteSpace(message), "owned UTF-8 error: " + name);
        ExpectedErrors.Add(name + ": " + message);
    }

    [StructLayout(LayoutKind.Sequential)] private struct FrameContext
    {
        public uint StructSize, Format, Width, Height;
        public nuint Stride;
        public uint CaptureWidth, CaptureHeight;
        public long FrameIndex;
        public uint FpsNum, FpsDen, Flags, Reserved;
    }
    [StructLayout(LayoutKind.Sequential)] private struct FrameView
    {
        public uint StructSize, Format, Width, Height;
        public nuint Stride;
        public nint Pixels;
        public nuint Length;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint VersionFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint TextFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint CreateFn(nint json, nuint length, out ulong handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint ProcessFn(ulong handle, in FrameContext context, nint pixels, nuint length, nint provider, nint user);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint HandleFn(ulong handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeFn(nint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ProviderFn(nint user, long index, nint sink, nint sinkUser);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SinkFn(nint user, in FrameView frame);

    private sealed class NativeApi : IDisposable
    {
        private readonly nint _library;
        public readonly VersionFn Version;
        public readonly TextFn Metadata, Defaults;
        public readonly CreateFn Create;
        public readonly ProcessFn Process;
        public readonly HandleFn Reset, Destroy;
        private readonly FreeFn _free;
        public NativeApi(string path)
        {
            _library = NativeLibrary.Load(path);
            try
            {
                Version = Export<VersionFn>("tt_digital_host_api_version");
                Metadata = Export<TextFn>("tt_digital_metadata_json"); Defaults = Export<TextFn>("tt_digital_defaults_json");
                Create = Export<CreateFn>("tt_digital_create"); Process = Export<ProcessFn>("tt_digital_process");
                Reset = Export<HandleFn>("tt_digital_reset"); Destroy = Export<HandleFn>("tt_digital_destroy");
                _free = Export<FreeFn>("tt_digital_string_free");
            }
            catch { NativeLibrary.Free(_library); throw; }
        }
        private T Export<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, name));
        public string Take(nint value)
        {
            if (value == 0) throw new InvalidOperationException("Native text export returned null.");
            try { return Marshal.PtrToStringUTF8(value) ?? throw new InvalidOperationException("Invalid native UTF-8 string."); }
            finally { _free(value); }
        }
        public void Check(nint error) { if (error != 0) throw new InvalidOperationException(Take(error)); }
        public void Dispose() => NativeLibrary.Free(_library);
    }
    private sealed class Session : IDisposable
    {
        private readonly NativeApi _api;
        public ulong Handle { get; private set; }
        public Session(NativeApi api, Dictionary<string, double> values)
        {
            _api = api;
            var json = JsonSerializer.SerializeToUtf8Bytes(values);
            using var pinned = new Pin(json);
            api.Check(api.Create(pinned.Pointer, (nuint)json.Length, out var handle));
            if (handle == 0) throw new InvalidOperationException("Create returned a null handle.");
            Handle = handle;
        }
        public void Dispose() { if (Handle != 0) { _api.Check(_api.Destroy(Handle)); Handle = 0; } }
    }
    private sealed class Pin : IDisposable
    {
        private GCHandle _handle;
        public nint Pointer => _handle.AddrOfPinnedObject();
        public Pin(byte[] bytes) => _handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        public void Dispose() => _handle.Free();
    }
    private enum ProviderMode { Valid, Missing, InvalidFormat, DoubleSink, ManagedFailure }
    private sealed class ProviderState
    {
        public ProviderMode Mode;
        public int Calls, CaughtManagedFailures;
        public readonly HashSet<long> Indices = [];
        public readonly List<string> Exceptions = [];
    }
    private sealed class ProviderLease : IDisposable
    {
        public readonly ProviderState State = new();
        private GCHandle _handle;
        private readonly ProviderFn _callback = Provide;
        public nint User => GCHandle.ToIntPtr(_handle);
        public nint Pointer => Marshal.GetFunctionPointerForDelegate(_callback);
        public ProviderLease() => _handle = GCHandle.Alloc(State);
        public void Dispose() { GC.KeepAlive(_callback); _handle.Free(); }
        private static int Provide(nint user, long index, nint sinkPointer, nint sinkUser)
        {
            ProviderState? state = null;
            try
            {
                state = (ProviderState)GCHandle.FromIntPtr(user).Target!;
                state.Calls++; state.Indices.Add(index);
                if (state.Mode == ProviderMode.Missing) return 0;
                if (state.Mode == ProviderMode.ManagedFailure) throw new InvalidDataException("Intentional managed provider failure");
                var bytes = Source(index); // Always the genuine source frame requested by the codec.
                using var pixels = new Pin(bytes);
                var view = new FrameView
                {
                    StructSize = (uint)Marshal.SizeOf<FrameView>(), Format = state.Mode == ProviderMode.InvalidFormat ? 99u : 1u,
                    Width = Width, Height = Height, Stride = Stride, Pixels = pixels.Pointer, Length = (nuint)bytes.Length
                };
                var sink = Marshal.GetDelegateForFunctionPointer<SinkFn>(sinkPointer);
                var accepted = sink(sinkUser, in view);
                if (state.Mode == ProviderMode.DoubleSink) { sink(sinkUser, in view); return 1; }
                return accepted;
            }
            catch (Exception error)
            {
                if (state != null)
                {
                    if (state.Mode == ProviderMode.ManagedFailure) state.CaughtManagedFailures++;
                    else state.Exceptions.Add(error.ToString());
                }
                return 0; // No managed exception crosses the C callback boundary.
            }
        }
    }
}
