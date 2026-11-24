// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Windows;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private NtscrtImportedLook? _importedLook;
    private string? _importedLookText;

    private void ApplyImportedLook(NtscrtImportedLook? look, string? text = null, string? path = null)
    {
        PauseVideo();
        _importedLook = look;
        _importedLookText = text;
        _frameIndex = 0;
        ImportedLookName.Text = look == null ? "" : $"NTSCRT · {Path.GetFileNameWithoutExtension(path)}";
        if (look?.Crop is { } crop) ImportedLookName.Text += $" · {crop.Ratio}";
        ImportedLookNotice.Visibility = look == null ? Visibility.Collapsed : Visibility.Visible;
        EffectControls.IsEnabled = look == null;
        ScreenLoopPanel.IsEnabled = look == null;
        RefreshTimeline();
        Schedule();
    }

    private void ExitImportedLook(object sender, RoutedEventArgs e) => ApplyImportedLook(null);
}
