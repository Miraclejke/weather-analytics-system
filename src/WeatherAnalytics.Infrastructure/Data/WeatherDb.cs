using Microsoft.EntityFrameworkCore;

namespace WeatherAnalytics.Infrastructure.Data;

public static class WeatherDb
{
    public static WeatherDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<WeatherDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WeatherDbContext(options);
    }
}
