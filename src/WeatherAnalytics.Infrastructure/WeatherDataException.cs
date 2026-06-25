namespace WeatherAnalytics.Infrastructure;

public class WeatherDataException(string message, Exception? innerException = null) : Exception(message, innerException);
