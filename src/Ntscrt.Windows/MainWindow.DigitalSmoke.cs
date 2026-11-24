// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    internal static async Task CheckStillMotionAsync(Action<bool, string> check, CancellationToken token)
    {
        var app = Application.Current;
        var shutdownMode = app.ShutdownMode;
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow? window = null;
        try
        {
            window = new MainWindow();
            window.LoadDemo();
            check(window.StillMotionChoice.Visibility == Visibility.Visible && !window.StillMotionActive,
                "Still motion is available and initially off for an ordinary image");
            window.DigitalFormatChoice.SelectedItem = window.DigitalFormatChoice.Items.OfType<ComboBoxItem>()
                .Single(item => Equals(item.Tag, "minidv"));
            window.NtscEnabled.IsChecked = true;
            window._ntsc["composite_noise"] = true;
            window._ntsc["composite_noise_intensity"] = 0.5;
            window.StillMotionChoice.IsChecked = true;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            while (window._output == null || window._stillMotionFrame < 1) await Task.Delay(20, timeout.Token);
            var pixels = window._output.Pixels;
            while (window._output.Pixels.SequenceEqual(pixels)) await Task.Delay(20, timeout.Token);
            check(window.TimelineInfo == null && window.Settings(preview: false).Frame == 0
                && window.ExportButton.IsEnabled && !window.VideoExportButton.IsEnabled,
                "Live artifacts change the rendered still without creating an animation or replacing the PNG action");
            window.AnimationEnabled.IsChecked = true;
            check(window.TimelineInfo is { IsStill: true } && !window.StillMotionActive
                && window.StillMotionChoice.Visibility == Visibility.Collapsed,
                "An explicit animation takes over the preview clock");
        }
        finally
        {
            window?.Close();
            app.ShutdownMode = shutdownMode;
        }
    }

    internal static async Task CheckPreparedPreviewAsync(string input, Action<bool, string> check, CancellationToken token)
    {
        var app = Application.Current;
        var shutdownMode = app.ShutdownMode;
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow? window = null;
        try
        {
            window = new MainWindow();
            window.DigitalFormatChoice.SelectedItem = window.DigitalFormatChoice.Items.OfType<ComboBoxItem>()
                .Single(item => Equals(item.Tag, "minidv"));
            await window.LoadVideo(input);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            while (window._preparedFrames.Count < 2) await Task.Delay(20, timeout.Token);
            check(window._frameIndex == 0 && window._preparedBytes <= PreviewBufferBytes,
                "Paused preview prepares upcoming video frames within the buffer budget");
            var prepared = window._preparedFrames.Peek();
            var video = window._video!;
            var settings = window.Settings() with { Frame = (uint)prepared.Index };
            window.TogglePlayback(window, new RoutedEventArgs());
            window.SetFrame(prepared.Index);
            window.InvalidateRender(discardPrepared: false);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await window.Render();
            var cachedMs = clock.Elapsed.TotalMilliseconds;
            window.PauseVideo();
            check(ReferenceEquals(window._output, prepared.Rendered.Frame), $"Resuming presents the prepared bitmap ({cachedMs:F2} ms)");

            using var pipeline = new EffectPipeline();
            await using var reference = await VideoSource.OpenAsync(input, token);
            var next = await reference.ReadFrameAsync(prepared.Index, token);
            var expected = await pipeline.RenderAsync(next, settings, "comparison", video.Info, reference.ReadFrameAsync, token);
            check(expected.Frame.Pixels.SequenceEqual(prepared.Rendered.Frame.Pixels),
                "Prepared MiniDV frame matches a normal render with its temporal history");
            window.JumpToFrame(0);
            check(window._preparedFrames.Count == 0 && window._preparedBytes == 0,
                "Seeking immediately invalidates the prepared preview frames");
        }
        finally
        {
            window?.Close();
            app.ShutdownMode = shutdownMode;
        }
    }

    internal static void CheckDigitalFormatSelection(Action<bool, string> check)
    {
        var app = Application.Current;
        var shutdownMode = app.ShutdownMode;
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var window = new MainWindow();
            try
            {
                check(window._ready && new[] { window.DigitalEnabled, window.NtscEnabled, window.VcrEnabled,
                    window.ReceptionEnabled, window.CrtEnabled, window.ScreenLoopEnabled }.All(toggle => toggle.IsChecked == false),
                    "The effects workspace initializes with every effect off");
                check(!window.NtscControls.IsEnabled && !window.ShaderChoice.IsEnabled && !window.DigitalControls.IsEnabled
                    && !window.VcrControls.IsEnabled && !window.ReceptionControls.IsEnabled && !window.ScreenLoopSettingsPanel.IsEnabled
                    && !window.WidthChoice.IsEnabled && !window.ScaleChoice.IsEnabled,
                    "Inactive effects and unused processing settings start disabled");
                window.ShaderChoice.SelectedItem = "crtsim";
                window.NtscEnabled.IsChecked = true;
                check(window.NtscControls.IsEnabled && !window.WidthChoice.IsEnabled, "NTSC can be enabled independently");
                window.CrtEnabled.IsChecked = true;
                check(window.ShaderChoice.IsEnabled && window.WidthChoice.IsEnabled && window.ScaleChoice.IsEnabled,
                    "CRT activates its details and automatic scaling controls");
                window.RestoreOutput(new OutputSettings(960, 1707, OutputFit.Crop));
                check(!window.ScaleChoice.IsEnabled && window.WidthChoice.IsEnabled, "Fixed output disables CRT scale only");
                window.CrtEnabled.IsChecked = false;
                window.ScaleChoice.SelectedIndex = 2;
                window._ntsc["random_seed"] = 12345;
                window._shaderValues["custom-test-value"] = 0.5f;
                var original = window.Settings();
                window.VcrEnabled.IsChecked = true;
                var tape = (ComboBox)window.TapeControls.Children[0];
                tape.SelectedItem = tape.Items.OfType<ComboBoxItem>().Single(i => i.Tag is AnalogCondition { Name: "Moldy" });
                window.ReceptionEnabled.IsChecked = true;
                var analog = window.Settings();
                check(window.VcrControls.IsEnabled && window.ReceptionControls.IsEnabled && window.WidthChoice.IsEnabled
                    && analog.Renderer == DigitalCatalog.Profile && analog.Analog is { Vcr: true, Reception: true }
                    && analog.Analog.Values["tape_mold"] > 0 && analog.NtscJson == original.NtscJson
                    && analog.Shader == original.Shader && analog.Output == original.Output,
                    "VCR condition and reception switches apply on the window without replacing NTSC, CRT or output settings");
                window.RestoreAnalog(null);
                window.LoadDemo();
                window.PreviewHost.Measure(new Size(800, 600));
                window.PreviewHost.Arrange(new Rect(0, 0, 800, 600));
                window.PreviewZoomChoice.Value = 2;
                check(window.PreviewNavigation.Visibility == Visibility.Visible && window.Preview.Width == 1600
                    && window.Preview.Height == 1200 && window.FitPreviewButton.IsEnabled,
                    "The preview zoom control enlarges the displayed image");
                window.FitPreviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(window.PreviewZoomChoice.Value == 1 && window.Preview.Width == 800 && !window.FitPreviewButton.IsEnabled,
                    "Fit view resets the preview zoom");
                window.IntegerPreviewChoice.IsChecked = true;
                var previewDpi = System.Windows.Media.VisualTreeHelper.GetDpi(window.PreviewHost);
                var displayedRowsPerLine = window.Preview.Height * previewDpi.DpiScaleY / window._source!.Height;
                check(displayedRowsPerLine >= 1 && Math.Abs(displayedRowsPerLine - Math.Round(displayedRowsPerLine)) < 1e-6,
                    "Integer scaling uses whole display pixels per line in the preview");
                window.IntegerPreviewChoice.IsChecked = false;
                window.ScreenLoopPresetChoice.SelectedIndex = 2;
                check(window.InScreenLoop && !window.DigitalPanel.IsEnabled && window.DigitalPausedHint.Visibility == Visibility.Visible
                    && window.TimelineInfo is { IsStill: true, FrameCount: 60 }
                    && window.VideoExportButton.IsEnabled && window.Settings().ScreenLoop != null,
                    "Selecting a Screen Loop preset activates the mode, animates a still and enables MP4 export");
                window._output = window._source;
                window.UpdatePreview();
                var pad = new Rect(40, 60, 800, 400);
                var target = new Point(0.25, -0.25);
                check(ScreenLoopPoints.FromScreen(ScreenLoopPoints.OnScreen(target, pad), pad) == target
                    && window.ScreenLoopPadLayer.Visibility == Visibility.Visible,
                    "Screen Loop handles are displayed and map to the picture coordinates");
                window.ApplyScreenLoopPoints(new ScreenLoopPoints(new Point(-0.25, 0.25), target));
                var padSettings = window.Settings().ScreenLoop;
                check(padSettings?["drift_x"] == 0.5 && padSettings["drift_y"] == -0.5
                    && window._screenLoopPointSliders["drift_x"].Value == 50 && window.ScreenLoopPresetChoice.SelectedIndex == 0,
                    "Editing the Screen Loop handles synchronizes the effect, sliders and custom preset state");
                window.ResetScreenLoopHandle(end: true);
                check(window.ReadScreenLoopPoints().Start == window.ReadScreenLoopPoints().End,
                    "Resetting the drift ring returns it to the vanishing point");
                window.ShowOriginal.IsChecked = true;
                check(window.ScreenLoopPadLayer.Visibility == Visibility.Collapsed, "Screen Loop handles are hidden in the original preview");
                window.ShowOriginal.IsChecked = false;
                window.ScreenLoopEnabled.IsChecked = false;
                check(window.DigitalPanel.IsEnabled && window.DigitalPausedHint.Visibility == Visibility.Collapsed
                    && window.TimelineInfo == null && !window.VideoExportButton.IsEnabled,
                    "Exiting Screen Loop restores the normal still workspace");
                var imported = NtscrtImportedLook.Parse(ImportedLookSmoke.Minimal)!;
                window.ApplyImportedLook(imported, ImportedLookSmoke.Minimal, "Test look.json");
                check(!window.EffectControls.IsEnabled && !window.ScreenLoopPanel.IsEnabled && window.VideoExportButton.IsEnabled
                    && window.TimelineInfo is { IsStill: true, FrameCount: 6 } && window.Settings().ImportedLook == imported,
                    "Imported looks are active in the normal window and expose the still animation timeline");
                window.ApplyImportedLook(null);
                check(window.EffectControls.IsEnabled && window.ScreenLoopPanel.IsEnabled && window.Settings().ImportedLook == null
                    && window.Settings().NtscJson == original.NtscJson && window.Settings().Output == original.Output,
                    "Exiting an imported look returns to the previous effect and output controls");

                foreach (var id in new[] { "webcam", "minidv", "cd_rom" })
                {
                    var format = DigitalCatalog.Find(id);
                    window.DigitalFormatChoice.SelectedItem = window.DigitalFormatChoice.Items.OfType<ComboBoxItem>()
                        .Single(item => Equals(item.Tag, id));
                    var selected = window.Settings();
                    check(selected.Renderer == DigitalCatalog.Profile && selected.RetroWidth == format.Width
                        && selected.Digital != null && selected.Digital.OrderBy(p => p.Key).SequenceEqual(format.Values.OrderBy(p => p.Key)),
                        $"{id}: format selection activates digital video and applies the recommended internal width");
                    check(selected.Ntsc == original.Ntsc && selected.NtscJson == original.NtscJson && selected.Crt == original.Crt
                        && selected.Shader == original.Shader && selected.ShaderValues.SequenceEqual(original.ShaderValues)
                        && selected.Output == original.Output && selected.Scale == original.Scale,
                        $"{id}: format selection preserves NTSC, CRT, scale and custom export geometry");
                }
                var digitalFormat = window.DigitalFormatChoice.SelectedItem;
                window.DigitalEnabled.IsChecked = false;
                window.DigitalEnabled.IsChecked = true;
                check(Equals(window.DigitalFormatChoice.SelectedItem, digitalFormat) && window.DigitalControls.IsEnabled,
                    "The digital effect switch preserves the selected format");
                window.ScreenLoopEnabled.IsChecked = true;
                window.ScreenLoopEnabled.IsChecked = false;
                check(window.DigitalEnabled.IsChecked == true && Equals(window.DigitalFormatChoice.SelectedItem, digitalFormat),
                    "Leaving Screen Loop resumes the previously enabled digital effect");

                window.ScreenLoopEnabled.IsChecked = true;
                window.ApplyLookPreset(new("tape-and-tube", 1, true, true, "crtsim", 320, 3,
                    NtscProcessor.Defaults(), [], DownscaleMethod: "lanczos"));
                check(window.NtscControls.IsEnabled && window.ShaderChoice.IsEnabled && window.ScaleChoice.IsEnabled
                    && !window.InScreenLoop && window.DigitalEnabled.IsChecked == false && window.TimelineInfo == null,
                    "Applying a normal saved preset restores its explicit switches and exits Screen Loop");
                check(window.DownscaleMethodChoice.IsEnabled && window.Settings().DownscaleMethod == "lanczos"
                    && window.Settings().Renderer == DigitalCatalog.Profile, "A preset restores its explicit downscale method in the window");
                window.DownscaleMethodChoice.SelectedIndex = 0;
                check(window.Settings().DownscaleMethod == null && window.Settings().Renderer == "legacy-v1",
                    "Automatic downscale restores the look's previous renderer");
                window.DownscaleEnabledChoice.SelectedIndex = 2;
                check(window.Settings() is { Crt: true, Ntsc: true, DownscaleEnabled: false }
                    && !window.WidthChoice.IsEnabled && !window.DownscaleMethodChoice.IsEnabled && !window.ScaleChoice.IsEnabled,
                    "Switching off internal downscale keeps CRT and NTSC on and disables only the scaling controls");
                window.DownscaleEnabledChoice.SelectedIndex = 0;

                window.CrtEnabled.IsChecked = false;
                window.ReceptionEnabled.IsChecked = true;
                window.AnimationEnabled.IsChecked = true;
                var args = new RoutedEventArgs();
                window.AnimationFpsChoice.SelectedItem = 24;
                check(window.TimelineInfo is { FrameCount: 120, IsStill: true, FramesPerSecond: 24 } &&
                    window.VideoExportButton.IsEnabled && window.Settings().EffectTimeline == null,
                    "A still can be exported at the selected playback rate before the first key");
                window.AnimationFpsChoice.SelectedItem = 30;
                window.CaptureKeyframe(window, args);
                window.JumpToFrame(149);
                window._analogValues["signal_strength"] = 0.25;
                window.CaptureKeyframe(window, args);
                check(window._effectTimeline.Keys.Select(key => key.T).SequenceEqual(new[] { 0d, 1d }) &&
                    window.VideoExportButton.IsEnabled && window.Settings().EffectTimeline != null,
                    "Capturing current values adds both ends of a still animation");
                window.EditKeyframe(window, args);
                check(window.KeyframeAudition.IsChecked == true && window.Settings().EffectTimeline == null
                    && window.Settings(preview: false).EffectTimeline != null && window._analogValues["signal_strength"] == 0.25,
                    "Editing loads a key's settings and affects audition without replacing the export animation");
                window.KeyframeEasing.SelectedIndex = 4;
                window.JumpToFrame(75);
                window.MoveKeyframe(window, args);
                check(window.SelectedKeyframe is { T: > 0.5 and < 0.51, Easing: "Hold" },
                    "A selected key can be moved and its easing can be changed");
                window.DeleteKeyframe(window, args);
                check(window._effectTimeline.Keys is [{ T: 0 }], "Removing a key keeps the other key");
                window.ScreenLoopEnabled.IsChecked = true;
                check(window.Settings().EffectTimeline == null && window.KeyframeEditor.Visibility == Visibility.Collapsed,
                    "Screen Loop temporarily takes over the animation mode");
                window.ScreenLoopEnabled.IsChecked = false;
                window.SetAudition(false);
                check(window.Settings().EffectTimeline != null && window._effectTimeline.Keys.Length == 1,
                    "Leaving Screen Loop returns to the saved animation");
            }
            finally { window.Close(); }
        }
        finally { app.ShutdownMode = shutdownMode; }
    }
}
