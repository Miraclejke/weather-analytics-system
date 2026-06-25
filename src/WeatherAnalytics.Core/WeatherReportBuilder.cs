namespace WeatherAnalytics.Core;

public class WeatherReportBuilder
{
    private readonly AnomalyFinder anomalyFinder = new();
    private readonly ForecastBuilder forecastBuilder = new();

    public WeatherReportData BuildReport(IReadOnlyList<WeatherDay> weatherDays, ReportRequest request)
    {
        var orderedDays = weatherDays
            .OrderBy(day => day.City)
            .ThenBy(day => day.Date)
            .ToList();

        var notes = new List<string>();
        if (orderedDays.Count == 0)
        {
            notes.Add("За выбранный период нет дневных погодных наблюдений.");
        }

        var temperatureSummaries = BuildTemperatureSummaries(orderedDays, notes);
        var monthlyStats = BuildMonthlyStats(orderedDays);
        var seasonalStats = BuildSeasonalStats(orderedDays);
        var rainSummaries = BuildRainSummaries(orderedDays, request.RainThresholdMm, notes);
        var cityComparisons = BuildCityComparisons(orderedDays);
        var yearlyStats = BuildYearlyStats(orderedDays);
        var longTermTrends = BuildLongTermTrends(yearlyStats, orderedDays, request, notes);
        var coldestDays = BuildExtremeDays(orderedDays, "Самый холодный день", day => day.MinTempC, ascending: true);
        var warmestDays = BuildExtremeDays(orderedDays, "Самый теплый день", day => day.MaxTempC, ascending: false);
        var rainiestDays = BuildExtremeDays(orderedDays, "Самый дождливый день", day => day.RainMm, ascending: false);
        var anomalies = anomalyFinder.Find(orderedDays);
        var forecasts = forecastBuilder.BuildForecast(orderedDays, notes);

        return new WeatherReportData(
            request,
            orderedDays,
            temperatureSummaries,
            monthlyStats,
            seasonalStats,
            rainSummaries,
            cityComparisons,
            yearlyStats,
            longTermTrends,
            coldestDays,
            warmestDays,
            rainiestDays,
            anomalies,
            forecasts,
            notes);
    }

    private static IReadOnlyList<TemperatureSummary> BuildTemperatureSummaries(
        IReadOnlyList<WeatherDay> weatherDays,
        List<string> notes)
    {
        var result = new List<TemperatureSummary>();

        foreach (var group in weatherDays.GroupBy(day => new { day.LocationId, day.City }))
        {
            var days = group.ToList();
            var avgValues = days.Where(day => day.AvgTempC.HasValue).ToList();
            var minValues = days.Where(day => day.MinTempC.HasValue).ToList();
            var maxValues = days.Where(day => day.MaxTempC.HasValue).ToList();

            if (avgValues.Count == 0)
            {
                notes.Add($"Для города {group.Key.City} нет средней температуры за выбранный период.");
            }

            var coldest = minValues.MinBy(day => day.MinTempC);
            var warmest = maxValues.MaxBy(day => day.MaxTempC);

            result.Add(new TemperatureSummary(
                group.Key.LocationId,
                group.Key.City,
                Round(avgValues.Select(day => day.AvgTempC).AverageOrNull()),
                Round(minValues.Select(day => day.MinTempC).MinOrNull()),
                Round(maxValues.Select(day => day.MaxTempC).MaxOrNull()),
                coldest?.Date,
                warmest?.Date,
                days.Count));
        }

        return result;
    }

    private static IReadOnlyList<MonthlyWeatherStat> BuildMonthlyStats(IReadOnlyList<WeatherDay> weatherDays)
    {
        var result = new List<MonthlyWeatherStat>();

        foreach (var group in weatherDays.GroupBy(day => new { day.LocationId, day.City, day.Date.Year, day.Date.Month }))
        {
            var days = group.ToList();
            result.Add(new MonthlyWeatherStat(
                group.Key.LocationId,
                group.Key.City,
                group.Key.Year,
                group.Key.Month,
                Round(days.Select(day => day.AvgTempC).AverageOrNull()),
                Round(days.Select(day => day.MinTempC).MinOrNull()),
                Round(days.Select(day => day.MaxTempC).MaxOrNull()),
                Round(days.Select(day => day.RainMm).SumOrNull()),
                CountRainyDays(days)));
        }

        return result
            .OrderBy(stat => stat.City)
            .ThenBy(stat => stat.Year)
            .ThenBy(stat => stat.Month)
            .ToList();
    }

