using WeatherAnalytics.Infrastructure;

namespace WeatherAnalytics.Infrastructure.Data;

public class WeatherDailyEntity
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public DateOnly Date { get; set; }
    public double? AvgTempC { get; set; }
    public double? MinTempC { get; set; }
    public double? MaxTempC { get; set; }
    public double? AvgHumidity { get; set; }
    public double? AvgPressureHpa { get; set; }
    public double? AvgWindMs { get; set; }
    public double? MaxWindMs { get; set; }
    public double? RainMm { get; set; }
    public double? AvgClouds { get; set; }
    public int? WeatherCode { get; set; }
    public string Source { get; set; } = WeatherSources.OpenMeteo;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public LocationEntity? Location { get; set; }
}
