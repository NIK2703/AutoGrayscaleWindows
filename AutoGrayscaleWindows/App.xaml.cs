using System.Windows;
using System.Windows.Interop;
using AutoGrayscaleWindows.Core;
using AutoGrayscaleWindows.Models;
using AutoGrayscaleWindows.Native;
using AutoGrayscaleWindows.Services;
using AutoGrayscaleWindows.UI.Views;
using AutoGrayscaleWindows.Utils;
using Serilog;
using Wpf.Ui.Appearance;

namespace AutoGrayscaleWindows;

/// <summary>
/// Класс приложения Auto Grayscale Windows
/// </summary>
public partial class App : Application
{
    // Сервисы
    private SingleInstanceManager? _singleInstanceManager;
    private ConfigManager? _configManager;
    private WindowMonitor? _windowMonitor;
    private FilterController? _filterController;
    private RuleEngine? _ruleEngine;
    private HotkeyManager? _hotkeyManager;
    private TrayIconManager? _trayIconManager;
    private StartupManager? _startupManager;

    // Главное окно
    private MainWindow? _mainWindow;

    // Состояние
    private bool _isMonitoringEnabled = true;
    private bool _startMinimized = false;

    /// <summary>
    /// Точка входа приложения
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Проверяем аргументы командной строки
        if (e.Args.Length > 0)
        {
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-minimized", StringComparison.OrdinalIgnoreCase))
                {
                    _startMinimized = true;
                    Log.Information("Запуск в свёрнутом режиме (только трей)");
                }
            }
        }

        // Инициализация логирования
#if DEBUG
        LoggerSetup.Initialize(Serilog.Events.LogEventLevel.Debug);
#else
        LoggerSetup.Initialize(Serilog.Events.LogEventLevel.Information);
