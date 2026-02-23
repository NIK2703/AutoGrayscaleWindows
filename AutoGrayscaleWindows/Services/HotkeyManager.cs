using System.Windows.Interop;
using AutoGrayscaleWindows.Native;
using Serilog;

namespace AutoGrayscaleWindows.Services;

/// <summary>
/// Аргументы события нажатия горячей клавиши
/// </summary>
public class HotkeyEventArgs : EventArgs
{
    /// <summary>
    /// ID зарегистрированной горячей клавиши
    /// </summary>
    public int HotkeyId { get; set; }

    /// <summary>
    /// Модификаторы нажатой клавиши
    /// </summary>
    public uint Modifiers { get; set; }

    /// <summary>
    /// Виртуальный код клавиши
    /// </summary>
    public uint VirtualKey { get; set; }
}

/// <summary>
/// Модификаторы горячих клавиш
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = Constants.MOD_ALT,
    Control = Constants.MOD_CONTROL,
    Shift = Constants.MOD_SHIFT,
    Win = Constants.MOD_WIN
}

/// <summary>
/// Менеджер глобальных горячих клавиш
/// </summary>
public class HotkeyManager : IDisposable
{
    private nint _windowHandle;
    private HwndSource? _hwndSource;
    private readonly Dictionary<int, (HotkeyModifiers Modifiers, uint VirtualKey, string Name)> _registeredHotkeys = new();
    private bool _disposed;
    private bool _isInitialized;

    /// <summary>
    /// Событие нажатия горячей клавиши
    /// </summary>
    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    /// <summary>
    /// Событие ошибки регистрации горячей клавиши
    /// </summary>
    public event EventHandler<(int Id, string Error)>? RegistrationError;

    /// <summary>
    /// Количество зарегистрированных горячих клавиш
    /// </summary>
    public int RegisteredCount => _registeredHotkeys.Count;

    /// <summary>
    /// Инициализирован ли менеджер
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Инициализирует менеджер с дескриптором окна
    /// </summary>
    /// <param name="windowHandle">Дескриптор окна</param>
    /// <returns>true при успешной инициализации</returns>
    public bool Initialize(nint windowHandle)
    {
        if (_isInitialized)
        {
            Log.Warning("HotkeyManager уже инициализирован");
            return true;
        }

        if (windowHandle == nint.Zero)
        {
            Log.Error("Передан нулевой дескриптор окна");
            return false;
        }

        _windowHandle = windowHandle;
        _hwndSource = HwndSource.FromHwnd(windowHandle);

        if (_hwndSource == null)
        {
            Log.Error("Не удалось получить HwndSource для окна");
            return false;
        }

        // Добавляем hook для обработки сообщений окна
        _hwndSource.AddHook(WndProc);
        _isInitialized = true;

        Log.Information("HotkeyManager инициализирован");
        return true;
    }

    /// <summary>
    /// Регистрирует глобальную горячую клавишу
    /// </summary>
    /// <param name="id">Уникальный идентификатор горячей клавиши</param>
    /// <param name="modifiers">Модификаторы (Alt, Ctrl, Shift, Win)</param>
    /// <param name="virtualKey">Виртуальный код клавиши</param>
    /// <param name="name">Опциональное имя для логирования</param>
    /// <returns>true при успешной регистрации</returns>
    public bool RegisterHotKey(int id, HotkeyModifiers modifiers, uint virtualKey, string? name = null)
    {
        return RegisterHotKey(id, (uint)modifiers, virtualKey, name);
    }

