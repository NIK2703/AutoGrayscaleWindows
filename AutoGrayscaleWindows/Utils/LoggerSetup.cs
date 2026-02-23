using System.IO;
using Serilog;
using Serilog.Events;

namespace AutoGrayscaleWindows.Utils;

/// <summary>
/// Настройка системы логирования Serilog
/// </summary>
public static class LoggerSetup
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoGrayscaleWindows",
        "logs");

    private static readonly string LogFilePath = Path.Combine(LogDirectory, "app-.json");

    /// <summary>
    /// Инициализирует и настраивает глобальный логгер
    /// </summary>
    /// <param name="minimumLevel">Минимальный уровень логирования</param>
    public static void Initialize(LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        // Создаём директорию для логов, если не существует
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithProperty("Application", "AutoGrayscaleWindows")
            .Enrich.WithProperty("Version", GetApplicationVersion());

        // В Debug конфигурации добавляем консольный вывод
#if DEBUG
        loggerConfiguration.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}");
#endif

        // Файловый вывод в формате JSON с rolling
        loggerConfiguration.WriteTo.File(
            path: LogFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [Thread:{ThreadId}] [Process:{ProcessId}] {Message:lj}{NewLine}{Exception}",
            restrictedToMinimumLevel: LogEventLevel.Debug);

        Log.Logger = loggerConfiguration.CreateLogger();

        Log.Information("=== Auto Grayscale Windows запущен ===");
        Log.Information("Версия: {Version}", GetApplicationVersion());
        Log.Information("Директория логов: {LogDirectory}", LogDirectory);
        Log.Information("Уровень логирования: {LogLevel}", minimumLevel);
    }

    /// <summary>
    /// Закрывает и освобождает ресурсы логгера
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.Information("=== Auto Grayscale Windows завершает работу ===");
        Log.CloseAndFlush();
    }

    /// <summary>
    /// Получает версию приложения
    /// </summary>
    private static string GetApplicationVersion()
    {
        try
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Парсит строковое представление уровня логирования
    /// </summary>
    public static LogEventLevel ParseLogLevel(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
