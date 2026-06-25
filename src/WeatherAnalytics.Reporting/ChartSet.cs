namespace WeatherAnalytics.Reporting;

public record ChartSet(
    string? TemperatureChart,
    string? RainChart,
    string? AvgTempComparisonChart,
    string? RainComparisonChart,
    string? RainyDaysComparisonChart,
    string? ForecastChart);
