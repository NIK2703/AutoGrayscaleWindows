using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AutoGrayscaleWindows.UI.Converters;

/// <summary>
/// Конвертер для преобразования булевых значений в цвета
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    /// <summary>
    /// Цвет для значения true
    /// </summary>
    public Brush TrueColor { get; set; } = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green

    /// <summary>
    /// Цвет для значения false
    /// </summary>
    public Brush FalseColor { get; set; } = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Gray

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueColor : FalseColor;
        }

        return FalseColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер для преобразования булевых значений в цвета статуса
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Проверяем параметр для инверсии
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            bool result = invert ? !boolValue : boolValue;

            return result
                ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) // Зелёный - активно
                : new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Красный - неактивно
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер для преобразования булевых значений в текст статуса
/// </summary>
public class BoolToStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            string prefix = parameter?.ToString() ?? "";
            string status = boolValue ? "ВКЛ" : "ВЫКЛ";
            return string.IsNullOrEmpty(prefix) ? status : $"{prefix}: {status}";
        }

        return "Неизвестно";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер для преобразования булевых значений в видимость
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            bool result = invert ? !boolValue : boolValue;
            return result ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Windows.Visibility visibility)
        {
            return visibility == System.Windows.Visibility.Visible;
        }
        return false;
    }
}
