using AutoGrayscaleWindows.Native;
using Microsoft.Win32;
using Serilog;

namespace AutoGrayscaleWindows.Services;

/// <summary>
/// Менеджер автозапуска приложения с Windows
/// </summary>
public class StartupManager
{
    private const string AppName = "AutoGrayscaleWindows";

    /// <summary>
    /// Включает автозапуск приложения
    /// </summary>
    /// <returns>true если автозапуск успешно включён</returns>
    public bool EnableAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Constants.AUTOSTART_REGISTRY_PATH, true);
            if (key == null)
            {
                Log.Error("Не удалось открыть ключ автозапуска: {Path}", Constants.AUTOSTART_REGISTRY_PATH);
                return false;
            }

            string? exePath = GetExecutablePath();
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Error("Не удалось определить путь к исполняемому файлу");
                return false;
            }

            // Добавляем --minimized для запуска в свёрнутом виде
            string value = $"\"{exePath}\" --minimized";
            key.SetValue(AppName, value);

            Log.Information("Автозапуск включён: {Value}", value);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Error(ex, "Нет прав на изменение ключа автозапуска");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при включении автозапуска");
            return false;
        }
    }

    /// <summary>
    /// Выключает автозапуск приложения
    /// </summary>
    /// <returns>true если автозапуск успешно выключен</returns>
    public bool DisableAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Constants.AUTOSTART_REGISTRY_PATH, true);
            if (key == null)
            {
                Log.Error("Не удалось открыть ключ автозапуска: {Path}", Constants.AUTOSTART_REGISTRY_PATH);
                return false;
            }

            // Проверяем, есть ли запись
            var existingValue = key.GetValue(AppName);
            if (existingValue == null)
            {
                Log.Debug("Автозапуск уже выключен");
                return true;
            }

            key.DeleteValue(AppName, false);

            Log.Information("Автозапуск выключен");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Error(ex, "Нет прав на изменение ключа автозапуска");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выключении автозапуска");
            return false;
        }
    }

    /// <summary>
    /// Проверяет, включён ли автозапуск
    /// </summary>
    /// <returns>true если автозапуск включён</returns>
    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Constants.AUTOSTART_REGISTRY_PATH);
            if (key == null)
                return false;

            var value = key.GetValue(AppName);
            return value != null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при проверке автозапуска");
            return false;
        }
    }

    /// <summary>
    /// Устанавливает состояние автозапуска
    /// </summary>
    /// <param name="enable">true чтобы включить, false чтобы выключить</param>
    /// <returns>true если операция успешна</returns>
    public bool SetAutoStart(bool enable)
    {
        return enable ? EnableAutoStart() : DisableAutoStart();
    }

    /// <summary>
    /// Получает путь к исполняемому файлу
    /// </summary>
    private static string? GetExecutablePath()
    {
        try
        {
            // .NET 6+ способ
            return Environment.ProcessPath;
        }
        catch
        {
            // Fallback
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
