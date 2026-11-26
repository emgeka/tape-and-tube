// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ntscrt.Windows;

internal readonly record struct ScreenLoopPoints(Point Start, Point End)
{
    public static Point OnScreen(Point point, Rect image) =>
        new(image.Left + (point.X + 0.5) * image.Width, image.Top + (point.Y + 0.5) * image.Height);

    public static Point FromScreen(Point point, Rect image) =>
        new((point.X - image.Left) / image.Width - 0.5, (point.Y - image.Top) / image.Height - 0.5);

    public ScreenLoopPoints Move(bool endHandle, Point point)
    {
        point = new(Math.Clamp(point.X, -0.5, 0.5), Math.Clamp(point.Y, -0.5, 0.5));
        return endHandle ? this with { End = point } : new(point, Start == End ? point : End);
    }
}

public partial class MainWindow
{
    private Rect _screenLoopPadBounds;
    private Point _screenLoopDot, _screenLoopRing;
    private (ScreenLoopPoints Points, Point Pointer, Point Handle)? _screenLoopDrag;

    private ScreenLoopPoints ReadScreenLoopPoints()
    {
        var start = new Point(_screenLoopValues["center_x"], _screenLoopValues["center_y"]);
        return new(start, start + new Vector(_screenLoopValues["drift_x"], _screenLoopValues["drift_y"]));
    }

    private void UpdateScreenLoopPad()
    {
        if (!_ready) return;
        if (!InScreenLoop || _output == null || ShowOriginal.IsChecked == true || SplitComparison.IsChecked == true || SideBySideActive)
        {
            ScreenLoopPadLayer.Visibility = Visibility.Collapsed;
            _screenLoopDrag = null;
            return;
        }

        var x = Canvas.GetLeft(Preview);
        var y = Canvas.GetTop(Preview);
        var width = Preview.Width;
        var height = Preview.Height;
        if (_previewLayout is { } layout)
        {
            x += layout.Left * width / layout.Width;
            y += layout.Top * height / layout.Height;
            width *= layout.ContentWidth / (double)layout.Width;
            height *= layout.ContentHeight / (double)layout.Height;
        }
        _screenLoopPadBounds = new(x, y, width, height);
        var visible = Rect.Intersect(_screenLoopPadBounds, new Rect(PreviewSize));
        if (visible.IsEmpty || visible.Width <= 0 || visible.Height <= 0)
        {
            ScreenLoopPadLayer.Visibility = Visibility.Collapsed;
            return;
        }

        Point Show(Point point)
        {
            var target = ScreenLoopPoints.OnScreen(point, _screenLoopPadBounds);
            // Keep the handles reachable beside image edges, including with fill/crop and zoom.
            var insetX = Math.Min(12, visible.Width * 0.45);
            var insetY = Math.Min(12, visible.Height * 0.45);
            return new(Math.Clamp(target.X, visible.Left + insetX, visible.Right - insetX),
                Math.Clamp(target.Y, visible.Top + insetY, visible.Bottom - insetY));
        }

        var points = ReadScreenLoopPoints();
        _screenLoopDot = Show(points.Start);
        _screenLoopRing = Show(points.End);
        Place(ScreenLoopStartHandle, _screenLoopDot);
        Place(ScreenLoopEndHandle, _screenLoopRing);
        ScreenLoopPadLine.X1 = _screenLoopDot.X;
        ScreenLoopPadLine.Y1 = _screenLoopDot.Y;
        ScreenLoopPadLine.X2 = _screenLoopRing.X;
        ScreenLoopPadLine.Y2 = _screenLoopRing.Y;
        ScreenLoopPadLayer.Visibility = Visibility.Visible;
    }

    private static void Place(Thumb thumb, Point point)
    {
        Canvas.SetLeft(thumb, point.X - thumb.Width / 2);
        Canvas.SetTop(thumb, point.Y - thumb.Height / 2);
    }

    private void ApplyScreenLoopPoints(ScreenLoopPoints points)
    {
        var drift = points.End - points.Start;
        var values = new (string Key, double Value)[]
        {
            ("center_x", points.Start.X), ("center_y", points.Start.Y),
            ("drift_x", Math.Clamp(drift.X, -1, 1)), ("drift_y", Math.Clamp(drift.Y, -1, 1))
        };
        if (values.All(pair => _screenLoopValues[pair.Key] == pair.Value)) return;
        if (_video == null) PauseVideo();
        _updatingScreenLoop = true;
        try
        {
            foreach (var (key, value) in values)
            {
                _screenLoopValues[key] = value;
                _screenLoopPointSliders[key].Value = value * 100;
            }
        }
        finally { _updatingScreenLoop = false; }
        ScreenLoopEdited();
    }

    private void ScreenLoopHandleStarted(object sender, DragStartedEventArgs e) =>
        _screenLoopDrag = (ReadScreenLoopPoints(), Mouse.GetPosition(PreviewHost),
            ReferenceEquals(sender, ScreenLoopEndHandle) ? _screenLoopRing : _screenLoopDot);

    private void ScreenLoopHandleDragged(object sender, DragDeltaEventArgs e)
    {
        if (_screenLoopDrag is not { } drag || ScreenLoopPadLayer.Visibility != Visibility.Visible) return;
        var end = ReferenceEquals(sender, ScreenLoopEndHandle);
        var moved = drag.Handle + (Mouse.GetPosition(PreviewHost) - drag.Pointer);
        if (end && (moved - _screenLoopDot).Length <= 9)
            ApplyScreenLoopPoints(drag.Points with { End = drag.Points.Start });
        else ApplyScreenLoopPoints(drag.Points.Move(end, ScreenLoopPoints.FromScreen(moved, _screenLoopPadBounds)));
    }

    private void ScreenLoopHandleCompleted(object sender, DragCompletedEventArgs e) => _screenLoopDrag = null;

    private void ScreenLoopHandlePressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        ResetScreenLoopHandle(ReferenceEquals(sender, ScreenLoopEndHandle));
        e.Handled = true;
    }

    private void ScreenLoopHandleKey(object sender, KeyEventArgs e)
    {
        var end = ReferenceEquals(sender, ScreenLoopEndHandle);
        if (e.Key == Key.Home) ResetScreenLoopHandle(end);
        else
        {
            var distance = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.1 : 0.01;
            var offset = e.Key switch
            {
                Key.Left => new Vector(-distance, 0), Key.Right => new Vector(distance, 0),
                Key.Up => new Vector(0, -distance), Key.Down => new Vector(0, distance),
                _ => default
            };
            if (offset == default) return;
            var points = ReadScreenLoopPoints();
            ApplyScreenLoopPoints(points.Move(end, (end ? points.End : points.Start) + offset));
        }
        e.Handled = true;
    }

    private void ResetScreenLoopHandle(bool end)
    {
        var points = ReadScreenLoopPoints();
        ApplyScreenLoopPoints(end ? points with { End = points.Start } : points.Move(false, new Point()));
    }
}
