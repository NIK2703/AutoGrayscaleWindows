namespace AutoGrayscaleWindows.Native;

/// <summary>
/// Константы Windows API
/// </summary>
public static class Constants
{
    #region WinEvent Constants

    /// <summary>
    /// Событие смены активного окна
    /// </summary>
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    /// <summary>
    /// Событие изменения имени объекта (заголовка окна)
    /// </summary>
    public const uint EVENT_OBJECT_NAMECHANGE = 0x800C;

    #endregion

    #region WinEvent Hook Flags

    /// <summary>
    /// Функция hook не находится в DLL
    /// </summary>
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    /// <summary>
    /// Hook для всех процессов
    /// </summary>
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    #endregion

    #region Window Messages

    /// <summary>
    /// Сообщение об изменении настроек системы
    /// </summary>
    public const int WM_SETTINGCHANGE = 0x001A;

    /// <summary>
    /// Сообщение о нажатии горячей клавиши
    /// </summary>
    public const int WM_HOTKEY = 0x0312;

    #endregion

    #region Hotkey Modifiers

    /// <summary>
    /// Модификатор Alt
    /// </summary>
    public const uint MOD_ALT = 0x0001;

    /// <summary>
    /// Модификатор Control
    /// </summary>
    public const uint MOD_CONTROL = 0x0002;

    /// <summary>
    /// Модификатор Shift
    /// </summary>
    public const uint MOD_SHIFT = 0x0004;

    /// <summary>
    /// Модификатор Windows
    /// </summary>
    public const uint MOD_WIN = 0x0008;

    #endregion

    #region Registry Paths

    /// <summary>
    /// Путь к настройкам цветового фильтра в реестре
    /// </summary>
    public const string COLOR_FILTER_REGISTRY_PATH = @"Software\Microsoft\ColorFiltering";

    /// <summary>
    /// Ключ для включения цветового фильтра
    /// </summary>
    public const string COLOR_FILTER_ACTIVE_KEY = "Active";

    /// <summary>
    /// Ключ для типа фильтра
    /// </summary>
    public const string COLOR_FILTER_TYPE_KEY = "FilterType";

    /// <summary>
    /// Путь к автозапуску в реестре
    /// </summary>
    public const string AUTOSTART_REGISTRY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Имя приложения в автозапуске
    /// </summary>
    public const string AUTOSTART_APP_NAME = "AutoGrayscaleWindows";

    #endregion

    #region Hotkey IDs

    /// <summary>
    /// ID хоткея переключения grayscale
    /// </summary>
    public const int HOTKEY_TOGGLE_GRAYSCALE = 9001;

    /// <summary>
    /// ID хоткея паузы
    /// </summary>
    public const int HOTKEY_PAUSE = 9002;

    #endregion

    #region SendMessageTimeout Constants

    /// <summary>
    /// Отправить сообщение всем окнам верхнего уровня
    /// </summary>
    public const nint HWND_BROADCAST = 0xFFFF;

    /// <summary>
    /// Прервать если окно зависло
    /// </summary>
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>
    /// Таймаут отправки сообщения (мс)
    /// </summary>
    public const uint SMTO_TIMEOUT = 5000;

    #endregion
}
