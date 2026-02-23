using System.Text.Json.Serialization;

namespace AutoGrayscaleWindows.Models;

/// <summary>
/// Конфигурация горячих клавиш
/// </summary>
public class HotkeyConfig
{
    /// <summary>
    /// Клавиша-модификатор (Alt, Ctrl, Shift, Win)
    /// </summary>
    public uint Modifier { get; set; }

    /// <summary>
    /// Виртуальный код клавиши
    /// </summary>
    public uint VirtualKey { get; set; }

    /// <summary>
    /// Строковое представление хоткея
    /// </summary>
    public string DisplayString { get; set; } = string.Empty;

    /// <summary>
    /// Включены ли горячие клавиши
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Проверяет валидность конфигурации хоткея
    /// </summary>
    public bool IsValid()
    {
        return VirtualKey > 0;
    }

    /// <summary>
    /// Создаёт конфигурацию по умолчанию для переключения grayscale
    /// </summary>
    public static HotkeyConfig DefaultToggleGrayscale()
    {
        return new HotkeyConfig
        {
            Modifier = 0x0001 | 0x0002, // Alt + Ctrl
            VirtualKey = 0x47, // G
            DisplayString = "Ctrl+Alt+G",
            IsEnabled = true
        };
    }

    /// <summary>
    /// Создаёт конфигурацию по умолчанию для паузы
    /// </summary>
    public static HotkeyConfig DefaultPause()
    {
        return new HotkeyConfig
        {
            Modifier = 0x0001 | 0x0002, // Alt + Ctrl
            VirtualKey = 0x50, // P
            DisplayString = "Ctrl+Alt+P",
            IsEnabled = true
        };
    }
}

/// <summary>
/// Глобальная конфигурация приложения
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Версия формата конфигурации
    /// </summary>
    public string Version { get; set; } = "2.0";

    /// <summary>
    /// Глобальный флаг включения/выключения
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Использовать белый список (true) или чёрный список (false)
    /// Белый список: приложения в списке становятся чёрно-белыми, остальные цветные
    /// Чёрный список: приложения в списке становятся цветными, остальные чёрно-белые
    /// </summary>
    public bool UseWhitelist { get; set; } = false;

    /// <summary>
    /// Список правил для чёрного списка (приложения, которые должны быть цветными)
    /// </summary>
    public List<AppRule> BlacklistRules { get; set; } = new();

    /// <summary>
    /// Список правил для белого списка (приложения, которые должны быть чёрно-белыми)
    /// </summary>
    public List<AppRule> WhitelistRules { get; set; } = new();

    /// <summary>
    /// Автозапуск при входе в Windows
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Сворачивать в трей при закрытии
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Конфигурация хоткея для переключения grayscale
    /// </summary>
    public HotkeyConfig ToggleGrayscaleHotkey { get; set; } = HotkeyConfig.DefaultToggleGrayscale();

    /// <summary>
    /// Конфигурация хоткея для временного приостановления
    /// </summary>
    public HotkeyConfig PauseHotkey { get; set; } = HotkeyConfig.DefaultPause();

    /// <summary>
    /// Минимальный уровень логирования
    /// </summary>
    public string MinimumLogLevel { get; set; } = "Information";

    /// <summary>
    /// Язык интерфейса (ru, en)
    /// </summary>
    public string Language { get; set; } = "ru";

    /// <summary>
    /// Дата создания конфигурации
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Дата последнего изменения
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Получает текущий активный список правил в зависимости от режима
    /// </summary>
    public List<AppRule> GetCurrentRules()
    {
        return UseWhitelist ? WhitelistRules : BlacklistRules;
    }

    /// <summary>
    /// Проверяет валидность конфигурации
    /// </summary>
    public bool Validate()
    {
        bool isValid = true;

        // Проверяем правила
        foreach (var rule in BlacklistRules)
        {
            if (!rule.Validate(out _))
            {
                rule.IsActive = false;
                isValid = false;
            }
        }

        foreach (var rule in WhitelistRules)
        {
            if (!rule.Validate(out _))
            {
                rule.IsActive = false;
                isValid = false;
            }
        }

        // Проверяем хоткеи
        if (!ToggleGrayscaleHotkey.IsValid())
        {
            ToggleGrayscaleHotkey = HotkeyConfig.DefaultToggleGrayscale();
            isValid = false;
        }

        if (!PauseHotkey.IsValid())
        {
            PauseHotkey = HotkeyConfig.DefaultPause();
            isValid = false;
        }

        return isValid;
    }

    /// <summary>
    /// Получает все активные правила текущего режима
    /// </summary>
    public IEnumerable<AppRule> GetActiveRules()
    {
        return GetCurrentRules()
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.DisplayName);
    }

    /// <summary>
    /// Создаёт конфигурацию по умолчанию
    /// </summary>
    public static AppConfig CreateDefault()
    {
        var config = new AppConfig
        {
            Version = "2.0",
            IsEnabled = true,
            UseWhitelist = false,
            BlacklistRules = new List<AppRule>(),
            WhitelistRules = new List<AppRule>(),
            AutoStart = true,
            MinimizeToTray = true,
            MinimumLogLevel = "Information",
            Language = "ru",
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };

        return config;
    }

    /// <summary>
    /// Создаёт глубокую копию конфигурации
    /// </summary>
    public AppConfig Clone()
    {
        var clone = new AppConfig
        {
            Version = Version,
            IsEnabled = IsEnabled,
            UseWhitelist = UseWhitelist,
            BlacklistRules = BlacklistRules.Select(r => r.Clone()).ToList(),
            WhitelistRules = WhitelistRules.Select(r => r.Clone()).ToList(),
            AutoStart = AutoStart,
            MinimizeToTray = MinimizeToTray,
            ToggleGrayscaleHotkey = new HotkeyConfig
            {
                Modifier = ToggleGrayscaleHotkey.Modifier,
                VirtualKey = ToggleGrayscaleHotkey.VirtualKey,
                DisplayString = ToggleGrayscaleHotkey.DisplayString,
                IsEnabled = ToggleGrayscaleHotkey.IsEnabled
            },
            PauseHotkey = new HotkeyConfig
            {
                Modifier = PauseHotkey.Modifier,
                VirtualKey = PauseHotkey.VirtualKey,
                DisplayString = PauseHotkey.DisplayString,
                IsEnabled = PauseHotkey.IsEnabled
            },
            MinimumLogLevel = MinimumLogLevel,
            Language = Language,
            CreatedAt = CreatedAt,
            ModifiedAt = DateTime.Now
        };

        return clone;
    }
}
