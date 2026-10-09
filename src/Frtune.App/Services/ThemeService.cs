using System.Windows;
using System.Windows.Media;

namespace Frtune.App.Services;

public static class ThemeService
{
    public static readonly (string Name, string Hex)[] Accents =
    {
        ("Violet", "#8B5CF6"), ("Blue", "#3B82F6"), ("Cyan", "#06B6D4"), ("Green", "#22C55E"),
        ("Pink", "#EC4899"), ("Orange", "#F97316"), ("Red", "#EF4444"),
    };

    /// <summary>Swaps the accent resources. Everything bound with DynamicResource updates immediately.</summary>
    public static void ApplyAccent(string hex)
    {
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(hex); }
        catch { c = (Color)ColorConverter.ConvertFromString("#8B5CF6"); }
        var res = Application.Current.Resources;
        res["AccentColor"] = c;
        res["AccentGlowColor"] = c;
        res["AccentBrush"] = Freeze(new SolidColorBrush(c));
        res["AccentSoftBrush"] = Freeze(new SolidColorBrush(Color.FromArgb(0x33, c.R, c.G, c.B)));
    }

    private static SolidColorBrush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
