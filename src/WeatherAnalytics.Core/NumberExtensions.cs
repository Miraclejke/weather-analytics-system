namespace WeatherAnalytics.Core;

internal static class NumberExtensions
{
    public static double? AverageOrNull(this IEnumerable<double?> values)
    {
        var actualValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        return actualValues.Count == 0 ? null : actualValues.Average();
    }

    public static double? MinOrNull(this IEnumerable<double?> values)
    {
        var actualValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        return actualValues.Count == 0 ? null : actualValues.Min();
    }

    public static double? MaxOrNull(this IEnumerable<double?> values)
    {
        var actualValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        return actualValues.Count == 0 ? null : actualValues.Max();
    }

    public static double? SumOrNull(this IEnumerable<double?> values)
    {
        var actualValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        return actualValues.Count == 0 ? null : actualValues.Sum();
    }
}
