namespace AutoGrayscaleWindows.Models;

/// <summary>
/// Информация об активном окне
/// </summary>
public class WindowInfo
{
    /// <summary>
    /// Дескриптор окна (HWND)
    /// </summary>
    public nint Handle { get; set; }

    /// <summary>
    /// ID процесса, которому принадлежит окно
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Имя процесса
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Заголовок окна
    /// </summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>
    /// Имя класса окна
    /// </summary>
    public string WindowClass { get; set; } = string.Empty;

    /// <summary>
    /// Путь к исполняемому файлу
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// AppUserModelId (для UWP-приложений)
    /// </summary>
    public string? AppUserModelId { get; set; }

    /// <summary>
    /// Время получения информации
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Признак того, что окно является UWP-приложением
    /// </summary>
    public bool IsUwpApp => 
        WindowClass.Contains("ApplicationFrame") || 
        WindowClass.Contains("Windows.UI.Core.CoreWindow");

    /// <summary>
    /// Признак того, что окно является рабочим столом
    /// </summary>
    public bool IsDesktop =>
        ProcessName.Contains("explorer", StringComparison.OrdinalIgnoreCase) &&
        (WindowClass.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
         WindowClass.Equals("WorkerW", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Признак того, что окно является системным (панель задач и другие системные окна)
    /// </summary>
    public bool IsSystemWindow =>
        string.IsNullOrEmpty(ProcessName) ||
        IsDesktop ||
        (ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
         WindowClass.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase)) ||
        ProcessName.Equals("System", StringComparison.OrdinalIgnoreCase) ||
        ProcessName.Equals("dwm", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Уникальный идентификатор приложения для правил
    /// </summary>
    public string AppId => AppUserModelId ?? ProcessName ?? string.Empty;

    /// <summary>
    /// Пустая информация об окне
    /// </summary>
    public static WindowInfo Empty => new();

    /// <summary>
    /// Возвращает строковое представление информации об окне
    /// </summary>
    public override string ToString()
    {
        if (!string.IsNullOrEmpty(AppUserModelId))
            return $"[{ProcessName}] {AppUserModelId} - {WindowTitle}";
        return $"[{ProcessName}] {WindowTitle}";
    }
}
