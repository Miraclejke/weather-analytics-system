namespace WeatherAnalytics.Core;

public class AnomalyFinder
{
    public IReadOnlyList<WeatherAnomaly> Find(IReadOnlyList<WeatherDay> weatherDays)
    {
        var anomalies = new List<WeatherAnomaly>();

        foreach (var cityDays in weatherDays.GroupBy(day => new { day.LocationId, day.City }))
        {
            var days = cityDays.OrderBy(day => day.Date).ToList();
            anomalies.AddRange(FindTemperatureAnomalies(days));
            anomalies.AddRange(FindSharpTemperatureChanges(days));
            anomalies.AddRange(FindHeavyRain(days));
            anomalies.AddRange(FindStrongWind(days));
            anomalies.AddRange(FindDryPeriods(days));
        }

        return anomalies
            .OrderBy(item => item.City)
            .ThenBy(item => item.FromDate)
            .ThenBy(item => item.Type)
            .ToList();
    }

    private static IReadOnlyList<WeatherAnomaly> FindTemperatureAnomalies(IReadOnlyList<WeatherDay> days)
    {
        var anomalies = new List<WeatherAnomaly>();

        foreach (var monthGroup in days.Where(day => day.AvgTempC.HasValue).GroupBy(day => day.Date.Month))
        {
            var monthDays = monthGroup.ToList();
            if (monthDays.Count < WeatherRules.MinTemperatureAnomalySamples)
            {
                continue;
            }

            var monthAvg = monthDays.Average(day => day.AvgTempC!.Value);
            var monthStd = StandardDeviation(monthDays.Select(day => day.AvgTempC!.Value));
            if (monthStd <= 0)
            {
                continue;
            }

            foreach (var day in monthDays)
            {
                var z = (day.AvgTempC!.Value - monthAvg) / monthStd;
                if (z >= WeatherRules.TemperatureAnomalyZScore)
                {
                    anomalies.Add(new WeatherAnomaly(
                        day.LocationId,
                        day.City,
                        day.Date,
                        day.Date,
                        "Аномально высокая температура",
                        Round(day.AvgTempC),
                        Round(monthAvg),
                        Round(z),
                        $"{day.City}, {day.Date:yyyy-MM-dd}: средняя температура {Round(day.AvgTempC)}°C выше месячной нормы {Round(monthAvg)}°C, z-score {Round(z)}."));
                }
                else if (z <= -WeatherRules.TemperatureAnomalyZScore)
                {
                    anomalies.Add(new WeatherAnomaly(
                        day.LocationId,
                        day.City,
                        day.Date,
                        day.Date,
                        "Аномально низкая температура",
                        Round(day.AvgTempC),
                        Round(monthAvg),
                        Round(z),
                        $"{day.City}, {day.Date:yyyy-MM-dd}: средняя температура {Round(day.AvgTempC)}°C ниже месячной нормы {Round(monthAvg)}°C, z-score {Round(z)}."));
                }
            }
        }

        return anomalies;
    }

    private static IReadOnlyList<WeatherAnomaly> FindSharpTemperatureChanges(IReadOnlyList<WeatherDay> days)
    {
        var tempDays = days.Where(day => day.AvgTempC.HasValue).OrderBy(day => day.Date).ToList();
        var changes = new List<(WeatherDay Day, double Change, double SignedChange)>();

        for (var i = 1; i < tempDays.Count; i++)
        {
            if (tempDays[i].Date != tempDays[i - 1].Date.AddDays(1))
            {
                continue;
            }

            var signedChange = tempDays[i].AvgTempC!.Value - tempDays[i - 1].AvgTempC!.Value;
            changes.Add((tempDays[i], Math.Abs(signedChange), signedChange));
        }

        if (changes.Count < WeatherRules.MinTemperatureChangeSamples)
        {
            return [];
        }

        var threshold = Percentile(changes.Select(item => item.Change), WeatherRules.HighPercentile);
        if (threshold <= 0)
        {
            return [];
        }

        return changes
            .Where(item => item.Change >= threshold)
            .Select(item => new WeatherAnomaly(
                item.Day.LocationId,
                item.Day.City,
                item.Day.Date,
                item.Day.Date,
                item.SignedChange > 0 ? "Резкий рост температуры" : "Резкое падение температуры",
                Round(item.SignedChange),
                Round(threshold),
                Round(item.Change / threshold),
                $"{item.Day.City}, {item.Day.Date:yyyy-MM-dd}: изменение средней температуры на {Round(item.SignedChange)}°C, порог 95-го перцентиля {Round(threshold)}°C."))
            .ToList();
    }

