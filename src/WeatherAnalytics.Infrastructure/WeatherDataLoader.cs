using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeatherAnalytics.Core;
using WeatherAnalytics.Infrastructure.Data;
using WeatherAnalytics.Infrastructure.OpenMeteo;

namespace WeatherAnalytics.Infrastructure;

public class WeatherDataLoader(WeatherDbContext db, OpenMeteoClient openMeteo)
{
    public async Task<IReadOnlyList<WeatherDay>> LoadAsync(
        ReportRequest request,
        TextWriter log,
        CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var loadRun = new LoadRunEntity
        {
            StartedAt = DateTimeOffset.UtcNow,
            Status = "running",
            Source = WeatherSources.OpenMeteo,
            ParamsJson = JsonSerializer.Serialize(request)
        };

        db.LoadRuns.Add(loadRun);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var selectedLocations = new List<LocationEntity>();
            foreach (var city in request.Cities)
            {
                var location = await GetOrCreateLocationAsync(city, request.Country, loadRun.Id, log, cancellationToken);
                selectedLocations.Add(location);
            }

            foreach (var location in selectedLocations)
            {
                await LoadMissingWeatherAsync(location, request, loadRun.Id, log, cancellationToken);
            }

            var locationIds = selectedLocations.Select(location => location.Id).ToList();
            var days = await db.WeatherDaily
                .AsNoTracking()
                .Include(day => day.Location)
                .Where(day => locationIds.Contains(day.LocationId)
                    && day.Date >= request.From
                    && day.Date <= request.To)
                .OrderBy(day => day.Location!.Name)
                .ThenBy(day => day.Date)
                .ToListAsync(cancellationToken);

            if (days.Count == 0)
            {
                throw new WeatherDataException($"В БД нет погодных данных за период {request.From:yyyy-MM-dd} - {request.To:yyyy-MM-dd}.");
            }

            await EnsureCompleteCoverageAsync(selectedLocations, days, request, loadRun.Id, cancellationToken);

            loadRun.Status = "completed";
            loadRun.FinishedAt = DateTimeOffset.UtcNow;
            loadRun.Message = "Данные загружены и прочитаны из БД.";
            await db.SaveChangesAsync(cancellationToken);

            return days.Select(MapToCore).ToList();
        }
        catch (Exception ex) when (ex is not WeatherDataException)
        {
            await MarkLoadRunFailedAsync(loadRun.Id, ex.Message, "Ошибка загрузки данных", ex.Message, request.Country, cancellationToken);
            throw new WeatherDataException("Не удалось загрузить погодные данные. Подробности записаны в журнал загрузки.", ex);
        }
        catch (WeatherDataException ex)
        {
            await MarkLoadRunFailedAsync(loadRun.Id, ex.Message, null, null, request.Country, cancellationToken);
            throw;
        }
    }

    private async Task MarkLoadRunFailedAsync(
        int loadRunId,
        string runMessage,
        string? errorMessage,
        string? errorDetails,
        string country,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();

        var storedRun = await db.LoadRuns
            .FirstOrDefaultAsync(run => run.Id == loadRunId, cancellationToken);

        if (storedRun is not null)
        {
            storedRun.Status = "failed";
            storedRun.FinishedAt = DateTimeOffset.UtcNow;
            storedRun.Message = runMessage;
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            await AddLoadErrorAsync(loadRunId, null, country, null, errorMessage, errorDetails, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<LocationEntity> GetOrCreateLocationAsync(
        string city,
        string country,
        int loadRunId,
        TextWriter log,
        CancellationToken cancellationToken)
    {
        var cleanCity = city.Trim();
        var cleanCountry = country.Trim().ToUpperInvariant();
        var location = await db.Locations
            .FirstOrDefaultAsync(item =>
                item.Country == cleanCountry
                && item.Name.ToLower() == cleanCity.ToLower(), cancellationToken);

        if (location is not null)
        {
            await log.WriteLineAsync($"Город {location.Name}: координаты взяты из БД.");
            return location;
        }

        await log.WriteLineAsync($"Город {cleanCity}: поиск через Open-Meteo Geocoding API.");
        FoundLocation? lookup;
        try
        {
            lookup = await openMeteo.FindLocationAsync(cleanCity, cleanCountry, cancellationToken);
        }
        catch (WeatherDataException ex)
        {
            await AddLoadErrorAsync(loadRunId, cleanCity, cleanCountry, null, "Ошибка поиска города через Open-Meteo", ex.Message, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }

        if (lookup is null)
        {
            var message = $"Город {cleanCity} не найден в стране {cleanCountry}. Проверьте название города и параметр --country.";
            await AddLoadErrorAsync(loadRunId, cleanCity, cleanCountry, null, message, null, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            throw new WeatherDataException(message);
        }

        location = await FindExistingLocationAsync(lookup, cancellationToken);
        if (location is not null)
        {
            await log.WriteLineAsync($"Город {location.Name}: координаты взяты из БД.");
            return location;
        }

        location = new LocationEntity
        {
            Name = lookup.Name,
            Country = lookup.Country.ToUpperInvariant(),
            Latitude = lookup.Latitude,
            Longitude = lookup.Longitude,
            Timezone = lookup.Timezone,
            Source = WeatherSources.OpenMeteo,
            SourceId = lookup.SourceId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        await log.WriteLineAsync($"Город {location.Name}: найден и сохранен в БД.");
        return location;
    }

    private async Task<LocationEntity?> FindExistingLocationAsync(FoundLocation lookup, CancellationToken cancellationToken)
    {
        var country = lookup.Country.Trim().ToUpperInvariant();
        var sourceId = lookup.SourceId.Trim();

        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var locationBySourceId = await db.Locations
                .FirstOrDefaultAsync(item =>
                    item.Country == country
                    && item.Source == WeatherSources.OpenMeteo
                    && item.SourceId == sourceId, cancellationToken);

            if (locationBySourceId is not null)
            {
                return locationBySourceId;
            }
        }

        return await db.Locations
            .FirstOrDefaultAsync(item =>
                item.Country == country
                && item.Name == lookup.Name
                && item.Latitude == lookup.Latitude
                && item.Longitude == lookup.Longitude, cancellationToken);
    }

    private async Task LoadMissingWeatherAsync(
        LocationEntity location,
        ReportRequest request,
        int loadRunId,
        TextWriter log,
        CancellationToken cancellationToken)
    {
        var existingDates = await db.WeatherDaily
            .Where(day => day.LocationId == location.Id && day.Date >= request.From && day.Date <= request.To)
            .Select(day => day.Date)
            .ToListAsync(cancellationToken);

        var existingDateSet = existingDates.ToHashSet();
        var missingDates = EachDate(request.From, request.To)
            .Where(date => !existingDateSet.Contains(date))
            .ToList();

        if (missingDates.Count == 0)
        {
            await log.WriteLineAsync($"{location.Name}: все дневные данные уже есть в БД, API не вызывается.");
            return;
        }

        var intervals = BuildIntervals(missingDates);
        await log.WriteLineAsync($"{location.Name}: нужно загрузить {missingDates.Count} дней, интервалов: {intervals.Count}.");

        foreach (var interval in intervals)
        {
            var locationInfo = new LocationInfo(
                location.Id,
                location.Name,
                location.Country,
                location.Latitude,
                location.Longitude,
                location.Timezone);

            IReadOnlyList<WeatherDay> downloadedDays;
            try
            {
                downloadedDays = await openMeteo.GetDailyWeatherAsync(
                    locationInfo,
                    interval.From,
                    interval.To,
                    cancellationToken);
            }
            catch (WeatherDataException ex)
            {
                var message = $"Не удалось загрузить данные для города {location.Name} за период {interval.From:yyyy-MM-dd} - {interval.To:yyyy-MM-dd}.";
                await AddLoadErrorAsync(loadRunId, location.Name, location.Country, interval.From, message, ex.Message, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                throw;
            }

            foreach (var day in downloadedDays)
            {
                if (existingDateSet.Contains(day.Date))
                {
                    continue;
                }

                var validationError = Validate(day, request.From, request.To);
                if (validationError is not null)
                {
                    await AddLoadErrorAsync(
                        loadRunId,
                        location.Name,
                        location.Country,
                        day.Date,
                        "Запись пропущена при проверке данных",
                        validationError,
                        cancellationToken);
                    continue;
                }

                db.WeatherDaily.Add(ToEntity(day));
                existingDateSet.Add(day.Date);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureCompleteCoverageAsync(
        IReadOnlyList<LocationEntity> selectedLocations,
        IReadOnlyList<WeatherDailyEntity> days,
        ReportRequest request,
        int loadRunId,
        CancellationToken cancellationToken)
    {
        var expectedDates = EachDate(request.From, request.To).ToHashSet();
        var errors = new List<string>();

        foreach (var location in selectedLocations)
        {
            var actualDates = days
                .Where(day => day.LocationId == location.Id)
                .Select(day => day.Date)
                .ToHashSet();

            var missingDates = expectedDates
                .Where(date => !actualDates.Contains(date))
                .OrderBy(date => date)
                .ToList();

            if (missingDates.Count == 0)
            {
                continue;
            }

            var sample = string.Join(", ", missingDates.Take(10).Select(date => $"{date:yyyy-MM-dd}"));
            var details = missingDates.Count > 10
                ? $"Первые отсутствующие даты: {sample}. Всего отсутствует: {missingDates.Count}."
                : $"Отсутствующие даты: {sample}.";
            var message = $"После загрузки для города {location.Name} отсутствуют дневные данные за {missingDates.Count} дат.";

            await AddLoadErrorAsync(loadRunId, location.Name, location.Country, missingDates[0], message, details, cancellationToken);
            errors.Add($"{location.Name}: {missingDates.Count} дат");
        }

        if (errors.Count == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        throw new WeatherDataException("Не удалось получить полный набор дневных данных: " + string.Join("; ", errors) + ".");
    }

    private static string? Validate(WeatherDay day, DateOnly from, DateOnly to)
    {
        if (day.Date < from || day.Date > to)
        {
            return $"Дата {day.Date:yyyy-MM-dd} выходит за запрошенный период.";
        }

        if (!day.AvgTempC.HasValue && !day.MinTempC.HasValue && !day.MaxTempC.HasValue)
        {
            return "Нет основных температурных показателей.";
        }

        var numericValues = new[]
        {
            day.AvgTempC,
            day.MinTempC,
            day.MaxTempC,
            day.AvgHumidity,
            day.AvgPressureHpa,
            day.AvgWindMs,
            day.MaxWindMs,
            day.RainMm,
            day.AvgClouds
        };

        if (numericValues.Any(value => value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value))))
        {
            return "В записи есть некорректное числовое значение.";
        }

        if (day.RainMm < 0)
        {
            return "Количество осадков не может быть отрицательным.";
        }

        if (day.AvgHumidity is < 0 or > 100)
        {
            return "Влажность должна быть в диапазоне 0-100.";
        }

        if (day.AvgClouds is < 0 or > 100)
        {
            return "Облачность должна быть в диапазоне 0-100.";
        }

        if (day.AvgWindMs < 0 || day.MaxWindMs < 0)
        {
            return "Скорость ветра не может быть отрицательной.";
        }

        if (day.AvgPressureHpa <= 0)
        {
            return "Атмосферное давление должно быть положительным.";
        }

        return null;
    }

    private async Task AddLoadErrorAsync(
        int? runId,
        string? city,
        string? country,
        DateOnly? date,
        string message,
        string? details,
        CancellationToken cancellationToken)
    {
        db.LoadErrors.Add(new LoadErrorEntity
        {
            RunId = runId,
            City = city,
            Country = country,
            Date = date,
            Message = message,
            Details = details,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await Task.CompletedTask;
    }

    private static WeatherDailyEntity ToEntity(WeatherDay day)
    {
        return new WeatherDailyEntity
        {
            LocationId = day.LocationId,
            Date = day.Date,
            AvgTempC = day.AvgTempC,
            MinTempC = day.MinTempC,
            MaxTempC = day.MaxTempC,
            AvgHumidity = day.AvgHumidity,
            AvgPressureHpa = day.AvgPressureHpa,
            AvgWindMs = day.AvgWindMs,
            MaxWindMs = day.MaxWindMs,
            RainMm = day.RainMm,
            AvgClouds = day.AvgClouds,
            WeatherCode = day.WeatherCode,
            Source = WeatherSources.OpenMeteo,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static WeatherDay MapToCore(WeatherDailyEntity entity)
    {
        var location = entity.Location ?? throw new WeatherDataException("Дневная запись прочитана без связанного города.");
        return new WeatherDay(
            entity.LocationId,
            location.Name,
            location.Country,
            entity.Date,
            entity.AvgTempC,
            entity.MinTempC,
            entity.MaxTempC,
            entity.AvgHumidity,
            entity.AvgPressureHpa,
            entity.AvgWindMs,
            entity.MaxWindMs,
            entity.RainMm,
            entity.AvgClouds,
            entity.WeatherCode);
    }

    private static IReadOnlyList<DateOnly> EachDate(DateOnly from, DateOnly to)
    {
        var dates = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        return dates;
    }

    private static IReadOnlyList<MissingInterval> BuildIntervals(IReadOnlyList<DateOnly> missingDates)
    {
        var intervals = new List<MissingInterval>();
        if (missingDates.Count == 0)
        {
            return intervals;
        }

        var ordered = missingDates.OrderBy(date => date).ToList();
        var start = ordered[0];
        var previous = ordered[0];

        for (var i = 1; i < ordered.Count; i++)
        {
            var current = ordered[i];
            if (current == previous.AddDays(1))
            {
                previous = current;
                continue;
            }

            intervals.Add(new MissingInterval(start, previous));
            start = current;
            previous = current;
        }

        intervals.Add(new MissingInterval(start, previous));
        return intervals;
    }

    private record MissingInterval(DateOnly From, DateOnly To);
}
