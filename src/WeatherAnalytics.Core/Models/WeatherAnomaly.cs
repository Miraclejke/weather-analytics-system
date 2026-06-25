namespace WeatherAnalytics.Core;

public record WeatherAnomaly(
    int LocationId,
    string City,
    DateOnly FromDate,
    DateOnly ToDate,
    string Type,
    double? Actual,
    double? Normal,
    double? Score,
    string Description);
