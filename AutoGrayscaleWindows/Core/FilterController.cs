using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using AutoGrayscaleWindows.Native;
using Microsoft.Win32;
using Serilog;

namespace AutoGrayscaleWindows.Core;

/// <summary>
/// Тип цветового фильтра Windows
/// </summary>
public enum ColorFilterType
{
    /// <summary>
    /// Оттенки серого (Grayscale)
    /// </summary>
    Grayscale = 0,

    /// <summary>
    /// Инверсия цветов
    /// </summary>
    Invert = 1,

    /// <summary>
    /// Инверсия оттенков серого
    /// </summary>
    InvertGrayscale = 2,

    /// <summary>
    /// Дейтеранопия (красно-зелёная слепота)
    /// </summary>
    Deuteranopia = 3,

    /// <summary>
    /// Протанопия (красно-зелёная слепота)
    /// </summary>
    Protanopia = 4,

    /// <summary>
    /// Тританопия (сине-жёлтая слепота)
    /// </summary>
    Tritanopia = 5
}

/// <summary>
/// Состояние цветового фильтра
/// </summary>
public class FilterState
{
    /// <summary>
    /// Фильтр активен
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Тип фильтра
    /// </summary>
    public ColorFilterType FilterType { get; set; }

    /// <summary>
    /// Время последнего изменения
    /// </summary>
    public DateTime LastChanged { get; set; }

    /// <summary>
    /// Источник изменения
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Создаёт копию состояния
    /// </summary>
    public FilterState Clone()
    {
        return new FilterState
        {
            IsActive = IsActive,
            FilterType = FilterType,
            LastChanged = LastChanged,
            Source = Source
        };
    }
}

/// <summary>
/// Аргументы события изменения фильтра
/// </summary>
public class FilterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Предыдущее состояние
    /// </summary>
    public FilterState PreviousState { get; set; } = new();

    /// <summary>
    /// Текущее состояние
    /// </summary>
    public FilterState CurrentState { get; set; } = new();

    /// <summary>
    /// Источник изменения
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// Аргументы события ошибки
/// </summary>
public class FilterErrorEventArgs : EventArgs
{
    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Исключение
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Операция, вызвавшая ошибку
    /// </summary>
    public string? Operation { get; set; }
}

