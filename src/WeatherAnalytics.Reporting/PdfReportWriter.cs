using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WeatherAnalytics.Core;

namespace WeatherAnalytics.Reporting;

public class PdfReportWriter
{
    public void Write(WeatherReportData reportData, ChartSet charts)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(reportData.Request.OutputPath));
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(9));

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    AddRequestSection(column, reportData);
                    AddTemperatureSummary(column, reportData);
                    AddRainSummary(column, reportData);
                    AddComparisonSection(column, reportData);
                    AddCharts(column, charts);
                    AddCityDetails(column, reportData);
                    AddAnomalies(column, reportData);
                    AddForecast(column, reportData, charts);
                    AddFinalConclusion(column, reportData);
                });
            });
        }).GeneratePdf(reportData.Request.OutputPath);
    }

    private static void AddRequestSection(ColumnDescriptor column, WeatherReportData reportData)
    {
        AddSectionTitle(column, "Параметры запуска");
        var request = reportData.Request;
        AddKeyValueTable(column, new[]
        {
            ("Города", string.Join(", ", request.Cities)),
            ("Страна", request.Country),
            ("Период", $"{request.From:yyyy-MM-dd} - {request.To:yyyy-MM-dd}"),
            ("Порог осадков", $"{request.RainThresholdMm:0.##} мм"),
            ("Файл отчета", request.OutputPath),
            ("Количество дневных наблюдений", reportData.WeatherDays.Count.ToString())
        });

        if (reportData.Notes.Count > 0)
        {
            column.Item().Text("Замечания по данным").Bold();
            foreach (var note in reportData.Notes)
            {
                column.Item().Text("- " + note);
            }
        }
    }

    private static void AddTemperatureSummary(ColumnDescriptor column, WeatherReportData reportData)
    {
        AddSectionTitle(column, "Температура");
        AddSimpleTable(
            column,
            ["Город", "Дней", "Средняя", "Минимум", "Дата минимума", "Максимум", "Дата максимума"],
            reportData.TemperatureSummaries.Select(item => new[]
            {
                item.City,
                item.Days.ToString(),
                Format(item.AvgTempC, " °C"),
                Format(item.MinTempC, " °C"),
                Format(item.ColdestDate),
                Format(item.MaxTempC, " °C"),
                Format(item.WarmestDate)
            }));
    }

    private static void AddRainSummary(ColumnDescriptor column, WeatherReportData reportData)
    {
        AddSectionTitle(column, "Осадки");
        AddSimpleTable(
            column,
            ["Город", "Всего", "В день", "Дней с осадками", "Дней выше порога", "Самый дождливый день", "Максимум за день"],
            reportData.RainSummaries.Select(item => new[]
            {
                item.City,
                Format(item.TotalRainMm, " мм"),
                Format(item.AvgRainMm, " мм"),
                item.RainyDays.ToString(),
                item.DaysAboveThreshold.ToString(),
                Format(item.WettestDate),
                Format(item.WettestRainMm, " мм")
            }));
    }

    private static void AddComparisonSection(ColumnDescriptor column, WeatherReportData reportData)
    {
        AddSectionTitle(column, "Сравнение городов");
        if (reportData.CityComparisons.Count < 2)
        {
            column.Item().Text("Сравнение городов не выполнялось, так как выбран один город.");
            return;
        }

        AddSimpleTable(
            column,
            ["Город", "Средняя t", "Мин. t", "Макс. t", "Осадки", "Дождливые дни", "Ветер", "Облачность"],
            reportData.CityComparisons.Select(item => new[]
            {
                item.City,
                Format(item.AvgTempC, " °C"),
                Format(item.MinTempC, " °C"),
                Format(item.MaxTempC, " °C"),
                Format(item.TotalRainMm, " мм"),
                item.RainyDays.ToString(),
                Format(item.AvgWindMs, " м/с"),
                Format(item.AvgClouds, " %")
            }));
    }

    private static void AddCharts(ColumnDescriptor column, ChartSet charts)
    {
        AddSectionTitle(column, "Графики");
        AddImage(column, charts.TemperatureChart, "Изменение средней температуры");
        AddImage(column, charts.RainChart, "Осадки по месяцам");
        AddImage(column, charts.AvgTempComparisonChart, "Сравнение средней температуры по городам");
        AddImage(column, charts.RainComparisonChart, "Сравнение общего количества осадков");
        AddImage(column, charts.RainyDaysComparisonChart, "Сравнение количества дней с осадками");
    }

    private static void AddCityDetails(ColumnDescriptor column, WeatherReportData reportData)
    {
        AddSectionTitle(column, "Детали по городам");
        foreach (var city in GetReportCities(reportData))
        {
            column.Item().Text(city).Bold();
            AddSimpleTable(
                column,
                ["Показатель", "Дата", "Значение"],
                reportData.ColdestDays
                    .Where(item => SameCity(item.City, city))
                    .Concat(reportData.WarmestDays.Where(item => SameCity(item.City, city)))
                    .Concat(reportData.RainiestDays.Where(item => SameCity(item.City, city)))
                    .Take(15)
                    .Select(item => new[]
                    {
                        item.Type,
                        Format(item.Date),
                        Format(item.Value, item.Type.Contains("дожд", StringComparison.OrdinalIgnoreCase) ? " мм" : " °C")
                    }));

            var seasonalRows = reportData.SeasonalStats
                .Where(item => SameCity(item.City, city))
                .OrderBy(item => item.Year)
                .ThenBy(item => item.Season)
                .Select(item => new[]
                {
                    $"{item.Year} {item.Season}",
                    Format(item.AvgTempC, " °C"),
                    Format(item.MinTempC, " °C"),
                    Format(item.MaxTempC, " °C")
                });

            AddSimpleTable(column, ["Сезон", "Средняя", "Минимум", "Максимум"], seasonalRows);
        }
    }

    private static void AddAnomalies(ColumnDescriptor column, WeatherReportData reportData)
    {
        AddSectionTitle(column, "Аномалии");
        if (reportData.Anomalies.Count == 0)
        {
            column.Item().Text("За выбранный период аномалии не обнаружены.");
            return;
        }

        AddSimpleTable(
            column,
            ["Город", "Дата/период", "Тип", "Показатель", "Норма/порог", "Сила отклонения"],
            reportData.Anomalies.Select(item => new[]
            {
                item.City,
                item.FromDate == item.ToDate
                    ? $"{item.FromDate:yyyy-MM-dd}"
                    : $"{item.FromDate:yyyy-MM-dd} - {item.ToDate:yyyy-MM-dd}",
                item.Type,
                Format(item.Actual),
                Format(item.Normal),
                Format(item.Score)
            }));
    }

    private static void AddForecast(ColumnDescriptor column, WeatherReportData reportData, ChartSet charts)
    {
        AddSectionTitle(column, "Прогноз");
        if (reportData.Forecasts.Count == 0)
        {
            column.Item().Text("Прогноз не построен: для одного или нескольких городов недостаточно данных.");
            return;
        }

        var forecastCities = reportData.Forecasts.Select(item => item.City).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingCities = GetReportCities(reportData).Where(city => !forecastCities.Contains(city)).ToList();
        if (missingCities.Count > 0)
        {
            column.Item().Text("Прогноз не построен для городов: " + string.Join(", ", missingCities) + ".");
        }

        AddSimpleTable(
            column,
            ["Город", "Дата", "Средняя", "Минимум", "Максимум", "Вероятность осадков"],
            reportData.Forecasts.Select(item => new[]
            {
                item.City,
                Format(item.Date),
                Format(item.AvgTempC, " °C"),
                Format(item.MinTempC, " °C"),
                Format(item.MaxTempC, " °C"),
                Format(item.RainChance.HasValue ? item.RainChance.Value * 100 : null, " %")
            }));

        AddImage(column, charts.ForecastChart, "График прогноза средней температуры");
    }

    private static void AddFinalConclusion(ColumnDescriptor column, WeatherReportData reportData)
    {
        AddSectionTitle(column, "Итоговый вывод");
        var warmest = reportData.CityComparisons.MaxBy(item => item.AvgTempC);
        var coldest = reportData.CityComparisons.MinBy(item => item.AvgTempC);
        var wettest = reportData.CityComparisons.MaxBy(item => item.TotalRainMm);
        var rainyDays = reportData.CityComparisons.MaxBy(item => item.RainyDays);
        var anomalyText = reportData.Anomalies.Count == 0
            ? "Значимых аномалий не обнаружено."
            : $"Найдено аномалий: {reportData.Anomalies.Count}.";
        var trendText = string.Join("; ", reportData.LongTermTrends.Select(item => $"{item.City}: {item.Note}"));

        column.Item().Text(
            $"За выбранный период самым теплым городом по средней температуре был {warmest?.City ?? "нет данных"}, "
            + $"самым холодным - {coldest?.City ?? "нет данных"}. "
            + $"Больше всего осадков зафиксировано в городе {wettest?.City ?? "нет данных"}, "
            + $"а больше всего дней с осадками - в городе {rainyDays?.City ?? "нет данных"}. "
            + $"{anomalyText} {trendText}");

        if (reportData.Forecasts.Count > 0)
        {
            var forecastAvg = reportData.Forecasts.Average(item => item.AvgTempC);
            column.Item().Text($"Средняя прогнозная температура по всем городам на ближайшие {WeatherRules.ForecastDays} дней: {forecastAvg:0.##} °C.");
        }
    }

    private static void AddSectionTitle(ColumnDescriptor column, string title)
    {
        column.Item().PaddingTop(6).Text(title).FontSize(13).Bold();
    }

    private static void AddImage(ColumnDescriptor column, string? path, string title)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        column.Item().Text(title).Bold();
        column.Item().Image(path).FitWidth();
    }

    private static void AddKeyValueTable(ColumnDescriptor column, IEnumerable<(string Key, string Value)> rows)
    {
        AddSimpleTable(
            column,
            ["Параметр", "Значение"],
            rows.Select(row => new[] { row.Key, row.Value }));
    }

    private static void AddSimpleTable(ColumnDescriptor column, string[] headers, IEnumerable<string[]> rows)
    {
        var rowList = rows.ToList();
        if (rowList.Count == 0)
        {
            column.Item().Text("Нет данных для таблицы.");
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in headers)
                {
                    columns.RelativeColumn();
                }
            });

            table.Header(header =>
            {
                foreach (var title in headers)
                {
                    header.Cell().Element(HeaderCell).Text(title).Bold();
                }
            });

            foreach (var row in rowList)
            {
                foreach (var cell in row)
                {
                    table.Cell().Element(BodyCell).Text(cell);
                }
            }
        });
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .Padding(3);
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(3);
    }

    private static string Format(DateOnly? value)
    {
        return value.HasValue ? $"{value.Value:yyyy-MM-dd}" : "нет данных";
    }

    private static string Format(double? value, string suffix = "")
    {
        return value.HasValue ? $"{value.Value:0.##}{suffix}" : "нет данных";
    }

    private static IReadOnlyList<string> GetReportCities(WeatherReportData reportData)
    {
        var cities = reportData.WeatherDays
            .Select(item => item.City)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(city => city)
            .ToList();

        return cities.Count == 0 ? reportData.Request.Cities : cities;
    }

    private static bool SameCity(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
