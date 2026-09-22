using System.Globalization;

namespace Core.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? value)
        => string.IsNullOrEmpty(value);

    // TODO move to utils/helpers project in future
    // requires the ':' form, because TimeSpan.Parse reads a bare number as days
    public static bool IsTimeSpan(this string? value)
        => value is not null && value.Contains(':') && TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out _);
}