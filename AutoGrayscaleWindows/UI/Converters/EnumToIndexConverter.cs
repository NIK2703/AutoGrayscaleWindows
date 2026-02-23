using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoGrayscaleWindows.Models
{
    /// <summary>
    /// Конвертер для преобразования enum в индекс и обратно
    /// </summary>
    public class EnumToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum enumValue)
            {
                return System.Convert.ToInt32(enumValue);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index && targetType.IsEnum)
            {
                return Enum.ToObject(targetType, index);
            }
            return Enum.ToObject(targetType, 0);
        }
    }
}
