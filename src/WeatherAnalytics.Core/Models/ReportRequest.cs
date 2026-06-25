namespace WeatherAnalytics.Core;

public record ReportRequest(
    IReadOnlyList<string> Cities,
    string Country,
    DateOnly From,
    DateOnly To,
    string OutputPath,
    double RainThresholdMm);
