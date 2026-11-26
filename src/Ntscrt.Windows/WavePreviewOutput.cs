// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;

namespace Ntscrt.Windows;

internal sealed class WavePreviewOutput : IDisposable
{
    private const uint Done = 1, TimeMilliseconds = 1, TimeSamples = 2, TimeBytes = 4;
    private static readonly uint HeaderSize = (uint)Marshal.SizeOf<WaveHeader>();
    private static readonly int LengthOffset = Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Length)).ToInt32();
    private static readonly int FlagsOffset = Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags)).ToInt32();
    private readonly List<Buffer> _buffers = new(3);
    private IntPtr _device;
    private uint _lastPosition, _lastPositionKind;
    private long _position;

    public WavePreviewOutput(int bufferSize)
    {
        var format = new WaveFormat
        {
            FormatTag = 1, Channels = 2, SamplesPerSecond = PreviewAudio.SampleRate,
            BytesPerSecond = PreviewAudio.SampleRate * PreviewAudio.BlockAlign,
            BlockAlign = PreviewAudio.BlockAlign, BitsPerSample = 16
        };
        Check(waveOutOpen(out var device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0));
        _device = device;
        try
        {
            for (var i = 0; i < 3; i++) _buffers.Add(new Buffer(_device, bufferSize));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public bool HasPending => _buffers.Any(buffer => !buffer.Available);
    public bool CanWrite => _buffers.Any(buffer => buffer.Available);

    public void Write(byte[] pcm, int length)
    {
        var buffer = _buffers.First(buffer => buffer.Available);
        Marshal.Copy(pcm, 0, buffer.Data, length);
        Marshal.WriteInt32(buffer.Header, LengthOffset, length);
        Check(waveOutWrite(_device, buffer.Header, HeaderSize));
        buffer.Submitted = true;
    }

    public long PlayedSamples()
    {
        var time = new MultimediaTime { Type = TimeSamples };
        Check(waveOutGetPosition(_device, ref time, (uint)Marshal.SizeOf<MultimediaTime>()));
        if (_lastPositionKind != time.Type)
        {
            // Choose the unit reported by the driver, which may differ from the request.
            _lastPositionKind = time.Type;
            _position = 0;
            _lastPosition = 0;
        }
        var delta = unchecked(time.Value - _lastPosition);
        if (delta <= int.MaxValue)
        {
            _position += delta;
            _lastPosition = time.Value;
        }
        return time.Type switch
        {
            TimeSamples => _position,
            TimeBytes => _position / PreviewAudio.BlockAlign,
            TimeMilliseconds => _position * PreviewAudio.SampleRate / 1000,
            _ => throw new InvalidOperationException("Unsupported audio clock.")
        };
    }

    public void Dispose()
    {
        if (_device == IntPtr.Zero) return;
        waveOutReset(_device);
        foreach (var buffer in _buffers) buffer.Dispose();
        _buffers.Clear();
        waveOutClose(_device);
        _device = IntPtr.Zero;
    }

    private static void Check(uint result)
    {
        if (result != 0) throw new InvalidOperationException($"WaveOut error {result}.");
    }

    private sealed class Buffer : IDisposable
    {
        private readonly IntPtr _device;
        public IntPtr Header { get; }
        public IntPtr Data { get; }
        public bool Submitted { get; set; }
        public bool Available => !Submitted || (Marshal.ReadInt32(Header, FlagsOffset) & Done) != 0;

        public Buffer(IntPtr device, int size)
        {
            _device = device;
            Data = Marshal.AllocHGlobal(size);
            try
            {
                Header = Marshal.AllocHGlobal((int)HeaderSize);
                Marshal.StructureToPtr(new WaveHeader { Data = Data, Length = (uint)size }, Header, false);
                Check(waveOutPrepareHeader(device, Header, HeaderSize));
            }
            catch
            {
                Marshal.FreeHGlobal(Header);
                Marshal.FreeHGlobal(Data);
                throw;
            }
        }

        public void Dispose()
        {
            if (waveOutUnprepareHeader(_device, Header, HeaderSize) != 0) return;
            Marshal.FreeHGlobal(Header);
            Marshal.FreeHGlobal(Data);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSecond, BytesPerSecond;
        public ushort BlockAlign, BitsPerSample, ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint Length, BytesRecorded;
        public IntPtr User;
        public uint Flags, Loops;
        public IntPtr Next, Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 12)]
    private struct MultimediaTime
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(4)] public uint Value;
    }

    [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr device, nuint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr device);
    [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr device);
    [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutGetPosition(IntPtr device, ref MultimediaTime time, uint size);
}