    private static IReadOnlyList<SeasonalWeatherStat> BuildSeasonalStats(IReadOnlyList<WeatherDay> weatherDays)
    {
        var result = new List<SeasonalWeatherStat>();

        foreach (var group in weatherDays.GroupBy(day => new
                 {
                     day.LocationId,
                     day.City,
                     SeasonYear = GetSeasonYear(day.Date),
                     Season = GetSeasonName(day.Date.Month)
                 }))
        {
            var days = group.ToList();
            result.Add(new SeasonalWeatherStat(
                group.Key.LocationId,
                group.Key.City,
                group.Key.SeasonYear,
                group.Key.Season,
                Round(days.Select(day => day.AvgTempC).AverageOrNull()),
                Round(days.Select(day => day.MinTempC).MinOrNull()),
                Round(days.Select(day => day.MaxTempC).MaxOrNull())));
        }

        return result
            .OrderBy(stat => stat.City)
            .ThenBy(stat => stat.Year)
            .ThenBy(stat => SeasonOrder(stat.Season))
            .ToList();
    }

    private static IReadOnlyList<RainSummary> BuildRainSummaries(
        IReadOnlyList<WeatherDay> weatherDays,
        double rainThresholdMm,
        List<string> notes)
    {
        var result = new List<RainSummary>();

        foreach (var group in weatherDays.GroupBy(day => new { day.LocationId, day.City }))
        {
            var days = group.ToList();
            var rainValues = days.Where(day => day.RainMm.HasValue).ToList();
            var wettest = days
                .Where(day => day.RainMm.HasValue)
                .MaxBy(day => day.RainMm);

            if (rainValues.Count == 0)
            {
                notes.Add($"Для города {group.Key.City} нет данных по осадкам за выбранный период.");
            }

            var totalRain = days.Select(day => day.RainMm).SumOrNull();
            result.Add(new RainSummary(
                group.Key.LocationId,
                group.Key.City,
                Round(totalRain),
                rainValues.Count == 0 ? null : Round(totalRain / rainValues.Count),
                CountRainyDays(days),
                days.Count(day => day.RainMm.HasValue && day.RainMm.Value >= rainThresholdMm),
                wettest?.Date,
                Round(wettest?.RainMm)));
        }

        return result;
    }

    private static IReadOnlyList<CityComparison> BuildCityComparisons(IReadOnlyList<WeatherDay> weatherDays)
    {
        var result = new List<CityComparison>();

        foreach (var group in weatherDays.GroupBy(day => new { day.LocationId, day.City }))
        {
            var days = group.ToList();
            result.Add(new CityComparison(
                group.Key.LocationId,
                group.Key.City,
                Round(days.Select(day => day.AvgTempC).AverageOrNull()),
                Round(days.Select(day => day.MinTempC).MinOrNull()),
                Round(days.Select(day => day.MaxTempC).MaxOrNull()),
                Round(days.Select(day => day.RainMm).SumOrNull()),
                CountRainyDays(days),
                Round(days.Select(day => day.AvgWindMs).AverageOrNull()),
                Round(days.Select(day => day.AvgClouds).AverageOrNull())));
        }

        return result.OrderBy(item => item.City).ToList();
    }

    private static IReadOnlyList<YearlyWeatherStat> BuildYearlyStats(IReadOnlyList<WeatherDay> weatherDays)
    {
        var result = new List<YearlyWeatherStat>();

        foreach (var group in weatherDays.GroupBy(day => new { day.LocationId, day.City, day.Date.Year }))
        {
            var days = group.ToList();
            result.Add(new YearlyWeatherStat(
                group.Key.LocationId,
                group.Key.City,
                group.Key.Year,
                Round(days.Select(day => day.AvgTempC).AverageOrNull()),
                Round(days.Select(day => day.RainMm).SumOrNull())));
        }

        return result
            .OrderBy(stat => stat.City)
            .ThenBy(stat => stat.Year)
            .ToList();
    }

