using WeatherAnalytics.Core;

namespace WeatherAnalytics.Tests;

public class AnomalyAndForecastTests
{
    [Fact]
    public void AnomalyFinder_FindsTemperatureAnomalyByZScore()
    {
        var days = new List<WeatherDay>();
        for (var i = 1; i <= 9; i++)
        {
            days.Add(Day(new DateOnly(2024, 7, i), avgTempC: 10));
        }

        days.Add(Day(new DateOnly(2024, 7, 10), avgTempC: 30));

        var anomalies = new AnomalyFinder().Find(days);

        Assert.Contains(anomalies, item =>
            item.Type == "Аномально высокая температура"
            && item.FromDate == new DateOnly(2024, 7, 10)
            && item.Score >= 2);
    }

    [Fact]
    public void AnomalyFinder_FindsDryPeriodAfterSevenDryDays()
    {
        var days = new List<WeatherDay>();
        for (var i = 1; i <= 7; i++)
        {
            days.Add(Day(new DateOnly(2024, 8, i), rainMm: 0));
        }

        days.Add(Day(new DateOnly(2024, 8, 8), rainMm: 2));

        var anomalies = new AnomalyFinder().Find(days);

        Assert.Contains(anomalies, item =>
            item.Type == "Длительный период без осадков"
            && item.FromDate == new DateOnly(2024, 8, 1)
            && item.ToDate == new DateOnly(2024, 8, 7));
    }

    [Fact]
    public void AnomalyFinder_DoesNotJoinDryPeriodAcrossDateGaps()
    {
        var days = new List<WeatherDay>();
        for (var i = 0; i < 7; i++)
        {
            days.Add(Day(new DateOnly(2024, 8, 1).AddDays(i * 2), rainMm: 0));
        }

        var anomalies = new AnomalyFinder().Find(days);

        Assert.DoesNotContain(anomalies, item => item.Type == "Длительный период без осадков");
    }

    [Fact]
    public void AnomalyFinder_DoesNotTreatMissingRainAsDry()
    {
        var days = new List<WeatherDay>();
        for (var i = 0; i < 7; i++)
        {
            days.Add(Day(new DateOnly(2024, 8, 1).AddDays(i), rainMm: null));
        }

        var anomalies = new AnomalyFinder().Find(days);

        Assert.DoesNotContain(anomalies, item => item.Type == "Длительный период без осадков");
    }

    [Fact]
    public void ForecastBuilder_ReturnsSevenForecastDays()
    {
        var days = BuildForecastInput();

        var forecast = new ForecastBuilder().BuildForecast(days);

        Assert.Equal(7, forecast.Count);
        Assert.Equal(new DateOnly(2024, 1, 31), forecast[0].Date);
        Assert.Equal(new DateOnly(2024, 2, 6), forecast[^1].Date);
    }

    [Fact]
    public void ForecastBuilder_CalculatesRainChanceFromLastFourteenDays()
    {
        var days = BuildForecastInput();

        var forecast = new ForecastBuilder().BuildForecast(days);

        Assert.All(forecast, item => Assert.Equal(0.5, item.RainChance));
    }

    [Fact]
    public void ForecastBuilder_LeavesRainChanceEmptyWhenRecentRainDataIsMissing()
    {
        var days = new List<WeatherDay>();
        for (var i = 0; i < 30; i++)
        {
            days.Add(Day(new DateOnly(2024, 1, 1).AddDays(i), avgTempC: 10 + i * 0.1, rainMm: null));
        }

        var forecast = new ForecastBuilder().BuildForecast(days);

        Assert.All(forecast, item => Assert.Null(item.RainChance));
    }

    private static IReadOnlyList<WeatherDay> BuildForecastInput()
    {
        var days = new List<WeatherDay>();
        for (var i = 0; i < 30; i++)
        {
            var date = new DateOnly(2024, 1, 1).AddDays(i);
            var isLast14 = i >= 16;
            var rainMm = isLast14 && i % 2 == 0 ? 1 : 0;
            days.Add(Day(date, avgTempC: 10 + i * 0.1, minTempC: 8 + i * 0.1, maxTempC: 12 + i * 0.1, rainMm: rainMm));
        }

        return days;
    }

    private static WeatherDay Day(
        DateOnly date,
        double avgTempC = 10,
        double? minTempC = null,
        double? maxTempC = null,
        double? rainMm = 0)
    {
        return new WeatherDay(
            1,
            "Moscow",
            "RU",
            date,
            avgTempC,
            minTempC ?? avgTempC - 2,
            maxTempC ?? avgTempC + 2,
            70,
            1010,
            3,
            5,
            rainMm,
            50,
            null);
    }
}
