// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private UpdatePreferences _updates = new(UpdateChannel.Release);
    private readonly CancellationTokenSource _updateLifetime = new();
    private UpdateRelease? _availableUpdate;
    private bool _updateCheckBusy;

    private void InitializeUpdates()
    {
        _updates = UpdateService.ReadPreferences(UpdateService.SettingsPath);
        BuildChannelBadge.Visibility = UpdateService.BuildChannel == UpdateChannel.Beta ? Visibility.Visible : Visibility.Collapsed;
        Title = "Tape & Tube" + (UpdateService.BuildChannel == UpdateChannel.Beta ? " · Beta" : "");
        UpdatesButton.Content = Localization.T("Updates");
        Localization.Changed += RefreshUpdateNotice;
        Closed += (_, _) => { _updateLifetime.Cancel(); Localization.Changed -= RefreshUpdateNotice; };
        Loaded += async (_, _) =>
        {
            if (!_updates.Automatic) return;
            try { await Task.Delay(TimeSpan.FromSeconds(3), _updateLifetime.Token); await CheckForUpdate(); }
            catch (OperationCanceledException) { }
        };
    }
    private void RefreshUpdateNotice()
    {
        var text = _availableUpdate == null ? Localization.T("Updates") : Localization.F($"Update verfügbar: {_availableUpdate.Version}");
        UpdatesButton.Content = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 160 };
        UpdatesButton.ToolTip = text;
    }

    private async Task<string> CheckForUpdate()
    {
        if (_updateCheckBusy) return Localization.T("Prüfung läuft…");
        _updateCheckBusy = true;
        var channel = _updates.Channel;
        try
        {
            var update = await UpdateService.CheckAsync(channel, _updateLifetime.Token);
            if (_updates.Channel != channel || _closing) return "";
            _availableUpdate = update;
            RefreshUpdateNotice();
            return update == null ? Localization.T("Keine neuere Version verfügbar.") : Localization.F($"Update verfügbar: {update.Version}");
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or System.Text.Json.JsonException or OperationCanceledException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            return Localization.T("Updateprüfung fehlgeschlagen.");
        }
        finally { _updateCheckBusy = false; }
    }
    private void ShowUpdates(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = Localization.F($"Aktuelle Version: {UpdateService.CurrentVersion}"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = Localization.T("Update-Kanal"), Margin = new Thickness(0, 0, 0, 6) });
        var channel = new ComboBox { ItemsSource = new[] { Localization.T("Release – stabile Versionen"), Localization.T("Beta – stabile Versionen und Vorabversionen") }, SelectedIndex = (int)_updates.Channel, Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(channel);
        var automatic = new CheckBox { Content = Localization.T("Beim Start automatisch nach Updates suchen"), IsChecked = _updates.Automatic, Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(automatic);
        var status = new TextBlock { Text = _availableUpdate == null ? "" : Localization.F($"Update verfügbar: {_availableUpdate.Version}"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(status);
        var buttons = new WrapPanel(); panel.Children.Add(buttons);
        var check = new Button { Content = Localization.T("Jetzt prüfen") };
        var download = new Button { Content = Localization.T("Download öffnen"), IsEnabled = _availableUpdate != null };
        buttons.Children.Add(check); buttons.Children.Add(download);
        panel.Children.Add(new TextBlock { Text = Localization.T("Installation wird von dir gestartet."), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSteelBlue, Margin = new Thickness(0, 14, 0, 0) });
        var dialog = new Window { Title = "Tape & Tube · " + Localization.T("Updates"), Owner = this, Width = 560, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Foreground = Foreground, Content = panel };
        void Save()
        {
            try
            {
                var next = new UpdatePreferences((UpdateChannel)channel.SelectedIndex, automatic.IsChecked == true);
                UpdateService.WritePreferences(UpdateService.SettingsPath, next); _updates = next;
                _availableUpdate = null; RefreshUpdateNotice(); download.IsEnabled = false; status.Text = "";
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { status.Text = Localization.T("Einstellung konnte nicht gespeichert werden."); }
        }
        channel.SelectionChanged += (_, _) => Save(); automatic.Click += (_, _) => Save();
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false; status.Text = Localization.T("Prüfung läuft…");
            status.Text = await CheckForUpdate();
            check.IsEnabled = true; download.IsEnabled = _availableUpdate != null;
        };
        download.Click += (_, _) =>
        {
            if (_availableUpdate is { } update && UpdateService.TrustedPage(update.Page))
                try { Process.Start(new ProcessStartInfo(update.Page.AbsoluteUri) { UseShellExecute = true }); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { status.Text = Localization.T("Downloadseite konnte nicht geöffnet werden."); }
        };
        dialog.ShowDialog();
    }
}
