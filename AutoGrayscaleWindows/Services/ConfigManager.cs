using System.Text.Json;
using System.Text.Json.Serialization;
using AutoGrayscaleWindows.Models;
using Serilog;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOFileSystemWatcher = System.IO.FileSystemWatcher;
using IONotifyFilters = System.IO.NotifyFilters;
using IOFileSystemEventArgs = System.IO.FileSystemEventArgs;

namespace AutoGrayscaleWindows.Services;

/// <summary>
/// Аргументы события ошибки конфигурации
/// </summary>
public class ConfigErrorEventArgs : EventArgs
{
    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Исключение
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Менеджер конфигурации приложения
/// </summary>
public class ConfigManager : IDisposable
{
    private static readonly string ConfigDirectory = IOPath.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoGrayscaleWindows");

    private static readonly string ConfigFilePath = IOPath.Combine(ConfigDirectory, "config.json");
    private static readonly string BackupFilePath = IOPath.Combine(ConfigDirectory, "config.json.bak");

    private AppConfig _config;
    private IOFileSystemWatcher? _configWatcher;
    private bool _disposed;
    private readonly object _fileLock = new();

    /// <summary>
    /// Настройки JSON-сериализации
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Текущая конфигурация
    /// </summary>
    public AppConfig Config => _config;

    /// <summary>
    /// Путь к файлу конфигурации
    /// </summary>
    public string ConfigPath => ConfigFilePath;

    /// <summary>
    /// Загружена ли конфигурация
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Событие изменения конфигурации
    /// </summary>
    public event EventHandler<AppConfig>? ConfigChanged;

    /// <summary>
    /// Событие ошибки конфигурации
    /// </summary>
    public event EventHandler<ConfigErrorEventArgs>? ConfigError;

    /// <summary>
    /// Событие загрузки конфигурации
    /// </summary>
    public event EventHandler<AppConfig>? ConfigLoaded;

    /// <summary>
    /// Событие сохранения конфигурации
    /// </summary>
    public event EventHandler<AppConfig>? ConfigSaved;

    /// <summary>
    /// Создаёт экземпляр менеджера конфигурации
    /// </summary>
    public ConfigManager()
    {
        _config = AppConfig.CreateDefault();
    }

    /// <summary>
    /// Загружает конфигурацию из файла
    /// </summary>
    public AppConfig Load()
    {
        lock (_fileLock)
        {
            try
            {
                // Проверяем portable-режим (config.json рядом с exe)
                string? portablePath = GetPortableConfigPath();
                string configPath = ConfigFilePath;

                if (portablePath != null && IOFile.Exists(portablePath))
                {
                    configPath = portablePath;
                    Log.Information("Используется portable-конфигурация: {Path}", configPath);
                }

                if (!IOFile.Exists(configPath))
                {
                    Log.Information("Файл конфигурации не найден, создаём конфигурацию по умолчанию");
                    _config = AppConfig.CreateDefault();
                    Save();
                    IsLoaded = true;
                    return _config;
                }

                string json = IOFile.ReadAllText(configPath);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Warning("Файл конфигурации пуст, создаём конфигурацию по умолчанию");
                    _config = AppConfig.CreateDefault();
                    Save();
                    IsLoaded = true;
                    return _config;
                }

                _config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? AppConfig.CreateDefault();

                // Миграция версии
                MigrateConfig(_config);

                // Валидация загруженной конфигурации
                ValidateConfig(_config);

                IsLoaded = true;
                Log.Information("Конфигурация загружена: {BlacklistCount} blacklist правил, {WhitelistCount} whitelist правил, версия {Version}",
                    _config.BlacklistRules.Count, _config.WhitelistRules.Count, _config.Version);

                ConfigLoaded?.Invoke(this, _config);
                return _config;
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Ошибка формата JSON в файле конфигурации");
                OnConfigError("Ошибка формата конфигурации", ex);

                // Пытаемся восстановить из backup
                if (TryRestoreFromBackup())
                {
                    return _config;
                }

                _config = AppConfig.CreateDefault();
                Save();
                IsLoaded = true;
                return _config;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при загрузке конфигурации, используем значения по умолчанию");
                OnConfigError("Ошибка загрузки конфигурации", ex);
                _config = AppConfig.CreateDefault();
                IsLoaded = true;
                return _config;
            }
        }
    }

