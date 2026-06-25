using Microsoft.EntityFrameworkCore;

namespace WeatherAnalytics.Infrastructure.Data;

public class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<LocationEntity> Locations => Set<LocationEntity>();
    public DbSet<WeatherDailyEntity> WeatherDaily => Set<WeatherDailyEntity>();
    public DbSet<LoadRunEntity> LoadRuns => Set<LoadRunEntity>();
    public DbSet<LoadErrorEntity> LoadErrors => Set<LoadErrorEntity>();
    public DbSet<AnalysisRunEntity> AnalysisRuns => Set<AnalysisRunEntity>();
    public DbSet<AnalysisResultEntity> AnalysisResults => Set<AnalysisResultEntity>();
    public DbSet<AnomalyEntity> Anomalies => Set<AnomalyEntity>();
    public DbSet<ForecastEntity> Forecasts => Set<ForecastEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureLocations(modelBuilder);
        ConfigureWeatherDaily(modelBuilder);
        ConfigureLoadRuns(modelBuilder);
        ConfigureLoadErrors(modelBuilder);
        ConfigureAnalysisRuns(modelBuilder);
        ConfigureAnalysisResults(modelBuilder);
        ConfigureAnomalies(modelBuilder);
        ConfigureForecasts(modelBuilder);
    }

    private static void ConfigureLocations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LocationEntity>();
        entity.ToTable("locations");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.Name, item.Country, item.Latitude, item.Longitude }).IsUnique();
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        entity.Property(item => item.Country).HasColumnName("country").HasMaxLength(2).IsRequired();
        entity.Property(item => item.Latitude).HasColumnName("latitude");
        entity.Property(item => item.Longitude).HasColumnName("longitude");
        entity.Property(item => item.Timezone).HasColumnName("timezone").HasMaxLength(120).IsRequired();
        entity.Property(item => item.Source).HasColumnName("source").HasMaxLength(80).IsRequired();
        entity.Property(item => item.SourceId).HasColumnName("source_id").HasMaxLength(80);
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
    }

    private static void ConfigureWeatherDaily(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WeatherDailyEntity>();
        entity.ToTable("weather_daily");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => item.Date);
        entity.HasIndex(item => item.LocationId);
        entity.HasIndex(item => new { item.LocationId, item.Date }).IsUnique();
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.LocationId).HasColumnName("location_id");
        entity.Property(item => item.Date).HasColumnName("date");
        entity.Property(item => item.AvgTempC).HasColumnName("avg_temp_c");
        entity.Property(item => item.MinTempC).HasColumnName("min_temp_c");
        entity.Property(item => item.MaxTempC).HasColumnName("max_temp_c");
        entity.Property(item => item.AvgHumidity).HasColumnName("avg_humidity");
        entity.Property(item => item.AvgPressureHpa).HasColumnName("avg_pressure_hpa");
        entity.Property(item => item.AvgWindMs).HasColumnName("avg_wind_ms");
        entity.Property(item => item.MaxWindMs).HasColumnName("max_wind_ms");
        entity.Property(item => item.RainMm).HasColumnName("rain_mm");
        entity.Property(item => item.AvgClouds).HasColumnName("avg_clouds");
        entity.Property(item => item.WeatherCode).HasColumnName("weather_code");
        entity.Property(item => item.Source).HasColumnName("source").HasMaxLength(80).IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        entity.HasOne(item => item.Location)
            .WithMany(item => item.WeatherDays)
            .HasForeignKey(item => item.LocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureLoadRuns(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LoadRunEntity>();
        entity.ToTable("load_runs");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.StartedAt).HasColumnName("started_at");
        entity.Property(item => item.FinishedAt).HasColumnName("finished_at");
        entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        entity.Property(item => item.Source).HasColumnName("source").HasMaxLength(80).IsRequired();
        entity.Property(item => item.ParamsJson).HasColumnName("params_json").HasColumnType("jsonb").IsRequired();
        entity.Property(item => item.Message).HasColumnName("message");
    }

    private static void ConfigureLoadErrors(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LoadErrorEntity>();
        entity.ToTable("load_errors");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.RunId).HasColumnName("run_id");
        entity.Property(item => item.City).HasColumnName("city").HasMaxLength(160);
        entity.Property(item => item.Country).HasColumnName("country").HasMaxLength(2);
        entity.Property(item => item.Date).HasColumnName("date");
        entity.Property(item => item.Message).HasColumnName("message").IsRequired();
        entity.Property(item => item.Details).HasColumnName("details");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
        entity.HasOne(item => item.Run)
            .WithMany(item => item.Errors)
            .HasForeignKey(item => item.RunId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureAnalysisRuns(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AnalysisRunEntity>();
        entity.ToTable("analysis_runs");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.StartedAt).HasColumnName("started_at");
        entity.Property(item => item.FinishedAt).HasColumnName("finished_at");
        entity.Property(item => item.Cities).HasColumnName("cities").IsRequired();
        entity.Property(item => item.Country).HasColumnName("country").HasMaxLength(2).IsRequired();
        entity.Property(item => item.FromDate).HasColumnName("from_date");
        entity.Property(item => item.ToDate).HasColumnName("to_date");
        entity.Property(item => item.OutputPath).HasColumnName("output_path").IsRequired();
        entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
    }

    private static void ConfigureAnalysisResults(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AnalysisResultEntity>();
        entity.ToTable("analysis_results");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.ReportRunId).HasColumnName("report_run_id");
        entity.Property(item => item.LocationId).HasColumnName("location_id");
        entity.Property(item => item.Type).HasColumnName("type").HasMaxLength(100).IsRequired();
        entity.Property(item => item.FromDate).HasColumnName("from_date");
        entity.Property(item => item.ToDate).HasColumnName("to_date");
        entity.Property(item => item.DataJson).HasColumnName("data_json").HasColumnType("jsonb").IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
        entity.HasOne(item => item.ReportRun)
            .WithMany()
            .HasForeignKey(item => item.ReportRunId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Location)
            .WithMany()
            .HasForeignKey(item => item.LocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureAnomalies(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AnomalyEntity>();
        entity.ToTable("anomalies");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.ReportRunId).HasColumnName("report_run_id");
        entity.Property(item => item.LocationId).HasColumnName("location_id");
        entity.Property(item => item.Type).HasColumnName("type").HasMaxLength(120).IsRequired();
        entity.Property(item => item.FromDate).HasColumnName("from_date");
        entity.Property(item => item.ToDate).HasColumnName("to_date");
        entity.Property(item => item.Actual).HasColumnName("actual");
        entity.Property(item => item.Normal).HasColumnName("normal");
        entity.Property(item => item.Score).HasColumnName("score");
        entity.Property(item => item.Description).HasColumnName("description").IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
        entity.HasOne(item => item.ReportRun)
            .WithMany()
            .HasForeignKey(item => item.ReportRunId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Location)
            .WithMany()
            .HasForeignKey(item => item.LocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureForecasts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ForecastEntity>();
        entity.ToTable("forecasts");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.ReportRunId).HasColumnName("report_run_id");
        entity.Property(item => item.LocationId).HasColumnName("location_id");
        entity.Property(item => item.Date).HasColumnName("date");
        entity.Property(item => item.AvgTempC).HasColumnName("avg_temp_c");
        entity.Property(item => item.MinTempC).HasColumnName("min_temp_c");
        entity.Property(item => item.MaxTempC).HasColumnName("max_temp_c");
        entity.Property(item => item.RainChance).HasColumnName("rain_chance");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
        entity.HasOne(item => item.ReportRun)
            .WithMany()
            .HasForeignKey(item => item.ReportRunId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Location)
            .WithMany()
            .HasForeignKey(item => item.LocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