/// <summary>
/// Класс для управления цветовыми фильтрами Windows через реестр
/// </summary>
public class FilterController : IDisposable, INotifyPropertyChanged
{
    private readonly object _stateLock = new();
    private FilterState _currentState;
    private bool _isPaused;
    private bool _isInitialized;
    private bool _disposed;
    private System.Timers.Timer? _monitoringTimer;
    private readonly TimeSpan _monitoringInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Текущее состояние фильтра
    /// </summary>
    public FilterState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState.Clone();
            }
        }
    }

    /// <summary>
    /// Фильтр активен
    /// </summary>
    public bool IsGrayscaleEnabled
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState.IsActive && _currentState.FilterType == ColorFilterType.Grayscale;
            }
        }
    }

    /// <summary>
    /// Приостановлен ли контроллер
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Доступны ли цветовые фильтры в системе
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Включить мониторинг внешних изменений
    /// </summary>
    public bool EnableMonitoring { get; set; } = true;

    /// <summary>
    /// Событие изменения состояния фильтра
    /// </summary>
    public event EventHandler<FilterChangedEventArgs>? FilterChanged;

    /// <summary>
    /// Событие ошибки
    /// </summary>
    public event EventHandler<FilterErrorEventArgs>? Error;

    /// <summary>
    /// Событие изменения свойства (для INotifyPropertyChanged)
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Создаёт экземпляр контроллера фильтров
    /// </summary>
    public FilterController()
    {
        _currentState = new FilterState
        {
            IsActive = false,
            FilterType = ColorFilterType.Grayscale,
            LastChanged = DateTime.Now
        };

        Initialize();
    }

    /// <summary>
    /// Инициализирует контроллер
    /// </summary>
    private void Initialize()
    {
        try
        {
            // Проверяем доступность цветовых фильтров
            IsAvailable = CheckColorFilterAvailability();

            if (!IsAvailable)
            {
                Log.Warning("Цветовые фильтры недоступны в этой системе");
                _isInitialized = true;
                return;
            }

            // Загружаем текущее состояние из реестра
            LoadStateFromRegistry();

            // Запускаем мониторинг внешних изменений
            if (EnableMonitoring)
            {
                StartMonitoring();
            }

            _isInitialized = true;
            Log.Information("FilterController инициализирован. Состояние: {State}, Тип: {Type}",
                _currentState.IsActive ? "Active" : "Inactive", _currentState.FilterType);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации FilterController");
            IsAvailable = false;
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Проверяет доступность цветовых фильтров в системе
    /// </summary>
    private bool CheckColorFilterAvailability()
    {
        try
        {
            // Проверяем версию Windows (нужна 10.0.15063 или выше)
            var osVersion = Environment.OSVersion.Version;
            if (osVersion.Major < 10 || (osVersion.Major == 10 && osVersion.Build < 15063))
            {
                Log.Warning("Версия Windows не поддерживает цветовые фильтры: {Version}", osVersion);
                return false;
            }

            // Проверяем существование ключа реестра или возможность его создания
            using var key = Registry.CurrentUser.OpenSubKey(Constants.COLOR_FILTER_REGISTRY_PATH);
            if (key != null)
            {
                return true;
            }

            // Пробуем создать ключ
            using var newKey = Registry.CurrentUser.CreateSubKey(Constants.COLOR_FILTER_REGISTRY_PATH);
            return newKey != null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Цветовые фильтры недоступны");
            return false;
        }
    }

    /// <summary>
    /// Загружает состояние из реестра
    /// </summary>
    private void LoadStateFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Constants.COLOR_FILTER_REGISTRY_PATH);
            if (key == null)
            {
                return;
            }

            var activeValue = key.GetValue(Constants.COLOR_FILTER_ACTIVE_KEY);
            var typeValue = key.GetValue(Constants.COLOR_FILTER_TYPE_KEY);

            lock (_stateLock)
            {
                _currentState.IsActive = activeValue is int intValue && intValue == 1;
                _currentState.FilterType = typeValue is int typeInt 
                    ? (ColorFilterType)typeInt 
                    : ColorFilterType.Grayscale;
                _currentState.LastChanged = DateTime.Now;
                _currentState.Source = "Registry";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при загрузке состояния из реестра");
        }
    }

    /// <summary>
    /// Запускает мониторинг внешних изменений
    /// </summary>
    private void StartMonitoring()
    {
        if (_monitoringTimer != null)
            return;

        _monitoringTimer = new System.Timers.Timer(_monitoringInterval.TotalMilliseconds);
        _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
        _monitoringTimer.Start();

        Log.Debug("Мониторинг внешних изменений запущен (интервал: {Interval} сек)", _monitoringInterval.TotalSeconds);
    }

    /// <summary>
    /// Останавливает мониторинг внешних изменений
    /// </summary>
    private void StopMonitoring()
    {
        if (_monitoringTimer == null)
            return;

        _monitoringTimer.Stop();
        _monitoringTimer.Elapsed -= OnMonitoringTimerElapsed;
        _monitoringTimer.Dispose();
        _monitoringTimer = null;

        Log.Debug("Мониторинг внешних изменений остановлен");
    }

    /// <summary>
    /// Обработчик таймера мониторинга
    /// </summary>
    private void OnMonitoringTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            CheckForExternalChanges();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при проверке внешних изменений");
        }
    }

    /// <summary>
    /// Проверяет внешние изменения состояния фильтра
    /// </summary>
    private void CheckForExternalChanges()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Constants.COLOR_FILTER_REGISTRY_PATH);
            if (key == null)
                return;

            var activeValue = key.GetValue(Constants.COLOR_FILTER_ACTIVE_KEY);
            var typeValue = key.GetValue(Constants.COLOR_FILTER_TYPE_KEY);

            bool isActive = activeValue is int intValue && intValue == 1;
            ColorFilterType filterType = typeValue is int typeInt 
                ? (ColorFilterType)typeInt 
                : ColorFilterType.Grayscale;

            lock (_stateLock)
            {
                // Проверяем, изменилось ли состояние
                if (_currentState.IsActive != isActive || _currentState.FilterType != filterType)
                {
                    var previousState = _currentState.Clone();

                    _currentState.IsActive = isActive;
                    _currentState.FilterType = filterType;
                    _currentState.LastChanged = DateTime.Now;
                    _currentState.Source = "External (Hotkey/Settings)";

                    Log.Information("Обнаружено внешнее изменение фильтра: {Previous} -> {Current}",
                        previousState.IsActive ? "Active" : "Inactive",
                        isActive ? "Active" : "Inactive");

                    OnFilterChanged(previousState, _currentState.Clone(), "External");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при проверке внешних изменений");
        }
    }

    /// <summary>
    /// Включает режим оттенков серого
    /// </summary>
    /// <param name="source">Источник изменения (для логирования)</param>
    /// <returns>true если операция успешна</returns>
    public bool EnableGrayscale(string? source = null)
    {
        if (!_isInitialized)
        {
            Log.Warning("FilterController не инициализирован");
            return false;
        }

        if (!IsAvailable)
        {
            Log.Warning("Цветовые фильтры недоступны");
            OnError("Цветовые фильтры недоступны в этой системе", null, "EnableGrayscale");
            return false;
        }

        if (_isPaused)
        {
            Log.Debug("FilterController приостановлен, включение grayscale пропущено");
            return false;
        }

        lock (_stateLock)
        {
            if (_currentState.IsActive && _currentState.FilterType == ColorFilterType.Grayscale)
            {
                Log.Debug("Grayscale уже включен");
                return true;
            }

            return SetColorFilterState(true, ColorFilterType.Grayscale, source ?? "AutoSwitch");
        }
    }

    /// <summary>
    /// Выключает режим оттенков серого
    /// </summary>
    /// <param name="source">Источник изменения (для логирования)</param>
    /// <returns>true если операция успешна</returns>
    public bool DisableGrayscale(string? source = null)
    {
        if (!_isInitialized)
        {
            Log.Warning("FilterController не инициализирован");
            return false;
        }

        if (!IsAvailable)
        {
            Log.Warning("Цветовые фильтры недоступны");
            OnError("Цветовые фильтры недоступны в этой системе", null, "DisableGrayscale");
            return false;
        }

        if (_isPaused)
        {
            Log.Debug("FilterController приостановлен, выключение grayscale пропущено");
            return false;
        }

        lock (_stateLock)
        {
            if (!_currentState.IsActive)
            {
                Log.Debug("Grayscale уже выключен");
                return true;
            }

            return SetColorFilterState(false, _currentState.FilterType, source ?? "AutoSwitch");
        }
    }

    /// <summary>
    /// Переключает состояние grayscale
    /// </summary>
    /// <param name="source">Источник изменения</param>
    /// <returns>true если операция успешна</returns>
    public bool ToggleGrayscale(string? source = null)
    {
        return _currentState.IsActive ? DisableGrayscale(source) : EnableGrayscale(source);
    }

    /// <summary>
    /// Устанавливает тип фильтра
    /// </summary>
    /// <param name="filterType">Тип фильтра</param>
    /// <param name="source">Источник изменения</param>
    /// <returns>true если операция успешна</returns>
    public bool SetFilterType(ColorFilterType filterType, string? source = null)
    {
        if (!_isInitialized || !IsAvailable)
            return false;

        lock (_stateLock)
        {
            return SetColorFilterState(_currentState.IsActive, filterType, source ?? "SetFilterType");
        }
    }

    /// <summary>
    /// Приостанавливает работу контроллера
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
        Log.Information("FilterController приостановлен");
        OnPropertyChanged(nameof(IsPaused));
    }

    /// <summary>
    /// Возобновляет работу контроллера
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
        Log.Information("FilterController возобновлён");
        OnPropertyChanged(nameof(IsPaused));
    }

    /// <summary>
    /// Получает текущее состояние фильтра из реестра
    /// </summary>
    public bool GetFilterState()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Constants.COLOR_FILTER_REGISTRY_PATH);
            if (key == null)
            {
                Log.Debug("Ключ реестра цветового фильтра не найден");
                return false;
            }

            var activeValue = key.GetValue(Constants.COLOR_FILTER_ACTIVE_KEY);
            bool isActive = activeValue is int intValue && intValue == 1;

            Log.Debug("Состояние фильтра из реестра: {IsActive}", isActive);
            return isActive;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при чтении состояния фильтра из реестра");
            return false;
        }
    }

    /// <summary>
    /// Устанавливает состояние цветового фильтра
    /// </summary>
    private bool SetColorFilterState(bool enable, ColorFilterType filterType, string source)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Читаем ТЕКУЩЕЕ фактическое состояние из реестра
            bool currentRegistryState = GetFilterState();
            
            Log.Information("SetColorFilterState: желаемое={Desired}, текущее в реестре={Current}",
                enable ? "ON" : "OFF", currentRegistryState ? "ON" : "OFF");

            var previousState = _currentState.Clone();

            // Проверяем, нужно ли изменение
            if (currentRegistryState == enable)
            {
                Log.Information("Состояние фильтра уже правильное, изменение не требуется");
                _currentState.IsActive = enable;
                _currentState.FilterType = filterType;
                _currentState.LastChanged = DateTime.Now;
                _currentState.Source = source;
                return true;
            }

            // Сначала устанавливаем тип фильтра в реестре
            using (var key = Registry.CurrentUser.CreateSubKey(Constants.COLOR_FILTER_REGISTRY_PATH))
            {
                if (key != null)
                {
                    key.SetValue(Constants.COLOR_FILTER_TYPE_KEY, (int)filterType, RegistryValueKind.DWord);
                    Log.Debug("Установлен тип фильтра: {Type}", (int)filterType);
                }
            }

            // Используем хоткей Win+Ctrl+C для переключения фильтра
            // Хоткей ПЕРЕКЛЮЧАЕТ состояние, поэтому нажимаем один раз
            Log.Information("Переключение фильтра через хоткей Win+Ctrl+C");
            SimulateColorFilterHotkey();
            
            // Ждём применения изменения системой (минимальная задержка)
            System.Threading.Thread.Sleep(50);
            
            // Проверяем результат
            bool newState = GetFilterState();
            Log.Information("После хоткея: ИСТИННОЕ состояние = {State} (ожидалось: {Expected})",
                newState ? "ON" : "OFF", enable ? "ON" : "OFF");
            
            // Если хоткей не сработал (редкий случай), пробуем ещё раз
            if (newState != enable)
            {
                Log.Warning("Первый хоткей не сработал, пробуем ещё раз");
                SimulateColorFilterHotkey();
                System.Threading.Thread.Sleep(50);
                newState = GetFilterState();
                Log.Information("После второго хоткея: ИСТИННОЕ состояние = {State}",
                    newState ? "ON" : "OFF");
            }

            // Обновляем внутреннее состояние
            _currentState.IsActive = newState;
            _currentState.FilterType = filterType;
            _currentState.LastChanged = DateTime.Now;
            _currentState.Source = source;

            stopwatch.Stop();

            Log.Information("Цветовой фильтр: желаемое={Desired}, ИСТИННОЕ={Actual}, Время: {Ms}мс, Источник: {Source}",
                enable ? "ON" : "OFF",
                newState ? "ON" : "OFF",
                stopwatch.ElapsedMilliseconds,
                source);

            OnFilterChanged(previousState, _currentState.Clone(), source);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при установке состояния цветового фильтра");
            OnError("Ошибка при установке фильтра", ex, "SetColorFilterState");
            return false;
        }
    }

    /// <summary>
    /// Симулирует нажатие хоткея Win+Ctrl+C для переключения цветового фильтра
    /// </summary>
    private void SimulateColorFilterHotkey()
    {
        try
        {
            // Проверяем текущее состояние в реестре
            bool currentActive = GetFilterState();
            
            // Если состояние уже соответствует желаемому, не нужно переключать
            // Этот метод вызывается после записи в реестр, поэтому состояние уже изменено
            // Но Windows не применяет изменение автоматически
            
            // Нам нужно "пнуть" систему, чтобы она перечитала реестр
            // Симулируем двойное нажатие Win+Ctrl+C: первое вернёт к старому состоянию, второе - к новому
            // Но это ненадёжно. Лучше использовать другой подход.
            
            // Альтернативный подход: записать желаемое состояние и принудительно обновить через API
            
            Log.Debug("Симуляция хоткея Win+Ctrl+C для обновления цветового фильтра");
            
            // Нажимаем Win
            WinAPI.keybd_event(WinAPI.VK_LWIN, 0, WinAPI.KEYEVENTF_KEYDOWN, nint.Zero);
            
            // Нажимаем Ctrl
            WinAPI.keybd_event(WinAPI.VK_CONTROL, 0, WinAPI.KEYEVENTF_KEYDOWN, nint.Zero);
            
            // Нажимаем C
            WinAPI.keybd_event(WinAPI.VK_C, 0, WinAPI.KEYEVENTF_KEYDOWN, nint.Zero);
            
            // Отпускаем C
            WinAPI.keybd_event(WinAPI.VK_C, 0, WinAPI.KEYEVENTF_KEYUP, nint.Zero);
            
            // Отпускаем Ctrl
            WinAPI.keybd_event(WinAPI.VK_CONTROL, 0, WinAPI.KEYEVENTF_KEYUP, nint.Zero);
            
            // Отпускаем Win
            WinAPI.keybd_event(WinAPI.VK_LWIN, 0, WinAPI.KEYEVENTF_KEYUP, nint.Zero);
            
            Log.Debug("Хоткей Win+Ctrl+C симулирован");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при симуляции хоткея");
        }
    }

    /// <summary>
    /// Синхронизирует внутреннее состояние с реестром
    /// </summary>
    public void SyncState()
    {
        LoadStateFromRegistry();
        Log.Information("Синхронизация состояния фильтра: {State}, Тип: {Type}",
            _currentState.IsActive ? "Active" : "Inactive", _currentState.FilterType);
    }

    /// <summary>
    /// Вызывает событие FilterChanged
    /// </summary>
    private void OnFilterChanged(FilterState previousState, FilterState currentState, string? source)
    {
        FilterChanged?.Invoke(this, new FilterChangedEventArgs
        {
            PreviousState = previousState,
            CurrentState = currentState,
            Source = source
        });

        OnPropertyChanged(nameof(IsGrayscaleEnabled));
        OnPropertyChanged(nameof(CurrentState));
    }

    /// <summary>
    /// Вызывает событие Error
    /// </summary>
    private void OnError(string message, Exception? exception, string? operation)
    {
        Error?.Invoke(this, new FilterErrorEventArgs
        {
            Message = message,
            Exception = exception,
            Operation = operation
        });
    }

    /// <summary>
    /// Вызывает событие PropertyChanged
    /// </summary>
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        StopMonitoring();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~FilterController()
    {
        Dispose();
    }
}
