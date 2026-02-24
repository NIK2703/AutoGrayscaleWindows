using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using AutoGrayscaleWindows.Models;
using AutoGrayscaleWindows.Native;
using Serilog;

namespace AutoGrayscaleWindows.Core;

/// <summary>
/// Аргументы события смены активного окна
/// </summary>
public class WindowChangedEventArgs : EventArgs
{
    /// <summary>
    /// Информация об окне
    /// </summary>
    public WindowInfo WindowInfo { get; set; } = WindowInfo.Empty;

    /// <summary>
    /// Время события
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Класс для мониторинга смены активного окна с использованием WinEvent hook
/// </summary>
public class WindowMonitor : IDisposable
{
    private nint _foregroundHookHandle;
    private nint _nameChangeHookHandle;
    private WinEventDelegate? _foregroundEventDelegate;
    private WinEventDelegate? _nameChangeEventDelegate;
    private bool _isRunning;
    private bool _disposed;
    private readonly Dispatcher _dispatcher;
    
    // Таймер для периодической проверки foreground окна (для обнаружения рабочего стола)
    private DispatcherTimer? _pollingTimer;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(500);
    
    // Кэш информации об окнах
    private readonly ConcurrentDictionary<int, WindowInfo> _windowInfoCache = new();
    private readonly ConcurrentDictionary<int, DateTime> _cacheTimestamps = new();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    // Последнее обработанное окно для предотвращения дубликатов
    private nint _lastWindowHandle;
    private string _lastWindowTitle = string.Empty;
    private DateTime _lastEventTime;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Событие при смене активного окна
    /// </summary>
    public event EventHandler<WindowChangedEventArgs>? WindowChanged;

    /// <summary>
    /// Признак того, что мониторинг запущен
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Включить фильтрацию системных окон
    /// </summary>
    public bool FilterSystemWindows { get; set; } = true;

    /// <summary>
    /// Включить кэширование информации об окнах
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Создаёт экземпляр монитора окон
    /// </summary>
    public WindowMonitor() : this(Dispatcher.CurrentDispatcher)
    {
    }

    /// <summary>
    /// Создаёт экземпляр монитора окон с указанным диспетчером
    /// </summary>
    /// <param name="dispatcher">Диспетчер для маршализации событий в UI-поток</param>
    public WindowMonitor(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Запускает мониторинг смены активного окна и изменения заголовков
    /// </summary>
    /// <returns>true если мониторинг успешно запущен</returns>
    public bool Start()
    {
        if (_isRunning)
        {
            Log.Warning("WindowMonitor уже запущен");
            return true;
        }

        try
        {
            // Важно сохранить ссылку на делегаты, чтобы они не были собраны GC
            _foregroundEventDelegate = OnForegroundChanged;
            _nameChangeEventDelegate = OnNameChanged;

            // Хук на смену активного окна
            _foregroundHookHandle = WinAPI.SetWinEventHook(
                Constants.EVENT_SYSTEM_FOREGROUND,
                Constants.EVENT_SYSTEM_FOREGROUND,
                nint.Zero,
                _foregroundEventDelegate,
                0,
                0,
                Constants.WINEVENT_OUTOFCONTEXT | Constants.WINEVENT_SKIPOWNPROCESS);

            if (_foregroundHookHandle == nint.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Log.Error("Не удалось установить WinEvent hook для FOREGROUND. Код ошибки: {ErrorCode}", error);
                return false;
            }

            // Хук на изменение имени (заголовка) окна
            _nameChangeHookHandle = WinAPI.SetWinEventHook(
                Constants.EVENT_OBJECT_NAMECHANGE,
                Constants.EVENT_OBJECT_NAMECHANGE,
                nint.Zero,
                _nameChangeEventDelegate,
                0,
                0,
                Constants.WINEVENT_OUTOFCONTEXT | Constants.WINEVENT_SKIPOWNPROCESS);

            if (_nameChangeHookHandle == nint.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Log.Warning("Не удалось установить WinEvent hook для NAMECHANGE. Код ошибки: {ErrorCode}. Продолжаем без отслеживания изменения заголовков.", error);
                // Не возвращаем false, так как основной хук работает
            }

            _isRunning = true;
            
            // Запускаем таймер для периодической проверки (для обнаружения рабочего стола)
            _pollingTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
            _pollingTimer.Interval = _pollingInterval;
            _pollingTimer.Tick += OnPollingTimerTick;
            _pollingTimer.Start();
            
            Log.Information("WindowMonitor запущен. Foreground hook: {ForegroundHook}, NameChange hook: {NameChangeHook}", 
                _foregroundHookHandle, _nameChangeHookHandle);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при запуске WindowMonitor");
            return false;
        }
    }

    /// <summary>
    /// Останавливает мониторинг
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        try
        {
            // Останавливаем таймер
            if (_pollingTimer != null)
            {
                _pollingTimer.Stop();
                _pollingTimer.Tick -= OnPollingTimerTick;
                _pollingTimer = null;
            }

            if (_foregroundHookHandle != nint.Zero)
            {
                WinAPI.UnhookWinEvent(_foregroundHookHandle);
                _foregroundHookHandle = nint.Zero;
            }

            if (_nameChangeHookHandle != nint.Zero)
            {
                WinAPI.UnhookWinEvent(_nameChangeHookHandle);
                _nameChangeHookHandle = nint.Zero;
            }

            _isRunning = false;
            Log.Information("WindowMonitor остановлен");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при остановке WindowMonitor");
        }
    }

