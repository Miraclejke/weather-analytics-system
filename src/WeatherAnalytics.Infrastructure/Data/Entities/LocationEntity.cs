using WeatherAnalytics.Infrastructure;

namespace WeatherAnalytics.Infrastructure.Data;

public class LocationEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Timezone { get; set; } = "";
    public string Source { get; set; } = WeatherSources.OpenMeteo;
    public string? SourceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<WeatherDailyEntity> WeatherDays { get; set; } = [];
}
