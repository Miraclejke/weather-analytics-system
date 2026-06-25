namespace WeatherAnalytics.Infrastructure.OpenMeteo;

public record FoundLocation(
    string Name,
    string Country,
    double Latitude,
    double Longitude,
    string Timezone,
    string SourceId);
