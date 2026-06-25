namespace WeatherAnalytics.Core;

public record LocationInfo(
    int Id,
    string Name,
    string Country,
    double Latitude,
    double Longitude,
    string Timezone);
