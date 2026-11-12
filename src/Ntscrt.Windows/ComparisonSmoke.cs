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
        return checks;
    }
}
