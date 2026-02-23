using System.Runtime.InteropServices;

namespace AutoGrayscaleWindows.Native;

/// <summary>
/// COM-интерфейсы для работы с UWP-приложениями
/// </summary>
public static class COMInterfaces
{
    /// <summary>
    /// IID интерфейса IPropertyStore
    /// </summary>
    public static readonly Guid IID_IPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    /// <summary>
    /// PROPERTYKEY для System.AppUserModel.ID
    /// </summary>
    public static readonly PROPERTYKEY PKEY_AppUserModel_ID = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
}

/// <summary>
/// Структура ключа свойства
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;

    public PROPERTYKEY(Guid fmtid, uint pid)
    {
        this.fmtid = fmtid;
        this.pid = pid;
    }
}

/// <summary>
/// Структура значения свойства (VARIANT)
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct PROPVARIANT
{
    [FieldOffset(0)]
    public ushort vt;

    [FieldOffset(2)]
    public ushort wReserved1;

    [FieldOffset(4)]
    public ushort wReserved2;

    [FieldOffset(6)]
    public ushort wReserved3;

    [FieldOffset(8)]
    public nint pVal;

    /// <summary>
    /// Тип варианта: VT_EMPTY
    /// </summary>
    public const ushort VT_EMPTY = 0;

    /// <summary>
    /// Тип варианта: VT_LPWSTR (строка)
    /// </summary>
    public const ushort VT_LPWSTR = 31;

    /// <summary>
    /// Получает строковое значение из PROPVARIANT
    /// </summary>
    public string? GetString()
    {
        if (vt == VT_LPWSTR && pVal != nint.Zero)
        {
            return Marshal.PtrToStringUni(pVal);
        }
        return null;
    }

    /// <summary>
    /// Освобождает память, занятую PROPVARIANT
    /// </summary>
    public void Clear()
    {
        if (vt == VT_LPWSTR && pVal != nint.Zero)
        {
            Marshal.FreeCoTaskMem(pVal);
            pVal = nint.Zero;
        }
        vt = VT_EMPTY;
    }
}

/// <summary>
/// COM-интерфейс IPropertyStore для работы со свойствами окна
/// </summary>
[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyStore
{
    /// <summary>
    /// Получает количество свойств
    /// </summary>
    [PreserveSig]
    int GetCount(out uint cProps);

    /// <summary>
    /// Получает ключ свойства по индексу
    /// </summary>
    [PreserveSig]
    int GetAt(uint iProp, out PROPERTYKEY pkey);

    /// <summary>
    /// Получает значение свойства
    /// </summary>
    [PreserveSig]
    int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);

    /// <summary>
    /// Устанавливает значение свойства
    /// </summary>
    [PreserveSig]
    int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);

    /// <summary>
    /// Сохраняет изменения
    /// </summary>
    [PreserveSig]
    int Commit();
}