    /// <summary>
    /// Регистрирует глобальную горячую клавишу
    /// </summary>
    /// <param name="id">Уникальный идентификатор горячей клавиши</param>
    /// <param name="modifiers">Модификаторы (битовая маска)</param>
    /// <param name="virtualKey">Виртуальный код клавиши</param>
    /// <param name="name">Опциональное имя для логирования</param>
    /// <returns>true при успешной регистрации</returns>
    public bool RegisterHotKey(int id, uint modifiers, uint virtualKey, string? name = null)
    {
        if (!_isInitialized)
        {
            Log.Error("HotkeyManager не инициализирован. Вызовите Initialize() сначала.");
            RegistrationError?.Invoke(this, (id, "Менеджер не инициализирован"));
            return false;
        }

        if (_registeredHotkeys.ContainsKey(id))
        {
            Log.Warning("Горячая клавиша с ID {Id} уже зарегистрирована. Перезапись...", id);
            UnregisterHotKey(id);
        }

        bool result = WinAPI.RegisterHotKey(_windowHandle, id, modifiers, virtualKey);

        if (result)
        {
            var hotkeyName = name ?? GetHotkeyDisplayName((HotkeyModifiers)modifiers, virtualKey);
            _registeredHotkeys[id] = ((HotkeyModifiers)modifiers, virtualKey, hotkeyName);

            Log.Information("Горячая клавиша зарегистрирована: {Name} (ID={Id}, Modifiers={Modifiers}, VK={VK})",
                hotkeyName, id, modifiers, virtualKey);
        }
        else
        {
            int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            string errorMessage = GetErrorMessage(error);

            Log.Error("Не удалось зарегистрировать горячую клавишу ID={Id}: {Error} (код {ErrorCode})",
                id, errorMessage, error);

            RegistrationError?.Invoke(this, (id, errorMessage));
        }

        return result;
    }

