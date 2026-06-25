namespace WeatherAnalytics.Infrastructure.Data;

public class LoadErrorEntity
{
    public int Id { get; set; }
    public int? RunId { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateOnly? Date { get; set; }
    public string Message { get; set; } = "";
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public LoadRunEntity? Run { get; set; }
}
