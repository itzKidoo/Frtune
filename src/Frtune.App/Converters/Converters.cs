using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Frtune.App.Converters;

/// <summary>true → Visible. Pass ConverterParameter="Invert" to flip.</summary>
public sealed class BoolToVisibility : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var b = value is true;
        if (p as string == "Invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>null, empty string or 0 → Collapsed.</summary>
public sealed class EmptyToCollapsed : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var empty = value is null || value is string { Length: 0 } || value is 0 || value is System.Collections.ICollection { Count: 0 };
        if (p as string == "Invert") empty = !empty;
        return empty ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class InverseBool : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is not true;
}

/// <summary>Compares an enum/value with the parameter (for preset chips bound to a single property).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => Equals(value?.ToString(), p?.ToString());
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Hex string → SolidColorBrush (accent swatches).</summary>
public sealed class HexToBrush : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        try { return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString((string)value)); }
        catch { return System.Windows.Media.Brushes.Transparent; }
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>True when all bound values are equal (used to highlight the active segment).</summary>
public sealed class AreEqual : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object p, CultureInfo c) => values.Length == 2 && Equals(values[0], values[1]);
    public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Short labels for service start types.</summary>
public sealed class StartTypeLabel : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value?.ToString() switch
    {
        "Automatic" => "Auto",
        "AutomaticDelayed" => "Delayed",
        "Manual" => "Manual",
        "Disabled" => "Off",
        var s => s ?? "",
    };
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
