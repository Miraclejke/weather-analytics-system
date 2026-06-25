using ScottPlot;
using WeatherAnalytics.Core;

namespace WeatherAnalytics.Reporting;

public class WeatherChartBuilder
{
    public ChartSet CreateCharts(WeatherReportData reportData)
    {
        var chartDir = Path.Combine(Path.GetTempPath(), "weather-analytics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(chartDir);

        return new ChartSet(
            CreateTemperatureChart(reportData, chartDir),
            CreateRainChart(reportData, chartDir),
            CreateComparisonChart(
                reportData.CityComparisons,
                chartDir,
                "avg-temp-comparison.png",
                "Средняя температура по городам",
                "°C",
                item => item.AvgTempC),
            CreateComparisonChart(
                reportData.CityComparisons,
                chartDir,
                "rain-comparison.png",
                "Общее количество осадков по городам",
                "мм",
                item => item.TotalRainMm),
            CreateComparisonChart(
                reportData.CityComparisons,
                chartDir,
                "rainy-days-comparison.png",
                "Количество дней с осадками по городам",
                "дни",
                item => item.RainyDays),
            CreateForecastChart(reportData, chartDir));
    }

    private static string? CreateTemperatureChart(WeatherReportData reportData, string chartDir)
    {
        if (reportData.WeatherDays.Count == 0)
        {
            return null;
        }

        var plot = new Plot();
        plot.Title("Изменение средней температуры");
        plot.XLabel("Дата");
        plot.YLabel("°C");

        if ((reportData.Request.To.DayNumber - reportData.Request.From.DayNumber) > 365)
        {
            foreach (var cityGroup in reportData.MonthlyStats.GroupBy(item => item.City))
            {
                var points = cityGroup
                    .Where(item => item.AvgTempC.HasValue)
                    .OrderBy(item => item.Year)
                    .ThenBy(item => item.Month)
                    .ToList();

                AddDateScatter(plot, points.Select(item => new DateOnly(item.Year, item.Month, 1)), points.Select(item => item.AvgTempC!.Value), cityGroup.Key);
            }
        }
        else
        {
            foreach (var cityGroup in reportData.WeatherDays.GroupBy(item => item.City))
            {
                var points = cityGroup
                    .Where(item => item.AvgTempC.HasValue)
                    .OrderBy(item => item.Date)
                    .ToList();

                AddDateScatter(plot, points.Select(item => item.Date), points.Select(item => item.AvgTempC!.Value), cityGroup.Key);
            }
        }

        plot.Axes.DateTimeTicksBottom();
        plot.ShowLegend();
        return Save(plot, chartDir, "temperature.png");
    }

    private static string? CreateRainChart(WeatherReportData reportData, string chartDir)
    {
        if (reportData.MonthlyStats.Count == 0)
        {
            return null;
        }

        var plot = new Plot();
        plot.Title("Осадки по месяцам");
        plot.XLabel("Месяц");
        plot.YLabel("мм");

        foreach (var cityGroup in reportData.MonthlyStats.GroupBy(item => item.City))
        {
            var points = cityGroup
                .Where(item => item.RainMm.HasValue)
                .OrderBy(item => item.Year)
                .ThenBy(item => item.Month)
                .ToList();

            AddDateScatter(plot, points.Select(item => new DateOnly(item.Year, item.Month, 1)), points.Select(item => item.RainMm!.Value), cityGroup.Key);
        }

        plot.Axes.DateTimeTicksBottom();
        plot.ShowLegend();
        return Save(plot, chartDir, "rain.png");
    }

    private static string? CreateComparisonChart(
        IReadOnlyList<CityComparison> comparisons,
        string chartDir,
        string fileName,
        string title,
        string yLabel,
        Func<CityComparison, double?> valueSelector)
    {
        if (comparisons.Count < 2)
        {
            return null;
        }

        var rows = comparisons
            .Select(item => new { item.City, Value = valueSelector(item) })
            .Where(item => item.Value.HasValue)
            .OrderByDescending(item => item.Value)
            .ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        var xs = Enumerable.Range(1, rows.Count).Select(item => (double)item).ToArray();
        var ys = rows.Select(item => item.Value!.Value).ToArray();
        var labels = rows.Select(item => item.City).ToArray();

        var plot = new Plot();
        plot.Title(title);
        plot.XLabel("Город");
        plot.YLabel(yLabel);
        plot.Add.Scatter(xs, ys);
        plot.Axes.Bottom.SetTicks(xs, labels);

        return Save(plot, chartDir, fileName);
    }

    private static string? CreateForecastChart(WeatherReportData reportData, string chartDir)
    {
        if (reportData.Forecasts.Count == 0)
        {
            return null;
        }

        var plot = new Plot();
        plot.Title($"Прогноз средней температуры на {WeatherRules.ForecastDays} дней");
        plot.XLabel("Дата");
        plot.YLabel("°C");

        foreach (var cityGroup in reportData.Forecasts.GroupBy(item => item.City))
        {
            var points = cityGroup.OrderBy(item => item.Date).ToList();
            AddDateScatter(plot, points.Select(item => item.Date), points.Select(item => item.AvgTempC), cityGroup.Key);
        }

        plot.Axes.DateTimeTicksBottom();
        plot.ShowLegend();
        return Save(plot, chartDir, "forecast.png");
    }

    private static void AddDateScatter(Plot plot, IEnumerable<DateOnly> dates, IEnumerable<double> values, string city)
    {
        var xs = dates.Select(ToOADate).ToArray();
        var ys = values.ToArray();
        if (xs.Length == 0)
        {
            return;
        }

        var scatter = plot.Add.Scatter(xs, ys);
        scatter.LegendText = city;
    }

    private static double ToOADate(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue).ToOADate();
    }

    private static string Save(Plot plot, string chartDir, string fileName)
    {
        var path = Path.Combine(chartDir, fileName);
        plot.SavePng(path, 1000, 520);
        return path;
    }
}
