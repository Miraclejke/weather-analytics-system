namespace WeatherAnalytics.Infrastructure.OpenMeteo;

public class OpenMeteoOptions
{
    public string GeocodingBaseUrl { get; init; } = "https://geocoding-api.open-meteo.com";
    public string HistoricalBaseUrl { get; init; } = "https://archive-api.open-meteo.com";
    public int GeocodingResultCount { get; init; } = 10;
    public string GeocodingLanguage { get; init; } = "en";
    public string Format { get; init; } = "json";
    public string Timezone { get; init; } = "auto";
    public string WindSpeedUnit { get; init; } = "ms";

    public IReadOnlyList<string> DailyVariables { get; init; } =
    [
        "temperature_2m_mean",
        "temperature_2m_min",
        "temperature_2m_max",
        "relative_humidity_2m_mean",
        "pressure_msl_mean",
        "wind_speed_10m_mean",
        "wind_speed_10m_max",
        "precipitation_sum",
        "cloud_cover_mean",
        "weather_code"
    ];
}