    /// <summary>
    /// Обработчик события смены активного окна (foreground)
    /// </summary>
    private void OnForegroundChanged(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        try
        {
            // Игнорируем события не от окон (idObject != 0 означает не окно)
            if (idObject != 0 || idChild != 0)
                return;

            // Проверяем валидность окна
            if (hwnd == nint.Zero)
                return;

            // Debounce: игнорируем быстрые повторные события для того же окна
            var now = DateTime.Now;
            if (hwnd == _lastWindowHandle && (now - _lastEventTime) < _debounceInterval)
                return;

            _lastWindowHandle = hwnd;
            _lastEventTime = now;
            _lastWindowTitle = WinAPI.GetWindowTitle(hwnd);

            // Получаем информацию об окне
            var windowInfo = GetWindowInfo(hwnd);
            
            // Логируем все окна для отладки определения рабочего стола
            Log.Debug("Foreground окно: Process={Process}, Class={Class}, Title={Title}, IsDesktop={IsDesktop}, IsSystem={IsSystem}",
                windowInfo.ProcessName, windowInfo.WindowClass, windowInfo.WindowTitle, windowInfo.IsDesktop, windowInfo.IsSystemWindow);
            
            // Фильтрация системных окон (но НЕ рабочего стола - он обрабатывается отдельно)
            if (FilterSystemWindows && windowInfo.IsSystemWindow && !windowInfo.IsDesktop)
            {
                Log.Debug("Пропуск системного окна: {ProcessName} ({WindowClass})",
                    windowInfo.ProcessName, windowInfo.WindowClass);
                return;
            }

            Log.Debug("Смена активного окна: {WindowInfo}", windowInfo);

            // Синхронная маршализация в UI-поток через Dispatcher.Invoke для немедленного выполнения
            // Используем DispatcherPriority.Normal для быстрой обработки
            _dispatcher.Invoke(() =>
            {
                try
                {
                    WindowChanged?.Invoke(this, new WindowChangedEventArgs
                    {
                        WindowInfo = windowInfo,
                        Timestamp = now
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка при генерации события WindowChanged");
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обработке события смены окна");
        }
    }

    /// <summary>
    /// Обработчик события изменения имени (заголовка) окна
    /// </summary>
    private void OnNameChanged(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        try
        {
            // Игнорируем события не от окон (idObject != 0 означает не окно)
            if (idObject != 0 || idChild != 0)
                return;

            // Проверяем валидность окна
            if (hwnd == nint.Zero)
                return;

            // Получаем текущий заголовок
            var newTitle = WinAPI.GetWindowTitle(hwnd);

            // Debounce: игнорируем быстрые повторные события для того же окна с тем же заголовком
            var now = DateTime.Now;
            if (hwnd == _lastWindowHandle && newTitle == _lastWindowTitle && (now - _lastEventTime) < _debounceInterval)
                return;

            // Проверяем, что это окно в фокусе (на переднем плане)
            var foregroundWindow = WinAPI.GetForegroundWindow();
            if (hwnd != foregroundWindow)
            {
                // Событие не от окна в фокусе - игнорируем
                return;
            }

            _lastWindowHandle = hwnd;
            _lastWindowTitle = newTitle;
            _lastEventTime = now;

            // Получаем информацию об окне
            var windowInfo = GetWindowInfo(hwnd);
            
            // Фильтрация системных окон (но НЕ рабочего стола - он обрабатывается отдельно)
            if (FilterSystemWindows && windowInfo.IsSystemWindow && !windowInfo.IsDesktop)
            {
                Log.Debug("Пропуск системного окна при изменении заголовка: {ProcessName} ({WindowClass})",
                    windowInfo.ProcessName, windowInfo.WindowClass);
                return;
            }

            Log.Debug("Изменение заголовка окна: {WindowTitle} ({ProcessName})", windowInfo.WindowTitle, windowInfo.ProcessName);

            // Синхронная маршализация в UI-поток через Dispatcher.Invoke для немедленного выполнения
            _dispatcher.Invoke(() =>
            {
                try
                {
                    WindowChanged?.Invoke(this, new WindowChangedEventArgs
                    {
                        WindowInfo = windowInfo,
                        Timestamp = now
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка при генерации события WindowChanged (name change)");
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обработке события изменения заголовка окна");
        }
    }

    /// <summary>
    /// Обработчик тика таймера для периодической проверки foreground окна
    /// Используется для обнаружения рабочего стола, который не всегда генерирует события
    /// </summary>
    private void OnPollingTimerTick(object? sender, EventArgs e)
    {
        try
        {
            var foregroundHwnd = WinAPI.GetForegroundWindow();
            if (foregroundHwnd == nint.Zero)
                return;

            // Получаем информацию об окне
            var windowInfo = GetWindowInfo(foregroundHwnd);
            
            // Проверяем, изменилось ли окно
            if (foregroundHwnd == _lastWindowHandle)
                return;
            
            // Debounce
            var now = DateTime.Now;
            if ((now - _lastEventTime) < _debounceInterval)
                return;

            _lastWindowHandle = foregroundHwnd;
            _lastEventTime = now;
            _lastWindowTitle = windowInfo.WindowTitle;

            // Фильтрация системных окон (но НЕ рабочего стола)
            if (FilterSystemWindows && windowInfo.IsSystemWindow && !windowInfo.IsDesktop)
                return;

            Log.Debug("Polling: обнаружена смена окна: {ProcessName} ({WindowClass}), IsDesktop={IsDesktop}",
                windowInfo.ProcessName, windowInfo.WindowClass, windowInfo.IsDesktop);

            // Генерируем событие
            WindowChanged?.Invoke(this, new WindowChangedEventArgs
            {
                WindowInfo = windowInfo,
                Timestamp = now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при polling проверке окна");
        }
    }

    /// <summary>
    /// Получает информацию об окне с кэшированием
    /// </summary>
    /// <param name="hWnd">Дескриптор окна</param>
    /// <returns>Информация об окне</returns>
    private WindowInfo GetWindowInfo(nint hWnd)
    {
        // Получаем ID процесса для проверки кэша
        uint processId = WinAPI.GetProcessIdFromWindow(hWnd);
        
        // Используем hWnd как ключ кэша, чтобы различать разные окна одного процесса
        int cacheKey = hWnd.ToInt32();
        
        // Проверяем кэш (короткий TTL для быстрого реагирования на смену окна)
        if (EnableCaching && cacheKey != 0)
        {
            if (_windowInfoCache.TryGetValue(cacheKey, out var cachedInfo))
            {
                if (_cacheTimestamps.TryGetValue(cacheKey, out var cacheTime))
                {
                    // Уменьшенный TTL для более точного определения класса окна
                    if (DateTime.Now - cacheTime < TimeSpan.FromSeconds(5))
                    {
                        // Обновляем дескриптор, заголовок и класс окна (они могли измениться)
                        cachedInfo.Handle = hWnd;
                        cachedInfo.WindowTitle = WinAPI.GetWindowTitle(hWnd);
                        cachedInfo.WindowClass = WinAPI.GetWindowClassName(hWnd);
                        cachedInfo.Timestamp = DateTime.Now;
                        return cachedInfo;
                    }
                }
            }
        }

        // Получаем полную информацию
        var info = BuildWindowInfo(hWnd, processId);

        // Сохраняем в кэш с ключом hWnd
        if (EnableCaching && cacheKey != 0)
        {
            _windowInfoCache[cacheKey] = info;
            _cacheTimestamps[cacheKey] = DateTime.Now;
        }

        return info;
    }

    /// <summary>
    /// Строит объект WindowInfo с полной информацией об окне
    /// </summary>
    private WindowInfo BuildWindowInfo(nint hWnd, uint processId)
    {
        var info = new WindowInfo
        {
            Handle = hWnd,
            ProcessId = (int)processId,
            Timestamp = DateTime.Now
        };

        try
        {
            // Получаем класс окна
            info.WindowClass = WinAPI.GetWindowClassName(hWnd);

            // Получаем заголовок окна
            info.WindowTitle = WinAPI.GetWindowTitle(hWnd);

            if (processId > 0)
            {
                // Быстро получаем имя процесса через WinAPI (намного быстрее Process.GetProcessById)
                info.ProcessName = WinAPI.GetProcessBaseName(processId);
                
                // Если не удалось получить через WinAPI, пробуем через Process
                if (string.IsNullOrEmpty(info.ProcessName))
                {
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        info.ProcessName = process.ProcessName ?? "Unknown";
                    }
                    catch
                    {
                        info.ProcessName = "Unknown";
                    }
                }

                // Для UWP-приложений получаем AppUserModelId
                if (info.IsUwpApp)
                {
                    info.AppUserModelId = GetAppUserModelId(hWnd);
                    Log.Debug("UWP приложение: {AppUserModelId}", info.AppUserModelId);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при получении информации об окне {HWnd}", hWnd);
        }

        return info;
    }

    /// <summary>
    /// Получает AppUserModelId для UWP-приложения
    /// </summary>
    /// <param name="hWnd">Дескриптор окна</param>
    /// <returns>AppUserModelId или null</returns>
    private string? GetAppUserModelId(nint hWnd)
    {
        try
        {
            var iid = COMInterfaces.IID_IPropertyStore;
            int hr = WinAPI.SHGetPropertyStoreForWindow(hWnd, ref iid, out nint pPropertyStore);

            if (hr != 0 || pPropertyStore == nint.Zero)
            {
                Log.Debug("Не удалось получить IPropertyStore для окна. HRESULT: {HR}", hr);
                return null;
            }

            try
            {
                // Получаем IPropertyStore
                var propertyStore = Marshal.GetObjectForIUnknown(pPropertyStore) as IPropertyStore;
                
                if (propertyStore == null)
                {
                    return null;
                }

                try
                {
                    // Получаем значение AppUserModelId
                    var key = COMInterfaces.PKEY_AppUserModel_ID;
                    int result = propertyStore.GetValue(ref key, out PROPVARIANT variant);

                    if (result == 0)
                    {
                        string? appId = variant.GetString();
                        variant.Clear();
                        return appId;
                    }

                    return null;
                }
                finally
                {
                    Marshal.ReleaseComObject(propertyStore);
                }
            }
            finally
            {
                Marshal.Release(pPropertyStore);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Ошибка при получении AppUserModelId для окна {HWnd}", hWnd);
            return null;
        }
    }

    /// <summary>
    /// Очищает кэш информации об окнах
    /// </summary>
    public void ClearCache()
    {
        _windowInfoCache.Clear();
        _cacheTimestamps.Clear();
        Log.Debug("Кэш WindowMonitor очищен");
    }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        ClearCache();
        _foregroundEventDelegate = null;
        _nameChangeEventDelegate = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~WindowMonitor()
    {
        Dispose();
    }
}
