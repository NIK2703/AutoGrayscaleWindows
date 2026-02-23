using System.Text.RegularExpressions;
using Serilog;

namespace AutoGrayscaleWindows.Models;

/// <summary>
/// Тип сопоставления правила
/// </summary>
public enum MatchType
{
    /// <summary>
    /// Точное совпадение
    /// </summary>
    Exact = 0,

    /// <summary>
    /// Содержит подстроку
    /// </summary>
    Contains = 1,

    /// <summary>
    /// Регулярное выражение
    /// </summary>
    Regex = 2
}

/// <summary>
/// Тип цели для сопоставления
/// </summary>
public enum MatchTarget
{
    /// <summary>
    /// Сопоставление по исполняемому файлу (по умолчанию)
    /// </summary>
    Executable = 0,

    /// <summary>
    /// Сопоставление по заголовку окна
    /// </summary>
    WindowTitle = 1
}

/// <summary>
/// Действие правила
/// </summary>
public enum RuleAction
{
    /// <summary>
    /// Игнорировать (использовать действие по умолчанию)
    /// </summary>
    Ignore = 0,

    /// <summary>
    /// Включить grayscale
    /// </summary>
    EnableGrayscale = 1,

    /// <summary>
    /// Выключить grayscale
    /// </summary>
    DisableGrayscale = 2
}

/// <summary>
/// Правило переключения фильтра
/// </summary>
public class AppRule
{
    private Regex? _compiledRegex;

    /// <summary>
    /// Уникальный идентификатор правила
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Идентификатор приложения (имя процесса, путь или AppUserModelId)
    /// </summary>
    public string AppIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Отображаемое имя правила
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Действие при срабатывании правила
    /// </summary>
    public RuleAction Action { get; set; } = RuleAction.EnableGrayscale;

    /// <summary>
    /// Флаг активности правила
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Приоритет правила (чем выше, тем важнее)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Тип сопоставления (всегда Contains)
    /// </summary>
    public MatchType MatchType { get; set; } = MatchType.Contains;

    /// <summary>
    /// Тип цели для сопоставления: по исполняемому файлу или по заголовку окна
    /// </summary>
    public MatchTarget MatchTarget { get; set; } = MatchTarget.Executable;

    /// <summary>
    /// Паттерн для сопоставления заголовка окна (опционально)
    /// </summary>
    public string? WindowTitlePattern { get; set; }

    /// <summary>
    /// Использовать regex для заголовка окна
    /// </summary>
    public bool UseRegexForTitle { get; set; } = false;

    /// <summary>
    /// Описание правила
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Дата создания правила
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Дата последнего изменения
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Компилированное регулярное выражение для AppIdentifier
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Regex? CompiledRegex
    {
        get
        {
            if (MatchType == MatchType.Regex && _compiledRegex == null && !string.IsNullOrEmpty(AppIdentifier))
            {
                try
                {
                    _compiledRegex = new Regex(AppIdentifier, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Не удалось скомпилировать regex для правила {Name}", DisplayName);
                }
            }
            return _compiledRegex;
        }
    }

    /// <summary>
    /// Проверяет, соответствует ли окно этому правилу
    /// </summary>
    /// <param name="windowInfo">Информация об окне</param>
    /// <returns>true если окно соответствует правилу</returns>
    public bool Matches(WindowInfo windowInfo)
    {
        if (!IsActive || string.IsNullOrWhiteSpace(AppIdentifier))
            return false;

        // В зависимости от типа цели проверяем разные параметры
        if (MatchTarget == MatchTarget.WindowTitle)
        {
            // Сопоставление по заголовку окна
            return CheckWindowTitle(windowInfo.WindowTitle);
        }
        else
        {
            // Сопоставление по исполняемому файлу (по умолчанию)
            return CheckIdentifier(windowInfo);
        }
    }

    /// <summary>
    /// Проверяет идентификатор приложения
    /// </summary>
    private bool CheckIdentifier(WindowInfo windowInfo)
    {
        // Получаем все возможные идентификаторы для проверки
        var identifiers = new[]
        {
            windowInfo.ProcessName,
            windowInfo.ExecutablePath,
            windowInfo.AppUserModelId,
            windowInfo.AppId
        }.Where(id => !string.IsNullOrEmpty(id));

        foreach (var identifier in identifiers)
        {
            if (MatchesByIdentifier(identifier!))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Проверяет соответствие идентификатора правилу
    /// </summary>
    private bool MatchesByIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return false;

        try
        {
            return MatchType switch
            {
                MatchType.Exact => string.Equals(identifier, AppIdentifier, StringComparison.OrdinalIgnoreCase),
                // Проверяем в обоих направлениях: либо identifier содержит AppIdentifier, либо наоборот
                MatchType.Contains => identifier.Contains(AppIdentifier, StringComparison.OrdinalIgnoreCase) ||
                                      AppIdentifier.Contains(identifier, StringComparison.OrdinalIgnoreCase),
                MatchType.Regex => CompiledRegex?.IsMatch(identifier) ?? false,
                _ => false
            };
        }
        catch (RegexMatchTimeoutException)
        {
            Log.Warning("Таймаут при сопоставлении regex для правила {Name}", DisplayName);
            return false;
        }
    }

    /// <summary>
    /// Проверяет заголовок окна
    /// </summary>
    private bool CheckWindowTitle(string windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
        {
            Log.Debug("CheckWindowTitle: заголовок окна пуст, правило {Name} не сработает", DisplayName);
            return false;
        }

        // Используем AppIdentifier как паттерн для заголовка
        // MatchType всегда Contains
        bool matches = windowTitle.Contains(AppIdentifier, StringComparison.OrdinalIgnoreCase);
        Log.Debug("CheckWindowTitle: заголовок '{Title}' {Action} паттерн '{Pattern}' для правила {Name}",
            windowTitle, matches ? "содержит" : "не содержит", AppIdentifier, DisplayName);
        return matches;
    }

    /// <summary>
    /// Проверяет валидность правила
    /// </summary>
    /// <param name="errorMessage">Сообщение об ошибке</param>
    /// <returns>true если правило валидно</returns>
    public bool Validate(out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(AppIdentifier))
        {
            errorMessage = "Идентификатор приложения не может быть пустым";
            return false;
        }

        if (MatchType == MatchType.Regex)
        {
            try
            {
                _ = new Regex(AppIdentifier, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                errorMessage = $"Некорректное регулярное выражение: {ex.Message}";
                return false;
            }
        }

        if (!string.IsNullOrEmpty(WindowTitlePattern) && UseRegexForTitle)
        {
            try
            {
                _ = new Regex(WindowTitlePattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                errorMessage = $"Некорректное регулярное выражение для заголовка: {ex.Message}";
                return false;
            }
        }

        if (Priority < 0)
        {
            errorMessage = "Приоритет не может быть отрицательным";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Создаёт копию правила
    /// </summary>
    public AppRule Clone()
    {
        return new AppRule
        {
            Id = Id,
            AppIdentifier = AppIdentifier,
            DisplayName = DisplayName,
            Action = Action,
            IsActive = IsActive,
            Priority = Priority,
            MatchType = MatchType,
            MatchTarget = MatchTarget,
            WindowTitlePattern = WindowTitlePattern,
            UseRegexForTitle = UseRegexForTitle,
            Description = Description,
            CreatedAt = CreatedAt,
            UpdatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// Возвращает строковое представление правила
    /// </summary>
    public override string ToString()
    {
        return $"[{Priority}] {DisplayName} ({AppIdentifier}) -> {Action}";
    }
}
