namespace WeatherAnalytics.Infrastructure.Data;

public class AnalysisResultEntity
{
    public int Id { get; set; }
    public int ReportRunId { get; set; }
    public int? LocationId { get; set; }
    public string Type { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string DataJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AnalysisRunEntity? ReportRun { get; set; }
    public LocationEntity? Location { get; set; }
}
