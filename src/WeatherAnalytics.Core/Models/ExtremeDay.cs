namespace WeatherAnalytics.Core;

public record ExtremeDay(
    int LocationId,
    string City,
    DateOnly Date,
    string Type,
    double? Value);
