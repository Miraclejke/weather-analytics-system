using System.Globalization;
using WeatherAnalytics.Core;
using WeatherAnalytics.Infrastructure;
using WeatherAnalytics.Infrastructure.Data;
using WeatherAnalytics.Infrastructure.OpenMeteo;
using WeatherAnalytics.Reporting;

namespace WeatherAnalytics.Cli;

internal static class Program
{
    private static readonly HashSet<string> KnownArgs = new(StringComparer.OrdinalIgnoreCase)
    {
        "--cities",
        "--country",
        "--from",
        "--to",
        "--output",
        "--rain-threshold"
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
            {
                PrintUsage();
                return 0;
            }

            var request = ParseArgs(args);
            var connectionString = Environment.GetEnvironmentVariable("WEATHER_DB_CONNECTION")
                ?? BuildConnectionString();

            await using var db = WeatherDb.Create(connectionString);
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            var openMeteoOptions = BuildOpenMeteoOptions();

            var loader = new WeatherDataLoader(db, new OpenMeteoClient(httpClient, openMeteoOptions));
            var reportBuilder = new WeatherReportBuilder();
            var chartBuilder = new WeatherChartBuilder();
            var pdf = new PdfReportWriter();
            var analysisStore = new AnalysisStore(db);

            Console.WriteLine("Запуск погодной аналитической системы.");
            Console.WriteLine($"Период: {request.From:yyyy-MM-dd} - {request.To:yyyy-MM-dd}");
            Console.WriteLine($"Города: {string.Join(", ", request.Cities)}");

            var weatherDays = await loader.LoadAsync(request, Console.Out, CancellationToken.None);
            Console.WriteLine($"Дневных наблюдений для анализа: {weatherDays.Count}");

            var reportData = reportBuilder.BuildReport(weatherDays, request);
            var chartSet = chartBuilder.CreateCharts(reportData);

            pdf.Write(reportData, chartSet);
            await analysisStore.SaveAsync(reportData, CancellationToken.None);

            Console.WriteLine($"PDF-отчет создан: {request.OutputPath}");
            Console.WriteLine("Результаты аналитики, аномалии и прогноз сохранены в PostgreSQL.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("Ошибка параметров: " + ex.Message);
            PrintUsage();
            return 1;
        }
        catch (WeatherDataException ex)
        {
            Console.Error.WriteLine("Ошибка данных: " + ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Непредвиденная ошибка: " + ex.Message);
            return 3;
        }
    }

    private static ReportRequest ParseArgs(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Неожиданный аргумент '{arg}'.");
            }

            if (!KnownArgs.Contains(arg))
            {
                throw new ArgumentException($"Неизвестный параметр '{arg}'.");
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Для параметра '{arg}' не указано значение.");
            }

            values[arg] = args[i + 1];
            i++;
        }

        var cities = Required(values, "--cities")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cities.Count == 0)
        {
            throw new ArgumentException("Параметр --cities должен содержать хотя бы один город.");
        }

        var country = Required(values, "--country").Trim().ToUpperInvariant();
        if (country.Length != 2 || !country.All(char.IsLetter))
        {
            throw new ArgumentException("Параметр --country должен быть двухбуквенным ISO-кодом страны, например RU.");
        }

        var from = ParseDate(Required(values, "--from"), "--from");
        var to = ParseDate(Required(values, "--to"), "--to");
        if (from > to)
        {
            throw new ArgumentException("Дата --from должна быть меньше или равна --to.");
        }

        var output = Required(values, "--output").Trim();
        if (!output.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Параметр --output должен указывать путь к PDF-файлу.");
        }

        var rainThreshold = WeatherRules.DefaultRainThresholdMm;
        if (values.TryGetValue("--rain-threshold", out var thresholdText)
            && !double.TryParse(thresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out rainThreshold))
        {
            throw new ArgumentException("Параметр --rain-threshold должен быть числом, например 1.0.");
        }

        if (rainThreshold < 0)
        {
            throw new ArgumentException("Параметр --rain-threshold не может быть отрицательным.");
        }

        return new ReportRequest(cities, country, from, to, output, rainThreshold);
    }

    private static string Required(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Не указан обязательный параметр {key}.");
        }

        return value;
    }

    private static DateOnly ParseDate(string value, string name)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ArgumentException($"Параметр {name} должен быть датой в формате yyyy-MM-dd.");
        }

        return date;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Пример запуска:");
        Console.WriteLine("dotnet run --project src/WeatherAnalytics.Cli -- --cities Moscow,Kazan,Saint-Petersburg --country RU --from 2021-01-01 --to 2025-12-31 --output reports/weather-report.pdf");
        Console.WriteLine();
        Console.WriteLine("Обязательные параметры: --cities, --country, --from, --to, --output.");
        Console.WriteLine($"Дополнительно: --rain-threshold {WeatherRules.DefaultRainThresholdMm.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine("Строку подключения можно переопределить через WEATHER_DB_CONNECTION.");
    }

    private static string BuildConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "weather_analytics";
        var user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "weather";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "weather";

        return $"Host={host};Port={port};Database={database};Username={user};Password={password}";
    }

    private static OpenMeteoOptions BuildOpenMeteoOptions()
    {
        var defaults = new OpenMeteoOptions();
        return new OpenMeteoOptions
        {
            GeocodingBaseUrl = Environment.GetEnvironmentVariable("OPEN_METEO_GEOCODING_BASE_URL")
                ?? defaults.GeocodingBaseUrl,
            HistoricalBaseUrl = Environment.GetEnvironmentVariable("OPEN_METEO_HISTORICAL_BASE_URL")
                ?? defaults.HistoricalBaseUrl,
            GeocodingResultCount = defaults.GeocodingResultCount,
            GeocodingLanguage = defaults.GeocodingLanguage,
            Format = defaults.Format,
            Timezone = defaults.Timezone,
            WindSpeedUnit = defaults.WindSpeedUnit,
            DailyVariables = defaults.DailyVariables
        };
    }
}
