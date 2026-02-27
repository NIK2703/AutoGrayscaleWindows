using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Serilog;

namespace AutoGrayscaleWindows.Services;

/// <summary>
/// Менеджер иконки в системном трее
/// </summary>
public class TrayIconManager : IDisposable
{
    private TaskbarIcon? _taskbarIcon;
    private bool _isGrayscaleEnabled;
    private bool _isPaused;
    private bool _isMonitoringEnabled = true;
    private bool _disposed;

    // Элементы меню для динамического обновления
    private MenuItem? _statusMenuItem;
    private MenuItem? _toggleMonitoringMenuItem;
    private MenuItem? _settingsMenuItem;
    private MenuItem? _exitMenuItem;

    /// <summary>
    /// Событие запроса на открытие настроек
    /// </summary>
    public event EventHandler? OpenSettingsRequested;

    /// <summary>
    /// Событие запроса на переключение grayscale
    /// </summary>
    public event EventHandler? ToggleGrayscaleRequested;

    /// <summary>
    /// Событие запроса на переключение мониторинга
    /// </summary>
    public event EventHandler? ToggleMonitoringRequested;

    /// <summary>
    /// Событие запроса на выход из приложения
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Видима ли иконка в трее
    /// </summary>
    public bool IsVisible => _taskbarIcon != null;

