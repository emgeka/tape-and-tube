// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private string ComponentText()
    {
        var text = new StringBuilder();
        text.AppendLine(Localization.T("Tape & Tube · Komponenten & Credits\n"));
        text.AppendLine(Localization.T("Windows-Adaption von NTSCRT von Finn McKenty. Konzept, Signalkette und Workflow gehen auf das Originalprojekt zurück; Windows-Oberfläche und Host-Anbindung sind neu implementiert. Quelle: https://github.com/finnmckenty/NTSCRT"));
        text.AppendLine();
        text.AppendLine(Localization.T("Unser eigener Anwendungscode und die Host-Anbindung: GPL-3.0-only. Die Fremdprojekte behalten ihre eigenen Lizenzen.\n"));
        var metadataPath = Path.Combine(Paths.Root, "dependencies.json");
        var metadata = File.Exists(metadataPath) ? JsonNode.Parse(File.ReadAllText(metadataPath))!.AsObject() : new JsonObject();
        text.AppendLine(Localization.T("ntsc-rs · NTSC-/VHS-Signalverarbeitung"));
        text.AppendLine(Localization.T("Lizenz: MIT OR ISC OR Apache-2.0"));
        text.AppendLine(Localization.T("Getesteter Referenzstand: ") + metadata["ntsc-rs"]?["revision"]);
        text.AppendLine(Localization.T("Quelle: https://github.com/ntsc-rs/ntsc-rs"));
        text.AppendLine(Components.NativeStatus("ntscLibrary", "TAPETUBE_NTSC_LIBRARY", "ntscrt_ntsc.dll") + "\n");
        text.AppendLine(Localization.T("librashader · Ausführung der RetroArch-Shader über Direct3D 11"));
        text.AppendLine(Localization.T("Lizenz: MPL-2.0"));
        text.AppendLine(Localization.T("Getesteter Referenzstand: ") + metadata["librashader"]?["release"]);
        text.AppendLine(Localization.T("Quelle: https://github.com/SnowflakePowered/librashader"));
        text.AppendLine(Components.NativeStatus("librashaderLibrary", "TAPETUBE_LIBRASHADER_LIBRARY", "librashader.dll") + "\n");
        text.AppendLine(Localization.T("RetroArch-Shader und ihre Autoren · CRT-Bild, Masken, Scanlines und Glow"));
        text.AppendLine(Localization.T("Unterschiedliche Lizenzen je Datei; siehe Originalhinweise, THIRD_PARTY.md und licenses/."));
        text.AppendLine(Localization.T("Getesteter Referenzstand: ") + metadata["slang-shaders"]?["revision"]);
        text.AppendLine(Localization.T("Quelle: https://github.com/libretro/slang-shaders"));
        try { text.AppendLine(Localization.T("Aktueller Shader-Ordner: ") + Paths.ShaderRoot); }
        catch (Exception ex) { text.AppendLine(ex.Message); }
        text.AppendLine(Localization.T("Geladener Preset-Katalog:"));
        foreach (var shader in _shaders) text.AppendLine("  " + shader.Key + " → " + shader.Value);
        text.AppendLine(Localization.T("\nFFmpeg / ffprobe · Videoeinlesen und MP4-/Audio-Kodierung"));
        text.AppendLine(Localization.T("Externe Installation, nicht Bestandteil dieses Prüfpakets. Die Lizenz hängt vom jeweiligen Build ab."));
        text.AppendLine(Localization.T("Quelle: https://ffmpeg.org/\n"));
        text.AppendLine(Localization.T("Weitere Komponenten: Vortice.Windows, Vortice.Mathematics und SharpGen.Runtime (MIT), .NET (Microsoft und Mitwirkende). Details: licenses/dependency-inventory.json.\n"));
        text.AppendLine(Localization.T("Tape & Tube ist ein eigenständiges Projekt und keine offizielle Ausgabe der genannten Projekte.\n"));
        text.AppendLine(Localization.T("Eigene Versionen und Pfade lassen sich über components.local.json oder TAPETUBE_* konfigurieren. Referenzstände bestätigen nicht die Version einer selbst ersetzten Bibliothek."));
        return text.ToString();
    }

    private async void ShowComponents(object sender, RoutedEventArgs e)
    {
        try
        {
        var box = new TextBox { Text = ComponentText() + Localization.T("\n\nMedienwerkzeuge werden geprüft…"), IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(16), Background = new SolidColorBrush(Color.FromRgb(17, 21, 28)), Foreground = Brushes.White };
        var dialog = new Window { Title = Localization.T("Tape & Tube · Komponenten & Credits"), Width = 820, Height = 700, MinWidth = 600, MinHeight = 450,
            Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = box };
        var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        string? report = null;
        void RefreshText()
        {
            dialog.Title = Localization.T("Tape & Tube · Komponenten & Credits");
            box.Text = ComponentText() + (report ?? Localization.T("\n\nMedienwerkzeuge werden geprüft…"));
        }
        Localization.Changed += RefreshText;
        dialog.Closed += (_, _) => { Localization.Changed -= RefreshText; cancellation.Cancel(); cancellation.Dispose(); };
        dialog.Show();
        async Task<string> MediaReport(string name)
        {
            try
            {
                var path = MediaTools.Find(name);
                var version = await MediaTools.CaptureAsync(name, ["-version"], cancellation.Token);
                return name + ": " + path + "\n" + version.Split('\n')[0].Trim();
            }
            catch (Exception ex) { return name + ": " + ex.Message; }
        }
        var reports = await Task.WhenAll(MediaReport("ffmpeg"), MediaReport("ffprobe"));
        report = "\n\n" + string.Join("\n\n", reports);
        if (dialog.IsVisible) RefreshText();
        }
        catch (Exception ex) { Error(ex); }
    }
}
