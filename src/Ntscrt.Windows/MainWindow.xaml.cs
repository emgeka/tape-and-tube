// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private string? _controlsRenderer;

    public MainWindow()
    {
        InitializeComponent();
        DpiChanged += (_, _) => Dispatcher.InvokeAsync(UpdatePreviewNavigationLayout, System.Windows.Threading.DispatcherPriority.Loaded);
        Mp4LoopChoice.ItemsSource = Enumerable.Range(1, VideoExporter.MaxLoops).Select(count => $"{count}×");
        Mp4LoopChoice.SelectedIndex = 0;
        InitializeLanguage();
        InitializeUpdates();
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await Render(); };
        _prepareTimer.Tick += (_, _) => _preparation = PreparePreviewAsync();
        _stillMotionTimer.Tick += async (_, _) => await TickStillMotionAsync();
        try
        {
            InitializeDigital();
            RestoreAnalog(null);
            InitializeScreenLoop();
            _ntsc = NtscProcessor.Defaults(); _schema = NtscProcessor.Schema();
            foreach (var (name, path) in Components.AvailableShaderPresets())
            {
                _shaders.Add(name, path);
                ShaderChoice.Items.Add(name);
            }
            if (_shaders.Count == 0) throw new FileNotFoundException(Localization.T("Die CRT-Shader fehlen. Bitte scripts/setup.ps1 ausführen."));
            ShaderChoice.SelectedIndex = 0;
            BuildNtscControls();
            InitializeCrop();
            RefreshEffectAvailability();
            InitializeKeyframes();
            InitializeWorkspace();
            _ready = true;
            Status.Text = Localization.T("Bereit · Originaleffekte von ntsc-rs und der RetroArch-Shader-Community");
        }
        catch (Exception ex) { Error(ex); }
    }

    private void Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        RefreshEffectAvailability();
        RefreshKeyframeAvailability();
        Schedule();
    }

    private HistoricalDisplay SelectedHistoricalDisplay =>
        (HistoricalDisplay)Math.Max(0, HistoricalDisplayChoice?.SelectedIndex ?? 0);
    private bool KinescopeActive => FilmCaptureChoice?.SelectedIndex == 1;
    private bool HistoricalDisplayActive => _importedLook == null && (SelectedHistoricalDisplay != HistoricalDisplay.None || KinescopeActive);

    private void HistoricalDisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (_video == null) PauseVideo();
        RefreshTimeline();
        Schedule();
    }

    private void RefreshEffectAvailability()
    {
        var shaderAvailable = SelectedHistoricalDisplay == HistoricalDisplay.None;
        CrtCard.IsEnabled = shaderAvailable;
        ScreenLoopPanel.IsEnabled = _importedLook == null && !HistoricalDisplayActive;
        SnapScanlinesChoice.IsEnabled = !HistoricalDisplayActive;
        OutputFitChoice.IsEnabled = !HistoricalDisplayActive && OutputChoice.SelectedIndex != 0;
        var screenLoop = InScreenLoop;
        DigitalPanel.IsEnabled = !screenLoop;
        DigitalPausedHint.Visibility = screenLoop ? Visibility.Visible : Visibility.Collapsed;
        var downscale = SelectedDownscaleEnabled;
        WidthChoice.IsEnabled = downscale ?? ((shaderAvailable && CrtEnabled.IsChecked == true) || VcrEnabled.IsChecked == true ||
            ReceptionEnabled.IsChecked == true || DigitalEnabled.IsChecked == true || screenLoop);
        DownscaleMethodChoice.IsEnabled = WidthChoice.IsEnabled;
        ScaleChoice.IsEnabled = shaderAvailable && downscale != false && CrtEnabled.IsChecked == true && OutputChoice.SelectedIndex == 0;
    }
    private int SelectedRetroWidth => int.Parse((string)((ComboBoxItem)WidthChoice.SelectedItem).Content, CultureInfo.InvariantCulture);
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
        RefreshTimeline(); UpdatePreview(); Schedule();
    }
    private void Schedule()
    {
        if (!_ready || _closing) return;
        if (_playing && _video != null) HoldPlaybackClock();
        InvalidateRender();
        _debounce.Stop(); _debounce.Start();
    }
    private void ShaderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _shaderValues.Clear(); _controlsShader = null; CrtControls.Children.Clear();
        _keyframeShaderParameters = [];
        RefreshKeyframeAvailability();
        Schedule();
    }
    private RenderSettings Settings(bool preview = true)
    {
        var scale = int.Parse((string)((ComboBoxItem)ScaleChoice.SelectedItem).Content, CultureInfo.InvariantCulture);
        if (_importedLook is { } look)
            return new(false, look.Crt, "{}", "", look.RetroWidth, scale, (uint)_frameIndex, [], _outputSettings,
                DigitalCatalog.Profile, ImportedLook: look, Crop: look.Crop, SnapScanlines: SnapScanlinesChoice.IsChecked == true);
        var shaderName = (string)ShaderChoice.SelectedItem;
        var animation = AnimationPayload(shaderName, preview);
        return new(NtscEnabled.IsChecked == true, SelectedHistoricalDisplay == HistoricalDisplay.None && CrtEnabled.IsChecked == true,
            _ntsc.ToJsonString(), _shaders[shaderName],
            SelectedRetroWidth,
            scale, (uint)_frameIndex, new(_shaderValues), _outputSettings,
            OwnRenderer,
            !InScreenLoop && DigitalEnabled.IsChecked == true ? new(_digitalValues) : null,
            VcrEnabled.IsChecked == true || ReceptionEnabled.IsChecked == true ? ReadAnalogSettings() : null,
            InScreenLoop ? new(_screenLoopValues) : null,
            EffectTimeline: animation, Crop: _sourceCrop, SnapScanlines: !HistoricalDisplayActive && SnapScanlinesChoice.IsChecked == true,
            DownscaleMethod: SelectedDownscaleMethod, DownscaleEnabled: SelectedDownscaleEnabled,
            Display: SelectedHistoricalDisplay, Film: KinescopeActive);
    }

    private async Task Render()
    {
        if ((_source == null && _video == null) || !_ready || _closing || _exportCancellation != null) return;
        if (_busy) { _pending = true; return; }
        var liveStill = StillMotionActive;
        _busy = true; _pending = false;
        if (!liveStill || _completedPreviewVersion != _version) ExportButton.IsEnabled = false;
        int version = _version;
        var source = _source; var video = _video; var index = liveStill ? _stillMotionFrame : _frameIndex; var sourceId = _sourceIdentity;
        var timeline = liveStill ? StillMotionClock(source!) : TimelineInfo;
        var settings = Settings(); var clock = Stopwatch.StartNew();
        using var cancellation = new CancellationTokenSource();
        _renderCancellation = cancellation;
        if (!_playing && !liveStill) Status.Text = Localization.T("Effekte werden berechnet…");
        try
        {
            var token = cancellation.Token;
            var ready = TakePreparedFrame(index);
            if (ready == null)
            {
                await _preparation;
                token.ThrowIfCancellationRequested();
                ready = TakePreparedFrame(index);
            }
            if (ready == null)
            {
                if ((!_playing || !_playClock.IsRunning) && video != null && index > 0 && !settings.HasHistoricalStages && settings.Renderer == DigitalCatalog.Profile &&
                    settings.ScreenLoop == null && settings.ImportedLook == null && settings.EffectTimeline == null &&
                    settings.Digital?.GetValueOrDefault("fields") is not (2 or 3))
                {
                    // Show the changed look before rebuilding its exact temporal history.
                    var provisional = await ComputePreviewAsync(index, source, video, settings, sourceId, timeline, true, token);
                    if (_closing || version != _version) return;
                    Present(provisional.Source, provisional.Rendered, settings);
                }
                ready = await ComputePreviewAsync(index, source, video, settings, sourceId, timeline, false, token);
            }
            if (_closing || version != _version) return;
            var firstForVersion = _completedPreviewVersion != version;
            _completedPreviewVersion = version;
            var decoded = ready.Source;
            var result = ready.Rendered;
            Present(decoded, result, settings);
            if (_playing && !_playClock.IsRunning) StartPlaybackClock();
            _previewAudio?.Present(index);
            ExportButton.IsEnabled = !_playing && _outputInputValid;
            if (liveStill)
            {
                if (firstForVersion) Status.Text = Localization.T("Laufende Störeffekte in der Vorschau");
            }
            else Status.Text = $"{decoded.Width} × {decoded.Height} → {result.Frame.Width} × {result.Frame.Height} · {clock.ElapsedMilliseconds} ms · " + (_playing ? Localization.T("Video-Vorschau") : Localization.T("Bild bereit"));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!_closing && version == _version)
            {
                if (liveStill) _stillMotionTimer.Stop();
                PauseVideo();
                Error(ex);
            }
        }
        finally
        {
            _busy = false;
            if (ReferenceEquals(_renderCancellation, cancellation)) _renderCancellation = null;
            if (_closing) await DisposePreviewPipelineAsync();
            else if (_pending && !_debounce.IsEnabled) await Render();
            else SchedulePreviewPreparation();
        }
    }

    private void Present(PixelFrame source, RenderResult result, RenderSettings settings)
    {
        _source = source;
        _output = result.Frame;
        _previewLayout = result.Layout;
        _previewScanlineRows = result.ScanlineRows;
        _previewCrop = settings.Crop;
        if (settings.ImportedLook == null && settings.Crt &&
            (_controlsShader != settings.Shader || _controlsRenderer != settings.Renderer))
        {
            BuildShaderControls(result.Parameters);
            _controlsShader = settings.Shader;
            _controlsRenderer = settings.Renderer;
            _keyframeShaderParameters = result.Parameters;
            RefreshKeyframeAvailability();
        }
        UpdatePreview();
        UpdateOutputInfo();
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
            _source = source; _output = null; _stillPath = dialog.FileName;
            Title = $"{Path.GetFileName(dialog.FileName)} · Tape & Tube";
            EmptyHint.Visibility = Visibility.Collapsed;
            RefreshTimeline(); UpdatePreview(); Schedule();
        }
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
            var help = d["description"]?.GetValue<string>();
            if (kind is "group" or "bool")
            {
                var toggle = new CheckBox { IsChecked = _ntsc[id]!.GetValue<bool>() };
                Localization.BindEffectText(toggle, ContentControl.ContentProperty, label);
                if (!string.IsNullOrEmpty(help)) Localization.BindEffectText(toggle, ToolTipProperty, help);
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
                parent.Children.Add(EffectLabel(label));
                var combo = new ComboBox { Margin = new Thickness(0, 2, 0, 8) };
                Localization.BindEffectText(combo, System.Windows.Automation.AutomationProperties.NameProperty, label);
                if (!string.IsNullOrEmpty(help)) Localization.BindEffectText(combo, ToolTipProperty, help);
                foreach (var option in d["options"]!.AsArray())
                {
                    var value = option!["value"]!.GetValue<int>();
                    var item = EffectChoice(option["label"]!.GetValue<string>());
                    item.Tag = value;
                    combo.Items.Add(item);
                    if (value == _ntsc[id]!.GetValue<int>()) combo.SelectedItem = item;
                }
                combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem item) { _ntsc[id] = (int)item.Tag; Schedule(); } };
                parent.Children.Add(combo);
            }
            else if (kind is "number" or "int")
            {
                double min = d["min"]!.GetValue<double>(), max = d["max"]!.GetValue<double>();
                var slider = AddSlider(parent, label, min, max, _ntsc[id]!.GetValue<double>(), kind == "int" ? 1 : (max - min) / 1000,
                    v => { if (kind == "int") _ntsc[id] = (int)Math.Round(v); else _ntsc[id] = v; Schedule(); }, integer: kind == "int", effectText: true);
                if (!string.IsNullOrEmpty(help) && slider.Parent is FrameworkElement row) Localization.BindEffectText(row, ToolTipProperty, help);
            }
        }
    }
    private static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 2), Foreground = System.Windows.Media.Brushes.LightSteelBlue };
    private static TextBlock EffectLabel(string text, string suffix = "")
    {
        var label = Label("");
        Localization.BindEffectText(label, TextBlock.TextProperty, text, suffix);
        return label;
    }
    private static ComboBoxItem EffectChoice(string text)
    {
        var item = new ComboBoxItem();
        Localization.BindEffectText(item, ContentControl.ContentProperty, text);
        return item;
    }
    private static Slider AddSlider(StackPanel parent, string label, double min, double max, double value, double step,
        Action<double> update, bool integer = false, bool effectText = false, string suffix = "")
    {
        string Format(double v) => integer
            ? Math.Round(v).ToString("0", CultureInfo.InvariantCulture)
            : v.ToString("G5", CultureInfo.InvariantCulture);
        parent.Children.Add(effectText ? EffectLabel(label, suffix) : Label(label));
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var number = new TextBox { Width = integer ? 112 : 76, Margin = new Thickness(8, 0, 0, 0), Text = Format(value) };
        DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
        var slider = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), TickFrequency = Math.Max(step, 0.000001), IsSnapToTickEnabled = step >= 1, VerticalAlignment = VerticalAlignment.Center };
        foreach (var control in new FrameworkElement[] { number, slider })
        {
            if (effectText) Localization.BindEffectText(control, System.Windows.Automation.AutomationProperties.NameProperty, label, suffix);
            else System.Windows.Automation.AutomationProperties.SetName(control, label);
        }
        row.Children.Add(slider); parent.Children.Add(row);
        slider.ValueChanged += (_, _) => { number.Text = Format(slider.Value); update(slider.Value); };
        void Commit()
        {
            if (double.TryParse(number.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)) slider.Value = Math.Clamp(v, min, max);
            number.Text = Format(slider.Value);
        }
        number.LostKeyboardFocus += (_, _) => Commit();
        number.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { Commit(); e.Handled = true; } };
        return slider;
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
        if (_importedLook == null && !_outputInputValid) { Error(new ArgumentException(Localization.T("Bitte zuerst gültige Ausgabemaße eingeben."))); return; }
        var dialog = new SaveFileDialog { Title = Localization.T("Preset speichern…"), Filter = "JSON|*.json", FileName = "look.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (_importedLookText is string original)
            {
                File.WriteAllText(dialog.FileName, original);
                Status.Text = Localization.T("Preset gespeichert.");
                return;
            }
            var analog = ReadAnalogSettings();
            var saveAnalog = analog.Active || AnalogCatalog.Current.Parameters.Any(p => analog.Values[p.Id] != p.Default);
            var track = _effectTimeline.Enabled || _effectTimeline.Keys.Length > 0 ? _effectTimeline : null;
            var version = _sourceCrop != null ? 5 : track != null ? 4 : saveAnalog ? 3 : _renderer == "legacy-v1" ? 1 : 2;
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new LookPreset("tape-and-tube", version, NtscEnabled.IsChecked == true, CrtEnabled.IsChecked == true,
                (string)ShaderChoice.SelectedItem, SelectedRetroWidth, Settings().Scale, _ntsc, new(_shaderValues), _outputSettings,_renderer,
                DigitalEnabled.IsChecked==true?new(_digitalValues):null, saveAnalog ? analog : null, track, _sourceCrop,
                SelectedDownscaleMethod, SelectedDownscaleEnabled, Display: SelectedHistoricalDisplay, Film: KinescopeActive),
                new JsonSerializerOptions { WriteIndented = true }));
            Status.Text = Localization.T("Preset gespeichert.");
        }
        catch (Exception ex) { Error(ex); }
    }
    private void LoadPreset(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var dialog = PresetDialogs.Open();
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1_000_000) throw new InvalidDataException(Localization.T("Preset ist zu groß."));
            var contents = File.ReadAllText(dialog.FileName);
            if (NtscrtImportedLook.Parse(contents) is { } look)
            {
                ApplyImportedLook(look, contents, dialog.FileName);
                Status.Text = Localization.T("Preset geladen.");
                return;
            }
            var p = LookPreset.Parse(contents,_shaders.Keys);
            // Let the original library validate before changing any UI state.
            p.Output?.Validate();
            NtscProcessor.Process(PixelFrame.TestPattern(), p.Ntsc.ToJsonString(), 0);
            ApplyLookPreset(p);
            Status.Text = Localization.T("Preset geladen.");
        }
        catch (Exception ex) { Error(ex); }
    }

    private void ApplyLookPreset(LookPreset p, bool preserveGeometry = false, bool keepPlayback = false)
    {
        if (!keepPlayback) PauseVideo();
        _ready = false;
        try
        {
            _ntsc = NtscProcessor.Defaults();
            foreach (var pair in p.Ntsc) _ntsc[pair.Key] = pair.Value?.DeepClone();
            _shaderValues.Clear(); foreach (var pair in p.Parameters) _shaderValues[pair.Key] = pair.Value;
            ShaderChoice.SelectedItem = p.Shader; _controlsShader = null; CrtControls.Children.Clear();
            _keyframeShaderParameters = [];
            NtscEnabled.IsChecked = p.NtscEnabled; CrtEnabled.IsChecked = p.CrtEnabled;
            HistoricalDisplayChoice.SelectedIndex = (int)p.Display;
            FilmCaptureChoice.SelectedIndex = p.Film ? 1 : 0;
            SelectRetroWidth(p.Width); ScaleChoice.SelectedIndex = p.Scale - 2;
            DownscaleEnabledChoice.SelectedIndex = p.DownscaleEnabled switch { true => 1, false => 2, _ => 0 };
            if (p.DownscaleMethod == null) DownscaleMethodChoice.SelectedIndex = 0;
            else DownscaleMethodChoice.SelectedValue = p.DownscaleMethod;
            _renderer=p.Version==1?"legacy-v1":p.Renderer;
            _digitalValues=p.Digital==null?DigitalCatalog.Defaults:new(p.Digital);
            DigitalEnabled.IsChecked=p.Digital!=null;DigitalFormatChoice.SelectedIndex=0;BuildDigitalControls();
            RestoreAnalog(p.Analog);
            ScreenLoopEnabled.IsChecked = false;
            RestoreKeyframes(p.Timeline);
            if (!preserveGeometry)
            {
                RestoreCrop(p.Crop);
                RestoreOutput(p.Output);
            }
            BuildNtscControls();
        }
        finally { _ready = true; }
        if (_importedLook != null) ApplyImportedLook(null, keepPlayback: keepPlayback);
        else
        {
            RefreshTimeline();
            Schedule();
        }
    }
    private void Error(Exception ex) { Status.Text = Localization.T("Fehler: ") + ex.Message; }
    protected override void OnClosed(EventArgs e)
    {
        _closing = true;
        StopPreviewAudio();
        SaveWorkspaceMode();
        _debounce.Stop();
        _stillMotionTimer.Stop();
        DiscardPreparedFrames();
        CompositionTarget.Rendering -= PlaybackRendering;
        _loadCancellation?.Cancel(); _renderCancellation?.Cancel(); _video?.Cancel();
        if (!_busy) _ = DisposePreviewPipelineAsync();
        base.OnClosed(e);
    }

    private async Task DisposePreviewPipelineAsync()
    {
        await _preparation;
        _pipeline.Dispose();
    }
}