    /// <summary>
    /// Инициализирует иконку в трее
    /// </summary>
    public void Initialize()
    {
        if (_taskbarIcon != null)
        {
            Log.Warning("TrayIconManager уже инициализирован");
            return;
        }

        try
        {
            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = "Auto Grayscale Windows",
                Icon = CreateIcon(false, false),
                ContextMenu = CreateContextMenu()
            };

            // Двойной клик открывает настройки
            _taskbarIcon.TrayMouseDoubleClick += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

            // Одиночный клик переключает мониторинг
            _taskbarIcon.TrayLeftMouseDown += (_, _) => ToggleMonitoringRequested?.Invoke(this, EventArgs.Empty);

            Log.Information("TrayIconManager инициализирован");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации TrayIconManager");
        }
    }

    /// <summary>
    /// Создаёт контекстное меню
    /// </summary>
    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        // Статус (неактивный пункт)
        _statusMenuItem = new MenuItem
        {
            Header = GetStatusText(),
            IsEnabled = false,
            FontWeight = FontWeights.Bold
        };
        menu.Items.Add(_statusMenuItem);

        menu.Items.Add(new Separator());

        // Мониторинг (переключатель)
        _toggleMonitoringMenuItem = new MenuItem
        {
            Header = GetMonitoringText(),
            IsCheckable = true,
            IsChecked = true
        };
        _toggleMonitoringMenuItem.Click += (_, _) => ToggleMonitoringRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_toggleMonitoringMenuItem);

        menu.Items.Add(new Separator());

        // Настройки
        _settingsMenuItem = new MenuItem
        {
            Header = GetSettingsText()
        };
        _settingsMenuItem.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_settingsMenuItem);

        menu.Items.Add(new Separator());

        // Выход
        _exitMenuItem = new MenuItem
        {
            Header = GetExitText()
        };
        _exitMenuItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_exitMenuItem);

        return menu;
    }

    /// <summary>
    /// Получает локализованный текст статуса
    /// </summary>
    private string GetStatusText()
    {
        if (_isPaused)
        {
            return "⏸ " + LocalizationManager.GetString("TrayPaused");
        }
        if (!_isMonitoringEnabled)
        {
            return "⏹ " + LocalizationManager.GetString("TrayMonitoringStopped");
        }
        if (_isGrayscaleEnabled)
        {
            return "⚫ " + LocalizationManager.GetString("TrayGrayscaleOn");
        }
        return "⚪ " + LocalizationManager.GetString("TrayGrayscaleOff");
    }

    /// <summary>
    /// Получает локализованный текст мониторинга
    /// </summary>
    private string GetMonitoringText()
    {
        return LocalizationManager.GetString("TrayMonitoringEnabled");
    }

    /// <summary>
    /// Получает локализованный текст настроек
    /// </summary>
    private string GetSettingsText()
    {
        return "⚙ " + LocalizationManager.GetString("TraySettings");
    }

    /// <summary>
    /// Получает локализованный текст выхода
    /// </summary>
    private string GetExitText()
    {
        return "✖ " + LocalizationManager.GetString("TrayExit");
    }

    /// <summary>
    /// Обновляет локализацию меню
    /// </summary>
    public void UpdateLocalization()
    {
        if (_taskbarIcon?.ContextMenu == null)
            return;

        UpdateIconAndTooltip();
        UpdateMenuItems();
    }

    /// <summary>
    /// Обновляет состояние иконки и меню
    /// </summary>
    public void UpdateState(bool isGrayscaleEnabled, bool isPaused)
    {
        _isGrayscaleEnabled = isGrayscaleEnabled;
        _isPaused = isPaused;

        UpdateIconAndTooltip();
        UpdateMenuItems();
    }

    /// <summary>
    /// Обновляет состояние мониторинга
    /// </summary>
    public void UpdateMonitoringState(bool isMonitoringEnabled)
    {
        _isMonitoringEnabled = isMonitoringEnabled;
        UpdateMenuItems();
    }

    /// <summary>
    /// Обновляет иконку и подсказку
    /// </summary>
    private void UpdateIconAndTooltip()
    {
        if (_taskbarIcon == null)
            return;

        try
        {
            _taskbarIcon.Icon = CreateIcon(_isGrayscaleEnabled, _isPaused);

            // Формируем текст подсказки
            var lines = new List<string>
            {
                "Auto Grayscale Windows"
            };
            
            if (_isPaused)
            {
                lines.Add("⏸ " + LocalizationManager.GetString("TrayPaused"));
            }
            else if (!_isMonitoringEnabled)
            {
                lines.Add("⏹ " + LocalizationManager.GetString("TrayMonitoringStopped"));
            }
            else
            {
                lines.Add(_isGrayscaleEnabled 
                    ? "⚫ " + LocalizationManager.GetString("TrayGrayscaleOn")
                    : "⚪ " + LocalizationManager.GetString("TrayGrayscaleOff"));
            }

            _taskbarIcon.ToolTipText = string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обновлении иконки");
        }
    }

    /// <summary>
    /// Обновляет пункты меню
    /// </summary>
    private void UpdateMenuItems()
    {
        if (_taskbarIcon?.ContextMenu == null)
            return;

        try
        {
            // Обновляем статус
            if (_statusMenuItem != null)
            {
                _statusMenuItem.Header = GetStatusText();
            }

            // Обновляем пункт мониторинга
            if (_toggleMonitoringMenuItem != null)
            {
                _toggleMonitoringMenuItem.Header = GetMonitoringText();
                _toggleMonitoringMenuItem.IsChecked = _isMonitoringEnabled;
            }

            // Обновляем пункт настроек
            if (_settingsMenuItem != null)
            {
                _settingsMenuItem.Header = GetSettingsText();
            }

            // Обновляем пункт выхода
            if (_exitMenuItem != null)
            {
                _exitMenuItem.Header = GetExitText();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обновлении меню");
        }
    }

    /// <summary>
    /// Создаёт иконку для трея
    /// </summary>
    private Icon CreateIcon(bool isGrayscaleEnabled, bool isPaused)
    {
        int size = 16;
        using var bitmap = new Bitmap(size, size);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);

            // Определяем цвет
            Color baseColor;
            if (isPaused)
            {
                baseColor = Color.Orange;
            }
            else if (isGrayscaleEnabled)
            {
                baseColor = Color.Gray;
            }
            else
            {
                baseColor = Color.LimeGreen;
            }

            // Рисуем круг
            using var brush = new SolidBrush(baseColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(brush, 1, 1, size - 2, size - 2);

            // Обводка
            using var pen = new Pen(Color.White, 1);
            g.DrawEllipse(pen, 1, 1, size - 2, size - 2);

            // Символ паузы
            if (isPaused)
            {
                using var pausePen = new Pen(Color.White, 2);
                g.DrawLine(pausePen, 5, 4, 5, 11);
                g.DrawLine(pausePen, 10, 4, 10, 11);
            }
            // Символ G для grayscale
            else if (isGrayscaleEnabled)
            {
                using var font = new Font(new FontFamily("Arial"), 8, System.Drawing.FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.White);
                var textSize = g.MeasureString("G", font);
                g.DrawString("G", font, textBrush, (size - textSize.Width) / 2, (size - textSize.Height) / 2 - 1);
            }
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// Показывает всплывающее уведомление
    /// </summary>
    /// <param name="title">Заголовок</param>
    /// <param name="message">Сообщение</param>
    /// <param name="icon">Тип иконки</param>
    public void ShowNotification(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        try
        {
            _taskbarIcon?.ShowBalloonTip(title, message, icon);
            Log.Debug("Показано уведомление: {Title} - {Message}", title, message);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось показать уведомление");
        }
    }

    /// <summary>
    /// Показывает информационное уведомление
    /// </summary>
    public void ShowInfo(string title, string message)
    {
        ShowNotification(title, message, BalloonIcon.Info);
    }

    /// <summary>
    /// Показывает предупреждение
    /// </summary>
    public void ShowWarning(string title, string message)
    {
        ShowNotification(title, message, BalloonIcon.Warning);
    }

    /// <summary>
    /// Показывает уведомление об ошибке
    /// </summary>
    public void ShowError(string title, string message)
    {
        ShowNotification(title, message, BalloonIcon.Error);
    }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _taskbarIcon?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при освобождении TrayIconManager");
        }

        _taskbarIcon = null;
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~TrayIconManager()
    {
        Dispose();
    }
}
