namespace WeatherAnalytics.Core;

public record WeatherReportData(
    ReportRequest Request,
    IReadOnlyList<WeatherDay> WeatherDays,
    IReadOnlyList<TemperatureSummary> TemperatureSummaries,
    IReadOnlyList<MonthlyWeatherStat> MonthlyStats,
    IReadOnlyList<SeasonalWeatherStat> SeasonalStats,
    IReadOnlyList<RainSummary> RainSummaries,
    IReadOnlyList<CityComparison> CityComparisons,
    IReadOnlyList<YearlyWeatherStat> YearlyStats,
    IReadOnlyList<LongTermTrend> LongTermTrends,
    IReadOnlyList<ExtremeDay> ColdestDays,
    IReadOnlyList<ExtremeDay> WarmestDays,
    IReadOnlyList<ExtremeDay> RainiestDays,
    IReadOnlyList<WeatherAnomaly> Anomalies,
    IReadOnlyList<ForecastDay> Forecasts,
    IReadOnlyList<string> Notes);
