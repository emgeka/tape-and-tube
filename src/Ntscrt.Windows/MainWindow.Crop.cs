// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private SourceCrop? _sourceCrop, _previewCrop;
    private bool _syncCrop;

    private void InitializeCrop()
    {
        foreach (var ratio in SourceCrop.Ratios)
            SourceRatioChoice.Items.Add(new ComboBoxItem { Tag = ratio, Content = ratio });
        RestoreCrop(null);
    }

    private void SourceRatioChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncCrop) return;
        var ratio = (SourceRatioChoice.SelectedItem as ComboBoxItem)?.Tag as string;
        _sourceCrop = ratio == null ? null : (_sourceCrop ?? new(ratio)) with { Ratio = ratio };
        BuildCropControls();
        CropChanged();
    }

    private void RestoreCrop(SourceCrop? crop)
    {
        _sourceCrop = crop;
        _syncCrop = true;
        try
        {
            if (crop == null) SourceRatioChoice.SelectedIndex = 0;
            else
            {
                var choice = SourceRatioChoice.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, crop.Ratio));
                if (choice == null)
                {
                    choice = new ComboBoxItem { Tag = crop.Ratio, Content = crop.Ratio };
                    SourceRatioChoice.Items.Add(choice);
                }
                SourceRatioChoice.SelectedItem = choice;
            }
            BuildCropControls();
        }
        finally { _syncCrop = false; }
    }

    private void BuildCropControls()
    {
        SourceCropControls.Children.Clear();
        if (_sourceCrop is not { } crop) return;
        AddSlider(SourceCropControls, Localization.T("Horizontal (%)"), 0, 100, crop.X * 100, 1, x =>
        {
            _sourceCrop = _sourceCrop! with { X = x / 100 };
            CropChanged();
        }, integer: true);
        AddSlider(SourceCropControls, Localization.T("Vertikal (%)"), 0, 100, crop.Y * 100, 1, y =>
        {
            _sourceCrop = _sourceCrop! with { Y = y / 100 };
            CropChanged();
        }, integer: true);
        AddSlider(SourceCropControls, Localization.T("Quellzoom"), 1, SourceCrop.MaxZoom, crop.Zoom, 0.01, zoom =>
        {
            _sourceCrop = _sourceCrop! with { Zoom = zoom };
            CropChanged();
        });
        var reset = new Button { HorizontalAlignment = HorizontalAlignment.Left };
        reset.SetResourceReference(ContentControl.ContentProperty, "SourceCropResetLabel");
        reset.Click += (_, _) =>
        {
            RestoreCrop(new(_sourceCrop!.Ratio));
            CropChanged();
        };
        SourceCropControls.Children.Add(reset);
    }

    private void CropChanged()
    {
        if (!_ready || _syncCrop) return;
        PauseVideo();
        Schedule();
    }
}
