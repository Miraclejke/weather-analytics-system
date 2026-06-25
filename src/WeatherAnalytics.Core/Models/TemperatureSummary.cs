namespace WeatherAnalytics.Core;

public record TemperatureSummary(
    int LocationId,
    string City,
    double? AvgTempC,
    double? MinTempC,
    double? MaxTempC,
    DateOnly? ColdestDate,
    DateOnly? WarmestDate,
    int Days);
