namespace WeatherAnalytics.Core;

public record CityComparison(
    int LocationId,
    string City,
    double? AvgTempC,
    double? MinTempC,
    double? MaxTempC,
    double? TotalRainMm,
    int RainyDays,
    double? AvgWindMs,
    double? AvgClouds);
