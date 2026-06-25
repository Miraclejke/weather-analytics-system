namespace WeatherAnalytics.Core;

public record MonthlyWeatherStat(
    int LocationId,
    string City,
    int Year,
    int Month,
    double? AvgTempC,
    double? MinTempC,
    double? MaxTempC,
    double? RainMm,
    int RainyDays);
