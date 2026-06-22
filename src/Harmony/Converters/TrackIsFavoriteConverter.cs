using System.Globalization;
using System.Windows.Data;
using Harmony.Models;
using Harmony.Services;

namespace Harmony.Converters;

/// <summary>true when track is in FavoriteLookup (second binding value forces refresh).</summary>
public sealed class TrackIsFavoriteConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length > 0 && values[0] is Track track)
            return FavoriteLookup.Instance.IsFavorite(track);
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
