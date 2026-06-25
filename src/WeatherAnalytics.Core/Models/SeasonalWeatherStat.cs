namespace WeatherAnalytics.Core;

public record SeasonalWeatherStat(
    int LocationId,
    string City,
    int Year,
    string Season,
    double? AvgTempC,
    double? MinTempC,
    double? MaxTempC);
