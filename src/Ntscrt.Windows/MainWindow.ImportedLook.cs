// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Windows;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private NtscrtImportedLook? _importedLook;
    private string? _importedLookText;
    private bool _basicOriginalActive;

    private void ApplyImportedLook(NtscrtImportedLook? look, string? text = null, string? path = null,
        bool fromBasic = false, bool keepPlayback = false)
    {
        if (!keepPlayback) PauseVideo();
        var leavingBasic = look == null && _basicOriginalActive;
        _basicOriginalActive = look != null && fromBasic;
        _importedLook = look;
        _importedLookText = text;
        if (_video == null || (!fromBasic && !leavingBasic)) _frameIndex = 0;
        ImportedLookName.Text = look == null ? "" : $"NTSCRT · {Path.GetFileNameWithoutExtension(path)}";
        if (look?.Crop is { } crop) ImportedLookName.Text += $" · {crop.Ratio}";
        ImportedLookHint.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty,
            _basicOriginalActive ? "BasicOriginalHint" : "ImportedLookHintLabel");
        ImportedLookNotice.Visibility = look == null ? Visibility.Collapsed : Visibility.Visible;
        EffectControls.IsEnabled = look == null;
        BasicLooks.IsEnabled = look == null || _basicOriginalActive;
        RefreshTimeline();
        Schedule();
    }

    private void ExitImportedLook(object sender, RoutedEventArgs e) => ApplyImportedLook(null);
}
