// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private Dictionary<string, double> _analogValues = AnalogCatalog.Current.Defaults();

    private AnalogSettings ReadAnalogSettings() =>
        new(VcrEnabled.IsChecked == true, ReceptionEnabled.IsChecked == true, new(_analogValues));

    private void AnalogToggleChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (VcrEnabled.IsChecked == true || ReceptionEnabled.IsChecked == true) _renderer = DigitalCatalog.Profile;
        RefreshEffectAvailability();
        Schedule();
    }

    private void RestoreAnalog(AnalogSettings? settings)
    {
        _analogValues = settings == null ? AnalogCatalog.Current.Defaults() : new(settings.Values);
        VcrEnabled.IsChecked = settings?.Vcr == true;
        ReceptionEnabled.IsChecked = settings?.Reception == true;
        BuildAnalogGroup(TapeControls, "Tape", AnalogCatalog.Current.Tapes);
        BuildAnalogGroup(DeckControls, "Deck", AnalogCatalog.Current.Decks);
        BuildAnalogGroup(ReceptionControls, "Reception");
    }

    private void BuildAnalogGroup(StackPanel panel, string group, AnalogCondition[]? conditions = null)
    {
        panel.Children.Clear();
        ComboBox? picker = null;
        if (conditions != null)
        {
            picker = new ComboBox { Margin = new Thickness(0, 4, 0, 10) };
            var custom = new ComboBoxItem();
            custom.SetResourceReference(ContentControl.ContentProperty, "DigitalCustomLabel");
            picker.Items.Add(custom);
            foreach (var condition in conditions)
            {
                var item = EffectChoice(condition.Name);
                item.Tag = condition;
                Localization.BindEffectText(item, ToolTipProperty, condition.Help);
                picker.Items.Add(item);
            }
            var matching = Array.FindIndex(conditions, c => c.Values.All(p => Math.Abs(_analogValues[p.Key] - p.Value) < 1e-6));
            picker.SelectedIndex = matching + 1;
            picker.SelectionChanged += (_, _) =>
            {
                if (!_ready || picker.SelectedItem is not ComboBoxItem { Tag: AnalogCondition condition }) return;
                foreach (var (key, value) in condition.Values) _analogValues[key] = value;
                BuildAnalogGroup(panel, group, conditions);
                Schedule();
            };
            panel.Children.Add(picker);
        }

        var conditionKeys = conditions?.FirstOrDefault()?.Values;
        string? section = null;
        foreach (var parameter in AnalogCatalog.Current.Parameters.Where(p => p.Group == group))
        {
            if (parameter.Section != section)
            {
                section = parameter.Section;
                if (section != null) panel.Children.Add(EffectLabel(section));
            }
            var control = new StackPanel();
            Localization.BindEffectText(control, ToolTipProperty, parameter.Help);
            panel.Children.Add(control);
            AddAnalogControl(control, parameter, value =>
            {
                _analogValues[parameter.Id] = value;
                if (picker != null && conditionKeys?.ContainsKey(parameter.Id) == true) picker.SelectedIndex = 0;
                Schedule();
            });
        }
    }

    private void AddAnalogControl(StackPanel panel, AnalogParameter parameter, Action<double> change)
    {
        var value = _analogValues[parameter.Id];
        switch (parameter.Kind)
        {
            case "toggle":
                var toggle = new CheckBox { IsChecked = value >= 0.5 };
                Localization.BindEffectText(toggle, ContentControl.ContentProperty, parameter.Label);
                toggle.Click += (_, _) => change(toggle.IsChecked == true ? 1 : 0);
                panel.Children.Add(toggle);
                break;
            case "choice":
                panel.Children.Add(EffectLabel(parameter.Label));
                var choice = new ComboBox();
                Localization.BindEffectText(choice, System.Windows.Automation.AutomationProperties.NameProperty, parameter.Label);
                foreach (var option in parameter.Choices) choice.Items.Add(EffectChoice(option));
                choice.SelectedIndex = (int)value;
                choice.SelectionChanged += (_, _) => { if (choice.SelectedIndex >= 0) change(choice.SelectedIndex); };
                panel.Children.Add(choice);
                break;
            case "seed":
                var cassette = new StackPanel { Orientation = Orientation.Horizontal };
                var cassetteLabel = Label("");
                cassetteLabel.SetResourceReference(TextBlock.TextProperty, "CassetteLabel");
                var seed = Label(": " + ((int)value).ToString(CultureInfo.CurrentCulture));
                cassette.Children.Add(cassetteLabel);
                cassette.Children.Add(seed);

                var actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
                var another = new Button();
                another.SetResourceReference(ContentControl.ContentProperty, "NextCassetteLabel");
                var reset = new Button { IsEnabled = value != parameter.Default };
                reset.SetResourceReference(ContentControl.ContentProperty, "ResetCassetteLabel");
                void SetCassette(int number)
                {
                    change(number);
                    seed.Text = ": " + number.ToString(CultureInfo.CurrentCulture);
                    reset.IsEnabled = number != parameter.Default;
                }
                another.Click += (_, _) =>
                {
                    var next = _analogValues[parameter.Id] >= parameter.Max ? 0 : (int)_analogValues[parameter.Id] + 1;
                    SetCassette(next);
                };
                reset.Click += (_, _) => SetCassette((int)parameter.Default);
                actions.Children.Add(another);
                actions.Children.Add(reset);
                var hint = Label("");
                hint.SetResourceReference(TextBlock.TextProperty, "CassetteHintLabel");
                panel.Children.Add(cassette);
                panel.Children.Add(actions);
                panel.Children.Add(hint);
                break;
            default:
                var factor = parameter.Percent ? 100 : 1;
                var unit = parameter.Percent ? "%" : parameter.Unit;
                AddSlider(panel, parameter.Label, parameter.Min * factor, parameter.Max * factor, value * factor,
                    parameter.Percent ? 1 : 0.1, v => change(v / factor), effectText: true,
                    suffix: unit.Length == 0 ? "" : $" ({unit})");
                break;
        }
    }
}
