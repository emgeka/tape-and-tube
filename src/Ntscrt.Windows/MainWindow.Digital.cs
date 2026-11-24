// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private string _renderer="legacy-v1", _sourceIdentity=Guid.NewGuid().ToString("N");
    private Dictionary<string,double> _digitalValues=DigitalCatalog.Defaults;
    private bool _syncDigital;
    private string? SelectedDownscaleMethod => DownscaleMethodChoice.SelectedValue as string;
    private bool? SelectedDownscaleEnabled => DownscaleEnabledChoice.SelectedIndex switch
    { 1 => true, 2 => false, _ => null };
    private string OwnRenderer => InScreenLoop || OwnAnimationEnabled || SelectedDownscaleMethod != null || SelectedDownscaleEnabled.HasValue
        ? DigitalCatalog.Profile : _renderer;

    private void DownscaleEnabledChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        RefreshEffectAvailability();
        Schedule();
    }

    private void DownscaleMethodChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) Schedule();
    }
    private void InitializeDigital()
    {
        foreach (var look in DigitalCatalog.Looks)
        {
            var item = EffectChoice(look.Name);
            item.Tag = look.Id;
            DigitalFormatChoice.Items.Add(item);
        }
        BuildDigitalControls();
    }
    private void SelectRetroWidth(int width)
    {
        var item=WidthChoice.Items.OfType<ComboBoxItem>().FirstOrDefault(i=>i.Content.ToString()==width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if(item==null){item=new ComboBoxItem{Content=width.ToString(System.Globalization.CultureInfo.InvariantCulture)};WidthChoice.Items.Add(item);}
        WidthChoice.SelectedItem=item;
    }
    private void DigitalToggleChanged(object sender,RoutedEventArgs e)
    {
        if(!_ready||_syncDigital)return;
        if (DigitalEnabled.IsChecked == true) _renderer = DigitalCatalog.Profile;
        RefreshEffectAvailability();
        Schedule();
    }
    private void DigitalFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncDigital || DigitalFormatChoice.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        _syncDigital = true;
        try
        {
            var format = DigitalCatalog.Find(id);
            _digitalValues = new(format.Values);
            _renderer = DigitalCatalog.Profile;
            DigitalEnabled.IsChecked = true;
            SelectRetroWidth(format.Width);
            BuildDigitalControls();
        }
        finally { _syncDigital = false; }
        RefreshEffectAvailability();
        Schedule();
    }

    private void RetroWidthChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncDigital) return;
        DigitalFormatChoice.SelectedIndex = 0;
        Schedule();
    }
    private void BuildDigitalControls()
    {
        DigitalControls.Children.Clear();
        foreach(var node in DigitalCatalog.Parameters)
        {
            var p=node!.AsObject();var id=p["id"]!.ToString();var label=p["label"]!.ToString();var kind=p["kind"]!;
            if(kind["type"]!.ToString()=="choice")
            {
                DigitalControls.Children.Add(EffectLabel(label));
                var choice=new ComboBox{Margin=new Thickness(0,2,0,8)};
                Localization.BindEffectText(choice, System.Windows.Automation.AutomationProperties.NameProperty, label);
                foreach(var value in kind["choices"]!.AsArray())choice.Items.Add(EffectChoice(value!.ToString()));
                choice.SelectedIndex=(int)_digitalValues[id];
                choice.SelectionChanged+=(_,_)=>{if(choice.SelectedIndex>=0){_digitalValues[id]=choice.SelectedIndex;DigitalEdited();}};
                DigitalControls.Children.Add(choice);
            }
            else
            {
                var min=kind["minimum"]!.GetValue<double>();var max=kind["maximum"]!.GetValue<double>();
                var step=kind["step"]?.GetValue<double>()??(max-min)/1000;
                AddSlider(DigitalControls,label,min,max,_digitalValues[id],step,v=>{_digitalValues[id]=v;DigitalEdited();},integer:kind["step"]!=null,effectText:true);
            }
        }
    }
    private void DigitalEdited()
    {
        if(!_ready||_syncDigital)return;
        DigitalFormatChoice.SelectedIndex=0;_renderer=DigitalCatalog.Profile;Schedule();
    }
}
