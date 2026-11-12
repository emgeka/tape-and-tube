// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Ntscrt.Windows;

public partial class MainWindow : Window
{
    private readonly EffectPipeline _pipeline = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private JsonObject _ntsc = new();
    private JsonArray _schema = new();
    private readonly Dictionary<string, float> _shaderValues = [];
    private readonly Dictionary<string, string> _shaders = [];
    private PixelFrame? _source, _output;
    private bool _ready, _busy, _pending, _closing;
    private int _version;
    private string? _controlsShader;

    public MainWindow()
    {
        InitializeComponent();
        InitializeLanguage();
        InitializeVideo();
        InitializeUpdates();
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await Render(); };
        try
        {
            _ntsc = NtscProcessor.Defaults(); _schema = NtscProcessor.Schema();
            foreach (var preset in Components.ShaderPresets())
            {
                var path = Path.GetFullPath(preset, Paths.ShaderRoot);
                var name = Path.GetFileNameWithoutExtension(path);
                if (_shaders.ContainsKey(name)) name = preset.Replace('\\', '/');
                if (_shaders.ContainsKey(name)) continue;
                if (File.Exists(path)) { _shaders.Add(name, path); ShaderChoice.Items.Add(name); }
            }
            if (_shaders.Count == 0) throw new FileNotFoundException(Localization.T("Die CRT-Shader fehlen. Bitte scripts/setup.ps1 ausführen."));
            ShaderChoice.SelectedIndex = 0;
            BuildNtscControls();
            _ready = true;
            Status.Text = Localization.T("Bereit · Originaleffekte von ntsc-rs und der RetroArch-Shader-Community");
        }
        catch (Exception ex) { Error(ex); }
    }

    private void Changed(object sender, RoutedEventArgs e) => Schedule();
    private void OpenDemo(object sender, RoutedEventArgs e) => LoadDemo();
    internal async void LoadDemo()
    {
        if (!_ready) return;
        _loadCancellation?.Cancel();
        await ClearVideo();
        if (_closing) return;
        _source = PixelFrame.TestPattern();
        _output = null;
        EmptyHint.Visibility = Visibility.Collapsed;
        UpdatePreview(); Schedule();
    }
    private void Schedule()
    {
        if (!_ready || _closing) return;
        InvalidateRender();
        _debounce.Stop(); _debounce.Start();
    }
    private void ShaderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _shaderValues.Clear(); _controlsShader = null; CrtControls.Children.Clear(); Schedule();
    }
    private RenderSettings Settings() => new(NtscEnabled.IsChecked == true, CrtEnabled.IsChecked == true,
        _ntsc.ToJsonString(), _shaders[(string)ShaderChoice.SelectedItem],
        int.Parse((string)((ComboBoxItem)WidthChoice.SelectedItem).Content, CultureInfo.InvariantCulture),
        int.Parse((string)((ComboBoxItem)ScaleChoice.SelectedItem).Content, CultureInfo.InvariantCulture), (uint)_frameIndex, new(_shaderValues), _outputSettings);

    private async Task Render()
    {
        if ((_source == null && _video == null) || !_ready || _closing || _exportCancellation != null) return;
        if (_busy) { _pending = true; return; }
        _busy = true; ExportButton.IsEnabled = false;
        int version = _version;
        var source = _source; var video = _video; var index = _frameIndex;
        var settings = Settings(); var clock = Stopwatch.StartNew();
        using var cancellation = new CancellationTokenSource();
        _renderCancellation = cancellation;
        Status.Text = Localization.T("Effekte werden berechnet…");
        try
        {
            if (video != null) source = await video.ReadFrameAsync(index, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var result = await Task.Run(() => _pipeline.Render(source!, settings), cancellation.Token);
            if (_closing || version != _version) return;
            _source = source;
            _output = result.Frame;
            _previewOutputSettings = settings.Output;
            if (settings.Crt && _controlsShader != settings.Shader)
            {
                BuildShaderControls(result.Parameters); _controlsShader = settings.Shader;
            }
            UpdatePreview();
            ExportButton.IsEnabled = !_playing && _outputInputValid;
            UpdateOutputInfo();
            Status.Text = $"{source!.Width} × {source.Height} → {_output.Width} × {_output.Height} · {clock.ElapsedMilliseconds} ms · " + (_playing ? Localization.T("Video-Vorschau") : Localization.T("PNG bereit"));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closing && version == _version) { PauseVideo(); Error(ex); } }
        finally
        {
            _busy = false;
            if (ReferenceEquals(_renderCancellation, cancellation)) _renderCancellation = null;
            if (_closing) _pipeline.Dispose();
            else if (_pending) { _pending = false; await Render(); }
        }
    }

    private async void OpenImage(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var dialog = new OpenFileDialog { Title = Localization.T("Bild öffnen…"), Filter = Localization.T("Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Alle Dateien|*.*") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _loadCancellation?.Cancel();
            PauseVideo();
            var source = PixelFrame.Read(dialog.FileName);
            await ClearVideo();
            if (_closing) return;
            _source = source; _output = null;
            Title = $"{Path.GetFileName(dialog.FileName)} · Tape & Tube";
            EmptyHint.Visibility = Visibility.Collapsed;
            UpdatePreview(); Schedule();
        }
        catch (Exception ex) { Error(ex); }
    }
    private void ExportImage(object sender, RoutedEventArgs e)
    {
        if (_output == null || !ExportButton.IsEnabled || !_outputInputValid) return;
        var dialog = new SaveFileDialog { Title = Localization.T("Bild speichern…"), Filter = "PNG|*.png", FileName = "tape-and-tube.png" };
        if (dialog.ShowDialog(this) != true) return;
        try { _output.Save(dialog.FileName); Status.Text = Localization.T("PNG gespeichert: ") + dialog.FileName; }
        catch (Exception ex) { Error(ex); }
    }

    private void BuildNtscControls()
    {
        NtscControls.Children.Clear();
        BuildDescriptors(_schema, NtscControls);
    }
    private void BuildDescriptors(JsonArray descriptors, StackPanel parent)
    {
        foreach (var node in descriptors)
        {
            if (node is not JsonObject d) continue;
            var id = d["id"]!.GetValue<string>(); var label = d["label"]!.GetValue<string>(); var kind = d["kind"]!.GetValue<string>();
            if (!_ntsc.ContainsKey(id)) continue;
            if (kind is "group" or "bool")
            {
                var toggle = new CheckBox { Content = label, IsChecked = _ntsc[id]!.GetValue<bool>(), ToolTip = d["description"]?.ToString() };
                parent.Children.Add(toggle);
                StackPanel? children = null;
                if (kind == "group")
                {
                    children = new StackPanel { Margin = new Thickness(12, 0, 0, 0), IsEnabled = toggle.IsChecked == true };
                    var expander = new Expander { Content = children };
                    expander.SetResourceReference(HeaderedContentControl.HeaderProperty, "DetailsLabel");
                    parent.Children.Add(expander);
                    BuildDescriptors(d["children"]!.AsArray(), children);
                }
                toggle.Click += (_, _) => { _ntsc[id] = toggle.IsChecked == true; if (children != null) children.IsEnabled = toggle.IsChecked == true; Schedule(); };
            }
            else if (kind == "enum")
            {
                parent.Children.Add(Label(label));
                var combo = new ComboBox { Margin = new Thickness(0, 2, 0, 8) };
                foreach (var option in d["options"]!.AsArray())
                {
                    var value = option!["value"]!.GetValue<int>();
                    var item = new ComboBoxItem { Content = option["label"]!.GetValue<string>(), Tag = value };
                    combo.Items.Add(item);
                    if (value == _ntsc[id]!.GetValue<int>()) combo.SelectedItem = item;
                }
                combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem item) { _ntsc[id] = (int)item.Tag; Schedule(); } };
                parent.Children.Add(combo);
            }
            else if (kind is "number" or "int")
            {
                double min = d["min"]!.GetValue<double>(), max = d["max"]!.GetValue<double>();
                AddSlider(parent, label, min, max, _ntsc[id]!.GetValue<double>(), kind == "int" ? 1 : (max - min) / 1000,
                    v => { if (kind == "int") _ntsc[id] = (int)Math.Round(v); else _ntsc[id] = v; Schedule(); }, integer: kind == "int");
            }
        }
    }
    private static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 2), Foreground = System.Windows.Media.Brushes.LightSteelBlue };
    private static void AddSlider(StackPanel parent, string label, double min, double max, double value, double step, Action<double> update, bool integer = false)
    {
        string Format(double v) => integer
            ? Math.Round(v).ToString("0", CultureInfo.InvariantCulture)
            : v.ToString("G5", CultureInfo.InvariantCulture);
        parent.Children.Add(Label(label));
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var number = new TextBox { Width = integer ? 112 : 76, Margin = new Thickness(8, 0, 0, 0), Text = Format(value) };
        System.Windows.Automation.AutomationProperties.SetName(number, label);
        DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
        var slider = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), TickFrequency = Math.Max(step, 0.000001), IsSnapToTickEnabled = step >= 1, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        row.Children.Add(slider); parent.Children.Add(row);
        slider.ValueChanged += (_, _) => { number.Text = Format(slider.Value); update(slider.Value); };
        void Commit()
        {
            if (double.TryParse(number.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)) slider.Value = Math.Clamp(v, min, max);
            number.Text = Format(slider.Value);
        }
        number.LostKeyboardFocus += (_, _) => Commit();
        number.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { Commit(); e.Handled = true; } };
    }
    private void BuildShaderControls(IReadOnlyList<ShaderParameter> parameters)
    {
        CrtControls.Children.Clear();
        foreach (var p in parameters)
            AddSlider(CrtControls, p.Label, p.Minimum, p.Maximum, _shaderValues.GetValueOrDefault(p.Name, p.Initial), p.Step,
                v => { _shaderValues[p.Name] = (float)v; Schedule(); });
    }

    private void SavePreset(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (!_outputInputValid) { Error(new ArgumentException(Localization.T("Bitte zuerst gültige Ausgabemaße eingeben."))); return; }
        var dialog = new SaveFileDialog { Title = Localization.T("Preset speichern…"), Filter = "Tape & Tube Preset|*.json", FileName = "look.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new Preset("tape-and-tube", 1, NtscEnabled.IsChecked == true, CrtEnabled.IsChecked == true,
                (string)ShaderChoice.SelectedItem, Settings().RetroWidth, Settings().Scale, _ntsc, new(_shaderValues), _outputSettings), new JsonSerializerOptions { WriteIndented = true }));
            Status.Text = Localization.T("Preset gespeichert.");
        }
        catch (Exception ex) { Error(ex); }
    }
    private void LoadPreset(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var dialog = new OpenFileDialog { Title = Localization.T("Preset laden…"), Filter = "Tape & Tube Preset|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1_000_000) throw new InvalidDataException(Localization.T("Preset ist zu groß."));
            var p = JsonSerializer.Deserialize<Preset>(File.ReadAllText(dialog.FileName)) ?? throw new InvalidDataException(Localization.T("Leeres Preset."));
            if ((p.Kind != "ntscrt-windows" && p.Kind != "tape-and-tube") || p.Version != 1 || !_shaders.ContainsKey(p.Shader) || p.Ntsc == null || p.Parameters == null
                || !new[] { 256, 320, 640, 1280 }.Contains(p.Width) || p.Scale < 2 || p.Scale > 4 || p.Parameters.Values.Any(v => !float.IsFinite(v)))
                throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
            // Let the original library validate before changing any UI state.
            p.Output?.Validate();
            NtscProcessor.Process(PixelFrame.TestPattern(), p.Ntsc.ToJsonString(), 0);
            _ready = false;
            _ntsc = NtscProcessor.Defaults();
            foreach (var pair in p.Ntsc) _ntsc[pair.Key] = pair.Value?.DeepClone();
            _shaderValues.Clear(); foreach (var pair in p.Parameters) _shaderValues[pair.Key] = pair.Value;
            ShaderChoice.SelectedItem = p.Shader; _controlsShader = null; CrtControls.Children.Clear();
            NtscEnabled.IsChecked = p.NtscEnabled; CrtEnabled.IsChecked = p.CrtEnabled;
            WidthChoice.SelectedIndex = Array.IndexOf(new[] { 256, 320, 640, 1280 }, p.Width); ScaleChoice.SelectedIndex = p.Scale - 2;
            RestoreOutput(p.Output);
            BuildNtscControls(); _ready = true; Schedule();
            Status.Text = Localization.T("Preset geladen.");
        }
        catch (Exception ex) { _ready = true; Error(ex); }
    }
    private void Error(Exception ex) { Status.Text = Localization.T("Fehler: ") + ex.Message; }
    protected override void OnClosed(EventArgs e)
    {
        _closing = true; _debounce.Stop(); _playback.Stop();
        _loadCancellation?.Cancel(); _renderCancellation?.Cancel(); _video?.Cancel();
        if (!_busy) _pipeline.Dispose();
        base.OnClosed(e);
    }
    private sealed record Preset(string Kind, int Version, bool NtscEnabled, bool CrtEnabled, string Shader, int Width, int Scale, JsonObject Ntsc, Dictionary<string, float> Parameters, OutputSettings? Output = null);
}