#endif

        // Настройка темы приложения (следование за системной)
        InitializeTheme();

        Log.Information("Запуск Auto Grayscale Windows v{Version}",
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

        // Проверка на единственный экземпляр
        _singleInstanceManager = new SingleInstanceManager();
        if (!_singleInstanceManager.TryAcquire())
        {
            Log.Warning("Другой экземпляр приложения уже запущен");
            MessageBox.Show("Приложение Auto Grayscale Windows уже запущено.\n" +
                           "Проверьте иконку в системном трее.",
                           "Auto Grayscale Windows",
                           MessageBoxButton.OK,
                           MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        try
        {
            // Инициализация компонентов
            InitializeComponents();

            // Настройка обработчиков событий
            SetupEventHandlers();

            // Создание главного окна
            _mainWindow = new MainWindow();
            _mainWindow.Initialize(_filterController!, _windowMonitor!, _configManager!, _startupManager!);
            
            // Подписываемся на событие смены режима списка
            _mainWindow.ListModeChanged += (_, _) => ForceReevaluateCurrentWindow();

            // Запускаем отслеживание изменений системной темы для главного окна
            SystemThemeWatcher.Watch(_mainWindow);
            
            // Показываем окно только если не запущены в свёрнутом режиме
            if (!_startMinimized)
            {
                _mainWindow.Show();
            }
            else
            {
                // При запуске в свёрнутом режиме создаём дескриптор окна без его показа
                // Это необходимо для работы хоткеев - окно не будет отображаться вообще
                var helper = new WindowInteropHelper(_mainWindow);
                helper.EnsureHandle(); // Создаёт дескриптор окна без показа окна
                Log.Information("Приложение запущено в режиме трея (тихий запуск)");
            }

            // Инициализация хоткеев с дескриптором окна
            InitializeHotkeys();

            // Запуск мониторинга
            StartMonitoring();

            // Показать уведомление о запуске (если настроено)
            ShowStartupNotification();

            Log.Information("Приложение успешно инициализировано");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка при запуске приложения");
            MessageBox.Show($"Ошибка при запуске приложения:\n{ex.Message}",
                "Auto Grayscale Windows", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Инициализация всех компонентов приложения
    /// </summary>
    private void InitializeComponents()
    {
        // Менеджер автозапуска
        _startupManager = new StartupManager();

        // Менеджер конфигурации
        _configManager = new ConfigManager();
        _configManager.Load();
        
        // Инициализируем состояние мониторинга из конфигурации
        _isMonitoringEnabled = _configManager.Config.IsEnabled;

        // Инициализация локализации
        LocalizationManager.Instance.Initialize();
        if (!string.IsNullOrEmpty(_configManager.Config.Language))
        {
            LocalizationManager.Instance.SetLanguage(_configManager.Config.Language);
        }

        // Контроллер фильтров
        _filterController = new FilterController();
        _filterController.SyncState();

        // Движок правил
        _ruleEngine = new RuleEngine();
        UpdateRuleEngine();

        // Менеджер хоткеев
        _hotkeyManager = new HotkeyManager();

        // Менеджер трея
        _trayIconManager = new TrayIconManager();
        _trayIconManager.Initialize();
        UpdateTrayIconState();

        // Менеджер мониторинга окон
        _windowMonitor = new WindowMonitor();

        Log.Debug("Компоненты инициализированы");
    }

    /// <summary>
    /// Настройка обработчиков событий
    /// </summary>
    private void SetupEventHandlers()
    {
        // Событие смены активного окна
        _windowMonitor!.WindowChanged += OnWindowChanged;

        // События движка правил
        _ruleEngine!.RuleMatched += (_, e) =>
        {
            Log.Debug("Правило сработало: {RuleName} -> {Action}", e.MatchedRule?.DisplayName ?? "default", e.Action);
        };

        // События трея
        _trayIconManager!.OpenSettingsRequested += (_, _) => OpenMainWindow();
        _trayIconManager.ToggleGrayscaleRequested += (_, _) => ToggleGrayscale();
        _trayIconManager.ToggleMonitoringRequested += (_, _) => ToggleMonitoring();
        _trayIconManager.ExitRequested += (_, _) => GracefulShutdown();

        // Событие изменения конфигурации
        _configManager!.ConfigChanged += (_, config) =>
        {
            // Синхронизируем состояние мониторинга
            _isMonitoringEnabled = config.IsEnabled;
            UpdateRuleEngine();
            UpdateHotkeys();
            UpdateTrayIconState();
        };

        // Событие изменения состояния фильтра
        _filterController!.FilterChanged += (_, args) =>
        {
            UpdateTrayIconState();
        };

        // Событие ошибки регистрации хоткея
        _hotkeyManager!.RegistrationError += (_, e) =>
        {
            Log.Warning("Ошибка регистрации хоткея ID={Id}: {Error}", e.Id, e.Error);
            _trayIconManager?.ShowWarning("Ошибка горячих клавиш",
                $"Не удалось зарегистрировать горячую клавишу:\n{e.Error}");
        };

        // Обработчик нажатия хоткеев
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
    }

    /// <summary>
    /// Инициализация горячих клавиш
    /// </summary>
    private void InitializeHotkeys()
    {
        if (_mainWindow == null)
        {
            Log.Warning("Невозможно инициализировать хоткеи: главное окно не создано");
            return;
        }

        var handle = new WindowInteropHelper(_mainWindow).Handle;
        if (handle == nint.Zero)
        {
            // Если дескриптор ещё не создан, ждём события SourceInitialized
            _mainWindow.SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(_mainWindow).Handle;
                if (h != nint.Zero)
                {
                    _hotkeyManager!.Initialize(h);
                    UpdateHotkeys();
                }
            };
        }
        else
        {
            // Дескриптор уже создан (например, через EnsureHandle при тихом запуске)
            _hotkeyManager!.Initialize(handle);
            UpdateHotkeys();
        }
    }

    /// <summary>
    /// Запуск мониторинга окон
    /// </summary>
    private void StartMonitoring()
    {
        if (!_windowMonitor!.Start())
        {
            Log.Warning("Не удалось запустить мониторинг окон");
            _trayIconManager?.ShowWarning("Auto Grayscale Windows",
                "Не удалось запустить мониторинг окон.\nНекоторые функции могут не работать.");
        }
    }

    /// <summary>
    /// Показать уведомление о запуске
    /// </summary>
    private void ShowStartupNotification()
    {
        // Уведомления о запуске отключены
    }

    /// <summary>
    /// Обработчик смены активного окна
    /// </summary>
    private void OnWindowChanged(object? sender, WindowChangedEventArgs e)
    {
        // Проверяем включенность мониторинга (синхронизировано с Config.IsEnabled)
        if (!_isMonitoringEnabled)
            return;

        // Проверяем паузу
        if (_filterController?.IsPaused == true)
            return;

        var config = _configManager.Config;
        var windowInfo = e.WindowInfo;

        // Логируем информацию об окне для отладки
        Log.Debug("OnWindowChanged: Process={Process}, Class={Class}, Title={Title}, IsDesktop={IsDesktop}",
            windowInfo.ProcessName, windowInfo.WindowClass, windowInfo.WindowTitle, windowInfo.IsDesktop);

        // Оцениваем правила немедленно, без задержки
        var result = _ruleEngine!.Evaluate(e.WindowInfo);

        // Применяем действие
        if (result.Action == RuleAction.EnableGrayscale)
        {
            _filterController!.EnableGrayscale();
        }
        else
        {
            _filterController!.DisableGrayscale();
        }

        Log.Debug("Окно изменено: {ProcessName} -> {Action}",
            e.WindowInfo.ProcessName, result.Action);
    }

    /// <summary>
    /// Обновить правила в движке
    /// </summary>
    private void UpdateRuleEngine()
    {
        var config = _configManager!.Config;
        _ruleEngine?.SetRules(
            config.GetCurrentRules(),
            config.UseWhitelist);
    }

    /// <summary>
    /// Принудительно применить правила к текущему активному окну
    /// </summary>
    public void ForceReevaluateCurrentWindow()
    {
        try
        {
            // Получаем текущее активное окно
            var foregroundHwnd = WinAPI.GetForegroundWindow();
            if (foregroundHwnd == nint.Zero)
                return;

            // Получаем ID процесса
            uint processId = WinAPI.GetProcessIdFromWindow(foregroundHwnd);
            
            // Строим информацию об окне
            var windowInfo = new WindowInfo
            {
                Handle = foregroundHwnd,
                ProcessId = (int)processId,
                Timestamp = DateTime.Now
            };

            try
            {
                windowInfo.WindowClass = WinAPI.GetWindowClassName(foregroundHwnd);
                windowInfo.WindowTitle = WinAPI.GetWindowTitle(foregroundHwnd);

                if (processId > 0)
                {
                    windowInfo.ProcessName = WinAPI.GetProcessBaseName(processId);
                    
                    if (string.IsNullOrEmpty(windowInfo.ProcessName))
                    {
                        try
                        {
                            var process = System.Diagnostics.Process.GetProcessById((int)processId);
                            windowInfo.ProcessName = process.ProcessName ?? "Unknown";
                        }
                        catch
                        {
                            windowInfo.ProcessName = "Unknown";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при получении информации об окне {HWnd}", foregroundHwnd);
            }

            // Обновляем правила
            UpdateRuleEngine();

            // Оцениваем правила
            var result = _ruleEngine!.Evaluate(windowInfo);

            // Применяем действие
            if (result.Action == RuleAction.EnableGrayscale)
            {
                _filterController!.EnableGrayscale();
            }
            else
            {
                _filterController!.DisableGrayscale();
            }

            Log.Debug("Принудительная переоценка: {ProcessName} -> {Action}",
                windowInfo.ProcessName, result.Action);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при принудительной переоценке окна");
        }
    }

    /// <summary>
    /// Обновить регистрацию горячих клавиш
    /// </summary>
    private void UpdateHotkeys()
    {
        if (_hotkeyManager == null || !_hotkeyManager.IsInitialized)
            return;

        var config = _configManager!.Config;

        // Удаляем старые хоткеи
        _hotkeyManager.UnregisterAll();

        // Регистрируем хоткей переключения grayscale
        if (config.ToggleGrayscaleHotkey.IsEnabled)
        {
            _hotkeyManager.RegisterHotKey(
                Constants.HOTKEY_TOGGLE_GRAYSCALE,
                (HotkeyModifiers)config.ToggleGrayscaleHotkey.Modifier,
                config.ToggleGrayscaleHotkey.VirtualKey,
                "Переключить Grayscale");
        }

        // Регистрируем хоткей паузы
        if (config.PauseHotkey.IsEnabled)
        {
            _hotkeyManager.RegisterHotKey(
                Constants.HOTKEY_PAUSE,
                (HotkeyModifiers)config.PauseHotkey.Modifier,
                config.PauseHotkey.VirtualKey,
                "Пауза/Возобновить");
        }

        Log.Debug("Горячие клавиши обновлены: зарегистрировано {Count}", _hotkeyManager.RegisteredCount);
    }

    /// <summary>
    /// Обработчик нажатия горячих клавиш
    /// </summary>
    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        Log.Debug("Нажата горячая клавиша: ID={Id}", e.HotkeyId);

        switch (e.HotkeyId)
        {
            case Constants.HOTKEY_TOGGLE_GRAYSCALE:
                ToggleGrayscale();
                break;
            case Constants.HOTKEY_PAUSE:
                TogglePause();
                break;
        }
    }

    /// <summary>
    /// Переключить режим grayscale
    /// </summary>
    private void ToggleGrayscale()
    {
        _filterController!.ToggleGrayscale();
    }

    /// <summary>
    /// Переключить паузу
    /// </summary>
    private void TogglePause()
    {
        if (_filterController!.IsPaused)
        {
            _filterController.Resume();
        }
        else
        {
            _filterController.Pause();
        }

        UpdateTrayIconState();
    }

    /// <summary>
    /// Переключить мониторинг
    /// </summary>
    private void ToggleMonitoring()
    {
        _isMonitoringEnabled = !_isMonitoringEnabled;
        
        // Синхронизируем с конфигурацией
        _configManager!.Config.IsEnabled = _isMonitoringEnabled;
        _configManager.Save();
        
        // При отключении мониторинга также отключаем grayscale
        if (!_isMonitoringEnabled && _filterController?.IsGrayscaleEnabled == true)
        {
            _filterController.DisableGrayscale();
        }
        // При включении мониторинга переоцениваем текущее окно
        else if (_isMonitoringEnabled)
        {
            ForceReevaluateCurrentWindow();
        }
        
        // Обновляем UI главного окна
        if (_mainWindow != null)
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                // Обновляем чекбокс в интерфейсе через публичный метод
                _mainWindow.UpdateMonitoringCheckBox(_isMonitoringEnabled);
            });
        }
        
        UpdateTrayIconState();

        Log.Information("Мониторинг: {State}", _isMonitoringEnabled ? "включён" : "выключен");
    }

    /// <summary>
    /// Обновить состояние иконки в трее
    /// </summary>
    private void UpdateTrayIconState()
    {
        _trayIconManager?.UpdateState(
            _filterController?.IsGrayscaleEnabled ?? false,
            _filterController?.IsPaused ?? false);
        _trayIconManager?.UpdateMonitoringState(_isMonitoringEnabled);
    }

    /// <summary>
    /// Обновить локализацию в трее
    /// </summary>
    public void UpdateTrayLocalization()
    {
        _trayIconManager?.UpdateLocalization();
    }

    /// <summary>
    /// Открыть главное окно
    /// </summary>
    private void OpenMainWindow()
    {
        // Обновляем тему в соответствии с системной при открытии окна
        UpdateThemeFromSystem();

        if (_mainWindow != null)
        {
            if (_mainWindow.IsVisible)
            {
                _mainWindow.Activate();
            }
            else
            {
                _mainWindow.Show();
            }
        }
    }

    /// <summary>
    /// Корректное завершение работы
    /// </summary>
    private void GracefulShutdown()
    {
        Log.Information("Запрошено завершение работы");
        Shutdown();
    }

    /// <summary>
    /// Обработчик выхода из приложения
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Завершение работы приложения (код: {ExitCode})", e.ApplicationExitCode);

        try
        {
            // Отключаем grayscale при выходе из приложения
            _filterController?.DisableGrayscale();

            // Остановка мониторинга
            _windowMonitor?.Dispose();

            // Освобождение хоткеев
            _hotkeyManager?.Dispose();

            // Освобождение трея
            _trayIconManager?.Dispose();

            // Освобождение контроллера фильтров
            _filterController?.Dispose();

            // Освобождение менеджера единственного экземпляра
            _singleInstanceManager?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при освобождении ресурсов");
        }

        // Закрытие логгера
        LoggerSetup.CloseAndFlush();

        base.OnExit(e);
    }

    /// <summary>
    /// Инициализация темы приложения (следование за системной)
    /// </summary>
    private void InitializeTheme()
    {
        // Применяем системную тему
        ApplicationThemeManager.ApplySystemTheme();

        Log.Debug("Тема приложения инициализирована (следование за системной)");
    }

    /// <summary>
    /// Обновить тему приложения в соответствии с системной
    /// </summary>
    private static void UpdateThemeFromSystem()
    {
        // Применяем системную тему
        ApplicationThemeManager.ApplySystemTheme();

        Log.Debug("Тема приложения обновлена в соответствии с системной");
    }
}
