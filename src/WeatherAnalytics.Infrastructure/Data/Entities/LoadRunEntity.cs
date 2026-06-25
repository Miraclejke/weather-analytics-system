using WeatherAnalytics.Infrastructure;

namespace WeatherAnalytics.Infrastructure.Data;

public class LoadRunEntity
{
    public int Id { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "running";
    public string Source { get; set; } = WeatherSources.OpenMeteo;
    public string ParamsJson { get; set; } = "";
    public string? Message { get; set; }

    public List<LoadErrorEntity> Errors { get; set; } = [];
}