    /// <summary>
    /// Получает путь к portable-конфигурации
    /// </summary>
    private string? GetPortableConfigPath()
    {
        try
        {
            string? exeDir = IOPath.GetDirectoryName(Environment.ProcessPath);
            if (exeDir == null)
                return null;

            string portableConfig = IOPath.Combine(exeDir, "config.json");
            return IOFile.Exists(portableConfig) ? portableConfig : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Мигрирует конфигурацию на новую версию
    /// </summary>
    private void MigrateConfig(AppConfig config)
    {
        if (string.IsNullOrEmpty(config.Version))
        {
            config.Version = "1.0";
        }

        // Пример миграции с версии 0.9 на 1.0
        if (config.Version == "0.9")
        {
            Log.Information("Миграция конфигурации с версии 0.9 на 1.0");
            // Добавляем новые поля, если нужно
            config.Version = "1.0";
        }

        // Добавить другие миграции по мере необходимости
    }

    /// <summary>
    /// Сохраняет конфигурацию в файл
    /// </summary>
    public bool Save()
    {
        lock (_fileLock)
        {
            try
            {
                // Создаём директорию, если не существует
                if (!IODirectory.Exists(ConfigDirectory))
                {
                    IODirectory.CreateDirectory(ConfigDirectory);
                }

                // Создаём backup существующего файла
                CreateBackup();

                // Обновляем метаданные
                _config.ModifiedAt = DateTime.Now;

                string json = JsonSerializer.Serialize(_config, JsonOptions);
                IOFile.WriteAllText(ConfigFilePath, json);

                Log.Information("Конфигурация сохранена в {Path}", ConfigFilePath);
                ConfigSaved?.Invoke(this, _config);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при сохранении конфигурации");
                OnConfigError("Ошибка сохранения конфигурации", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Создаёт резервную копию конфигурации
    /// </summary>
    private void CreateBackup()
    {
        try
        {
            if (IOFile.Exists(ConfigFilePath))
            {
                IOFile.Copy(ConfigFilePath, BackupFilePath, overwrite: true);
                Log.Debug("Создана резервная копия конфигурации");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось создать резервную копию конфигурации");
        }
    }

    /// <summary>
    /// Пытается восстановить конфигурацию из backup
    /// </summary>
    private bool TryRestoreFromBackup()
    {
        try
        {
            if (!IOFile.Exists(BackupFilePath))
            {
                Log.Warning("Резервная копия не найдена");
                return false;
            }

            string json = IOFile.ReadAllText(BackupFilePath);
            _config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? AppConfig.CreateDefault();
            
            Log.Information("Конфигурация восстановлена из резервной копии");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось восстановить конфигурацию из резервной копии");
            return false;
        }
    }

    /// <summary>
    /// Обновляет конфигурацию
    /// </summary>
    public void UpdateConfig(AppConfig newConfig)
    {
        if (newConfig == null)
            throw new ArgumentNullException(nameof(newConfig));

        _config = newConfig;
        Save();
        ConfigChanged?.Invoke(this, _config);
    }

    /// <summary>
    /// Валидирует конфигурацию
    /// </summary>
    private void ValidateConfig(AppConfig config)
    {
        // Валидируем оба списка правил
        ValidateRulesList(config.BlacklistRules, "Blacklist");
        ValidateRulesList(config.WhitelistRules, "Whitelist");

        // Вызываем валидацию конфигурации
        config.Validate();
    }

    /// <summary>
    /// Валидирует список правил
    /// </summary>
    private void ValidateRulesList(List<AppRule> rules, string listName)
    {
        // Удаляем дубликаты правил
        var duplicateIds = rules
            .GroupBy(r => r.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Any())
        {
            Log.Warning("Обнаружены дубликаты ID правил в {ListName}: {Ids}", listName, string.Join(", ", duplicateIds));
            var uniqueRules = rules
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .ToList();
            rules.Clear();
            rules.AddRange(uniqueRules);
        }

        // Проверяем правила с некорректными regex
        foreach (var rule in rules.Where(r => r.MatchType == MatchType.Regex))
        {
            if (!rule.Validate(out string? error))
            {
                Log.Warning("Правило {Name} в {ListName} отключено из-за ошибки: {Error}", rule.DisplayName, listName, error);
                rule.IsActive = false;
            }
        }
    }

    /// <summary>
    /// Запускает отслеживание изменений файла конфигурации
    /// </summary>
    public void StartWatching()
    {
        if (_configWatcher != null)
            return;

        try
        {
            if (!IODirectory.Exists(ConfigDirectory))
                IODirectory.CreateDirectory(ConfigDirectory);

            _configWatcher = new IOFileSystemWatcher(ConfigDirectory)
            {
                Filter = "config.json",
                NotifyFilter = IONotifyFilters.LastWrite | IONotifyFilters.Size | IONotifyFilters.FileName
            };

            _configWatcher.Changed += OnConfigFileChanged;
            _configWatcher.Created += OnConfigFileChanged;
            _configWatcher.EnableRaisingEvents = true;

            Log.Information("Отслеживание изменений конфигурации запущено");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при запуске отслеживания конфигурации");
        }
    }

    /// <summary>
    /// Останавливает отслеживание изменений файла конфигурации
    /// </summary>
    public void StopWatching()
    {
        if (_configWatcher == null)
            return;

        _configWatcher.EnableRaisingEvents = false;
        _configWatcher.Changed -= OnConfigFileChanged;
        _configWatcher.Created -= OnConfigFileChanged;
        _configWatcher.Dispose();
        _configWatcher = null;

        Log.Information("Отслеживание изменений конфигурации остановлено");
    }

    private void OnConfigFileChanged(object? sender, IOFileSystemEventArgs e)
    {
        // Небольшая задержка для завершения записи файла
        Thread.Sleep(100);

        Log.Information("Обнаружено изменение файла конфигурации");
        Load();
        ConfigChanged?.Invoke(this, _config);
    }

    /// <summary>
    /// Генерирует событие ошибки конфигурации
    /// </summary>
    private void OnConfigError(string message, Exception? exception)
    {
        ConfigError?.Invoke(this, new ConfigErrorEventArgs
        {
            Message = message,
            Exception = exception
        });
    }

    /// <summary>
    /// Экспортирует конфигурацию в указанный файл
    /// </summary>
    public bool ExportConfig(string filePath)
    {
        try
        {
            string json = JsonSerializer.Serialize(_config, JsonOptions);
            IOFile.WriteAllText(filePath, json);
            Log.Information("Конфигурация экспортирована в {Path}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при экспорте конфигурации");
            return false;
        }
    }

    /// <summary>
    /// Импортирует конфигурацию из указанного файла
    /// </summary>
    public bool ImportConfig(string filePath)
    {
        try
        {
            string json = IOFile.ReadAllText(filePath);
            var importedConfig = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            
            if (importedConfig == null)
            {
                Log.Error("Не удалось импортировать конфигурацию: файл пуст или некорректен");
                return false;
            }

            MigrateConfig(importedConfig);
            ValidateConfig(importedConfig);

            _config = importedConfig;
            Save();
            ConfigChanged?.Invoke(this, _config);

            Log.Information("Конфигурация импортирована из {Path}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при импорте конфигурации");
            return false;
        }
    }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        StopWatching();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~ConfigManager()
    {
        Dispose();
    }
}
