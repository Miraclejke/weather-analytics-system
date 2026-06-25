namespace WeatherAnalytics.Core;

public record WeatherDay(
    int LocationId,
    string City,
    string Country,
    DateOnly Date,
    double? AvgTempC,
    double? MinTempC,
    double? MaxTempC,
    double? AvgHumidity,
    double? AvgPressureHpa,
    double? AvgWindMs,
    double? MaxWindMs,
    double? RainMm,
    double? AvgClouds,
    int? WeatherCode);
