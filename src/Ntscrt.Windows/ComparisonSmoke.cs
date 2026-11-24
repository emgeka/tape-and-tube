// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;

namespace Ntscrt.Windows;

internal static class ComparisonSmoke
{
    public static IEnumerable<string> Run()
    {
        var checks = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        var same = ComparisonLayout.Calculate(800, 600, 320, 240, 960, 720, OutputFit.Fit, .5);
        Check(same.Viewport == new Rect(0, 0, 800, 600) && same.Original == same.Viewport, "Vergleich richtet Original und Ausgabe bei gleichem Seitenverhältnis aus");
        var fit = ComparisonLayout.Calculate(800, 600, 400, 300, 1600, 900, OutputFit.Fit, .5);
        Check(fit.Viewport == new Rect(0, 75, 800, 450), "Vergleich berücksichtigt Vorschau-Letterboxing");
        Check(fit.Original == new Rect(100, 75, 600, 450), "Original erhält dieselben schwarzen Ränder wie die Ausgabe");
        var crop = ComparisonLayout.Calculate(800, 600, 400, 300, 1600, 900, OutputFit.Crop, .5);
        Check(crop.Original == new Rect(0, 0, 800, 600), "Original verwendet denselben mittigen Beschnitt wie die Ausgabe");
        var portrait = ComparisonLayout.Calculate(800, 600, 300, 600, 1920, 1080, OutputFit.Fit, .5);
        Check(portrait.Original == new Rect(287.5, 75, 225, 450), "Hochkant-Original bleibt proportional und zentriert");
        var wide = ComparisonLayout.Calculate(1000, 400, 400, 300, 800, 600, OutputFit.Fit, .25);
        Check(Math.Abs((wide.Divider - wide.Viewport.Left) / wide.Viewport.Width - .25) < .000001, "Vergleichsposition bleibt beim Ändern der Fenstergröße relativ zum Bild");
        var left = ComparisonLayout.Calculate(1000, 400, 400, 300, 800, 600, OutputFit.Fit, 0);
        var right = ComparisonLayout.Calculate(1000, 400, 400, 300, 800, 600, OutputFit.Fit, 1);
        Check(left.Divider == left.Viewport.Left && right.Divider == right.Viewport.Right, "Trenner-Endpunkte zeigen vollständig Ausgabe oder Original");
        Check(ComparisonLayout.Calculate(1000, 400, 400, 300, 800, 600, OutputFit.Fit, -1).Divider == left.Divider
            && ComparisonLayout.Calculate(1000, 400, 400, 300, 800, 600, OutputFit.Fit, 2).Divider == right.Divider, "Trenner bleibt innerhalb des Bildes");
        Check(ComparisonLayout.Calculate(0, 0, 320, 240, 960, 720, OutputFit.Fit, .5).Viewport.Width == 0
            && ComparisonLayout.Calculate(double.NaN, 600, 320, 240, 960, 720, OutputFit.Fit, .5).Viewport.Width == 0, "Nicht sichtbare Vorschau erzeugt keine ungültige Geometrie");
        var odd = ComparisonLayout.Calculate(321, 181, 400, 300, 321, 181, OutputFit.Fit, .5);
        var geometry = new OutputSettings(321, 181).Geometry(400, 300);
        Check(odd.Original.Left == geometry.Left && odd.Original.Top == geometry.Top && odd.Original.Width == geometry.ContentWidth,
            "Vergleich übernimmt dieselbe Pixelrundung bei ungeraden Ausgabemaßen");
        var zoomed = ComparisonLayout.Calculate(800, 600, 400, 300, 1600, 900, OutputFit.Fit, .5,
            zoom: 2, pan: new Vector(100, -50));
        Check(zoomed.Viewport == new Rect(-300, -200, 1600, 900) && zoomed.Original == new Rect(-100, -200, 1200, 900)
            && zoomed.Divider == 500, "Zoom und Verschieben halten Original, Ausgabe und Trenner zusammen");
        var size = new Size(800, 600);
        var clamped = ComparisonLayout.ImageBounds(size, 300, 600, 2, new Vector(2000, -2000));
        Check(clamped == new Rect(100, -600, 600, 1200), "Verschieben bleibt an den Bildrändern begrenzt");
        var anchor = new Point(600, 400);
        var anchorPan = ComparisonLayout.PanAtZoomAnchor(size, same.Viewport, anchor, 2);
        var anchored = ComparisonLayout.ImageBounds(size, 960, 720, 2, anchorPan);
        Check(anchored.Left + 600 * 2 == anchor.X && anchored.Top + 400 * 2 == anchor.Y,
            "Beim Zoomen bleibt die Stelle unter dem Mauszeiger stehen");
        var pixelGrid = new PreviewGrid(1, 0, new DpiScale(1, 1));
        var integerFit = ComparisonLayout.ImageBounds(size, 320, 240, 1, default, pixelGrid);
        Check(integerFit == new Rect(80, 60, 640, 480), "Ganzzahlskalierung passt das größte ganze Vielfache ein");
        var tooSmall = ComparisonLayout.ImageBounds(size, 1920, 1080, 1, default, pixelGrid);
        Check(tooSmall == ComparisonLayout.ImageBounds(size, 1920, 1080, 1, default),
            "Eine größere Quelle bleibt beim Einpassen vollständig sichtbar");
        var highDpi = new PreviewGrid(2, 120, new DpiScale(1.25, 1.25));
        var letterbox = new OutputGeometry(960, 720, 960, 480, 0, 120);
        var integerCompare = ComparisonLayout.Calculate(800, 600, 480, 240, 960, 720, OutputFit.Fit, .5, letterbox, grid: highDpi);
        Check(Math.Abs(integerCompare.Original.Height * 1.25 / 240 - 2) < 1e-6
            && Math.Abs(integerCompare.Original.Top * 1.25 - 135) < 1e-6 && integerCompare.Divider == 400,
            "Der Vergleich richtet sichtbare Scanlines am physischen Raster eines 125-Prozent-Displays aus");
        var integerPan = ComparisonLayout.ImageBounds(size, 960, 720, 2, new Vector(3000, 3000), highDpi);
        Check(integerPan.Left <= 0 && integerPan.Top <= 0 && integerPan.Right >= size.Width && integerPan.Bottom >= size.Height,
            "Verschieben bei Ganzzahlskalierung bleibt an den Bildrändern begrenzt");
        return checks;
    }
}