    private static IReadOnlyList<LongTermTrend> BuildLongTermTrends(
        IReadOnlyList<YearlyWeatherStat> yearlyStats,
        IReadOnlyList<WeatherDay> weatherDays,
        ReportRequest request,
        List<string> notes)
    {
        var result = new List<LongTermTrend>();
        var periodDays = request.To.DayNumber - request.From.DayNumber + 1;

        foreach (var city in weatherDays.GroupBy(day => new { day.LocationId, day.City }))
        {
            var stats = yearlyStats
                .Where(stat => stat.LocationId == city.Key.LocationId)
                .OrderBy(stat => stat.Year)
                .ToList();

            if (periodDays < 730 || stats.Count < 2 || stats.Count(stat => stat.AvgTempC.HasValue) < 2)
            {
                notes.Add($"Для города {city.Key.City} меньше двух лет данных, долгосрочный тренд не рассчитывался.");
                result.Add(new LongTermTrend(city.Key.LocationId, city.Key.City, null, null, null, "Недостаточно данных"));
                continue;
            }

            var first = stats.First();
            var last = stats.Last();
            var trend = LinearSlope(
                stats.Where(stat => stat.AvgTempC.HasValue).Select(stat => ((double)stat.Year, stat.AvgTempC!.Value)).ToList());

            result.Add(new LongTermTrend(
                city.Key.LocationId,
                city.Key.City,
                Round(last.AvgTempC - first.AvgTempC),
                Round(last.RainMm - first.RainMm),
                Round(trend),
                BuildTrendNote(trend)));
        }

        return result;
    }

    private static int CountRainyDays(IEnumerable<WeatherDay> days)
    {
        return days.Count(day => day.RainMm.HasValue && day.RainMm.Value >= WeatherRules.RainyDayMm);
    }

    private static IReadOnlyList<ExtremeDay> BuildExtremeDays(
        IReadOnlyList<WeatherDay> weatherDays,
        string type,
        Func<WeatherDay, double?> value,
        bool ascending)
    {
        var result = new List<ExtremeDay>();

        foreach (var group in weatherDays.GroupBy(day => new { day.LocationId, day.City }))
        {
            var days = group
                .Where(day => value(day).HasValue)
                .OrderBy(day => ascending ? value(day) : -value(day))
                .Take(5);

            foreach (var day in days)
            {
                result.Add(new ExtremeDay(group.Key.LocationId, group.Key.City, day.Date, type, Round(value(day))));
            }
        }

        return result;
    }

    private static string GetSeasonName(int month)
    {
        return month switch
        {
            12 or 1 or 2 => "Зима",
            >= 3 and <= 5 => "Весна",
            >= 6 and <= 8 => "Лето",
            _ => "Осень"
        };
    }

    private static int GetSeasonYear(DateOnly date)
    {
        return date.Month == 12 ? date.Year + 1 : date.Year;
    }

    private static int SeasonOrder(string season)
    {
        return season switch
        {
            "Зима" => 1,
            "Весна" => 2,
            "Лето" => 3,
            _ => 4
        };
    }

    private static string BuildTrendNote(double? trend)
    {
        if (!trend.HasValue)
        {
            return "Тренд не рассчитан";
        }

        if (Math.Abs(trend.Value) < WeatherRules.NoticeableTrendPerYearC)
        {
            return "Заметного изменения средней температуры по годам нет";
        }

        return trend.Value > 0
            ? "Средняя температура растет"
            : "Средняя температура снижается";
    }

    private static double? LinearSlope(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var avgX = points.Average(point => point.X);
        var avgY = points.Average(point => point.Y);
        var numerator = points.Sum(point => (point.X - avgX) * (point.Y - avgY));
        var denominator = points.Sum(point => Math.Pow(point.X - avgX, 2));

        if (denominator == 0)
        {
            return null;
        }

        return numerator / denominator;
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
