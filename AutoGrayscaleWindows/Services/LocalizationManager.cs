using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using Serilog;

namespace AutoGrayscaleWindows.Services;

/// <summary>
/// Менеджер локализации приложения
/// </summary>
public class LocalizationManager : INotifyPropertyChanged
{
    private static LocalizationManager? _instance;
    private CultureInfo _currentCulture;
    private readonly Dictionary<string, ResourceDictionary> _loadedDictionaries = new();

    /// <summary>
    /// Экземпляр синглтона
    /// </summary>
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    /// <summary>
    /// Текущая культура
    /// </summary>
    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture.Name != value.Name)
            {
                _currentCulture = value;
                OnPropertyChanged(nameof(CurrentCulture));
                OnPropertyChanged(nameof(CurrentLanguageCode));
                ApplyLanguage(value.TwoLetterISOLanguageName);
            }
        }
    }

    /// <summary>
    /// Код текущего языка (ru, en)
    /// </summary>
    public string CurrentLanguageCode => _currentCulture.TwoLetterISOLanguageName;

    /// <summary>
    /// Доступные языки
    /// </summary>
    public ObservableCollection<LanguageInfo> AvailableLanguages { get; } = new()
    {
        new LanguageInfo { Code = "ru", DisplayName = "Русский", CultureName = "ru-RU" },
        new LanguageInfo { Code = "en", DisplayName = "English", CultureName = "en-US" },
        new LanguageInfo { Code = "zh", DisplayName = "中文", CultureName = "zh-CN" },
        new LanguageInfo { Code = "es", DisplayName = "Español", CultureName = "es-ES" },
        new LanguageInfo { Code = "fr", DisplayName = "Français", CultureName = "fr-FR" },
        new LanguageInfo { Code = "de", DisplayName = "Deutsch", CultureName = "de-DE" },
        new LanguageInfo { Code = "ja", DisplayName = "日本語", CultureName = "ja-JP" }
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    
    /// <summary>
    /// Событие при смене языка
    /// </summary>
    public event EventHandler? LanguageChanged;

    private LocalizationManager()
    {
        // По умолчанию используем язык системы, если он поддерживается, иначе русский
        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var supportedLanguage = AvailableLanguages.Any(l => l.Code == systemLanguage) ? systemLanguage : "ru";
        _currentCulture = new CultureInfo(supportedLanguage);
    }

    /// <summary>
    /// Инициализирует менеджер локализации
    /// </summary>
    public void Initialize()
    {
        ApplyLanguage(_currentCulture.TwoLetterISOLanguageName);
        Log.Information("Локализация инициализирована: {Language}", _currentCulture.Name);
    }

    /// <summary>
    /// Применяет указанный язык
    /// </summary>
    /// <param name="languageCode">Код языка (ru, en)</param>
    public void ApplyLanguage(string languageCode)
    {
        try
        {
            var dictionaryPath = $"/AutoGrayscaleWindows;component/UI/Resources/Languages/StringResources.{languageCode}.xaml";
            
            // Удаляем старые словари локализации
            var oldDictionaries = Application.Current.Resources.MergedDictionaries
                .Where(d => d.Source != null && d.Source.ToString().Contains("StringResources."))
                .ToList();

            foreach (var oldDict in oldDictionaries)
            {
                Application.Current.Resources.MergedDictionaries.Remove(oldDict);
            }

            // Загружаем новый словарь
            var newDict = new ResourceDictionary
            {
                Source = new Uri(dictionaryPath, UriKind.Relative)
            };

            Application.Current.Resources.MergedDictionaries.Add(newDict);
            
            // Обновляем культуру
            CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;
            CultureInfo.DefaultThreadCurrentCulture = _currentCulture;
            
            LanguageChanged?.Invoke(this, EventArgs.Empty);
            
            Log.Debug("Язык изменён на: {Language}", languageCode);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при смене языка на {Language}", languageCode);
        }
    }

    /// <summary>
    /// Устанавливает язык по коду
    /// </summary>
    /// <param name="languageCode">Код языка (ru, en, zh, es, fr, de, ja)</param>
    public void SetLanguage(string languageCode)
    {
        var culture = languageCode switch
        {
            "en" => new CultureInfo("en"),
            "ru" => new CultureInfo("ru"),
            "zh" => new CultureInfo("zh"),
            "es" => new CultureInfo("es"),
            "fr" => new CultureInfo("fr"),
            "de" => new CultureInfo("de"),
            "ja" => new CultureInfo("ja"),
            _ => new CultureInfo("ru")
        };
        CurrentCulture = culture;
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Получает локализованную строку по ключу
    /// </summary>
    /// <param name="key">Ключ ресурса</param>
    /// <returns>Локализованная строка или ключ, если ресурс не найден</returns>
    public static string GetString(string key)
    {
        if (Application.Current.Resources.Contains(key))
        {
            return Application.Current.Resources[key] as string ?? key;
        }
        return key;
    }
}

/// <summary>
/// Информация о языке
/// </summary>
public class LanguageInfo
{
    /// <summary>
    /// Код языка (ru, en)
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Отображаемое название
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Название культуры
    /// </summary>
    public string CultureName { get; set; } = string.Empty;
}
