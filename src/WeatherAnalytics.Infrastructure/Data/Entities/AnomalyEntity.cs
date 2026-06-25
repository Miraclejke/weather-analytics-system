namespace WeatherAnalytics.Infrastructure.Data;

public class AnomalyEntity
{
    public int Id { get; set; }
    public int ReportRunId { get; set; }
    public int LocationId { get; set; }
    public string Type { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public double? Actual { get; set; }
    public double? Normal { get; set; }
    public double? Score { get; set; }
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AnalysisRunEntity? ReportRun { get; set; }
    public LocationEntity? Location { get; set; }
}
