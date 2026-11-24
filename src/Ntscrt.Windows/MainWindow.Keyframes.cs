// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private EffectTimeline _effectTimeline = new(false, EffectTimeline.DefaultSeconds, []);
    private (EffectTimeline Track, string Shader, JsonObject Payload)? _timelinePayload;
    private IReadOnlyList<ShaderParameter> _keyframeShaderParameters = [];
    private bool _syncKeyframes;

    private bool OwnAnimationEnabled => _effectTimeline.Enabled && _importedLook == null && !InScreenLoop;
    private bool HasOwnAnimation => OwnAnimationEnabled && _effectTimeline.Keys.Length > 0;
    private EffectKeyframe? SelectedKeyframe => KeyframeChoice.SelectedItem as ComboBoxItem is { Tag: EffectKeyframe key } ? key : null;

    private void InitializeKeyframes()
    {
        AnimationFpsChoice.ItemsSource = EffectTimeline.StillFrameRates;
        foreach (var easing in EffectTimeline.Easings)
        {
            var item = new ComboBoxItem { Tag = easing };
            item.SetResourceReference(ContentControl.ContentProperty, "KeyframeEasing" + KeyframeEasing.Items.Count);
            KeyframeEasing.Items.Add(item);
        }
        RestoreKeyframes(null);
    }

    private void RestoreKeyframes(EffectTimeline? track)
    {
        _effectTimeline = track ?? new(false, EffectTimeline.DefaultSeconds, []);
        _syncKeyframes = true;
        try
        {
            AnimationEnabled.IsChecked = _effectTimeline.Enabled;
            KeyframeAudition.IsChecked = false;
            AnimationFpsChoice.SelectedItem = _effectTimeline.Fps;
            AnimationStillOptions.Children.Clear();
            var step = 0.5;
            var value = _effectTimeline.Seconds;
            AddSlider(AnimationStillOptions, Localization.T("Standbilddauer (Sekunden)"), 0.5, NtscrtImportedLook.MaxStillSeconds, value, step,
                seconds =>
                {
                    if (_syncKeyframes || !_ready) return;
                    _effectTimeline = _effectTimeline with { Seconds = seconds };
                    RefreshStillAnimation();
                });
        }
        finally { _syncKeyframes = false; }
        RefreshKeyframeList();
    }

    private void ChangeStillAnimationRate(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncKeyframes || AnimationFpsChoice.SelectedItem is not int fps) return;
        var seconds = _frameIndex / (double)_effectTimeline.Fps;
        _effectTimeline = _effectTimeline with { Fps = fps };
        if (_video == null) _frameIndex = (int)Math.Round(seconds * fps);
        RefreshStillAnimation();
    }

    private void RefreshStillAnimation()
    {
        PauseVideo();
        RefreshTimeline();
        Schedule();
    }

    private void RefreshKeyframeList(EffectKeyframe? selected = null)
    {
        _syncKeyframes = true;
        try
        {
            KeyframeChoice.Items.Clear();
            foreach (var key in _effectTimeline.Keys.OrderBy(key => key.T))
            {
                var item = new ComboBoxItem { Content = key.T.ToString("0.###%", System.Globalization.CultureInfo.CurrentUICulture), Tag = key };
                KeyframeChoice.Items.Add(item);
                if (ReferenceEquals(key, selected)) KeyframeChoice.SelectedItem = item;
            }
            UpdateEasingChoice();
        }
        finally { _syncKeyframes = false; }
        RefreshKeyframeAvailability();
    }

    private void UpdateEasingChoice()
    {
        KeyframeEasing.SelectedIndex = SelectedKeyframe is { } key ? Array.IndexOf(EffectTimeline.Easings, key.Easing) : -1;
    }

    private void RefreshKeyframeAvailability()
    {
        var unavailable = InScreenLoop || _importedLook != null;
        KeyframesUnavailable.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        KeyframeEditor.Visibility = unavailable ? Visibility.Collapsed : Visibility.Visible;
        var active = OwnAnimationEnabled;
        AnimationControls.IsEnabled = active;
        AnimationStillSettings.Visibility = _video == null ? Visibility.Visible : Visibility.Collapsed;
        CaptureKeyframeButton.IsEnabled = active && TimelineInfo != null &&
            (CrtEnabled.IsChecked != true ||
                (_controlsRenderer == DigitalCatalog.Profile && _controlsShader == _shaders[(string)ShaderChoice.SelectedItem]));
        SelectedKeyframeControls.IsEnabled = active && SelectedKeyframe != null;
        UpdateKeyframeMarkers();
    }

    private void ResizeKeyframeMarkers(object sender, SizeChangedEventArgs e) => UpdateKeyframeMarkers();

    private void UpdateKeyframeMarkers()
    {
        KeyframeMarkers.Children.Clear();
        var visible = HasOwnAnimation && TimelineInfo != null;
        KeyframeMarkers.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible || KeyframeMarkers.ActualWidth <= 0) return;
        foreach (var key in _effectTimeline.Keys)
        {
            var marker = new Border { Width = 3, Height = 5, Background = (Brush)FindResource("Accent") };
            Canvas.SetLeft(marker, key.T * KeyframeMarkers.ActualWidth - 1.5);
            KeyframeMarkers.Children.Add(marker);
        }
    }

    private void ToggleOwnAnimation(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncKeyframes) return;
        PauseVideo();
        _effectTimeline = _effectTimeline with { Enabled = AnimationEnabled.IsChecked == true };
        if (!_effectTimeline.Enabled) SetAudition(false);
        RefreshTimeline();
        Schedule();
    }

    private void ToggleKeyframeAudition(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncKeyframes) return;
        PauseVideo();
        Schedule();
    }

    private void SetAudition(bool enabled)
    {
        _syncKeyframes = true;
        try { KeyframeAudition.IsChecked = enabled; }
        finally { _syncKeyframes = false; }
    }

    private JsonObject? AnimationPayload(string shaderName, bool preview)
    {
        if (!HasOwnAnimation || (preview && KeyframeAudition.IsChecked == true && !_playing)) return null;
        if (_timelinePayload is not { } cached || !ReferenceEquals(cached.Track, _effectTimeline) || cached.Shader != shaderName)
        {
            cached = (_effectTimeline, shaderName, _effectTimeline.WorkerTimeline(shaderName));
            _timelinePayload = cached;
        }
        return cached.Payload;
    }

    private double PlayheadPosition(VideoInfo info) => _frameIndex / (double)Math.Max(1, info.FrameCount - 1);
    private static int KeyframeIndex(EffectKeyframe key, VideoInfo info) => (int)Math.Round(key.T * (info.FrameCount - 1));

    private void CaptureKeyframe(object sender, RoutedEventArgs e)
    {
        if (!OwnAnimationEnabled || TimelineInfo is not VideoInfo info || !CaptureKeyframeButton.IsEnabled) return;
        PauseVideo();
        var existing = SelectedKeyframe is { } selected && KeyframeIndex(selected, info) == _frameIndex
            ? selected : _effectTimeline.Keys.FirstOrDefault(key => KeyframeIndex(key, info) == _frameIndex);
        if (existing == null && _effectTimeline.Keys.Length >= EffectTimeline.MaxKeys)
        {
            Status.Text = Localization.T("Das Keyframe-Limit ist erreicht.");
            return;
        }
        var parameters = CrtEnabled.IsChecked == true
            ? _keyframeShaderParameters.ToDictionary(p => p.Name, p => _shaderValues.GetValueOrDefault(p.Name, p.Initial))
            : new Dictionary<string, float>(_shaderValues);
        var keyframe = new EffectKeyframe(existing?.T ?? PlayheadPosition(info), existing?.Easing ?? EffectTimeline.Easings[0],
            (string)ShaderChoice.SelectedItem, parameters, (JsonObject)_ntsc.DeepClone(), new(_analogValues), new(_digitalValues));
        SetAudition(false);
        ReplaceKeyframe(existing, keyframe);
    }

    private void SelectKeyframe(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncKeyframes || SelectedKeyframe is not { } key) return;
        _syncKeyframes = true;
        try { UpdateEasingChoice(); }
        finally { _syncKeyframes = false; }
        RefreshKeyframeAvailability();
        if (TimelineInfo is VideoInfo info) JumpToFrame(KeyframeIndex(key, info));
    }

    private void ChangeKeyframeEasing(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_syncKeyframes && SelectedKeyframe is { } key &&
            KeyframeEasing.SelectedItem is ComboBoxItem { Tag: string easing } && key.Easing != easing)
            ReplaceKeyframe(key, key with { Easing = easing });
    }

    private void EditKeyframe(object sender, RoutedEventArgs e)
    {
        if (!OwnAnimationEnabled || SelectedKeyframe is not { } key || TimelineInfo is not VideoInfo info) return;
        PauseVideo();
        _ntsc = (JsonObject)key.Ntsc.DeepClone();
        BuildNtscControls();
        _digitalValues = new(key.Digital);
        DigitalFormatChoice.SelectedIndex = 0;
        BuildDigitalControls();
        RestoreAnalog(ReadAnalogSettings() with { Values = key.Glitch });
        if (key.ShaderName == (string)ShaderChoice.SelectedItem)
        {
            _shaderValues.Clear();
            foreach (var (id, value) in key.Shader) _shaderValues[id] = value;
            BuildShaderControls(_keyframeShaderParameters);
        }
        SetAudition(true);
        JumpToFrame(KeyframeIndex(key, info));
    }

    private void MoveKeyframe(object sender, RoutedEventArgs e)
    {
        if (!OwnAnimationEnabled || SelectedKeyframe is not { } key || TimelineInfo is not VideoInfo info) return;
        if (_effectTimeline.Keys.Any(other => !ReferenceEquals(other, key) && KeyframeIndex(other, info) == _frameIndex))
        {
            Status.Text = Localization.T("An dieser Position liegt bereits ein Keyframe.");
            return;
        }
        ReplaceKeyframe(key, key with { T = PlayheadPosition(info) });
    }

    private void DeleteKeyframe(object sender, RoutedEventArgs e)
    {
        if (OwnAnimationEnabled && SelectedKeyframe is { } key) ReplaceKeyframe(key, null);
    }

    private void ReplaceKeyframe(EffectKeyframe? previous, EffectKeyframe? next)
    {
        PauseVideo();
        var remaining = _effectTimeline.Keys.Where(key => !ReferenceEquals(key, previous));
        _effectTimeline = _effectTimeline with { Keys = next == null ? remaining.ToArray() : remaining.Append(next).ToArray() };
        RefreshKeyframeList(next);
        RefreshVideoExportButton();
        Schedule();
    }
}
