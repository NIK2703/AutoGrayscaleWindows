using System.Runtime.InteropServices;

namespace AutoGrayscaleWindows.Native;

/// <summary>
/// Делегат для callback-функции WinEvent
/// </summary>
/// <param name="hWinEventHook">Дескриптор hook</param>
/// <param name="eventType">Тип события</param>
/// <param name="hwnd">Дескриптор окна</param>
/// <param name="idObject">ID объекта</param>
/// <param name="idChild">ID дочернего элемента</param>
/// <param name="dwEventThread">ID потока события</param>
/// <param name="dwmsEventTime">Время события</param>
public delegate void WinEventDelegate(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

/// <summary>
/// Импортируемые функции Windows API
/// </summary>
public static class WinAPI
{
    #region user32.dll functions

    /// <summary>
    /// Устанавливает hook на события Windows
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    /// <summary>
    /// Удаляет hook на события Windows
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWinEvent(nint hWinEventHook);

    /// <summary>
    /// Получает дескриптор активного окна
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint GetForegroundWindow();

    /// <summary>
    /// Получает ID процесса по дескриптору окна
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Получает текст заголовка окна
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(nint hWnd, char[] lpString, int nMaxCount);

    /// <summary>
    /// Получает длину текста окна
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowTextLength(nint hWnd);

    /// <summary>
    /// Регистрирует глобальную горячую клавишу
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>
    /// Отменяет регистрацию горячей клавиши
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    /// <summary>
    /// Получает имя класса окна
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetClassName(nint hWnd, char[] lpClassName, int nMaxCount);

    /// <summary>
    /// Отправляет сообщение окну с таймаутом (строковый параметр)
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint SendMessageTimeout(
        nint hWnd,
        int Msg,
        nint wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out nint lpdwResult);

    /// <summary>
    /// Симулирует нажатие клавиши
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nint dwExtraInfo);

    #endregion

    #region kernel32.dll functions

    /// <summary>
    /// Получает полный путь к файлу процесса
    /// </summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    /// <summary>
    /// Закрывает дескриптор объекта
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(nint hObject);

    #endregion

    #region psapi.dll functions

    /// <summary>
    /// Получает базовое имя модуля
    /// </summary>
    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetModuleBaseName(nint hProcess, nint hModule, char[] lpBaseName, int nSize);

    #endregion

    #region Process Access Rights

    /// <summary>
    /// Права доступа для получения информации о процессе
    /// </summary>
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_VM_READ = 0x0010;

    #endregion

    #region shell32.dll functions

    /// <summary>
    /// Получает хранилище свойств для окна (для UWP-приложений)
    /// </summary>
    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int SHGetPropertyStoreForWindow(nint hwnd, ref Guid riid, out nint ppv);

    #endregion

    #region keybd_event Constants

    /// <summary>
    /// Флаги для keybd_event
    /// </summary>
    public const uint KEYEVENTF_KEYDOWN = 0x0000;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// Виртуальные коды клавиш
    /// </summary>
    public const byte VK_LWIN = 0x5B;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_C = 0x43;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Получает заголовок окна по его дескриптору
    /// </summary>
    public static string GetWindowTitle(nint hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0)
            return string.Empty;

        char[] buffer = new char[length + 1];
        GetWindowText(hWnd, buffer, buffer.Length);
        return new string(buffer).TrimEnd('\0');
    }

    /// <summary>
    /// Получает имя класса окна
    /// </summary>
    public static string GetWindowClassName(nint hWnd)
    {
        char[] buffer = new char[256];
        GetClassName(hWnd, buffer, buffer.Length);
        return new string(buffer).TrimEnd('\0');
    }

    /// <summary>
    /// Получает ID процесса по дескриптору окна
    /// </summary>
    public static uint GetProcessIdFromWindow(nint hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint processId);
        return processId;
    }

    /// <summary>
    /// Получает имя процесса (без пути) через WinAPI - быстрее чем Process.GetProcessById
    /// </summary>
    public static string GetProcessBaseName(uint processId)
    {
        nint hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
        if (hProcess == nint.Zero)
            return string.Empty;

        try
        {
            char[] buffer = new char[260];
            if (GetModuleBaseName(hProcess, nint.Zero, buffer, buffer.Length))
            {
                return new string(buffer).TrimEnd('\0');
            }
            return string.Empty;
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    #endregion
}
