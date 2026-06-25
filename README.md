# Weather Analytics System

Консольная C#/.NET 9 система для загрузки исторических погодных данных из Open-Meteo, хранения в PostgreSQL, аналитики, поиска аномалий, простого прогноза и создания PDF-отчета.

## Запуск PostgreSQL

```bash
docker compose up -d
```

По умолчанию приложение использует строку подключения:

```text
Host=localhost;Port=5432;Database=weather_analytics;Username=weather;Password=weather
```

Если нужна другая БД, задайте переменную окружения `WEATHER_DB_CONNECTION`.
Также можно переопределить отдельные параметры: `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`.

## Основной запуск

```bash
dotnet run --project src/WeatherAnalytics.Cli -- \
  --cities Moscow,Kazan,Saint-Petersburg \
  --country RU \
  --from 2021-01-01 \
  --to 2025-12-31 \
  --output reports/weather-report.pdf
```

Дополнительный параметр:

```bash
--rain-threshold 1.0
```

Он влияет только на аналитический показатель "дней с осадками выше порога".

Open-Meteo endpoints при необходимости переопределяются через:

```text
OPEN_METEO_GEOCODING_BASE_URL
OPEN_METEO_HISTORICAL_BASE_URL
```

## Что делает приложение

1. Принимает параметры командной строки.
2. Создает схему PostgreSQL, если ее еще нет.
3. Ищет города в таблице `locations` или через Open-Meteo Geocoding API.
4. Проверяет таблицу `weather_daily` и загружает только недостающие даты.
5. Сохраняет данные без дублей по `location_id + date`.
6. Выполняет аналитику, поиск аномалий и прогноз на 7 дней.
7. Создает PDF-отчет с таблицами, графиками, аномалиями, прогнозом и итоговым выводом.
8. Сохраняет результаты аналитики в `analysis_results`, `anomalies` и `forecasts`.

## Проверка

```bash
dotnet build
dotnet test
```
