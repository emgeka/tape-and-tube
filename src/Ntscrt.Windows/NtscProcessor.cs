// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace Ntscrt.Windows;

internal static class NtscProcessor
{
    private const string Dll = "ntscrt_ntsc.dll";
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern uint tapetube_host_api_version();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern nint ntscrt_defaults();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern nint ntscrt_schema();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void ntscrt_string_free(nint value);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern nint ntscrt_process(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string settings, [In, Out] byte[] pixels, nuint length, uint width, uint height, uint frame);

    private static string TakeString(nint value)
    {
        if (value == 0) throw new InvalidOperationException(Localization.T("ntsc-rs lieferte keine Daten."));
        try { return Marshal.PtrToStringUTF8(value)!; }
        finally { ntscrt_string_free(value); }
    }
    public static JsonObject Defaults()
    {
        if (tapetube_host_api_version() != 1) throw new InvalidOperationException(Localization.T("Die NTSC-Anbindung benötigt Tape & Tube Host-API 1."));
        return JsonNode.Parse(TakeString(ntscrt_defaults()))!.AsObject();
    }
    public static JsonArray Schema() => JsonNode.Parse(TakeString(ntscrt_schema()))!.AsArray();
    public static void Process(PixelFrame frame, string settings, uint index)
    {
        var error = ntscrt_process(settings, frame.Pixels, (nuint)frame.Pixels.Length, (uint)frame.Width, (uint)frame.Height, index);
        if (error != 0) throw new InvalidOperationException(TakeString(error));
    }
}
