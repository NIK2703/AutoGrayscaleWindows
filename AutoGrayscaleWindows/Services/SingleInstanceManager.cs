using System.Threading;
using Serilog;

namespace AutoGrayscaleWindows.Services;

/// <summary>
/// Менеджер для обеспечения единственного экземпляра приложения
/// </summary>
public class SingleInstanceManager : IDisposable
{
    private const string MutexName = "AutoGrayscaleWindows_SingleInstance_Mutex";
    private Mutex? _mutex;
    private bool _ownsMutex;
    private bool _disposed;

    /// <summary>
    /// Пытается получить эксклюзивный доступ (запустить как единственный экземпляр)
    /// </summary>
    /// <returns>true если это первый экземпляр, false если уже запущен другой</returns>
    public bool TryAcquire()
    {
        try
        {
            // Пытаемся создать или открыть существующий Mutex
            _mutex = new Mutex(true, MutexName, out _ownsMutex);

            if (!_ownsMutex)
            {
                // Mutex уже существует - другой экземпляр запущен
                Log.Warning("Обнаружен уже запущенный экземпляр приложения");
                return false;
            }

            Log.Debug("Получен Mutex для единственного экземпляра");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при попытке получить Mutex");
            return false;
        }
    }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_mutex != null && _ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // Игнорируем ошибки при освобождении
            }
        }
        
        _mutex?.Dispose();
        _mutex = null;
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~SingleInstanceManager()
    {
        Dispose();
    }
}
