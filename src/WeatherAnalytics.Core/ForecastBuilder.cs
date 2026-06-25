namespace WeatherAnalytics.Core;

public class ForecastBuilder
{
    public IReadOnlyList<ForecastDay> BuildForecast(IReadOnlyList<WeatherDay> weatherDays, List<string>? notes = null)
    {
        var result = new List<ForecastDay>();

        foreach (var group in weatherDays.GroupBy(day => new { day.LocationId, day.City }))
        {
            var days = group
                .Where(day => day.AvgTempC.HasValue)
                .OrderBy(day => day.Date)
                .ToList();

            if (days.Count < WeatherRules.ForecastMinHistoryDays)
            {
                notes?.Add($"Для города {group.Key.City} меньше {WeatherRules.ForecastMinHistoryDays} дней данных, прогноз на {WeatherRules.ForecastDays} дней не строился.");
                continue;
            }

            var lastDate = days[^1].Date;
            var last7 = days.TakeLast(WeatherRules.ForecastAverageDays).ToList();
            var last14 = days.TakeLast(WeatherRules.ForecastRangeDays).ToList();
            var last30 = days.TakeLast(WeatherRules.ForecastMinHistoryDays).ToList();

            var avg7 = last7.Average(day => day.AvgTempC!.Value);
            var trendPerDay = LinearSlope(last30);
            var minGap = last14
                .Where(day => day.MinTempC.HasValue)
                .Select(day => (double?)(day.AvgTempC!.Value - day.MinTempC!.Value))
                .AverageOrNull() ?? WeatherRules.DefaultTemperatureGapC;
            var maxGap = last14
                .Where(day => day.MaxTempC.HasValue)
                .Select(day => (double?)(day.MaxTempC!.Value - day.AvgTempC!.Value))
                .AverageOrNull() ?? WeatherRules.DefaultTemperatureGapC;
            var rainDays = last14.Where(day => day.RainMm.HasValue).ToList();
            double? rainChance = null;
            if (rainDays.Count == 0)
            {
                notes?.Add($"Для города {group.Key.City} нет данных по осадкам за последние {WeatherRules.ForecastRangeDays} дней, вероятность осадков не рассчитывалась.");
            }
            else
            {
                rainChance = rainDays.Count(day => day.RainMm!.Value >= WeatherRules.RainyDayMm) / (double)rainDays.Count;
            }

            for (var dayNumber = 1; dayNumber <= WeatherRules.ForecastDays; dayNumber++)
            {
                var avgTemp = avg7 + trendPerDay * dayNumber;
                result.Add(new ForecastDay(
                    group.Key.LocationId,
                    group.Key.City,
                    lastDate.AddDays(dayNumber),
                    Round(avgTemp),
                    Round(avgTemp - minGap),
                    Round(avgTemp + maxGap),
                    Round(rainChance)));
            }
        }

        return result;
    }

    private static double LinearSlope(IReadOnlyList<WeatherDay> days)
    {
        var n = days.Count;
        var avgX = (n + 1) / 2.0;
        var avgY = days.Average(day => day.AvgTempC!.Value);
        var numerator = 0.0;
        var denominator = 0.0;

        for (var i = 0; i < n; i++)
        {
            var x = i + 1;
            var y = days[i].AvgTempC!.Value;
            numerator += (x - avgX) * (y - avgY);
            denominator += Math.Pow(x - avgX, 2);
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static double Round(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static double? Round(double? value)
    {
        return value.HasValue ? Round(value.Value) : null;
    }
}
