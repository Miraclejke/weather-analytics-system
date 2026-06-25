namespace WeatherAnalytics.Core;

public record ForecastDay(
    int LocationId,
    string City,
    DateOnly Date,
    double AvgTempC,
    double MinTempC,
    double MaxTempC,
    double? RainChance);
