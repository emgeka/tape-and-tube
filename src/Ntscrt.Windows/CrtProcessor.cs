// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using static Ntscrt.Windows.LibraNative;

namespace Ntscrt.Windows;

internal sealed record ShaderParameter(string Name, string Label, float Initial, float Minimum, float Maximum, float Step);

// Accessed only by EffectPipeline's serialized worker, including disposal.
internal sealed class CrtProcessor : IDisposable
{
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private nint _chain;
    private string? _path;
    public IReadOnlyList<ShaderParameter> Parameters { get; private set; } = [];

    public CrtProcessor()
    {
        if (libra_instance_abi_version() != 2) throw new InvalidOperationException(Localization.T("Unpassende librashader-Schnittstelle (ABI 2 erforderlich)."));
        D3D11CreateDevice(nint.Zero, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_0], out _device, out _, out _context).CheckError();
    }

    public void Load(string path)
    {
        if (_path == path) return;
        Check(libra_preset_create(path, out var preset));
        nint next = 0;
        try
        {
            Check(libra_preset_get_runtime_params(ref preset, out var list));
            var parameters = new List<ShaderParameter>();
            try
            {
                for (ulong i = 0; i < list.Length; i++)
                {
                    var p = Marshal.PtrToStructure<Param>(list.Parameters + checked((int)i * Marshal.SizeOf<Param>()));
                    parameters.Add(new(Marshal.PtrToStringUTF8(p.Name)!, Marshal.PtrToStringUTF8(p.Description)!, p.Initial, p.Minimum, p.Maximum, p.Step));
                }
            }
            finally { Check(libra_preset_free_runtime_params(list)); }
            Check(libra_d3d11_filter_chain_create(ref preset, _device.NativePointer, 0, out next));
            // librashader consumes the preset on success.
            preset = 0;
            if (_chain != 0) Check(libra_d3d11_filter_chain_free(ref _chain));
            _chain = next; next = 0;
            _path = path;
            Parameters = parameters;
        }
        finally
        {
            if (preset != 0) Check(libra_preset_free(ref preset));
            if (next != 0) Check(libra_d3d11_filter_chain_free(ref next));
        }
    }

    public unsafe PixelFrame Render(PixelFrame input, int width, int height, uint frame, IReadOnlyDictionary<string, float> values)
    {
        foreach (var p in Parameters)
            Check(libra_d3d11_filter_chain_set_param(ref _chain, p.Name,
                values.TryGetValue(p.Name, out var value) && float.IsFinite(value) ? Math.Clamp(value, p.Minimum, p.Maximum) : p.Initial));

        fixed (byte* bytes = input.Pixels)
        {
            using var source = _device.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)input.Width, Height = (uint)input.Height, MipLevels = 1, ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource
            }, new SubresourceData((nint)bytes, (uint)(input.Width * 4)));
            using var view = _device.CreateShaderResourceView(source);
            var desc = new Texture2DDescription
            {
                Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default, BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource
            };
            using var output = _device.CreateTexture2D(desc);
            using var target = _device.CreateRenderTargetView(output);
            Check(libra_d3d11_filter_chain_frame(ref _chain, _context.NativePointer, frame, view.NativePointer, target.NativePointer, 0, 0, 0));
            _context.ClearState();
            desc.Usage = ResourceUsage.Staging; desc.BindFlags = BindFlags.None; desc.CPUAccessFlags = CpuAccessFlags.Read;
            using var staging = _device.CreateTexture2D(desc);
            _context.CopyResource(staging, output);
            var mapped = _context.Map(staging, 0, MapMode.Read);
            try
            {
                var result = new byte[checked(width * height * 4)];
                for (var y = 0; y < height; y++)
                    Marshal.Copy(mapped.DataPointer + checked((int)(y * mapped.RowPitch)), result, y * width * 4, width * 4);
                for (int p = 3; p < result.Length; p += 4) result[p] = 255;
                return new(width, height, result);
            }
            finally { _context.Unmap(staging, 0); }
        }
    }

    public void Dispose()
    {
        if (_chain != 0) Check(libra_d3d11_filter_chain_free(ref _chain));
        _context.ClearState(); _context.Flush();
        _context.Dispose(); _device.Dispose();
    }
}
