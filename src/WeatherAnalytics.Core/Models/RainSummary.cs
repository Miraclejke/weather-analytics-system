namespace WeatherAnalytics.Core;

public record RainSummary(
    int LocationId,
    string City,
    double? TotalRainMm,
    double? AvgRainMm,
    int RainyDays,
    int DaysAboveThreshold,
    DateOnly? WettestDate,
    double? WettestRainMm);