    private static IReadOnlyList<WeatherAnomaly> FindHeavyRain(IReadOnlyList<WeatherDay> days)
    {
        var rainyDays = days
            .Where(day => day.RainMm.HasValue && day.RainMm.Value >= WeatherRules.RainyDayMm)
            .ToList();

        if (rainyDays.Count < WeatherRules.MinPercentileAnomalySamples)
        {
            return [];
        }

        var threshold = Percentile(rainyDays.Select(day => day.RainMm!.Value), WeatherRules.HighPercentile);
        return rainyDays
            .Where(day => day.RainMm!.Value >= threshold)
            .Select(day => new WeatherAnomaly(
                day.LocationId,
                day.City,
                day.Date,
                day.Date,
                "Аномально сильные осадки",
                Round(day.RainMm),
                Round(threshold),
                threshold == 0 ? null : Round(day.RainMm!.Value / threshold),
                $"{day.City}, {day.Date:yyyy-MM-dd}: осадки {Round(day.RainMm)} мм, порог сильных осадков {Round(threshold)} мм."))
            .ToList();
    }

    private static IReadOnlyList<WeatherAnomaly> FindStrongWind(IReadOnlyList<WeatherDay> days)
    {
        var windDays = days.Where(day => day.MaxWindMs.HasValue).ToList();
        if (windDays.Count < WeatherRules.MinPercentileAnomalySamples)
        {
            return [];
        }

        var threshold = Percentile(windDays.Select(day => day.MaxWindMs!.Value), WeatherRules.HighPercentile);
        if (threshold <= 0)
        {
            return [];
        }

        return windDays
            .Where(day => day.MaxWindMs!.Value >= threshold)
            .Select(day => new WeatherAnomaly(
                day.LocationId,
                day.City,
                day.Date,
                day.Date,
                "Аномально сильный ветер",
                Round(day.MaxWindMs),
                Round(threshold),
                Round(day.MaxWindMs!.Value / threshold),
                $"{day.City}, {day.Date:yyyy-MM-dd}: максимальный ветер {Round(day.MaxWindMs)} м/с, порог 95-го перцентиля {Round(threshold)} м/с."))
            .ToList();
    }

    private static IReadOnlyList<WeatherAnomaly> FindDryPeriods(IReadOnlyList<WeatherDay> days)
    {
        var anomalies = new List<WeatherAnomaly>();
        WeatherDay? start = null;
        WeatherDay? last = null;
        var count = 0;

        foreach (var day in days.OrderBy(day => day.Date))
        {
            if (last is not null && day.Date != last.Date.AddDays(1))
            {
                AddDryPeriodIfNeeded(anomalies, start, last, count);
                start = null;
                last = null;
                count = 0;
            }

            if (!day.RainMm.HasValue)
            {
                AddDryPeriodIfNeeded(anomalies, start, last, count);
                start = null;
                last = null;
                count = 0;
                continue;
            }

            var isDry = day.RainMm.Value < WeatherRules.RainyDayMm;
            if (isDry)
            {
                start ??= day;
                last = day;
                count++;
                continue;
            }

            AddDryPeriodIfNeeded(anomalies, start, last, count);
            start = null;
            last = null;
            count = 0;
        }

        AddDryPeriodIfNeeded(anomalies, start, last, count);
        return anomalies;
    }

    private static void AddDryPeriodIfNeeded(
        List<WeatherAnomaly> anomalies,
        WeatherDay? start,
        WeatherDay? last,
        int count)
    {
        if (start is null || last is null || count < WeatherRules.DryPeriodDays)
        {
            return;
        }

        anomalies.Add(new WeatherAnomaly(
            start.LocationId,
            start.City,
            start.Date,
            last.Date,
            "Длительный период без осадков",
            count,
            WeatherRules.DryPeriodDays,
            Round((double)count / WeatherRules.DryPeriodDays),
            $"{start.City}, {start.Date:yyyy-MM-dd} - {last.Date:yyyy-MM-dd}: {count} сухих дней подряд."));
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.OrderBy(value => value).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        if (sorted.Count == 1)
        {
            return sorted[0];
        }

        var position = (sorted.Count - 1) * percentile;
        var left = (int)Math.Floor(position);
        var right = (int)Math.Ceiling(position);
        if (left == right)
        {
            return sorted[left];
        }

        var weight = position - left;
        return sorted[left] + (sorted[right] - sorted[left]) * weight;
    }

    private static double StandardDeviation(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var avg = list.Average();
        var variance = list.Sum(value => Math.Pow(value - avg, 2)) / list.Count;
        return Math.Sqrt(variance);
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
