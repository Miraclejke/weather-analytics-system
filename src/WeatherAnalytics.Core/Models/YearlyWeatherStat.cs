namespace WeatherAnalytics.Core;

public record YearlyWeatherStat(
    int LocationId,
    string City,
    int Year,
    double? AvgTempC,
    double? RainMm);
