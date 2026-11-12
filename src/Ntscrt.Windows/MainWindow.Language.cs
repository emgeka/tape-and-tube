// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private bool _syncLanguage;
    private void InitializeLanguage()
    {
        _syncLanguage = true;
        if (LanguageChoice.Items.Count == 1)
            foreach (var language in Localization.Supported)
                LanguageChoice.Items.Add(new ComboBoxItem { Tag = language.Code, Content = language.NativeName });
        LanguageChoice.SelectedItem = LanguageChoice.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == Localization.Preference);
        _syncLanguage = false;
    }
    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncLanguage || !_ready || LanguageChoice.SelectedItem is not ComboBoxItem item) return;
        try
        {
            Localization.SetPreference((string)item.Tag);
            PlayButton.Content = _playing ? Localization.T("Pause") : Localization.T("Abspielen");
            UpdateTransport();
            if (_outputInputValid) UpdateOutputInfo();
            else OutputSizeInfo.Text = Localization.T("Bitte gültige ganze Maße eingeben: 1–8192 Pixel, zusammen höchstens 24 Megapixel.");
            if (_exportCancellation == null) Status.Text = Localization.T("Sprache geändert.");
        }
        catch (Exception ex)
        {
            InitializeLanguage();
            Status.Text = Localization.T("Die Sprachwahl konnte nicht gespeichert werden: ") + ex.Message;
        }
    }
}
