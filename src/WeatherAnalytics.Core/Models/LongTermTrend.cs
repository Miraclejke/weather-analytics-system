namespace WeatherAnalytics.Core;

public record LongTermTrend(
    int LocationId,
    string City,
    double? TempChangeC,
    double? RainChangeMm,
    double? TempTrendPerYear,
    string Note);
