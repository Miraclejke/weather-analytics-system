namespace WeatherAnalytics.Infrastructure.Data;

public class AnalysisRunEntity
{
    public int Id { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public string Cities { get; set; } = "";
    public string Country { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string OutputPath { get; set; } = "";
    public string Status { get; set; } = "completed";
}
