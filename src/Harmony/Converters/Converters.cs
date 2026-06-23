using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Harmony.Models;
using Harmony.Services.Localization;
using Harmony.ViewModels;
namespace Harmony.Converters;

/// <summary>Returns a "pause" glyph when playing, otherwise a "play" glyph.</summary>
public sealed class PlayPauseGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "\uE769" /* pause */ : "\uE768" /* play */;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>null / empty string -> Collapsed, otherwise Visible.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is string s ? !string.IsNullOrWhiteSpace(s) : value != null;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps volume (0–1) to a pixel width for the click bar.</summary>
public sealed class VolumeToWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var vol = value is double d ? d : 0d;
        var maxWidth = 110.0;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out var w))
            maxWidth = w;
        else if (parameter is double wd)
            maxWidth = wd;

        return Math.Max(0, vol) * maxWidth;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true -> Visible, false -> Collapsed. Pass parameter "invert" to flip.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>true when filter chip matches the selected filter id.</summary>
public sealed class FilterChipSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        if (values[0]?.Equals(values[1]) == true) return true;

        var chipId = values[0] as string;
        var selectedId = values[1] switch
        {
            HomeFilterChip f => f.Id,
            HomeTabChip t => t.Id,
            _ => null
        };
        return chipId != null && chipId == selectedId;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Alternation index (0-based) -> 1-based track number.</summary>
public sealed class TrackNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
            return (i + 1).ToString(culture);
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True when two tracks share the same source identity.</summary>
public sealed class IsSameTrackConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        return values[0] is Track a && values[1] is Track b && a.Matches(b);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps AudioQuality enum to localized label; pass Language as second binding value.</summary>
public sealed class AudioQualityLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 1 || values[0] is not AudioQuality quality)
            return values.Length > 0 ? values[0] ?? "" : "";

        var lang = values.Length > 1 ? values[1] as string : null;
        return LocalizationTable.QualityLabel(lang ?? "en", quality.ToString());
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps PlaybackSourceMode enum to localized label; pass Language as second binding value.</summary>
public sealed class PlaybackSourceLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 1 || values[0] is not PlaybackSourceMode mode)
            return values.Length > 0 ? values[0] ?? "" : "";

        var lang = values.Length > 1 ? values[1] as string : null;
        return LocalizationTable.PlaybackSourceLabel(lang ?? "en", mode.ToString());
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Russian track count label: 1 трек, 2 трека, 5 треков.</summary>
public sealed class TrackCountLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            long l => (int)l,
            _ => 0
        };
        return count switch
        {
            1 => "1 трек",
            >= 2 and <= 4 => $"{count} трека",
            _ => $"{count} треков"
        };
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>#AARRGGBB or #RRGGBB hex string to SolidColorBrush.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return System.Windows.Media.Brushes.Transparent;
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
            return new System.Windows.Media.SolidColorBrush(color);
        }
        catch
        {
            return System.Windows.Media.Brushes.Transparent;
        }
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>#AARRGGBB or #RRGGBB hex string to Color (for gradients).</summary>
public sealed class HexToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return System.Windows.Media.Colors.Transparent;
        try
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
        }
        catch
        {
            return System.Windows.Media.Colors.Transparent;
        }
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