    /// <summary>
    /// Пытается зарегистрировать горячую клавишу, возвращает результат без исключений
    /// </summary>
    public (bool Success, string? Error) TryRegisterHotKey(int id, HotkeyModifiers modifiers, uint virtualKey, string? name = null)
    {
        try
        {
            bool result = RegisterHotKey(id, modifiers, virtualKey, name);
            return result ? (true, null) : (false, "Не удалось зарегистрировать горячую клавишу");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Исключение при регистрации горячей клавиши ID={Id}", id);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Отменяет регистрацию горячей клавиши
    /// </summary>
    /// <param name="id">Идентификатор горячей клавиши</param>
    /// <returns>true при успешной отмене</returns>
    public bool UnregisterHotKey(int id)
    {
        if (!_isInitialized)
        {
            Log.Warning("Невозможно отменить регистрацию: HotkeyManager не инициализирован");
            return false;
        }

        if (!_registeredHotkeys.ContainsKey(id))
        {
            Log.Debug("Горячая клавиша с ID {Id} не зарегистрирована", id);
            return true;
        }

        bool result = WinAPI.UnregisterHotKey(_windowHandle, id);

        if (result)
        {
            var name = _registeredHotkeys[id].Name;
            _registeredHotkeys.Remove(id);
            Log.Information("Горячая клавиша отменена: {Name} (ID={Id})", name, id);
        }
        else
        {
            int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Log.Warning("Не удалось отменить регистрацию горячей клавиши ID={Id}. Код ошибки: {ErrorCode}",
                id, error);
        }

        return result;
    }

    /// <summary>
    /// Проверяет, зарегистрирована ли горячая клавиша с указанным ID
    /// </summary>
    public bool IsRegistered(int id)
    {
        return _registeredHotkeys.ContainsKey(id);
    }

    /// <summary>
    /// Получает информацию о зарегистрированной горячей клавише
    /// </summary>
    public (HotkeyModifiers Modifiers, uint VirtualKey, string Name)? GetHotkeyInfo(int id)
    {
        if (_registeredHotkeys.TryGetValue(id, out var info))
        {
            return info;
        }
        return null;
    }

    /// <summary>
    /// Отменяет регистрацию всех горячих клавиш
    /// </summary>
    public void UnregisterAll()
    {
        var ids = _registeredHotkeys.Keys.ToList();
        foreach (int id in ids)
        {
            UnregisterHotKey(id);
        }

        Log.Information("Все горячие клавиши отменены ({Count} шт)", ids.Count);
    }

    /// <summary>
    /// Перерегистрирует все горячие клавиши (полезно при изменении настроек)
    /// </summary>
    public void ReregisterAll()
    {
        var hotkeys = _registeredHotkeys.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Сначала отменяем все регистрации
        foreach (var id in hotkeys.Keys.ToList())
        {
            WinAPI.UnregisterHotKey(_windowHandle, id);
        }

        _registeredHotkeys.Clear();

        // Регистрируем заново
        foreach (var kvp in hotkeys)
        {
            RegisterHotKey(kvp.Key, (uint)kvp.Value.Modifiers, kvp.Value.VirtualKey, kvp.Value.Name);
        }

        Log.Information("Горячие клавиши перерегистрированы");
    }

    /// <summary>
    /// Обработчик сообщений окна
    /// </summary>
    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Constants.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();

            // Извлекаем модификаторы и виртуальный код из lParam
            uint virtualKey = ((uint)lParam & 0xFFFF);
            uint modifiers = ((uint)lParam >> 16) & 0xFFFF;

            string hotkeyName = _registeredHotkeys.TryGetValue(hotkeyId, out var info)
                ? info.Name
                : $"ID={hotkeyId}";

            Log.Debug("Нажата горячая клавиша: {Name}", hotkeyName);

            try
            {
                HotkeyPressed?.Invoke(this, new HotkeyEventArgs
                {
                    HotkeyId = hotkeyId,
                    Modifiers = modifiers,
                    VirtualKey = virtualKey
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в обработчике горячей клавиши {Name}", hotkeyName);
            }

            handled = true;
        }

        return nint.Zero;
    }

    /// <summary>
    /// Получает отображаемое имя горячей клавиши
    /// </summary>
    public static string GetHotkeyDisplayName(HotkeyModifiers modifiers, uint virtualKey)
    {
        var parts = new List<string>();

        if ((modifiers & HotkeyModifiers.Control) != 0)
            parts.Add("Ctrl");
        if ((modifiers & HotkeyModifiers.Alt) != 0)
            parts.Add("Alt");
        if ((modifiers & HotkeyModifiers.Shift) != 0)
            parts.Add("Shift");
        if ((modifiers & HotkeyModifiers.Win) != 0)
            parts.Add("Win");

        // Получаем имя клавиши
        string keyName = GetKeyName(virtualKey);
        parts.Add(keyName);

        return string.Join(" + ", parts);
    }

    /// <summary>
    /// Получает имя клавиши по виртуальному коду
    /// </summary>
    private static string GetKeyName(uint virtualKey)
    {
        return virtualKey switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "Page Up",
            0x22 => "Page Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "←",
            0x26 => "↑",
            0x27 => "→",
            0x28 => "↓",
            0x2C => "Print Screen",
            0x2D => "Insert",
            0x2E => "Delete",
            0x5B => "Left Win",
            0x5C => "Right Win",
            0x70 => "F1",
            0x71 => "F2",
            0x72 => "F3",
            0x73 => "F4",
            0x74 => "F5",
            0x75 => "F6",
            0x76 => "F7",
            0x77 => "F8",
            0x78 => "F9",
            0x79 => "F10",
            0x7A => "F11",
            0x7B => "F12",
            _ => GetCharFromVirtualKey(virtualKey)
        };
    }

    /// <summary>
    /// Получает символ из виртуального кода клавиши
    /// </summary>
    private static string GetCharFromVirtualKey(uint virtualKey)
    {
        // Буквы A-Z
        if (virtualKey >= 0x41 && virtualKey <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        // Цифры 0-9
        if (virtualKey >= 0x30 && virtualKey <= 0x39)
        {
            return ((char)virtualKey).ToString();
        }

        // Numpad цифры
        if (virtualKey >= 0x60 && virtualKey <= 0x69)
        {
            return $"Num{(virtualKey - 0x60)}";
        }

        return $"VK{virtualKey:X}";
    }

    /// <summary>
    /// Получает сообщение об ошибке по коду Windows
    /// </summary>
    private static string GetErrorMessage(int errorCode)
    {
        return errorCode switch
        {
            0 => "Успешно",
            5 => "Отказано в доступе",
            87 => "Неверный параметр",
            1409 => "Горячая клавиша уже используется другим приложением",
            _ => $"Ошибка Windows {errorCode}"
        };
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
            UnregisterAll();

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при освобождении HotkeyManager");
        }

        _isInitialized = false;
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~HotkeyManager()
    {
        Dispose();
    }
}
