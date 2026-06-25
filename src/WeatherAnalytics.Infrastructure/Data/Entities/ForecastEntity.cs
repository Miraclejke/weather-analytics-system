namespace WeatherAnalytics.Infrastructure.Data;

public class ForecastEntity
{
    public int Id { get; set; }
    public int ReportRunId { get; set; }
    public int LocationId { get; set; }
    public DateOnly Date { get; set; }
    public double AvgTempC { get; set; }
    public double MinTempC { get; set; }
    public double MaxTempC { get; set; }
    public double? RainChance { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AnalysisRunEntity? ReportRun { get; set; }
    public LocationEntity? Location { get; set; }
}
