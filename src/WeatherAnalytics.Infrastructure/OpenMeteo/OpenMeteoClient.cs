using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WeatherAnalytics.Core;
using WeatherAnalytics.Infrastructure;

namespace WeatherAnalytics.Infrastructure.OpenMeteo;

public class OpenMeteoClient(HttpClient httpClient, OpenMeteoOptions? options = null)
{
    private readonly OpenMeteoOptions options = options ?? new OpenMeteoOptions();

    public async Task<FoundLocation?> FindLocationAsync(string city, string country, CancellationToken cancellationToken)
    {
        var url = BuildGeocodingUrl(city);

        var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new WeatherDataException($"Open-Meteo Geocoding API вернул статус {(int)response.StatusCode} для города {city}.");
        }

        var data = await response.Content.ReadFromJsonAsync<GeocodingResponse>(cancellationToken);
        var match = data?.Results?
            .FirstOrDefault(item => string.Equals(item.CountryCode, country, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return null;
        }

        return new FoundLocation(
            match.Name ?? city,
            match.CountryCode ?? country,
            match.Latitude,
            match.Longitude,
            match.Timezone ?? "auto",
            match.Id?.ToString() ?? "");
    }

    public async Task<IReadOnlyList<WeatherDay>> GetDailyWeatherAsync(
        LocationInfo location,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var url = BuildHistoricalUrl(location, from, to);

        var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new WeatherDataException($"Open-Meteo Historical Weather API вернул статус {(int)response.StatusCode} для города {location.Name}.");
        }

        var data = await response.Content.ReadFromJsonAsync<ArchiveResponse>(cancellationToken);
        if (data?.Daily?.Time is null)
        {
            throw new WeatherDataException($"Open-Meteo не вернул дневные данные для города {location.Name} за период {from:yyyy-MM-dd} - {to:yyyy-MM-dd}.");
        }

        var result = new List<WeatherDay>();
        for (var i = 0; i < data.Daily.Time.Count; i++)
        {
            if (!DateOnly.TryParse(data.Daily.Time[i], out var date))
            {
                continue;
            }

            result.Add(new WeatherDay(
                location.Id,
                location.Name,
                location.Country,
                date,
                Read(data.Daily.Temperature2mMean, i),
                Read(data.Daily.Temperature2mMin, i),
                Read(data.Daily.Temperature2mMax, i),
                Read(data.Daily.RelativeHumidity2mMean, i),
                Read(data.Daily.PressureMslMean, i),
                Read(data.Daily.WindSpeed10mMean, i),
                Read(data.Daily.WindSpeed10mMax, i),
                Read(data.Daily.PrecipitationSum, i),
                Read(data.Daily.CloudCoverMean, i),
                Read(data.Daily.WeatherCode, i)));
        }

        return result;
    }

    private static double? Read(IReadOnlyList<double?>? values, int index)
    {
        return values is not null && index < values.Count ? values[index] : null;
    }

    private static int? Read(IReadOnlyList<int?>? values, int index)
    {
        return values is not null && index < values.Count ? values[index] : null;
    }

    private string BuildGeocodingUrl(string city)
    {
        return $"{options.GeocodingBaseUrl.TrimEnd('/')}/v1/search"
            + $"?name={Uri.EscapeDataString(city)}"
            + $"&count={options.GeocodingResultCount}"
            + $"&language={Uri.EscapeDataString(options.GeocodingLanguage)}"
            + $"&format={Uri.EscapeDataString(options.Format)}";
    }

    private string BuildHistoricalUrl(LocationInfo location, DateOnly from, DateOnly to)
    {
        var daily = string.Join(',', options.DailyVariables);

        return $"{options.HistoricalBaseUrl.TrimEnd('/')}/v1/archive"
            + $"?latitude={location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            + $"&longitude={location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            + $"&start_date={from:yyyy-MM-dd}"
            + $"&end_date={to:yyyy-MM-dd}"
            + $"&daily={Uri.EscapeDataString(daily)}"
            + $"&timezone={Uri.EscapeDataString(options.Timezone)}"
            + $"&wind_speed_unit={Uri.EscapeDataString(options.WindSpeedUnit)}";
    }

    private class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult>? Results { get; set; }
    }

    private class GeocodingResult
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }
    }

    private class ArchiveResponse
    {
        [JsonPropertyName("daily")]
        public ArchiveDaily? Daily { get; set; }
    }

    private class ArchiveDaily
    {
        [JsonPropertyName("time")]
        public List<string>? Time { get; set; }

        [JsonPropertyName("temperature_2m_mean")]
        public List<double?>? Temperature2mMean { get; set; }

        [JsonPropertyName("temperature_2m_min")]
        public List<double?>? Temperature2mMin { get; set; }

        [JsonPropertyName("temperature_2m_max")]
        public List<double?>? Temperature2mMax { get; set; }

        [JsonPropertyName("relative_humidity_2m_mean")]
        public List<double?>? RelativeHumidity2mMean { get; set; }

        [JsonPropertyName("pressure_msl_mean")]
        public List<double?>? PressureMslMean { get; set; }

        [JsonPropertyName("wind_speed_10m_mean")]
        public List<double?>? WindSpeed10mMean { get; set; }

        [JsonPropertyName("wind_speed_10m_max")]
        public List<double?>? WindSpeed10mMax { get; set; }

        [JsonPropertyName("precipitation_sum")]
        public List<double?>? PrecipitationSum { get; set; }

        [JsonPropertyName("cloud_cover_mean")]
        public List<double?>? CloudCoverMean { get; set; }

        [JsonPropertyName("weather_code")]
        public List<int?>? WeatherCode { get; set; }
    }
}
