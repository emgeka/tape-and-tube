// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Threading;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private const int StillMotionFps = 30;
    // The worker requires a fixed source length. This clock is independent of the animation timeline.
    private const int StillMotionFrameLimit = 10_000_000;
    private readonly DispatcherTimer _stillMotionTimer = new(DispatcherPriority.Background)
        { Interval = TimeSpan.FromSeconds(1.0 / StillMotionFps) };
    private int _stillMotionFrame;

    private bool CanPreviewStillMotion => _source != null && _video == null && StillAnimationTimeline() == null;
    private bool StillMotionActive => _stillMotionTimer.IsEnabled && CanPreviewStillMotion;

    private void UpdateStillMotionAvailability()
    {
        var available = CanPreviewStillMotion;
        StillMotionChoice.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        if (available && StillMotionChoice.IsChecked == true) _stillMotionTimer.Start();
        else
        {
            _stillMotionTimer.Stop();
            _stillMotionFrame = 0;
        }
    }

    private void StillMotionChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        UpdateStillMotionAvailability();
        Schedule();
    }

    private async Task TickStillMotionAsync()
    {
        if (!StillMotionActive || _closing || _busy || _debounce.IsEnabled || _loadCancellation != null ||
            _exportCancellation != null || ShowOriginal.IsChecked == true) return;
        if (_importedLook == null && NtscEnabled.IsChecked != true && CrtEnabled.IsChecked != true &&
            DigitalEnabled.IsChecked != true && VcrEnabled.IsChecked != true && ReceptionEnabled.IsChecked != true) return;
        _stillMotionFrame = (_stillMotionFrame + 1) % StillMotionFrameLimit;
        await Render();
    }

    private static VideoInfo StillMotionClock(PixelFrame image) =>
        new("", image.Width, image.Height, StillMotionFps, FormattableString.Invariant($"{StillMotionFps}/1"),
            StillMotionFrameLimit, 0, IsStill: true);
}
