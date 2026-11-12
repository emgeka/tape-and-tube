// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private OutputSettings? _outputSettings;
    private bool _syncOutputUi, _outputInputValid = true;

    private void OutputChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncOutputUi) return;
        CustomOutputFields.Visibility = OutputChoice.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        OutputFitChoice.IsEnabled = OutputChoice.SelectedIndex != 0;
        ScaleChoice.IsEnabled = OutputChoice.SelectedIndex == 0;
        ApplyOutputChoice();
    }
    private void CustomOutputChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready && !_syncOutputUi && OutputChoice.SelectedIndex == 4) ApplyOutputChoice();
    }
    private void OutputFitChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_syncOutputUi) ApplyOutputChoice();
    }
    private void ApplyOutputChoice()
    {
        try
        {
            OutputSettings? next = OutputChoice.SelectedIndex switch
            {
                0 => null,
                1 => new(1280, 720),
                2 => new(1920, 1080),
                3 => new(3840, 2160),
                _ => new(int.Parse(OutputWidth.Text, NumberStyles.None, CultureInfo.InvariantCulture),
                    int.Parse(OutputHeight.Text, NumberStyles.None, CultureInfo.InvariantCulture))
            };
            if (next != null)
            {
                next = next with { Fit = OutputFitChoice.SelectedIndex == 1 ? OutputFit.Crop : OutputFit.Fit };
                next.Validate();
            }
            _outputSettings = next; _outputInputValid = true;
            UpdateOutputInfo();
            VideoExportButton.IsEnabled = _video != null && _loadCancellation == null && _exportCancellation == null;
            Schedule();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            _outputInputValid = false;
            ExportButton.IsEnabled = false; VideoExportButton.IsEnabled = false;
            OutputSizeInfo.Text = Localization.T("Bitte gültige ganze Maße eingeben: 1–8192 Pixel, zusammen höchstens 24 Megapixel.");
        }
    }
    private void UpdateOutputInfo()
    {
        if (!_outputInputValid) return;
        int width, height;
        if (_outputSettings != null) { width = _outputSettings.Width; height = _outputSettings.Height; }
        else if (_output != null) { width = _output.Width; height = _output.Height; }
        else { OutputSizeInfo.Text = Localization.T("PNG und MP4: Größe der Vorschau"); return; }
        var mp4Width = (width + 1) & ~1; var mp4Height = (height + 1) & ~1;
        OutputSizeInfo.Text = $"PNG: {width} × {height} · MP4: {mp4Width} × {mp4Height}" +
            (width == mp4Width && height == mp4Height ? "" : Localization.T(" · MP4 ergänzt ungerade Maße um höchstens einen Randpixel"));
    }
    private void RestoreOutput(OutputSettings? settings)
    {
        _syncOutputUi = true;
        _outputSettings = settings; _outputInputValid = true;
        OutputChoice.SelectedIndex = settings == null ? 0 : (settings.Width, settings.Height) switch
        { (1280, 720) => 1, (1920, 1080) => 2, (3840, 2160) => 3, _ => 4 };
        if (settings != null)
        {
            OutputWidth.Text = settings.Width.ToString(CultureInfo.InvariantCulture);
            OutputHeight.Text = settings.Height.ToString(CultureInfo.InvariantCulture);
            OutputFitChoice.SelectedIndex = settings.Fit == OutputFit.Crop ? 1 : 0;
        }
        CustomOutputFields.Visibility = OutputChoice.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        OutputFitChoice.IsEnabled = settings != null; ScaleChoice.IsEnabled = settings == null;
        _syncOutputUi = false; UpdateOutputInfo();
    }
}
