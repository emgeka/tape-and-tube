// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;

namespace Ntscrt.Windows;

internal static class LibraNative
{
    private const string Dll = "librashader.dll";
    [StructLayout(LayoutKind.Sequential)] internal struct ParamList { public nint Parameters; public ulong Length; }
    [StructLayout(LayoutKind.Sequential)] internal struct Param { public nint Name, Description; public float Initial, Minimum, Maximum, Step; }
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nuint libra_instance_abi_version();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint libra_preset_create([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint preset);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint libra_preset_free(ref nint preset);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint libra_preset_get_runtime_params(ref nint preset, out ParamList list);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint libra_preset_free_runtime_params(ParamList list);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint libra_d3d11_filter_chain_create(ref nint preset, nint device, nint options, out nint chain);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint libra_d3d11_filter_chain_free(ref nint chain);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint libra_d3d11_filter_chain_frame(ref nint chain, nint context, nuint frame, nint input, nint output, nint viewport, nint mvp, nint options);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint libra_d3d11_filter_chain_set_param(ref nint chain, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, float value);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int libra_error_write(nint error, out nint message);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int libra_error_free_string(ref nint message);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int libra_error_free(ref nint error);
    internal static void Check(nint error)
    {
        if (error == 0) return;
        nint message = 0;
        try
        {
            libra_error_write(error, out message);
            throw new InvalidOperationException(Marshal.PtrToStringUTF8(message) ?? Localization.T("CRT-Shader konnte nicht verarbeitet werden."));
        }
        finally
        {
            if (message != 0) libra_error_free_string(ref message);
            libra_error_free(ref error);
        }
    }
}
