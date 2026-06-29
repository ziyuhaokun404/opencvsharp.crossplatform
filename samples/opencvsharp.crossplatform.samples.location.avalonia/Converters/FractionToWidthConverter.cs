using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Converters;

/// <summary>
/// Converts a fraction (0.0–1.0) to a pixel width for proportional bar display.
/// The bar track is approximately 160px wide in the 280px sidebar.
/// </summary>
public sealed class FractionToWidthConverter : IValueConverter
{
    private const double MaxBarWidth = 160.0;

    public static FractionToWidthConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double fraction)
            return Math.Max(1, fraction * MaxBarWidth);
        return 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
