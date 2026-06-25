namespace WeatherAnalytics.Core;

public static class WeatherRules
{
    public const double DefaultRainThresholdMm = 1.0;
    public const double RainyDayMm = 0.1;
    public const double TemperatureAnomalyZScore = 2.0;
    public const double HighPercentile = 0.95;
    public const int DryPeriodDays = 7;
    public const int ForecastDays = 7;
    public const int ForecastMinHistoryDays = 30;
    public const int ForecastAverageDays = 7;
    public const int ForecastRangeDays = 14;
    public const double DefaultTemperatureGapC = 2.0;
    public const double NoticeableTrendPerYearC = 0.05;
    public const int MinTemperatureAnomalySamples = 3;
    public const int MinPercentileAnomalySamples = 3;
    public const int MinTemperatureChangeSamples = 5;
}
