using System.Globalization;

namespace CamperoDesktop.Helpers;

public static class DecimalParser
{
    public static bool TryParse(string? value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(value, out result);
    }

    public static bool TryParsePositive(string? value, out decimal result)
    {
        if (TryParse(value, out var parsed) && parsed >= 0)
        {
            result = parsed;
            return true;
        }
        result = 0;
        return false;
    }

    public static decimal ParseOrDefault(string? value, decimal defaultValue = 0)
    {
        return TryParse(value, out var result) ? result : defaultValue;
    }

    public static decimal ParsePositiveOrDefault(string? value, decimal defaultValue = 0)
    {
        return TryParsePositive(value, out var result) ? result : defaultValue;
    }
}
