// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private const string NtscrtSource = "https://github.com/finnmckenty/NTSCRT";
    private const string LibrashaderSource = "https://github.com/SnowflakePowered/librashader";
    private Window? _componentsDialog;
    private sealed record ComponentLocation(string Status, string? Path = null, string? Error = null);
    private sealed record MediaComponent(string? Path, string? Version, string? Error = null);

    private async void ShowComponents(object sender, RoutedEventArgs e)
    {
        if (_componentsDialog is { } open)
        {
            if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
            open.Activate();
            return;
        }

        try
        {
            var tabs = new TabControl();
            var project = AddComponentsTab(tabs);
            var software = AddComponentsTab(tabs);
            var hardware = AddComponentsTab(tabs);
            var dialog = new Window
            {
                Width = 780, Height = 660, MinWidth = 480, MinHeight = 380, Owner = this, Icon = Icon,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = tabs,
                Foreground = Foreground, Background = Background, UseLayoutRounding = true
            };
            MediaComponent? ffmpeg = null, ffprobe = null;
            void Refresh()
            {
                dialog.Title = Localization.T("Tape & Tube · Komponenten & Credits");
                ((TabItem)tabs.Items[0]).Header = Localization.T("Projekt");
                ((TabItem)tabs.Items[1]).Header = Localization.T("Software");
                ((TabItem)tabs.Items[2]).Header = Localization.T("Hardware");
                PopulateProject(project);
                PopulateSoftware(software, ffmpeg, ffprobe);
                PopulateHardware(hardware);
            }
            dialog.Closed += (_, _) => { _componentsDialog = null; Localization.Changed -= Refresh; };
            tabs.SelectionChanged += (_, _) =>
            {
                if (tabs.SelectedIndex == 1) PopulateSoftware(software, ffmpeg, ffprobe);
                if (tabs.SelectedIndex == 2) PopulateHardware(hardware);
            };
            Refresh();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            void CancelProbe(object? source, EventArgs args) => cancellation.Cancel();
            dialog.Closed += CancelProbe;
            _componentsDialog = dialog;
            Localization.Changed += Refresh;
            try
            {
                dialog.Show();
                var reports = await Task.WhenAll(ProbeMediaComponent("ffmpeg", cancellation.Token), ProbeMediaComponent("ffprobe", cancellation.Token));
                (ffmpeg, ffprobe) = (reports[0], reports[1]);
                if (dialog.IsVisible) PopulateSoftware(software, ffmpeg, ffprobe);
            }
            finally { dialog.Closed -= CancelProbe; }
        }
        catch (Exception ex) { Error(ex); }
    }

    private static StackPanel AddComponentsTab(TabControl tabs)
    {
        var panel = new StackPanel { Margin = new Thickness(20, 14, 20, 20) };
        panel.SetResourceReference(TextElement.ForegroundProperty, "Ink");
        tabs.Items.Add(new TabItem { Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        return panel;
    }

    private void PopulateProject(StackPanel panel)
    {
        panel.Children.Clear();
        var project = new StackPanel();
        project.Children.Add(new TextBlock { Text = "Tape & Tube", FontSize = 25, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        project.Children.Add(Paragraph(Localization.T("Tape & Tube ist die Windows-Adaption von NTSCRT von Finn McKenty. Konzept, Signalkette und Workflow stammen aus dem Originalprojekt. Die Windows-Oberfläche und die Host-Anbindung wurden für Tape & Tube entwickelt.")));
        AddSource(project, NtscrtSource, "NTSCRT · Finn McKenty");
        project.Children.Add(Paragraph(Localization.T("Unser eigener Anwendungscode und die Host-Anbindung: GPL-3.0-only. Die Fremdprojekte behalten ihre eigenen Lizenzen.\n").TrimEnd()));
        project.Children.Add(Paragraph(Localization.T("Tape & Tube ist ein eigenständiges Projekt und keine offizielle Ausgabe der genannten Projekte.\n").TrimEnd()));
        panel.Children.Add(Card(project));
        var credits = new StackPanel();
        credits.Children.Add(new TextBlock { Text = Localization.T("Danksagungen"), FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        AddSource(credits, "https://github.com/ntsc-rs/ntsc-rs", "ntsc-rs");
        AddSource(credits, "https://github.com/libretro/slang-shaders", Localization.T("RetroArch-Shader und ihre Autoren · CRT-Bild, Masken, Scanlines und Glow"));
        AddSource(credits, LibrashaderSource, "librashader");
        AddSource(credits, "https://ffmpeg.org/", "FFmpeg");
        panel.Children.Add(Card(credits));
    }

    private void PopulateHardware(StackPanel panel)
    {
        panel.Children.Clear();
        var summary = new StackPanel();
        AddField(summary, "Betriebssystem", RuntimeInformation.OSDescription);
        AddField(summary, "Prozessarchitektur", RuntimeInformation.ProcessArchitecture.ToString());
        AddField(summary, "Verfügbare logische CPU-Kerne", Environment.ProcessorCount.ToString());
        panel.Children.Add(Card(summary));
        var graphics = new StackPanel();
        var (gpu, backend) = _pipeline.GraphicsAdapter;
        AddField(graphics, "Grafikadapter (gemeinsamer Effektkern)", gpu ?? Localization.T("Noch nicht initialisiert"));
        if (backend != null) AddField(graphics, "Grafik-Backend", backend);
        AddField(graphics, "Grafikadapter (Direct3D 11)", _pipeline.D3D11Adapter ?? Localization.T("Noch nicht initialisiert"));
        panel.Children.Add(Card(graphics));
    }

    private void PopulateSoftware(StackPanel panel, MediaComponent? ffmpeg, MediaComponent? ffprobe)
    {
        var expanded = panel.Children.OfType<Expander>().Where(item => item.IsExpanded).Select(item => item.Tag).ToHashSet();
        panel.Children.Clear();
        var metadataPath = Path.Combine(Paths.Root, "dependencies.json");
        var metadata = File.Exists(metadataPath) ? JsonNode.Parse(File.ReadAllText(metadataPath)) : null;
        ComponentLocation worker = LocateComponent(() => (EffectPipeline.WorkerPath, _pipeline.GraphicsAdapter.Name != null));
        ComponentLocation ntsc = LocateComponent(() => Components.NativeLocation("ntscLibrary", "TAPETUBE_NTSC_LIBRARY", "ntscrt_ntsc.dll"));
        ComponentLocation dx11 = LocateComponent(() => Components.NativeLocation("librashaderLibrary", "TAPETUBE_LIBRASHADER_LIBRARY", "librashader.dll"));
        ComponentLocation shaders = LocateComponent(() => (Paths.ShaderRoot, false), directory: true);

        void Add(string name, string role, string? version, string license, string source,
            ComponentLocation? location = null, string? notes = null, bool reference = false)
        {
            var fields = new StackPanel();
            AddField(fields, "Aufgabe", Localization.T(role));
            if (version != null) AddField(fields, reference ? "Referenzstand" : "Version", version);
            if (location != null) AddField(fields, "Status", location.Status);
            AddField(fields, "Lizenz", license);
            AddSource(fields, source);
            if (location?.Path != null || location?.Error != null || notes != null)
            {
                var details = new StackPanel();
                if (location?.Path is { } path) details.Children.Add(new TextBox { Text = path, IsReadOnly = true, TextWrapping = TextWrapping.Wrap });
                if (location?.Error is { } error) details.Children.Add(Paragraph(error));
                if (notes != null) details.Children.Add(Paragraph(notes));
                fields.Children.Add(new Expander { Header = Localization.T("Details"), Content = details });
            }
            panel.Children.Add(new Expander { Header = name, Tag = name, IsExpanded = expanded.Contains(name), Content = Card(fields) });
        }

        Add("NTSCRT", "Gemeinsamer Effektkern für Digitalvideo, Videorekorder, TV-Empfang und Screen Loop", DigitalCatalog.Revision,
            "MIT", NtscrtSource + "/tree/" + DigitalCatalog.Revision + "/port", worker,
            "Copyright (c) 2026 Finn McKenty · host-lifecycle.patch", reference: true);
        Add("ntsc-rs", "NTSC-/VHS-Signalverarbeitung", metadata?["ntsc-rs"]?["revision"]?.ToString(),
            "MIT OR ISC OR Apache-2.0", "https://github.com/ntsc-rs/ntsc-rs", ntsc, reference: true);
        Add("librashader · Direct3D 11", "RetroArch-Shader ausführen", metadata?["librashader"]?["release"]?.ToString(),
            "MPL-2.0", LibrashaderSource, dx11, reference: true);
        Add("librashader · wgpu", "RetroArch-Shader ausführen", "76462c030b75c4f2d56e5386c3d4d7d1128318b8",
            "MPL-2.0 OR GPL-3.0-only", LibrashaderSource, worker, "NTSCRT · Push-Constant-Patch", reference: true);
        Add("RetroArch · slang-shaders", "CRT-Bild, Masken, Scanlines und Glow", metadata?["slang-shaders"]?["revision"]?.ToString(),
            Localization.T("Unterschiedliche Lizenzen je Datei; siehe Originalhinweise, THIRD_PARTY.md und licenses/."),
            "https://github.com/libretro/slang-shaders", shaders,
            Localization.T("Geladener Preset-Katalog:") + "\n" + string.Join("\n", _shaders.Select(shader => shader.Key + " → " + shader.Value)), reference: true);
        var mediaLicense = Localization.T("Abhängig vom Build (licenses/ffmpeg).");
        Add("FFmpeg", "Video- und Audioverarbeitung", ffmpeg?.Version, mediaLicense, "https://ffmpeg.org/", MediaLocation(ffmpeg));
        Add("ffprobe", "Metadaten und Medienformate erkennen", ffprobe?.Version, mediaLicense, "https://ffmpeg.org/", MediaLocation(ffprobe));
        Add("Vortice.Windows", "Windows-Grafikschnittstellen", typeof(Vortice.Direct3D11.ID3D11Device).Assembly.GetName().Version?.ToString(),
            "MIT", "https://github.com/amerkoleci/Vortice.Windows");
        Add("SharpGen.Runtime", "Windows-Grafikschnittstellen", typeof(SharpGen.Runtime.ComObject).Assembly.GetName().Version?.ToString(),
            "MIT", "https://github.com/SharpGenTools/SharpGenTools");
        Add(".NET", "Laufzeitumgebung der Windows-App", RuntimeInformation.FrameworkDescription,
            "MIT · Microsoft", "https://github.com/dotnet/runtime", notes: "licenses/dependency-inventory.json");
        panel.Children.Add(Paragraph(Localization.T("Eigene Versionen und Pfade lassen sich über components.local.json oder TAPETUBE_* konfigurieren. Referenzstände bestätigen nicht die Version einer selbst ersetzten Bibliothek.")));
    }

    private static ComponentLocation LocateComponent(Func<(string Path, bool Loaded)> locate, bool directory = false)
    {
        try
        {
            var (path, loaded) = locate();
            var exists = directory ? Directory.Exists(path) : File.Exists(path);
            return new(Localization.T(loaded ? "Geladen" : exists ? "Verfügbar" : "Nicht verfügbar"), path);
        }
        catch (Exception ex) { return new(Localization.T("Nicht verfügbar"), Error: ex.Message); }
    }

    private static ComponentLocation MediaLocation(MediaComponent? tool) => new(
        Localization.T(tool == null ? "Wird geprüft…" : tool.Version == null ? "Nicht verfügbar" : "Verfügbar"), tool?.Path, tool?.Error);

    private static async Task<MediaComponent> ProbeMediaComponent(string name, CancellationToken token)
    {
        string? path = null;
        try
        {
            path = MediaTools.Find(name);
            var report = await MediaTools.CaptureAsync(name, ["-version"], token);
            var version = report.Split('\n', 2)[0].Trim();
            return new(path, version.Length > 0 ? version : null);
        }
        catch (OperationCanceledException) { return new(path, null); }
        catch (Exception ex) { return new(path, null, ex.Message); }
    }

    private static Border Card(UIElement content) => new()
    {
        Style = (Style)Application.Current.FindResource("Card"),
        Margin = new Thickness(0, 0, 0, 12),
        Child = content
    };

    private static TextBlock Paragraph(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8)
    };

    private static void AddField(Panel panel, string label, string value)
    {
        var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        row.Children.Add(new TextBlock { Text = Localization.T(label), TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("Muted"), Margin = new Thickness(0, 0, 12, 0) });
        var field = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(field, 1);
        row.Children.Add(field);
        panel.Children.Add(row);
    }

    private void AddSource(Panel panel, string url, string? label = null)
    {
        var source = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) };
        var link = new Hyperlink(new Run(label ?? Localization.T("Quelle öffnen")))
        {
            NavigateUri = new Uri(url), Foreground = (Brush)Application.Current.FindResource("Accent"), ToolTip = url
        };
        link.RequestNavigate += (_, e) =>
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
            catch (Exception ex) { Error(ex); }
            e.Handled = true;
        };
        source.Inlines.Add(link);
        panel.Children.Add(source);
    }
}
