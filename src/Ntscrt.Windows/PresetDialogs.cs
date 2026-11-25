// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using Microsoft.Win32;

namespace Ntscrt.Windows;

internal static class PresetDialogs
{
    public static string NtscrtFolder => Path.Combine(AppContext.BaseDirectory, "presets", "NTSCRT");

    public static OpenFileDialog Open(bool screenLoop = false)
    {
        var folder = screenLoop ? Path.Combine(NtscrtFolder, "Screen Loop") : NtscrtFolder;
        var dialog = new OpenFileDialog
        {
            Title = Localization.T(screenLoop ? "Screen-Loop-Preset laden…" : "Preset laden…"),
            Filter = screenLoop ? "JSON|*.json" : "Tape & Tube / NTSCRT (*.json)|*.json",
            ClientGuid = screenLoop ? new("81185b72-1ea4-4c89-81a9-025a26e5ff3a") : new("13a14eb9-b360-4dc6-8788-2478a53e6183")
        };
        if (Directory.Exists(folder))
        {
            dialog.DefaultDirectory = folder;
            dialog.CustomPlaces = [new FileDialogCustomPlace(folder)];
        }
        return dialog;
    }
}
