using System.Text.Json;
using WeatherAnalytics.Core;
using WeatherAnalytics.Infrastructure.Data;

namespace WeatherAnalytics.Infrastructure;

public class AnalysisStore(WeatherDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task SaveAsync(WeatherReportData reportData, CancellationToken cancellationToken)
    {
        var request = reportData.Request;
        var run = new AnalysisRunEntity
        {
            StartedAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
            Cities = string.Join(",", request.Cities),
            Country = request.Country,
            FromDate = request.From,
            ToDate = request.To,
            OutputPath = request.OutputPath,
            Status = "completed"
        };

        db.AnalysisRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        AddResult(run.Id, null, "temperature_summary", request, reportData.TemperatureSummaries);
        AddResult(run.Id, null, "monthly_stats", request, reportData.MonthlyStats);
        AddResult(run.Id, null, "seasonal_stats", request, reportData.SeasonalStats);
        AddResult(run.Id, null, "rain_summary", request, reportData.RainSummaries);
        AddResult(run.Id, null, "city_comparison", request, reportData.CityComparisons);
        AddResult(run.Id, null, "yearly_stats", request, reportData.YearlyStats);
        AddResult(run.Id, null, "long_term_trends", request, reportData.LongTermTrends);

        foreach (var anomaly in reportData.Anomalies)
        {
            db.Anomalies.Add(new AnomalyEntity
            {
                ReportRunId = run.Id,
                LocationId = anomaly.LocationId,
                Type = anomaly.Type,
                FromDate = anomaly.FromDate,
                ToDate = anomaly.ToDate,
                Actual = anomaly.Actual,
                Normal = anomaly.Normal,
                Score = anomaly.Score,
                Description = anomaly.Description,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach (var forecast in reportData.Forecasts)
        {
            db.Forecasts.Add(new ForecastEntity
            {
                ReportRunId = run.Id,
                LocationId = forecast.LocationId,
                Date = forecast.Date,
                AvgTempC = forecast.AvgTempC,
                MinTempC = forecast.MinTempC,
                MaxTempC = forecast.MaxTempC,
                RainChance = forecast.RainChance,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private void AddResult<T>(
        int runId,
        int? locationId,
        string type,
        ReportRequest request,
        T value)
    {
        db.AnalysisResults.Add(new AnalysisResultEntity
        {
            ReportRunId = runId,
            LocationId = locationId,
            Type = type,
            FromDate = request.From,
            ToDate = request.To,
            DataJson = JsonSerializer.Serialize(value, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
