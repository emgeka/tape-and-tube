// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private Dictionary<string, double> _screenLoopValues = ScreenLoopCatalog.Current.Defaults();
    private double _screenLoopSeconds = 5;
    private string? _stillPath;
    private bool _updatingScreenLoop;
    private readonly Dictionary<string, Slider> _screenLoopPointSliders = new();

    private bool InScreenLoop => _importedLook == null && !HistoricalDisplayActive && ScreenLoopEnabled.IsChecked == true;
    private VideoInfo? TimelineInfo => _video?.Info ?? StillAnimationTimeline();

    private VideoInfo? StillAnimationTimeline()
    {
        if (_source == null) return null;
        if (_importedLook is { KeyframeCount: > 0 } look)
            return ScreenLoopCatalog.StillTimeline(_source, look.Seconds, look.Fps);
        if (OwnAnimationEnabled)
            return ScreenLoopCatalog.StillTimeline(_source, _effectTimeline.Seconds, _effectTimeline.Fps);
        return InScreenLoop ? ScreenLoopCatalog.StillTimeline(_source, _screenLoopSeconds) : null;
    }

    private void InitializeScreenLoop()
    {
        foreach (var entry in ScreenLoopCatalog.Current.Presets)
        {
            var item = EffectChoice(entry.Name);
            item.Tag = entry.Preset;
            ScreenLoopPresetChoice.Items.Add(item);
        }
        BuildScreenLoopControls();
        UpdateScreenLoopControls();
    }

    private void UpdateScreenLoopControls()
    {
        ScreenLoopStillOptions.Visibility = _video == null ? Visibility.Visible : Visibility.Collapsed;
        RefreshEffectAvailability();
    }

    private void ToggleScreenLoop(object sender, RoutedEventArgs e)
    {
        if (!_ready || _updatingScreenLoop) return;
        if (_video == null)
        {
            PauseVideo();
            if (InScreenLoop) _frameIndex = 0;
        }
        RefreshTimeline();
        UpdatePreviewNavigationLayout();
        Schedule();
    }

    private void BuildScreenLoopControls()
    {
        _screenLoopPointSliders.Clear();
        ScreenLoopStillOptions.Children.Clear();
        AddSlider(ScreenLoopStillOptions, Localization.T("Standbilddauer (Sekunden)"), 0.5, 60, _screenLoopSeconds, 0.5, value =>
        {
            _screenLoopSeconds = value;
            if (!_ready) return;
            PauseVideo();
            RefreshTimeline();
            ScreenLoopEdited();
        });

        foreach (var (name, panel) in new[] { ("Framing", ScreenLoopFraming), ("Movement", ScreenLoopMovement),
            ("Camera", ScreenLoopCamera), ("Signal", ScreenLoopSignal) })
        {
            panel.Children.Clear();
            foreach (var parameter in ScreenLoopCatalog.Current.Parameters.Where(p => p.Group == name))
            {
                var container = new StackPanel();
                Localization.BindEffectText(container, ToolTipProperty, parameter.Help);
                panel.Children.Add(container);
                var value = _screenLoopValues[parameter.Id];
                void Change(double next)
                {
                    if (_updatingScreenLoop) return;
                    _screenLoopValues[parameter.Id] = next;
                    ScreenLoopEdited();
                }
                if (parameter.Kind == "toggle")
                {
                    var toggle = new CheckBox { IsChecked = value == 1 };
                    Localization.BindEffectText(toggle, ContentControl.ContentProperty, parameter.Label);
                    toggle.Click += (_, _) => Change(toggle.IsChecked == true ? 1 : 0);
                    container.Children.Add(toggle);
                }
                else
                {
                    var factor = parameter.Percent ? 100 : 1;
                    var unit = parameter.Percent ? "%" : parameter.Unit;
                    var slider = AddSlider(container, parameter.Label, parameter.Min * factor, parameter.Max * factor, value * factor,
                        parameter.Step ?? (parameter.Percent ? 1 : 0.1), v => Change(v / factor), parameter.Step == 1,
                        effectText: true, suffix: unit.Length == 0 ? "" : $" ({unit})");
                    if (parameter.Id is "center_x" or "center_y" or "drift_x" or "drift_y")
                        _screenLoopPointSliders[parameter.Id] = slider;
                }
            }
        }
    }

    private void ScreenLoopEdited()
    {
        if (!_ready || _updatingScreenLoop) return;
        ScreenLoopPresetChoice.SelectedIndex = 0;
        UpdateScreenLoopPad();
        Schedule();
    }

    private void ChooseScreenLoopPreset(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_updatingScreenLoop && ScreenLoopPresetChoice.SelectedItem is ComboBoxItem { Tag: ScreenLoopPreset preset })
            ApplyScreenLoopPreset(preset);
    }

    private void ApplyScreenLoopPreset(ScreenLoopPreset preset)
    {
        var values = ScreenLoopCatalog.Current.Defaults();
        foreach (var (key, value) in preset.Values) values[key] = value;
        ScreenLoopCatalog.Current.Validate(values);
        _updatingScreenLoop = true;
        try
        {
            if (_video == null) PauseVideo();
            _screenLoopValues = values;
            if (preset.Seconds.HasValue) _screenLoopSeconds = preset.Seconds.Value;
            ScreenLoopEnabled.IsChecked = true;
            if (_video == null) _frameIndex = 0;
            BuildScreenLoopControls();
            RefreshTimeline();
        }
        finally { _updatingScreenLoop = false; }
        UpdatePreviewNavigationLayout();
        Schedule();
    }

    private void OpenScreenLoopPreset(object sender, RoutedEventArgs e)
    {
        var dialog = PresetDialogs.Open(screenLoop: true);
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1_000_000)
                throw new InvalidDataException(Localization.T("Preset ist zu groß."));
            var preset = ScreenLoopPreset.Parse(File.ReadAllText(dialog.FileName));
            ScreenLoopPresetChoice.SelectedIndex = 0;
            ApplyScreenLoopPreset(preset);
        }
        catch (Exception ex) { Error(ex); }
    }

    private void SaveScreenLoopPreset(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = Localization.T("Screen-Loop-Preset speichern…"), Filter = "JSON|*.json", FileName = "screen-loop.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var preset = new ScreenLoopPreset(ScreenLoopPreset.PresetKind, 2, _screenLoopValues, _screenLoopSeconds);
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(preset, new JsonSerializerOptions(CoreWorkerClient.Json) { WriteIndented = true }));
            Status.Text = Localization.T("Preset gespeichert.");
        }
        catch (Exception ex) { Error(ex); }
    }
}
