// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private static string WorkspaceModePath => Path.Combine(Path.GetDirectoryName(Localization.PreferencesPath)!, "workspace-mode.txt");
    private bool _workspaceModeChanged;

    private void InitializeWorkspace()
    {
        foreach (var (heading, looks) in BasicLookCatalog.Groups)
        {
            var label = new TextBlock { Style = (Style)FindResource("SectionLabel"), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, BasicLooks.Children.Count == 0 ? 0 : 12, 0, 8) };
            label.SetResourceReference(TextBlock.TextProperty, heading);
            AutomationProperties.SetHeadingLevel(label, AutomationHeadingLevel.Level2);
            BasicLooks.Children.Add(label);
            foreach (var look in looks) AddBasicLook(look);
        }
        WorkspaceModeChoice.SelectedIndex = ReadExpertMode(WorkspaceModePath) ? 1 : 0;
    }

    private void AddBasicLook(BasicLook look)
    {
        var title = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap };
        if (look.OriginalName is { } original) title.Text = original;
        else if (look.DigitalFormat is { } format)
            Localization.BindEffectText(title, TextBlock.TextProperty, DigitalCatalog.Find(format).Name);
        else title.SetResourceReference(TextBlock.TextProperty, look.TitleResource!);
        var description = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) };
        description.SetResourceReference(TextBlock.TextProperty, look.DescriptionResource);
        description.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var origin = new TextBlock { FontSize = 11, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        origin.SetResourceReference(TextBlock.TextProperty, look.OriginalName != null ? "BasicNtscrtOrigin" : "BasicTapeTubeOrigin");
        origin.SetResourceReference(TextBlock.ForegroundProperty, look.OriginalName != null ? "Accent" : "Muted");
        var content = new StackPanel { Children = { title, description, origin } };
        var button = new Button { Content = content, Tag = look.Id, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(14, 11, 14, 11) };
        if (look.AnalogEffect != BasicAnalogEffect.None)
            button.ToolTip = "NTSCRT · ntsc-rs · RetroArch";
        button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(TextBlock.Text)) { Source = title });
        button.Click += (_, _) => ApplyBasicLook(look);
        BasicLooks.Children.Add(button);
    }

    private void ApplyBasicLook(BasicLook look)
    {
        if (!_ready || (_importedLook != null && !_basicOriginalActive) || _exportCancellation != null) return;
        try
        {
            if (look.OriginalName is { } name)
            {
                var original = BasicLook.ReadOriginal(name);
                var imported = NtscrtImportedLook.Parse(original)
                    ?? throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
                ApplyImportedLook(imported with { Crop = _sourceCrop }, original, name + ".json", fromBasic: true);
            }
            else
            {
                var shader = _shaders.ContainsKey("crt-easymode") ? "crt-easymode" : _shaders.Keys.First();
                ApplyLookPreset(look.CreatePreset(shader), preserveGeometry: true);
            }
            Status.Text = Localization.T("Preset geladen.");
        }
        catch (Exception ex) { Error(ex); }
    }

    private void WorkspaceModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, WorkspaceModeChoice) || WorkspaceModeChoice.SelectedIndex < 0) return;
        if (_ready) _workspaceModeChanged = true;
        var expert = WorkspaceModeChoice.SelectedIndex == 1;
        if (expert) ExpertEffectsTab.Visibility = Visibility.Visible;
        else BasicTab.Visibility = Visibility.Visible;
        if (WorkspaceTabs.SelectedItem != OutputTab)
            WorkspaceTabs.SelectedItem = expert ? ExpertEffectsTab : BasicTab;
        BasicTab.Visibility = expert ? Visibility.Collapsed : Visibility.Visible;
        ExpertEffectsTab.Visibility = ExpertAnimationTab.Visibility = expert ? Visibility.Visible : Visibility.Collapsed;
    }

    internal static bool ReadExpertMode(string path)
    {
        try { return File.Exists(path) && File.ReadAllText(path).Trim() == "expert"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    internal static void WriteWorkspaceMode(string path, bool expert)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, expert ? "expert" : "basic");
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void SaveWorkspaceMode()
    {
        if (!_workspaceModeChanged) return;
        try { WriteWorkspaceMode(WorkspaceModePath, WorkspaceModeChoice.SelectedIndex == 1); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning("Workspace mode preference could not be saved: {0}", ex.Message);
        }
    }
}
